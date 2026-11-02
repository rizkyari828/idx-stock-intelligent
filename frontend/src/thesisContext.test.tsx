import assert from 'node:assert/strict';
import { test } from 'node:test';
import { renderToStaticMarkup } from 'react-dom/server';
import { ThesisJournal } from './ThesisJournal.js';
import { MarketContextView } from './thesisMarketContext.js';
import type { Holding, Thesis } from './view.js';
import type { TechnicalFeaturesReport } from './stocks.js';

const thesis: Thesis = {
  id: '11111111-1111-4111-8111-111111111111', version: 1, mandate: 'LONG_SWING', text: 'Owner-authored thesis.',
  knownAt: '2026-10-01T02:00:00+00:00', supersedes: null, invalidationNote: 'Close below the prior low.', active: true,
};

const holding = (market: Partial<Holding['market']> = {}): Holding => ({
  position: { instrumentId: '22222222-2222-4222-8222-222222222222', shares: 100, averageCost: 9000, investedCost: 900000, realizedPnl: 0, cashImpact: 0, latestEventDate: '2026-10-01' },
  market: { displaySymbol: 'BBCA.JK', freshness: 'STALE', quality: 'VERIFIED', completeness: 'UNKNOWN', retrievedAt: null, knownAt: null, revision: 1, source: 'EODHD', priceBasis: 'RAW_CLOSE', marketDate: '2026-10-23', price: 9850, unavailableReason: null, features: {}, ...market },
  valuation: { availability: 'AVAILABLE', price: 9850, date: '2026-10-23', priceAgeDays: 7, marketValue: 985000, unrealizedPnl: 85000, unavailableReason: null, unrealizedPercent: 9.44 },
  activeThesis: thesis,
});

const report: TechnicalFeaturesReport = {
  instrumentId: '22222222-2222-4222-8222-222222222222', symbol: 'BBCA.JK', name: 'Bank Central Asia Tbk',
  through: '2026-10-30', cutoff: '2026-10-30T12:00:00+00:00', marketDate: '2026-10-23', sessions: 2, index: false,
  features: {
    changePercent: { availability: 'AVAILABLE', value: 1.5, reason: null, minimumSessions: 2 },
    distanceToHighPercent: { availability: 'WARMUP', value: null, reason: 'INSUFFICIENT_SESSIONS', minimumSessions: 21 },
  },
};

const context = (market: Partial<Holding['market']> = {}, value: TechnicalFeaturesReport | null = report, error = '') =>
  renderToStaticMarkup(<MarketContextView holding={holding(market)} through="2026-10-30" report={value} error={error}/>);

test('a canonical bar shows its own date, close and quality beside the price', () => {
  const html = context();
  for (const text of ['Market context', '2026-10-23', '9,850', 'VERIFIED', 'Bar date 2026-10-23', 'STALE',
    'older than the selected through date 2026-10-30', 'Change %', '1.50']) {
    assert.ok(html.includes(text), text);
  }
  assert.match(html, /data-value="1\.5"/);
});

test('a missing canonical bar is explicit and never renders a zero', () => {
  const html = context({ marketDate: null, price: null, quality: 'UNKNOWN', freshness: 'UNKNOWN' });
  assert.match(html, /no canonical bar recorded for BBCA\.JK/);
  assert.match(html, /No price is shown/);
  assert.doesNotMatch(html, /Rp 0/);
  assert.doesNotMatch(html, /data-value="0"/);
});

test('a warmup feature is never rendered as a number', () => {
  const html = context();
  assert.match(html, /data-feature="changePercent"[\s\S]*?data-value="1\.5"/);
  assert.match(html, /data-feature="distanceToHighPercent"[\s\S]*?data-value="UNAVAILABLE"/);
  assert.match(html, /warmup — needs 21 sessions/);
  assert.match(html, /WARMUP/);
});

test('a rejected close is unavailable with its reason, never zero', () => {
  const html = context({ price: null, quality: 'REJECTED', unavailableReason: 'PRICE_QUALITY_UNAVAILABLE' });
  assert.match(html, /Close is unavailable/);
  assert.match(html, /PRICE_QUALITY_UNAVAILABLE/);
  assert.doesNotMatch(html, /Rp 0/);
  assert.doesNotMatch(html, /data-value="0"/);
});

test('a failed feature read keeps the canonical bar and states the error', () => {
  const html = context({}, null, 'through is outside the frozen feature horizon.');
  assert.match(html, /Technical projection unavailable: through is outside the frozen feature horizon\./);
  assert.match(html, /Bar date 2026-10-23/);
  assert.match(html, /9,850/);
});

test('the versioned thesis journal keeps its create and invalidate actions', () => {
  const html = renderToStaticMarkup(<ThesisJournal portfolioId="33333333-3333-4333-8333-333333333333"
    holdings={[holding()]} cutoff="" revision={0} onSaved={async()=>{}} through="2026-10-30"/>);
  for (const text of ['Create New Version', 'Invalidate Thesis', 'Version history',
    'Every version is retained', 'Market context', '2026-10-23']) {
    assert.ok(html.includes(text), text);
  }
  assert.match(html, /Thesis &amp; mandate/);
});
