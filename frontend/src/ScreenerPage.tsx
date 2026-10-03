import { StockLink } from './StocksPage.js';
import { useEffect, useState } from 'react';
import { jakartaToday, recorded } from './view.js';
import { Badge, Dialog, Empty, Icon, LoadingSkeleton, Metric, Notice, PageHeader } from './ui.js';
import { defaultControls, initialState, number, ScreenerSession, setupLabel, type Context, type Controls, type FieldStates, type MarketContext, type Provenance, type ScreenerResponse, type ScreenerRow, type ScreenerState } from './screener.js';

const raw = (value: unknown): string => value === null ? 'Unavailable' : Array.isArray(value) ? value.join(', ') || 'None' : typeof value === 'boolean' ? value ? 'Yes' : 'No' : String(value);
export function Facts({ values }: { values: Record<string, unknown> }) {
 return <dl className="screener-facts">{Object.entries(values).map(([name,value])=><div key={name}><dt>{name}</dt><dd>{raw(value)}</dd></div>)}</dl>;
}
export function Reasons({ values }: { values: string[] }) { return values.length ? <ul className="screener-reasons">{values.map(reason=><li key={reason}>{reason}</li>)}</ul> : <p className="muted">No reasons reported.</p>; }
export function Availability({ fields }: { fields: FieldStates }) {
 return <section aria-label="Feature availability"><h3>Feature availability</h3>{Object.keys(fields).length ? <dl className="screener-facts">{Object.entries(fields).map(([name,state])=><div key={name}><dt>{name}</dt><dd>{state.availability} · {state.reason ?? 'No reason reported'}</dd></div>)}</dl> : <p>No unavailable fields reported.</p>}</section>;
}
export function ProvenanceDetails({ value }: { value: Provenance }) {
 const { currentEvidence, ...observation }=value;
 return <section aria-label="Provenance"><h3>Observation provenance</h3><Facts values={observation}/><h3>Selected current evidence</h3><p className="muted">May differ from the last usable observation above. Rejected selected evidence stays visible.</p>{currentEvidence ? <Facts values={currentEvidence}/> : <p>Unavailable</p>}</section>;
}
export function RowDetails({ row, onClose }: { row: ScreenerRow; onClose: () => void }) {
 return <Dialog title={`${row.symbol ?? 'Unavailable'} · Screener details`} eyebrow="DESCRIPTIVE CONTEXT" onClose={onClose}><div className="dialog-body page-stack screener-detail">
  <section><h3>Identity and portfolio metadata</h3><Facts values={{ instrumentId: row.instrumentId, symbol: row.symbol, displayName: row.displayName, configured: row.configured, held: row.held, mandate: row.mandate ?? 'Unassigned', discoveryRank: row.discoveryRank }}/></section>
  <section><h3>Eligibility · {row.eligibility}</h3><Reasons values={row.eligibilityReasons}/></section>
  <section><h3>Setup · {setupLabel(row)}</h3><Facts values={{ setup: row.setup, setupEvaluated: row.setupEvaluated }}/><Reasons values={row.setupReasons}/></section>
  <section><h3>Episode</h3>{row.episode ? <Facts values={row.episode}/> : <p>No episode reported.</p>}</section>
  <section><h3>Technical context · full returned values</h3><Facts values={{ 'Observed date': row.marketDate, 'Close · IDR': row.close, 'Change · %': row.changePercent, EMA20: row.ema20, EMA50: row.ema50, Trend: row.trend, 'ATR14 · IDR': row.atr14, 'ATR · %': row.atrPercent, 'Prior high20 · IDR': row.priorHigh20, 'Prior low20 · IDR': row.priorLow20, 'Distance to prior high · %': row.distanceToHighPercent, 'Volume · source units': row.volume, 'Volume ratio20': row.volumeRatio20, 'Daily value proxy · IDR': row.dailyValueProxyIdr, 'Prior-20 value proxy · IDR': row.monetaryLiquidity20Idr, 'RS20 · pp': row.rs20Pp, 'RS60 · pp': row.rs60Pp, Stale: row.stale, 'No trade': row.noTrade, 'Trading status': row.tradingStatus }}/></section>
  <section><h3>Data state · {row.dataQuality}</h3><Reasons values={row.dataReasons}/></section><Availability fields={row.fieldStates}/><ProvenanceDetails value={row.provenance}/>
 </div><div className="dialog-footer"><button className="button" onClick={onClose}>Done</button></div></Dialog>;
}
function SetupBadge({ row }: { row: ScreenerRow }) { return <Badge kind={row.setupEvaluated ? 'info' : 'unknown'}>{setupLabel(row)}</Badge>; }
function DataState({ row }: { row: ScreenerRow }) {
 return <div className="stack screener-cell"><Badge kind={row.dataQuality==='COMPLETE'?'outline':'review'}>{row.dataQuality}</Badge><span>{row.eligibility}</span><small>Canonical: {row.provenance.canonicalQuality ?? 'Unavailable'}</small>{row.provenance.currentEvidence && row.provenance.currentEvidence.canonicalQuality!==row.provenance.canonicalQuality && <small>Selected: {row.provenance.currentEvidence.canonicalQuality}</small>}</div>;
}
function Rows({ ids, rows, held = false, onOpen, onOpenStock }: { ids: string[]; rows: Map<string,ScreenerRow>; held?: boolean; onOpen: (row: ScreenerRow) => void; onOpenStock?: (id:string)=>void }) {
 return <div className="table-scroll" tabIndex={0} aria-label={`${held?'All held positions':'Discovery instruments'}; scroll horizontally`}><table className="screener-table">
  <caption className="sr-only">{held?'All held positions, independent of discovery filters and pages':'Discovery in backend order'}. Unavailable values are not zero. Open symbol details for reasons and provenance.</caption>
  <thead><tr><th scope="col">Symbol / Held</th>{held&&<th scope="col">Mandate</th>}<th scope="col">Setup</th><th scope="col" className="num">Close · IDR / Date</th><th scope="col" className="num">Distance to prior high · %</th><th scope="col" className="num">RS60 · pp</th><th scope="col" className="num">RS20 · pp</th>{!held&&<th scope="col" className="num">Prior-20 Value Proxy · IDR</th>}<th scope="col">Data State</th></tr></thead>
  <tbody>{ids.map(id=>{const row=rows.get(id);if(!row)return <tr key={id}><td colSpan={8}>Instrument {id}: unavailable in response.</td></tr>;
   return <tr key={id} data-instrument-id={id}><td><button className="text-button holding-button" aria-label={`Details for ${row.symbol ?? row.instrumentId}`} onClick={()=>onOpen(row)}><span className="symbol-name">{row.symbol ?? 'Unavailable'}</span></button> <StockLink id={row.instrumentId} onNavigate={onOpenStock}>Stock Detail</StockLink>{row.displayName&&<span className="symbol-description">{row.displayName}</span>}{row.held&&<Badge kind="outline">Held</Badge>}</td>{held&&<td>{row.mandate ?? 'Unassigned'}</td>}<td><SetupBadge row={row}/></td><td className="num">{number(row.close)}<small className="screener-date">{row.marketDate ?? 'Unavailable date'}</small>{row.stale&&<Badge kind="review">Stale</Badge>}</td><td className="num">{number(row.distanceToHighPercent)}</td><td className="num">{number(row.rs60Pp)}</td><td className="num">{number(row.rs20Pp)}</td>{!held&&<td className="num">{number(row.monetaryLiquidity20Idr,0)}</td>}<td><DataState row={row}/></td></tr>;
  })}</tbody></table></div>;
}
function Market({ context }: { context: MarketContext }) {
 return <section className="panel panel-body page-stack" aria-label="IHSG market context"><div className="screener-market-head"><h2>IHSG market context</h2><Badge kind={context.trend==='UNKNOWN'?'unknown':'outline'}>Trend · {context.trend}</Badge><Badge kind={context.volatility==='UNKNOWN'?'unknown':'outline'}>Volatility · {context.volatility}</Badge><span className="muted">Observed {context.marketDate ?? 'Unavailable'}</span></div>
  <details><summary>Market values, availability and provenance</summary><div className="page-stack"><Facts values={{ 'Close · IDR': context.close, EMA20: context.ema20, EMA50: context.ema50, 'ATR14 · IDR': context.atr14, 'ATR · %': context.atrPercent, instrumentId: context.instrumentId }}/><Reasons values={context.reasons}/><Availability fields={context.fieldStates}/><ProvenanceDetails value={context.provenance}/></div></details>
 </section>;
}
export function ScreenerResults({ result, portfolioSelected, busy = false, onOpen, onOpenStock }: { result: ScreenerResponse; portfolioSelected: boolean; busy?: boolean; onOpen: (row: ScreenerRow) => void; onOpenStock?: (id:string)=>void }) {
 const rows=new Map(result.rows.map(row=>[row.instrumentId,row]));const s=result.summary;
 return <>
  <p className="read-context muted">Screener V0.1 · {result.policyId} · {result.universe} · Through {result.through} · Target session {result.targetSession ?? 'Unavailable'} · Known by {recorded(result.cutoff)}</p>
  <Notice kind={result.status==='COMPLETE'?'info':'review'}><strong>{result.status}</strong> · {result.status==='BLOCKED'?'Screening could not be fully evaluated because required evidence/history is unavailable.':result.status==='PARTIAL'?'Coverage is partial. Some evidence or features are unavailable; evaluable setups remain descriptive.':s.candidates===0?'Evaluation complete. No candidates met this descriptive setup.':'Evaluation complete.'}{result.reasons.length>0&&<Reasons values={result.reasons}/>}</Notice>
  <div className="summary-strip screener-metrics">{([['Configured',s.configured],['Eligible',s.eligible],['Data Blocked',s.dataBlocked],['Candidates',s.candidates],['Held',s.held]] as const).map(([label,value])=><Metric key={label} label={label} value={value ?? 'Unavailable'} note={label==='Held'?'All positive portfolio positions':'Backend evaluation count'}/>)}</div>
  <details className="panel panel-body"><summary>Candidate breakdown and coverage counts</summary><p className="muted">Diagnostic counts may overlap; they do not sum to configured instruments.</p><Facts values={{ Confirmed: s.confirmed, Watch: s.watch, Ineligible: s.ineligible, Evaluated: s.evaluated, 'Insufficient history': s.insufficientHistory, Stale: s.stale, Unsupported: s.unsupported, Shortlisted: s.shortlisted, 'Omitted confirmed': s.omittedConfirmed, 'Omitted watch': s.omittedWatch, 'Held outside universe': s.heldOutsideUniverse }}/></details>
  <Market context={result.marketContext}/>
  <section className="panel" aria-label="Discovery results"><div className="panel-body"><h2>{result.page.view==='shortlist'?'Shortlist':'All instruments'}</h2><p className="muted">Backend order · Open a symbol for full values, unavailable reasons and provenance.</p></div>{busy?<LoadingSkeleton label="Loading Screener discovery…"/>:result.discoveryIds.length?<Rows ids={result.discoveryIds} rows={rows} onOpen={onOpen} onOpenStock={onOpenStock}/>:<div className="panel-body"><p>{result.status==='BLOCKED'?'Discovery unavailable under this evidence context.':'No discovery rows match this view, filter and page.'}</p></div>}</section>
  {portfolioSelected&&<section className="panel" aria-label="All Held Positions"><div className="panel-body"><h2>All Held Positions</h2><p className="muted">Same instruments as discovery where shared. Independent of filters and pages; mandate is factual metadata.</p></div>{result.heldIds.length?<Rows ids={result.heldIds} rows={rows} held onOpen={onOpen} onOpenStock={onOpenStock}/>:<p className="panel-body">No positive held positions at this context.</p>}</section>}
  <details className="panel panel-body"><summary>Evaluation identity</summary><Facts values={{ inputHash: result.inputHash, historyAnchor: result.historyAnchor, universeSnapshotId: result.universeSnapshotId, replayScope: result.replayScope }}/></details>
 </>;
}
export function Screener({ portfolioId, portfolioName, onOpenPortfolio, onOpenStock }: { portfolioId: string; portfolioName?: string; onOpenPortfolio: () => void; onOpenStock?: (id:string)=>void }) {
 const [context,setContext]=useState<Context>({through:jakartaToday(),cutoff:'',portfolioId:portfolioId || undefined});
 const [controls,setControls]=useState<Controls>({...defaultControls});const [dirty,setDirty]=useState(false);
 const [state,setState]=useState<ScreenerState>(initialState);const [session]=useState(()=>new ScreenerSession(setState));
 const [detail,setDetail]=useState<ScreenerRow|null>(null);
 useEffect(()=>{void session.load(context,defaultControls);return ()=>session.invalidate();},[session]);
 function edit(next: Context){session.invalidate();setDetail(null);setContext(next);setDirty(true);setControls(c=>({...c,offset:0}));}
 function load(next=controls,refresh=false,nextContext=context){setDetail(null);setControls(next);setDirty(false);void session.load(nextContext,next,refresh);}
 function filter(next: Partial<Controls>){load({...controls,...next,offset:0});}
 const page=state.result?.page;
 return <div className="page-stack screener-page"><PageHeader eyebrow="PILOT · SCREENER V0.1" title="Screener" subtitle="Descriptive breakout screening; not an order."><span className="readonly-label"><Icon name="shield"/>Read-only evaluation</span></PageHeader>
  <form className="panel panel-body screener-context" aria-label="Screener context" onSubmit={e=>{e.preventDefault();load({...controls,offset:0},true);}}>
   <label className="field"><span>Through · market date</span><input name="screenerThrough" type="date" required min="2026-08-24" max={jakartaToday()<'2027-08-24'?jakartaToday():'2027-08-24'} value={context.through} onChange={e=>edit({...context,through:e.target.value})}/></label>
   <label className="field"><span>Known by · WIB</span><input name="screenerCutoff" type="datetime-local" step="1" aria-describedby="screener-cutoff-help" value={context.cutoff} onChange={e=>edit({...context,cutoff:e.target.value})}/><small id="screener-cutoff-help">Blank = now on first evaluation; resolved time stays pinned for filters and pages.</small></label>
   <label className="field"><span>Portfolio · optional</span><select name="screenerPortfolio" className="control" value={context.portfolioId ?? ''} onChange={e=>{const next={...context,portfolioId:e.target.value || undefined};edit(next);load({...controls,offset:0},true,next);}}><option value="">Discovery only</option>{portfolioId&&<option value={portfolioId}>{portfolioName ?? portfolioId}</option>}</select><button type="button" className="text-button" onClick={onOpenPortfolio}>Open another portfolio</button></label>
   <div className="wrap"><button className="button" type="submit">Apply context</button><button className="button ghost" type="button" onClick={()=>{const next={...context,cutoff:''};edit(next);load({...controls,offset:0},true,next);}}>Now</button></div>
  </form>
  {dirty&&<Notice>Context changed. Apply the through date and known-by time to evaluate.</Notice>}
  <div className="panel panel-body screener-filters"><div className="tabs" aria-label="Screener view">{(['shortlist','all'] as const).map(view=><button type="button" className={`tab ${controls.view===view?'active':''}`} aria-pressed={controls.view===view} disabled={dirty || state.refreshRequired} onClick={()=>filter({view})} key={view}>{view==='shortlist'?'Shortlist':'All instruments'}</button>)}</div>
   <label className="field"><span>Setup</span><select name="screenerSetup" className="control" value={controls.setup} disabled={dirty || state.refreshRequired} onChange={e=>filter({setup:e.target.value as Controls['setup']})}>{['ALL','NONE','WATCH','CONFIRMED','FAILED'].map(value=><option key={value}>{value}</option>)}</select></label>
   <label className="field"><span>Eligibility</span><select name="screenerEligibility" className="control" value={controls.eligibility} disabled={dirty || state.refreshRequired} onChange={e=>filter({eligibility:e.target.value as Controls['eligibility']})}>{['ALL','ELIGIBLE','INELIGIBLE','DATA_BLOCKED'].map(value=><option key={value}>{value}</option>)}</select></label>
   <button type="button" className="button" disabled={dirty || state.busy} onClick={()=>load({...controls,offset:0},true)}>Refresh evaluation</button>
  </div>
  {state.error&&<Notice kind="error">{state.error}</Notice>}
  {state.busy&&!state.result&&<LoadingSkeleton label="Loading Screener evaluation…"/>}
  {state.result&&<><ScreenerResults result={state.result} portfolioSelected={!!context.portfolioId} busy={state.busy} onOpen={setDetail} onOpenStock={onOpenStock}/><div className="panel panel-body screener-paging" aria-label="Discovery pagination"><span role="status">{page!.total===0?'0 discovery rows':`${page!.offset+1}–${Math.min(page!.offset+page!.limit,page!.total)} of ${page!.total} discovery rows`} · {page!.limit} per page</span><div className="wrap"><button type="button" className="button" disabled={state.busy || page!.offset===0} onClick={()=>load({...controls,offset:Math.max(0,page!.offset-page!.limit),limit:page!.limit})}>Previous</button><button type="button" className="button" disabled={state.busy || page!.offset+page!.limit>=page!.total} onClick={()=>load({...controls,offset:page!.offset+page!.limit,limit:page!.limit})}>Next</button></div></div></>}
  {!dirty&&!state.busy&&!state.error&&!state.result&&<Empty title="Ready to evaluate">Apply the Screener context.</Empty>}
  {detail&&<RowDetails row={detail} onClose={()=>setDetail(null)}/>}
 </div>;
}
