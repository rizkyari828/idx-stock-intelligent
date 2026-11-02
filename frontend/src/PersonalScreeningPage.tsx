import { useEffect, useState } from 'react';
import { errorMessage } from './api.js';
import { jakartaToday, recorded } from './view.js';
import { Badge, Empty, Icon, LoadingSkeleton, Metric, Notice, PageHeader, PersonalBadge } from './ui.js';
import { number } from './screener.js';
import { technicalFeatures } from './StocksPage.js';
import { SCREENING_LIMITATIONS, defaultScreeningQuery, loadScreening, type ScreeningQuery, type ScreeningReport, type ScreeningRow } from './personalScreening.js';

const label = (name: string) => technicalFeatures.find(([key])=>key===name)?.[1] ?? name;

function useScreening(query: ScreeningQuery) {
  const [report,setReport]=useState<ScreeningReport|null>(null);
  const [error,setError]=useState('');
  const [busy,setBusy]=useState(true);
  useEffect(()=>{
    const controller=new AbortController();
    setBusy(true); setError('');
    void loadScreening(query, controller.signal)
      .then(next=>{ if(!controller.signal.aborted) setReport(next); })
      .catch(e=>{ if(!controller.signal.aborted) setError(errorMessage(e)); })
      .finally(()=>{ if(!controller.signal.aborted) setBusy(false); });
    return ()=>controller.abort();
  },[query.through,query.sort,query.direction,query.availability]);
  return { report, error, busy };
}

/** The literal limitations are never hidden behind a disclosure and are shown even before a read succeeds. */
export function ScreeningLimitations({lines}:{lines?: string[]}) {
  const values = lines?.length ? lines : SCREENING_LIMITATIONS;
  return <section className="panel panel-body screening-limitations" aria-label="Screening limitations">
    <div className="section-heading"><h2>Limitations</h2><PersonalBadge/></div>
    <ul>{values.map(line=><li key={line}>{line}</li>)}</ul>
    <p className="muted">A ranked row is only an ordering of an already-computed feature value. It is not a trade, a setup, an eligibility result or advice.</p>
  </section>;
}

/** Feature states are always rendered: available names and every warmup/unavailable name with its own reason. */
function FeatureStates({row}:{row:ScreeningRow}) {
  const entries = Object.entries(row.features);
  if(!entries.length) return <span className="muted" data-features="NO_PERSONAL_CAPTURE">No personal capture — no feature state is reported.</span>;
  const available = entries.filter(([,state])=>state.availability==='AVAILABLE');
  const missing = entries.filter(([,state])=>state.availability!=='AVAILABLE');
  return <div className="feature-states">
    <span>{row.availableCount} of {entries.length} AVAILABLE{row.warmupCount?` · ${row.warmupCount} WARMUP`:''}{row.unavailableCount?` · ${row.unavailableCount} UNAVAILABLE`:''}</span>
    <small>AVAILABLE: {available.length?available.map(([name])=>name).join(', '):'none'}</small>
    {missing.length>0&&<ul>{missing.map(([name,state])=><li key={name}><strong>{name}</strong> {state.availability}{state.reason?` (${state.reason})`:''}</li>)}</ul>}
  </div>;
}

function RowState({row,sort}:{row:ScreeningRow;sort:string}) {
  if(row.status==='RANKED') return <><Badge kind="outline" icon="check">RANKED</Badge><small>Ordered on an AVAILABLE value.</small></>;
  if(row.status==='NOT_RANKED') return <><Badge kind="review" icon="warning">NOT RANKED</Badge><small>{label(sort)} is {row.sortAvailability??'UNAVAILABLE'}{row.reason?` — ${row.reason}`:''}; listed after every ranked row. No value is assumed.</small></>;
  return <><Badge kind="unknown" icon="missing">NO PERSONAL CAPTURE</Badge><small>{row.reason??'PERSONAL_CAPTURE_MISSING'}; no value is shown.</small></>;
}

function ScreeningControls({draft,onDraft,onApply}:{draft:ScreeningQuery;onDraft:(next:ScreeningQuery)=>void;onApply:()=>void}) {
  return <form className="panel panel-body screening-context" aria-label="Screening context" onSubmit={e=>{e.preventDefault();onApply();}}>
    <label className="field"><span>Session date · through</span><input name="screeningThrough" type="date" required max={jakartaToday()} value={draft.through} onChange={e=>onDraft({...draft,through:e.target.value})}/></label>
    <label className="field"><span>Order by</span><select name="screeningSort" className="control" value={draft.sort} onChange={e=>onDraft({...draft,sort:e.target.value})}>{technicalFeatures.map(([key,title])=><option key={key} value={key}>{title}</option>)}</select></label>
    <label className="field"><span>Direction</span><select name="screeningDirection" className="control" value={draft.direction} onChange={e=>onDraft({...draft,direction:e.target.value as ScreeningQuery['direction']})}><option value="DESC">DESC · highest first</option><option value="ASC">ASC · lowest first</option></select></label>
    <label className="field"><span>Rows</span><select name="screeningAvailability" className="control" value={draft.availability} onChange={e=>onDraft({...draft,availability:e.target.value as ScreeningQuery['availability']})}><option value="ALL">ALL · ranked and not ranked</option><option value="AVAILABLE">AVAILABLE only · ranked rows</option></select></label>
    <div className="wrap"><button className="button" type="submit">Apply</button></div>
    <small className="wrap-note">Ordering and filtering use AVAILABLE values only. A warmup, unavailable or missing value is never treated as zero.</small>
  </form>;
}

