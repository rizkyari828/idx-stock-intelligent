import { jakartaToday, type ExpectedHolding } from './view.js';
export type DraftHolding = ExpectedHolding & { accounts?: string[]; mandates?: string[] };
export type ReconciliationDraft = { version: 1; portfolioId: string; expectedCash: string; holdings: DraftHolding[]; through: string; cutoff: string };
export const draftKey = (portfolioId: string) => `idx.reconcile.draft.v1:${portfolioId}`;
const text = (value: unknown, max: number): value is string => typeof value === 'string' && value.length <= max;
function inputOnly(value: unknown, portfolioId: string): ReconciliationDraft | null {
 const d = value as ReconciliationDraft | null;
 if (!d || d.version !== 1 || d.portfolioId !== portfolioId || !text(d.expectedCash,64) || !text(d.through,10) || !/^\d{4}-\d{2}-\d{2}$/.test(d.through) || Number.isNaN(Date.parse(d.through)) || !text(d.cutoff,19) || d.cutoff !== '' && (!/^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}(?::\d{2})?$/.test(d.cutoff) || Number.isNaN(Date.parse(d.cutoff))) || !Array.isArray(d.holdings) || d.holdings.length > 200) return null;
 if (d.through < '1900-01-01' || d.through > jakartaToday() || new Date(d.through).toISOString().slice(0,10) !== d.through || d.cutoff && (new Date(d.cutoff+'+07:00').getTime()>Date.now() || d.cutoff<'1900-01-01')) return null;
 const holdings: DraftHolding[] = [];
 for (const h of d.holdings) {
  if (!h || !text(h.symbol,50) || !text(h.shares,64) || h.instrumentId !== null && !text(h.instrumentId,36) || h.averageCost !== null && !text(h.averageCost,64)) return null;
  if (h.accounts !== undefined && (!Array.isArray(h.accounts) || !h.accounts.every(a=>text(a,1024*1024))) || h.mandates !== undefined && (!Array.isArray(h.mandates) || !h.mandates.every(m=>['FAST_SWING','LONG_SWING','INVEST'].includes(m)))) return null;
  holdings.push({instrumentId:h.instrumentId,symbol:h.symbol,shares:h.shares,averageCost:h.averageCost,...(h.accounts?{accounts:h.accounts}:{}),...(h.mandates?{mandates:h.mandates}:{})});
 }
 return {version:1,portfolioId,expectedCash:d.expectedCash,holdings,through:d.through,cutoff:d.cutoff};
}
export function readDraft(portfolioId: string, storage?: Pick<Storage,'getItem'>) {
 try {const raw=(storage??localStorage).getItem(draftKey(portfolioId));return raw && raw.length<=1024*1024 ? inputOnly(JSON.parse(raw),portfolioId) : null;} catch {return null;}
}
export function saveDraft(draft: ReconciliationDraft, storage?: Pick<Storage,'setItem'|'removeItem'>) {
 try {
  const browserStorage=storage??localStorage;
  if (!draft.expectedCash && !draft.holdings.length) {browserStorage.removeItem(draftKey(draft.portfolioId));return '';}
  const clean=inputOnly(draft,draft.portfolioId);if(!clean)throw new Error('Invalid draft');
  const raw=JSON.stringify(clean);if(raw.length>1024*1024)throw new Error('Draft too large');
  browserStorage.setItem(draftKey(draft.portfolioId),raw);return '';
 } catch {return 'Draft could not be saved in this browser. Inputs remain available on this page.';}
}
export function clearDraft(portfolioId: string, storage?: Pick<Storage,'removeItem'>) {
 try {(storage??localStorage).removeItem(draftKey(portfolioId));return '';} catch {return 'Draft could not be removed from browser storage.';}
}
