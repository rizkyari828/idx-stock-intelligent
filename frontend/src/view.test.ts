import assert from 'node:assert/strict';
import { test } from 'node:test';
import { money, valuationLabel, coverageLabel, canConfirmImport, type ImportPreview, type Holding, type PortfolioView } from './view.js';
test('missing prices and incomplete totals stay visible; zero cash is a number', () => {
  assert.equal(money(null), 'UNAVAILABLE');
  assert.equal(money(0), '0');
  assert.equal(valuationLabel({ valuation: { availability: 'UNAVAILABLE', unavailableReason: 'NO_CURRENT_MARKET_PRICE' } } as Holding), 'UNAVAILABLE · NO_CURRENT_MARKET_PRICE');
  assert.equal(coverageLabel({ valuationCoverage: 'PARTIAL', pricedHoldings: 1, holdingCount: 2 } as PortfolioView), 'PARTIAL · 1/2 holdings priced');
  assert.equal(valuationLabel({ valuation: { availability: 'AVAILABLE' }, market: { freshness: 'STALE', quality: 'DEGRADED' } } as Holding), 'STALE · DEGRADED');
});

test('only a valid preview enables explicit import confirmation', () => {
  assert.equal(canConfirmImport(null), false);
  const valid = { status: 'READY', canImport: true, invalidRows: 0, errors: [] } as unknown as ImportPreview;
  assert.equal(canConfirmImport(valid), true);
  assert.equal(canConfirmImport({ ...valid, status: 'INVALID', invalidRows: 1 }), false);
  assert.equal(canConfirmImport({ ...valid, status: 'CONFLICT', errors: ['conflict'] }), false);
  assert.equal(canConfirmImport({ ...valid, status: 'ALREADY_PRESENT' }), true);
});
