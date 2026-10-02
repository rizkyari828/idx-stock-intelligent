"""Milestone 4 HTTP acceptance using the existing unittest/disposable PostgreSQL harness.
Run after dotnet build: python3.13 -m unittest discover -s scripts -p 'test_screener_*.py' -v
Synthetic evidence lives only in owned DBs and temporary API working directories.
"""
from concurrent.futures import ThreadPoolExecutor
from contextlib import contextmanager
from copy import deepcopy
from datetime import date, datetime, timedelta, timezone
import hashlib
import http.client
import json
from pathlib import Path
import socket
import subprocess
import tempfile
import time
import unittest
from urllib.parse import urlencode
import uuid

import test_product_slice as product
import test_screener_evidence as evidence

ROOT = product.ROOT
KNOWN = "2026-10-01T00:00:00Z"
CUTOFF = "2026-10-01T12:00:00Z"
THROUGH = "2026-09-30"
INDEX = "76237e96-232f-5085-9b13-dcb7104222bc"
RUN = "20000000-0000-4000-8000-000000000001"
ARTIFACT = "30000000-0000-4000-8000-000000000001"


def identifier(n):
    return f"10000000-0000-4000-8000-{n:012d}"


def snapshot_hash(snapshot):
    # Same canonical JSON as ScreenerReferences: ordinal objects/arrays, UTC instants, root hash omitted.
    def canonical(value):
        if isinstance(value, dict):
            return {key: (datetime.fromisoformat(item.replace("Z", "+00:00")).astimezone(timezone.utc)
                          .strftime("%Y-%m-%dT%H:%M:%S.0000000+00:00")
                          if key in ("knownAt", "retrievedAt", "publishedAt", "completedAt") and item is not None
                          else canonical(item)) for key, item in sorted(value.items())}
        if isinstance(value, list):
            return sorted((canonical(item) for item in value), key=encoded)
        return value

    def encoded(value):
        text = json.dumps(value, separators=(",", ":"), ensure_ascii=True)
        for char in ("+", "<", ">", "&", "'"):
            text = text.replace(char, f"\\u{ord(char):04X}")
        return text

    return hashlib.sha256(encoded(canonical({k: v for k, v in snapshot.items() if k != "contentHash"})).encode()).hexdigest()


