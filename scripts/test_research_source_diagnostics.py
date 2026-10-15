"""Source-independent loopback HTTP acceptance. No PostgreSQL, migrations or provider access.
After dotnet build: python3 -m unittest discover -s scripts -p test_research_source_diagnostics.py -v
"""
from contextlib import contextmanager
from datetime import datetime, timedelta, timezone
import json
import os
from pathlib import Path
import socket
import subprocess
import tempfile
import time
import unittest
from urllib.error import HTTPError, URLError
from urllib.request import urlopen

ROOT = Path(__file__).resolve().parents[1]
DLL = ROOT / "src/IdxStockIntelligence.Api/bin/Debug/net10.0/IdxStockIntelligence.Api.dll"
PATH = "/api/research/source-diagnostics"


@contextmanager
def api(config=None):
    with tempfile.TemporaryDirectory(prefix="idx-source-diagnostics-") as directory, tempfile.TemporaryFile() as log:
        with socket.socket() as listener:
            listener.bind(("127.0.0.1", 0))
            base = "http://127.0.0.1:" + str(listener.getsockname()[1])
        env = os.environ.copy()
        env.pop("IDX_SOURCE_USAGE_CONFIG", None)
        # Deliberately unusable, never operational. This endpoint must not open a DB connection.
        env["IDX_DATABASE_CONNECTION"] = "Host=127.0.0.1;Port=1;Database=idx_diagnostics_unused;Username=unused;Password=unused;Timeout=1"
        env["IDX_UI_ROOT"] = directory
        if config is not None:
            env["IDX_SOURCE_USAGE_CONFIG"] = config if isinstance(config, str) else json.dumps(config)
        process = subprocess.Popen(["dotnet", str(DLL), "--urls", base], cwd=directory, env=env, stdout=log, stderr=log)
        def request(suffix=""):
            try:
                response = urlopen(base + PATH + suffix, timeout=5)
            except HTTPError as error:
                response = error
            with response:
                return response.status, json.loads(response.read())
        try:
            for _ in range(100):
                if process.poll() is not None:
                    raise AssertionError("Diagnostic API exited before readiness")
                try:
                    request()
                    break
                except (URLError, ConnectionError):
                    time.sleep(.05)
            else:
                raise AssertionError("Diagnostic API startup timed out")
            yield request
        finally:
            process.terminate()
            try:
                process.wait(timeout=10)
            except subprocess.TimeoutExpired:
                process.kill()
                process.wait(timeout=5)


def fixture():
    now = datetime.now(timezone.utc)
    capabilities = ["OHLC", "INSTRUMENT_IDENTITY", "TRADING_STATUS", "SCHEDULED_SESSION", "COMPLETED_SESSION", "CORPORATE_ACTION"]
    operations = ["PERSONAL_USE", "COMMERCIAL_USE", "RETAIN_ORIGINAL", "INTERNAL_ANALYSIS", "PROCESS_DERIVED", "DISPLAY", "EXPORT", "SAAS_CONSUMPTION"]
    return {"schemaVersion": 1, "profile": "PERSONAL",
            "enabledFeatures": ["PRICE_INSPECTION", "INDICATIVE_TECHNICALS", "VERIFIED_SCREENING", "PRICE_EXPORT"],
            "sources": [{"sourceId": "authored-fixture", "scopeId": "fixture-scope", "enabled": True,
                         "capabilities": [{"capability": c, "evidenceReference": "synthetic-capability"} for c in capabilities],
                         "unresolvedRestrictions": [],
                         "usage": {"sourceId": "authored-fixture", "scopeId": "fixture-scope", "decisionId": "synthetic-grant",
                                   "termsReference": "PRIVATE-TERMS-SENTINEL", "reviewedAt": (now - timedelta(days=2)).isoformat(),
                                   "validFrom": (now - timedelta(days=1)).isoformat(), "validUntil": (now + timedelta(days=1)).isoformat(),
                                   "permissions": [{"operation": o, "permission": "EXPLICIT_PROHIBITION" if o == "EXPORT" else "EXPLICIT_PERMISSION"} for o in operations]}}],
            "selections": [{"capability": c, "sourceId": "authored-fixture", "scopeId": "fixture-scope"} for c in capabilities]}


class ResearchSourceDiagnosticsAcceptance(unittest.TestCase):
    def test_default_is_safe_and_direct_requests_cannot_inject_permission(self):
        with api() as request:
            status, report = request()
            self.assertEqual(200, status)
            self.assertEqual("INDICATIVE_DIAGNOSTICS_ONLY", report["classification"])
            self.assertFalse(report["marketDataRead"])
            self.assertFalse(report["evidenceGateSatisfied"])
            self.assertFalse(report["candidatePromotionPerformed"])
            self.assertEqual(4, len(report["features"]))
            self.assertTrue(all(f["reason"] == "FEATURE_DISABLED" for f in report["features"]))
            for query in ["?mode=0", "?mode=", "?mode=RESEARCH&mode=VERIFIED", "?permission=EXPLICIT_PERMISSION",
                          "?sourceId=idx", "?evidenceGateSatisfied=true", "?mode=RESEARCH&export=true"]:
                with self.subTest(query=query):
                    self.assertEqual((400, {"code": "RESEARCH_DIAGNOSTICS_QUERY_INVALID", "error": "RESEARCH_DIAGNOSTICS_QUERY_INVALID"}), request(query))

    def test_trusted_fixture_rights_never_create_verified_evidence_or_export_rights(self):
        with api(fixture()) as request:
            status, report = request()
            self.assertEqual(200, status)
            features = {f["feature"]: f for f in report["features"]}
            self.assertTrue(features["PriceInspection"]["usagePermitted"])
            self.assertTrue(features["IndicativeTechnicals"]["usagePermitted"])
            self.assertEqual("EXPORT_NOT_PERMITTED", features["PriceExport"]["reason"])
            status, verified = request("?mode=VERIFIED")
            self.assertEqual(200, status)
            self.assertTrue(all(not f["usagePermitted"] for f in verified["features"]))
            self.assertEqual("EVIDENCE_NOT_ADMITTED", next(f for f in verified["features"] if f["feature"] == "VerifiedScreening")["reason"])
            self.assertNotIn("PRIVATE-TERMS-SENTINEL", json.dumps(report))
            self.assertNotIn("fixture-scope", json.dumps(report))

    def test_bad_server_config_has_safe_diagnostic_and_no_default_fallback(self):
        with api('{"private-setting":"PRIVATE-CONFIG-SENTINEL"}') as request:
            self.assertEqual((503, {"code": "SOURCE_USAGE_CONFIG_INVALID", "error": "SOURCE_USAGE_CONFIG_INVALID"}), request())


if __name__ == "__main__":
    unittest.main()
