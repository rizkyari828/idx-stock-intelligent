import { api, ApiError, type Request } from './api.js';
import { cutoffIso } from './view.js';

export type Setup = 'NONE' | 'WATCH' | 'CONFIRMED' | 'FAILED';
export type Eligibility = 'ELIGIBLE' | 'INELIGIBLE' | 'DATA_BLOCKED';
export type Quality = 'COMPLETE' | 'PARTIAL' | 'BLOCKED';
export type Trend = 'POSITIVE' | 'NEUTRAL' | 'NEGATIVE' | 'UNKNOWN';
export type FieldStates = Record<string, { availability: 'AVAILABLE' | 'UNAVAILABLE' | 'WARMUP'; reason: string | null }>;
export type Revision = { sessionDate: string; revision: number; knownAt: string; retrievedAt: string | null; contentHash: string; canonicalQuality: string; source: string };
export type Provenance = {
 priceBasis: string | null; source: string | null; currency: string | null; volumeUnit: string | null;
 volumeBasis: string | null; marketSegment: string | null; revision: number | null;
 knownAt: string | null; retrievedAt: string | null; contentHash: string | null;
 priceSequenceStart: string | null; emaSeedStart: string | null; atrSeedStart: string | null;
 consecutiveSessions: number; referenceSnapshotIds: string[]; canonicalQuality: string | null; currentEvidence: Revision | null;
};
export type Episode = { id: string; startDate: string; confirmationDate: string | null; endDate: string | null;
 endReason: string | null; ageSessions: number; confirmedAgeSessions: number | null; triggerPrice: number | null; watchThreshold: number | null };
export type ScreenerRow = {
 instrumentId: string; symbol: string | null; displayName: string | null; configured: boolean; held: boolean;
 mandate: 'FAST_SWING' | 'LONG_SWING' | 'INVEST' | null; discoveryRank: number | null;
 eligibility: Eligibility; eligibilityReasons: string[]; setup: Setup; setupEvaluated: boolean; setupReasons: string[];
 episode: Episode | null; marketDate: string | null; close: number | null; changePercent: number | null;
 ema20: number | null; ema50: number | null; trend: Trend; priorHigh20: number | null; priorLow20: number | null;
 distanceToHighPercent: number | null; volume: number | null; volumeRatio20: number | null;
 dailyValueProxyIdr: number | null; monetaryLiquidity20Idr: number | null; atr14: number | null; atrPercent: number | null;
 rs20Pp: number | null; rs60Pp: number | null; stale: boolean | null; noTrade: boolean | null; tradingStatus: string | null;
 dataQuality: Quality; dataReasons: string[]; fieldStates: FieldStates; provenance: Provenance;
};
export type MarketContext = { instrumentId: string; marketDate: string | null; trend: Trend;
 volatility: 'NORMAL' | 'ELEVATED' | 'UNKNOWN'; close: number | null; ema20: number | null; ema50: number | null;
 atr14: number | null; atrPercent: number | null; reasons: string[]; fieldStates: FieldStates; provenance: Provenance };
export type Summary = { configured: number | null; eligible: number | null; ineligible: number | null; dataBlocked: number | null;
 evaluated: number | null; candidates: number | null; confirmed: number | null; watch: number | null;
 shortlisted: number | null; omittedConfirmed: number | null; omittedWatch: number | null; insufficientHistory: number | null;
 stale: number | null; unsupported: number | null; held: number; heldOutsideUniverse: number | null };
export type Controls = { view: 'shortlist' | 'all'; setup: 'ALL' | Setup; eligibility: 'ALL' | Eligibility; offset: number; limit: number };
export type ScreenerResponse = { policyId: string; through: string; targetSession: string | null; cutoff: string;
 historyAnchor: string; universe: 'PILOT'; universeSnapshotId: string | null; replayScope: 'FIXED_PILOT_KNOWN_INPUTS';
 inputHash: string; status: Quality; reasons: string[]; summary: Summary; marketContext: MarketContext;
 page: Controls & { total: number }; discoveryIds: string[]; heldIds: string[]; rows: ScreenerRow[] };
export type Context = { through: string; cutoff: string; portfolioId?: string };
export const defaultControls: Controls = { view: 'shortlist', setup: 'ALL', eligibility: 'ALL', offset: 0, limit: 20 };
export const setupLabel = (row: ScreenerRow) => row.setupEvaluated ? row.setup === 'NONE' ? 'NONE / No setup' : row.setup : 'Not evaluated';
export const number = (value: number | null, decimals = 2) => value === null ? 'Unavailable' : new Intl.NumberFormat('en-US', { minimumFractionDigits: decimals, maximumFractionDigits: decimals }).format(value);
export function loadScreener(context: Context, controls: Controls, signal: AbortSignal, pin?: ScreenerResponse, request: Request = api) {
 const params = new URLSearchParams({ through: pin?.through ?? context.through, universe: 'PILOT', ...controls, offset: String(controls.offset), limit: String(controls.limit) });
 if(context.portfolioId)params.set('portfolioId',context.portfolioId);
 if(pin){params.set('cutoff',pin.cutoff);params.set('inputHash',pin.inputHash);}
 else if(context.cutoff)params.set('cutoff',cutoffIso(context.cutoff));
 return request<ScreenerResponse>(`/screener?${params}`,undefined,signal);
}
export type ScreenerState = { result: ScreenerResponse | null; busy: boolean; error: string; refreshRequired: boolean };
export const initialState: ScreenerState = { result: null, busy: false, error: '', refreshRequired: false };
// One request owner per mounted page. Generation also rejects transports that ignore abort.
export class ScreenerSession {
 private pending?: AbortController;
 private generation = 0;
 private pin?: ScreenerResponse;
 private contextKey = '';
 private refreshRequired = false;
 constructor(private readonly publish: (state: ScreenerState) => void, private readonly request: Request = api) {}
 invalidate() { this.pending?.abort();this.generation++;this.pin=undefined;this.refreshRequired=false;this.publish(initialState); }
 async load(context: Context, controls: Controls, refresh = false) {
  const key=JSON.stringify(context);
  if(refresh || key!==this.contextKey){this.invalidate();this.contextKey=key;}
  if(this.refreshRequired)return;
  this.pending?.abort();const controller=new AbortController();this.pending=controller;const generation=++this.generation;
  this.publish({ result: this.pin ?? null, busy: true, error: '', refreshRequired: false });
  try {
   const result=await loadScreener(context,controls,controller.signal,this.pin,this.request);
   if(generation!==this.generation || controller.signal.aborted)return;
   this.pin=result;this.publish({result,busy:false,error:'',refreshRequired:false});
  } catch(error) {
   if(generation!==this.generation || controller.signal.aborted)return;
   this.refreshRequired=error instanceof ApiError && error.status===409 && error.code==='INPUT_CHANGED';
   const message=this.refreshRequired?'Underlying Screener inputs changed. Refresh the evaluation.':error instanceof ApiError && error.status===400?'Check the through date, known-by time, and filters.':error instanceof ApiError && error.status===404?'The selected portfolio is unavailable under this context.':'Screener service or evidence is unavailable. Retry the evaluation.';
   this.publish({result:null,busy:false,error:message,refreshRequired:this.refreshRequired});
  }
 }
}
