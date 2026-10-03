import { api, ApiError, errorMessage, type Request } from './api.js';
import { validInstrumentId } from './stocks.js';
export const horizons = [1,5,10,20] as const;
export type Horizon = typeof horizons[number];
export type OutcomeCell = {
 instrumentId:string; horizonSessions:Horizon; resolution:'UNRESOLVED'|'TERMINAL'; materialized:boolean; newlyMaterialized:boolean;
 state:string; reason:string|null; anchorMarketDate:string|null; anchorClose:number|null; horizonMarketDate:string|null;
 horizonClose:number|null; priceReturnPct:number|null; outcomeKnownAt:string|null; recordedAt:string|null;
};
export type Outcomes = {runId:string;horizonSessions:Horizon|null;outcomePolicyId:string;schemaVersion:number;assessedAt:string;
 newlyMaterializedCount:number;summary:{cells:number;available:number;terminalUnavailable:number;unresolved:number;materialized:number;newlyMaterialized:number};cells:OutcomeCell[]};
export type OutcomeState = {result:Outcomes|null;busy:boolean;evaluating:Horizon|null;error:string;message:string;lastEvaluationAt:string|null};
export const outcomeLabel = (c:OutcomeCell) => c.resolution==='UNRESOLVED'?'UNRESOLVED':c.state;
export function horizonCounts(cells:OutcomeCell[],n:Horizon) {
 const selected=cells.filter(c=>c.horizonSessions===n),terminal=selected.filter(c=>c.resolution==='TERMINAL');
 return {total:selected.length,unresolved:selected.length-terminal.length,terminal:terminal.length,
  available:terminal.filter(c=>c.state==='AVAILABLE').length,terminalUnavailable:terminal.filter(c=>c.state!=='AVAILABLE').length};
}
// One active run operation prevents duplicate clicks and refresh races; generations reject transports ignoring abort.
export class OutcomeSession {
 private controller?:AbortController; private generation=0;
 private state:OutcomeState={result:null,busy:false,evaluating:null,error:'',message:'',lastEvaluationAt:null};
 constructor(private publish:(s:OutcomeState)=>void,private request:Request=api,private timeoutMs=65000) {}
 private update(values:Partial<OutcomeState>){this.state={...this.state,...values};this.publish(this.state);}
 invalidate(){this.controller?.abort();this.generation++;this.state={result:null,busy:false,evaluating:null,error:'',message:'',lastEvaluationAt:null};this.publish(this.state);}
 read(id:string){return this.load(id);}
 evaluate(id:string,n:Horizon){if(this.state.busy)return Promise.resolve();return this.load(id,n);}
 private async load(id:string,n?:Horizon){
  if(n===undefined)this.invalidate();
  if(!validInstrumentId(id)){this.update({error:'Invalid decision ID.'});return;}
  const controller=new AbortController(),generation=++this.generation;this.controller=controller;
  let timedOut=false,completed=false;
  const timer=setTimeout(()=>{timedOut=true;controller.abort();},this.timeoutMs);
  const current=()=>generation===this.generation;
  const path='/screener/decision-snapshots/'+encodeURIComponent(id)+'/outcomes';
  this.update({busy:true,evaluating:n??null,error:'',...(n===undefined?{}:{message:'',lastEvaluationAt:null})});
  try {
   if(n!==undefined){
    const assessment=await this.request<Outcomes>(path+'/evaluate',{horizonSessions:n},controller.signal);
    if(!current()||controller.signal.aborted)return;
    completed=true;
    this.update({lastEvaluationAt:assessment.assessedAt,message:assessment.newlyMaterializedCount===0
     ?(assessment.summary.unresolved===assessment.summary.cells
      ?'No outcomes resolved from currently retained evidence. No new terminal outcomes were recorded.'
      :`No new terminal outcomes recorded. Existing terminal outcomes are retained; ${assessment.summary.unresolved} cell(s) remain unresolved.`)
     :`${assessment.newlyMaterializedCount} new terminal outcome(s) recorded; ${assessment.summary.unresolved} cell(s) remain unresolved.`});
   }
   const result=await this.request<Outcomes>(path,undefined,controller.signal);
   if(current()&&!controller.signal.aborted)this.update({result});
  }catch(e){
   if(current()&&(!controller.signal.aborted||timedOut))this.update({error:completed
    ?'Evaluation completed, but the outcome read could not refresh. Retry read; the displayed grid retains its previous assessment time.'
    :timedOut?'Outcome request timed out. Retry read or evaluate again; terminal results are retained.'
    :e instanceof ApiError&&e.status===404?'Decision capture not found.':`Outcome service unavailable: ${errorMessage(e)}`});
  }finally{clearTimeout(timer);if(current())this.update({busy:false,evaluating:null});}
 }
}