export function ScreeningResults({report}:{report:ScreeningReport}) {
  const ranked = report.rows.filter(row=>row.status==='RANKED').length;
  return <>
    <div className="summary-strip portfolio-metrics">
      <Metric label="Panel rows" value={report.rows.length} note={`${report.panel} · personal, unverified`}/>
      <Metric label="Ranked" value={ranked} note="Ordering feature AVAILABLE"/>
      <Metric label="Not ranked" value={report.rows.length-ranked} note="Warmup, unavailable or no capture"/>
      <Metric label="Session" value={report.through} note="Selected through date"/>
      <Metric label="Known by" value={recorded(report.cutoff)} note="UTC record time · WIB display"/>
    </div>
    <section className="panel" aria-label="Descriptive screening rows">
      <div className="panel-body"><h2>{label(report.sort)} · {report.direction}</h2><p className="muted">Rows arrive in backend order. A row whose ordering feature is not AVAILABLE is never ranked and is listed after the ranked rows.</p></div>
      {report.rows.length
        ? <div className="table-scroll" tabIndex={0} aria-label="Descriptive screening rows"><table className="stocks-table screening-table">
            <caption className="sr-only">Personal pilot panel ordered by one already-projected feature. Unavailable values are not zero.</caption>
            <thead><tr>{['Rank','Symbol','Session',`${label(report.sort)} · ${report.direction}`,'Feature states','State'].map(column=><th key={column} scope="col">{column}</th>)}</tr></thead>
            <tbody>{report.rows.map(row=><tr key={row.symbol} data-screening-status={row.status}>
              <td className="num">{row.rank??'—'}</td>
              <td><span className="symbol-name">{row.symbol}</span>{row.index&&<Badge kind="outline">Index</Badge>}<small className="symbol-description">{row.name}</small></td>
              <td>{row.marketDate??'NO CAPTURE'}<small className="screener-date">{row.sessions} continuous session{row.sessions===1?'':'s'}</small></td>
              <td className="num" data-sort-value={row.sortValue===null?'MISSING':'AVAILABLE'}>{row.sortValue===null?'—':number(row.sortValue)}</td>
              <td><FeatureStates row={row}/></td>
              <td><RowState row={row} sort={report.sort}/></td>
            </tr>)}</tbody>
          </table></div>
        : <div className="panel-body"><Empty title="No row matches this filter">No panel row has an AVAILABLE value for {label(report.sort)} at this session date. Clear the Rows filter to see every row and its own feature states.</Empty></div>}
    </section>
    <p className="read-context muted">Source {report.source} · proof {report.sessionProof} · surface {report.surface} · Rows {report.availability} · <time dateTime={report.cutoff}>{report.cutoff}</time></p>
    <p className="muted">{report.note}</p>
  </>;
}

export function ScreeningView({report,draft,onDraft,onApply,busy=false,error=''}:{report:ScreeningReport|null;draft:ScreeningQuery;onDraft:(next:ScreeningQuery)=>void;onApply:()=>void;busy?:boolean;error?:string}) {
  return <div className="page-stack screening-page">
    <PageHeader eyebrow="PERSONAL DIAGNOSTICS · READ ONLY" title="Descriptive screening" subtitle="Ranks the retained pilot panel by already-projected personal feature values. Descriptive only; not an order, signal or recommendation."><span className="readonly-label"><Icon name="shield"/>Read-only diagnostics</span></PageHeader>
    <ScreeningLimitations lines={report?.limitations}/>
    <ScreeningControls draft={draft} onDraft={onDraft} onApply={onApply}/>
    {busy&&!report&&<LoadingSkeleton label="Loading descriptive screening…"/>}
    {error&&<Notice kind="error">{error}</Notice>}
    {report&&<ScreeningResults report={report}/>}
  </div>;
}

export function PersonalScreeningPage() {
  const [draft,setDraft]=useState<ScreeningQuery>(()=>defaultScreeningQuery(jakartaToday()));
  const [query,setQuery]=useState<ScreeningQuery>(draft);
  const {report,error,busy}=useScreening(query);
  return <ScreeningView report={report} draft={draft} onDraft={setDraft} onApply={()=>setQuery(draft)} busy={busy} error={error}/>;
}
