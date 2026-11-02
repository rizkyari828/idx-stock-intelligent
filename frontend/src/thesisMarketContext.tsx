import { useEffect, useState } from 'react';
import { api, errorMessage } from './api.js';
import { cutoffIso, money, type Holding } from './view.js';
import { Badge, Metric, Notice } from './ui.js';
import { number } from './screener.js';
import type { TechnicalFeatureValue, TechnicalFeaturesReport } from './stocks.js';

// Display context only: the newest canonical bar and the two already-projected features. Nothing here writes,
// promotes or infers a decision, outcome or verification state.
const TRACKED = [['changePercent','Change %'],['distanceToHighPercent','Distance to prior high %']] as const;

const qualityKind = (quality: string) => quality === 'VERIFIED' ? 'outline' : quality === 'UNKNOWN' ? 'unknown' : 'degraded';

function FeatureCell({name,label,value}:{name:string;label:string;value:TechnicalFeatureValue | undefined}) {
  const available = value?.availability === 'AVAILABLE' && value.value !== null;
  return <div className="metric" data-feature={name}>
    <span className="metric-label">{label}</span>
    <strong className="metric-value number" data-value={available?String(value.value):'UNAVAILABLE'}>{available?number(value.value):'Unavailable'}</strong>
    <Badge kind={available?'outline':'unknown'}>{value?.availability??'UNAVAILABLE'}</Badge>
    <span className="metric-note">{available?'Available on the retained canonical series':value?.availability==='WARMUP'?`warmup — needs ${value.minimumSessions} sessions`:value?.reason??'No projection reported for this context.'}</span>
  </div>;
}

/**
 * Presentational market context. The bar keeps its own date next to its price, a stale bar says so, and a
 * missing bar is stated explicitly instead of rendering a zero or a blank that reads as zero.
 */
export function MarketContextView({holding,through,report,error='',featuresBusy=false}:{holding:Holding | undefined;through:string;report:TechnicalFeaturesReport | null;error?:string;featuresBusy?:boolean}) {
  if(!holding) return null;
  const market = holding.market;
  const barDate = market.marketDate ?? null;
  const close = market.price ?? null;
  const stale = market.freshness === 'STALE';
  return <section className="panel panel-body thesis-market" aria-label="Canonical market context">
    <div className="section-heading"><h2>Market context</h2><span className="muted">Newest canonical bar at the selected through date</span></div>
    {barDate === null
      ? <Notice kind="review">no canonical bar recorded for {market.displaySymbol} at this through date. No price is shown.</Notice>
      : <>
          <div className="summary-strip portfolio-metrics">
            <Metric label="Bar date" value={barDate} note="The bar's own date"/>
            <Metric label="Close · IDR" value={close===null?'UNAVAILABLE':money(close)} note="Not interpolated or backfilled"/>
            <Metric label="Quality" value={market.quality} note={market.quality==='VERIFIED'?'Canonical VALID':'Retained quality status'}/>
            <Metric label="Freshness" value={market.freshness} note={stale?'Older than the selected through date':'Equals the selected through date'}/>
          </div>
          <p className="market-bar"><span className="number">{close===null?'UNAVAILABLE':`Rp ${money(close)}`}</span>{' · '}Bar date {barDate}{' · '}<Badge kind={qualityKind(market.quality)}>{market.quality}</Badge>{stale&&<> <Badge kind="degraded" icon="warning">STALE</Badge></>}</p>
          {stale&&<Notice kind="review">This bar is older than the selected through date {through}. It is shown with its own date; no price is copied back onto a thesis recorded later.</Notice>}
          {close===null&&<Notice kind="review"><strong>Close is unavailable.</strong> {market.unavailableReason??'The retained quality state does not permit a priced close.'} No zero is substituted.</Notice>}
          <section aria-label="Canonical feature context"><h3>Feature context</h3>
            {error&&<Notice kind="review">Technical projection unavailable: {error} The bar above keeps its own date and quality.</Notice>}
            {!error&&featuresBusy&&<p className="loading-text"><span className="spinner"/>Loading canonical features…</p>}
            {!error&&!featuresBusy&&!report&&<p className="muted">No technical projection was returned for this context.</p>}
            {report&&<div className="stock-features">{TRACKED.map(([name,label])=><FeatureCell key={name} name={name} label={label} value={report.features[name]}/>)}</div>}
            {report&&<p className="read-context muted">As of {report.marketDate??'UNAVAILABLE'} · {report.sessions} continuous retained session{report.sessions===1?'':'s'} · through {report.through}</p>}
          </section>
        </>}
  </section>;
}

/** One bounded read per selected holding and pinned context; an aborted selection cannot publish. */
export function ThesisMarketContext({holding,through,cutoff}:{holding:Holding | undefined;through:string;cutoff:string}) {
  const id = holding?.position.instrumentId ?? '';
  const enabled = !!id && holding?.market.marketDate != null;
  const [report,setReport]=useState<TechnicalFeaturesReport|null>(null);
  const [error,setError]=useState('');
  const [busy,setBusy]=useState(false);
  useEffect(()=>{
    setReport(null); setError(''); setBusy(false);
    if(!enabled) return;
    const controller=new AbortController();
    setBusy(true);
    const params=new URLSearchParams({through,cutoff:cutoffIso(cutoff)});
    void api<TechnicalFeaturesReport>(`/instruments/${id}/technical-features?${params}`,undefined,controller.signal)
      .then(next=>{if(!controller.signal.aborted)setReport(next);})
      .catch(e=>{if(!controller.signal.aborted)setError(errorMessage(e));})
      .finally(()=>{if(!controller.signal.aborted)setBusy(false);});
    return ()=>controller.abort();
  },[id,enabled,through,cutoff]);
  return <MarketContextView holding={holding} through={through} report={report} error={error} featuresBusy={busy}/>;
}
