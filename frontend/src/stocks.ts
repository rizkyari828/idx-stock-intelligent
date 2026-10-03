import { api, ApiError, errorMessage, type Request } from './api.js';
import { cutoffIso, type Holding, type Instrument, type PortfolioView } from './view.js';
import { type Context, type ScreenerResponse, type ScreenerRow } from './screener.js';

export type StockRegistry = Instrument & { symbolSource?: string; marketDate: string | null; close: number | null; quality: string };
export type HistoryRow = { date: string; open: number | null; high: number | null; low: number | null;
 close: number | null; volume: number | null; availability: string; reason: string | null;
 evidence: { revisionNumber: number; knownAt: string; retrievedAt: string | null; sourceId: string;
 canonicalQuality: string; volumeUnit: string; volumeBasis: string; marketSegment: string; contentHash: string;
 sessionReference: string | null; sessionKnownAt: string | null; open: string; high: string; low: string; close: string } };
export type StockHistory = { registry: Instrument & { currency: string; symbolSource?: string }; registryAsOf: string; through: string;
 cutoff: string; limit: number; order: string; rows: HistoryRow[] };
export type StockResult = { history: StockHistory; screener: ScreenerResponse | null; screenerError: string;
 portfolio: PortfolioView | null; portfolioError: string };
export type StockState = { result: StockResult | null; busy: boolean; error: string };
export const emptyStockState: StockState = { result:null,busy:false,error:'' };
export const validInstrumentId = (id: string) => /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(id);
export const stockRoute = (id: string) => `/stocks/${encodeURIComponent(id)}`;
export const selectedStock = (result: StockResult): ScreenerRow | null => result.screener?.rows.find(r=>r.instrumentId===result.history.registry.id) ?? null;
export const selectedHolding = (result: StockResult): Holding | null => result.portfolio?.holdings.find(h=>h.position.instrumentId===result.history.registry.id) ?? null;

// The response's exact clock pins all subsequent reads; aborted generations cannot publish.
export class StockSession {
 private controller?: AbortController;
 private generation=0;
 constructor(private publish:(state:StockState)=>void,private request:Request=api) {}
 invalidate(){this.controller?.abort();this.generation++;this.publish(emptyStockState);}
 async load(id:string,context:Context){
  this.invalidate();if(!validInstrumentId(id)){this.publish({...emptyStockState,error:'Invalid stable instrument ID.'});return;}
  const controller=new AbortController();this.controller=controller;const generation=++this.generation;
  this.publish({...emptyStockState,busy:true});
  try {
   const query=new URLSearchParams({through:context.through,limit:'60'});if(context.cutoff)query.set('cutoff',cutoffIso(context.cutoff));
   const history=await this.request<StockHistory>(`/instruments/${id}/history?${query}`,undefined,controller.signal);
   if(controller.signal.aborted||generation!==this.generation)return;
   const pinned=new URLSearchParams({through:history.through,cutoff:history.cutoff});
   let screenerError='',portfolioError='';
   const inHorizon=history.through>='2026-08-24'&&history.through<='2027-08-24';
   if(!inHorizon)screenerError='Outside the frozen Screener history horizon.';
   const [screener,portfolio]=await Promise.all([
    inHorizon?this.request<ScreenerResponse>(`/screener?${pinned}&universe=PILOT&view=all&offset=0&limit=100${context.portfolioId?`&portfolioId=${encodeURIComponent(context.portfolioId)}`:''}`,undefined,controller.signal).catch(e=>{screenerError=errorMessage(e);return null;}):null,
    context.portfolioId?this.request<PortfolioView>(`/portfolios/${context.portfolioId}?${pinned}`,undefined,controller.signal).catch(e=>{portfolioError=errorMessage(e);return null;}):null
   ]);
   if(controller.signal.aborted||generation!==this.generation)return;
   this.publish({result:{history,screener,screenerError,portfolio,portfolioError},busy:false,error:''});
  }catch(e){if(!controller.signal.aborted&&generation===this.generation)this.publish({...emptyStockState,error:e instanceof ApiError&&e.status===404?'Instrument not found.':errorMessage(e)});}
 }
}
