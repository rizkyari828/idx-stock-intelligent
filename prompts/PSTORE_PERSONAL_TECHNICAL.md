# Personal store — personal-backed technical features (B.1b-2)

```text
Cheapest sufficient (use this):
DeepSeek
Model: DeepSeek Flash
```

## Why

`5821e23` exposes the 13 technical features, but it reads **canonical** bars only. The
canonical history is sparse, so 12 of 13 features are `WARMUP` and the newest continuous
run is one session. The personal store can hold ~195 consecutive sessions after the
backfill, but nothing projects features from it yet. Until this exists, the backfill is
data with no consumer.

## Task

Add a **personal** technical-feature projection, clearly separate from the canonical one.

1. Endpoint `GET /api/personal/technical-features/{symbol}?through=YYYY-MM-DD&cutoff=RFC3339`.
   Read `data/personal/bars/` via the existing `PersonalBarFileStore` (highest revision
   only, exactly as the bars endpoint already does). Match the symbol exactly (trim +
   uppercase); never fuzzy-match.
2. Compute the same thirteen features by calling the existing
   `TechnicalFeatures.Project(...)` facade. **No new indicator math**, no period change.
3. Continuity: use the trailing calendar-continuous run of personal sessions ending at
   `through`, following the same rule the canonical service uses (a cited closure does not
   break continuity; a missing session does, and ends the run).
4. Response must state its provenance explicitly, for example
   `"source": "PERSONAL_FILE_STORE"`, `"sessionProof": "UNPROVEN_PERSONAL"`, plus
   `sessions`, `through`, `marketDate`, `warmupNote`, and the same 13-key `features` object
   with `availability` / `value` / `reason` / `minimumSessions`. Values `null` for every
   non-`AVAILABLE` feature, never `0`.
5. UI (`frontend/src/StocksPage.tsx`): when the canonical profile is insufficient
   (all-`WARMUP` or missing) and a personal run exists, show the personal feature panel
   with the existing `PERSONAL` badge and the run length. Canonical always wins when it
   has more usable features; never mix canonical and personal values in one table.
6. Errors: unknown symbol or no personal capture → explicit 404/empty state, never zeros.

## Do not

- Do not modify the canonical technical endpoint, its service, `ScreenerTechnicalArithmetic`,
  canonical admission, the Worker, migrations, quota or soak.
- Do not persist anything or cache computed features.
- Do not claim personal features are canonical, verified, or `DATA_READY`.
- Quota is exhausted today: no provider calls; fixtures only.

## Acceptance

- Focused .NET tests: features `AVAILABLE` on a synthetic consecutive personal series,
  `WARMUP` one session short, missing session breaks the run, cited holiday does not,
  `PERSONAL` provenance always present, values never `0` for non-available.
- Full root `dotnet test` (report new totals; current baseline 1,296 / 1,092 / 204 / 0).
- `cd frontend && npm run build && npm test` (baseline 116; Node 20.17 warning is
  pre-existing, ignore it).
- `git diff --check` clean; commit scoped; report the commit hash and literal output.

## Note for the owner

The real payoff only appears after the backfill has run. State in your report what the
endpoint will show once `data/personal/bars/` holds ~195 sessions, and confirm you did not
fake such data.
