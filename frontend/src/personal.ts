import { useEffect, useState } from 'react';
import { api, errorMessage } from './api.js';

// Personal, non-canonical capture (phase 2: GET /api/personal/bars). Prices stay the stored
// strings: this module never parses them to numbers and never substitutes for a canonical bar.
export type PersonalBarRow = {
  symbol: string; open: string; high: string; low: string; close: string;
  adjustedClose: string; volume: number; qualityStatus: string;
};
export type PersonalBarSession = { sessionDate: string; sessionProof: string; capturedAt: string; rows: PersonalBarRow[] };
export type PersonalBars = { source: string; generatedAt: string; sessions: PersonalBarSession[] };

export const personalBarsRoute = '/personal/bars';
export const PERSONAL_BAR_NOTE = 'Local personal capture — no independent session proof; not a canonical bar.';

export const loadPersonalBars = (signal?: AbortSignal) => api<PersonalBars>(personalBarsRoute, undefined, signal);

/** One read per mount. An empty store is a normal result; only a failed read sets error. */
export function usePersonalBars() {
  const [bars,setBars] = useState<PersonalBars | null>(null);
  const [error,setError] = useState('');
  useEffect(()=>{
    const controller = new AbortController();
    void loadPersonalBars(controller.signal)
      .then(next=>{ if(!controller.signal.aborted) setBars(next); })
      .catch(e=>{ if(!controller.signal.aborted) setError(errorMessage(e)); });
    return ()=>controller.abort();
  },[]);
  return { bars, error };
}

const normalized = (symbol: string) => symbol.trim().toUpperCase();

/** Newest stored session that holds this exact symbol. Symbols are compared, never approximated. */
function newestPersonalCapture(bars: PersonalBars | null | undefined, symbol: string) {
  if (!bars || !symbol) return null;
  const sessions = [...bars.sessions].sort((left, right) => right.sessionDate.localeCompare(left.sessionDate));
  for (const session of sessions) {
    const row = session.rows.find(candidate => normalized(candidate.symbol) === normalized(symbol));
    if (row) return { session, row };
  }
  return null;
}

/**
 * A personal bar is surfaced only when no canonical bar exists for its session: the personal
 * session must be strictly newer than the newest canonical date. A canonical bar for the same
 * date always wins, and a capture is withheld until the selected knowledge cutoff has reached
 * its capturedAt, so a historical view cannot expose a later capture.
 */
export function personalBarWithoutCanonical(
  bars: PersonalBars | null | undefined, symbol: string, newestCanonicalDate: string | null, cutoff: string,
) {
  const match = newestPersonalCapture(bars, symbol);
  if (!match) return null;
  if (newestCanonicalDate !== null && match.session.sessionDate <= newestCanonicalDate) return null;
  const captured = Date.parse(match.session.capturedAt);
  const known = Date.parse(cutoff);
  if (!Number.isFinite(captured) || !Number.isFinite(known) || captured > known) return null;
  return { sessionDate: match.session.sessionDate, row: match.row };
}
