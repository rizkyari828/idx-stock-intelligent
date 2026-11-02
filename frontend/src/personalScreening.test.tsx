import assert from 'node:assert/strict';
import { test } from 'node:test';
import { renderToStaticMarkup } from 'react-dom/server';
import { ScreeningView } from './PersonalScreeningPage.js';
import { defaultScreeningQuery, SCREENING_LIMITATIONS, type ScreeningFeature, type ScreeningReport, type ScreeningRow } from './personalScreening.js';
import { technicalFeatures } from './StocksPage.js';

const feature = (availability: ScreeningFeature['availability'], value: number | null = null,
  reason: string | null = null, minimumSessions = 21): ScreeningFeature => ({ availability, value, reason, minimumSessions });

const features = (overrides: Record<string, ScreeningFeature> = {}): Record<string, ScreeningFeature> =>
  Object.fromEntries(technicalFeatures.map(([name]) =>
    [name, overrides[name] ?? feature('UNAVAILABLE', null, 'VOLUME_BASIS_UNVERIFIED')]));

const rankedFeatures = features({
  changePercent: feature('AVAILABLE', 1.5, null, 2),
  ema20: feature('AVAILABLE', 101.25, null, 20),
  ema50: feature('AVAILABLE', 99.5, null, 50),
  atr14: feature('AVAILABLE', 2.1, null, 15),
  atrPercent: feature('AVAILABLE', 2, null, 15),
  priorHigh20: feature('AVAILABLE', 105, null, 21),
  priorLow20: feature('AVAILABLE', 90, null, 21),
  distanceToHighPercent: feature('AVAILABLE', -3.2, null, 21),
  rs20Pp: feature('AVAILABLE', 1.1, null, 21),
  rs60Pp: feature('AVAILABLE', -0.4, null, 61),
});

const warmupFeatures = features({
  changePercent: feature('WARMUP', null, 'INSUFFICIENT_SESSIONS', 2),
  ema20: feature('WARMUP', null, 'INSUFFICIENT_SESSIONS', 20),
});

const ranked: ScreeningRow = {
  symbol: 'BBCA.JK', name: 'Bank Central Asia Tbk', index: false, marketDate: '2026-10-30', runStart: '2026-08-04',
  sessions: 61, status: 'RANKED', rank: 1, sortValue: 1.5, sortAvailability: 'AVAILABLE', reason: null,
  availableCount: 10, warmupCount: 0, unavailableCount: 3, features: rankedFeatures,
};
const warmup: ScreeningRow = {
  symbol: 'BBRI.JK', name: 'Bank Rakyat Indonesia', index: false, marketDate: '2026-10-30', runStart: '2026-10-30',
  sessions: 1, status: 'NOT_RANKED', rank: null, sortValue: null, sortAvailability: 'WARMUP',
  reason: 'INSUFFICIENT_SESSIONS', availableCount: 0, warmupCount: 2, unavailableCount: 11, features: warmupFeatures,
};
const missing: ScreeningRow = {
  symbol: 'VKTR.JK', name: 'VKTR Teknologi Mobilitas', index: false, marketDate: null, runStart: null, sessions: 0,
  status: 'NO_PERSONAL_CAPTURE', rank: null, sortValue: null, sortAvailability: null,
  reason: 'PERSONAL_CAPTURE_MISSING', availableCount: 0, warmupCount: 0, unavailableCount: 0, features: {},
};

const report: ScreeningReport = {
  source: 'PERSONAL_FILE_STORE', sessionProof: 'UNPROVEN_PERSONAL', surface: 'DESCRIPTIVE_DIAGNOSTICS', panel: 'PILOT',
  through: '2026-10-30', cutoff: '2026-10-30T12:00:00+00:00', sort: 'changePercent', direction: 'DESC',
  availability: 'ALL', limitations: SCREENING_LIMITATIONS, note: 'Only AVAILABLE feature values are ordered.',
  rows: [ranked, warmup, missing],
};

const render = (value: ScreeningReport | null = report, error = '') =>
  renderToStaticMarkup(<ScreeningView report={value} draft={defaultScreeningQuery('2026-10-30')}
    onDraft={()=>{}} onApply={()=>{}} busy={value===null} error={error}/>);

test('header carries every literal limitation before and after a read', () => {
  const html = render();
  for (const line of SCREENING_LIMITATIONS) assert.ok(html.includes(line), line);
  for (const text of ['Descriptive screening', 'Read-only diagnostics', 'PERSONAL', 'Limitations']) {
    assert.ok(html.includes(text), text);
  }
  const loading = render(null);
  for (const line of SCREENING_LIMITATIONS) assert.ok(loading.includes(line), `loading: ${line}`);
});

test('a warmup row is never ranked and is listed after the ranked rows with its reason', () => {
  const html = render();
  assert.match(html, /data-screening-status="RANKED"/);
  assert.match(html, /data-screening-status="NOT_RANKED"/);
  assert.match(html, /data-sort-value="MISSING"/);
  assert.match(html, /NOT RANKED/);
  assert.match(html, /INSUFFICIENT_SESSIONS/);
  assert.match(html, /Change % is WARMUP/);
  assert.ok(html.indexOf('BBRI.JK') > html.indexOf('BBCA.JK'), 'warmup row comes after the ranked row');
  assert.ok(html.indexOf('VKTR.JK') > html.indexOf('BBRI.JK'), 'no-capture row comes last');
  assert.ok(!html.includes('data-sort-value="AVAILABLE">0'), 'a missing value is never rendered as zero');
});

test('every row states its available and non-available feature names', () => {
  const html = render();
  assert.match(html, /10 of 13 AVAILABLE/);
  assert.match(html, /AVAILABLE: changePercent, ema20/);
  assert.match(html, /volumeRatio20/);
  assert.match(html, /VOLUME_BASIS_UNVERIFIED/);
  assert.match(html, /ema20<\/strong> WARMUP/);
  assert.match(html, /0 of 13 AVAILABLE|No personal capture — no feature state is reported\./);
});

test('no capture row is explicit and shows no value', () => {
  const html = render();
  assert.match(html, /NO PERSONAL CAPTURE/);
  assert.match(html, /PERSONAL_CAPTURE_MISSING/);
  assert.match(html, /No personal capture — no feature state is reported\./);
  assert.match(html, /NO CAPTURE/);
});

test('the surface cannot render a readiness or verification token', () => {
  const html = render();
  assert.doesNotMatch(html, /\bVERIFIED\b/);
  assert.doesNotMatch(html, /\bMARKET_ELIGIBLE\b/);
  assert.doesNotMatch(html, /\bDATA_READY\b/);
});

test('an empty filtered result is an explicit empty state, not a fabricated row', () => {
  const html = render({ ...report, rows: [] });
  assert.match(html, /No row matches this filter/);
  assert.match(html, /0/);
  assert.doesNotMatch(html, /data-screening-status/);
});

test('a failed read is reported without hiding the limitations', () => {
  const html = render(null, 'Personal screening is unavailable.');
  assert.match(html, /Personal screening is unavailable\./);
  assert.ok(html.includes(SCREENING_LIMITATIONS[0]));
});
