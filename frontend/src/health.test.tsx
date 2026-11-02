import assert from 'node:assert/strict';
import { test } from 'node:test';
import { renderToStaticMarkup } from 'react-dom/server';
import { PersonalBadge, QualityBadge } from './ui.js';
import { DataHealthView, SinceLastSessionCard, TodayCard, type DataHealthDelta, type DataHealthReport } from './dataHealth.js';
import { type PersonalBars } from './personal.js';
import { workspaceRoute } from './decisions.js';

const delta: DataHealthDelta = {
  since: null,
  previousSession: '2026-10-22',
  newSession: '2026-10-23',
  newSessions: ['2026-10-23'],
  changes: [
    { instrumentId: 'instrument-a', symbol: 'BBCA.JK', status: 'COMPARED', previousSession: '2026-10-22', previousClose: 9000, newSession: '2026-10-23', newClose: 9100, change: 100, changePercent: 1.1111111111111112, reason: null },
    { instrumentId: 'instrument-b', symbol: 'BBRI.JK', status: 'MISSING', previousSession: '2026-10-22', previousClose: 4500, newSession: null, newClose: null, change: null, changePercent: null, reason: 'MISSING_NEW_SESSION' }
  ],
  coverageDifferences: [{ instrumentId: 'instrument-b', symbol: 'BBRI.JK', presentIn: 'PREVIOUS_ONLY' }],
  flat: 1, zeroVolume: 2, quality: { DEGRADED: 1 }, degraded: true,
  note: 'Facts only: a close change is not a cause, signal, breakout or recommendation.',
};

const report: DataHealthReport = {
  generatedAt: '2026-10-26T09:37:02+00:00',
  lastSession: '2026-10-23',
  expectedPanel: 11,
  sessions: [
    { date: '2026-10-23', bars: 11, instruments: 11, coverageGap: 0, quality: { VALID: 0, DEGRADED: 11 }, flat: 0, zeroVolume: 0 },
    { date: '2026-08-14', bars: 10, instruments: 10, coverageGap: 1, quality: { VALID: 10 }, flat: 1, zeroVolume: 1 }
  ],
  gaps: [
    { date: '2026-08-17', knownHoliday: true, holidayName: 'Proklamasi Kemerdekaan RI', holidayKind: 'national_holiday' },
    { date: '2026-08-19', knownHoliday: false, holidayName: null, holidayKind: null }
  ],
  anomalies: [{ date: '2026-08-24', instrumentId: 'instrument-1', symbol: 'GOTO.JK', observation: 'FLAT_OHLC', qualityStatus: 'DEGRADED' }],
  quality: { VALID: 10, DEGRADED: 11 },
  degraded: true,
  delta
};

test('DEGRADED badge appears for every non-VALID quality and is absent for VALID', () => {
  for (const quality of ['DEGRADED', 'STALE', 'REJECTED', 'UNKNOWN']) {
    const html = renderToStaticMarkup(<QualityBadge quality={quality}/>);
    assert.match(html, /DEGRADED/, quality);
    assert.match(html, /Personal, unverified data/, quality);
    assert.match(html, /not an exchange tape/, quality);
  }
  assert.equal(renderToStaticMarkup(<QualityBadge quality="VALID"/>), '');
  assert.equal(renderToStaticMarkup(<QualityBadge quality={null}/>), '');
});

test('Data Health renders sessions, gaps with holiday markers and column observations', () => {
  const html = renderToStaticMarkup(<DataHealthView report={report}/>);
  for (const text of ['Data Health', '2026-10-23', 'GOTO.JK', 'FLAT_OHLC', 'Proklamasi Kemerdekaan RI',
    'Holiday', 'No retained session row', 'DEGRADED', 'do not prove', 'not an exchange tape']) {
    assert.ok(html.includes(text), text);
  }
  assert.match(html, /11\/11 panel instruments/);
});

test('Today card links to Data Health and reports last session, bars and degraded flag', () => {
  const html = renderToStaticMarkup(<TodayCard report={report} onNavigate={()=>{}}/>);
  for (const text of ['Today', '2026-10-23', 'Bars ingested', 'DEGRADED', 'href="/data-health"', 'Personal, unverified data']) {
    assert.ok(html.includes(text), text);
  }
  const failed = renderToStaticMarkup(<TodayCard report={null} error="Data health is unavailable." onNavigate={()=>{}}/>);
  assert.match(failed, /Data health is unavailable/);
});

test('empty data health never invents a session or a gap', () => {
  const empty: DataHealthReport = { ...report, lastSession: null, sessions: [], gaps: [], anomalies: [], quality: {}, degraded: false };
  const html = renderToStaticMarkup(<DataHealthView report={empty}/>);
  assert.match(html, /No canonical sessions/);
  assert.match(html, /NONE/);
  assert.doesNotMatch(html, /Proklamasi Kemerdekaan RI/);
});

test('the SPA route resolves Data Health without disturbing stock routes', () => {
  assert.equal(workspaceRoute('/data-health').page, 'health');
  assert.equal(workspaceRoute('/stocks').page, 'stocks');
});

