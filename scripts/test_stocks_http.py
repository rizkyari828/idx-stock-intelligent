"""Focused read-only Stocks acceptance using the existing owned Screener API/DB fixture.
Run: python3 -m unittest discover -s scripts -p 'test_stocks_http.py' -v
IDX_TEST_BROWSER=1 also runs the production React surface in native Chrome.
"""
import os
import subprocess
import unittest
from urllib.parse import urlencode
from urllib.request import urlopen
from test_screener_http import ScreenerHttpAcceptance, identifier, INDEX, THROUGH, CUTOFF
import test_screener_evidence as evidence


class StocksHttpAcceptance(ScreenerHttpAcceptance):
    def history(self, n=1, **query):
        return self.request('/api/instruments/' + (INDEX if n == 'index' else identifier(n)) + '/history?' +
                            urlencode(dict(through=THROUGH, cutoff=CUTOFF, **query)))

    def test_history_through_cutoff_latest_revision_and_bounds(self):
        self.fixture()
        result = self.history(limit=2)
        self.assertEqual(['2026-09-29', THROUGH], [r['date'] for r in result['rows']])
        self.assertEqual('UNKNOWN', result['registry']['currency'])
        self.assertEqual('MARKET_DATE_ASCENDING', result['order'])
        self.assertEqual(111, result['rows'][-1]['close'])
        self.append_bar(identifier(1), 2, close=115, known='2026-10-02T00:00:00Z')
        self.assertEqual(1, self.history()['rows'][-1]['evidence']['revisionNumber'])
        later = self.request('/api/instruments/' + identifier(1) + '/history?through=' + THROUGH + '&cutoff=2026-10-02T12:00:00Z')
        self.assertEqual(115, later['rows'][-1]['close'])
        earlier = self.request('/api/instruments/' + identifier(1) + '/history?through=2026-09-29&cutoff=' + CUTOFF)
        self.assertEqual('2026-09-29', earlier['rows'][-1]['date'])
        before = self.request('/api/instruments/' + identifier(1) + '/history?through=' + THROUGH + '&cutoff=2026-09-30T12:00:00Z')
        self.assertEqual([], before['rows'])
        for limit in (0, 121):
            self.request('/api/instruments/' + identifier(1) + '/history?limit=' + str(limit), expected=400)
        self.request('/api/instruments/' + identifier(99) + '/history', expected=404)
        self.assertEqual(60, self.history()['limit'])
        self.assertEqual(0, self.history('index')['rows'][-1]['volume'])

    def test_rejected_latest_never_falls_back_and_future_provenance_is_filtered(self):
        self.fixture(); self.append_bar(identifier(1), 2, quality='REJECTED')
        row = self.history()['rows'][-1]
        self.assertIsNone(row['close']); self.assertIsNone(row['volume'])
        listing = self.request('/api/instruments?through=' + THROUGH + '&cutoff=' + CUTOFF)
        self.assertIsNone(next(r for r in listing if r['id'] == identifier(1))['close'])
        self.assertEqual(('UNAVAILABLE', 2, 'REJECTED'), (row['availability'], row['evidence']['revisionNumber'], row['evidence']['canonicalQuality']))
        # A later fetched artifact is invisible even when the canonical clock is earlier.
        artifact = '30000000-0000-4000-8000-000000000099'
        self.sql(f"INSERT INTO raw_artifact SELECT '{artifact}',ingestion_run_id,source_id,original_uri,request_parameters,"
                 "'2026-10-02T00:00:00Z',local_uri,repeat('f',64),byte_length,parser_version FROM raw_artifact LIMIT 1")
        statement = self.bar_statement(identifier(2), 2, close=115).replace("raw_artifact_id,repeat", f"'{artifact}',repeat")
        self.sql(statement)
        self.assertEqual(1, self.history(2)['rows'][-1]['evidence']['revisionNumber'])

    def test_registry_search_paging_unknown_and_missing_reference(self):
        self.fixture()
        self.sql(f"INSERT INTO instrument(instrument_id,issuer_name,instrument_type) VALUES ('{identifier(99)}','UNKNOWN ONLY','UNKNOWN');"
                 f"INSERT INTO instrument_history(instrument_id,symbol,valid_from) VALUES ('{identifier(99)}','ZZUNKNOWN','2026-10-02')")
        retained = self.request('/api/instruments?search=SYN')
        self.assertEqual('RETAINED_LISTING', retained[0]['symbolSource'])
        self.assertFalse(retained[0]['hasEffectiveSymbol'])
        self.assertEqual('RETAINED_LISTING', self.history()['registry']['symbolSource'])
        rows = self.request('/api/instruments?search=zzunknown&limit=1')
        self.assertEqual((identifier(99), 'UNKNOWN', None, 'UNKNOWN'),
                         (rows[0]['id'], rows[0]['type'], rows[0]['close'], rows[0]['quality']))
        self.assertEqual([], self.request('/api/instruments?search=absent'))
        self.assertEqual([], self.request('/api/instruments?search=zzunknown&offset=1&limit=1'))
        self.request('/api/instruments?limit=201', expected=400)
        self.request('/api/instruments?offset=10001', expected=400)
        self.assertEqual([], self.history(99)['rows'])
        self.document['instruments'] = []; self.write_reference()
        screen = self.screen(view='all')
        self.assertEqual('DATA_BLOCKED', next(r for r in screen['rows'] if r['instrumentId'] == identifier(1))['eligibility'])
        self.assertNotIn(identifier(99), [r['instrumentId'] for r in screen['rows']])
        for route in ('/stocks', '/stocks/' + identifier(1), '/stocks/bad'):
            with urlopen(type(self).base + route) as response:
                self.assertIn('<div id="root">', response.read().decode())

    def test_portfolio_context_uses_same_cutoff(self):
        self.fixture(); p = self.portfolio(held=(1,))
        view = self.request('/api/portfolios/' + p + '?' + urlencode(dict(through=THROUGH, cutoff=CUTOFF)))
        self.assertEqual(identifier(1), view['holdings'][0]['position']['instrumentId'])
        self.assertNotIn(identifier(2), [h['position']['instrumentId'] for h in view['holdings']])
        older = self.request('/api/portfolios/' + p + '?through=' + THROUGH + '&cutoff=2026-09-30T12:00:00Z')
        self.assertEqual([], older['holdings'])

    @unittest.skipUnless(os.environ.get('IDX_TEST_BROWSER') == '1', 'Opt-in native Chrome Stocks acceptance')
    def test_stocks_production_browser(self):
        self.fixture(); p = self.portfolio(held=(1,))
        self.sql(f"INSERT INTO instrument(instrument_id,issuer_name,instrument_type) VALUES ('{identifier(99)}','UNKNOWN ONLY','UNKNOWN');"
                 f"INSERT INTO instrument_history(instrument_id,symbol,valid_from) VALUES ('{identifier(99)}','ZZUNKNOWN','2026-08-24');"
                 f"INSERT INTO instrument_history(instrument_id,symbol,valid_from) VALUES ('{identifier(1)}','SYN1','2026-08-24')")
        env, _, _ = evidence.environment(type(self).database)
        result = subprocess.run([os.environ.get('IDX_TEST_NODE', 'node'), 'scripts/check_product_ui.mjs',
                                 type(self).base, p, 'stocks', self.directory.name], cwd=evidence.ROOT,
                                env=env, text=True, capture_output=True, timeout=150)
        self.assertEqual(0, result.returncode, result.stdout + result.stderr)
        print(result.stdout)


def load_tests(loader, tests, pattern):
    # Reuse the fixture, without rediscovering its unrelated Screener/portfolio tests.
    return unittest.TestSuite(StocksHttpAcceptance(name) for name in StocksHttpAcceptance.__dict__ if name.startswith('test_'))
