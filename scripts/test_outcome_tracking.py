"""Outcome V0.1: owned synthetic PostgreSQL/HTTP acceptance, standard discovery.
The disposable database alone supplies a historical clock for initial capture;
that function is removed before every outcome assessment. Production clocks,
retained operational snapshots, evidence and provider endpoints are never changed.
"""
from concurrent.futures import ThreadPoolExecutor
from copy import deepcopy
from datetime import datetime, timedelta, timezone
import hashlib
import json
from pathlib import Path
import subprocess
import unittest
import urllib.error
import urllib.request
import uuid

import test_decision_snapshots as snapshots
import test_screener_evidence as evidence
import test_screener_http as screener

BASE = '2026-09-04'
END = '2026-09-07'
CAPTURE = '2026-09-04T13:00:00Z'
KNOWN = '2026-09-04T12:00:00Z'

class OutcomeAcceptance(snapshots.DecisionSnapshotAcceptance):
    def send(self, rid, body=None, expected=200, suffix='/outcomes/evaluate'):
        request=urllib.request.Request(self.base+snapshots.PATH+'/'+rid+suffix,
            data=None if body is None else json.dumps(body).encode(),headers={'Content-Type':'application/json'})
        try: response=urllib.request.urlopen(request,timeout=65)
        except urllib.error.HTTPError as error: response=error
        with response: status,payload=response.status,json.loads(response.read())
        if expected is not None:self.assertEqual(expected,status,payload)
        return status,payload

    def assess(self, expected=200, n=1):
        return self.send(self.rid,dict(horizonSessions=n),expected)[1]

    def restart(self, historical=False):
        self.stop()
        env,connection,_=evidence.environment(type(self).database)
        env['IDX_DATABASE_CONNECTION']=connection+(';Options=-c search_path=public,pg_catalog' if historical else '')
        env['IDX_UI_ROOT']=str(screener.ROOT/'frontend/dist')
        self.process=subprocess.Popen(['dotnet',str(screener.ROOT/'src/IdxStockIntelligence.Api/bin/Debug/net10.0/IdxStockIntelligence.Api.dll'),
            '--urls',type(self).base],cwd=self.directory.name,env=env,stdout=self.log,stderr=self.log)
        self.wait_for(lambda:len(self.request('/api/instruments'))==2,'owned API restart')

    @staticmethod
    def digest(day,close):
        # DailyBar canonical representation; volume and units are retained unchanged.
        return hashlib.sha256(json.dumps([day,str(close),str(close),str(close),str(close),"100",None,'SHARES','RAW_AS_TRADED','REGULAR'],separators=(',',':')).encode()).hexdigest()

    def bar(self, i, day=END, close=110, source='synthetic', known=None, revision=1):
        known=known or self.later
        self.sql(f"""INSERT INTO daily_bar_revision(instrument_id,session_date,revision_number,known_at,ingestion_run_id,raw_artifact_id,
            canonical_content_sha256,open,high,low,close,volume,quality_status,volume_unit,volume_basis,market_segment,retrieved_at,session_reference,session_known_at)
            VALUES ('{i}','{day}',{revision},'{known}','{screener.RUN}','{screener.ARTIFACT}','{self.digest(day,close)}',
            {close},{close},{close},{close},100,'VALID','SHARES','RAW_AS_TRADED','REGULAR','{known}','https://reference.example/session','{known}')""")

    def seed(self, anchor_basis=True):
        self.ids=[screener.identifier(1),screener.identifier(2)]
        self.later=(datetime.now(timezone.utc)-timedelta(minutes=1)).replace(microsecond=0).isoformat()
        self.sql("INSERT INTO instrument(instrument_id,issuer_name,instrument_type) VALUES "+','.join(f"('{i}','SYNTHETIC ONLY','EQUITY')" for i in self.ids))
        self.sql(f"""INSERT INTO source VALUES ('synthetic','SYNTHETIC ONLY','UNKNOWN',NULL,NULL);
            INSERT INTO ingestion_run VALUES ('{screener.RUN}','synthetic','{KNOWN}','{KNOWN}','SUCCEEDED','synthetic','{{}}',NULL);
            INSERT INTO raw_artifact VALUES ('{screener.ARTIFACT}','{screener.RUN}','synthetic','fixture:only','{{}}','{KNOWN}','fixture:only',repeat('c',64),0,'synthetic');""")
        source=dict(id='proof',source='synthetic',reference='https://reference.example/clearance',publishedAt=None,retrievedAt=KNOWN,knownAt=KNOWN)
        self.document=dict(schemaVersion=1,universes=[dict(snapshotId='pilot',knownAt=KNOWN,universeId='PILOT',memberIds=self.ids,
            benchmarkId=screener.INDEX,evidence=[source],contentHash='')],instruments=[])
        for i in self.ids:
            self.bar(i,BASE,100,known=KNOWN)
            self.document['instruments'].append(dict(snapshotId='capture-'+i,instrumentId=i,knownAt=KNOWN,evidence=[source],contentHash='',
                identities=[{'from':'2026-08-24','through':None,'symbol':'SYN'+i[-1],'displayName':'Synthetic','classification':'ORDINARY','currency':'IDR','board':'MAIN','evidenceIds':['proof']}],
                trading=[{'from':'2026-08-24','through':'2027-08-24','status':'TRADING','mechanism':'CONTINUOUS','evidenceIds':['proof']}],
                prices=[{'from':'2026-08-24','through':'2027-08-24','sourceId':'synthetic','convention':'STOCK_RAW','continuity':'RAW_AS_TRADED','eventCoverage':'CLEARED',
                    'contentHashes':[self.digest(BASE,100)],'evidenceIds':['proof']}] if anchor_basis else [],volumes=[]))
        self.write_reference()
        self.sessions=[dict(date=BASE,status='ObservedTrading',reference='https://reference.example/session',known_at=KNOWN,completed_at=KNOWN)]
        (self.pilot/'sessions.json').write_text(json.dumps(self.sessions))
        self.assertTrue(type(self).database.startswith('idx_screener_test_'))
        self.sql("CREATE FUNCTION public.clock_timestamp() RETURNS timestamptz LANGUAGE sql AS $$ SELECT '"+CAPTURE+"'::timestamptz $$")
        self.restart(historical=True)
        run=self.capture()
        self.rid=run['header']['runId']
        self.assertEqual(BASE,run['header']['targetSession']);self.assertEqual(2,len(run['rows']))
        self.sql('DROP FUNCTION public.clock_timestamp()')
        self.restart()
        return run

    def proof(self, closed=False, day=END):
        self.sessions.append(dict(date=day,status='AnnouncedClosed' if closed else 'ObservedTrading',reference='https://reference.example/session',known_at=self.later))
        (self.pilot/'sessions.json').write_text(json.dumps(self.sessions))

    def reference(self, prices=True, status='TRADING', mechanism='CONTINUOUS', coverage='CLEARED', classification='ORDINARY'):
        new=[]
        for old in self.document['instruments'][:2]:
            r=deepcopy(old);r['snapshotId']=str(uuid.uuid4());r['knownAt']=self.later
            for s in r['evidence']:s.update(knownAt=self.later,retrievedAt=self.later)
            r['identities'][0]['classification']=classification
            r['trading'][0].update(status=status,mechanism=mechanism)
            r['prices']=deepcopy(old['prices']) or [{'from':'2026-08-24','through':'2027-08-24','sourceId':'synthetic','convention':'STOCK_RAW','continuity':'RAW_AS_TRADED','eventCoverage':'CLEARED','contentHashes':[],'evidenceIds':['proof']}]
            r['prices'][0].update(eventCoverage=coverage,contentHashes=[self.digest(BASE,100),self.digest(END,110),self.digest(END,90),self.digest(END,100)])
            if not prices:r['prices']=[]
            new.append(r)
        self.document['instruments']=self.document['instruments'][:2]+new
        self.write_reference()

    def count(self):return int(self.sql('SELECT count(*) FROM decision_snapshot_outcome'))
    def fingerprint(self):return self.sql("SELECT coalesce(md5(string_agg(to_jsonb(t)::text,',' ORDER BY to_jsonb(t)::text)),'') FROM decision_snapshot_outcome t")

    def test_delayed_bar_incremental_subset_actual_clocks_immutable_reads(self):
        self.seed();self.proof();self.reference()
        first=self.assess();self.assertEqual(2,first['summary']['unresolved']);self.assertEqual(0,self.count())
        self.bar(self.ids[0]);a=self.assess(201);self.assertEqual(1,a['newlyMaterializedCount']);self.assertEqual(1,a['summary']['unresolved'])
        old=a['cells'][0];self.assertEqual(10,old['priceReturnPct']);self.assertGreater(old['outcomeKnownAt'],CAPTURE)
        again=self.assess();self.assertEqual(0,again['newlyMaterializedCount']);self.assertEqual(old['recordedAt'],again['cells'][0]['recordedAt'])
        self.bar(self.ids[1]);b=self.assess(201);self.assertEqual(1,b['newlyMaterializedCount']);self.assertEqual(old['outcomeKnownAt'],b['cells'][0]['outcomeKnownAt'])
        before=self.fingerprint();preview=self.send(self.rid,suffix='/outcomes')[1]
        self.assertEqual(8,len(preview['cells']));self.assertEqual(2,preview['summary']['materialized']);self.assertEqual(before,self.fingerprint())
        row=self.send(self.rid,suffix='/rows/'+self.ids[0]+'/outcomes')[1];self.assertEqual(4,len(row['cells']))
        for mutation in ('UPDATE decision_snapshot_outcome SET reason=reason','DELETE FROM decision_snapshot_outcome','TRUNCATE decision_snapshot_outcome'):
            with self.assertRaisesRegex(RuntimeError,'append-only'):self.sql(mutation)
        columns=self.sql("SELECT string_agg(column_name,',' ORDER BY ordinal_position) FROM information_schema.columns WHERE table_name='decision_snapshot_outcome'").split(',')
        for changes,reason in [({'anchor_close':'anchor_close+1'},'capture/date/clock mismatch'),
            ({'evidence_manifest':"jsonb_set(evidence_manifest,'{endpoint,contentHash}',to_jsonb(repeat('0',64)))"},'canonical linkage mismatch'),
            ({'horizon_sessions':'2'},'manifest identity mismatch')]:
            with self.assertRaisesRegex(RuntimeError,reason):
                self.sql('INSERT INTO decision_snapshot_outcome SELECT '+','.join(changes.get(c,c) for c in columns)+' FROM decision_snapshot_outcome LIMIT 1')
        (self.pilot/'screener-reference.json').write_text('{malformed')
        self.assess();self.assertEqual(before,self.fingerprint())

    def test_late_basis_and_generic_coverage_remain_retryable(self):
        self.seed();self.proof()
        for i in self.ids:self.bar(i)
        self.reference(prices=False);self.assertEqual(0,self.assess()['summary']['available']);self.assertEqual(0,self.count())
        self.reference(coverage='UNRESOLVED');self.assertEqual(2,self.assess()['summary']['unresolved']);self.assertEqual(0,self.count())
        self.reference();self.assertEqual(2,self.assess(201)['summary']['available'])

    def test_delayed_session_and_calendar_alignment(self):
        self.seed();self.reference()
        for i in self.ids:self.bar(i)
        self.assertTrue(all(c['state']=='SESSION_UNAVAILABLE' for c in self.assess()['cells']));self.assertEqual(0,self.count())
        self.proof();a=self.assess(201);self.assertTrue(all(c['horizonMarketDate']==END for c in a['cells']))
        # Newly announced closure would move the ordinal; committed cells remain exact.
        self.sessions[-1]['status']='AnnouncedClosed';self.sessions[-1]['known_at']=datetime.now(timezone.utc).isoformat()
        (self.pilot/'sessions.json').write_text(json.dumps(self.sessions));self.assess();self.assertEqual(2,self.count())

    def test_terminal_anchor_cannot_be_repaired_and_positive_status(self):
        self.seed(anchor_basis=False);self.proof();self.reference()
        a=self.assess(201);self.assertTrue(all(c['state']=='ANCHOR_UNAVAILABLE' for c in a['cells']))
        for i in self.ids:self.bar(i)
        self.assertTrue(all(c['state']=='ANCHOR_UNAVAILABLE' for c in self.assess()['cells']))

    def test_positive_suspension_no_trade_and_unsupported_regime(self):
        for status,mechanism,reason in [('SUSPENSION','CONTINUOUS','SUSPENDED_AT_HORIZON'),('NO_TRADE','CONTINUOUS','NO_TRADE_AT_HORIZON'),('TRADING','CALL_AUCTION','SPECIAL_REGIME_UNSUPPORTED')]:
            with self.subTest(reason=reason):
                # Independent prospective run, same immutable anchors and archived inputs.
                if not hasattr(self,'rid'):self.seed();self.proof()
                else:
                    self.reference();self.sql("CREATE FUNCTION public.clock_timestamp() RETURNS timestamptz LANGUAGE sql AS $$ SELECT '"+CAPTURE+"'::timestamptz $$")
                    self.restart(True);self.rid=self.capture()['header']['runId'];self.sql('DROP FUNCTION public.clock_timestamp()');self.restart()
                self.reference(status=status,mechanism=mechanism)
                self.assertTrue(all(c['reason']==reason for c in self.assess(201)['cells']))

    def test_zero_positive_negative_returns_precision_and_no_benchmark(self):
        self.seed();self.proof();self.reference();self.bar(self.ids[0],close=100);self.bar(self.ids[1],close=90)
        a=self.assess(201);self.assertEqual([0,-10],[c['priceReturnPct'] for c in a['cells']])
        before=self.fingerprint()
        for i in self.ids:self.bar(i,close=110,revision=2,known=datetime.now(timezone.utc).isoformat())
        self.assertEqual([0,-10],[c['priceReturnPct'] for c in self.assess()['cells']]);self.assertEqual(before,self.fingerprint())
        self.assertEqual(2,self.count());self.assertEqual('0',self.sql("SELECT count(*) FROM daily_bar_revision WHERE instrument_id='"+screener.INDEX+"'"))
        manifest=json.loads(self.sql('SELECT evidence_manifest FROM decision_snapshot_outcome LIMIT 1'))
        self.assertEqual(3,len(manifest['archives']));self.assertEqual(4,len(manifest['calendar']));self.assertEqual(BASE,manifest['anchor']['sessionDate'])

    def test_concurrent_retry_atomicity_and_missing_evidence(self):
        self.seed();self.proof();self.reference()
        with ThreadPoolExecutor(max_workers=2) as pool:
            responses=list(pool.map(lambda _:self.send(self.rid,dict(horizonSessions=1),None),range(2)))
        self.assertEqual([200,200],sorted(s for s,_ in responses));self.assertEqual(0,self.count())
        for i in self.ids:self.bar(i)
        self.sql("""CREATE FUNCTION fail_outcome() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN IF NEW.instrument_id='"""+self.ids[1]+"""' THEN RAISE EXCEPTION 'synthetic subset failure'; END IF; RETURN NEW; END $$;
            CREATE TRIGGER fixture_fail BEFORE INSERT ON decision_snapshot_outcome FOR EACH ROW EXECUTE FUNCTION fail_outcome();""")
        self.assess(503);self.assertEqual(0,self.count());self.sql('DROP TRIGGER fixture_fail ON decision_snapshot_outcome')
        with ThreadPoolExecutor(max_workers=2) as pool:
            responses=list(pool.map(lambda _:self.send(self.rid,dict(horizonSessions=1),None),range(2)))
        self.assertEqual([200,201],sorted(s for s,_ in responses));self.assertEqual(2,self.count())
        self.assertEqual([0,2],sorted(p['newlyMaterializedCount'] for _,p in responses))

    def test_verified_source_change_terminal_basis_and_later_revision_ignored(self):
        self.seed();self.proof();self.reference()
        self.sql("INSERT INTO source VALUES ('other-source','SYNTHETIC ONLY','UNKNOWN',NULL,NULL)")
        self.sql(f"INSERT INTO ingestion_run VALUES ('{str(uuid.uuid4())}','other-source','{self.later}','{self.later}','SUCCEEDED','synthetic','{{}}',NULL)")
        other=self.sql("SELECT ingestion_run_id FROM ingestion_run WHERE source_id='other-source'")
        artifact=str(uuid.uuid4())
        self.sql(f"INSERT INTO raw_artifact VALUES ('{artifact}','{other}','other-source','fixture:only','{{}}','{self.later}','fixture:only',repeat('d',64),0,'synthetic')")
        for i in self.ids:
            self.sql(f"""INSERT INTO daily_bar_revision(instrument_id,session_date,revision_number,known_at,ingestion_run_id,raw_artifact_id,
                canonical_content_sha256,open,high,low,close,volume,quality_status,volume_unit,volume_basis,market_segment,retrieved_at,session_reference,session_known_at)
                VALUES ('{i}','{END}',1,'{self.later}','{other}','{artifact}','{self.digest(END,110)}',110,110,110,110,100,'VALID','SHARES','RAW_AS_TRADED','REGULAR',
                    '{self.later}','https://reference.example/session','{self.later}')""")
        for r in self.document['instruments'][2:]:r['prices'][0]['sourceId']='other-source'
        self.write_reference()
        a=self.assess(201);self.assertTrue(all(c['state']=='BASIS_UNCERTAIN' for c in a['cells']))
        before=self.fingerprint();self.reference()
        for i in self.ids:self.bar(i,close=100,revision=2,known=datetime.now(timezone.utc).isoformat())
        self.assertTrue(all(c['state']=='BASIS_UNCERTAIN' for c in self.assess()['cells']));self.assertEqual(before,self.fingerprint())

    def test_advisory_wait_is_bounded_and_connection_recovers(self):
        self.seed();self.proof();self.reference()
        for i in self.ids:self.bar(i)
        with self.locked_bars() as locker:
            locker.stdin.write("SELECT pg_advisory_xact_lock(hashtextextended('outcome/"+self.rid+"/1',0)); SELECT 'OUTCOME_LOCKED';\n")
            locker.stdin.flush()
            while locker.stdout.readline().strip()!='OUTCOME_LOCKED':pass
            self.assertEqual('OUTCOME_UNAVAILABLE',self.assess(503)['code'])
            self.assertEqual(0,self.count())
        self.assertEqual(2,self.assess(201)['newlyMaterializedCount'])

    def test_strict_api_and_corrupt_archive_fail_closed(self):
        self.seed();self.proof();self.reference()
        for body in ({},{'horizonSessions':2},{'horizonSessions':1,'cutoff':CAPTURE},{'horizonSessions':'1'}):self.send(self.rid,body,400)
        self.send(str(uuid.uuid4()),{'horizonSessions':1},404)
        archive=self.manifest(self.request(snapshots.PATH+'/'+self.rid))['archives'][0]
        path=Path(self.directory.name)/'data/raw/decision-reference'/archive['contentSha256'][:2]/(archive['contentSha256']+'.json')
        path.write_bytes(b'corrupt');self.assertEqual('OUTCOME_UNAVAILABLE',self.assess(503)['code']);self.assertEqual(0,self.count())

    def test_migration_upgrade_rerun_and_populated_restore(self):
        database='idx_screener_test_'+uuid.uuid4().hex
        self.sql('CREATE DATABASE '+database,'idx_stock_intelligence')
        try:
            for m in sorted(snapshots.MIGRATIONS.glob('000[1-5]*.sql')):self.sql(m.read_text(),database)
            before=evidence.fingerprints(lambda statement,ignored:self.sql(statement,database))
            migration=(snapshots.MIGRATIONS/'0006_decision_snapshot_outcomes.sql').read_text()
            self.sql(migration,database);self.sql(migration,database)
            self.assertEqual(before,evidence.fingerprints(lambda statement,ignored:self.sql(statement,database)))
            self.assertEqual('2\n4\n5\n6',self.sql('SELECT version FROM pilot_schema_version ORDER BY version',database))
            self.assertEqual('3',self.sql("SELECT count(*) FROM pg_trigger WHERE tgrelid='decision_snapshot_outcome'::regclass AND NOT tgisinternal",database))
        finally:self.sql('DROP DATABASE '+database+' WITH (FORCE)','idx_stock_intelligence')
        self.seed();self.proof();self.reference()
        for i in self.ids:self.bar(i)
        self.assess(201);before=self.fingerprint()
        # Existing native dump/restore procedure, owned source and destination only.
        restore='idx_screener_test_'+uuid.uuid4().hex
        self.sql('CREATE DATABASE '+restore,'idx_stock_intelligence')
        try:
            dump=subprocess.run(['docker','compose','exec','-T','postgres','pg_dump','-U','idx_stock','--no-owner','--no-acl',type(self).database],cwd=screener.ROOT,capture_output=True,text=True,check=True).stdout
            self.sql(dump,restore)
            self.assertEqual(before,self.sql("SELECT coalesce(md5(string_agg(to_jsonb(t)::text,',' ORDER BY to_jsonb(t)::text)),'') FROM decision_snapshot_outcome t",restore))
            with self.assertRaisesRegex(RuntimeError,'append-only'):self.sql('DELETE FROM decision_snapshot_outcome',restore)
        finally:self.sql('DROP DATABASE '+restore+' WITH (FORCE)','idx_stock_intelligence')


def load_tests(loader, tests, pattern):
    return unittest.TestSuite(loader.loadTestsFromName(name,OutcomeAcceptance) for name in sorted(OutcomeAcceptance.__dict__) if name.startswith('test_'))
