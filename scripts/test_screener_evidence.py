"""Milestone 2 SQL checks through standard .NET discovery in an owned disposable DB.
Run: python3.13 -m unittest discover -s scripts -p test_screener_evidence.py -v
No provider calls, API, collector, registry or operational evidence writes.
"""
import os
from pathlib import Path
import shlex
import subprocess
import unittest
import uuid

import test_product_slice

ROOT = Path(__file__).resolve().parents[1]


def fingerprints(sql):
    tables = ("daily_bar_revision", "raw_artifact", "raw_fetch_observation", "ingestion_run",
              "instrument", "instrument_history", "instrument_listing_evidence", "market_session",
              "portfolio", "portfolio_event", "thesis_version", "decision_snapshot_run",
              "decision_snapshot_row", "decision_snapshot_outcome", "pilot_schema_version")
    return {table: sql(f"SELECT count(*)||':'||coalesce(md5(string_agg(to_jsonb(t)::text,',' "
                       f"ORDER BY to_jsonb(t)::text)),'') FROM {table} t;", "idx_stock_intelligence") for table in tables}


def files():
    paths = list((ROOT / "pilot").glob("*.json"))
    paths += list((ROOT / "data/collector-output/pilot").glob("*.operation.json"))
    return {p.relative_to(ROOT).as_posix(): p.read_bytes() for p in paths}


def environment(database):
    env = {k: v for k, v in os.environ.items() if k != "EODHD_API_TOKEN"}
    config = {}
    for line in (ROOT / ".env").read_text().splitlines():
        if line.strip() and not line.lstrip().startswith("#") and "=" in line:
            key, value = line.split("=", 1)
            values = shlex.split(value, comments=True)
            config[key.strip()] = values[0] if values else ""
    password = env.get("POSTGRES_PASSWORD", config.get("POSTGRES_PASSWORD", ""))
    port = env.get("POSTGRES_PORT", config.get("POSTGRES_PORT", "5432"))
    connection = (f'Host=127.0.0.1;Port={port};Database={database};Username=idx_stock;'
                  f'Password="{password.replace(chr(34), chr(34)*2)}";Timeout=5;Command Timeout=15')
    return env, connection, password


class ScreenerEvidenceAcceptance(unittest.TestCase):
    sql = classmethod(test_product_slice.ProductAcceptance.sql.__func__)

    def test_standard_discovery_with_disposable_postgres_and_unchanged_operations(self):
        cls = type(self)
        cls.database = "idx_screener_test_" + uuid.uuid4().hex
        before, files_before = fingerprints(cls.sql), files()
        env, connection, password = environment(cls.database)
        env["IDX_SCREENER_TEST_CONNECTION"] = connection
        cls.sql("CREATE DATABASE " + cls.database + ";", "idx_stock_intelligence")
        try:
            for migration in sorted((ROOT / "src/IdxStockIntelligence.Infrastructure/Migrations").glob("*.sql")):
                cls.sql(migration.read_text())
            # Canonical discovery is the primary runner; the wrapper only supplies disposable ownership/environment.
            command = ["rtk", "proxy", "dotnet", "test"]
            if env.get("IDX_SCREENER_TEST_FILTER_CLASS"):
                command += ["--filter-class", env["IDX_SCREENER_TEST_FILTER_CLASS"]]
            result = subprocess.run(command, cwd=ROOT, env=env,
                                    # Bounded historical episode replay adds per-session retained reads to the full suite.
                                    capture_output=True, text=True, timeout=600)
            output = (result.stdout + result.stderr).replace(password, "[redacted]") if password else result.stdout + result.stderr
            print(output, flush=True)
            self.assertEqual(0, result.returncode, output)
        finally:
            cls.sql("DROP DATABASE " + cls.database + " WITH (FORCE);", "idx_stock_intelligence")
            self.assertEqual(before, fingerprints(cls.sql))
            self.assertEqual(files_before, files())
