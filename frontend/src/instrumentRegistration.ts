import { api, errorMessage, type Request } from './api.js';
import type { Instrument } from './view';
export async function loadInstruments(through?: string, signal?: AbortSignal, request: Request = api) {
 const all: Instrument[] = [];
 for(let offset=0;offset<10000;offset+=200){
  const next=await request<Instrument[]>(`/instruments?limit=200&offset=${offset}${through?'&through='+encodeURIComponent(through):''}`,undefined,signal);
  all.push(...next);if(next.length<200)return all;
 }
 throw new Error('Registry exceeds UI browsing bound.');
}
export function registryCheck(symbols: string[], registry: Instrument[]) {
 return [...new Set(symbols.map(s=>s.trim().toUpperCase()).filter(Boolean))].map(symbol=>{
  const matches=registry.filter(i=>i.hasEffectiveSymbol!==false && i.symbol.trim().toUpperCase()===symbol);
  return {symbol,status:matches.length===0?'MISSING':matches.length===1&&matches[0].type==='EQUITY'?'REGISTERED':'CONFLICT',name:matches.length===1?matches[0].name:symbol};
 });
}
export type RegistrationOutcome = {symbol:string; status:'REGISTERED'|'ALREADY_REGISTERED'|'FAILED'; message:string};
export async function registerMissing(symbols: string[], through: string, request: Request = api, onOutcome: (outcome: RegistrationOutcome)=>void = ()=>{}, signal?: AbortSignal) {
 // Independent canonical identities commit one at a time; successful registrations are never rolled back.
 let registry=await loadInstruments(through,signal,request);
 const outcomes: RegistrationOutcome[]=[];
 for(const check of registryCheck(symbols,registry)) {
  if(signal?.aborted)break;
  let outcome: RegistrationOutcome;
  if(check.status==='REGISTERED')outcome={symbol:check.symbol,status:'ALREADY_REGISTERED',message:'Already registered; metadata unchanged.'};
  else if(check.status==='CONFLICT')outcome={symbol:check.symbol,status:'FAILED',message:'Symbol has conflicting or non-EQUITY identities; resolve in the registry.'};
  else {
   try {
    await request('/instruments',{id:crypto.randomUUID(),name:check.symbol,symbol:check.symbol,type:'EQUITY',validFrom:through},signal);
    outcome={symbol:check.symbol,status:'REGISTERED',message:'Registered EQUITY.'};
   } catch(e) {
    if(signal?.aborted)break;
    const message=errorMessage(e);
    try {registry=await loadInstruments(through,signal,request);outcome=registryCheck([check.symbol],registry)[0].status==='REGISTERED'?{symbol:check.symbol,status:'ALREADY_REGISTERED',message:'Registry confirms an existing EQUITY; no metadata overwritten.'}:{symbol:check.symbol,status:'FAILED',message};}
    catch {outcome={symbol:check.symbol,status:'FAILED',message:message+' Registry refresh failed; check before retrying.'};}
   }
  }
  outcomes.push(outcome);onOutcome(outcome);
 }
 return {outcomes,registry:await loadInstruments(through,signal,request)};
}
