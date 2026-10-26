from datetime import date
import io
import json
from pathlib import Path
import tempfile
import unittest
from contextlib import redirect_stderr, redirect_stdout

from idx_stock_collector import pilot_auto

ROOT = Path(__file__).resolve().parents[3]
FIXTURE = ROOT / "pilot" / "holidays-2026.json"


def cited(dates: list[str]) -> dict[date, dict]:
    return {date.fromisoformat(day): {"date": day, "name": "fixture",
                                      "reference": "https://example.test/calendar",
                                      "known_at": "2026-01-01T00:00:00Z"} for day in dates}


class LatestTradingDayTests(unittest.TestCase):
    def test_normal_weekday_selects_previous_friday(self):
        self.assertEqual(date(2026, 10, 23), pilot_auto.latest_trading_day(date(2026, 10, 26), {}, 2026))

    def test_saturday_selects_previous_friday(self):
        self.assertEqual(date(2026, 10, 23), pilot_auto.latest_trading_day(date(2026, 10, 24), {}, 2026))

    def test_sunday_selects_previous_friday(self):
        self.assertEqual(date(2026, 10, 23), pilot_auto.latest_trading_day(date(2026, 10, 25), {}, 2026))

    def test_holiday_gap_is_skipped(self):
        holidays = cited(["2026-01-16", "2026-01-19"])
        self.assertEqual(date(2026, 1, 15), pilot_auto.latest_trading_day(date(2026, 1, 20), holidays, 2026))

    def test_empty_holiday_list_still_skips_weekend(self):
        self.assertEqual(date(2026, 1, 2), pilot_auto.latest_trading_day(date(2026, 1, 5), {}, 2026))

    def test_coverage_boundary_fails_closed(self):
        with self.assertRaises(pilot_auto.CalendarError):
            pilot_auto.latest_trading_day(date(2026, 1, 1), {}, 2026)

    def test_no_trading_day_inside_lookback_fails_closed(self):
        with self.assertRaises(pilot_auto.CalendarError):
            pilot_auto.latest_trading_day(date(2026, 1, 7), cited(["2026-01-05", "2026-01-06"]), 2026, max_lookback=2)


class ExplicitOverrideTests(unittest.TestCase):
    def test_holiday_override_is_refused(self):
        with self.assertRaisesRegex(pilot_auto.CalendarError, "listed exchange holiday"):
            pilot_auto.require_trading_day(date(2026, 10, 23), cited(["2026-10-23"]), 2026)

    def test_weekend_override_is_refused(self):
        with self.assertRaisesRegex(pilot_auto.CalendarError, "weekend"):
            pilot_auto.require_trading_day(date(2026, 10, 24), {}, 2026)

    def test_trading_day_override_is_accepted(self):
        plan = pilot_auto.build_plan(date(2026, 10, 26), {}, 2026, explicit=date(2026, 10, 22))
        self.assertEqual("2026-10-22", plan["date"])
        self.assertEqual("EXPLICIT_OVERRIDE", plan["selection"])
        self.assertFalse(plan["refused"])

    def test_out_of_coverage_override_is_refused(self):
        with self.assertRaises(pilot_auto.CalendarError):
            pilot_auto.require_trading_day(date(2025, 12, 31), {}, 2026)


