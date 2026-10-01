import { money, valuationLabel, type Holding } from './view.js';
export function PortfolioTable({ holdings, onOpen }: { holdings: Holding[]; onOpen: (h: Holding) => void }) {
  return <div className="table"><table><caption>Holdings</caption><thead><tr>{['Symbol', 'Mandate', 'Shares', 'Average cost', 'Latest price', 'Price date', 'Market value', 'Unrealized P&L', 'Data state'].map(c => <th key={c}>{c}</th>)}</tr></thead><tbody>
    {holdings.map(h => <tr key={h.position.instrumentId}><td><button type="button" onClick={() => onOpen(h)}>{h.market.displaySymbol}</button></td><td>{h.activeThesis?.mandate ?? 'UNASSIGNED'}</td><td>{h.position.shares}</td><td>{money(h.position.averageCost)}</td><td>{money(h.valuation.price)}</td><td>{h.valuation.date ?? 'UNAVAILABLE'}</td><td>{money(h.valuation.marketValue)}</td><td>{money(h.valuation.unrealizedPnl)}</td><td>{valuationLabel(h)}</td></tr>)}
  </tbody></table></div>;
}
