"""Research Delivery V0.1 HTTP acceptance using the existing disposable PostgreSQL harness.
Run after dotnet build:
    python3.13 -m unittest discover -s scripts -p test_research_delivery.py -v
Synthetic research facts live only in owned disposable databases; operational tables and
pilot/operation files are fingerprinted before and after. No provider calls.
"""
from datetime import date, datetime, timedelta, timezone
import base64
import hashlib
import json
import os
from pathlib import Path
import socket
import subprocess
import tempfile
import time
import unittest
from urllib.parse import urlencode
import urllib.error
import urllib.request
import uuid

import test_product_slice as product
import test_screener_evidence as evidence

ROOT = product.ROOT
NOW = datetime.now(timezone.utc)
CAPTURED = (NOW - timedelta(days=30)).replace(minute=0, second=0, microsecond=0)
KNOWN = (NOW - timedelta(days=10)).replace(minute=0, second=0, microsecond=0)
RECORDED = KNOWN + timedelta(seconds=1)
CUTOFF = (NOW - timedelta(days=1)).replace(minute=0, second=0, microsecond=0)
CAPTURE_DATE = CAPTURED.astimezone(timezone(timedelta(hours=7))).date()
HORIZON_DATE = CAPTURE_DATE + timedelta(days=1)
CUTOFF_ISO = CUTOFF.strftime("%Y-%m-%dT%H:%M:%SZ")
ANCHOR = date(2026, 8, 24)
PRIVATE_SENTINEL = "RESEARCH-PRIVATE-SENTINEL-7371"


def fingerprints(sql):
    result = evidence.fingerprints(sql)
    for table in ("decision_snapshot_run", "decision_snapshot_row", "decision_snapshot_outcome"):
        result[table] = sql(f"SELECT count(*)||':'||coalesce(md5(string_agg(to_jsonb(t)::text,',' "
                            f"ORDER BY to_jsonb(t)::text)),'') FROM {table} t;", "idx_stock_intelligence")
    return result