class ScreenerHttpAcceptance(unittest.TestCase):
    sql = classmethod(product.ProductAcceptance.sql.__func__)
    request = classmethod(product.ProductAcceptance.request.__func__)

    @classmethod
    def setUpClass(cls):
        cls.before = evidence.fingerprints(cls.sql)
        cls.files_before = evidence.files()

    @classmethod
    def tearDownClass(cls):
        if cls.before != evidence.fingerprints(cls.sql): raise AssertionError("Operational database changed.")
        if cls.files_before != evidence.files(): raise AssertionError("Operational reference/operation files changed.")

    def setUp(self):
        cls = type(self)
        cls.database = "idx_screener_test_" + uuid.uuid4().hex
        self.sql("CREATE DATABASE " + cls.database, "idx_stock_intelligence")
        self.addCleanup(lambda: self.sql("DROP DATABASE " + cls.database + " WITH (FORCE)", "idx_stock_intelligence"))
        for migration in sorted((ROOT / "src/IdxStockIntelligence.Infrastructure/Migrations").glob("*.sql")):
            self.sql(migration.read_text())
        self.directory = tempfile.TemporaryDirectory(prefix="idx-screener-http-")
        self.addCleanup(self.directory.cleanup)
        self.pilot = Path(self.directory.name) / "pilot"
        self.pilot.mkdir()
        self.document = dict(schemaVersion=1, universes=[], instruments=[])
        self.write_reference()
        (self.pilot / "sessions.json").write_text("[]")
        (self.pilot / "instrument-sessions.json").write_text("[]")
        with socket.socket() as listener:
            listener.bind(("127.0.0.1", 0))
            cls.base = "http://127.0.0.1:" + str(listener.getsockname()[1])
        env, connection, _ = evidence.environment(cls.database)
        env["IDX_DATABASE_CONNECTION"] = connection
        env["IDX_UI_ROOT"] = self.directory.name  # No frontend/browser acceptance in this milestone.
        self.log = tempfile.TemporaryFile()
        self.addCleanup(self.log.close)
        self.process = subprocess.Popen(["dotnet", str(ROOT / "src/IdxStockIntelligence.Api/bin/Debug/net10.0/IdxStockIntelligence.Api.dll"),
                                         "--urls", cls.base], cwd=self.directory.name, env=env, stdout=self.log, stderr=self.log)
        self.addCleanup(self.stop)
        self.wait_for(lambda: self.request("/api/instruments") == [], "API startup")

    def stop(self):
        self.process.terminate()
        try: self.process.wait(timeout=10)
        except subprocess.TimeoutExpired: self.process.kill(); self.process.wait()

    def wait_for(self, predicate, description):
        for _ in range(100):
            try:
                if predicate(): return
            except OSError: pass
            time.sleep(.05)
        self.fail(description + " timed out")

    def write_reference(self):
        for snapshot in self.document["universes"] + self.document["instruments"]:
            snapshot["contentHash"] = snapshot_hash(snapshot)
        (self.pilot / "screener-reference.json").write_text(json.dumps(self.document))

    def screen(self, expected=200, **query):
        return self.request("/api/screener?" + urlencode(dict(through=THROUGH, cutoff=CUTOFF, **query)), expected=expected)

    def fixture(self):
        self.dates = []
        day = date(2026, 8, 24)
        while day <= date.fromisoformat(THROUGH):
            if day.weekday() < 5: self.dates.append(day.isoformat())
            day += timedelta(days=1)
        ids = [identifier(n) for n in range(1, 7)] + [INDEX]
        self.sql("INSERT INTO instrument(instrument_id,issuer_name,instrument_type) VALUES " +
                 ",".join(f"('{i}','SYNTHETIC ONLY','{'INDEX' if i == INDEX else 'EQUITY'}')" for i in ids))
        for i in ids:
            listing = dict(instrument_id=i, symbol="SYN", issuer_name="Synthetic", listed_from="2026-08-24",
                           delisted_at=None, first_trading_date=None, status="VERIFIED", source="synthetic",
                           reference="https://reference.example/listing", publication_reference="synthetic",
                           retrieved_at=KNOWN, known_at=KNOWN, confidence="VERIFIED", source_version="1", notes="Synthetic only")
            self.sql(f"INSERT INTO instrument_listing_evidence VALUES ('{i}','{KNOWN}','{json.dumps(listing)}'::jsonb)")
        self.sql(f"""INSERT INTO source VALUES ('synthetic','SYNTHETIC ONLY','UNKNOWN',NULL,NULL);
            INSERT INTO ingestion_run VALUES ('{RUN}','synthetic','{KNOWN}','{KNOWN}','SUCCEEDED','synthetic','{{}}',NULL);
            INSERT INTO raw_artifact VALUES ('{ARTIFACT}','{RUN}','synthetic','fixture:only','{{}}','{KNOWN}',
                'fixture:only',repeat('c',64),0,'synthetic');""")
        self.hashes = {i: [] for i in ids}
        rows = []
        for i in ids:
            for day in self.dates:
                if i == identifier(5) and day == THROUGH: continue
                close = 100 if i == INDEX else 111 if i == identifier(1) and day == THROUGH else 99 if i == identifier(2) and day == THROUGH else 90
                high = max(100, close)
                low = 100 if i == INDEX else 80
                digest = hashlib.sha256(f"{i}/{day}/{close}".encode()).hexdigest()
                self.hashes[i].append(digest)
                rows.append(f"('{i}','{day}',1,'{KNOWN}','{RUN}','{ARTIFACT}','{digest}',{close},{high},{low},{close},"
                            f"{0 if i == INDEX else 100},'DEGRADED','SHARES','RAW_AS_TRADED','REGULAR','{KNOWN}',"
                            f"'https://reference.example/session','{KNOWN}')")
        self.sql("INSERT INTO daily_bar_revision(instrument_id,session_date,revision_number,known_at,ingestion_run_id,raw_artifact_id,"
                 "canonical_content_sha256,open,high,low,close,volume,quality_status,volume_unit,volume_basis,market_segment,"
                 "retrieved_at,session_reference,session_known_at) VALUES " + ",".join(rows))
        source = dict(id="synthetic", source="synthetic", reference="https://reference.example/synthetic", publishedAt=None,
                      retrievedAt=KNOWN, knownAt=KNOWN)
        self.document["universes"] = [dict(snapshotId="synthetic-universe", knownAt=KNOWN, universeId="PILOT",
                                           memberIds=ids[:5], benchmarkId=INDEX, evidence=[source], contentHash="")]
        for i in ids:
            index = i == INDEX
            snapshot = dict(snapshotId="synthetic-" + i, instrumentId=i, knownAt=KNOWN, evidence=[source], contentHash="",
                identities=[dict(from_="2026-08-24", through=None, symbol="SYNINDEX" if index else "SYN" + i[-1], displayName="Synthetic",
                                 classification="INDEX" if index else "UNSUPPORTED" if i == identifier(4) else "ORDINARY",
                                 currency="NOT_APPLICABLE" if index else "IDR", board="NOT_APPLICABLE" if index else "MAIN", evidenceIds=["synthetic"])],
                trading=[dict(from_="2026-08-24", through="2027-08-24", status="TRADING", mechanism="CONTINUOUS", evidenceIds=["synthetic"])],
                prices=[dict(from_="2026-08-24", through="2027-08-24", sourceId="synthetic", convention="INDEX_LEVEL" if index else "STOCK_RAW",
                             continuity="RAW_AS_TRADED", eventCoverage="NOT_APPLICABLE" if index else "CLEARED", contentHashes=self.hashes[i], evidenceIds=["synthetic"])],
                volumes=[dict(from_="2026-08-24", through="2027-08-24", sourceId="synthetic", unit="SHARES", basis="RAW_AS_TRADED",
                              rawPriceCompatible=True, currency="IDR", marketSegment="REGULAR", contentHashes=self.hashes[i], evidenceIds=["synthetic"])])
            for collection in ("identities", "trading", "prices", "volumes"):
                for interval in snapshot[collection]: interval["from"] = interval.pop("from_")
            self.document["instruments"].append(snapshot)
        self.write_reference()
        sessions = [dict(date=d, status="ObservedTrading", reference="https://reference.example/session", known_at=KNOWN) for d in self.dates]
        (self.pilot / "sessions.json").write_text(json.dumps(sessions))

    def portfolio(self, held=(1, 4, 5, 6)):
        p = str(uuid.uuid4())
        self.sql(f"INSERT INTO portfolio VALUES ('{p}','SYNTHETIC ONLY',true,'2026-09-01T00:00:00Z')")
        for n in held: self.buy(p, n)
        self.sql(f"INSERT INTO thesis_version VALUES ('{uuid.uuid4()}','{p}','{identifier(1)}',1,'FAST_SWING',"
                 f"'SYNTHETIC ONLY','{KNOWN}',NULL,NULL,true)")
        return p

    def buy(self, p, n, known=KNOWN):
        self.sql(f"INSERT INTO portfolio_event(event_id,portfolio_id,instrument_id,event_type,trade_date,known_at,quantity,quantity_unit,price,fees,cash_amount,source) "
                 f"VALUES ('{uuid.uuid4()}','{p}','{identifier(n)}','BUY','{THROUGH}','{known}',100,'SHARES',10,0,0,'USER')")

    def append_bar(self, i, revision, close=111, known=KNOWN, quality="DEGRADED"):
        self.sql(self.bar_statement(i, revision, close, known, quality))

    @staticmethod
    def bar_statement(i, revision, close=111, known=KNOWN, quality="DEGRADED"):
        return (f"INSERT INTO daily_bar_revision(instrument_id,session_date,revision_number,known_at,ingestion_run_id,raw_artifact_id,"
                 "canonical_content_sha256,open,high,low,close,volume,quality_status,volume_unit,volume_basis,market_segment,"
                 f"adjusted_close,retrieved_at,session_reference,session_known_at) SELECT instrument_id,session_date,{revision},'{known}',ingestion_run_id,raw_artifact_id,"
                 f"repeat('{chr(96 + revision)}',64),{close},GREATEST(high,{close}),LEAST(low,{close}),{close},volume,'{quality}',"
                 "volume_unit,volume_basis,market_segment,adjusted_close,retrieved_at,session_reference,session_known_at "
                 f"FROM daily_bar_revision WHERE instrument_id='{i}' AND session_date='{THROUGH}' AND revision_number=1")

    def test_query_validation_and_empty_evidence_are_honest(self):
        response = self.request("/api/screener")
        self.assertEqual("BLOCKED", response["status"]); self.assertIsNone(response["summary"]["configured"])
        self.assertEqual([], response["heldIds"]); self.assertEqual([], response["rows"])
        self.assertEqual("UNKNOWN", response["marketContext"]["volatility"])
        for query in ("through=2026-08-23", "through=2027-08-25", "through=2099-01-01", "through=bad", "cutoff=2026-09-30T00:00:00",
                      "cutoff=1899-01-01T00:00:00Z", "cutoff=2099-01-01T00:00:00Z", "universe=FullIdx", "universe=AAA,BBB",
                      "portfolioId=bad", "view=invalid", "setup=BUY", "eligibility=bad", "offset=-1", "offset=10001", "limit=0",
                      "limit=101", "inputHash=A", "path=/tmp/data", "view=all&view=shortlist"):
            self.assertEqual("INVALID_QUERY", self.request("/api/screener?" + query, expected=400)["code"])
        self.screen(portfolioId=str(uuid.uuid4()), expected=404)
        p = str(uuid.uuid4())
        self.sql(f"INSERT INTO portfolio VALUES ('{p}','SYNTHETIC FUTURE',true,'2026-10-02T00:00:00Z')")
        self.screen(portfolioId=p, expected=404)
        (self.pilot / "screener-reference.json").write_text("{")
        self.assertEqual("REFERENCE_MALFORMED", self.screen(expected=503)["code"])

    def test_filter_page_held_union_nulls_and_quality(self):
        self.fixture(); p = self.portfolio()
        result = self.screen(portfolioId=p, view="all", limit=1)
        self.assertEqual(("PARTIAL", 5, 3, 1, 1), (result["status"], result["summary"]["configured"], result["summary"]["eligible"],
                                                  result["summary"]["ineligible"], result["summary"]["dataBlocked"]))
        self.assertEqual(1, len(result["discoveryIds"])); self.assertEqual(4, len(result["heldIds"])); self.assertEqual(4, len(result["rows"]))
        rows = {r["instrumentId"]: r for r in result["rows"]}
        first = rows[identifier(1)]
        self.assertEqual(("CONFIRMED", "FAST_SWING", 1), (first["setup"], first["mandate"], first["discoveryRank"]))
        self.assertEqual("DEGRADED", first["provenance"]["canonicalQuality"])
        self.assertIsNone(first["ema50"]); self.assertIsNone(first["rs60Pp"])
        self.assertEqual("WARMUP", first["fieldStates"]["ema50"]["availability"])
        self.assertEqual(0, result["marketContext"]["atr14"]); self.assertEqual("NORMAL", result["marketContext"]["volatility"])
        self.assertEqual(9000, first["monetaryLiquidity20Idr"])
        self.assertFalse(rows[identifier(6)]["configured"]); self.assertIsNone(rows[identifier(6)]["discoveryRank"])
        self.assertEqual("2026-09-29", rows[identifier(5)]["marketDate"]); self.assertTrue(rows[identifier(5)]["stale"])
        filtered = self.screen(portfolioId=p, view="all", setup="NONE", eligibility="ELIGIBLE", offset=100, limit=1,
                               inputHash=result["inputHash"])
        self.assertEqual([], filtered["discoveryIds"]); self.assertEqual(result["heldIds"], filtered["heldIds"])
        self.assertEqual(result["summary"], filtered["summary"]); self.assertEqual(result["inputHash"], filtered["inputHash"])
        pages = [self.screen(view="all", offset=n, limit=1)["discoveryIds"][0] for n in range(5)]
        self.assertEqual(5, len(set(pages)))
        historic = self.request("/api/screener?" + urlencode(dict(through="2026-09-15", cutoff="2026-09-15T12:00:00Z")))
        self.assertEqual("BLOCKED", historic["status"]); self.assertIsNone(historic["summary"]["configured"])

    def test_hash_pinning_future_inputs_and_selected_changes(self):
        self.fixture(); p = self.portfolio()
        before = self.screen(portfolioId=p)
        self.assertEqual(before, self.screen(portfolioId=p, inputHash=before["inputHash"]))
        self.append_bar(identifier(1), 2, known="2026-10-02T00:00:00Z")
        self.buy(p, 2, known="2026-10-02T00:00:00Z")
        future = deepcopy(self.document["universes"][0]); future.update(snapshotId="future-universe", knownAt="2026-10-02T00:00:00Z", memberIds=[])
        self.document["universes"].append(future); self.write_reference()
        future_identity = deepcopy(self.document["instruments"][0])
        future_identity.update(snapshotId="future-identity", knownAt="2026-10-02T00:00:00Z")
        future_identity["identities"][0]["symbol"] = "FUTURE"
        self.document["instruments"].append(future_identity); self.write_reference()
        sessions = json.loads((self.pilot / "sessions.json").read_text())
        sessions.append(dict(date=THROUGH, status="AnnouncedClosed", reference="https://reference.example/future", known_at="2026-10-02T00:00:00Z"))
        (self.pilot / "sessions.json").write_text(json.dumps(sessions))
        self.assertEqual(before, self.screen(portfolioId=p, inputHash=before["inputHash"]))
        # A newly inserted fact already visible at the supplied cutoff must invalidate the pin.
        self.append_bar(INDEX, 2, 120)
        self.assertEqual("INPUT_CHANGED", self.screen(portfolioId=p, inputHash=before["inputHash"], expected=409)["code"])
        changed = self.screen(portfolioId=p)
        self.assertNotEqual(before["inputHash"], changed["inputHash"])
        self.buy(p, 2)
        self.screen(portfolioId=p, inputHash=changed["inputHash"], expected=409)
        changed = self.screen(portfolioId=p)
        self.sql(f"INSERT INTO thesis_version SELECT '{uuid.uuid4()}',portfolio_id,instrument_id,2,'INVEST','SYNTHETIC REPLACEMENT',"
                 "'2026-10-01T01:00:00Z',thesis_id,NULL,true FROM thesis_version WHERE version=1")
        self.screen(portfolioId=p, inputHash=changed["inputHash"], expected=409)
        changed = self.screen(portfolioId=p)
        replacement = deepcopy(self.document["universes"][0]); replacement.update(snapshotId="visible-universe", knownAt="2026-10-01T02:00:00Z", memberIds=[identifier(1)])
        self.document["universes"].append(replacement); self.write_reference()
        self.screen(portfolioId=p, inputHash=changed["inputHash"], expected=409)

    @contextmanager
    def locked_bars(self):
        locker = subprocess.Popen(["docker", "compose", "exec", "-T", "postgres", "psql", "-X", "-q", "-A", "-t", "-v", "ON_ERROR_STOP=1",
                                   "-U", "idx_stock", "-d", self.database], cwd=ROOT, stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True)
        try:
            locker.stdin.write("BEGIN; LOCK TABLE daily_bar_revision IN ACCESS EXCLUSIVE MODE; SELECT 'LOCKED';\n"); locker.stdin.flush()
            self.assertEqual("LOCKED", locker.stdout.readline().strip())
            yield locker
        finally:
            locker.communicate("COMMIT;\n", timeout=10)
            if locker.returncode: raise AssertionError("Owned lock cleanup failed.")

    def blocked_reader(self):
        return self.sql("SELECT count(*) FROM pg_stat_activity WHERE datname=current_database() AND wait_event_type='Lock' "
                        "AND query LIKE '%WITH selected AS%'") == "1"

    def test_one_database_snapshot_and_copied_references_survive_concurrent_change(self):
        self.fixture(); p = self.portfolio()
        before = self.screen(portfolioId=p)
        with ThreadPoolExecutor(max_workers=1) as pool:
            with self.locked_bars() as locker:
                pending = pool.submit(self.screen, portfolioId=p)
                self.wait_for(self.blocked_reader, "Screener bar read")
                # Header/events/theses were already selected; later reads must keep that same DB snapshot.
                self.buy(p, 2)
                locker.stdin.write(self.bar_statement(INDEX, 2, 120) + ";\n"); locker.stdin.flush()
                # The copied, parsed bundle must also survive replacement on disk during this request.
                self.document["universes"][0]["memberIds"] = []; self.write_reference()
            same = pending.result(timeout=10)
        self.assertEqual(before, same)
        self.assertNotEqual(before["inputHash"], self.screen(portfolioId=p)["inputHash"])

    def test_http_replays_stock_and_benchmark_a_b_a_with_original_cutoff(self):
        self.fixture()
        for snapshot in self.document["instruments"]:
            snapshot["prices"][0]["contentHashes"] += ["b" * 64, "c" * 64]
        self.write_reference()
        for i, initial, changed in ((identifier(1), 111, 121), (INDEX, 100, 110)):
            self.append_bar(i, 2, changed, "2026-10-01T01:00:00Z")
            self.append_bar(i, 3, initial, "2026-10-01T02:00:00Z")
        def at(cutoff):
            return self.request("/api/screener?" + urlencode(dict(through=THROUGH, cutoff=cutoff)))
        a, b, restored = [at(c) for c in (KNOWN, "2026-10-01T01:00:00Z", "2026-10-01T02:00:00Z")]
        self.assertEqual([111, 121, 111], [r["rows"][0]["close"] for r in (a, b, restored)])
        self.assertEqual([100, 110, 100], [r["marketContext"]["close"] for r in (a, b, restored)])
        self.assertEqual([1, 2, 3], [r["rows"][0]["provenance"]["revision"] for r in (a, b, restored)])
        self.assertEqual(a, at(KNOWN))
        self.assertNotEqual(b["inputHash"], restored["inputHash"])
        self.assertEqual("ELIGIBLE", restored["rows"][0]["eligibility"])

    def test_portfolio_correction_closed_position_and_inactive_thesis_chronology(self):
        self.fixture(); p = self.portfolio(held=(1, 6))
        original = self.screen(portfolioId=p)
        # Correct an earlier buy to a date outside through; existing ledger correction precedence removes it.
        self.sql(f"INSERT INTO portfolio_event(event_id,portfolio_id,instrument_id,event_type,trade_date,known_at,quantity,quantity_unit,price,fees,cash_amount,source,supersedes) "
                 f"SELECT '{uuid.uuid4()}',portfolio_id,instrument_id,event_type,'2026-10-01','2026-10-01T01:00:00Z',quantity,quantity_unit,price,fees,cash_amount,source,event_id "
                 f"FROM portfolio_event WHERE portfolio_id='{p}' AND instrument_id='{identifier(6)}'")
        self.sql(f"INSERT INTO thesis_version SELECT '{uuid.uuid4()}',portfolio_id,instrument_id,2,'INVEST','SYNTHETIC INACTIVE',"
                 "'2026-10-01T01:00:00Z',thesis_id,'SYNTHETIC invalidation',false FROM thesis_version WHERE version=1")
        changed = self.screen(portfolioId=p)
        self.assertNotIn(identifier(6), changed["heldIds"])
        self.assertIsNone(next(r for r in changed["rows"] if r["instrumentId"] == identifier(1))["mandate"])
        prior = self.request("/api/screener?" + urlencode(dict(through=THROUGH, cutoff=KNOWN, portfolioId=p)))
        self.assertIn(identifier(6), prior["heldIds"])
        self.assertEqual("FAST_SWING", next(r for r in prior["rows"] if r["instrumentId"] == identifier(1))["mandate"])
        self.screen(portfolioId=p, inputHash=original["inputHash"], expected=409)
        self.sql(f"INSERT INTO portfolio_event(event_id,portfolio_id,instrument_id,event_type,trade_date,known_at,quantity,quantity_unit,price,fees,cash_amount,source) "
                 f"VALUES ('{uuid.uuid4()}','{p}','{identifier(1)}','SELL','{THROUGH}','2026-10-01T02:00:00Z',100,'SHARES',10,0,0,'USER')")
        self.assertEqual([], self.screen(portfolioId=p)["heldIds"])

    def test_http_disconnect_cancels_database_work(self):
        self.fixture()
        with self.locked_bars():
            connection = http.client.HTTPConnection(self.base.removeprefix("http://"), timeout=5)
            connection.request("GET", "/api/screener?" + urlencode(dict(through=THROUGH, cutoff=CUTOFF)))
            self.wait_for(self.blocked_reader, "Cancellable Screener read")
            connection.close()
            self.wait_for(lambda: not self.blocked_reader(), "Cancelled database command")

    def test_database_command_timeout_returns_sanitized_503(self):
        self.fixture()
        with self.locked_bars():
            started = time.monotonic()
            result = self.screen(expected=503)
            elapsed = time.monotonic() - started
            self.assertGreaterEqual(elapsed, 14); self.assertLess(elapsed, 25)
            self.assertEqual({"code": "SCREENER_UNAVAILABLE", "error": "SCREENER_UNAVAILABLE"}, result)

    def test_complete_exclusions_optional_benchmark_invalid_selected_revision_and_bounds(self):
        self.fixture()
        for snapshot in self.document["instruments"]:
            if snapshot["instrumentId"] != INDEX: snapshot["identities"][0]["classification"] = "UNSUPPORTED"
        self.write_reference()
        complete = self.screen(view="all")
        self.assertEqual(("COMPLETE", 0, 5), (complete["status"], complete["summary"]["candidates"], complete["summary"]["ineligible"]))
        self.document["instruments"][0]["identities"][0]["classification"] = "ORDINARY"
        self.document["instruments"] = [s for s in self.document["instruments"] if s["instrumentId"] != INDEX]
        self.write_reference()
        partial = self.screen()
        self.assertEqual("PARTIAL", partial["status"]); self.assertEqual("ELIGIBLE", partial["rows"][0]["eligibility"])
        self.append_bar(identifier(1), 2, quality="REJECTED")
        blocked = self.screen(view="all")
        row = next(r for r in blocked["rows"] if r["instrumentId"] == identifier(1))
        self.assertEqual("DATA_BLOCKED", row["eligibility"]); self.assertFalse(row["setupEvaluated"])
        self.assertEqual("REJECTED", row["provenance"]["currentEvidence"]["canonicalQuality"])
        self.assertEqual(2, row["provenance"]["currentEvidence"]["revision"])
        self.assertEqual("2026-09-29", row["marketDate"])
        self.assertIsNone(row["priorHigh20"])
        (self.pilot / "screener-reference.json").write_bytes(b" " * (4 * 1024 * 1024 + 1))
        self.assertEqual("REFERENCE_BOUND_EXCEEDED", self.screen(expected=503)["code"])

    def test_portfolio_bounds_are_explicit_service_failures(self):
        self.fixture(); p = self.portfolio(held=())
        self.sql(f"INSERT INTO instrument(instrument_id,issuer_name,instrument_type) SELECT md5('holding-'||i)::uuid,'SYNTHETIC ONLY','EQUITY' FROM generate_series(1,201) i;")
        self.sql(f"INSERT INTO portfolio_event(event_id,portfolio_id,instrument_id,event_type,trade_date,known_at,quantity,quantity_unit,price,fees,cash_amount,source) "
                 f"SELECT md5('event-'||i)::uuid,'{p}',md5('holding-'||i)::uuid,'BUY','{THROUGH}','{KNOWN}',1,'SHARES',1,0,0,'USER' FROM generate_series(1,201) i;")
        result = self.screen(portfolioId=p, expected=503)
        self.assertEqual("SCREENER_UNAVAILABLE", result["code"])
        self.assertNotIn("Password", json.dumps(result)); self.assertNotIn("Host=", json.dumps(result))
        event_bound = self.portfolio(held=())
        self.sql(f"INSERT INTO portfolio_event(event_id,portfolio_id,event_type,trade_date,known_at,quantity,quantity_unit,price,fees,cash_amount,source) "
                 f"SELECT md5('cash-'||i)::uuid,'{event_bound}','CASH_DEPOSIT','{THROUGH}','{KNOWN}',0,'SHARES',0,0,1,'USER' FROM generate_series(1,10001) i;")
        self.assertEqual("SCREENER_UNAVAILABLE", self.screen(portfolioId=event_bound, expected=503)["code"])
        thesis_bound = self.portfolio(held=())
        self.sql(f"INSERT INTO thesis_version(thesis_id,portfolio_id,instrument_id,version,mandate,thesis_text,known_at,active) "
                 f"SELECT md5('thesis-'||i)::uuid,'{thesis_bound}','{identifier(2)}',i,'INVEST','SYNTHETIC ONLY','{KNOWN}',true FROM generate_series(1,2001) i;")
        self.assertEqual("SCREENER_UNAVAILABLE", self.screen(portfolioId=thesis_bound, expected=503)["code"])
