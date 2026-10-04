"""Exact retained Outcome replay in owned disposable DBs/archives, standard discovery.
No operational writes or provider calls. Corruption fixtures never leave owned DBs.
"""
from copy import deepcopy
from datetime import datetime, timezone
import hashlib
import json
from pathlib import Path
import unittest
import urllib.error
import urllib.request
import uuid

from test_outcome_tracking import OutcomeAcceptance, BASE, END
from test_decision_verification import DecisionVerificationAcceptance
import test_decision_snapshots as snapshots
import test_screener_http as screener


class OutcomeVerificationAcceptance(OutcomeAcceptance):
    mutate = DecisionVerificationAcceptance.mutate
    snapshot_fingerprints = DecisionVerificationAcceptance.snapshot_fingerprints

    def protected(self):
        tables = ('daily_bar_revision', 'raw_artifact', 'raw_fetch_observation', 'ingestion_run',
                  'instrument', 'instrument_history', 'instrument_listing_evidence', 'market_session',
                  'portfolio', 'portfolio_event', 'thesis_version', 'decision_snapshot_run',
                  'decision_snapshot_row', 'decision_snapshot_outcome')
        return {t: self.sql(f"SELECT count(*)||':'||coalesce(md5(string_agg(to_jsonb(t)::text,',' ORDER BY to_jsonb(t)::text)),'') FROM {getattr(self, "canonical_table", t) if t == "daily_bar_revision" else t} t") for t in tables}

    def verify(self, state='MATCH', horizon=1, instrument=None, body=None, expected=200, suffix=''):
        instrument = instrument or self.ids[0]
        before = self.protected()
        files = {p: hashlib.sha256(p.read_bytes()).hexdigest() for p in Path(self.directory.name).rglob('*.json')}
        route = f'{snapshots.PATH}/{self.rid}/rows/{instrument}/outcomes/{horizon}/verify'+suffix
        request = urllib.request.Request(self.base+route, data=json.dumps({} if body is None else body).encode(),
                                         headers={'Content-Type': 'application/json'})
        try: response = urllib.request.urlopen(request, timeout=65)
        except urllib.error.HTTPError as error: response = error
        with response: status, payload = response.status, json.loads(response.read())
        self.assertEqual(expected, status, payload)
        if status == 200:
            self.assertEqual(state, payload['state'], payload)
            self.assertEqual(self.rid, payload['runId']); self.assertEqual(instrument, payload['instrumentId'])
            self.assertLessEqual(len(payload['differences']), 100)
            self.assertNotIn('data/raw', json.dumps(payload)); self.assertNotIn(self.directory.name, json.dumps(payload))
        self.assertEqual(before, self.protected())
        self.assertEqual(files, {p: hashlib.sha256(p.read_bytes()).hexdigest() for p in Path(self.directory.name).rglob('*.json')})
        return payload

    def committed(self, state='AVAILABLE'):
        self.run = self.seed(anchor_basis=state != 'ANCHOR_UNAVAILABLE')
        self.proof(); self.reference(status='SUSPENSION' if state == 'DATA_UNAVAILABLE' else 'TRADING')
        if state == 'AVAILABLE': self.bar(self.ids[0])
        if state == 'BASIS_UNCERTAIN':
            other, artifact = str(uuid.uuid4()), str(uuid.uuid4())
            self.sql("INSERT INTO source VALUES ('other-source','SYNTHETIC ONLY','UNKNOWN',NULL,NULL)")
            self.sql(f"INSERT INTO ingestion_run VALUES ('{other}','other-source','{self.later}','{self.later}','SUCCEEDED','synthetic','{{}}',NULL)")
            self.sql(f"INSERT INTO raw_artifact VALUES ('{artifact}','{other}','other-source','fixture:only','{{}}','{self.later}','fixture:only',repeat('d',64),0,'synthetic')")
            self.bar(self.ids[0])
            self.mutate('daily_bar_revision', f"UPDATE daily_bar_revision SET ingestion_run_id='{other}',raw_artifact_id='{artifact}' WHERE session_date='{END}'")
            for r in self.document['instruments'][2:]: r['prices'][0]['sourceId'] = 'other-source'
            self.write_reference()
        result = self.assess(201)
        self.assertEqual(state, result['cells'][0]['state'])
        return result

    def where(self):
        return f"run_id='{self.rid}' AND instrument_id='{self.ids[0]}' AND horizon_sessions=1"

    def outcome_manifest(self):
        return json.loads(self.sql('SELECT evidence_manifest FROM decision_snapshot_outcome WHERE '+self.where()))

    def replace_outcome_manifest(self, m):
        payload = json.dumps(m).replace("'", "''")
        self.mutate('decision_snapshot_outcome', f"UPDATE decision_snapshot_outcome SET evidence_manifest='{payload}'::jsonb WHERE {self.where()}")

    def outcome_archive(self, kind='screener-reference'):
        a = next(a for a in self.outcome_manifest()['archives'] if a['kind'] == kind)
        h = a['contentSha256']
        return Path(self.directory.name)/'data/raw/decision-reference'/h[:2]/(h+'.json')

    def drop_outcome_checks(self):
        self.assertTrue(type(self).database.startswith('idx_screener_test_'))
        for name in self.sql("SELECT conname FROM pg_constraint WHERE conrelid='decision_snapshot_outcome'::regclass AND contype='c'").splitlines():
            self.sql('ALTER TABLE decision_snapshot_outcome DROP CONSTRAINT '+name)

    def test_available_match_original_clocks_no_writes_and_unresolved_404(self):
        a = self.committed(); result = self.verify()
        self.assertEqual(10, result['replayedProjection']['priceReturnPct'])
        self.assertEqual(a['cells'][0]['outcomeKnownAt'], result['outcomeKnownAt'])
        self.assertEqual(a['cells'][0]['recordedAt'], result['recordedAt'])
        self.assertGreater(result['verifiedAt'], result['recordedAt']); self.assertEqual([], result['differences'])
        self.assertEqual('OUTCOME_NOT_FOUND', self.verify(instrument=self.ids[1], expected=404)['code'])
        self.assertEqual('OUTCOME_NOT_FOUND', self.verify(horizon=5, expected=404)['code'])
        DecisionVerificationAcceptance.verify(self, self.run)

    def test_anchor_unavailable_match_preserves_original_absent_basis(self):
        self.committed('ANCHOR_UNAVAILABLE'); self.verify()
        self.bar(self.ids[0]); self.reference(); self.verify()

    def test_positive_status_and_explicit_endpoint_absence_match(self):
        self.committed('DATA_UNAVAILABLE'); self.verify()
        self.assertIsNone(self.outcome_manifest()['endpoint'])
        self.bar(self.ids[0]); self.reference(); self.verify()

    def test_positive_source_convention_basis_uncertain_match(self):
        self.committed('BASIS_UNCERTAIN'); self.verify()
        self.reference(); self.bar(self.ids[0], close=100, revision=2); self.verify()

    def test_missing_changed_bytes_and_wrong_archive_length(self):
        self.committed(); path = self.outcome_archive(); original = path.read_bytes()
        path.unlink(); self.verify('INPUT_NOT_AVAILABLE')
        path.write_bytes(b'x'+original[1:]); self.assertEqual('REFERENCE_ARCHIVE_HASH_MISMATCH', self.verify('INPUT_NOT_AVAILABLE')['detail'])
        path.write_bytes(original+b' '); self.assertEqual('REFERENCE_ARCHIVE_LENGTH_MISMATCH', self.verify('INPUT_NOT_AVAILABLE')['detail'])
        path.write_bytes(original); self.verify()
        m = self.outcome_manifest(); m['archives'][0]['contentSha256'] = 'f'*64
        self.replace_outcome_manifest(m); self.verify('INPUT_NOT_AVAILABLE')

    def test_malformed_manifests_nulls_duplicate_identities_and_missing_links(self):
        self.committed(); original = self.outcome_manifest(); self.drop_outcome_checks()
        changes = [dict(schemaVersion=2), dict(calendar=None), dict(archives=[None, None, None]),
                   dict(endpoint=None), dict(captureSelectedDigest='f'*64),
                   dict(instruments=original['instruments']*2)]
        for change in changes:
            with self.subTest(change=change):
                m = deepcopy(original); m.update(change); self.replace_outcome_manifest(m); self.verify('INPUT_NOT_AVAILABLE')
        m = deepcopy(original); del m['anchor']; self.replace_outcome_manifest(m); self.verify('INPUT_NOT_AVAILABLE')
        self.replace_outcome_manifest(original)
        capture = self.manifest(self.run); capture['bars'] = None
        DecisionVerificationAcceptance.replace_manifest(self, self.run, capture); self.verify('INPUT_NOT_AVAILABLE')

    def test_missing_exact_canonical_revision_and_corrupt_capture_provenance(self):
        self.committed(); b = self.outcome_manifest()['endpoint']
        where = f"instrument_id='{b['instrumentId']}' AND session_date='{b['sessionDate']}' AND revision_number={b['revisionNumber']}"
        self.mutate('daily_bar_revision', f'UPDATE daily_bar_revision SET open=111,high=111,low=111,close=111 WHERE {where}')
        self.verify('INPUT_NOT_AVAILABLE')
        self.mutate('daily_bar_revision', f'UPDATE daily_bar_revision SET open=110,high=110,low=110,close=110 WHERE {where}'); self.verify()
        self.bar(self.ids[0], revision=2); self.mutate('daily_bar_revision', 'DELETE FROM daily_bar_revision WHERE '+where)
        self.verify('INPUT_NOT_AVAILABLE')

    def test_lost_capture_link_and_required_session_basis_status_identity(self):
        self.committed(); original = self.outcome_manifest()
        for field, value in [('anchor', None), ('instruments', []), ('calendar', original['calendar'][:-1])]:
            with self.subTest(field=field):
                m = deepcopy(original); m[field] = value; self.replace_outcome_manifest(m); self.verify('INPUT_NOT_AVAILABLE')
        self.replace_outcome_manifest(original)
        m = deepcopy(original); m['calendar'][-1]['proof']['reference'] = 'https://reference.example/replacement'
        self.replace_outcome_manifest(m); self.verify('INPUT_NOT_AVAILABLE')

    def test_listing_identity_must_be_retained_and_available(self):
        self.seed(); self.proof(); self.reference()
        listing = dict(instrument_id=self.ids[0], symbol='SYN', issuer_name='Synthetic', listed_from='2026-08-24',
                       delisted_at=BASE, first_trading_date=None, status='VERIFIED', source='synthetic',
                       reference='https://reference.example/listing', publication_reference='Synthetic only', retrieved_at=self.later,
                       known_at=self.later, confidence='VERIFIED', source_version='1', notes='Synthetic only')
        self.sql(f"INSERT INTO instrument_listing_evidence VALUES ('{self.ids[0]}','{self.later}','{json.dumps(listing)}'::jsonb)")
        result = self.assess(201); self.assertEqual('POST_DELISTING', result['cells'][0]['reason']); self.verify()
        self.mutate('instrument_listing_evidence', 'DELETE FROM instrument_listing_evidence'); self.verify('INPUT_NOT_AVAILABLE')

    def test_unknown_outcome_policy_and_schema_precede_broken_inputs(self):
        self.committed(); self.drop_outcome_checks(); self.outcome_archive().unlink()
        for policy, schema in [('outcome-v9.9.9', 1), ('outcome-v0.1.0', 2)]:
            self.mutate('decision_snapshot_outcome', f"UPDATE decision_snapshot_outcome SET outcome_policy_id='{policy}',schema_version={schema},evidence_manifest='{{}}'::jsonb WHERE {self.where()}")
            self.verify('POLICY_VERSION_UNAVAILABLE')

    def test_unknown_capture_policy_precedes_broken_inputs(self):
        self.committed(); self.outcome_archive().unlink()
        self.sql('ALTER TABLE decision_snapshot_run DROP CONSTRAINT decision_snapshot_run_policy_id_check')
        self.mutate('decision_snapshot_run', f"UPDATE decision_snapshot_run SET policy_id='screener-v9' WHERE run_id='{self.rid}'")
        self.verify('POLICY_VERSION_UNAVAILABLE')

    def test_exact_typed_mismatches_do_not_reclassify_as_missing_inputs(self):
        self.committed(); self.drop_outcome_checks()
        for column, changed, original in [('horizon_market_date', "'2026-09-08'", "'2026-09-07'"),
                                          ('anchor_close', '101', '100'), ('horizon_close', '111', '110'),
                                          ('price_return_pct', '11', '10'), ('outcome_state', "'DATA_UNAVAILABLE'", "'AVAILABLE'"),
                                          ('reason', "'SYNTHETIC_REASON'", 'NULL')]:
            with self.subTest(column=column):
                self.mutate('decision_snapshot_outcome', f'UPDATE decision_snapshot_outcome SET {column}={changed} WHERE {self.where()}')
                result = self.verify('DIFFERENT_RESULT'); self.assertEqual(1, len(result['differences']))
                self.assertEqual('TYPED_VALUE_DIFFERENT', result['differences'][0]['reason'])
                self.mutate('decision_snapshot_outcome', f'UPDATE decision_snapshot_outcome SET {column}={original} WHERE {self.where()}')
        self.verify()

    def test_future_revisions_live_files_new_captures_portfolio_and_registry_are_isolated(self):
        self.committed(); self.verify()
        self.bar(self.ids[0], close=999, revision=2) # Late import, even with an old domain knownAt.
        self.bar(self.ids[1]); self.assess(201) # A later sibling outcome does not become a replay input.
        self.reference(coverage='UNRESOLVED', status='SUSPENSION')
        self.sessions[-1].update(status='AnnouncedClosed', known_at=datetime.now(timezone.utc).isoformat())
        (self.pilot/'sessions.json').write_text(json.dumps(self.sessions))
        self.sql(f"UPDATE instrument SET issuer_name='NEW REGISTRY NAME' WHERE instrument_id='{self.ids[0]}'")
        self.portfolio(held=(1,))
        new_run = self.capture() # New snapshot at the current clock, unrelated to the original run.
        self.sql('ALTER TABLE decision_snapshot_run DROP CONSTRAINT decision_snapshot_run_policy_id_check')
        self.mutate('decision_snapshot_run', f"UPDATE decision_snapshot_run SET policy_id='screener-v99' WHERE run_id='{new_run['header']['runId']}'")
        listing = dict(instrument_id=self.ids[0], symbol='NEW', issuer_name='New Listing', listed_from='2026-08-24',
                       delisted_at=None, first_trading_date=None, status='VERIFIED', source='synthetic',
                       reference='https://reference.example/listing', publication_reference='Synthetic', retrieved_at=self.later,
                       known_at=self.later, confidence='VERIFIED', source_version='1', notes='Synthetic only')
        self.sql(f"INSERT INTO instrument_listing_evidence VALUES ('{self.ids[0]}','{self.later}','{json.dumps(listing)}'::jsonb)")
        (self.pilot/'screener-reference.json').write_text('{malformed live replacement')
        (self.pilot/'instrument-sessions.json').write_text(json.dumps([dict(instrument_id=self.ids[0], date=END,
            status='Suspension', reference='https://reference.example/status', known_at=self.later)]))
        self.verify()

    def test_strict_api_not_found_and_service_failure(self):
        self.committed()
        for body in ({'policyId': 'latest'}, {'knownAt': None}, [], {'expectedState': 'MATCH'}): self.verify(body=body, expected=400)
        for horizon in (0, 2, 'invalid'): self.verify(horizon=horizon, expected=400)
        for instrument in ('bad', str(uuid.UUID(int=0))): self.verify(instrument=instrument, expected=400)
        self.verify(instrument=str(uuid.uuid4()), expected=404); self.verify(suffix='?cutoff=now', expected=400)
        for raw in (b'', b'{', b'null', b'{"knownAt":1,"knownAt":2}'):
            request = urllib.request.Request(self.base+snapshots.PATH+'/'+self.rid+'/rows/'+self.ids[0]+'/outcomes/1/verify',
                data=raw, headers={'Content-Type':'application/json'})
            with self.assertRaises(urllib.error.HTTPError) as error: urllib.request.urlopen(request, timeout=65)
            self.assertEqual(400, error.exception.code)
            error.exception.close()
        old = self.rid; self.rid = str(uuid.uuid4()); self.verify(expected=404); self.rid = old
        self.sql('ALTER TABLE daily_bar_revision RENAME TO unavailable_revision_table')
        self.canonical_table = 'unavailable_revision_table'
        try: self.assertEqual('OUTCOME_VERIFICATION_UNAVAILABLE', self.verify(expected=503)['code'])
        finally: self.sql('ALTER TABLE unavailable_revision_table RENAME TO daily_bar_revision')
        self.canonical_table = 'daily_bar_revision'
        self.verify()

    def test_database_wait_is_bounded_and_recovers_without_writes(self):
        self.committed(); before = self.protected()
        with self.locked_bars():
            _, payload = self.send(self.rid, {}, 503, suffix='/rows/'+self.ids[0]+'/outcomes/1/verify')
            self.assertEqual('OUTCOME_VERIFICATION_UNAVAILABLE', payload['code'])
        self.assertEqual(before, self.protected()); self.verify()


def load_tests(loader, tests, pattern):
    return unittest.TestSuite(loader.loadTestsFromName(name, OutcomeVerificationAcceptance)
                             for name in sorted(OutcomeVerificationAcceptance.__dict__) if name.startswith('test_'))
