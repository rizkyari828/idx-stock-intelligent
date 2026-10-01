export type Thesis = { id: string; version: number; mandate: string; text: string; knownAt: string; supersedes: string | null; invalidationNote: string | null; active: boolean };
export type Holding = {
  position: { instrumentId: string; shares: number; averageCost: number | null; investedCost: number; realizedPnl: number; cashImpact: number; latestEventDate: string };
  market: { displaySymbol: string; freshness: string; quality: string; completeness: string; retrievedAt: string | null; knownAt: string | null; revision: number | null; source: string | null; priceBasis: string; features: Record<string, { availability: string; unavailableReason: string | null }> };
  valuation: { availability: string; price: number | null; date: string | null; priceAgeDays: number | null; marketValue: number | null; unrealizedPnl: number | null; unavailableReason: string | null };
  activeThesis: Thesis | null;
};
export type PortfolioView = { portfolio: { id: string; name: string }; cash: number; negativeCash: boolean; holdings: Holding[]; pricedHoldings: number; holdingCount: number; valuationCoverage: string; pricedMarketValue: number; totalMarketValue: number | null; totalEquity: number | null; operation: string; through: string; knowledgeCutoff: string };
export const money = (value: number | null) => value === null ? 'UNAVAILABLE' : new Intl.NumberFormat('en-US', { maximumFractionDigits: 2 }).format(value);
export const valuationLabel = (h: Holding) => h.valuation.availability === 'UNAVAILABLE'
  ? `UNAVAILABLE · ${h.valuation.unavailableReason}` : `${h.market.freshness} · ${h.market.quality}`;
export const coverageLabel = (v: PortfolioView) => `${v.valuationCoverage} · ${v.pricedHoldings}/${v.holdingCount} holdings priced`;
export const jakartaToday = () => new Intl.DateTimeFormat('en-CA', { timeZone: 'Asia/Jakarta', year: 'numeric', month: '2-digit', day: '2-digit' }).format(new Date());
