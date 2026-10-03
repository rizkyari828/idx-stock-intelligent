"""Retained-input verification in owned PostgreSQL/API fixtures, never operational writes."""
from copy import deepcopy
from decimal import Decimal
import hashlib
import json
import os
import fnmatch
from pathlib import Path
import subprocess
import unittest
import urllib.error
import urllib.request
import uuid

from test_decision_snapshots import DecisionSnapshotAcceptance, PATH
import test_screener_evidence as evidence
import test_screener_http as screener


class DecisionVerificationAcceptance(DecisionSnapshotAcceptance):
    def current_fixture(self):
        super().current_fixture()
        # Older HTTP fixtures intentionally used placeholder canonical hashes. This verifier
        # fixture authenticates real PilotValidation.ContentHash serialization before capture.
        rows=json.loads(self.sql("SELECT jsonb_agg(to_jsonb(r)) FROM daily_bar_revision r"))
        replacements={}
        def number(value): return format(Decimal(str(value)).normalize(),'f')
        statements=[]
        for r in rows:
            content=[r['session_date'],*[number(r[k]) for k in ('open','high','low','close')],str(r['volume']),
                     None if r['adjusted_close'] is None else number(r['adjusted_close']),r['volume_unit'],r['volume_basis'],r['market_segment']]
            digest=hashlib.sha256(json.dumps(content,separators=(',',':')).encode()).hexdigest()
            replacements[r['canonical_content_sha256']]=digest
            statements.append(f"UPDATE daily_bar_revision SET canonical_content_sha256='{digest}' WHERE instrument_id='{r['instrument_id']}' AND session_date='{r['session_date']}' AND revision_number={r['revision_number']}")
        self.mutate('daily_bar_revision',';'.join(statements))
        for snapshot in self.document['instruments']:
            for group in ('prices','volumes'):
                for interval in snapshot[group]: interval['contentHashes']=[replacements[h] for h in interval['contentHashes']]
        self.write_reference()

    def mutate(self,table,statement):
        self.assertTrue(type(self).database.startswith('idx_screener_test_'))
        self.sql(f"BEGIN; ALTER TABLE {table} DISABLE TRIGGER USER; {statement}; ALTER TABLE {table} ENABLE TRIGGER USER; COMMIT")

    def snapshot_fingerprints(self):
        return {t:self.sql(f"SELECT count(*)||':'||coalesce(md5(string_agg(to_jsonb(t)::text,',' ORDER BY to_jsonb(t)::text)),'') FROM {t} t")
                for t in ('decision_snapshot_run','decision_snapshot_row')}

    def verify(self,run,state='MATCH',body=None,expected=200):
        before=self.snapshot_fingerprints()
        url=type(self).base+PATH+'/'+run['header']['runId']+'/verify'
        req=urllib.request.Request(url,data=json.dumps({} if body is None else body).encode(),headers={'Content-Type':'application/json'})
        try: response=urllib.request.urlopen(req,timeout=65)
        except urllib.error.HTTPError as error: response=error
        with response: status,payload=response.status,json.loads(response.read())
        self.assertEqual(expected,status,payload)
        if expected==200:
            self.assertEqual(state,payload['state'],payload)
            self.assertEqual(run['header']['capturedAt'],payload['capturedAt'])
            self.assertNotIn('data/raw',json.dumps(payload));self.assertNotIn('originatingXid',json.dumps(payload))
            self.assertLessEqual(len(payload['differences']),100)
        self.assertEqual(before,self.snapshot_fingerprints())
        return payload

    def archive(self,run,kind='screener-reference'):
        a=next(a for a in self.manifest(run)['archives'] if a['kind']==kind)
        h=a['contentSha256']
        return Path(self.directory.name)/'data/raw/decision-reference'/h[:2]/(h+'.json')

    def replace_manifest(self,run,m):
        text=json.dumps(m).replace("'","''")
        self.mutate('decision_snapshot_run',f"UPDATE decision_snapshot_run SET evidence_manifest='{text}'::jsonb WHERE run_id='{run['header']['runId']}'")

    def test_match_reproduces_blocked_absence_and_populated_portfolio(self):
        blocked=self.capture()
        v=self.verify(blocked)
        self.assertEqual(v['storedInputHash'],v['recomputedInputHash']);self.assertEqual(v['storedSelectedDigest'],v['recomputedSelectedDigest'])
        self.assertEqual([],v['differences']);self.assertFalse(v['differencesTruncated'])
        self.current_fixture();p=self.portfolio();run=self.capture(portfolioId=p)
        self.assertEqual('PARTIAL',run['header']['status']);self.assertEqual(6,len(run['rows']))
        self.verify(run);self.verify(blocked) # New rows/references cannot replace captured absence.

    def test_future_and_late_imports_live_files_and_portfolio_corrections_are_isolated(self):
        self.current_fixture();p=self.portfolio();run=self.capture(portfolioId=p)
        self.verify(run)
        self.append_bar(screener.identifier(1),2,close=999) # Late import with old supplied knownAt.
        self.buy(p,2) # Additional event with old knowledge, but absent from the retained manifest.
        old=self.sql(f"SELECT event_id FROM portfolio_event WHERE portfolio_id='{p}' ORDER BY event_order LIMIT 1")
        self.sql(f"INSERT INTO portfolio_event(event_id,portfolio_id,event_type,instrument_id,trade_date,known_at,quantity,quantity_unit,price,fees,cash_amount,source,supersedes) VALUES ('{uuid.uuid4()}','{p}','BUY','{screener.identifier(1)}','{screener.THROUGH}','{screener.KNOWN}',200,'SHARES',20,0,0,'USER','{old}')")
        self.sql(f"INSERT INTO thesis_version VALUES ('{uuid.uuid4()}','{p}','{screener.identifier(1)}',2,'INVEST','SYNTHETIC ONLY','{screener.KNOWN}',NULL,NULL,true)")
        (self.pilot/'screener-reference.json').write_text('{malformed live replacement')
        (self.pilot/'sessions.json').write_text('[]')
        self.verify(run)

    def test_selected_invalid_revision_is_retained_without_fallback(self):
        self.current_fixture()
        self.mutate('daily_bar_revision',f"UPDATE daily_bar_revision SET quality_status='REJECTED' WHERE instrument_id='{screener.identifier(1)}' AND session_date='{screener.THROUGH}'")
        run=self.capture();self.verify(run)
        self.append_bar(screener.identifier(1),2,quality='VALID')
        self.verify(run)

    def test_missing_changed_bytes_and_length_reference_archive(self):
        run=self.capture();path=self.archive(run);original=path.read_bytes()
        path.unlink();self.verify(run,'INPUT_NOT_AVAILABLE')
        path.write_bytes(b'x'+original[1:]);v=self.verify(run,'INPUT_NOT_AVAILABLE');self.assertEqual('REFERENCE_ARCHIVE_HASH_MISMATCH',v['detail'])
        path.write_bytes(original+b' ');v=self.verify(run,'INPUT_NOT_AVAILABLE');self.assertEqual('REFERENCE_ARCHIVE_LENGTH_MISMATCH',v['detail'])
        path.write_bytes(original);self.verify(run)

    def test_hash_link_and_malformed_retained_reference_fail_integrity(self):
        run=self.capture();original=self.manifest(run)
        m=deepcopy(original);m['archives'][0]['contentSha256']='a'*64;self.replace_manifest(run,m)
        self.verify(run,'INPUT_NOT_AVAILABLE')
        data=b'{malformed';h=hashlib.sha256(data).hexdigest()
        path=Path(self.directory.name)/'data/raw/decision-reference'/h[:2]/(h+'.json');path.parent.mkdir(parents=True,exist_ok=True);path.write_bytes(data)
        m=deepcopy(original);m['archives'][0].update(contentSha256=h,byteLength=len(data));self.replace_manifest(run,m)
        v=self.verify(run,'INPUT_NOT_AVAILABLE');self.assertEqual('RETAINED_REFERENCE_INVALID',v['detail'])

    def test_missing_or_changed_exact_canonical_revision_and_listing(self):
        self.current_fixture();run=self.capture();m=self.manifest(run);b=m['bars'][0]
        where=f"instrument_id='{b['instrumentId']}' AND session_date='{b['sessionDate']}' AND revision_number={b['revisionNumber']}"
        self.mutate('daily_bar_revision',f"UPDATE daily_bar_revision SET close=close+1 WHERE {where}")
        v=self.verify(run,'INPUT_NOT_AVAILABLE');self.assertEqual('CANONICAL_REVISION_INTEGRITY_FAILED',v['detail'])
        self.mutate('daily_bar_revision',f"UPDATE daily_bar_revision SET close=close-1 WHERE {where}")
        self.verify(run)
        self.sql(f"""INSERT INTO daily_bar_revision(instrument_id,session_date,revision_number,known_at,ingestion_run_id,raw_artifact_id,
            canonical_content_sha256,open,high,low,close,volume,quality_status,volume_unit,volume_basis,market_segment,retrieved_at,session_reference,session_known_at)
            SELECT instrument_id,session_date,revision_number+1,known_at,ingestion_run_id,raw_artifact_id,canonical_content_sha256,
                open,high,low,close,volume,quality_status,volume_unit,volume_basis,market_segment,retrieved_at,session_reference,session_known_at
            FROM daily_bar_revision WHERE {where}""")
        self.mutate('daily_bar_revision',f"DELETE FROM daily_bar_revision WHERE {where}")
        v=self.verify(run,'INPUT_NOT_AVAILABLE');self.assertEqual('CANONICAL_REVISION_MISSING',v['detail'])

    def test_listing_and_session_identity_mismatch_is_unavailable(self):
        self.current_fixture();run=self.capture();m=self.manifest(run)
        self.mutate('instrument_listing_evidence',f"UPDATE instrument_listing_evidence SET evidence=jsonb_set(evidence,'{{symbol}}','\"CHANGED\"') WHERE instrument_id='{screener.identifier(1)}'")
        self.verify(run,'INPUT_NOT_AVAILABLE')
        # Restore fixture listing bytes, then simulate a malformed retained session linkage.
        self.mutate('instrument_listing_evidence',f"UPDATE instrument_listing_evidence SET evidence=jsonb_set(evidence,'{{symbol}}','\"SYN\"') WHERE instrument_id='{screener.identifier(1)}'")
        self.verify(run)
        m['sessions'][0]['reference']='https://reference.example/changed';self.replace_manifest(run,m)
        v=self.verify(run,'INPUT_NOT_AVAILABLE');self.assertEqual('REFERENCE_SELECTION_MISMATCH',v['detail'])

    def test_unavailable_required_portfolio_event_and_thesis(self):
        self.current_fixture();p=self.portfolio();event_run=self.capture(portfolioId=p)
        other=self.portfolio();run=self.capture(portfolioId=other);m=self.manifest(run)
        thesis=m['portfolio']['thesisIds'][0]
        self.mutate('thesis_version',f"DELETE FROM thesis_version WHERE thesis_id='{thesis}'")
        self.verify(run,'INPUT_NOT_AVAILABLE')
        event=self.manifest(event_run)['portfolio']['eventIds'][0]
        self.mutate('portfolio_event',f"DELETE FROM portfolio_event WHERE event_id='{event}'")
        self.verify(event_run,'INPUT_NOT_AVAILABLE')

    def test_unsupported_policy_returns_successful_unavailable_without_fallback(self):
        run=self.capture()
        self.sql('ALTER TABLE decision_snapshot_run DROP CONSTRAINT decision_snapshot_run_policy_id_check')
        self.mutate('decision_snapshot_run',f"UPDATE decision_snapshot_run SET policy_id='screener-v9.9.9' WHERE run_id='{run['header']['runId']}'")
        self.archive(run).unlink() # Unsupported policy is decided before touching evidence.
        v=self.verify(run,'POLICY_VERSION_UNAVAILABLE');self.assertIsNone(v['recomputedInputHash']);self.assertEqual('screener-v9.9.9',v['policyId'])

    def test_manifest_null_links_missing_listing_and_missing_file_markers(self):
        self.current_fixture();run=self.capture();m=self.manifest(run)
        bad=deepcopy(m);bad['bars'][0]=None;self.replace_manifest(run,bad)
        self.verify(run,'INPUT_NOT_AVAILABLE')
        bad=deepcopy(m);bad['archives'][0]=None;self.replace_manifest(run,bad)
        self.verify(run,'INPUT_NOT_AVAILABLE')
        self.replace_manifest(run,m)
        self.mutate('instrument_listing_evidence',f"DELETE FROM instrument_listing_evidence WHERE instrument_id='{screener.identifier(1)}'")
        v=self.verify(run,'INPUT_NOT_AVAILABLE');self.assertEqual('LISTING_INPUT_MISSING',v['detail'])
        for file in self.pilot.glob('*.json'):file.unlink()
        absent=self.capture();self.verify(absent) # Explicit missing-file markers remain reproducible.
        self.write_reference();self.verify(absent) # Today's new file cannot fill the captured absence.

    def test_different_typed_result_and_hashes_are_bounded_and_never_repaired(self):
        self.current_fixture();run=self.capture();i=run['rows'][0]['instrumentId']
        self.mutate('decision_snapshot_row',f"UPDATE decision_snapshot_row SET result=jsonb_set(result,'{{rs20Pp}}','123.456') WHERE run_id='{run['header']['runId']}' AND instrument_id='{i}'")
        self.mutate('decision_snapshot_run',f"UPDATE decision_snapshot_run SET input_hash=repeat('a',64),selected_digest=repeat('b',64) WHERE run_id='{run['header']['runId']}'")
        v=self.verify(run,'DIFFERENT_RESULT')
        for suffix in ('inputHash','selectedDigest','.result.rs20Pp'): self.assertTrue(any(d['field'].endswith(suffix) for d in v['differences']),v)
        self.assertEqual(123.456,next(r for r in self.request(PATH+'/'+run['header']['runId'])['rows'] if r['instrumentId']==i)['result']['rs20Pp'])

    def test_strict_request_unknown_id_and_service_failure_are_http_errors(self):
        run=self.capture()
        for body in ({'policyId':'screener-v0.1.0'},{'evidence':[]},{'cutoff':screener.KNOWN},[]):
            self.assertEqual('VERIFICATION_REQUEST_INVALID',self.verify(run,body=body,expected=400)['code'])
        for ident,expected in [('bad',400),(str(uuid.UUID(int=0)),400),(str(uuid.uuid4()),404)]:
            fake=deepcopy(run);fake['header']['runId']=ident;self.verify(fake,expected=expected)
        self.sql('ALTER TABLE daily_bar_revision RENAME TO unavailable_revision_table')
        try:self.assertEqual('VERIFICATION_UNAVAILABLE',self.verify(run,expected=503)['code'])
        finally:self.sql('ALTER TABLE unavailable_revision_table RENAME TO daily_bar_revision')
        self.mutate('decision_snapshot_run',f"UPDATE decision_snapshot_run SET result=jsonb_set(result,'{{summary}}','\"invalid typed projection\"') WHERE run_id='{run['header']['runId']}'")
        self.assertEqual('VERIFICATION_UNAVAILABLE',self.verify(run,expected=503)['code'])

    @unittest.skipUnless(os.environ.get('IDX_TEST_BROWSER')=='1','Opt-in native Chrome verification acceptance')
    def test_verification_production_browser(self):
        blocked=self.capture();self.current_fixture();p=self.portfolio();match=self.capture(portfolioId=p)
        unavailable=self.capture();self.archive(unavailable).unlink()
        # Re-archive current copied bytes for the other captures, then isolate missing input via a bad link.
        self.capture();m=self.manifest(unavailable);m['archives'][0]['contentSha256']='f'*64;self.replace_manifest(unavailable,m)
        policy=self.capture();self.sql('ALTER TABLE decision_snapshot_run DROP CONSTRAINT decision_snapshot_run_policy_id_check')
        self.mutate('decision_snapshot_run',f"UPDATE decision_snapshot_run SET policy_id='screener-v9' WHERE run_id='{policy['header']['runId']}'")
        different=self.capture();self.mutate('decision_snapshot_row',f"UPDATE decision_snapshot_row SET result=jsonb_set(result,'{{rs20Pp}}','42') WHERE run_id='{different['header']['runId']}'")
        runs={'match':match['header']['runId'],'blocked':blocked['header']['runId'],'unavailable':unavailable['header']['runId'],'policy':policy['header']['runId'],'different':different['header']['runId']}
        before=self.snapshot_fingerprints();env,_,_=evidence.environment(type(self).database);env['IDX_TEST_VERIFICATION_RUNS']=json.dumps(runs)
        result=subprocess.run([os.environ.get('IDX_TEST_NODE','node'),'scripts/check_product_ui.mjs',type(self).base,p,'verification',self.directory.name],cwd=evidence.ROOT,env=env,text=True,capture_output=True,timeout=180)
        self.assertEqual(0,result.returncode,result.stdout+result.stderr);self.assertEqual(before,self.snapshot_fingerprints());print(result.stdout,flush=True)


def load_tests(loader,tests,pattern):
    return unittest.TestSuite(DecisionVerificationAcceptance(name) for name in loader.getTestCaseNames(DecisionVerificationAcceptance)
                             if name in DecisionVerificationAcceptance.__dict__ and (not loader.testNamePatterns or any(fnmatch.fnmatchcase(name,p) for p in loader.testNamePatterns)))