class JournalGuardTests(unittest.TestCase):
    def test_second_automated_run_for_same_date_is_refused(self):
        journal = {"date": "2026-10-23", "status": "FINISHED", "exit_code": 0,
                   "recorded_at": "2026-10-26T08:00:00+00:00"}
        plan = pilot_auto.build_plan(date(2026, 10, 26), {}, 2026, journal=journal)
        self.assertTrue(plan["refused"])
        self.assertEqual("AUTOMATED_DATE_ALREADY_ATTEMPTED", plan["refusal_reason"])
        self.assertEqual({"date", "status", "exit_code", "recorded_at"}, set(plan["prior_attempt"]))
        self.assertIn("--date 2026-10-23", plan["override_command"])

    def test_override_bypasses_the_repeat_guard_for_the_same_date(self):
        journal = {"date": "2026-10-23", "status": "FINISHED", "exit_code": 0}
        plan = pilot_auto.build_plan(date(2026, 10, 26), {}, 2026,
                                     explicit=date(2026, 10, 23), journal=journal)
        self.assertFalse(plan["refused"])
        self.assertIsNotNone(plan["prior_attempt"])

    def test_a_different_date_is_not_refused(self):
        journal = {"date": "2026-10-22", "status": "FINISHED", "exit_code": 0}
        plan = pilot_auto.build_plan(date(2026, 10, 26), {}, 2026, journal=journal)
        self.assertFalse(plan["refused"])
        self.assertIsNone(plan["prior_attempt"])

    def test_journal_round_trip_never_touches_the_default_path(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "nested" / "journal.json"
            self.assertIsNone(pilot_auto.read_journal(path))
            pilot_auto.write_journal(date(2026, 10, 23), "STARTED", path=path)
            self.assertEqual("STARTED", pilot_auto.read_journal(path)["status"])
            pilot_auto.write_journal(date(2026, 10, 23), "FINISHED", 0, path=path)
            self.assertEqual(0, pilot_auto.read_journal(path)["exit_code"])
            path.write_text("not json")
            self.assertIsNone(pilot_auto.read_journal(path))


class HolidayFixtureTests(unittest.TestCase):
    def test_retained_fixture_is_complete_and_cited(self):
        year, holidays = pilot_auto.load_holidays(FIXTURE)
        document = json.loads(FIXTURE.read_text())
        self.assertEqual(2026, year)
        self.assertEqual(22, len(holidays))
        self.assertEqual(239, document["trading_days"])
        self.assertEqual({"Media Asuransi News", "Kontan"},
                         {source["publisher"] for source in document["sources"]})
        for day, entry in holidays.items():
            self.assertLess(day.weekday(), 5)
            self.assertTrue(entry["reference"].startswith("https://"))
            self.assertTrue(entry["known_at"])

    def test_weekend_entry_is_rejected(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "calendar.json"
            path.write_text(json.dumps({"year": 2026, "holidays": [
                {"date": "2026-10-24", "reference": "https://example.test/x",
                 "known_at": "2026-01-01T00:00:00Z"}]}))
            with self.assertRaisesRegex(pilot_auto.CalendarError, "weekend"):
                pilot_auto.load_holidays(path)

    def test_uncited_entry_is_rejected(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "calendar.json"
            path.write_text(json.dumps({"year": 2026, "holidays": [
                {"date": "2026-10-23", "reference": "http://example.test/x",
                 "known_at": "2026-01-01T00:00:00Z"}]}))
            with self.assertRaisesRegex(pilot_auto.CalendarError, "source URL"):
                pilot_auto.load_holidays(path)

    def test_duplicate_entry_is_rejected(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "calendar.json"
            entry = {"date": "2026-10-23", "reference": "https://example.test/x",
                     "known_at": "2026-01-01T00:00:00Z"}
            path.write_text(json.dumps({"year": 2026, "holidays": [entry, dict(entry)]}))
            with self.assertRaisesRegex(pilot_auto.CalendarError, "listed twice"):
                pilot_auto.load_holidays(path)


class CommandLineTests(unittest.TestCase):
    def test_plan_prints_one_date_and_zero_provider_requests(self):
        with tempfile.TemporaryDirectory() as directory:
            journal = Path(directory) / "journal.json"
            output = io.StringIO()
            with redirect_stdout(output):
                status = pilot_auto.main(["plan", "--today", "2026-10-26", "--holidays", str(FIXTURE),
                                          "--journal", str(journal)])
            plan = json.loads(output.getvalue())
            self.assertEqual(0, status)
            self.assertEqual("2026-10-23", plan["date"])
            self.assertEqual(0, plan["provider_requests"])
            self.assertEqual(22, plan["holiday_count"])
            self.assertFalse(plan["refused"])

    def test_plan_refuses_a_cited_holiday_override(self):
        with tempfile.TemporaryDirectory() as directory:
            journal = Path(directory) / "journal.json"
            path = Path(directory) / "calendar.json"
            path.write_text(json.dumps({"year": 2026, "holidays": [
                {"date": "2026-10-23", "reference": "https://example.test/x",
                 "known_at": "2026-01-01T00:00:00Z"}]}))
            errors = io.StringIO()
            with redirect_stdout(io.StringIO()), redirect_stderr(errors):
                status = pilot_auto.main(["plan", "--today", "2026-10-26", "--date", "2026-10-23",
                                          "--holidays", str(path), "--journal", str(journal)])
            self.assertEqual(2, status)
            self.assertIn("STOP:", errors.getvalue())


if __name__ == "__main__":
    unittest.main()
