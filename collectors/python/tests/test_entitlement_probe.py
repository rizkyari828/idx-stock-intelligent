from datetime import date, datetime, timezone
import inspect
import json
from unittest import TestCase
from unittest.mock import patch

from idx_stock_collector import entitlement_probe as probe

DAY = date(2026, 9, 25)
NOW = datetime(2026, 9, 29, 12, tzinfo=timezone.utc)


def account(used=16, extra=500):
    return json.dumps(dict(subscriptionType="free", dailyRateLimit=20,
                           apiRequests=used, apiRequestsDate="2026-09-29", extraLimit=extra,
                           apiToken="SECRET", identity="PRIVATE")).encode()


def eod():
    return json.dumps([dict(date=str(DAY), open=100, high=110, low=90,
                           close=105, adjusted_close=104, volume=123)]).encode()


class EntitlementProbeTests(TestCase):
    def run_probe(self, after=account(20, 497), payload=None):
        calls = []
        def request(path, parameters):
            calls.append((path, parameters))
            return (account() if len(calls) == 1 else after) if path == "user" else eod() if payload is None else payload
        result = probe.execute(request, DAY, clock=lambda: NOW)
        return result, calls

    def test_expected_plans(self):
        self.assertEqual(probe.plan(16, 20, 500)["total_billable_requests"], 7)
        self.assertEqual(probe.plan(16, 20, 500)["expected_extra_after"], 497)
        self.assertEqual(probe.plan(20, 20, 500)["total_billable_requests"], 3)

    def test_unknown_insufficient_and_over_cap_fail_closed(self):
        for args in [(5, 20, 500), (16, 20, 2), (None, 20, 500), (16, None, 500),
                     (16, 20, None), (16, 20, 500, 7), (True, 20, 500), (16, 100000, 500)]:
            with self.subTest(args=args), self.assertRaises(ValueError):
                probe.plan(*args)

    def test_twelve_requires_explicit_approval(self):
        with self.assertRaises(ValueError):
            probe.plan(11, 20, 500, cap=12)
        self.assertEqual(probe.plan(11, 20, 500, cap=12, approved=True)["total_billable_requests"], 12)
        with self.assertRaises(ValueError):
            probe.plan(10, 20, 500, cap=12, approved=True)

    def test_correct_counters_verified_with_capped_or_total_usage(self):
        for used in (20, 23):
            (verdict, details), calls = self.run_probe(after=account(used, 497))
            self.assertEqual(verdict, probe.VERIFIED)
            self.assertEqual(details["billable_attempts"], 7)
            self.assertEqual(len(calls), 9)
            self.assertTrue(all(p == "user" or p in ["eod/" + s for s in probe.SYMBOLS] for p, _ in calls))
            for path, parameters in calls:
                if path != "user":
                    self.assertEqual(parameters["from"], parameters["to"])
            self.assertNotIn("SECRET", json.dumps(details))
            self.assertNotIn("PRIVATE", json.dumps(details))

    def test_counter_mismatch_not_verified(self):
        for after in (account(20, 498), account(19, 497), account(24, 497), b'{}'):
            (verdict, _), _ = self.run_probe(after=after)
            self.assertEqual(verdict, probe.NOT_VERIFIED)

    def test_non_eod_and_malformed_payloads_stop_without_retry(self):
        for payload in (b'{}', b'[]', b'invalid', b'[{"warning":"entitlement"}]',
                        eod().replace(b'110', b'95'), eod().replace(b'2026-09-25', b'2026-09-24')):
            (verdict, details), calls = self.run_probe(payload=payload)
            self.assertEqual(verdict, probe.NOT_VERIFIED)
            self.assertEqual(details["billable_attempts"], 1)
            self.assertEqual(len(calls), 3)

    def test_unknown_or_stale_account_blocks_before_eod(self):
        for payload in (b'{}', account().replace(b'2026-09-29', b'2026-09-28'),
                        account().replace(b'"free"', b'"paid"')):
            calls = []
            def request(path, parameters):
                calls.append(path)
                return payload
            verdict, _ = probe.execute(request, DAY, clock=lambda: NOW)
            self.assertEqual(verdict, probe.NOT_ELIGIBLE)
            self.assertEqual(calls, ["user"])

    def test_network_failure_is_not_retried_or_exposed(self):
        calls = []
        def request(path, parameters):
            calls.append(path)
            if path == "user":
                return account()
            raise OSError("SECRET token in failed URL")
        verdict, details = probe.execute(request, DAY, clock=lambda: NOW)
        self.assertEqual(verdict, probe.NOT_VERIFIED)
        self.assertEqual(len(calls), 3)
        self.assertNotIn("SECRET", json.dumps(details))

    def test_quota_reset_stops_without_billable_request(self):
        calls = []
        clocks = iter([NOW, NOW.replace(day=30), NOW.replace(day=30)])
        def request(path, parameters):
            calls.append(path)
            return account()
        verdict, details = probe.execute(request, DAY, clock=lambda: next(clocks))
        self.assertEqual(verdict, probe.NOT_VERIFIED)
        self.assertEqual(details["billable_attempts"], 0)
        self.assertEqual(calls, ["user", "user"])

    def test_dry_run_cannot_call_network_or_production_path(self):
        with patch.object(probe, "transport", side_effect=AssertionError("network")), \
             patch("sys.argv", ["probe", "--dry-run", "--daily-used", "16", "--daily-limit", "20", "--extra-balance", "500"]), \
             patch("builtins.print"):
            with self.assertRaises(SystemExit) as exit:
                probe.main()
            self.assertEqual(exit.exception.code, 0)
        # No pipeline dependency can hide an archive/DB/ledger side effect.
        imports = [line for line in inspect.getsource(probe).splitlines() if line.startswith(("import ", "from "))]
        self.assertFalse(any("idx_stock_collector" in line for line in imports))
        with patch("builtins.open", side_effect=AssertionError("file write")), \
             patch("subprocess.run", side_effect=AssertionError("ingestion")), \
             patch("idx_stock_collector.contract.archive_payload", side_effect=AssertionError("archive")), \
             patch("idx_stock_collector.pilot.normalized", side_effect=AssertionError("normalize")), \
             patch("idx_stock_collector.pilot.ingest_and_summarize", side_effect=AssertionError("ingest")):
            (verdict, _), _ = self.run_probe()
            self.assertEqual(verdict, probe.VERIFIED)
