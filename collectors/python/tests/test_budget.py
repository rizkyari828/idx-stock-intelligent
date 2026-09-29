import unittest

from idx_stock_collector.budget import preflight


class BudgetTests(unittest.TestCase):
    def test_panel_sizes_and_benchmark(self):
        for size in (100, 250, 500, 900, 924):
            for benchmark in (False, True):
                result = preflight(size, benchmark, 20, 2000, 2000)
                self.assertEqual(result["maximum_units"], size + int(benchmark))
                self.assertEqual(result["extra_consumption"], size + int(benchmark) - 20)
                self.assertEqual(result["status"], "ELIGIBLE")

    def test_ceiling_reserve_and_daily_remaining(self):
        self.assertEqual(preflight(924, True, 7, 1000, 925, 82)["status"], "ELIGIBLE")
        self.assertEqual(preflight(924, True, 7, 1000, 925, 83)["status"], "BUDGET_BLOCKED")
        self.assertEqual(preflight(924, True, 20, 1000, 924)["status"], "BUDGET_BLOCKED")
        self.assertEqual(preflight(50, True, 20, 0, 51)["status"], "BUDGET_BLOCKED")
        self.assertEqual(preflight(1, False, 20, 0, 1, 1)["status"], "BUDGET_BLOCKED")

    def test_retries_reserve_all_attempts(self):
        result = preflight(924, True, 20, 2000, 1850, retries=1)
        self.assertEqual(result["maximum_units"], 1850)
        self.assertEqual(result["extra_consumption"], 1830)
        self.assertEqual(preflight(924, True, 20, 2000, 925, retries=1)["status"], "BUDGET_BLOCKED")

    def test_invalid_or_unknown_counters_fail_closed(self):
        for value in (None, -1, True, 1.5):
            with self.assertRaises(ValueError):
                preflight(900, True, value, 1000, 1000)
