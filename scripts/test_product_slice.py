"""Offline HTTP/PostgreSQL acceptance. Owns a disposable synthetic DB; no provider I/O.
Run via standard discovery: python3.13 -m unittest discover -s scripts -p test_product_slice.py -v
Build .NET and frontend first. Requires existing local compose PostgreSQL and .env.
"""
from concurrent.futures import ThreadPoolExecutor
from datetime import datetime, timedelta, timezone
import json
import os
from pathlib import Path
import shlex
import socket
import subprocess
import tempfile
import time
import unittest
import urllib.error
import urllib.request
import uuid
from zoneinfo import ZoneInfo

ROOT = Path(__file__).resolve().parents[1]


class ProductAcceptance(unittest.TestCase):
    @classmethod
    def sql(cls, statement, database=None):
        result = subprocess.run(["docker", "compose", "exec", "-T", "postgres", "psql", "-X", "-q", "-A", "-t",
            "-v", "ON_ERROR_STOP=1", "-U", "idx_stock", "-d", database or cls.database],
            cwd=ROOT, input=statement, text=True, capture_output=True, timeout=30)
        if result.returncode:
            raise RuntimeError("Local acceptance SQL failed: " + result.stderr)
        return result.stdout.strip()

    @classmethod
    def request(cls, path, body=None, expected=200):
        # Only loopback HTTP is permitted; this fixture never imports collector networking.
        request = urllib.request.Request(cls.base + path, data=None if body is None else json.dumps(body).encode(),
            headers={"Content-Type": "application/json"})
        try:
            response = urllib.request.urlopen(request, timeout=30)
        except urllib.error.HTTPError as error:
            response = error
        with response:
            payload = response.read()
            if response.status != expected:
                raise AssertionError(f"{path}: expected {expected}, got {response.status}: {payload.decode()}")
            return json.loads(payload) if path.startswith("/api/") else payload.decode()

    @classmethod
    def setUpClass(cls):
        with socket.socket() as listener:
            listener.bind(("127.0.0.1", 0))
            cls.base = "http://127.0.0.1:" + str(listener.getsockname()[1])
        cls.database = "idx_product_test_" + uuid.uuid4().hex
        cls.fixture = json.loads((ROOT / "tests/fixtures/portfolio-slice.json").read_text())
        cls.production_before = cls.sql("SELECT count(*)||':'||coalesce(md5(string_agg(canonical_content_sha256::text,',' ORDER BY instrument_id,session_date,revision_number)),'') FROM daily_bar_revision;", "idx_stock_intelligence")
        cls.operational_tables = ("instrument", "instrument_history", "portfolio", "portfolio_event", "thesis_version")
        cls.operational_before = {name: cls.sql(f"SELECT count(*)||':'||coalesce(md5(string_agg(to_jsonb(t)::text,',' ORDER BY to_jsonb(t)::text)),'') FROM {name} t;", "idx_stock_intelligence") for name in cls.operational_tables}
        cls.soak_files_before = {p.name: p.read_bytes() for p in (ROOT / "data/collector-output/pilot").glob("*.operation.json")}
        cls.env = {k: v for k, v in os.environ.items() if k != "EODHD_API_TOKEN"}
        config = {}
        for line in (ROOT / ".env").read_text().splitlines():
            if line.strip() and not line.lstrip().startswith("#") and "=" in line:
                key, value = line.split("=", 1)
                values = shlex.split(value, comments=True)
                config[key.strip()] = values[0] if values else ""
        password = cls.env.get("POSTGRES_PASSWORD", config.get("POSTGRES_PASSWORD", ""))
        port = cls.env.get("POSTGRES_PORT", config.get("POSTGRES_PORT", "5432"))
        cls.env["IDX_DATABASE_CONNECTION"] = f'Host=127.0.0.1;Port={port};Database={cls.database};Username=idx_stock;Password="{password.replace(chr(34), chr(34)*2)}";Timeout=5;Command Timeout=15'
        cls.env["IDX_UI_ROOT"] = str(ROOT / "frontend/dist")
        cls.log = tempfile.TemporaryFile()
        cls.process = None
        cls.sql("CREATE DATABASE " + cls.database + ";", "idx_stock_intelligence")
        cls.addClassCleanup(cls.cleanup)
        for migration in sorted((ROOT / "src/IdxStockIntelligence.Infrastructure/Migrations").glob("*.sql")):
            cls.sql(migration.read_text())
        cls.sql((ROOT / "src/IdxStockIntelligence.Infrastructure/Migrations/0004_portfolio.sql").read_text())
        cls.process = subprocess.Popen(["dotnet", "run", "--project", "src/IdxStockIntelligence.Api", "--no-build", "--no-restore", "--", "--urls", cls.base],
            cwd=ROOT, env=cls.env, stdout=cls.log, stderr=cls.log)
        for _ in range(100):
            if cls.process.poll() is not None:
                cls.log.seek(0)
                raise RuntimeError("API failed to start: " + cls.log.read().decode().replace(password, "[redacted]"))
            try:
                instruments = cls.request("/api/instruments")
                if instruments != []:
                    raise RuntimeError("Acceptance API must start with an empty owned synthetic registry.")
                break
            except (OSError, AssertionError):
                time.sleep(.1)
        else:
            raise RuntimeError("API startup timed out.")
        today = datetime.now(timezone.utc).astimezone(ZoneInfo("Asia/Jakarta")).date()
        cls.day = str(today - timedelta(days=1))
        cls.market_day = str(today - timedelta(days=2))
        for instrument, symbol in ((cls.fixture["pilot_instrument_id"], cls.fixture["pilot_symbol"]),
                                   (cls.fixture["outside_instrument_id"], cls.fixture["outside_symbol"])):
            cls.request("/api/instruments", dict(id=instrument, name="SYNTHETIC " + symbol, symbol=symbol, type="EQUITY", validFrom="2000-01-01"))
        # Synthetic canonical observation with synthetic provenance, only in this temporary DB.
        cls.sql(f"""
            INSERT INTO source VALUES ('synthetic','SYNTHETIC TEST ONLY','UNKNOWN',NULL,NULL);
            INSERT INTO ingestion_run VALUES ('aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa','synthetic',now(),now(),'SUCCEEDED','synthetic','{{}}',NULL);
            INSERT INTO raw_artifact VALUES ('bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb','aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa','synthetic','fixture:synthetic','{{}}',now(),'fixture:synthetic',repeat('a',64),0,'synthetic');
            INSERT INTO daily_bar_revision(instrument_id,session_date,revision_number,known_at,ingestion_run_id,raw_artifact_id,canonical_content_sha256,open,high,low,close,volume,quality_status,retrieved_at)
            VALUES ('{cls.fixture['pilot_instrument_id']}','{cls.market_day}',1,now(),'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa','bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb',repeat('a',64),100,130,90,125,1000,'DEGRADED',now());
        """)

    @classmethod
    def cleanup(cls):
        if cls.process is not None:
            cls.process.terminate()
            try: cls.process.wait(timeout=10)
            except subprocess.TimeoutExpired: cls.process.kill(); cls.process.wait()
        cls.log.close()
        cls.sql("DROP DATABASE " + cls.database + " WITH (FORCE);", "idx_stock_intelligence")
        after = cls.sql("SELECT count(*)||':'||coalesce(md5(string_agg(canonical_content_sha256::text,',' ORDER BY instrument_id,session_date,revision_number)),'') FROM daily_bar_revision;", "idx_stock_intelligence")
        operational_after = {name: cls.sql(f"SELECT count(*)||':'||coalesce(md5(string_agg(to_jsonb(t)::text,',' ORDER BY to_jsonb(t)::text)),'') FROM {name} t;", "idx_stock_intelligence") for name in cls.operational_tables}
        if operational_after != cls.operational_before: raise AssertionError("Operational security master/portfolio history changed.")
        if after != cls.production_before: raise AssertionError("Production canonical history changed.")
        if cls.soak_files_before != {p.name: p.read_bytes() for p in (ROOT / "data/collector-output/pilot").glob("*.operation.json")}:
            raise AssertionError("Authoritative operation/soak ledger changed.")

    def portfolio(self):
        return self.request("/api/portfolios", dict(name="SYNTHETIC ACCEPTANCE ONLY"))["id"]

    def event(self, portfolio, type, **fields):
        body = dict(eventId=str(uuid.uuid4()), type=type, tradeDate=self.day, source="USER", **fields)
        return body, self.request(f"/api/portfolios/{portfolio}/events", body)

    def test_end_to_end_acceptance(self):
        f = self.fixture
        p = self.portfolio()
        self.event(p, "CASH_DEPOSIT", cashAmount=f["deposit"])
        buy, result = self.event(p, "BUY", instrumentId=f["pilot_instrument_id"], quantity=f["buy_lots"], unit="LOTS",
            price=f["buy_price"], fees=f["buy_fees"], externalReference="synthetic-import-1")
        self.assertEqual(1300, result["event"]["quantity"])
        self.assertEqual("SHARES", result["event"]["unit"])
        retry = dict(buy, eventId=str(uuid.uuid4()))
        self.assertTrue(self.request(f"/api/portfolios/{p}/events", retry)["duplicate"])
        self.request(f"/api/portfolios/{p}/events", dict(retry, price=999), expected=400)
        before = self.request(f"/api/portfolios/{p}")
        self.assertEqual(101, before["holdings"][0]["position"]["averageCost"])
        self.assertEqual(868700, before["cash"])
        self.event(p, "SELL", instrumentId=f["pilot_instrument_id"], quantity=f["partial_sell_shares"], price=f["sell_price"], fees=f["sell_fees"])
        correction, corrected = self.event(p, "BUY", instrumentId=f["pilot_instrument_id"], quantity=13, unit="LOTS", price=110,
            fees=1300, supersedes=result["event"]["id"], externalReference="synthetic-correction-1")
        current = self.request(f"/api/portfolios/{p}")
        self.assertEqual(1000, current["holdings"][0]["position"]["shares"])
        self.assertEqual(111, current["holdings"][0]["position"]["averageCost"])
        self.assertEqual(2400, current["holdings"][0]["position"]["realizedPnl"])
        self.assertEqual(891400, current["cash"])
        historical = self.request(f"/api/portfolios/{p}?cutoff={before['knowledgeCutoff'].replace('+', '%2B')}")
        self.assertEqual(1300, historical["holdings"][0]["position"]["shares"])
        self.assertEqual(101, historical["holdings"][0]["position"]["averageCost"])
        path = f"/api/portfolios/{p}/holdings/{f['pilot_instrument_id']}/theses"
        first = self.request(path, dict(mandate="LONG_SWING", text="SYNTHETIC first thesis"))
        second = self.request(path, dict(mandate="INVEST", text="SYNTHETIC explicit mandate change"))
        versions = self.request(path)
        self.assertEqual([2, 1], [v["version"] for v in versions])
        self.assertEqual(first["id"], second["supersedes"])
        asof = self.request(path + "?cutoff=" + first["knownAt"].replace('+', '%2B'))
        self.assertEqual([1], [v["version"] for v in asof])
        self.assertEqual("LONG_SWING", versions[1]["mandate"])
        self.event(p, "BUY", instrumentId=f["outside_instrument_id"], quantity=100, unit="SHARES", price=50, fees=50)
        view = self.request(f"/api/portfolios/{p}")
        self.assertEqual("SUCCESS", view["operation"])
        self.assertEqual(886350, view["cash"])
        self.assertEqual((1, 2, "PARTIAL"), (view["pricedHoldings"], view["holdingCount"], view["valuationCoverage"]))
        self.assertIsNone(view["totalMarketValue"]); self.assertIsNone(view["totalEquity"])
        outside = next(h for h in view["holdings"] if h["position"]["instrumentId"] == f["outside_instrument_id"])
        self.assertEqual("UNAVAILABLE", outside["valuation"]["availability"])
        self.assertEqual("NO_CURRENT_MARKET_PRICE", outside["valuation"]["unavailableReason"])
        self.assertEqual(50.5, outside["position"]["averageCost"])
        priced = next(h for h in view["holdings"] if h["position"]["instrumentId"] == f["pilot_instrument_id"])
        self.assertEqual("STALE", priced["market"]["freshness"]); self.assertEqual("DEGRADED", priced["market"]["quality"])
        self.assertEqual(125000, priced["valuation"]["marketValue"])
        self.assertEqual("INVEST", priced["activeThesis"]["mandate"])
        self.assertEqual("UNAVAILABLE", priced["market"]["features"]["ATR14"]["availability"])
        self.assertEqual([f["pilot_symbol"], f["outside_symbol"]], [h["market"]["displaySymbol"] for h in view["holdings"]])
        holdings = self.request(f"/api/portfolios/{p}/holdings?limit=1&offset=1")
        self.assertEqual(f["outside_instrument_id"], holdings["holdings"][0]["position"]["instrumentId"])
        events = self.request(f"/api/portfolios/{p}/events?limit=100")
        self.assertEqual(5, len(events))
        self.assertEqual(1, sum(e["supersedes"] is not None for e in events))
        self.assertEqual(corrected["event"]["id"], events[3]["id"])
        self.assertEqual("4", self.sql("SELECT version FROM pilot_schema_version WHERE version=4"))
        with self.assertRaises(RuntimeError): self.sql("UPDATE portfolio_event SET price=0;")
        with self.assertRaises(RuntimeError): self.sql("DELETE FROM thesis_version;")
        self.assertIn('id="root"', self.request('/'))
        market = self.request(f"/api/instruments/{f['outside_instrument_id']}/market-state")
        self.assertEqual("NO_CURRENT_MARKET_PRICE", market["unavailableReason"])
        # Built React component consumes the real API response, including outside holding.
        runtime = os.environ.get("IDX_TEST_NODE", "node")
        rendered = subprocess.run([runtime, "scripts/render_portfolio.mjs"], cwd=ROOT, input=json.dumps(view), text=True, capture_output=True, timeout=30)
        self.assertEqual(0, rendered.returncode, rendered.stderr)
        self.assertIn(f["outside_symbol"], rendered.stdout)
        self.assertIn("UNAVAILABLE", rendered.stdout)
        self.assertIn("PARTIAL", rendered.stdout)
        self.assertIn("111", rendered.stdout)
        self.request(path, dict(mandate="INVEST", text="SYNTHETIC explicit inactivation", active=False, invalidationNote="SYNTHETIC invalidation"))
        inactive = self.request(f"/api/portfolios/{p}")
        priced = next(h for h in inactive["holdings"] if h["position"]["instrumentId"] == f["pilot_instrument_id"])
        self.assertIsNone(priced["activeThesis"])
        self.assertEqual([3, 2, 1], [v["version"] for v in self.request(path)])

    def test_concurrent_duplicate_and_cash_rejection_are_atomic(self):
        p = self.portfolio()
        self.event(p, "CASH_DEPOSIT", cashAmount=10000)
        body = dict(eventId=str(uuid.uuid4()), type="BUY", tradeDate=self.day, source="BROKER_RECORD",
            instrumentId=self.fixture["outside_instrument_id"], quantity=100, price=10, externalReference="synthetic-concurrent")
        with ThreadPoolExecutor(max_workers=4) as pool:
            results = list(pool.map(lambda _: self.request(f"/api/portfolios/{p}/events", body), range(4)))
        self.assertEqual(1, sum(not r["duplicate"] for r in results))
        view = self.request(f"/api/portfolios/{p}")
        self.assertEqual(100, view["holdings"][0]["position"]["shares"])
        self.assertEqual(9000, view["cash"])
        self.assertEqual(0, view["pricedHoldings"])
        self.assertEqual("PARTIAL", view["valuationCoverage"])
        self.assertIsNone(view["totalMarketValue"])
        self.request(f"/api/portfolios/{p}/events", dict(body, eventId=str(uuid.uuid4()), externalReference=None, price=10000), expected=400)
        self.assertEqual(2, len(self.request(f"/api/portfolios/{p}/events")))
        self.request(f"/api/portfolios/{p}/events?limit=201", expected=400)
        self.request(f"/api/portfolios/{p}/holdings?offset=-1", expected=400)
        self.request(f"/api/portfolios/{p}?through=2099-01-01", expected=400)
        self.request("/api/instruments", dict(id=str(uuid.uuid4()), name="SYNTHETIC duplicate", symbol=self.fixture["outside_symbol"], type="EQUITY", validFrom="2000-01-01"), expected=400)