class ResearchDeliveryAcceptance(unittest.TestCase):
    sql = classmethod(product.ProductAcceptance.sql.__func__)
    request = classmethod(product.ProductAcceptance.request.__func__)

    @classmethod
    def setUpClass(cls):
        cls.before = fingerprints(cls.sql)
        cls.files_before = evidence.files()

    @classmethod
    def tearDownClass(cls):
        if cls.before != fingerprints(cls.sql): raise AssertionError("Operational database changed.")
        if cls.files_before != evidence.files(): raise AssertionError("Operational reference/operation files changed.")

    def setUp(self):
        if CAPTURE_DATE < ANCHOR:
            self.skipTest("Synthetic capture date predates the frozen research anchor.")
        cls = type(self)
        cls.database = "idx_screener_test_" + uuid.uuid4().hex
        self.sql("CREATE DATABASE " + cls.database, "idx_stock_intelligence")
        self.addCleanup(lambda: self.sql("DROP DATABASE " + cls.database + " WITH (FORCE)", "idx_stock_intelligence"))
        for migration in sorted((ROOT / "src/IdxStockIntelligence.Infrastructure/Migrations").glob("*.sql")):
            self.sql(migration.read_text())
        # Owned fixture: real LIKE types, no production clocks/triggers/checks, like ResearchDatabaseTests.
        self.sql("""
            ALTER TABLE decision_snapshot_run DISABLE TRIGGER USER;
            ALTER TABLE decision_snapshot_row DISABLE TRIGGER USER;
            ALTER TABLE decision_snapshot_outcome DISABLE TRIGGER USER;
            DO $owned$ DECLARE c record; BEGIN
            FOR c IN SELECT conname,conrelid::regclass AS t FROM pg_constraint WHERE contype='c'
                AND conrelid IN ('decision_snapshot_run'::regclass,'decision_snapshot_row'::regclass,
                    'decision_snapshot_outcome'::regclass)
            LOOP EXECUTE format('ALTER TABLE %s DROP CONSTRAINT %I',c.t,c.conname); END LOOP; END $owned$;
            """)
        self.directory = tempfile.TemporaryDirectory(prefix="idx-research-http-")
        self.addCleanup(self.directory.cleanup)
        with socket.socket() as listener:
            listener.bind(("127.0.0.1", 0))
            cls.base = "http://127.0.0.1:" + str(listener.getsockname()[1])
        env, connection, _ = evidence.environment(cls.database)
        env["IDX_DATABASE_CONNECTION"] = connection
        env["IDX_UI_ROOT"] = str(ROOT / "frontend/dist")
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

    def raw(self, path, expected=200):
        request = urllib.request.Request(self.base + path)
        try:
            response = urllib.request.urlopen(request, timeout=30)
        except urllib.error.HTTPError as error:
            response = error
        with response:
            payload = response.read()
            if response.status != expected:
                raise AssertionError(f"{path}: expected {expected}, got {response.status}: {payload.decode()}")
            return payload

    def get(self, pairs, expected=200):
        return json.loads(self.raw(self.url(pairs), expected=expected))

    def error_code(self, pairs, expected):
        return self.get(pairs, expected=expected)["code"]

    @staticmethod
    def url(pairs):
        return "/api/research/outcomes?" + urlencode(pairs)

    @staticmethod
    def params(**overrides):
        values = {"captureFrom": CAPTURE_DATE.isoformat(), "captureTo": CAPTURE_DATE.isoformat()}
        values.update(overrides)
        return [(key, str(value)) for key, value in values.items() if value is not None]

    @staticmethod
    def cursor(captured=CAPTURED, run="20000000-0000-4000-8000-000000000001",
               instrument="10000000-0000-4000-8000-000000001001", context="c" * 64):
        payload = {"capturedAt": captured.strftime("%Y-%m-%dT%H:%M:%S+00:00"), "runId": run,
                   "instrumentId": instrument, "context": context}
        return base64.urlsafe_b64encode(json.dumps(payload).encode()).decode().rstrip("=")

    def add_run(self, key, rows, captured=CAPTURED, through=CAPTURE_DATE, portfolio=None, status="COMPLETE",
                policy="screener-v0.1.0", private=False):
        run_id = f"20000000-0000-4000-8000-{key:012d}"
        request_id = f"30000000-0000-4000-8000-{key:012d}"
        stamp = captured.strftime("%Y-%m-%dT%H:%M:%S+00:00")
        instruments = [f"10000000-0000-4000-8000-{key * 1000 + i:012d}" for i in range(1, rows + 1)]
        manifest = json.dumps({"schemaVersion": 1, "evaluatedInstrumentIds": instruments,
            "request": {"through": through.isoformat(), "cutoff": stamp, "historyAnchor": ANCHOR.isoformat()}})
        result = json.dumps({"reasons": [], "summary": {}, "rankedCandidateIds": [], "allViewIds": [],
            "shortlistIds": [], "heldIds": [], "marketContext": {"trend": "UNKNOWN", "volatility": "UNKNOWN",
                "marketDate": through.isoformat(), "reasons": ["retained-context"]}})
        intent = json.dumps({"through": None, "portfolioId": portfolio})
        self.sql(f"""INSERT INTO decision_snapshot_run(run_id,request_id,schema_version,capture_kind,captured_at,
                knowledge_cutoff,recorded_at,through,history_anchor,target_session,policy_id,universe,universe_snapshot_id,
                portfolio_id,input_hash,selected_digest,status,row_count,request_intent,result,evidence_manifest)
            VALUES ('{run_id}','{request_id}',1,'PROSPECTIVE_CAPTURE','{stamp}','{stamp}','{stamp}','{through.isoformat()}',
                '{ANCHOR.isoformat()}','{through.isoformat()}','{policy}','PILOT','retained-universe',
                {'NULL' if portfolio is None else "'" + portfolio + "'"},repeat('a',64),repeat('b',64),'{status}',{rows},
                '{intent}'::jsonb,'{result}'::jsonb,'{manifest}'::jsonb);""")
        private_columns = ("123456.789,987654.321,777.5,'LONG_SWING','40000000-0000-4000-8000-000000000001',3,true"
                           if private else "NULL,NULL,NULL,NULL,NULL,NULL,NULL")
        for instrument in instruments:
            row_result = {"eligibilityReasons": [], "setupReasons": [], "episode": None, "fieldStates": {}, "provenance": {}}
            self.sql(f"""INSERT INTO decision_snapshot_row(run_id,instrument_id,symbol,configured,held,eligibility,setup,
                    setup_evaluated,market_date,close,shares,invested_cost,average_cost,mandate,thesis_version_id,
                    thesis_version,thesis_active,result)
                VALUES ('{run_id}','{instrument}','SYN',true,false,'ELIGIBLE','NONE',true,'{through.isoformat()}',100,
                    {private_columns},'{json.dumps(row_result)}'::jsonb);""")
        return run_id, instruments

    def add_outcome(self, run_id, instrument, horizon=5, value=None, state="AVAILABLE", reason=None,
                    known=KNOWN, recorded=None, horizon_date=HORIZON_DATE, anchor_close="100", close="110"):
        recorded = recorded or known + timedelta(seconds=1)
        known_text = known.strftime("%Y-%m-%dT%H:%M:%S+00:00")
        recorded_text = recorded.strftime("%Y-%m-%dT%H:%M:%S+00:00")
        reason_text = "NULL" if reason is None else "'" + reason + "'"
        value_text = "NULL" if value is None else "'" + str(value) + "'"
        close_text = "NULL" if close is None else "'" + str(close) + "'"
        self.sql(f"""INSERT INTO decision_snapshot_outcome(run_id,instrument_id,horizon_sessions,schema_version,
                outcome_policy_id,anchor_market_date,anchor_close,horizon_market_date,horizon_close,price_return_pct,
                outcome_state,reason,outcome_known_at,recorded_at,evidence_manifest)
            VALUES ('{run_id}','{instrument}',{horizon},1,'outcome-v0.1.0','{CAPTURE_DATE.isoformat()}',{anchor_close},
                '{horizon_date.isoformat()}',{close_text},{value_text},'{state}',{reason_text},'{known_text}',
                '{recorded_text}','{{"schemaVersion":1,"outcomePolicyId":"outcome-v0.1.0"}}'::jsonb);""")

    def test_strict_query_and_pin_validation(self):
        valid = self.params()
        for pairs in (valid + [("sort", "asc")], valid + [("policy", "latest")], valid + [("format", "CSV")],
                      valid + [("format", "json")], valid + [("limit", "0")], valid + [("limit", "-1")],
                      valid + [("limit", "101")], valid + [("limit", "many")], valid + [("horizonSessions", "0")],
                      valid + [("horizonSessions", "2")], valid + [("horizonSessions", "21")], valid + [("cohort", "all")],
                      valid + [("groupBy", "ALL")], valid + [("portfolioId", "")], valid + [("portfolioId", "null")],
                      valid + [("portfolioId", "00000000-0000-0000-0000-000000000000")], valid + [("instrumentId", "nope")],
                      valid + [("captureFrom", "2026-9-1")], valid + [("captureTo", "2099-01-01")],
                      valid + [("cutoff", "2026-09-30T00:00:00")], valid + [("cutoff", "not-a-time")],
                      valid + [("cutoff", "2099-01-01T00:00:00Z")], valid + [("datasetId", "A" * 64)],
                      valid + [("datasetId", "a" * 63)], valid + [("cursor", "not-a-cursor")],
                      valid + [("cursor", self.cursor())], valid + [("cutoff", CUTOFF_ISO), ("cursor", self.cursor())],
                      valid + [("instrumentId", "10000000-0000-4000-8000-000000001001"),
                          ("episodeId", "screener-v0.1.0/10000000-0000-4000-8000-000000001001/2026-09-30")],
                      valid + [("episodeId", "bogus")],
                      [("captureFrom", CAPTURE_DATE.isoformat())],
                      [("captureTo", CAPTURE_DATE.isoformat())],
                      [("captureFrom", CAPTURE_DATE.isoformat()), ("captureFrom", CAPTURE_DATE.isoformat()),
                       ("captureTo", CAPTURE_DATE.isoformat())],
                      [("captureFrom", CAPTURE_DATE.isoformat()), ("captureTo", CAPTURE_DATE.isoformat()),
                       ("horizonSessions", "5"), ("horizonSessions", "10")],
                      [("captureFrom[]", CAPTURE_DATE.isoformat()), ("captureTo", CAPTURE_DATE.isoformat())]):
            self.assertEqual("RESEARCH_QUERY_INVALID", self.error_code(pairs, 400), pairs)
        export = self.params(cutoff=CUTOFF_ISO, format="EXPORT_JSON")
        for pairs in (self.params(format="EXPORT_JSON"), self.params(cutoff=CUTOFF_ISO, format="EXPORT_JSON"),
                      export + [("limit", "50")], export + [("cursor", self.cursor())]):
            self.assertEqual("RESEARCH_QUERY_INVALID", self.error_code(pairs, 400), pairs)
        pin = "a" * 64
        self.assertEqual("RESEARCH_DATASET_CHANGED",
            self.error_code(self.params(cutoff=CUTOFF_ISO, datasetId=pin), 409))

    def test_first_page_continuation_and_identity_stability(self):
        run_id, instruments = self.add_run(1, 120)
        for index, value in enumerate(("10", "0", "-5")):
            self.add_outcome(run_id, instruments[index], value=value)
        first = self.get(self.params(cutoff=CUTOFF_ISO, limit=100))
        self.assertRegex(first["dataset"]["datasetId"], r"^[0-9a-f]{64}$")
        self.assertEqual(120, first["dataset"]["selectedObservationCount"])
        self.assertEqual({"N": 120, "A": 3, "T": 0, "R": 3, "U": 117},
            {key: first["summary"]["counts"][key.lower()] for key in ("N", "A", "T", "R", "U")})
        self.assertEqual(1, len(first["dataset"]["runs"]))
        self.assertEqual(7, len(first["dataset"]["baseCohortCounts"]))
        self.assertEqual(100, len(first["observations"]))
        self.assertEqual("2.5", first["summary"]["metrics"]["availableCoverage"])
        self.assertTrue(first["dataset"]["cutoff"].endswith("+00:00"))
        cursor = first["page"]["nextCursor"]
        self.assertIsNotNone(cursor)
        second = self.get(self.params(cutoff=CUTOFF_ISO, datasetId=first["dataset"]["datasetId"], cursor=cursor))
        self.assertEqual(first["dataset"]["datasetId"], second["dataset"]["datasetId"])
        self.assertEqual(first["summary"], second["summary"])
        self.assertEqual(20, len(second["observations"]))
        self.assertIsNone(second["page"]["nextCursor"])
        ids = [row["instrumentId"] for row in first["observations"] + second["observations"]]
        self.assertEqual(120, len(set(ids))); self.assertEqual(sorted(ids), ids)
        self.assertEqual(instruments, ids)
        resolved = self.get(self.params(limit=100))
        self.assertTrue(resolved["dataset"]["cutoff"].endswith("+00:00"))
        continued = self.get(self.params(cutoff=resolved["dataset"]["cutoff"], datasetId=resolved["dataset"]["datasetId"],
            cursor=resolved["page"]["nextCursor"]))
        self.assertEqual(resolved["dataset"]["datasetId"], continued["dataset"]["datasetId"])
        self.assertEqual(20, len(continued["observations"]))
        offset_cutoff = CUTOFF.astimezone(timezone(timedelta(hours=7))).strftime("%Y-%m-%dT%H:%M:%S") + "+07:00"
        for params in (self.params(cutoff=CUTOFF_ISO, limit=1), self.params(cutoff=CUTOFF_ISO, limit=50),
                       self.params(cutoff=CUTOFF_ISO, limit=100),
                       self.params(cutoff=CUTOFF_ISO, horizonSessions=5, cohort="ALL", groupBy="NONE"),
                       self.params(cutoff=offset_cutoff),
                       list(reversed(self.params(cutoff=CUTOFF_ISO)))):
            self.assertEqual(first["dataset"]["datasetId"], self.get(params)["dataset"]["datasetId"], params)
        for limit in (1, 50, 100):
            response = self.get(self.params(cutoff=CUTOFF_ISO, limit=limit))
            self.assertEqual(first["dataset"]["datasetId"], response["dataset"]["datasetId"])
            self.assertEqual(first["summary"], response["summary"])
        export = self.get(self.params(cutoff=CUTOFF_ISO, datasetId=first["dataset"]["datasetId"], format="EXPORT_JSON"))
        self.assertEqual(first["dataset"]["datasetId"], export["datasetId"])
        self.assertEqual(120, len(export["preimage"]["observations"]))
        self.assertEqual("research-evaluation-v0.1.0", export["preimage"]["evaluationPolicyId"])
        self.assertEqual("screener-v0.1.0", export["preimage"]["capturePolicyId"])
        self.assertEqual("outcome-v0.1.0", export["preimage"]["outcomePolicyId"])

    def test_late_commit_changes_pin_and_returns_409(self):
        run_id, instruments = self.add_run(2, 3)
        first = self.get(self.params(cutoff=CUTOFF_ISO, limit=2))
        self.assertEqual(3, first["summary"]["counts"]["u"])
        cursor = first["page"]["nextCursor"]
        self.add_outcome(run_id, instruments[0], value="10")
        changed = self.params(cutoff=CUTOFF_ISO, datasetId=first["dataset"]["datasetId"])
        self.assertEqual("RESEARCH_DATASET_CHANGED", self.error_code(changed, 409))
        self.assertEqual("RESEARCH_DATASET_CHANGED", self.error_code(changed + [("cursor", cursor)], 409))
        self.assertEqual("RESEARCH_DATASET_CHANGED",
            self.error_code(self.params(cutoff=CUTOFF_ISO, datasetId=first["dataset"]["datasetId"], format="EXPORT_JSON"), 409))
        fresh = self.get(self.params())
        self.assertNotEqual(first["dataset"]["datasetId"], fresh["dataset"]["datasetId"])
        self.assertEqual((1, 2), (fresh["summary"]["counts"]["a"], fresh["summary"]["counts"]["u"]))

    def test_export_is_lossless_deterministic_complete_and_private(self):
        portfolio = str(uuid.uuid4())
        self.sql(f"INSERT INTO portfolio VALUES ('{portfolio}','{PRIVATE_SENTINEL}',true,'2026-09-01T00:00:00Z')")
        run_id, instruments = self.add_run(3, 60, portfolio=portfolio, private=True)
        values = [("10.5", "10.5"), ("-10.25", "-10.25"), ("0", "0"),
                  ("0.0000000000000000000000000001", "1E-28"),
                  ("7922816251426433759354395033", "7922816251426433759354395033")]
        for index, (value, _) in enumerate(values):
            self.add_outcome(run_id, instruments[index], value=value)
        self.add_outcome(run_id, instruments[5], state="DATA_UNAVAILABLE", reason="SUSPENDED_AT_HORIZON")
        self.add_outcome(run_id, instruments[6], state="BASIS_UNCERTAIN", reason="PRICE_CONVENTION_UNSUPPORTED")
        fixture_before = self.fixture_fingerprint()
        page = self.get(self.params(cutoff=CUTOFF_ISO, limit=50, portfolioId=portfolio))
        pin = page["dataset"]["datasetId"]
        self.assertEqual(50, len(page["observations"])); self.assertIsNotNone(page["page"]["nextCursor"])
        pairs = self.params(cutoff=CUTOFF_ISO, portfolioId=portfolio, datasetId=pin, format="EXPORT_JSON")
        first_bytes = self.raw(self.url(pairs))
        second_bytes = self.raw(self.url(pairs))
        self.assertEqual(first_bytes, second_bytes)
        self.assertEqual(hashlib.sha256(first_bytes).hexdigest(), hashlib.sha256(second_bytes).hexdigest())
        self.assertEqual(fixture_before, self.fixture_fingerprint())
        self.assertNotIn(b"generatedAt", first_bytes)
        document = json.loads(first_bytes)
        self.assertEqual(pin, document["datasetId"])
        observations = document["preimage"]["observations"]
        self.assertEqual(60, len(observations))
        self.assertEqual((60, 5, 2, 7, 53), tuple(document["summary"]["counts"][key] for key in ("n", "a", "t", "r", "u")))
        returns = {row["instrumentId"]: row["outcome"]["priceReturnPct"] for row in observations if row["outcome"]}
        self.assertEqual([text for _, text in values], [returns[instrument] for instrument in instruments[:5]])
        terminal = next(row for row in observations if row["instrumentId"] == instruments[5])
        self.assertEqual("DATA_UNAVAILABLE", terminal["outcome"]["state"])
        self.assertEqual("SUSPENDED_AT_HORIZON", terminal["outcome"]["reason"])
        unresolved = [row for row in observations if row["resolution"] == "UNRESOLVED"]
        self.assertEqual(53, len(unresolved))
        self.assertTrue(all(row["outcome"] is None and row["researchReason"] == "NO_COMMITTED_OUTCOME_AS_OF_CUTOFF"
                            and row["close"] == "100" for row in unresolved))
        text = first_bytes.decode()
        for forbidden in (PRIVATE_SENTINEL, "123456.789", "987654.321", "777.5", "LONG_SWING",
                          '"shares"', '"averageCost"', '"investedCost"', '"thesis"', "outcome_verification"):
            self.assertNotIn(forbidden, text)
        # Future/unrelated evidence and verifier-like activity must not move the pinned dataset.
        later = dict(captured=CAPTURED + timedelta(days=5), through=CAPTURE_DATE + timedelta(days=5))
        other_run, other_instruments = self.add_run(4, 2, **later)
        self.add_outcome(other_run, other_instruments[0], value="999")
        self.sql("CREATE TABLE outcome_verification(run_id text, verdict text); "
                 "INSERT INTO outcome_verification VALUES ('" + run_id + "','DIFFERENT');")
        self.assertEqual(pin, self.get(self.params(cutoff=CUTOFF_ISO, portfolioId=portfolio))["dataset"]["datasetId"])
        self.assertEqual(first_bytes, self.raw(self.url(pairs)))

    def fixture_fingerprint(self):
        hashes = []
        for table in ("decision_snapshot_run", "decision_snapshot_row", "decision_snapshot_outcome"):
            hashes.append(self.sql(f"SELECT count(*)||':'||coalesce(md5(string_agg(to_jsonb(t)::text,',' "
                                   f"ORDER BY to_jsonb(t)::text)),'') FROM {table} t"))
        return hashes

    def test_empty_unresolved_and_unsupported_binding(self):
        empty_pairs = [("captureFrom", ANCHOR.isoformat()), ("captureTo", ANCHOR.isoformat()), ("cutoff", CUTOFF_ISO)]
        empty = self.get(empty_pairs)
        self.assertEqual(0, empty["summary"]["counts"]["n"])
        self.assertIsNone(empty["summary"]["metrics"]["mean"])
        self.assertIsNone(empty["summary"]["metrics"]["availableCoverage"])
        self.assertIsNone(empty["page"]["nextCursor"])
        self.assertEqual(empty["dataset"]["datasetId"], self.get(empty_pairs)["dataset"]["datasetId"])
        export = self.get(empty_pairs + [("datasetId", empty["dataset"]["datasetId"]), ("format", "EXPORT_JSON")])
        self.assertEqual([], export["preimage"]["observations"])
        run_id, instruments = self.add_run(5, 4)
        unresolved = self.get(self.params(cutoff=CUTOFF_ISO))
        self.assertEqual((0, 4, "0"), (unresolved["summary"]["counts"]["a"], unresolved["summary"]["counts"]["u"],
            unresolved["summary"]["metrics"]["availableCoverage"]))
        self.assertIsNone(unresolved["summary"]["metrics"]["mean"])
        self.assertEqual(4, len(self.get(self.params(cutoff=CUTOFF_ISO, datasetId=unresolved["dataset"]["datasetId"],
            format="EXPORT_JSON"))["preimage"]["observations"]))
        pin = unresolved["dataset"]["datasetId"]
        self.sql(f"UPDATE decision_snapshot_run SET policy_id='latest' WHERE run_id='{run_id}'")
        self.assertEqual("RESEARCH_POLICY_VERSION_UNAVAILABLE", self.error_code(self.params(), 409))
        self.assertEqual("RESEARCH_POLICY_VERSION_UNAVAILABLE",
            self.error_code(self.params(cutoff=CUTOFF_ISO, datasetId=pin, format="EXPORT_JSON"), 409))
        self.assertEqual("RESEARCH_POLICY_VERSION_UNAVAILABLE",
            self.error_code(self.params(cutoff=CUTOFF_ISO, datasetId=pin) + [("cursor", self.cursor())], 409))

    def test_cursor_safety_and_bounds(self):
        run_id, instruments = self.add_run(6, 5)
        first = self.get(self.params(cutoff=CUTOFF_ISO, limit=2))
        pin = first["dataset"]["datasetId"]
        cursor = first["page"]["nextCursor"]
        for bad in (cursor[:-1], cursor + "!", "A" * 600, "+++///", base64.urlsafe_b64encode(b"{}").decode().rstrip("=")):
            self.assertEqual("RESEARCH_QUERY_INVALID",
                self.error_code(self.params(cutoff=CUTOFF_ISO, datasetId=pin, cursor=bad), 400), bad)
        horizon_one = self.get(self.params(cutoff=CUTOFF_ISO, horizonSessions=1))
        self.assertEqual("RESEARCH_QUERY_INVALID", self.error_code(self.params(horizonSessions=1, cutoff=CUTOFF_ISO,
            datasetId=horizon_one["dataset"]["datasetId"], cursor=cursor), 400))
        watch = self.get(self.params(cutoff=CUTOFF_ISO, cohort="WATCH"))
        self.assertEqual("RESEARCH_QUERY_INVALID", self.error_code(self.params(cohort="WATCH", cutoff=CUTOFF_ISO,
            datasetId=watch["dataset"]["datasetId"], cursor=cursor), 400))
        self.assertEqual("RESEARCH_DATASET_CHANGED", self.error_code(self.params(cutoff=CUTOFF_ISO, datasetId=pin,
            cohort="WATCH", cursor=cursor), 409))
        self.assertEqual(5, len(self.get(self.params(cutoff=CUTOFF_ISO, limit=100))["observations"]))
        self.assertEqual(5, len(self.get(self.params(cutoff=CUTOFF_ISO, limit=5))["observations"]))
        self.assertEqual("RESEARCH_QUERY_INVALID", self.error_code(self.params(cutoff=CUTOFF_ISO, limit=101), 400))
        self.sql(f"UPDATE decision_snapshot_row SET symbol=repeat('x',2200000) WHERE run_id='{run_id}' "
                 f"AND instrument_id='{instruments[0]}'")
        self.assertEqual("RESEARCH_BOUND_EXCEEDED", self.error_code(self.params(cutoff=CUTOFF_ISO), 503))
        self.assertEqual("RESEARCH_BOUND_EXCEEDED",
            self.error_code(self.params(cutoff=CUTOFF_ISO, datasetId=pin, format="EXPORT_JSON"), 503))


if __name__ == "__main__":
    unittest.main()
