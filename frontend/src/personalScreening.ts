import { api } from './api.js';

// Descriptive, personal diagnostics (Phase B.2). Every row carries the exact feature states it has; nothing
// here is canonical, VERIFIED, MARKET_ELIGIBLE or DATA_READY, and no value is ever defaulted to zero.
export type ScreeningFeature = {
  availability: 'AVAILABLE' | 'UNAVAILABLE' | 'WARMUP';
  value: number | null; reason: string | null; minimumSessions: number;
};
export type ScreeningStatus = 'RANKED' | 'NOT_RANKED' | 'NO_PERSONAL_CAPTURE';
export type ScreeningRow = {
  symbol: string; name: string; index: boolean; marketDate: string | null; runStart: string | null; sessions: number;
  status: ScreeningStatus; rank: number | null; sortValue: number | null; sortAvailability: string | null;
  reason: string | null; availableCount: number; warmupCount: number; unavailableCount: number;
  features: Record<string, ScreeningFeature>;
};
export type ScreeningReport = {
  source: string; sessionProof: string; surface: string; panel: string; through: string; cutoff: string;
  sort: string; direction: 'ASC' | 'DESC'; availability: 'ALL' | 'AVAILABLE'; limitations: string[];
  note: string; rows: ScreeningRow[];
};
export type ScreeningQuery = { through: string; sort: string; direction: 'ASC' | 'DESC'; availability: 'ALL' | 'AVAILABLE' };

export const SCREENING_LIMITATIONS = [
  'Personal/unverified data — not a canonical bar and not an exchange tape.',
  'Descriptive only — no tradability, eligibility or recommendation is claimed.',
  'No confirmed trading status is known for these personal sessions.',
  'No corporate-action coverage is retained.',
  'No guaranteed session completeness — a missing or uncited weekday gap ends a personal run.',
];

export const defaultScreeningQuery = (through: string): ScreeningQuery =>
  ({ through, sort: 'changePercent', direction: 'DESC', availability: 'ALL' });

export const loadScreening = (query: ScreeningQuery, signal?: AbortSignal) =>
  api<ScreeningReport>(`/personal/screening?${new URLSearchParams(query)}`, undefined, signal);
