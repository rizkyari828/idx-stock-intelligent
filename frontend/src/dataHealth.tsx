import { useEffect, useState, type MouseEvent, type ReactNode } from 'react';
import { api, errorMessage } from './api.js';
import { jakartaToday, recorded } from './view.js';
import { Badge, Empty, Icon, LoadingSkeleton, Metric, Notice, PageHeader, QualityBadge } from './ui.js';

export type DataHealthSession = { date: string; bars: number; instruments: number; coverageGap: number; quality: Record<string, number>; flat: number; zeroVolume: number };
export type DataHealthGap = { date: string; knownHoliday: boolean; holidayName: string | null; holidayKind: string | null };
export type DataHealthAnomaly = { date: string; instrumentId: string; symbol: string; observation: string; qualityStatus: string };
export type DataHealthReport = {
  generatedAt: string; lastSession: string | null; expectedPanel: number;
  sessions: DataHealthSession[]; gaps: DataHealthGap[]; anomalies: DataHealthAnomaly[];
  quality: Record<string, number>; degraded: boolean;
};

export const dataHealthRoute = '/data-health';
const healthLink = (onNavigate:(page:string)=>void) => (event:MouseEvent<HTMLAnchorElement>) => {
  if (!event.ctrlKey && !event.metaKey && !event.shiftKey && !event.altKey && event.button === 0) {
    event.preventDefault();
    onNavigate('health');
  }
};

function useDataHealth() {
  const [report,setReport] = useState<DataHealthReport | null>(null);
  const [error,setError] = useState('');
  const [busy,setBusy] = useState(true);
  useEffect(()=>{
    const controller = new AbortController();
    setBusy(true); setError('');
    void api<DataHealthReport>('/data-health', undefined, controller.signal)
      .then(next=>{ if(!controller.signal.aborted) setReport(next); })
      .catch(e=>{ if(!controller.signal.aborted) setError(errorMessage(e)); })
      .finally(()=>{ if(!controller.signal.aborted) setBusy(false); });
    return ()=>controller.abort();
  },[]);
  return { report, error, busy };
}

const qualityLine = (quality: Record<string, number>) =>
  Object.entries(quality).filter(([,count])=>count>0).map(([status,count])=>`${status} ${count}`).join(' · ') || '—';

function DegradedFlag({degraded}: {degraded: boolean}) {
  return degraded
    ? <Badge kind="degraded" icon="warning">DEGRADED</Badge>
    : <Badge kind="info" icon="check">NO NON-VALID ROWS</Badge>;
}

export function TodayCard({report, error, onNavigate}: {report: DataHealthReport | null; error?: string; onNavigate: (page:string)=>void}) {
  const last = report?.lastSession ? report.sessions.find(session=>session.date===report.lastSession) : undefined;
  return <section className="panel panel-body today-card" aria-label="Today at a glance">
    <div className="section-heading"><h2>Today</h2>{report ? <DegradedFlag degraded={report.degraded}/> : null}</div>
    {error
      ? <Notice kind="error">{error}</Notice>
      : !report
        ? <p className="loading-text"><span className="spinner"/>Loading local data health…</p>
        : <>
            <div className="summary-strip portfolio-metrics">
              <Metric label="Last session" value={report.lastSession ?? 'NONE'} note="Newest retained market date"/>
              <Metric label="Bars ingested" value={last?.bars ?? 0} note={`${last?.instruments ?? 0}/${report.expectedPanel} panel instruments`}/>
              <Metric label="Coverage gap" value={last?.coverageGap ?? report.expectedPanel} note="Missing panel members in that session"/>
            </div>
            <p className="muted today-note">Personal, unverified data — not an exchange tape. Flat/zero-volume rows are observations only.</p>
            <a className="text-button" href={dataHealthRoute} onClick={healthLink(onNavigate)}>Open Data Health <Icon name="arrow"/></a>
          </>}
  </section>;
}

export function TodaySection({onNavigate}: {onNavigate: (page:string)=>void}) {
  const {report,error} = useDataHealth();
  return <TodayCard report={report} error={error} onNavigate={onNavigate}/>;
}

function Section({title, count, children}: {title: string; count?: number; children: ReactNode}) {
  return <section className="panel results-panel">
    <div className="table-toolbar"><span className="tab active">{title}{count===undefined?null:<span className="tab-count">{count}</span>}</span></div>
    {children}
  </section>;
}

