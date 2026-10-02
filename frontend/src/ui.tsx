import { useEffect, useRef, type ReactNode } from 'react';
const paths: Record<string, string> = {
 grid:'M3 3h7v7H3z M14 3h7v7h-7z M3 14h7v7H3z M14 14h7v7h-7z',
 portfolio:'M4 7h16v13H4z M8 7V4h8v3 M4 12h16 M10 12v3h4v-3',
 transactions:'M4 7h15m-4-4 4 4-4 4 M20 17H5m4-4-4 4 4 4',
 journal:'M5 3h14v18H5z M9 7h6 M9 11h6 M9 15h3',
 reconcile:'M8 3h8v3H8z M8 5H5v16h14V5h-3 M8 12l2 2 5-5 M8 18h7',
 transfer:'M7 15V3m-4 4 4-4 4 4 M17 9v12m-4-4 4 4 4-4',
 search:'M10.5 18a7.5 7.5 0 1 1 0-15 7.5 7.5 0 0 1 0 15 M16 16l5 5',
 chart:'M3 3v18h18 M7 15l4-5 4 3 5-7',
 globe:'M21 12a9 9 0 1 1-18 0 9 9 0 0 1 18 0 M3 12h18 M12 3c5 5 5 13 0 18-5-5-5-13 0-18',
 check:'M5 12l4 4L19 6',
 warning:'M12 3 2 21h20L12 3 M12 9v5 M12 17v.1',
 missing:'M21 12a9 9 0 1 1-18 0 9 9 0 0 1 18 0 M8 12h8',
 unknown:'M21 12a9 9 0 1 1-18 0 9 9 0 0 1 18 0 M9.5 9a2.5 2.5 0 1 1 4 2c-1.5 1-1.5 1-1.5 3 M12 17v.1',
 shield:'M12 3 4 6v6c0 4 4 7 8 9 4-2 8-5 8-9V6l-8-3 M8 12l3 3 5-6',
 lock:'M6 10h12v11H6z M8 10V7a4 4 0 0 1 8 0v3 M12 14v3',
 arrow:'M4 12h16m-6-6 6 6-6 6',
 chevron:'m7 10 5 5 5-5',
 plus:'M12 5v14 M5 12h14',
 close:'m6 6 12 12 M6 18 18 6',
 sun:'M16 12a4 4 0 1 1-8 0 4 4 0 0 1 8 0 M12 2v2 M12 20v2 M2 12h2 M20 12h2 M5 5l1.5 1.5 M17.5 17.5 1.5 1.5 M5 19l1.5-1.5 M17.5 6.5 19 5',
 menu:'M4 6h16 M4 12h16 M4 18h16',
 clock:'M21 12a9 9 0 1 1-18 0 9 9 0 0 1 18 0 M12 7v5l3 2',
 file:'M6 3h8l4 4v14H6z M14 3v5h4 M9 12h6 M9 16h4',
 upload:'M12 16V3m-5 5 5-5 5 5 M4 16v5h16v-5',
 download:'M12 3v13m-5-5 5 5 5-5 M4 17v4h16v-4',
 layers:'m12 3 10 6-10 6L2 9l10-6 M2 13l10 6 10-6 M2 17l10 6 10-6',
 info:'M21 12a9 9 0 1 1-18 0 9 9 0 0 1 18 0 M12 11v6 M12 7v.1',
 history:'M3 11a9 9 0 1 1 2 7 M3 4v7h7 M12 7v5l4 2',
 palette:'M12 3a9 9 0 1 0 0 18h2c2 0 3-2 2-3-2-2-1-3 1-3h1c6 0 3-12-6-12 M7 8h.1 M12 6h.1 M17 9h.1 M6 13h.1'
};

