from pathlib import Path
import sys
import unittest

COLLECTOR_ROOT = Path(__file__).parents[1]
sys.path.insert(0, str(COLLECTOR_ROOT / "src"))

from idx_stock_collector.ksei_master import EXPECTED_COLUMNS, inspect_text  # noqa: E402


def row(*, code="BBCA", isin="ID1000109507", kind="EQUITY", listed="31-MAY-2000"):
    values = dict.fromkeys(EXPECTED_COLUMNS, "")
    values.update({
        "Date": "31-AUG-2026", "Code": code, "Description": "EXAMPLE",
        "Type": kind, "Isin Code": isin, "Issuer": "EXAMPLE Tbk, PT",
        "Status": "ACTIVE", "Stock Exchange": "IDX", "Listing Date": listed,
    })
    return "|".join(values[column] for column in EXPECTED_COLUMNS)


def snapshot(*rows):
    return "|".join(EXPECTED_COLUMNS) + "\n" + "\n".join(rows) + "\n"


class KseiMasterTests(unittest.TestCase):
    def test_equity_candidate_and_non_equity_separation(self):
        report = inspect_text(snapshot(
            row(), row(code="BBCA-W", isin="ID4000000001", kind="WARRANT")
        ))
        self.assertEqual(1, len(report.equity_candidates))
        self.assertEqual("2000-05-31", report.equity_candidates[0].listing_date)
        self.assertEqual({"WARRANT": 1}, report.unsupported_types)

    def test_duplicate_equity_identity_is_rejected(self):
        with self.assertRaisesRegex(ValueError, "duplicate"):
            inspect_text(snapshot(row(), row(code="NEWC", isin="ID1000109507")))

    def test_missing_listing_date_stays_unknown(self):
        report = inspect_text(snapshot(row(listed="")))
        self.assertIsNone(report.equity_candidates[0].listing_date)
        self.assertEqual(1, report.missing_equity_listing_dates)

    def test_invalid_listing_date_is_rejected(self):
        with self.assertRaises(ValueError):
            inspect_text(snapshot(row(listed="31-XYZ-2026")))

    def test_schema_drift_is_rejected(self):
        with self.assertRaisesRegex(ValueError, "schema drift"):
            inspect_text("Date|Code|Changed\n")

    def test_broken_record_is_reported_not_silently_normalized(self):
        report = inspect_text(snapshot(row(), "31-AUG-2026|BROKEN"))
        self.assertEqual((3,), report.malformed_lines)
        self.assertEqual(1, report.total_records)


if __name__ == "__main__":
    unittest.main()
