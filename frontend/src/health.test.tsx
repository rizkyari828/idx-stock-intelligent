import assert from 'node:assert/strict';
import { test } from 'node:test';
import { renderToStaticMarkup } from 'react-dom/server';
import { PersonalBadge, QualityBadge } from './ui.js';
import { DataHealthView, TodayCard, type DataHealthReport } from './dataHealth.js';
import { type PersonalBars } from './personal.js';
import { workspaceRoute } from './decisions.js';

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
  degraded: true
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
