import { api, ApiError, errorMessage, type Request } from './api.js';
import { validInstrumentId } from './stocks.js';
import type { Episode, FieldStates, MarketContext, Provenance, ScreenerRow, Summary } from './screener.js';

export type DecisionHeader = {
 runId:string; requestId:string; schemaVersion:number; captureKind:string; capturedAt:string; knowledgeCutoff:string;
 recordedAt:string; through:string; targetSession:string|null; historyAnchor:string; policyId:string; universe:string;
 universeSnapshotId:string|null; portfolioId:string|null; inputHash:string; selectedDigest:string; status:string; rowCount:number;
};
export type DecisionRow = Pick<ScreenerRow,'instrumentId'|'symbol'|'configured'|'held'|'discoveryRank'|'eligibility'|'setup'|'setupEvaluated'|'marketDate'|'close'|'mandate'> & {
 episodeId:string|null; shares:number|null; investedCost:number|null; averageCost:number|null; thesisVersionId:string|null;
 thesisVersion:number|null; thesisActive:boolean|null;
 result:Pick<ScreenerRow,'eligibilityReasons'|'setupReasons'|'priorHigh20'|'distanceToHighPercent'|'ema20'|'ema50'|'atr14'|'atrPercent'|'volumeRatio20'|'rs20Pp'|'rs60Pp'|'trend'|'monetaryLiquidity20Idr'|'stale'|'noTrade'|'tradingStatus'|'dataQuality'|'dataReasons'> & {episode:Episode|null; fieldStates:FieldStates; provenance:Provenance};
};
export type DecisionRun = {header:DecisionHeader; result:{reasons:string[]; summary:Summary; rankedCandidateIds:string[]; allViewIds:string[]; shortlistIds:string[]; heldIds:string[]; marketContext:MarketContext}; rows:DecisionRow[]};
export type DecisionHistoryItem = {header:DecisionHeader; row:DecisionRow};
export type DecisionPage<T> = {items:T[]; nextCursor:string|null};
export type DecisionState<T> = {result:T|null; busy:boolean; error:string};
export const decisionRoute = (id:string) => id ? `/decisions/${encodeURIComponent(id)}` : '/decisions';
export const decisionSetup = (row:Pick<DecisionRow,'setup'|'setupEvaluated'>) => row.setupEvaluated ? row.setup==='NONE'?'NONE / No setup':row.setup : 'Not evaluated';
export const abbreviatedHash = (hash:string) => `${hash.slice(0,12)}…`;
export function workspaceRoute(path:string,fallback='portfolio') {
 if(path==='/decisions'||path.startsWith('/decisions/'))return {page:'decisions',stockId:'',runId:path.slice(11)};
 if(path==='/stocks'||path.startsWith('/stocks/'))return {page:'stocks',stockId:path.slice(8),runId:''};
 return {page:fallback,stockId:'',runId:''};
}
// One owner for the three bounded snapshot reads; generations also reject transports ignoring abort.
export class DecisionSession<T> {
 private controller?:AbortController;
 private generation=0;
 constructor(private publish:(state:DecisionState<T>)=>void,private request:Request=api) {}
 invalidate(){this.controller?.abort();this.generation++;this.publish({result:null,busy:false,error:''});}
 async load(path:string,id?:string){
  this.invalidate();
  if(id!==undefined&&!validInstrumentId(id)){this.publish({result:null,busy:false,error:'Invalid decision or instrument ID.'});return;}
  const controller=new AbortController();this.controller=controller;const generation=++this.generation;
  this.publish({result:null,busy:true,error:''});
  try {
   const result=await this.request<T>(path,undefined,controller.signal);
   if(!controller.signal.aborted&&generation===this.generation)this.publish({result,busy:false,error:''});
  }catch(e){if(!controller.signal.aborted&&generation===this.generation)this.publish({result:null,busy:false,error:e instanceof ApiError&&e.status===404?'Decision capture not found.':errorMessage(e)});}
 }
}
