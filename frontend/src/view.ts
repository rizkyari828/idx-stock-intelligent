export type Thesis = { id: string; version: number; mandate: string; text: string; knownAt: string; supersedes: string | null; invalidationNote: string | null; active: boolean };
export type Holding = {
  position: { instrumentId: string; shares: number; averageCost: number | null; investedCost: number; realizedPnl: number; cashImpact: number; latestEventDate: string };
  market: { displaySymbol: string; freshness: string; quality: string; completeness: string; retrievedAt: string | null; knownAt: string | null; revision: number | null; source: string | null; priceBasis: string; features: Record<string, { availability: string; unavailableReason: string | null }> };
  valuation: { availability: string; price: number | null; date: string | null; priceAgeDays: number | null; marketValue: number | null; unrealizedPnl: number | null; unavailableReason: string | null; unrealizedPercent: number | null };
  activeThesis: Thesis | null;
};
export type PortfolioView = { portfolio: { id: string; name: string }; cash: number; negativeCash: boolean; holdings: Holding[]; pricedHoldings: number; holdingCount: number; valuationCoverage: string; pricedMarketValue: number; totalMarketValue: number | null; totalEquity: number | null; operation: string; through: string; knowledgeCutoff: string; investedCost: number; realizedPnl: number; unrealizedPnl: number | null };
export const money = (value: number | null) => value === null ? 'UNAVAILABLE' : new Intl.NumberFormat('en-US', { maximumFractionDigits: 2 }).format(value);
export const valuationLabel = (h: Holding) => h.valuation.availability === 'UNAVAILABLE'
  ? `UNAVAILABLE · ${h.valuation.unavailableReason}` : `${h.market.freshness} · ${h.market.quality}`;
export const coverageLabel = (v: PortfolioView) => `${v.valuationCoverage} · ${v.pricedHoldings}/${v.holdingCount} holdings priced`;
export const jakartaToday = () => new Intl.DateTimeFormat('en-CA', { timeZone: 'Asia/Jakarta', year: 'numeric', month: '2-digit', day: '2-digit' }).format(new Date());

export type ImportPreview = { status: string; canImport: boolean; portfolioId: string | null; rowsRead: number; validRows: number; invalidRows: number; duplicates: number; estimatedResultingEvents: number; unknownInstruments: string[]; errors: string[]; rows: { row: number; error: string | null; duplicate: boolean; event: { type: string; quantity: number } | null }[] };
export const canConfirmImport = (preview: ImportPreview | null) => preview !== null && preview.canImport && preview.invalidRows === 0 && preview.errors.length === 0 && ['READY', 'ALREADY_PRESENT'].includes(preview.status);

export type Instrument = { id: string; symbol: string; type: string; name: string; hasEffectiveSymbol?: boolean };
export type LedgerEvent = { id: string; type: string; instrumentId: string | null; quantity: number; unit: string; price: number; fees: number; cashAmount: number; tradeDate: string; knownAt: string; order: number; supersedes: string | null; correctedBy: string | null; externalReference: string | null; note: string | null; source: string };
export type ExpectedHolding = { instrumentId: string | null; symbol: string; shares: string; averageCost: string | null };
export type Reconciliation = { through: string; knowledgeCutoff: string; status: string; ledgerCash: number; expectedCash: number; cashDifference: number; expectedHoldings: number; ledgerHoldings: number; matched: number; review: number; missing: number; unknown: number; rows: { instrumentId: string | null; symbol: string; ledgerShares: number | null; expectedShares: number | null; shareDifference: number | null; ledgerAverageCost: number | null; expectedAverageCost: number | null; averageCostDifference: number | null; status: string }[] };
export const quantity = (value: number | null) => value === null ? '—' : new Intl.NumberFormat('en-US', {maximumFractionDigits: 6}).format(value);
export const difference = (value: number | null) => value === null ? '—' : value === 0 ? '0' : `${value>0?'+':'−'}${Math.abs(value)<0.000001?'<0.000001':quantity(Math.abs(value))}`;
export const cutoffIso = (value: string) => value ? new Date(value+'+07:00').toISOString() : new Date().toISOString();
export const recorded = (value: string) => new Intl.DateTimeFormat('en-GB', {timeZone:'Asia/Jakarta',dateStyle:'medium',timeStyle:'medium'}).format(new Date(value))+' WIB';

export const percent = (value: number | null) => value == null ? '—' : `${value>0?'+':value<0?'−':''}${value!==0&&Math.abs(value)<0.01?'<0.01':money(Math.abs(value))}%`;
