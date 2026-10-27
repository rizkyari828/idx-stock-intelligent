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
export type TechnicalFeatureValue = { availability: string; value: number | null; reason: string | null; minimumSessions: number };
export type TechnicalFeaturesReport = { instrumentId: string; symbol: string; name: string; through: string; cutoff: string;
 marketDate: string | null; sessions: number; index: boolean; features: Record<string, TechnicalFeatureValue> };
// Personal, non-canonical fallback: unproven captures from the local file store, never a canonical bar.
export type PersonalTechnicalFeaturesReport = { source: string; sessionProof: string; symbol: string; through: string;
 cutoff: string; marketDate: string | null; runStart: string | null; sessions: number; warmupNote: string;
 features: Record<string, TechnicalFeatureValue> };
export type StockResult = { history: StockHistory; screener: ScreenerResponse | null; screenerError: string;
 features: TechnicalFeaturesReport | null; featuresError: string; personalFeatures: PersonalTechnicalFeaturesReport | null;
 personalFeaturesError: string; portfolio: PortfolioView | null; portfolioError: string };
export type StockState = { result: StockResult | null; busy: boolean; error: string };
export const emptyStockState: StockState = { result:null,busy:false,error:'' };
export const availableFeatureCount = (features: Record<string, TechnicalFeatureValue> | undefined) =>
 Object.values(features ?? {}).filter(state => state.availability === 'AVAILABLE').length;
/** Canonical features are insufficient when they are missing or hold no usable (AVAILABLE) value. */
export const canonicalFeaturesInsufficient = (report: TechnicalFeaturesReport | null) =>
 report === null || availableFeatureCount(report.features) === 0;
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
   let screenerError='',portfolioError='',featuresError='';
   const inHorizon=history.through>='2026-08-24'&&history.through<='2027-08-24';
   if(!inHorizon)screenerError=featuresError='Outside the frozen Screener history horizon.';
   // The features read is a diagnostic over retained canonical bars; its failure stays local to that panel.
   const [screener,portfolio,features]=await Promise.all([
    inHorizon?this.request<ScreenerResponse>(`/screener?${pinned}&universe=PILOT&view=all&offset=0&limit=100${context.portfolioId?`&portfolioId=${encodeURIComponent(context.portfolioId)}`:''}`,undefined,controller.signal).catch(e=>{screenerError=errorMessage(e);return null;}):null,
    context.portfolioId?this.request<PortfolioView>(`/portfolios/${context.portfolioId}?${pinned}`,undefined,controller.signal).catch(e=>{portfolioError=errorMessage(e);return null;}):null,
    inHorizon?this.request<TechnicalFeaturesReport>(`/instruments/${id}/technical-features?${pinned}`,undefined,controller.signal).catch(e=>{featuresError=errorMessage(e);return null;}):null
   ]);
   if(controller.signal.aborted||generation!==this.generation)return;
   // The personal read is a fallback only, at the same pinned knowledge frame: canonical values win and
   // the two are never merged. Outside the frozen canonical horizon features is null, so it still runs.
   let personalFeatures:PersonalTechnicalFeaturesReport|null=null,personalFeaturesError='';
   if(canonicalFeaturesInsufficient(features)){
    const symbol=encodeURIComponent(history.registry.symbol);
    await this.request<PersonalTechnicalFeaturesReport>(`/personal/technical-features/${symbol}?${pinned}`,undefined,controller.signal)
     .then(value=>{personalFeatures=value;}).catch(e=>{personalFeaturesError=errorMessage(e);});
    if(controller.signal.aborted||generation!==this.generation)return;
   }
   this.publish({result:{history,screener,screenerError,features,featuresError,personalFeatures,personalFeaturesError,portfolio,portfolioError},busy:false,error:''});
  }catch(e){if(!controller.signal.aborted&&generation===this.generation)this.publish({...emptyStockState,error:e instanceof ApiError&&e.status===404?'Instrument not found.':errorMessage(e)});}
 }
}
