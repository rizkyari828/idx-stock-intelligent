from datetime import date, datetime, timezone
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch
from types import SimpleNamespace
import os
import io

from idx_stock_collector.contract import archive_payload
from idx_stock_collector.pilot import collect, normalized, remaining, read_seed, window


class PilotTests(unittest.TestCase):
    def test_quota_and_completed_recent_dates(self):
        self.assertEqual(3, remaining(dict(subscriptionType="free", dailyRateLimit=20, apiRequests=13), 16))
        self.assertEqual(0, remaining(dict(subscriptionType="free", dailyRateLimit=20, apiRequests=20), 16))
        self.assertEqual(16, remaining(dict(subscriptionType="free",dailyRateLimit=20,apiRequests=16,apiRequestsDate="2026-09-28"),16,date(2026,9,29)))
        with self.assertRaises(ValueError):
            remaining(dict(subscriptionType="paid", dailyRateLimit=100, apiRequests=0), 16)
        with self.assertRaises(ValueError):
            window("2022-01-01", "2026-09-25", date(2026,9,28))
        with self.assertRaises(ValueError):
            window("2026-09-28", "2026-09-28", date(2026,9,28))

    def test_raw_exact_volume_adjusted_close_and_seed_integrity(self):
        payload = b'[{"date":"2026-09-25","open":100,"high":101,"low":99,"close":100,"adjusted_close":50,"volume":12345}]'
        first = last = date(2026,9,25)
        rows = normalized(payload,first,last)
        self.assertEqual(12345,rows[0]["volume"])
        self.assertEqual("50",rows[0]["adjusted_close"])
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            manifest = archive_payload(payload,root,source_id="eodhd",requested_uri="https://eodhd.com/api/eod/BBCA.JK",parser_version="fixture",extension=".json",request_parameters={"from":"2026-09-25","to":"2026-09-25"})
            path = root / "manifest.json"
            path.write_text(json.dumps(manifest))
            self.assertEqual(rows,read_seed(path,root,first,last)["rows"])
            (root / manifest["artifact"]["relative_uri"]).write_bytes(b"corrupt")
            with self.assertRaises(ValueError):
                read_seed(path,root,first,last)
        with self.assertRaises(ValueError):
            normalized(payload,date(2026,9,23),date(2026,9,24))

    def test_account_ceiling_archival_and_credential_response_guard(self):
        configuration = json.loads((Path(__file__).resolve().parents[3] / "pilot/universe.json").read_text())
        class Response(io.BytesIO):
            status = 200
        for unsafe in (False, True):
            used = 13
            def open_request(request, timeout):
                nonlocal used
                if "/user?" in request.full_url:
                    return Response(json.dumps(dict(subscriptionType="free",dailyRateLimit=20,apiRequests=used,apiRequestsDate="2026-09-28")).encode())
                used += 1
                if unsafe:
                    return Response(b"test-only-credential-sentinel")
                return Response(b'[{"date":"2026-09-25","open":100,"high":101,"low":99,"close":100,"adjusted_close":50,"volume":12345}]')
            with tempfile.TemporaryDirectory() as directory:
                original = Path.cwd()
                try:
                    os.chdir(directory)
                    Path("universe.json").write_text(json.dumps(configuration))
                    args = SimpleNamespace(universe="universe.json",start="2026-08-17",end="2026-09-25",resume=None,seed=[],offline=False)
                    with patch.dict(os.environ,{"EODHD_API_TOKEN":"test-only-credential-sentinel"}), patch("urllib.request.build_opener") as opener, patch("idx_stock_collector.pilot.datetime") as clock:
                        clock.now.return_value = datetime(2026,9,28,tzinfo=timezone.utc)
                        opener.return_value.open.side_effect = open_request
                        result = collect(args)
                    report = json.loads(result.read_text())
                    self.assertEqual(1 if unsafe else 3,report["reserved_units"])
                    self.assertNotIn("test-only-credential-sentinel",result.read_text())
                    self.assertEqual(11,len(report["entries"]))
                    self.assertEqual(0 if unsafe else 1,len(list(Path("data/raw/pilot").rglob("*.json"))))
                    self.assertEqual("SOURCE_ERROR",report["entries"][-1]["status"])
                finally:
                    os.chdir(original)


if __name__ == "__main__":
    unittest.main()
