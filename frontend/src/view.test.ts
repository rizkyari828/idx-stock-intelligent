import assert from 'node:assert/strict';
import { test } from 'node:test';
import { money, valuationLabel, coverageLabel, type Holding, type PortfolioView } from './view.js';
test('missing prices and incomplete totals stay visible; zero cash is a number', () => {
  assert.equal(money(null), 'UNAVAILABLE');
  assert.equal(money(0), '0');
  assert.equal(valuationLabel({ valuation: { availability: 'UNAVAILABLE', unavailableReason: 'NO_CURRENT_MARKET_PRICE' } } as Holding), 'UNAVAILABLE · NO_CURRENT_MARKET_PRICE');
  assert.equal(coverageLabel({ valuationCoverage: 'PARTIAL', pricedHoldings: 1, holdingCount: 2 } as PortfolioView), 'PARTIAL · 1/2 holdings priced');
  assert.equal(valuationLabel({ valuation: { availability: 'AVAILABLE' }, market: { freshness: 'STALE', quality: 'DEGRADED' } } as Holding), 'STALE · DEGRADED');
});
