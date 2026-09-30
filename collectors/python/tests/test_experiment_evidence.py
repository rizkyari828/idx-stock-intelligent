import ast
from datetime import date, datetime, timezone
from hashlib import sha256
import inspect
import json
import os
from pathlib import Path
import subprocess
import tempfile
import unittest
from unittest.mock import patch

from idx_stock_collector import experiment_evidence


class ExperimentEvidenceTests(unittest.TestCase):
    day = date(2026, 9, 25)
    retrieved = datetime(2026, 9, 30, 7, 10, tzinfo=timezone.utc)

    def payload(self, **extra):
        row = dict(date=self.day.isoformat(), open=100, high=110, low=90,
                   close=105, adjusted_close=99.1234, volume=0, **extra)
        return json.dumps([row]).encode()

    def test_ohlcv_hash_and_credential_allowlist(self):
        secret = "credential-must-never-appear"
        payload = self.payload(api_token=secret, unrelated_metadata=secret)
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            with patch("subprocess.run", side_effect=AssertionError("No subprocess")), \
                    patch("urllib.request.urlopen", side_effect=AssertionError("No network")):
                path = experiment_evidence.write_evidence(
                    "bounded-eod", "ALKA.JK", self.day, self.retrieved, 200, payload,
                    repository_root=root)
            record = json.loads(path.read_text())
            self.assertEqual(("100", "110", "90", "105", "99.1234", 0),
                             tuple(record[k] for k in ("open", "high", "low", "close", "adjusted_close", "volume")))
            self.assertEqual(("NONCANONICAL", "EXPERIMENT_ONLY", 1, True, len(payload)),
                             tuple(record[k] for k in ("scope", "purpose", "row_count", "payload_valid", "response_bytes")))
            self.assertEqual(sha256(payload).hexdigest(), record["content_hash"])
            self.assertEqual(record, experiment_evidence.evidence_record(
                "ALKA.JK", self.day, self.retrieved, 200, payload))
            self.assertNotIn(secret, path.read_text())
            self.assertEqual(path, experiment_evidence.write_evidence(
                "bounded-eod", "ALKA.JK", self.day, self.retrieved, 200, payload,
                repository_root=root))
            self.assertEqual([path], [p.resolve() for p in root.rglob("*.json")])
            self.assertTrue(path.is_relative_to(root.resolve() / "data/collector-output/experiments"))
            self.assertFalse((root / "data/raw").exists())
            self.assertFalse((root / "data/collector-output/pilot").exists())

    def test_warning_failure_and_account_cannot_be_persisted(self):
        warning = b'[{"warning":"secret text"}]'
        record = experiment_evidence.evidence_record("ALKA.JK", self.day, self.retrieved, 200, warning)
        self.assertEqual((False, 1, "PROVIDER_WARNING_OR_ERROR"),
                         (record["payload_valid"], record["row_count"], record["warning_error_classification"]))
        self.assertNotIn("secret text", json.dumps(record))
        timeout = experiment_evidence.evidence_record("ALKA.JK", self.day, self.retrieved, None, b"", "TIMEOUT")
        self.assertEqual(("TIMEOUT", "TIMEOUT"),
                         (timeout["http_outcome"], timeout["warning_error_classification"]))
        with tempfile.TemporaryDirectory() as directory:
            with self.assertRaisesRegex(ValueError, "Account responses"):
                experiment_evidence.write_evidence(
                    "bounded-eod", "ALKA.JK", self.day, self.retrieved, 200,
                    b'{"subscriptionType":"Free","apiRequests":1,"api_token":"secret"}',
                    repository_root=Path(directory))
            self.assertEqual([], list(Path(directory).rglob("*.json")))

    def test_isolation_and_path_escape(self):
        imports = [node.module for node in ast.walk(ast.parse(inspect.getsource(experiment_evidence)))
                   if isinstance(node, ast.ImportFrom)]
        self.assertEqual(["datetime", "hashlib", "pathlib", "eodhd_experimental"], imports)
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            with self.assertRaises(ValueError):
                experiment_evidence.write_evidence(
                    "../raw", "ALKA.JK", self.day, self.retrieved, 200,
                    self.payload(), repository_root=root)
            (root / "data/collector-output").mkdir(parents=True)
            (root / "data/raw").mkdir(parents=True)
            (root / "data/collector-output/experiments").symlink_to(root / "data/raw")
            with self.assertRaisesRegex(ValueError, "redirected"):
                experiment_evidence.write_evidence(
                    "bounded-eod", "ALKA.JK", self.day, self.retrieved, 200,
                    self.payload(), repository_root=root)

    @unittest.skipUnless(os.environ.get("IDX_EXPERIMENT_VERIFY_DB") == "1", "Opt-in local PostgreSQL fingerprint")
    def test_production_database_fingerprint_unchanged(self):
        tables = ("pilot_schema_version", "source", "ingestion_run", "raw_artifact", "instrument",
                  "instrument_history", "market_session", "daily_bar_revision",
                  "raw_fetch_observation", "instrument_listing_evidence")
        query = "\n".join(
            f"SELECT '{table}', md5(coalesce(string_agg(md5(row_to_json(t)::text), ',' "
            f"ORDER BY md5(row_to_json(t)::text)), '')) FROM {table} t;"
            for table in tables)

        def fingerprint():
            result = subprocess.run(
                ["docker", "compose", "exec", "-T", "postgres", "psql", "-X", "-q", "-A", "-t",
                 "-v", "ON_ERROR_STOP=1", "-U", "idx_stock", "-d", "idx_stock_intelligence"],
                input=query, text=True, capture_output=True, timeout=60, check=True)
            return result.stdout

        before = fingerprint()
        with tempfile.TemporaryDirectory() as directory:
            experiment_evidence.write_evidence(
                "bounded-eod", "ALKA.JK", self.day, self.retrieved, 200,
                self.payload(), repository_root=Path(directory))
        self.assertEqual(before, fingerprint())


if __name__ == "__main__":
    unittest.main()
