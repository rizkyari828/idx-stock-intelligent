"""Bounded JSON/CSV restore acceptance; synthetic writes only in owned disposable DBs.
Run: python3.13 -m unittest discover -s scripts -p test_portfolio_exchange.py -v
Requires dotnet build, frontend build, existing compose PostgreSQL; zero provider I/O.
"""
from concurrent.futures import ThreadPoolExecutor
from contextlib import contextmanager
from copy import deepcopy
import http.client
import json
import os
import socket
import subprocess
import tempfile
import time
import urllib.error
import urllib.request
import uuid
import unittest
import test_product_slice as product


class PortfolioExchangeAcceptance(product.ProductAcceptance):
    # Reuse the existing isolated source setup and original product regression tests.
    @contextmanager
    def clean_target(self):
        target = type("OwnedRestoreTarget", (product.ProductAcceptance,), {})
        target.database = "idx_restore_test_" + uuid.uuid4().hex
        with socket.socket() as listener:
            listener.bind(("127.0.0.1", 0))
            target.base = "http://127.0.0.1:" + str(listener.getsockname()[1])
        target.sql("CREATE DATABASE " + target.database + ";", "idx_stock_intelligence")
        process = None
        try:
            for migration in sorted((product.ROOT / "src/IdxStockIntelligence.Infrastructure/Migrations").glob("*.sql")):
                target.sql(migration.read_text())
            env = dict(self.env)
            env["IDX_DATABASE_CONNECTION"] = env["IDX_DATABASE_CONNECTION"].replace("Database=" + self.database + ";", "Database=" + target.database + ";")
            with tempfile.TemporaryFile() as log:
                process = subprocess.Popen(["dotnet", "run", "--project", "src/IdxStockIntelligence.Api", "--no-build", "--no-restore", "--", "--urls", target.base], cwd=product.ROOT, env=env, stdout=log, stderr=log)
                for _ in range(100):
                    if process.poll() is not None: raise RuntimeError("Disposable restore API failed to start.")
                    try:
                        self.assertEqual([], target.request("/api/instruments"))
                        break
                    except OSError: time.sleep(.1)
                else: raise RuntimeError("Disposable restore API startup timed out.")
                yield target
        finally:
            if process is not None:
                process.terminate()
                try: process.wait(timeout=10)
                except subprocess.TimeoutExpired: process.kill(); process.wait()
            target.sql("DROP DATABASE " + target.database + " WITH (FORCE);", "idx_stock_intelligence")

    @staticmethod
    def counts(target):
        return target.sql("SELECT json_build_array((SELECT count(*) FROM portfolio),(SELECT count(*) FROM portfolio_event),(SELECT count(*) FROM thesis_version),(SELECT count(*) FROM instrument),(SELECT count(*) FROM instrument_history))")

    @staticmethod
    def exchange(document, mode="CREATE_NEW"):
        return dict(format="JSON", content=json.dumps(document), mode=mode)

    @staticmethod
    def facts(view):
        # Market data is intentionally excluded from a portable portfolio archive.
        return dict(cash=view["cash"], holdings={h["position"]["instrumentId"]: dict(position=h["position"], thesis=h["activeThesis"]) for h in view["holdings"]})

    def historical_source(self):
        p = str(uuid.uuid4())
        pilot, outside = self.fixture["pilot_instrument_id"], self.fixture["outside_instrument_id"]
        self.sql(f"INSERT INTO portfolio VALUES ('{p}','SYNTHETIC HISTORICAL RESTORE',false,'2026-09-01T00:00:00Z')")
        events = [
            ("CASH_DEPOSIT", None, "2026-09-01", "2026-09-02", 0, 0, 0, 1000000, None),
            ("BUY", pilot, "2026-09-10", "2026-09-11", 1300, 100, 1300, 0, None),
            ("SELL", pilot, "2026-09-15", "2026-09-16", 300, 120, 300, 0, None),
            ("BUY", pilot, "2026-09-10", "2026-09-20", 1300, 110, 1300, 0, 1),
            ("BUY", outside, "2026-09-20", "2026-09-22", 100, 50, 50, 0, None),
        ]
        ids = [str(uuid.uuid4()) for _ in events]
        for n, (kind, instrument, day, known, qty, price, fees, cash, correction) in enumerate(events):
            stock = "NULL" if instrument is None else "'" + instrument + "'"
            supersedes = "NULL" if correction is None else "'" + ids[correction] + "'"
            self.sql(f"""INSERT INTO portfolio_event(event_id,portfolio_id,event_type,instrument_id,trade_date,known_at,quantity,quantity_unit,price,fees,cash_amount,external_reference,source,note,supersedes)
                VALUES ('{ids[n]}','{p}','{kind}',{stock},'{day}','{known}T00:00:00Z',{qty},'SHARES',{price},{fees},{cash},'synthetic-{n}','USER','SYNTHETIC ONLY',{supersedes})""")
        first, second, inactive = [str(uuid.uuid4()) for _ in range(3)]
        self.sql(f"""INSERT INTO thesis_version VALUES
            ('{first}','{p}','{pilot}',1,'FAST_SWING','SYNTHETIC swing','2026-09-12T00:00:00Z',NULL,NULL,true),
            ('{second}','{p}','{pilot}',2,'INVEST','SYNTHETIC explicit mandate change','2026-09-25T00:00:00Z','{first}',NULL,true),
            ('{inactive}','{p}','{pilot}',3,'INVEST','SYNTHETIC inactive','2026-09-28T00:00:00Z','{second}','SYNTHETIC invalidation metadata',false)""")
        return p

    def test_lossless_restore_historical_cutoffs_idempotency_conflicts_and_atomicity(self):
        p = self.historical_source()
        route = f"/api/portfolios/{p}"
        document = self.request(route + "/export")
        self.assertEqual(document, self.request(route + "/export"))
        self.assertEqual(1, document["schemaVersion"])
        self.assertEqual([1, 2, 3, 4, 5], [e["order"] for e in document["events"]])
        self.assertEqual(1300, document["events"][1]["quantity"])
        self.assertEqual(document["events"][1]["id"], document["events"][3]["supersedes"])
        self.assertEqual("SYNTHETIC invalidation metadata", document["theses"][2]["invalidationNote"])
        for forbidden in ("connectionString", "password", "raw_artifact", "daily_bar_revision", "EODHD"):
            self.assertNotIn(forbidden, json.dumps(document))
        through = "2026-09-30"
        cutoffs = ["2026-09-09", "2026-09-11", "2026-09-15", "2026-09-16", "2026-09-19", "2026-09-20", "2026-09-21", "2026-09-22", "2026-09-24", "2026-09-25", "2026-09-28"]
        queries = [f"?through={through}&cutoff={c}T00:00:00Z" for c in cutoffs]
        queries += [f"?through=2026-09-{d}&cutoff=2026-09-30T00:00:00Z" for d in ("09", "10", "14", "15", "19", "20")]
        expected = {q: self.facts(self.request(route + q)) for q in queries}
        pilot, outside = self.fixture["pilot_instrument_id"], self.fixture["outside_instrument_id"]
        self.assertEqual(101, expected[queries[2]]["holdings"][pilot]["position"]["averageCost"])
        self.assertEqual(111, expected[queries[5]]["holdings"][pilot]["position"]["averageCost"])
        self.assertNotIn(outside, expected[queries[6]]["holdings"])  # Sep20 trade, Sep22 recorded.
        self.assertEqual("FAST_SWING", expected[queries[8]]["holdings"][pilot]["thesis"]["mandate"])
        self.assertEqual("INVEST", expected[queries[9]]["holdings"][pilot]["thesis"]["mandate"])
        self.assertIsNone(expected[queries[10]]["holdings"][pilot]["thesis"])
        current = self.facts(self.request(route + "?through=" + through))
        self.assertEqual(886350, current["cash"])
        self.assertEqual((1000, 111, 2400), tuple(current["holdings"][pilot]["position"][k] for k in ("shares", "averageCost", "realizedPnl")))
        self.assertEqual((100, 50.5), tuple(current["holdings"][outside]["position"][k] for k in ("shares", "averageCost")))
        with self.clean_target() as target:
            blank = self.counts(target)
            body = self.exchange(document)
            preview = target.request("/api/portfolio-imports/preview", body)
            self.assertEqual(("READY", 5, 5, 0, 5), tuple(preview[k] for k in ("status", "rowsRead", "validRows", "invalidRows", "estimatedResultingEvents")))
            self.assertEqual(blank, self.counts(target))
            for invalid in (dict(document, schemaVersion=2), dict(document, events=[None]), dict(document, portfolio=None), dict(document, events=document["events"] * 2001)):
                self.assertEqual("INVALID", target.request("/api/portfolio-imports", self.exchange(invalid))["status"])
                self.assertEqual(blank, self.counts(target))
            self.assertEqual("INVALID", target.request("/api/portfolio-imports", dict(body, content="x" * (8 * 1024 * 1024 + 1)))["status"])
            self.assertEqual("CONFLICT", target.request("/api/portfolio-imports", self.exchange(document, "RESTORE_EXISTING_EMPTY"))["status"])
            # Force a failure after earlier inserts. The header, instrument references and events all roll back.
            target.sql("""CREATE FUNCTION synthetic_import_failure() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN
                IF NEW.event_type='SELL' THEN RAISE EXCEPTION 'SYNTHETIC rollback check'; END IF; RETURN NEW; END $$;
                CREATE TRIGGER synthetic_import_failure BEFORE INSERT ON portfolio_event FOR EACH ROW EXECUTE FUNCTION synthetic_import_failure();""")
            target.request("/api/portfolio-imports", body, expected=503)
            self.assertEqual(blank, self.counts(target))
            target.sql("DROP TRIGGER synthetic_import_failure ON portfolio_event; DROP FUNCTION synthetic_import_failure();")
            with ThreadPoolExecutor(max_workers=4) as pool:
                results = list(pool.map(lambda _: target.request("/api/portfolio-imports", body), range(4)))
            self.assertEqual(1, sum(r["status"] == "IMPORTED" for r in results))
            self.assertEqual(3, sum(r["status"] == "ALREADY_PRESENT" for r in results))
            result = next(r for r in results if r["status"] == "IMPORTED")
            self.assertEqual(("IMPORTED", 5, 3), (result["status"], result["eventsAdded"], result["thesesAdded"]))
            restored_counts = self.counts(target)
            self.assertEqual(document, target.request(route + "/export"))
            self.assertEqual(current, self.facts(target.request(route + "?through=" + through)))
            for q in queries: self.assertEqual(expected[q], self.facts(target.request(route + q)), q)
            theses = route + f"/holdings/{pilot}/theses"
            for c in cutoffs:
                self.assertEqual(self.request(theses + f"?cutoff={c}T00:00:00Z"), target.request(theses + f"?cutoff={c}T00:00:00Z"))
            restored_outside = next(h for h in target.request(route)["holdings"] if h["position"]["instrumentId"] == outside)
            self.assertEqual("NO_CURRENT_MARKET_PRICE", restored_outside["valuation"]["unavailableReason"])
            for mode in ("CREATE_NEW", "RESTORE_EXISTING_EMPTY"):
                retry = target.request("/api/portfolio-imports", self.exchange(document, mode))
                self.assertEqual(("ALREADY_PRESENT", 0, 0), (retry["status"], retry["eventsAdded"], retry["thesesAdded"]))
            changed = deepcopy(document); changed["events"][1]["note"] = "SYNTHETIC conflict"
            self.assertEqual("CONFLICT", target.request("/api/portfolio-imports", self.exchange(changed))["status"])
            changed = deepcopy(document); changed["instruments"][0]["issuerName"] = "SYNTHETIC identity conflict"
            self.assertEqual("CONFLICT", target.request("/api/portfolio-imports", self.exchange(changed))["status"])
            collision = deepcopy(document); collision["portfolio"]["id"] = str(uuid.uuid4())
            for record in collision["events"] + collision["theses"]: record["portfolioId"] = collision["portfolio"]["id"]
            self.assertEqual("CONFLICT", target.request("/api/portfolio-imports", self.exchange(collision))["status"])
            self.assertEqual(restored_counts, self.counts(target))
        with self.clean_target() as target:
            header = document["portfolio"]
            target.sql(f"INSERT INTO portfolio VALUES ('{p}','{header['name']}',false,'{header['createdAt']}')")
            self.assertEqual("CONFLICT", target.request("/api/portfolio-imports", self.exchange(document))["status"])
            self.assertEqual("READY", target.request("/api/portfolio-imports/preview", self.exchange(document, "RESTORE_EXISTING_EMPTY"))["status"])
            self.assertEqual("IMPORTED", target.request("/api/portfolio-imports", self.exchange(document, "RESTORE_EXISTING_EMPTY"))["status"])
            self.assertEqual(document, target.request(route + "/export"))

    def test_csv_preview_normalization_duplicate_conflict_invalid_rows_and_bounds(self):
        p = self.portfolio()
        pilot, outside = self.fixture["pilot_instrument_id"], self.fixture["outside_instrument_id"]
        header = "trade_date,type,instrument_id,symbol,quantity,unit,price,fees,cash_amount,external_reference,note\n"
        csv = header + "2026-09-01,CASH_DEPOSIT,,,0,SHARES,0,0,1000000,synthetic-cash,deposit\n"
        csv += f'2026-09-10,BUY,{pilot},,13,LOTS,100,1300,0,synthetic-buy,"comma, quote "" and newline\ntext"\n'
        csv += f"2026-09-20,BUY,,{self.fixture['outside_symbol']},100,SHARES,50,50,0,synthetic-outside,outside\n"
        csv += f"2026-09-21,SELL,{pilot},,300,SHARES,120,300,0,synthetic-sell,partial\n"
        body = dict(format="CSV", content=csv, mode="CREATE_NEW", portfolioId=p)
        before = self.counts(self)
        self.assertEqual("INVALID", self.request("/api/portfolio-imports", dict(body, mode="RESTORE_EXISTING_EMPTY"))["status"])
        preview = self.request("/api/portfolio-imports/preview", body)
        self.assertEqual(("READY", 4, 4, 0, 0, 4), tuple(preview[k] for k in ("status", "rowsRead", "validRows", "invalidRows", "duplicates", "estimatedResultingEvents")))
        self.assertEqual(1300, preview["rows"][1]["event"]["quantity"])
        self.assertEqual("SHARES", preview["rows"][1]["event"]["unit"])
        self.assertEqual(before, self.counts(self))
        insufficient = self.request("/api/portfolio-imports/preview", dict(body, content=csv.replace(",1000000,synthetic-cash,", ",1,synthetic-cash,")))
        self.assertEqual(("INVALID", 4, 4), (insufficient["status"], insufficient["rowsRead"], insufficient["invalidRows"]))
        self.assertEqual(before, self.counts(self))
        invalid_csv = csv + f"2026-09-22,BUY,{uuid.uuid4()},,1,SHARES,100,0,0,synthetic-unknown,unknown\n"
        invalid = self.request("/api/portfolio-imports/preview", dict(body, content=invalid_csv))
        self.assertEqual(("INVALID", 5, 4, 1, 1), (invalid["status"], invalid["rowsRead"], invalid["validRows"], invalid["invalidRows"], len(invalid["unknownInstruments"])))
        self.assertEqual("INVALID", self.request("/api/portfolio-imports", dict(body, content=invalid_csv))["status"])
        self.assertEqual(before, self.counts(self))
        for content in (csv.replace(",LOTS,", ",,", 1), header + '2026-09-10,BUY,"unterminated', "wrong,header\n", header + "2026-09-01,CASH_DEPOSIT,,,0,SHARES,0,0,1,synthetic-bound,x\n" * 10001):
            self.assertEqual("INVALID", self.request("/api/portfolio-imports", dict(body, content=content))["status"])
            self.assertEqual(before, self.counts(self))
        result = self.request("/api/portfolio-imports", body)
        self.assertEqual(("IMPORTED", 4), (result["status"], result["eventsAdded"]))
        after = self.counts(self)
        self.assertEqual("ALREADY_PRESENT", self.request("/api/portfolio-imports", body)["status"])
        retry = self.request("/api/portfolio-imports/preview", body)
        self.assertEqual((4, 4), (retry["duplicates"], retry["estimatedResultingEvents"]))
        self.assertEqual("CONFLICT", self.request("/api/portfolio-imports", dict(body, content=csv.replace(",100,1300,", ",110,1300,")))["status"])
        self.assertEqual(after, self.counts(self))
        view = self.request(f"/api/portfolios/{p}")
        self.assertEqual(899350, view["cash"])
        archive = self.request(f"/api/portfolios/{p}/export")
        self.assertEqual("comma, quote \" and newline\ntext", archive["events"][1]["note"])
        self.assertTrue(all(e["source"] == "GENERIC_CSV" for e in archive["events"]))
        with urllib.request.urlopen(self.base + f"/api/portfolios/{p}/export?format=CSV", timeout=30) as response:
            self.assertTrue(response.headers["Content-Type"].startswith("text/csv"))
            exported = response.read().decode()
        self.assertEqual("ALREADY_PRESENT", self.request("/api/portfolio-imports", dict(body, content=exported))["status"])

    def test_http_bounds_content_validation_and_cross_site_rejection(self):
        # Kestrel rejects oversized Content-Length before reading the body. Sending
        # 24 MiB first can give urllib a broken pipe before it reads the 413.
        for route, length in (("/api/portfolio-imports", 24 * 1024 * 1024 + 1), ("/api/portfolios", 65537)):
            connection = http.client.HTTPConnection("127.0.0.1", int(self.base.rsplit(":", 1)[1]), timeout=10)
            try:
                connection.putrequest("POST", route)
                connection.putheader("Content-Type", "application/json")
                connection.putheader("Content-Length", str(length))
                connection.endheaders()
                self.assertEqual(413, connection.getresponse().status)
            finally: connection.close()
        for route, data, headers, status in (
            ("/api/portfolio-imports", b"{}", {"Content-Type": "text/plain"}, 415),
            ("/api/portfolio-imports", b"{}", {"Content-Type": "application/json", "Sec-Fetch-Site": "cross-site"}, 415),
        ):
            request = urllib.request.Request(self.base + route, data=data, headers=headers)
            with self.assertRaises(urllib.error.HTTPError) as error:
                urllib.request.urlopen(request, timeout=30)
            self.assertEqual(status, error.exception.code)
            error.exception.close()
        before = self.counts(self)
        result = self.request("/api/portfolio-imports", dict(format="JSON", content="../../.env", mode="CREATE_NEW"))
        self.assertEqual("INVALID", result["status"])
        self.assertEqual(before, self.counts(self))

    def test_reconciliation_is_read_only_strict_and_historically_reproducible(self):
        p = self.historical_source()
        route = f"/api/portfolios/{p}"
        preview_route = route + "/reconciliation/preview"
        before = self.request(route + "/export")
        counts = self.counts(self)
        def expected(through="2026-09-30", cutoff="2026-09-30T00:00:00Z"):
            view = self.request(route + f"?through={through}&cutoff={cutoff}")
            return dict(through=through, cutoff=cutoff, expectedCash=str(view["cash"]), holdings=[dict(
                instrumentId=h["position"]["instrumentId"], symbol=h["market"]["displaySymbol"],
                shares=str(h["position"]["shares"]), averageCost=str(h["position"]["averageCost"])) for h in view["holdings"]])
        exact = expected()
        result = self.request(preview_route, exact)
        self.assertEqual(("MATCH", 2, 0), (result["status"], result["matched"], result["review"]))
        self.assertEqual("MATCH", next(r for r in result["rows"] if r["instrumentId"] == self.fixture["outside_instrument_id"])["status"])
        for field in ("expectedCash", "shares", "averageCost"):
            body = deepcopy(exact)
            if field == "expectedCash": body[field] = str(float(body[field]) + 1)
            else: body["holdings"][0][field] = str(float(body["holdings"][0][field]) + 1)
            self.assertEqual("REVIEW", self.request(preview_route, body)["status"])
        missing = deepcopy(exact); missing["holdings"].pop()
        self.assertIn("MISSING_FROM_EXPECTED", [r["status"] for r in self.request(preview_route, missing)["rows"]])
        blank = dict(through=exact["through"], cutoff="2026-09-09T00:00:00Z", expectedCash="1000000", holdings=exact["holdings"])
        self.assertEqual({"MISSING_FROM_LEDGER"}, {r["status"] for r in self.request(preview_route, blank)["rows"]})
        unknown = deepcopy(exact); unknown["holdings"].append(dict(instrumentId=None, symbol="NO_SUCH_EQUITY", shares=100, averageCost="10"))
        self.assertIn("UNKNOWN_INSTRUMENT", [r["status"] for r in self.request(preview_route, unknown)["rows"]])
        mismatch = deepcopy(exact); mismatch["holdings"][0]["symbol"] = "NO_SUCH_EQUITY"
        self.request(preview_route, mismatch, expected=400)
        duplicate = deepcopy(exact); duplicate["holdings"].append(exact["holdings"][0])
        self.request(preview_route, duplicate, expected=400)
        for through, cutoff in (("2026-09-30", "2026-09-19T00:00:00Z"), ("2026-09-30", "2026-09-20T00:00:00Z"), ("2026-09-09", "2026-09-30T00:00:00Z")):
            self.assertEqual("MATCH", self.request(preview_route, expected(through, cutoff))["status"])
        history = self.request(route + "/events")
        original = next(e for e in history if e["correctedBy"])
        corrected = next(e for e in history if e["supersedes"] == original["id"])
        self.assertEqual(corrected["id"], original["correctedBy"])
        earlier = self.request(route + "/events?cutoff=2026-09-19T00:00:00Z")
        self.assertIsNone(next(e for e in earlier if e["id"] == original["id"])["correctedBy"])
        self.assertEqual(before, self.request(route + "/export"))
        self.assertEqual(counts, self.counts(self))

    def test_registry_effective_date_and_registration_conflicts_are_authoritative(self):
        symbol = "SYNTHETIC_EFFECTIVE_" + uuid.uuid4().hex[:8].upper()
        instrument = str(uuid.uuid4())
        self.request("/api/instruments", dict(id=instrument, name="SYNTHETIC NAME ONLY", symbol=symbol,
            type="EQUITY", validFrom=self.day))
        current = next(i for i in self.request("/api/instruments?limit=200&through=" + self.day) if i["id"] == instrument)
        self.assertEqual(symbol, current["symbol"])
        self.assertTrue(current["hasEffectiveSymbol"])
        earlier = next(i for i in self.request("/api/instruments?limit=200&through=1900-01-01") if i["id"] == instrument)
        self.assertFalse(earlier["hasEffectiveSymbol"])
        self.assertEqual("SYNTHETIC NAME ONLY", earlier["symbol"])
        # Minimal API binding rejects malformed dates before the JSON error middleware.
        with self.assertRaises(urllib.error.HTTPError) as malformed:
            urllib.request.urlopen(self.base + "/api/instruments?through=not-a-date", timeout=30)
        self.assertEqual(400, malformed.exception.code)
        self.request("/api/instruments?through=2999-01-01", expected=400)
        p = self.portfolio()
        before = self.request(f"/api/portfolios/{p}/export")
        conflict = dict(id=str(uuid.uuid4()), name="MUST NOT OVERWRITE", symbol=symbol, type="EQUITY", validFrom=self.day)
        self.request("/api/instruments", conflict, expected=400)
        self.assertEqual(current, next(i for i in self.request("/api/instruments?limit=200&through=" + self.day) if i["id"] == instrument))
        # Historical closed ownership also blocks a new open interval that would overlap it.
        closed = str(uuid.uuid4())
        closed_symbol = "CLOSED_" + uuid.uuid4().hex[:8].upper()
        self.sql(f"INSERT INTO instrument(instrument_id,issuer_name,instrument_type) VALUES ('{closed}','SYNTHETIC CLOSED','EQUITY'); INSERT INTO instrument_history(instrument_id,symbol,valid_from,valid_to) VALUES ('{closed}','{closed_symbol}','2010-01-01','2020-01-01');")
        counts = self.counts(self)
        self.request("/api/instruments", dict(id=str(uuid.uuid4()), name=closed_symbol, symbol=closed_symbol,
            type="EQUITY", validFrom="2015-01-01"), expected=400)
        self.assertEqual(counts, self.counts(self))
        self.assertEqual(before, self.request(f"/api/portfolios/{p}/export"))

    @unittest.skipUnless(os.environ.get("IDX_TEST_BROWSER") == "1", "Opt-in real Chrome UI acceptance")
    def test_production_react_browser_interactions(self):
        p = self.historical_source()
        runtime = os.environ.get("IDX_TEST_NODE", "node")
        result = subprocess.run([runtime, "scripts/check_product_ui.mjs", self.base, p], cwd=product.ROOT,
            env=self.env, text=True, capture_output=True, timeout=150)
        self.assertEqual(0, result.returncode, result.stderr + result.stdout)
        self.assertIn('"status": "PASS"', result.stdout)
        print(result.stdout)
