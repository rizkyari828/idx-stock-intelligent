# Coding Agent Prompt — Phase B.1 technical features in API and UI

```text
Cheapest sufficient (use this):
DeepSeek
Model: DeepSeek Flash
```

Routing classifies this CORRECTNESS_SENSITIVE/STRONG, whose resolved model is already
`deepseek-flash`. Escalate only if the availability semantics below turn out ambiguous.

## Task

Expose the technical features that **already exist** and render them on the stock view.
Do not write new indicator math.

1. Backend: read-only endpoint returning, per instrument and date, the thirteen features
   from `src/IdxStockIntelligence.Application/ScreenerTechnicalArithmetic.cs`:
   `changePercent, ema20, ema50, atr14, atrPercent, priorHigh20, priorLow20,
   distanceToHighPercent, volumeRatio20, dailyValueProxyIdr, monetaryLiquidity20Idr,
   rs20Pp, rs60Pp`.
2. Every feature must carry its **availability** exactly as the arithmetic reports it:
   `AVAILABLE`, `WARMUP` (insufficient sessions), `UNAVAILABLE` + reason. Never emit a
   number for a warmup or unavailable feature, and never substitute 0.
3. Frontend: render these on `frontend/src/StocksPage.tsx` with the availability state
   visible (e.g. "warmup — needs 50 sessions"), alongside the existing DEGRADED badge.

## Read first

`src/IdxStockIntelligence.Application/ScreenerTechnicalArithmetic.cs`,
`src/IdxStockIntelligence.Application/ScreenerEvidenceV02Technical.cs`,
`src/IdxStockIntelligence.Api/Program.cs` (endpoint style),
`src/IdxStockIntelligence.Infrastructure/ScreenerEvidenceV02AsOfReader.cs`,
`frontend/src/StocksPage.tsx`, `frontend/src/dataHealth.tsx`.

## Important constraint discovered up front

`ScreenerTechnicalArithmetic` is `internal` to the Application assembly. Infrastructure
and Api cannot call it directly, so add a small **public Application facade** (for
example `TechnicalFeatures.Project(series, benchmark, ...)`) rather than duplicating the
formulas or widening internals with `InternalsVisibleTo`.

Also state this honestly in the UI and in your report: the pilot currently holds only a
handful of canonical sessions, so `ema50`, `rs60Pp` and other long-window features will
be `WARMUP` for a while. That is correct behaviour, not a bug.

## Do not

- Do not compute any indicator yourself, change a period, or smooth differently.
- Do not mark a feature `AVAILABLE` when the window is short.
- Do not touch screener readiness, admission or any frozen contract.
- Do not add a caching table or persistence.

## Acceptance

- Focused .NET tests: AVAILABLE when the window is met, WARMUP one session short,
  UNAVAILABLE with reason passed through, and index symbols omitting share-volume fields.
- Full root `dotnet test` from the repo root passes (baseline now 1,271 total / 1,067
  passed / 204 skipped / 0 failed); report the new totals.
- `cd frontend && npm run build && npm test` passes with a test asserting warmup is shown
  as warmup, not as a blank or zero.
- `git diff --check` clean; no push. Report the exact JSON shape and which features are
  currently WARMUP for the real panel.