export function Icon({name}: {name: string}) { return <svg viewBox="0 0 24 24" aria-hidden="true"><path d={paths[name] || paths.info}/></svg>; }
export function Badge({children, kind='outline', icon}: {children: ReactNode; kind?: string; icon?: string}) { return <span className={`badge ${kind}`}>{icon && <Icon name={icon}/>} {children}</span>; }
const statuses: Record<string, [string,string,string]> = { MATCH:['Match','match','check'], REVIEW:['Review','review','warning'], MISSING_FROM_LEDGER:['Missing from ledger','missing','journal'], MISSING_FROM_EXPECTED:['Missing from expected','missing-expected','file'], UNKNOWN_INSTRUMENT:['Unknown instrument','unknown','unknown'] };
export function StatusBadge({status}: {status: string}) { const [label,kind,icon] = statuses[status] ?? [status,'outline','info'];return <Badge kind={kind} icon={icon}>{label}</Badge>; }
export function PageHeader({eyebrow, title, subtitle, children}: {eyebrow: string; title: string; subtitle: string; children?: ReactNode}) {return <header className="page-header"><div><p className="eyebrow">{eyebrow}</p><h1>{title}</h1><p className="page-subtitle">{subtitle}</p></div><div className="header-actions">{children}</div></header>;}
export function Notice({children, kind='info'}: {children: ReactNode; kind?: string}) {return <div className={`notice ${kind==='review'?'warning':kind}`} role={kind==='error'?'alert':'status'}><Icon name={kind==='error'||kind==='review'?'warning':'info'}/><div>{children}</div></div>;}
export function Metric({label, value, note, tone=''}: {label: string; value: ReactNode; note: string; tone?: string}) {return <div className="metric"><span className="metric-label">{label}</span><strong className={`metric-value number ${tone}`}>{value}</strong><span className="metric-note">{note}</span></div>;}
export function InstrumentCell({symbol, name}: {symbol: string; name?: string}) {return <span className="symbol-cell"><span className="symbol-mark">{symbol.slice(0,1)}</span><span><span className="symbol-name">{symbol}</span>{name && <span className="symbol-description">{name}</span>}</span></span>;}
export function LoadingSkeleton({label="Loading portfolio records…"}: {label?: string}) {return <div className="panel" aria-busy="true"><p className="loading-text"><span className="spinner"/>{label}</p>{[0,1,2].map(i=><div className="skeleton-row" key={i}>{[0,1,2,3].map(n=><div className="skeleton" key={n}/>)}</div>)}</div>;}
export function Empty({title, children}: {title: string; children: ReactNode}) {return <div className="panel empty-state"><Icon name="portfolio"/><h3>{title}</h3><p>{children}</p></div>;}
export function Dialog({title, eyebrow, subtitle, onClose, children}: {title: string; eyebrow: string; subtitle?: string; onClose: () => void; children: ReactNode}) {
 const ref=useRef<HTMLDialogElement>(null);
 useEffect(()=>{const element=ref.current!;const previous=document.activeElement as HTMLElement|null;element.showModal();return ()=>{element.close();if(previous?.isConnected)previous.focus();else document.querySelector<HTMLElement>('#main')?.focus();};},[]);
 return <dialog ref={ref} aria-labelledby="dialog-title" onCancel={e=>{e.preventDefault();onClose();}} onKeyDown={e=>{
  if(e.key!=='Tab')return;const controls=Array.from(ref.current!.querySelectorAll<HTMLElement>('button:not(:disabled),input:not(:disabled),select:not(:disabled),textarea:not(:disabled),a[href],summary,[tabindex="0"]')).filter(el=>el.getClientRects().length);const first=controls[0],last=controls.at(-1);if(e.shiftKey&&document.activeElement===first||!e.shiftKey&&document.activeElement===last){e.preventDefault();(e.shiftKey?last:first)?.focus();}
 }}><div className="dialog-head"><div><p className="eyebrow">{eyebrow}</p><h2 id="dialog-title">{title}</h2>{subtitle&&<p>{subtitle}</p>}</div><button className="icon-button" type="button" aria-label="Close dialog" onClick={onClose}><Icon name="close"/></button></div>{children}</dialog>;
}

export const pages=[['overview','Overview','grid'],['portfolio','Portfolio','portfolio'],['screener','Screener','search'],['transactions','Transactions','transactions'],['thesis','Thesis','journal'],['reconcile','Reconcile','reconcile'],['import','Import / Export','transfer']];
export function Navigation({page,onNavigate}: {page: string; onNavigate: (page: string)=>void}) {
 return <nav>{pages.map(([name,title,icon])=><button className={`nav-link ${page===name?'active':''}`} key={name} aria-current={page===name?'page':undefined} title={title} onClick={()=>onNavigate(name)}><Icon name={icon}/><span className="nav-text">{title}</span></button>)}<div className="nav-label">RESEARCH · COMING LATER</div>{[['Stocks','chart'],['Market','globe']].map(([name,icon])=><button className="nav-link" disabled key={name} title={`${name} · reserved for a future milestone`}><Icon name={icon}/><span className="nav-text">{name}</span><small>Later</small></button>)}</nav>;
}
