import { useEffect, useState } from 'react';
import { Empty, LoadingSkeleton, Notice } from './ui.js';
import { Facts } from './ScreenerPage.js';
import { number } from './screener.js';
import { horizons, horizonCounts, outcomeLabel, OutcomeSession, type OutcomeCell, type OutcomeState, type Horizon } from './outcomes.js';
import type { DecisionRun } from './decisions.js';

export function OutcomeDetails({cell,policy,assessedAt,capturedAt,targetSession}:{cell:OutcomeCell;policy:string;assessedAt:string;capturedAt:string;targetSession:string|null}) {
 return <div className="page-stack"><Facts values={{'Outcome policy':policy,'Horizon · completed sessions':cell.horizonSessions,
  'Outcome state':outcomeLabel(cell),'Assessment state':cell.state,Reason:cell.reason,'Record status':cell.materialized?'Committed terminal record':'Unmaterialized preview',
  'Captured at':capturedAt,'Captured target session':targetSession,'Anchor market date':cell.anchorMarketDate,'Anchor close':cell.anchorClose,
  'Horizon market date':cell.horizonMarketDate,'Horizon close':cell.horizonClose,'Price return · %':cell.priceReturnPct,
  'Read assessment cutoff':assessedAt,'Outcome known at':cell.outcomeKnownAt,'Outcome recorded at':cell.recordedAt}}/>
 <p className="muted">Exact returned values and UTC clocks. Read assessment time is separate from capture time and committed outcome knowledge/recording times. Price return excludes cash dividends. Evidence manifests and raw provider payloads are not exposed by this API.</p></div>;
}
export function OutcomeView({run,state,onEvaluate,onRead}:{run:DecisionRun;state:OutcomeState;onEvaluate:(n:Horizon)=>void;onRead:()=>void}) {
 const r=state.result;
 return <section className="panel panel-body page-stack outcome-panel" aria-label="Outcomes" aria-busy={state.busy}>
  <h2 id="outcomes-heading">Outcomes</h2><p>Forward price observations for every captured instrument. Price return excludes cash dividends. Outcomes remain separate from captured eligibility, setup and verification.</p>
  <p className="muted">Evaluate uses currently retained evidence. It does not fetch market data or force an outcome. Reads may show unmaterialized previews; only explicit evaluation can record terminal results.</p>
  <div className="wrap" aria-label="Manual outcome evaluation">{horizons.map(n=><button key={n} className="button" disabled={state.busy||run.rows.length===0} onClick={()=>onEvaluate(n)}>{state.evaluating===n?`Evaluating +${n}…`:`Evaluate +${n}`}</button>)}<button className="button ghost" disabled={state.busy} onClick={onRead}>Retry read</button></div>
  {state.busy&&<LoadingSkeleton label={state.evaluating?`Evaluating +${state.evaluating} against retained evidence…`:'Loading outcome assessments…'}/>}
  {state.message&&<p role="status">{state.message}</p>}{state.lastEvaluationAt&&<p>Last manual assessment · UTC: <time dateTime={state.lastEvaluationAt}>{state.lastEvaluationAt}</time></p>}
  {state.error&&<Notice kind="error">{state.error}</Notice>}
  {run.rows.length===0?<Empty title="No captured instruments">No outcome cells are synthesized for an empty capture.</Empty>:r&&<>
   <p>Read assessment cutoff · UTC: <time dateTime={r.assessedAt}>{r.assessedAt}</time> · Policy: {r.outcomePolicyId}</p>
   {r.summary.materialized===0&&<p>No terminal outcomes recorded. Preview assessments may change when retained evidence changes.</p>}
   <div className="table-scroll" tabIndex={0} aria-label="Outcome counts by horizon; scroll horizontally"><table className="outcome-summary"><caption>Factual counts · captured population {run.rows.length}</caption><thead><tr>{['Horizon','Assessed cells','Unresolved','Terminal','Available','Terminal unavailable'].map(s=><th scope="col" key={s}>{s}</th>)}</tr></thead><tbody>{horizons.map(n=>{const c=horizonCounts(r.cells,n);return <tr key={n}><th scope="row">+{n}</th>{[c.total,c.unresolved,c.terminal,c.available,c.terminalUnavailable].map((v,i)=><td key={i}>{v}</td>)}</tr>;})}</tbody></table></div>
   <div className="table-scroll" tabIndex={0} aria-label="Captured population outcome matrix; scroll horizontally"><table className="outcome-matrix"><caption>Every captured row × four horizons · expand a cell for chronology and factual detail</caption><thead><tr><th scope="col">Captured instrument</th>{horizons.map(n=><th scope="col" key={n}>+{n}</th>)}</tr></thead><tbody>{run.rows.map(row=><tr key={row.instrumentId} data-outcome-instrument={row.instrumentId}><th scope="row">{row.symbol??row.instrumentId}<small className="amount-secondary">{row.configured?'Configured':'Outside configured scope'}{row.held?' · Held at capture':''}</small></th>{horizons.map(n=>{const c=r.cells.find(c=>c.instrumentId===row.instrumentId&&c.horizonSessions===n);return <td key={n}>{c?<details data-outcome-horizon={n}><summary aria-label={`${row.symbol??row.instrumentId} +${n} outcome details`}><strong className="outcome-state">{outcomeLabel(c)}</strong><small className="amount-secondary">{c.materialized?'Committed':'Preview · not recorded'}</small>
    {c.reason&&<span className="outcome-reason">{c.reason}</span>}{c.state==='AVAILABLE'&&<span className="outcome-values">{c.horizonMarketDate??'Date unknown'}<br/>Close {number(c.horizonClose)}<br/>Price return {c.priceReturnPct===null?'Unavailable':number(c.priceReturnPct)+'%'}</span>}
    </summary><OutcomeDetails cell={c} policy={r.outcomePolicyId} assessedAt={r.assessedAt} capturedAt={run.header.capturedAt} targetSession={run.header.targetSession}/></details>:'Assessment not returned'}</td>;})}</tr>)}</tbody></table></div>
   <a className="text-button" href="#captured-population">Return to captured population</a>
  </>}
 </section>;
}
export function OutcomePanel({run}:{run:DecisionRun}) {
 const [state,setState]=useState<OutcomeState>({result:null,busy:true,evaluating:null,error:'',message:'',lastEvaluationAt:null});
 const [session]=useState(()=>new OutcomeSession(setState));
 useEffect(()=>{void session.read(run.header.runId);return()=>session.invalidate();},[run.header.runId,session]);
 return <OutcomeView run={run} state={state} onEvaluate={n=>{void session.evaluate(run.header.runId,n);}} onRead={()=>{void session.read(run.header.runId);}}/>;
}
