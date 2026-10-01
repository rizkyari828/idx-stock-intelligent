import { useEffect, useRef, useState } from 'react';
import { errorMessage } from './api';
import { loadInstruments, registryCheck, registerMissing, type RegistrationOutcome } from './instrumentRegistration';
import type { DraftHolding } from './reconciliationDraft';
import type { Instrument } from './view';
import { Notice } from './ui';
export function MissingInstruments({holdings,through,onInvalidate,onRegistryChanged,onBusy}: {holdings:DraftHolding[];through:string;onInvalidate:()=>void;onRegistryChanged:()=>Promise<void>;onBusy:(busy:boolean)=>void}) {
 const [registry,setRegistry]=useState<Instrument[]|null>(null),[checking,setChecking]=useState(false),[error,setError]=useState(''),[review,setReview]=useState(false),[busy,setBusy]=useState(false),[outcomes,setOutcomes]=useState<RegistrationOutcome[]>([]),[message,setMessage]=useState(''),[revision,setRevision]=useState(0);
 const pending=useRef<AbortController|null>(null);
 const symbols=JSON.stringify([...new Set(holdings.map(h=>h.symbol.trim().toUpperCase()).filter(Boolean))]);
 useEffect(()=>{
  const controller=new AbortController();pending.current?.abort();pending.current=controller;setRegistry(null);setError('');setReview(false);
  const selected=JSON.parse(symbols) as string[];setChecking(selected.length>0);
  if(selected.length)void loadInstruments(through,controller.signal).then(next=>{if(!controller.signal.aborted)setRegistry(next);}).catch(e=>{if(!controller.signal.aborted)setError(errorMessage(e));}).finally(()=>{if(!controller.signal.aborted)setChecking(false);});
  return ()=>controller.abort();
 },[symbols,through,revision]);
 const checks=registry?registryCheck(JSON.parse(symbols),registry):[];
 const missing=checks.filter(c=>c.status==='MISSING'),conflicts=checks.filter(c=>c.status==='CONFLICT');
 async function confirm() {
  if(busy||checking||!review||!missing.length)return;
  const controller=pending.current!;setBusy(true);onBusy(true);onInvalidate();setOutcomes([]);setError('');setMessage('Registering…');
  try {
   const next=await registerMissing(missing.map(c=>c.symbol),through,undefined,outcome=>setOutcomes(rows=>[...rows,outcome]),controller.signal);
   if(!controller.signal.aborted){setRegistry(next.registry);setReview(false);setMessage(next.outcomes.some(o=>o.status==='FAILED')?'Partial/conflict failure. Successful identities remain registered. Review remaining symbols to retry.':'Completed. Compare Portfolio again; registration creates no positions.');}
  } catch(e) {if(!controller.signal.aborted){setError(errorMessage(e));setRegistry(null);setMessage('Registration or registry refresh failed. Reported successes remain registered. Recheck before retrying.');}}
  finally {if(controller.signal.aborted)setMessage('Registration interrupted. Recheck registry before retrying; completed identities remain registered.');setBusy(false);onBusy(false);void onRegistryChanged().catch(e=>setError(errorMessage(e)));}
 }
 if(!JSON.parse(symbols).length)return null;
 return <section className="page-stack" aria-label="Snapshot instrument registry"><h3>Instrument registry</h3>{checking?<Notice>Checking registry…</Notice>:error?<Notice kind="error">{error}</Notice>:registry&&<p role="status">{checks.length} snapshot symbols · {checks.filter(c=>c.status==='REGISTERED').length} already registered · {missing.length} missing{conflicts.length?` · ${conflicts.length} conflicts`:''}</p>}
 {registry&&!missing.length&&!conflicts.length&&<Notice>All symbols are registered EQUITY instruments.</Notice>}{conflicts.length>0&&<Notice kind="error">{conflicts.map(c=>c.symbol).join(', ')}: conflicting or non-EQUITY identities. Resolve separately; existing metadata will not be overwritten.</Notice>}
 {!review&&missing.length>0&&<button type="button" className="button" disabled={busy||checking} onClick={()=>setReview(true)}>Review Missing Instruments</button>}
 {review&&<div className="page-stack"><p>Register only these missing EQUITY identities. Display names use the symbol; no company legal names are assumed. Symbol effective from: <strong>{through}</strong> (the selected snapshot date, not an IPO date). Source accounts and mandates are informational.</p><div className="table-scroll" tabIndex={0} aria-label="Missing instrument registration review"><table><thead><tr><th>Symbol</th><th>Type</th><th>Display name</th><th>Source accounts</th></tr></thead><tbody>{missing.map(c=><tr key={c.symbol}><td>{c.symbol}</td><td>EQUITY</td><td>{c.symbol}</td><td>{[...new Set(holdings.filter(h=>h.symbol.trim().toUpperCase()===c.symbol).flatMap(h=>h.accounts??[]))].join(', ')||'—'}</td></tr>)}</tbody></table></div><div className="record-actions"><button type="button" className="button ghost" disabled={busy} onClick={()=>setReview(false)}>Cancel Registration</button><button type="button" className="button primary" disabled={busy||checking||!missing.length} onClick={()=>void confirm()}>{busy?'Registering…':`Register ${missing.length} Instruments`}</button></div></div>}
 {message&&<Notice>{message}</Notice>}{outcomes.length>0&&<ul aria-label="Registration outcomes">{outcomes.map(o=><li key={o.symbol}>{o.symbol}: {o.status} — {o.message}</li>)}</ul>}<button type="button" className="text-button" disabled={busy||checking} onClick={()=>setRevision(n=>n+1)}>Recheck registry</button></section>;
}