export function DataHealthView({report}: {report: DataHealthReport}) {
  const last = report.lastSession ? report.sessions.find(session=>session.date===report.lastSession) : undefined;
  const holidayGaps = report.gaps.filter(gap=>gap.knownHoliday).length;
  return <div className="page-stack data-health-page">
    <PageHeader eyebrow="LOCAL DATA · READ ONLY" title="Data Health" subtitle="Coverage of locally retained canonical bars. Personal, unverified data — not an exchange tape."/>
    <div className="summary-strip portfolio-metrics">
      <Metric label="Last session" value={report.lastSession ?? 'NONE'} note="Newest retained market date"/>
      <Metric label="Bars · last session" value={last?.bars ?? 0} note={`${last?.instruments ?? 0}/${report.expectedPanel} panel instruments`}/>
      <Metric label="Sessions retained" value={report.sessions.length} note="Distinct canonical market dates"/>
      <Metric label="Weekday gaps" value={report.gaps.length} note={`${holidayGaps} match a cited holiday`}/>
      <Metric label="Row quality" value={report.degraded ? 'DEGRADED' : 'NO NON-VALID ROWS'} note="Stored quality_status counts"/>
      <Metric label="Generated" value={recorded(report.generatedAt)} note="UTC record time · WIB display"/>
    </div>
    {report.degraded
      ? <Notice kind="review"><strong>DEGRADED rows are present.</strong> At least one retained bar is not marked VALID. This is the local quality status, not exchange verification.</Notice>
      : <Notice>No retained bar is marked outside VALID. That is a local status, not proof of accuracy or exchange provenance.</Notice>}
    <Section title="Retained sessions" count={report.sessions.length}>
      {report.sessions.length
        ? <div className="table-scroll" tabIndex={0} aria-label="Retained sessions"><table className="stocks-table"><caption className="sr-only">Latest canonical revision per instrument and session</caption><thead><tr>{['Session','Bars','Instruments','Panel gap','Flat rows','Zero volume','Quality'].map(column=><th key={column} scope="col">{column}</th>)}</tr></thead><tbody>{report.sessions.map(session=><tr key={session.date}><td>{session.date}</td><td className="num">{session.bars}</td><td className="num">{session.instruments}</td><td className="num">{session.coverageGap}</td><td className="num">{session.flat}</td><td className="num">{session.zeroVolume}</td><td>{qualityLine(session.quality)}</td></tr>)}</tbody></table></div>
        : <div className="panel-body"><Empty title="No canonical sessions">No retained daily bar exists yet. This page never invents one.</Empty></div>}
    </Section>
    <Section title="Weekday gaps" count={report.gaps.length}>
      {report.gaps.length
        ? <div className="table-scroll" tabIndex={0} aria-label="Weekday gaps between retained sessions"><table className="stocks-table"><caption className="sr-only">Weekdays without a retained row between the first and last session</caption><thead><tr>{['Date','State','Holiday','Kind'].map(column=><th key={column} scope="col">{column}</th>)}</tr></thead><tbody>{report.gaps.map(gap=><tr key={gap.date}><td>{gap.date}</td><td>{gap.knownHoliday ? 'Holiday' : 'No retained session row'}</td><td>{gap.holidayName ?? '—'}</td><td>{gap.holidayKind ?? '—'}</td></tr>)}</tbody></table></div>
        : <p className="muted table-note">No weekday gap between the first and last retained session.</p>}
    </Section>
    <Section title="Flat and zero-volume observations" count={report.anomalies.length}>
      <p className="muted table-note">Observations about stored rows only. They do not prove no trade, suspension or a completed session.</p>
      {report.anomalies.length
        ? <div className="table-scroll" tabIndex={0} aria-label="Flat and zero-volume observations"><table className="stocks-table"><caption className="sr-only">Stored rows with flat OHLC or zero volume</caption><thead><tr>{['Date','Symbol','Observation','Quality'].map(column=><th key={column} scope="col">{column}</th>)}</tr></thead><tbody>{report.anomalies.map(item=><tr key={`${item.date}-${item.instrumentId}-${item.observation}`}><td>{item.date}</td><td>{item.symbol}</td><td>{item.observation}</td><td>{item.qualityStatus} · <QualityBadge quality={item.qualityStatus}/></td></tr>)}</tbody></table></div>
        : <p className="muted table-note">No flat or zero-volume row in the retained sessions.</p>}
    </Section>
    <p className="read-context muted">Generated {recorded(report.generatedAt)} · <time dateTime={report.generatedAt}>{report.generatedAt}</time> · Today is {jakartaToday()} in Jakarta.</p>
  </div>;
}

export function DataHealthPage() {
  const {report,error,busy} = useDataHealth();
  if (busy && !report) return <LoadingSkeleton label="Loading data health…"/>;
  if (error) return <div className="page-stack"><PageHeader eyebrow="LOCAL DATA · READ ONLY" title="Data Health" subtitle="Coverage of locally retained canonical bars."/><Notice kind="error">{error}</Notice></div>;
  if (!report) return <Empty title="Data health unavailable">The local API returned no report.</Empty>;
  return <DataHealthView report={report}/>;
}
