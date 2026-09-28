from decimal import Decimal
import json
from pathlib import Path
import sys
import unittest

sys.path.insert(0, str(Path(__file__).parents[1] / "src"))
from idx_stock_collector.eodhd_experimental import parse_daily


class ExperimentalEodhdTests(unittest.TestCase):
    def row(self):
        return dict(date="2026-01-05", open=100, high=110, low=90,
                    close=105, adjusted_close=99.1234, volume=123)

    def test_distinct_prices_exact_decimal_and_zero_volume(self):
        row = self.row()
        row["volume"] = 0
        parsed = parse_daily(json.dumps([row]).encode())[0]
        self.assertEqual(Decimal("99.1234"), parsed["adjusted_close"])
        self.assertEqual(Decimal(105), parsed["close"])
        self.assertEqual(0, parsed["volume"])

    def test_invalid_or_missing_fields_are_rejected(self):
        for field, value in (("high", 95), ("low", 106), ("open", None),
                             ("close", "105"), ("close", True), ("close", 0),
                             ("close", float("nan")), ("close", float("inf")),
                             ("adjusted_close", None), ("volume", -1),
                             ("volume", 1.5), ("volume", True),
                             ("date", "2026-01-05T00:00:00Z")):
            row = self.row()
            row[field] = value
            with self.subTest(field=field, value=value), self.assertRaises(ValueError):
                parse_daily(json.dumps([row]).encode())
        for field in self.row():
            row = self.row()
            del row[field]
            with self.subTest(missing=field), self.assertRaises(ValueError):
                parse_daily(json.dumps([row]).encode())
        for payload in (b'{}', b'[null]', b'invalid json'):
            with self.assertRaises(ValueError):
                parse_daily(payload)

    def test_duplicates_rejected_and_normalization_deterministic_without_fill(self):
        first = self.row()
        second = dict(first, date="2026-01-07")
        with self.assertRaises(ValueError):
            parse_daily(json.dumps([first, first]).encode())
        ordered = parse_daily(json.dumps([second, first]).encode())
        self.assertEqual(ordered, parse_daily(json.dumps([first, second]).encode()))
        self.assertEqual(["2026-01-05", "2026-01-07"], [row["date"] for row in ordered])


if __name__ == "__main__":
    unittest.main()