const personalBars: PersonalBars = {
  source: 'PERSONAL_FILE_STORE',
  generatedAt: '2026-10-27T02:00:00Z',
  sessions: [{
    sessionDate: '2026-10-26',
    sessionProof: 'UNPROVEN_PERSONAL',
    capturedAt: '2026-10-26T11:00:00+00:00',
    rows: [
      { symbol: 'BBCA.JK', open: '100.25', high: '101.5', low: '99.75', close: '100.50', adjustedClose: '100.50', volume: 1234567, qualityStatus: 'UNPROVEN_PERSONAL' },
      { symbol: 'TLKM.JK', open: '3', high: '3.1', low: '2.9', close: '3.05', adjustedClose: '3.05', volume: 40, qualityStatus: 'UNPROVEN_PERSONAL' }
    ]
  }]
};

test('PERSONAL badge is distinct from DEGRADED and never claims verification', () => {
  const personal = renderToStaticMarkup(<PersonalBadge/>);
  assert.match(personal, /class="badge personal"/);
  assert.match(personal, />\s*PERSONAL/, personal);
  assert.match(personal, /no independent session proof/);
  assert.match(personal, /not a canonical bar/);
  assert.doesNotMatch(personal, /DEGRADED|badge degraded/);
  const degraded = renderToStaticMarkup(<QualityBadge quality="DEGRADED"/>);
  assert.match(degraded, /class="badge degraded"/);
  assert.doesNotMatch(degraded, /PERSONAL|badge personal/);
});

test('Data Health personal card shows the latest session date and symbol count', () => {
  const html = renderToStaticMarkup(<DataHealthView report={report} personal={personalBars}/>);
  for (const text of ['Personal capture', 'Latest personal session', '2026-10-26', 'Symbols captured',
    'no independent session proof', 'not a canonical bar']) {
    assert.ok(html.includes(text), text);
  }
  assert.match(html, />2<\/strong>/);
  assert.match(html, /badge personal/);
  assert.doesNotMatch(html, /No personal capture yet/);
});

test('empty personal store renders the explicit no-capture state and no PERSONAL badge', () => {
  const empty: PersonalBars = { source: 'PERSONAL_FILE_STORE', generatedAt: '2026-10-27T02:00:00Z', sessions: [] };
  const html = renderToStaticMarkup(<DataHealthView report={report} personal={empty}/>);
  assert.match(html, /No personal capture yet/);
  assert.match(html, /no personal price is shown in place of a canonical bar/);
  assert.doesNotMatch(html, /badge personal/);
});

test('personal capture read failure is reported on Data Health without hiding canonical cards', () => {
  const html = renderToStaticMarkup(<DataHealthView report={report} personal={null} personalError="The personal bar store is unavailable."/>);
  assert.match(html, /Personal capture unavailable: The personal bar store is unavailable\./);
  assert.match(html, /Retained sessions/);
});

test('Since last session compares the two newest sessions and flags missing rows', () => {
  const html = renderToStaticMarkup(<SinceLastSessionCard report={report}/>);
  for (const text of ['Since last session', '2026-10-22 → 2026-10-23', 'BBCA.JK', '9,000', '9,100', '+100',
    '+1.11%', 'DEGRADED', 'MISSING', 'MISSING_NEW_SESSION', 'BBRI.JK', 'Observation only', 'Coverage differences',
    'only in the previous session', 'do not prove']) {
    assert.ok(html.includes(text), text);
  }
  assert.match(html, /data-delta-status="MISSING"/);
  assert.doesNotMatch(html, /Rp 0/);
});

test('a missing instrument is never treated as unchanged', () => {
  const html = renderToStaticMarkup(<SinceLastSessionCard report={{ ...report, delta: { ...delta,
    changes: [delta.changes[1]], coverageDifferences: delta.coverageDifferences } }}/>);
  assert.match(html, /4,500/);
  assert.match(html, /MISSING/);
  assert.doesNotMatch(html, /0\.00%/);
});

test('an empty canonical store has an explicit delta empty state', () => {
  const empty: DataHealthReport = { ...report, lastSession: null, sessions: [], gaps: [], anomalies: [],
    quality: {}, degraded: false, delta: { since: null, previousSession: null, newSession: null, newSessions: [],
      changes: [], coverageDifferences: [], flat: 0, zeroVolume: 0, quality: {}, degraded: false, note: '' } };
  const html = renderToStaticMarkup(<SinceLastSessionCard report={empty}/>);
  assert.match(html, /No canonical session is retained, so no delta is computed\./);
  assert.doesNotMatch(html, /data-delta-status/);
});

test('one retained session states that no comparison is possible', () => {
  const html = renderToStaticMarkup(<SinceLastSessionCard report={{ ...report, delta: { ...delta,
    previousSession: null, changes: [], coverageDifferences: [] } }}/>);
  assert.match(html, /Only one canonical session \(2026-10-23\) is retained/);
  assert.match(html, /no change is inferred/);
  assert.doesNotMatch(html, /data-delta-status/);
});
