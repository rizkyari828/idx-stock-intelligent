"""Read-only Decision History browser acceptance using owned snapshot/API fixtures.
Run with IDX_TEST_BROWSER=1 and IDX_TEST_NODE=Node22+ through unittest discovery.
No operational snapshot creation or provider calls.
"""
import json
import os
import subprocess
import unittest
from urllib.request import urlopen
from test_decision_snapshots import DecisionSnapshotAcceptance
import test_screener_evidence as evidence
import test_screener_http as screener


class DecisionHistoryUiAcceptance(DecisionSnapshotAcceptance):
    def test_direct_routes_assets_and_api_errors_remain_separate(self):
        for route in ('/decisions','/decisions/'+screener.identifier(99),'/decisions/bad'):
            with urlopen(type(self).base+route) as response:
                body=response.read().decode()
                self.assertEqual(200,response.status)
                self.assertIn('text/html',response.headers['Content-Type'])
                self.assertIn('<div id="root">',body)
        asset=body.split('src="')[1].split('"')[0]
        with urlopen(type(self).base+asset) as response:
            self.assertEqual(200,response.status)
            self.assertIn('javascript',response.headers['Content-Type'])
        missing=self.request('/api/screener/decision-snapshots/'+screener.identifier(99),expected=404)
        self.assertEqual('SNAPSHOT_NOT_FOUND',missing['code'])
        import urllib.error
        for route in ('/api/not-a-route','/assets/missing.js','/decisions/missing.js','/unrelated-route'):
            with self.assertRaises(urllib.error.HTTPError) as failure: urlopen(type(self).base+route)
            self.assertEqual(404,failure.exception.code)
            failure.exception.close()

    @unittest.skipUnless(os.environ.get('IDX_TEST_BROWSER') == '1', 'Opt-in native Chrome Decision History acceptance')
    def test_decision_history_production_browser(self):
        blocked=self.capture()
        self.current_fixture()
        self.document['instruments']=[s for s in self.document['instruments'] if s['instrumentId']!=screener.identifier(6)]
        self.write_reference()
        p=self.portfolio()
        partial=self.capture(portfolioId=p)
        self.assertEqual('PARTIAL',partial['header']['status'])
        self.assertFalse(next(r for r in partial['rows'] if r['instrumentId']==screener.identifier(6))['setupEvaluated'])
        for _ in range(21): self.capture()
        before={t:self.sql(f"SELECT count(*)||':'||md5(string_agg(to_jsonb(t)::text,',' ORDER BY to_jsonb(t)::text)) FROM {t} t")
                for t in ('decision_snapshot_run','decision_snapshot_row')}
        env,_,_=evidence.environment(type(self).database)
        env['IDX_TEST_DECISION_RUNS']=json.dumps({'blocked':blocked['header']['runId'],'partial':partial['header']['runId']})
        result=subprocess.run([os.environ.get('IDX_TEST_NODE','node'),'scripts/check_product_ui.mjs',type(self).base,p,'decisions',self.directory.name],
                              cwd=evidence.ROOT,env=env,text=True,capture_output=True,timeout=180)
        self.assertEqual(0,result.returncode,result.stdout+result.stderr)
        self.assertEqual(before,{t:self.sql(f"SELECT count(*)||':'||md5(string_agg(to_jsonb(t)::text,',' ORDER BY to_jsonb(t)::text)) FROM {t} t") for t in before})
        print(result.stdout,flush=True)


def load_tests(loader,tests,pattern):
    return unittest.TestSuite(DecisionHistoryUiAcceptance(name) for name in loader.getTestCaseNames(DecisionHistoryUiAcceptance)
                              if name in DecisionHistoryUiAcceptance.__dict__)
