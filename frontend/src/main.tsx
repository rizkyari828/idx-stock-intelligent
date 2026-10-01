import React, { useEffect, useState } from 'react';
import { createRoot } from 'react-dom/client';
import { coverageLabel, jakartaToday, money, valuationLabel, type Holding, type PortfolioView, type Thesis } from './view';
import './style.css';
import { PortfolioTable } from './PortfolioTable';

type Instrument = { id: string; symbol: string; type: string; name: string };
type Event = { id: string; type: string; instrumentId: string | null; quantity: number; unit: string; price: number; fees: number; cashAmount: number; tradeDate: string; knownAt: string; supersedes: string | null; externalReference: string | null };
async function api<T>(path: string, body?: unknown): Promise<T> {
  const response = await fetch(`/api${path}`, body === undefined ? undefined : { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body) });
  const data = await response.json();
  if (!response.ok) throw new Error(data.error ?? data.title ?? `Request failed (${response.status})`);
  return data as T;
}
function App() {
  const [id, setId] = useState(localStorage.getItem('idx-portfolio-id') ?? '');
  const [openId, setOpenId] = useState(id);
  const [view, setView] = useState<PortfolioView | null>(null);
  const [instruments, setInstruments] = useState<Instrument[]>([]);
  const [selected, setSelected] = useState<Holding | null>(null);
  const [theses, setTheses] = useState<Thesis[]>([]);
  const [thesisOffset, setThesisOffset] = useState(0);
  const [events, setEvents] = useState<Event[]>([]);
  const [eventOffset, setEventOffset] = useState(0);
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);
  const [notice, setNotice] = useState('');
  async function load(portfolioId = id) {
    if (!portfolioId) return;
    const next = await api<PortfolioView>(`/portfolios/${portfolioId}`);
    const changed = view?.portfolio.id !== portfolioId;
    setView(next); setId(portfolioId); setOpenId(portfolioId); localStorage.setItem('idx-portfolio-id', portfolioId);
    if (changed) { setSelected(null); setTheses([]); setThesisOffset(0); setEventOffset(0); }
    setEvents(await api<Event[]>(`/portfolios/${portfolioId}/events?limit=100&offset=${changed ? 0 : eventOffset}`));
    if (!changed && selected) setSelected(next.holdings.find(h => h.position.instrumentId === selected.position.instrumentId) ?? null);
  }
  async function act(work: () => Promise<void>) {
    setBusy(true); setError(''); setNotice('');
    try { await work(); setNotice(previous => previous || 'Operation SUCCESS'); } catch (e) { setError(e instanceof Error ? e.message : String(e)); }
    finally { setBusy(false); }
  }
  async function loadInstruments() {
    const all: Instrument[] = [];
    for (let offset = 0; offset < 10000; offset += 200) {
      const page = await api<Instrument[]>(`/instruments?limit=200&offset=${offset}`); all.push(...page);
      if (page.length < 200) break;
    }
    setInstruments(all);
  }
  useEffect(() => { void act(async () => { await loadInstruments(); await load(); }); }, []);
  useEffect(() => {
    if (selected && id) void act(async () => setTheses(await api<Thesis[]>(`/portfolios/${id}/holdings/${selected.position.instrumentId}/theses?limit=100&offset=${thesisOffset}`)));
  }, [id, selected?.position.instrumentId, thesisOffset]);
  const fields = (form: HTMLFormElement) => Object.fromEntries(new FormData(form));
  return <main>
    <header><p>IDX Stock Intelligence · local portfolio accounting · IDR</p><h1>Portfolio ledger</h1></header>
    <p role="status">{busy ? 'Working…' : notice}</p>{error && <p role="alert" className="error">Operation FAILED · {error}</p>}
    <section aria-label="Open or create portfolio">
      <form onSubmit={e => { e.preventDefault(); void act(() => load(openId)); }}><label>Portfolio ID<input value={openId} onChange={e => setOpenId(e.target.value)} required /></label><button disabled={busy}>Open</button></form>
      <form onSubmit={e => { e.preventDefault(); const form = e.currentTarget; void act(async () => { const p = await api<{ id: string }>('/portfolios', { name: fields(form).name }); setId(p.id); await load(p.id); }); }}><label>New portfolio name<input name="name" required maxLength={200} /></label><button disabled={busy}>Create</button></form>
    </section>
    {view && <>
      <h2>{view.portfolio.name}</h2><p>Cash: {money(view.cash)}{view.negativeCash && ' · NEGATIVE CASH'}</p>
      <p>Valuation coverage: {coverageLabel(view)}</p><p>Market value: {money(view.totalMarketValue)} · Priced subtotal: {money(view.pricedMarketValue)} · Equity including cash: {money(view.totalEquity)}</p>
      <p>Read operation: {view.operation} · Through {view.through} · Known by {view.knowledgeCutoff}</p>
      <PortfolioTable holdings={view.holdings} onOpen={h => { setSelected(h); setThesisOffset(0); }} />
      <section><h2>Record transaction</h2><p>LOTS explicitly converts to 100 shares per IDX equity lot. Saved events always use SHARES. Cash events use cash amount only. Corrections replace an event by its ID and retain history.</p>
        <form onSubmit={e => { e.preventDefault(); const form = e.currentTarget; void act(async () => { const f = fields(form); const trade = f.type === 'BUY' || f.type === 'SELL'; const result = await api<{ duplicate: boolean }>(`/portfolios/${id}/events`, { eventId: crypto.randomUUID(), type: f.type, instrumentId: trade ? f.instrument : null, tradeDate: f.date, quantity: trade ? f.quantity : '0', unit: trade ? f.unit : 'SHARES', price: trade ? f.price : '0', fees: f.fees, cashAmount: trade ? '0' : f.cash, externalReference: f.reference || null, source: 'USER', note: f.note || null, supersedes: f.supersedes || null }); await load(); if (result.duplicate) setNotice('Duplicate ignored'); }); }}>
          <label>Type<select name="type"><option>CASH_DEPOSIT</option><option>BUY</option><option>SELL</option><option>CASH_WITHDRAWAL</option></select></label>
          <label>Instrument<select name="instrument"><option value="">Select for BUY / SELL</option>{instruments.filter(i => i.type === 'EQUITY').map(i => <option key={i.id} value={i.id}>{i.symbol}</option>)}</select></label>
          <label>Quantity<input name="quantity" type="number" min="0" step="1" defaultValue="0" required /></label>
          <label>Unit<select name="unit"><option>SHARES</option><option>LOTS</option></select></label>
          <label>Price<input name="price" type="number" min="0" step="any" defaultValue="0" required /></label>
          <label>Fees<input name="fees" type="number" min="0" step="any" defaultValue="0" required /></label>
          <label>Cash amount<input name="cash" type="number" min="0" step="any" defaultValue="0" required /></label>
          <label>Trade date<input name="date" type="date" defaultValue={jakartaToday()} max={jakartaToday()} required /></label>
          <label>Import reference<input name="reference" maxLength={200} /></label><label>Supersedes event ID (correction)<input name="supersedes" /></label><label>Note<input name="note" maxLength={4000} /></label><button disabled={busy}>Append event</button>
        </form>
      </section>
      <details><summary>Register equity outside collector coverage</summary><form onSubmit={e => { e.preventDefault(); const form = e.currentTarget; void act(async () => { const f = fields(form); await api('/instruments', { id: f.id || crypto.randomUUID(), name: f.name, symbol: f.symbol, type: 'EQUITY', validFrom: f.from }); await loadInstruments(); form.reset(); }); }}>
        <label>Existing stable ID, or blank for new<input name="id" /></label><label>Issuer name<input name="name" required maxLength={200} /></label><label>Display symbol<input name="symbol" required maxLength={50} /></label><label>Symbol valid from<input name="from" type="date" defaultValue={jakartaToday()} required /></label><button disabled={busy}>Register</button>
      </form></details>
      {selected && <section aria-label="Holding detail"><h2>{selected.market.displaySymbol} · holding detail</h2><p>Stable ID: {selected.position.instrumentId}</p><p>Invested cost {money(selected.position.investedCost)} · Realized P&L {money(selected.position.realizedPnl)} · Cash impact {money(selected.position.cashImpact)}</p><p>{valuationLabel(selected)} · Age {selected.valuation.priceAgeDays ?? 'UNKNOWN'} calendar days · Completeness {selected.market.completeness}</p><p>Source {selected.market.source ?? 'UNKNOWN'} · Basis {selected.market.priceBasis} · Revision {selected.market.revision ?? 'UNKNOWN'} · Retrieved {selected.market.retrievedAt ?? 'UNKNOWN'} · Known {selected.market.knownAt ?? 'UNKNOWN'}</p>
        <p>Feature readiness: {Object.entries(selected.market.features).map(([name, f]) => `${name}: ${f.availability} (${f.unavailableReason ?? ''})`).join('; ')}</p>
        <h3>Active thesis</h3><p>{selected.activeThesis ? `${selected.activeThesis.mandate} · ${selected.activeThesis.text}` : 'No active thesis'}</p>
        <form onSubmit={e => { e.preventDefault(); const form = e.currentTarget; void act(async () => { const f = fields(form); await api(`/portfolios/${id}/holdings/${selected.position.instrumentId}/theses`, { mandate: f.mandate, text: f.text, invalidationNote: f.invalidation || null, active: f.active === 'on' }); await load(); setThesisOffset(0); setTheses(await api<Thesis[]>(`/portfolios/${id}/holdings/${selected.position.instrumentId}/theses?limit=100`)); }); }}>
          <label>Mandate<select name="mandate"><option>LONG_SWING</option><option>FAST_SWING</option><option>INVEST</option></select></label><label>Thesis<textarea name="text" required maxLength={8000} /></label><label>Invalidation note<textarea name="invalidation" maxLength={4000} /></label><label><input type="checkbox" name="active" defaultChecked /> Active</label><button disabled={busy}>Append thesis version</button>
        </form><h3>Thesis history</h3><ol>{theses.map(t => <li key={t.id}>v{t.version} · {t.mandate} · {t.knownAt} · {t.active ? 'active when recorded' : 'inactive'}<p>{t.text}</p>{t.invalidationNote && <p>{t.invalidationNote}</p>}</li>)}</ol><button disabled={busy || thesisOffset === 0} onClick={() => setThesisOffset(thesisOffset - 100)}>Newer theses</button><button disabled={busy || theses.length < 100} onClick={() => setThesisOffset(thesisOffset + 100)}>Older theses</button>
      </section>}
      <details><summary>Append-only event history</summary><ol>{events.map(e => <li key={e.id}>{e.type} · {e.tradeDate} · {e.quantity} {e.unit} · price {money(e.price)} · cash {money(e.cashAmount)} · fees {money(e.fees)}<p>ID {e.id} · Known {e.knownAt}{e.supersedes && ` · Supersedes ${e.supersedes}`}</p></li>)}</ol><button disabled={busy || eventOffset === 0} onClick={() => void act(async () => { const offset = eventOffset - 100; setEventOffset(offset); setEvents(await api<Event[]>(`/portfolios/${id}/events?limit=100&offset=${offset}`)); })}>Previous events</button><button disabled={busy || events.length < 100} onClick={() => void act(async () => { const offset = eventOffset + 100; setEventOffset(offset); setEvents(await api<Event[]>(`/portfolios/${id}/events?limit=100&offset=${offset}`)); })}>Next events</button></details>
    </>}
  </main>;
}
createRoot(document.getElementById('root')!).render(<App />);
