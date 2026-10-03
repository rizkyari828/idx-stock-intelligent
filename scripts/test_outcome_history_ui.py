"""Outcome History production browser acceptance on owned synthetic evidence only."""
from copy import deepcopy
from datetime import date, datetime, timedelta, timezone
import json
import os
import subprocess
import unittest
import uuid
from test_outcome_tracking import OutcomeAcceptance, BASE, END, CAPTURE
import test_screener_evidence as evidence
import test_screener_http as screener

class OutcomeHistoryUiAcceptance(OutcomeAcceptance):
    def write_reference(self):
        if getattr(self,'omit_anchor',False) and self.document['instruments']:
            self.document['instruments'][0]['prices']=[]
        super().write_reference()

    def fixture_outcomes(self):
        self.omit_anchor=True
        positive=self.seed()['header']['runId']
        self.sql("CREATE FUNCTION public.clock_timestamp() RETURNS timestamptz LANGUAGE sql AS $$ SELECT '"+CAPTURE+"'::timestamptz $$")
        self.restart(True)
        negative=self.capture()['header']['runId'];zero=self.capture()['header']['runId']
        self.sql('DROP FUNCTION public.clock_timestamp()');self.restart()
        day=date.fromisoformat(END)
        while day<=date(2026,10,2):
            if day.weekday()<5:self.sessions.append(dict(date=str(day),status='ObservedTrading',reference='https://reference.example/session',known_at=self.later))
            day+=timedelta(days=1)
        (self.pilot/'sessions.json').write_text(json.dumps(self.sessions))
        source='other-source';other=str(uuid.uuid4());artifact=str(uuid.uuid4())
        self.sql(f"INSERT INTO source VALUES ('{source}','SYNTHETIC ONLY','UNKNOWN',NULL,NULL); INSERT INTO ingestion_run VALUES ('{other}','{source}','{self.later}','{self.later}','SUCCEEDED','synthetic','{{}}',NULL); INSERT INTO raw_artifact VALUES ('{artifact}','{other}','{source}','fixture:only','{{}}','{self.later}','fixture:only',repeat('d',64),0,'synthetic')")
        self.sql(f"""INSERT INTO daily_bar_revision(instrument_id,session_date,revision_number,known_at,ingestion_run_id,raw_artifact_id,canonical_content_sha256,
            open,high,low,close,volume,quality_status,volume_unit,volume_basis,market_segment,retrieved_at,session_reference,session_known_at)
            VALUES ('{self.ids[1]}','2026-09-18',1,'{self.later}','{other}','{artifact}','{self.digest('2026-09-18',110)}',110,110,110,110,100,'VALID',
            'SHARES','RAW_AS_TRADED','REGULAR','{self.later}','https://reference.example/session','{self.later}')""")
        for revision,(rid,close) in enumerate([(positive,110),(negative,90),(zero,100)],1):
            self.later=datetime.now(timezone.utc).replace(microsecond=0).isoformat()
            self.bar(self.ids[1],close=close,revision=revision)
            self.reference()
            for r in self.document['instruments'][2:]:
                trading=deepcopy(r['trading'][0]);r['trading']=[]
                for start,end,status,mechanism in [('2026-08-24','2026-09-10','TRADING','CONTINUOUS'),('2026-09-11','2026-09-11','NO_TRADE','CONTINUOUS'),('2026-09-12','2026-10-01','TRADING','CONTINUOUS'),('2026-10-02','2027-08-24','TRADING','CALL_AUCTION')]:
                    r['trading'].append(dict(trading,**{'from':start,'through':end,'status':status,'mechanism':mechanism}))
                p=deepcopy(r['prices'][0]);p['through']='2026-09-17'
                r['prices']=[p,dict(p,**{'from':'2026-09-18','through':'2026-09-18','sourceId':source,'contentHashes':[self.digest('2026-09-18',110)]})]
            self.write_reference();self.rid=rid
            result=self.assess(201);self.assertEqual(close-100,result['cells'][1]['priceReturnPct'])
        # Other horizons remain unmaterialized previews for the mixed positive run.
        preview=self.send(positive,suffix='/outcomes')[1]
        self.assertEqual({'AVAILABLE','ANCHOR_UNAVAILABLE','DATA_UNAVAILABLE','BASIS_UNCERTAIN'},set(c['state'] for c in preview['cells']))
        unresolved=self.capture()['header']['runId']
        self.assertTrue(all(c['resolution']=='UNRESOLVED' for c in self.send(unresolved,suffix='/outcomes')[1]['cells']))
        return dict(positive=positive,negative=negative,zero=zero,unresolved=unresolved)

    @unittest.skipUnless(os.environ.get('IDX_TEST_BROWSER')=='1','Opt-in native Chrome Outcome History acceptance')
    def test_outcome_history_production_browser(self):
        runs=self.fixture_outcomes()
        before={t:self.sql(f"SELECT md5(string_agg(to_jsonb(t)::text,',' ORDER BY to_jsonb(t)::text)) FROM {t} t") for t in ('decision_snapshot_run','decision_snapshot_row')}
        env,_,_=evidence.environment(type(self).database);env['IDX_TEST_OUTCOME_RUNS']=json.dumps(runs)
        result=subprocess.run([os.environ.get('IDX_TEST_NODE','node'),'scripts/check_product_ui.mjs',type(self).base,'','outcomes',self.directory.name],cwd=evidence.ROOT,env=env,text=True,capture_output=True,timeout=180)
        self.assertEqual(0,result.returncode,result.stdout+result.stderr)
        self.assertEqual(before,{t:self.sql(f"SELECT md5(string_agg(to_jsonb(t)::text,',' ORDER BY to_jsonb(t)::text)) FROM {t} t") for t in before})
        self.assertEqual(8,self.count()) # Three initial +1 pairs, one explicit +5 pair.
        print(result.stdout,flush=True)

def load_tests(loader,tests,pattern):
    return loader.loadTestsFromNames(['test_outcome_history_production_browser'],OutcomeHistoryUiAcceptance)
