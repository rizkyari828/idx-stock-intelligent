"""Personal historical backfill: one file per provider session, holidays dropped, idempotent."""
from datetime import datetime, timezone
import io
import json
import os
from pathlib import Path
import tempfile
from types import SimpleNamespace
import unittest
from unittest.mock import patch

from idx_stock_collector.pilot import collect, main, personal_row_digest

REPO = Path(__file__).resolve().parents[3]
SESSIONS = ("2026-08-14", "2026-08-17", "2026-08-18", "2026-08-19")
HOLIDAY = "2026-08-17"
START, END = SESSIONS[0], SESSIONS[-1]
PAYLOAD = json.dumps([
    {"date": session, "open": 100.25, "high": 101.5, "low": 99.75,
     "close": 100.5, "adjusted_close": 100.5, "volume": 1234567}
    for session in SESSIONS
]).encode()
TODAY = datetime(2026, 10, 27, tzinfo=timezone.utc)


class Response(io.BytesIO):
    status = 200


class PersonalBackfillTests(unittest.TestCase):
    def setUp(self):
        self.original = Path.cwd()
        self.temporary = tempfile.TemporaryDirectory()
        os.chdir(self.temporary.name)
        Path("pilot").mkdir()
        Path("pilot/universe.json").write_text((REPO / "pilot/universe.json").read_text())
        Path("pilot/collection-policy.json").write_text('{"SAFE_EOD_CUTOFF":"19:00"}')
        Path("pilot/holidays-2026.json").write_text((REPO / "pilot/holidays-2026.json").read_text())
        Path("pilot/soak.json").write_text('{"after_market_date":"2026-09-28","required_completed_runs":10}')
        Path("pilot/sessions.json").write_text("[]")
        self.config = json.loads(Path("pilot/universe.json").read_text())

    def tearDown(self):
        os.chdir(self.original)
        self.temporary.cleanup()

    def args(self):
        return SimpleNamespace(universe="pilot/universe.json", start=START, end=END,
                               resume=None, seed=[], offline=False, daily=False,
                               personal=True, backfill=True, ingest=True, refresh=False)

    def run_once(self, now=TODAY):
        """Run a stubbed backfill; subprocess is fatal so a canonical call cannot hide."""
        calls = []

        def open_request(request, timeout):
            calls.append(request.full_url)
            if "/api/user?" in request.full_url:
                return Response(json.dumps({"subscriptionType": "free", "dailyRateLimit": 20,
                                            "apiRequests": 0, "apiRequestsDate": "2026-10-27",
                                            "extraLimit": 0}).encode())
            return Response(PAYLOAD)

        with patch("idx_stock_collector.pilot.datetime") as clock, \
             patch("urllib.request.build_opener") as opener, \
             patch("subprocess.run", side_effect=AssertionError("the worker must not run")), \
             patch.dict(os.environ, {"EODHD_API_TOKEN": "fixture-only-sentinel"}):
            clock.now.return_value = now
            clock.fromisoformat.side_effect = datetime.fromisoformat
            opener.return_value.open.side_effect = open_request
            path = collect(self.args())
        return json.loads(path.read_text()), calls

    def test_range_accepted_holiday_rows_dropped_and_one_file_per_session(self):
        report, calls = self.run_once()
        self.assertEqual("PERSONAL_BACKFILL", report["mode"])
        self.assertEqual("SUCCEEDED", report["status"])
        self.assertEqual(11, report["reserved_units"])
        self.assertEqual([], report["collection_eligibility"])
        self.assertEqual(11, sum("/api/eod/" in url for url in calls))
        store = report["personal_backfill"]
        self.assertEqual("COMPLETE", store["status"])
        self.assertEqual(3, store["sessions_found"])
        self.assertEqual(3, store["sessions_written"])
        self.assertEqual(0, store["sessions_unchanged"])
        self.assertEqual(33, store["rows_written"])
        self.assertEqual(11, store["rows_dropped_holiday"])
        self.assertEqual([HOLIDAY], store["holiday_dates_dropped"])
        self.assertEqual(11, store["units_reserved"])
        self.assertEqual(["2026-08-14", "2026-08-18", "2026-08-19"],
                         [item["session_date"] for item in store["files"]])
        self.assertEqual(["WRITTEN"] * 3, [item["status"] for item in store["files"]])
        self.assertFalse(Path(f"data/personal/bars/2026/{HOLIDAY}.json").exists())
        symbols = sorted(item["symbol"] for item in self.config["instruments"])
        for session in ("2026-08-14", "2026-08-18", "2026-08-19"):
            path = Path(f"data/personal/bars/2026/{session}.json")
            self.assertTrue(path.is_file())
            document = json.loads(path.read_text())
            self.assertEqual({"schema_version", "session_date", "captured_at", "session_proof",
                              "source_id", "rows"}, set(document))
            self.assertEqual(session, document["session_date"])
            self.assertEqual("UNPROVEN_PERSONAL", document["session_proof"])
            self.assertEqual("eodhd", document["source_id"])
            self.assertEqual(symbols, sorted(row["symbol"] for row in document["rows"]))
            self.assertEqual(11, len(document["rows"]))
            for row in document["rows"]:
                self.assertEqual({"symbol", "instrument_id", "open", "high", "low", "close",
                                  "adjusted_close", "volume", "content_sha256", "artifact_sha256",
                                  "fetched_at", "quality_status"}, set(row))
                self.assertEqual("UNPROVEN_PERSONAL", row["quality_status"])
                self.assertIsInstance(row["volume"], int)
                for field in ("open", "high", "low", "close", "adjusted_close"):
                    self.assertIsInstance(row[field], str)
                self.assertEqual(personal_row_digest(session, row), row["content_sha256"])
        self.assertEqual([], list(Path("data/collector-output/pilot").glob("*.operation.json")))
        self.assertEqual([], list(Path("data/collector-output/pilot").glob("*.summary.json")))

    def test_identical_rerun_is_unchanged_and_still_reserves_units(self):
        first, _ = self.run_once()
        first_bytes = {item["session_date"]: Path(item["path"]).read_bytes()
                       for item in first["personal_backfill"]["files"]}
        later = datetime(2026, 10, 27, 9, 30, tzinfo=timezone.utc)
        second, _ = self.run_once(later)
        store = second["personal_backfill"]
        self.assertEqual(11, second["reserved_units"])
        self.assertEqual(0, store["sessions_written"])
        self.assertEqual(3, store["sessions_unchanged"])
        self.assertEqual(0, store["rows_written"])
        self.assertEqual(11, store["units_reserved"])
        self.assertIn("still reserves the same units", store["idempotency"])
        self.assertEqual(["UNCHANGED"] * 3, [item["status"] for item in store["files"]])
        for session, retained in first_bytes.items():
            path = Path(f"data/personal/bars/2026/{session}.json")
            self.assertEqual(retained, path.read_bytes())
            self.assertEqual(1, len(list(path.parent.glob(f"{session}*.json"))))

    def test_invalid_flag_combinations_are_refused_before_any_request(self):
        cases = [
            ["pilot", "--backfill", "--from", START, "--to", END, "--ingest"],
            ["pilot", "--personal", "--backfill", "--from", START, "--ingest"],
            ["pilot", "--personal", "--backfill", "--from", START, "--to", START, "--ingest"],
            ["pilot", "--personal", "--backfill", "--from", "2026-10-26", "--to", "2026-10-27", "--ingest"],
            ["pilot", "--personal", "--backfill", "--from", "2026-10-27", "--to", "2026-10-28", "--ingest"],
            ["pilot", "--personal", "--backfill", "--from", "2025-01-01", "--to", "2026-01-02", "--ingest"],
            ["pilot", "--personal", "--backfill", "--from", "2025-12-15", "--to", "2026-01-02", "--ingest"],
            ["pilot", "--personal", "--backfill", "--daily", "--from", START, "--to", END],
            ["pilot", "--personal", "--backfill", "--from", START, "--to", END, "--dry-run"],
            ["pilot", "--personal", "--backfill", "--from", START, "--to", END, "--offline"],
        ]
        for argv in cases:
            with patch("idx_stock_collector.pilot.datetime") as clock, \
                 patch("urllib.request.build_opener",
                       side_effect=AssertionError("no provider request")), \
                 patch("subprocess.run", side_effect=AssertionError("no worker")), \
                 patch("sys.argv", argv), \
                 patch.dict(os.environ, {"EODHD_API_TOKEN": "fixture-only-sentinel"}):
                clock.now.return_value = TODAY
                clock.fromisoformat.side_effect = datetime.fromisoformat
                with self.assertRaises(SystemExit) as exit_status:
                    main()
            self.assertIn(str(exit_status.exception), (
                "STOP: invalid local planning configuration.",
                "STOP: invalid configuration, evidence, entitlement or request. No automatic retry."))
        with patch("idx_stock_collector.pilot.datetime") as clock, \
             patch("urllib.request.build_opener",
                   side_effect=AssertionError("no provider request")), \
             patch("subprocess.run", side_effect=AssertionError("no worker")):
            clock.now.return_value = TODAY
            clock.fromisoformat.side_effect = datetime.fromisoformat
            with self.assertRaises(ValueError) as missing_personal:
                collect(SimpleNamespace(universe="pilot/universe.json", start=START, end=END,
                                        resume=None, seed=[], offline=False, daily=False,
                                        personal=False, backfill=True, ingest=True, refresh=False))
        self.assertIn("--personal", str(missing_personal.exception))
        self.assertFalse(Path("data/personal").exists())
        self.assertFalse(Path("data/raw").exists())

    def test_main_prints_backfill_summary_without_canonical_ingestion(self):
        def open_request(request, timeout):
            if "/api/user?" in request.full_url:
                return Response(json.dumps({"subscriptionType": "free", "dailyRateLimit": 20,
                                            "apiRequests": 0, "apiRequestsDate": "2026-10-27",
                                            "extraLimit": 0}).encode())
            return Response(PAYLOAD)

        with patch("idx_stock_collector.pilot.datetime") as clock, \
             patch("urllib.request.build_opener") as opener, \
             patch("subprocess.run", side_effect=AssertionError("the worker must not run")), \
             patch("sys.argv", ["pilot", "--from", START, "--to", END,
                                "--personal", "--backfill", "--ingest"]), \
             patch.dict(os.environ, {"EODHD_API_TOKEN": "fixture-only-sentinel"}), \
             patch("sys.stdout", new_callable=io.StringIO) as output:
            clock.now.return_value = TODAY
            clock.fromisoformat.side_effect = datetime.fromisoformat
            opener.return_value.open.side_effect = open_request
            with self.assertRaises(SystemExit) as exit_status:
                main()
        self.assertEqual(0, exit_status.exception.code)
        printed = output.getvalue()
        self.assertIn("PERSONAL-BACKFILL: COMPLETE sessions written 3, sessions unchanged 0, "
                      "rows written 33, rows dropped as cited holidays 11, units reserved 11", printed)
        self.assertIn("CANONICAL-INGESTION: SKIPPED_BACKFILL", printed)
        self.assertIn("IDEMPOTENCY: re-running the same range writes nothing new", printed)
        self.assertIn('"canonical_ingestion": "SKIPPED_BACKFILL"', printed)
        self.assertEqual([], list(Path("data/collector-output/pilot").glob("*.operation.json")))


if __name__ == "__main__":
    unittest.main()
