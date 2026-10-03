"""Decision Snapshot acceptance: owned PostgreSQL/loopback only, standard unittest discovery."""
from concurrent.futures import ThreadPoolExecutor
from copy import deepcopy
from datetime import date, datetime, timedelta, timezone
import hashlib
import json
from pathlib import Path
import subprocess
import urllib.error
import urllib.request
from urllib.parse import urlencode
import unittest
import uuid

import test_screener_http as screener
import test_screener_evidence as evidence

ROOT = screener.ROOT
MIGRATIONS = ROOT / "src/IdxStockIntelligence.Infrastructure/Migrations"
PATH = "/api/screener/decision-snapshots"

class DecisionSnapshotAcceptance(screener.ScreenerHttpAcceptance):
    def post(self, body, expected=201):
        request = urllib.request.Request(self.base + PATH, data=json.dumps(body).encode(), headers={"Content-Type":"application/json"})
        try: response = urllib.request.urlopen(request, timeout=65)
        except urllib.error.HTTPError as error: response = error
        with response:
            status, payload = response.status, json.loads(response.read())
        if expected is not None: self.assertEqual(expected, status, payload)
        return status, payload

    def capture(self, expected=201, **intent):
        return self.post(dict(requestId=str(uuid.uuid4()), **intent), expected)[1]

    def current_fixture(self):
        self.fixture()
        today = date.fromisoformat(self.sql("SELECT (clock_timestamp() AT TIME ZONE 'Asia/Jakarta')::date"))
        self.today = today.isoformat()
        # Test-only retained source facts. Add completed weekdays after the old HTTP fixture.
        known = (datetime.now(timezone.utc)-timedelta(minutes=1)).replace(microsecond=0).isoformat()
        sessions = json.loads((self.pilot / "sessions.json").read_text())
        day = date.fromisoformat(screener.THROUGH) + timedelta(days=1)
        while day < today:
            if day.weekday() < 5:
                for i in [screener.identifier(n) for n in range(1,7)] + [screener.INDEX]:
                    close = 100 if i == screener.INDEX else 111 if i in (screener.identifier(1),screener.identifier(6)) else 99 if i==screener.identifier(2) else 90
                    digest=hashlib.sha256(f"synthetic/{i}/{day}/{close}".encode()).hexdigest()
                    self.hashes[i].append(digest)
                    self.sql(f"""INSERT INTO daily_bar_revision(instrument_id,session_date,revision_number,known_at,ingestion_run_id,raw_artifact_id,
                        canonical_content_sha256,open,high,low,close,volume,quality_status,volume_unit,volume_basis,market_segment,retrieved_at,session_reference,session_known_at)
                        VALUES ('{i}','{day}',1,'{known}','{screener.RUN}','{screener.ARTIFACT}','{digest}',{close},{max(100,close)},80,{close},
                            {0 if i==screener.INDEX else 100},'DEGRADED','SHARES','RAW_AS_TRADED','REGULAR','{known}','https://reference.example/session','{known}')""")
                sessions.append(dict(date=day.isoformat(),status="ObservedTrading",reference="https://reference.example/session",known_at=known))
            day += timedelta(days=1)
        for snapshot in self.document["universes"]+self.document["instruments"]:
            snapshot["knownAt"]=known
            for source in snapshot["evidence"]: source.update(knownAt=known,retrievedAt=known)
            if "instrumentId" in snapshot:
                for group in ("prices","volumes"):
                    snapshot[group][0]["contentHashes"]=self.hashes[snapshot["instrumentId"]]
        self.document["instruments"][0]["volumes"]=[]  # Optional volume basis must not block price setup.
        self.write_reference()
        (self.pilot / "sessions.json").write_text(json.dumps(sessions))

    def manifest(self, run):
        return json.loads(self.sql(f"SELECT evidence_manifest FROM decision_snapshot_run WHERE run_id='{run['header']['runId']}'"))

    def test_migration_upgrade_rerun_schema_and_unchanged_source_data(self):
        database="idx_screener_test_"+uuid.uuid4().hex
        self.sql("CREATE DATABASE "+database,"idx_stock_intelligence")
        try:
            for migration in sorted(MIGRATIONS.glob("000[1-4]*.sql")): self.sql(migration.read_text(),database)
            before=evidence.fingerprints(lambda statement,ignored: self.sql(statement,database))
            self.sql((MIGRATIONS/"0005_decision_snapshots.sql").read_text(),database)
            self.sql((MIGRATIONS/"0005_decision_snapshots.sql").read_text(),database)
            self.assertEqual(before,evidence.fingerprints(lambda statement,ignored: self.sql(statement,database)))
            self.assertEqual("2\n4\n5",self.sql("SELECT version FROM pilot_schema_version ORDER BY version",database))
            self.assertEqual("7",self.sql("SELECT count(*) FROM pg_trigger WHERE tgrelid IN ('decision_snapshot_run'::regclass,'decision_snapshot_row'::regclass) AND NOT tgisinternal",database))
            self.assertEqual("7",self.sql("SELECT count(*) FROM pg_indexes WHERE tablename IN ('decision_snapshot_run','decision_snapshot_row')",database))
        finally: self.sql("DROP DATABASE "+database+" WITH (FORCE)","idx_stock_intelligence")

    def test_unknown_universe_zero_row_capture_actual_clock_and_pinned_hash(self):
        before=datetime.now(timezone.utc)
        run=self.capture()
        after=datetime.now(timezone.utc)
        h=run["header"]
        self.assertEqual("BLOCKED",h["status"]);self.assertEqual(0,h["rowCount"]);self.assertEqual([],run["rows"])
        self.assertIsNone(run["result"]["summary"]["configured"])
        captured=datetime.fromisoformat(h["capturedAt"])
        self.assertLessEqual(before,captured);self.assertLessEqual(captured,after)
        self.assertEqual(h["capturedAt"],h["knowledgeCutoff"])
        self.assertGreaterEqual(datetime.fromisoformat(h["recordedAt"]),captured)
        response=self.request("/api/screener?"+urlencode(dict(through=h["through"],cutoff=h["knowledgeCutoff"],view="all")))
        self.assertEqual(response["inputHash"],h["inputHash"])
        self.assertEqual(run,self.request(PATH+"/"+h["runId"]))

    def test_full_population_states_portfolio_optional_features_and_hash(self):
        self.current_fixture();p=self.portfolio()
        run=self.capture(portfolioId=p);h=run["header"]
        self.assertEqual("PARTIAL",h["status"])
        rows={r["instrumentId"]:r for r in run["rows"]}
        self.assertEqual(6,len(rows));self.assertEqual(5,len(run["result"]["allViewIds"]))
        self.assertEqual("CONFIRMED",rows[screener.identifier(1)]["setup"])
        self.assertEqual("WATCH",rows[screener.identifier(2)]["setup"])
        self.assertEqual("NONE",rows[screener.identifier(3)]["setup"])
        self.assertTrue(rows[screener.identifier(3)]["setupEvaluated"])
        self.assertEqual("INELIGIBLE",rows[screener.identifier(4)]["eligibility"])
        self.assertEqual("DATA_BLOCKED",rows[screener.identifier(5)]["eligibility"])
        outside=rows[screener.identifier(6)]
        self.assertFalse(outside["configured"]);self.assertTrue(outside["held"]);self.assertEqual("CONFIRMED",outside["setup"])
        self.assertIsNone(outside["discoveryRank"]);self.assertEqual(100,outside["shares"]);self.assertEqual(10,outside["averageCost"])
        one=rows[screener.identifier(1)]
        self.assertEqual("ELIGIBLE",one["eligibility"]);self.assertIsNone(one["result"]["volumeRatio20"])
        self.assertEqual("WARMUP",one["result"]["fieldStates"]["ema50"]["availability"])
        self.assertEqual("FAST_SWING",one["mandate"]);self.assertEqual(1,one["thesisVersion"])
        self.assertEqual(0,rows[screener.identifier(2)]["shares"]);self.assertIsNone(rows[screener.identifier(2)]["averageCost"])
        response=self.request("/api/screener?"+urlencode(dict(through=h["through"],cutoff=h["knowledgeCutoff"],portfolioId=p,view="all",limit=100)))
        self.assertEqual(response["inputHash"],h["inputHash"]);self.assertEqual(response["summary"],run["result"]["summary"])
        for source in response["rows"]:
            stored=rows[source["instrumentId"]]
            for key in ("setup","setupEvaluated","eligibility","marketDate","close","held","configured","episodeId"):
                if key!="episodeId": self.assertEqual(source[key],stored[key],key)
            for key in ("episode","eligibilityReasons","setupReasons","ema20","ema50","atr14","atrPercent","rs20Pp","rs60Pp","volumeRatio20"):
                self.assertEqual(source[key],stored["result"][key],key)
        self.assertNotIn("thesisText",json.dumps(run));self.assertNotIn("evidenceManifest",json.dumps(run));self.assertNotIn("originatingXid",json.dumps(run))
        self.assertEqual(len(run["rows"]),len(self.manifest(run)["evaluatedInstrumentIds"]))
        if date.fromisoformat(h["through"]).weekday()>=5: self.assertLess(h["targetSession"],h["through"])

    def test_complete_known_exclusions_and_no_portfolio_nulls(self):
        self.current_fixture()
        for snapshot in self.document["instruments"]:
            if snapshot["instrumentId"]!=screener.INDEX: snapshot["identities"][0]["classification"]="UNSUPPORTED"
        self.write_reference()
        run=self.capture()
        self.assertEqual("COMPLETE",run["header"]["status"])
        for row in run["rows"]:
            self.assertEqual("INELIGIBLE",row["eligibility"])
            self.assertIsNone(row["shares"]);self.assertIsNone(row["averageCost"]);self.assertFalse(row["held"])

    def test_retries_conflicts_and_lost_response_recovery_ignore_live_files(self):
        body=dict(requestId=str(uuid.uuid4()))
        _,run=self.post(body)
        (self.pilot/"screener-reference.json").write_text("{malformed")
        self.assertEqual(run,self.post(body,200)[1])  # Same recovery after a client discards its response.
        self.assertEqual(run,self.post(dict(body,through=None,portfolioId=None),200)[1])
        self.assertEqual("REQUEST_ID_CONFLICT",self.post(dict(body,through=run["header"]["through"]),409)[1]["code"])
        self.assertEqual("1",self.sql("SELECT count(*) FROM decision_snapshot_run"))

    def test_concurrent_same_request_one_run_and_different_intent_conflict(self):
        self.current_fixture();p=self.portfolio()
        for different in (False,True):
            key=str(uuid.uuid4());bodies=[dict(requestId=key),dict(requestId=key)]
            if different: bodies[1]["portfolioId"]=p
            with ThreadPoolExecutor(max_workers=2) as pool:
                with self.locked_bars():
                    pending=[pool.submit(self.post,b,None) for b in bodies]
                    self.wait_for(lambda:self.sql("SELECT count(*) FROM pg_stat_activity WHERE datname=current_database() AND wait_event_type='Lock' AND query LIKE '%WITH selected AS%'")=="2","two capture readers")
                responses=[f.result(timeout=20) for f in pending]
            self.assertEqual([201,409] if different else [200,201],sorted(status for status,_ in responses))
            self.assertEqual("1",self.sql(f"SELECT count(*) FROM decision_snapshot_run WHERE request_id='{key}'"))
            if not different:self.assertEqual(responses[0][1],responses[1][1])

    def test_same_transaction_with_concurrent_market_portfolio_and_file_change(self):
        self.current_fixture();p=self.portfolio()
        original=(self.pilot/"screener-reference.json").read_bytes()
        with ThreadPoolExecutor(max_workers=1) as pool:
            with self.locked_bars() as locker:
                pending=pool.submit(self.capture,portfolioId=p)
                self.wait_for(self.blocked_reader,"captured evidence read")
                self.buy(p,2)
                locker.stdin.write(self.bar_statement(screener.identifier(1),2,120)+";\n");locker.stdin.flush()
                (self.pilot/"screener-reference.json").write_text("{malformed")
            run=pending.result(timeout=20)
        rows={r["instrumentId"]:r for r in run["rows"]}
        self.assertFalse(rows[screener.identifier(2)]["held"])
        manifest=self.manifest(run)
        self.assertTrue(all(b["revisionNumber"]==1 for b in manifest["bars"]))
        archive=next(a for a in manifest["archives"] if a["kind"]=="screener-reference")
        self.assertEqual(hashlib.sha256(original).hexdigest(),archive["contentSha256"])
        archived=Path(self.directory.name)/"data/raw/decision-reference"/archive["contentSha256"][:2]/(archive["contentSha256"]+".json")
        self.assertEqual(original,archived.read_bytes())
        self.assertEqual(run,self.request(PATH+"/"+run["header"]["runId"]))

    def test_archive_removed_during_evaluation_prevents_commit(self):
        self.current_fixture()
        digest=hashlib.sha256((self.pilot/"screener-reference.json").read_bytes()).hexdigest()
        with ThreadPoolExecutor(max_workers=1) as pool:
            with self.locked_bars():
                pending=pool.submit(self.capture,503)
                self.wait_for(self.blocked_reader,"capture after archival")
                (Path(self.directory.name)/"data/raw/decision-reference"/digest[:2]/(digest+".json")).unlink()
            error=pending.result(timeout=20)
        self.assertEqual("SNAPSHOT_ARCHIVE_UNAVAILABLE",error["code"])
        self.assertEqual("0",self.sql("SELECT count(*) FROM decision_snapshot_run"))
        self.assertEqual("0",self.sql("SELECT count(*) FROM decision_snapshot_row"))

    def test_failure_after_evaluation_rolls_back_and_same_id_can_retry(self):
        self.current_fixture()
        self.sql("""CREATE FUNCTION fail_snapshot() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RAISE EXCEPTION 'fixture failure'; END $$;
            CREATE TRIGGER fixture_snapshot_failure BEFORE INSERT ON decision_snapshot_row FOR EACH ROW EXECUTE FUNCTION fail_snapshot();""")
        body=dict(requestId=str(uuid.uuid4()))
        self.post(body,503)
        self.assertEqual("0",self.sql("SELECT count(*) FROM decision_snapshot_run"));self.assertEqual("0",self.sql("SELECT count(*) FROM decision_snapshot_row"))
        self.assertTrue(list((Path(self.directory.name)/"data/raw/decision-reference").glob("*/*.json")))
        self.sql("DROP TRIGGER fixture_snapshot_failure ON decision_snapshot_row")
        self.post(body,201)

    def test_database_update_delete_truncate_late_insert_and_completeness_guards(self):
        self.current_fixture();run=self.capture();rid=run["header"]["runId"]
        for statement in ("UPDATE decision_snapshot_run SET status=status", "UPDATE decision_snapshot_row SET close=close",
                          "DELETE FROM decision_snapshot_row","DELETE FROM decision_snapshot_run", "TRUNCATE decision_snapshot_row CASCADE",
                          "TRUNCATE decision_snapshot_run CASCADE"):
            with self.assertRaisesRegex(RuntimeError,"append-only"):self.sql(statement)
        columns=self.sql("SELECT string_agg(column_name,',' ORDER BY ordinal_position) FROM information_schema.columns WHERE table_name='decision_snapshot_row'").split(',')
        projection=','.join("'"+str(uuid.uuid4())+"'" if c=='instrument_id' else c for c in columns)
        with self.assertRaisesRegex(RuntimeError,"creation transaction"):
            self.sql("INSERT INTO decision_snapshot_row SELECT "+projection+" FROM decision_snapshot_row LIMIT 1")
        cols=self.sql("SELECT string_agg(column_name,',' ORDER BY ordinal_position) FROM information_schema.columns WHERE table_name='decision_snapshot_run' AND column_name NOT IN ('recorded_at','originating_xid')").split(',')
        values=','.join("'"+str(uuid.uuid4())+"'" if c in ('run_id','request_id') else c for c in cols)
        with self.assertRaisesRegex(RuntimeError,"Incomplete decision population"):
            self.sql("BEGIN; INSERT INTO decision_snapshot_run("+','.join(cols)+") SELECT "+values+" FROM decision_snapshot_run LIMIT 1; COMMIT;")
        self.assertEqual("1",self.sql("SELECT count(*) FROM decision_snapshot_run"))
        self.assertEqual(run,self.request(PATH+"/"+rid))

    def test_cross_record_date_episode_membership_and_portfolio_guards(self):
        self.current_fixture();run=self.capture();old=run["header"]["runId"]
        header_cols=self.sql("SELECT string_agg(column_name,',' ORDER BY ordinal_position) FROM information_schema.columns WHERE table_name='decision_snapshot_run' AND column_name NOT IN ('recorded_at','originating_xid')").split(',')
        row_cols=self.sql("SELECT string_agg(column_name,',' ORDER BY ordinal_position) FROM information_schema.columns WHERE table_name='decision_snapshot_row'").split(',')
        episode=json.dumps(dict(id="fixture",startDate="2027-09-01",confirmationDate=None,endDate=None,endReason=None,ageSessions=1,confirmedAgeSessions=None,triggerPrice=None,watchThreshold=None))
        cases=[({}, {"market_date":"DATE '2027-09-01'"},"context mismatch"),
               ({}, {"shares":"1","invested_cost":"10","average_cost":"10","held":"true"},"context mismatch"),
               ({}, {"episode_id":"'fixture'", "result":"jsonb_set(result,'{episode}','"+episode+"'::jsonb)"},"episode chronology"),
               ({}, {"episode_id":"'wrong-link'"},"check constraint"),
               ({"evidence_manifest":"jsonb_set(evidence_manifest,'{configuredIds}','"+json.dumps([screener.identifier(1)]*5)+"'::jsonb)"}, {},"manifest mismatch")]
        for headers,rows,reason in cases:
            with self.subTest(reason=reason):
                new=str(uuid.uuid4())
                header_values=','.join("'"+new+"'" if c=='run_id' else "'"+str(uuid.uuid4())+"'" if c=='request_id' else headers.get(c,c) for c in header_cols)
                row_values=','.join("'"+new+"'" if c=='run_id' else rows.get(c,c) for c in row_cols)
                statement="BEGIN; INSERT INTO decision_snapshot_run("+','.join(header_cols)+") SELECT "+header_values+" FROM decision_snapshot_run WHERE run_id='"+old+"'; INSERT INTO decision_snapshot_row SELECT "+row_values+" FROM decision_snapshot_row WHERE run_id='"+old+"'; COMMIT;"
                with self.assertRaisesRegex(RuntimeError,reason):self.sql(statement)
        self.assertEqual("1",self.sql("SELECT count(*) FROM decision_snapshot_run"))

    def test_archive_corruption_and_missing_file_semantics(self):
        run=self.capture();manifest=self.manifest(run)
        archive=next(a for a in manifest["archives"] if a["kind"]=="sessions")
        path=Path(self.directory.name)/"data/raw/decision-reference"/archive["contentSha256"][:2]/(archive["contentSha256"]+".json")
        path.write_bytes(b"xx")
        error=self.capture(expected=503)
        self.assertEqual("SNAPSHOT_ARCHIVE_UNAVAILABLE",error["code"])
        self.post(dict(requestId=run["header"]["requestId"]),200)
        self.assertEqual("1",self.sql("SELECT count(*) FROM decision_snapshot_run"))
        for path in self.pilot.glob("*.json"):path.unlink()
        absent=self.capture()
        self.assertTrue(all(not a["present"] and a["contentSha256"] is None for a in self.manifest(absent)["archives"]))

    def test_read_keyset_history_empty_identity_and_strict_request(self):
        self.current_fixture()
        runs=[self.capture() for _ in range(3)]
        first=self.request(PATH+"?limit=2")
        self.assertEqual(2,len(first["items"]));self.assertIsNotNone(first["nextCursor"])
        second=self.request(PATH+"?"+urlencode(dict(limit=2,cursor=first["nextCursor"])))
        self.assertEqual(1,len(second["items"]));self.assertIsNone(second["nextCursor"])
        ids=[h["runId"] for h in first["items"]+second["items"]]
        self.assertEqual([r["header"]["runId"] for r in reversed(runs)],ids)
        history=self.request("/api/instruments/"+screener.identifier(1)+"/decision-snapshots?limit=2")
        self.assertEqual(2,len(history["items"]));self.assertNotIn("evidenceManifest",json.dumps(history))
        self.request("/api/instruments/"+screener.identifier(1)+"/decision-snapshots?"+urlencode(dict(cursor=first["nextCursor"])),expected=400)
        self.assertEqual([],self.request("/api/instruments/"+str(uuid.uuid4())+"/decision-snapshots")["items"])
        for query in ("limit=0","limit=101","limit=1&limit=2","offset=0","cursor=bad!"):
            self.request(PATH+"?"+query,expected=400)
        self.request(PATH+"/"+str(uuid.uuid4()),expected=404);self.request(PATH+"/bad",expected=400)
        for key in ("cutoff","capturedAt","recordedAt","knownAt","result","policyId","universe","expectedInputHash"):
            self.post(dict(requestId=str(uuid.uuid4()),**{key:"bad"}),400)
        self.post(dict(requestId=str(uuid.uuid4()),through="2026-09-30"),400)
        self.post(dict(requestId=str(uuid.uuid4()),portfolioId=str(uuid.uuid4())),404)

    def test_future_evidence_and_thesis_corrections_do_not_mutate_capture(self):
        self.current_fixture();p=self.portfolio()
        future=(datetime.now(timezone.utc)+timedelta(days=1)).isoformat()
        self.append_bar(screener.identifier(1),2,120,known=future)
        self.buy(p,2,known=future)
        run=self.capture(portfolioId=p)
        self.assertTrue(all(b["revisionNumber"]==1 for b in self.manifest(run)["bars"]))
        self.assertFalse(next(r for r in run["rows"] if r["instrumentId"]==screener.identifier(2))["held"])
        self.sql(f"INSERT INTO thesis_version VALUES ('{uuid.uuid4()}','{p}','{screener.identifier(1)}',2,'LONG_SWING','private future','{future}',NULL,NULL,false)")
        self.assertEqual(run,self.request(PATH+"/"+run["header"]["runId"]))
        self.assertEqual("FAST_SWING",next(r for r in run["rows"] if r["instrumentId"]==screener.identifier(1))["mandate"])

    def test_restored_legacy_transaction_id_cannot_authorize_late_insert(self):
        self.current_fixture();self.capture()
        # Emulate restored legacy metadata matching a current transaction in another cluster.
        child=subprocess.Popen(["docker","compose","exec","-T","postgres","psql","-X","-q","-A","-t","-v","ON_ERROR_STOP=1","-U","idx_stock","-d",self.database],cwd=ROOT,stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=subprocess.PIPE,text=True)
        try:
            child.stdin.write("BEGIN; SELECT pg_current_xact_id();\n");child.stdin.flush()
            transaction=int(child.stdout.readline().strip())
            self.sql(f"ALTER TABLE decision_snapshot_run DISABLE TRIGGER immutable_decision_run; UPDATE decision_snapshot_run SET originating_xid='{transaction}'::xid8; ALTER TABLE decision_snapshot_run ENABLE TRIGGER immutable_decision_run;")
            columns=self.sql("SELECT string_agg(column_name,',' ORDER BY ordinal_position) FROM information_schema.columns WHERE table_name='decision_snapshot_row'").split(',')
            values=','.join("'"+str(uuid.uuid4())+"'" if c=='instrument_id' else "NULL" if c=='discovery_rank' else c for c in columns)
            _,error=child.communicate("INSERT INTO decision_snapshot_row SELECT "+values+" FROM decision_snapshot_row LIMIT 1; COMMIT;\n",timeout=10)
            self.assertNotEqual(0,child.returncode,"A restored legacy ID must not authorize late insertion")
            self.assertIn("creation transaction",error)
        finally:
            if child.poll() is None:child.kill();child.communicate()

    def test_missing_registry_identity_is_still_captured_as_blocked(self):
        missing=str(uuid.uuid4())
        source=dict(id="synthetic",source="synthetic",reference="https://reference.example/test",publishedAt=None,retrievedAt=screener.KNOWN,knownAt=screener.KNOWN)
        self.document["universes"]=[dict(snapshotId="synthetic-missing-identity",knownAt=screener.KNOWN,universeId="PILOT",memberIds=[missing],benchmarkId=screener.INDEX,evidence=[source],contentHash="")]
        self.write_reference()
        run=self.capture()
        self.assertEqual("BLOCKED",run["header"]["status"]);self.assertEqual(1,len(run["rows"]))
        row=run["rows"][0];self.assertEqual(missing,row["instrumentId"]);self.assertEqual("DATA_BLOCKED",row["eligibility"])
        self.assertFalse(row["setupEvaluated"]);self.assertIsNone(row["symbol"])
        self.assertEqual(1,len(self.request("/api/instruments/"+missing+"/decision-snapshots")["items"]))

    def test_population_exceeding_display_page_retains_every_held_row(self):
        self.current_fixture();p=self.portfolio()
        for n in range(100,125):
            i=screener.identifier(n)
            self.sql(f"INSERT INTO instrument(instrument_id,issuer_name) VALUES ('{i}','synthetic outside')")
            self.buy(p,n)
        run=self.capture(portfolioId=p)
        self.assertEqual(31,len(run["rows"]));self.assertEqual(29,len(run["result"]["heldIds"]))
        h=run["header"]
        paged=self.request("/api/screener?"+urlencode(dict(through=h["through"],cutoff=h["knowledgeCutoff"],portfolioId=p,limit=1)))
        self.assertLess(len(paged["rows"]),len(run["rows"]))
        self.assertEqual(h["inputHash"],paged["inputHash"])
        self.assertEqual(31,len(self.manifest(run)["evaluatedInstrumentIds"]))

    def test_transport_body_limits_duplicate_keys_and_reference_bound(self):
        cases=[(b'{}',{"Content-Type":"text/plain"},415),
               (b'{}',{"Content-Type":"application/json","Sec-Fetch-Site":"cross-site"},415),
               ((json.dumps(dict(requestId=str(uuid.uuid4()),padding="x"*65536))).encode(),{"Content-Type":"application/json"},413),
               (b'{"requestId":"10000000-0000-4000-8000-000000000001","requestId":"10000000-0000-4000-8000-000000000001"}',{"Content-Type":"application/json"},400)]
        for body,headers,expected in cases:
            request=urllib.request.Request(self.base+PATH,data=body,headers=headers)
            try:response=urllib.request.urlopen(request,timeout=20)
            except urllib.error.HTTPError as error:response=error
            with response:self.assertEqual(expected,response.status,response.read())
        (self.pilot/"screener-reference.json").write_bytes(b" "*(4*1024*1024+1))
        self.assertEqual("REFERENCE_BOUND_EXCEEDED",self.capture(expected=503)["code"])
        self.assertEqual("0",self.sql("SELECT count(*) FROM decision_snapshot_run"))

    def test_controlled_dump_restore_preserves_committed_capture(self):
        self.current_fixture();run=self.capture();database="idx_screener_test_"+uuid.uuid4().hex
        dump=subprocess.run(["docker","compose","exec","-T","postgres","pg_dump","-U","idx_stock","--no-owner", "--no-privileges",self.database],cwd=ROOT,capture_output=True,text=True,timeout=30)
        self.assertEqual(0,dump.returncode,dump.stderr)
        self.sql("CREATE DATABASE "+database,"idx_stock_intelligence")
        try:
            self.sql(dump.stdout,database)
            self.assertEqual("1",self.sql("SELECT count(*) FROM decision_snapshot_run",database))
            self.assertEqual(str(run["header"]["rowCount"]),self.sql("SELECT count(*) FROM decision_snapshot_row",database))
            self.assertEqual(run["header"]["inputHash"],self.sql("SELECT input_hash FROM decision_snapshot_run",database))
            with self.assertRaisesRegex(RuntimeError,"append-only"):self.sql("DELETE FROM decision_snapshot_row",database)
        finally:self.sql("DROP DATABASE "+database+" WITH (FORCE)","idx_stock_intelligence")


def load_tests(loader, tests, pattern):
    # Reuse fixture methods, not the inherited unrelated acceptance cases.
    return unittest.TestSuite(DecisionSnapshotAcceptance(name) for name in loader.getTestCaseNames(DecisionSnapshotAcceptance) if name in DecisionSnapshotAcceptance.__dict__)
