# Coding Agent Prompt — Phase B.2 descriptive screening

```text
Cheapest sufficient (use this):
DeepSeek
Model: DeepSeek Flash
```

## Task

Give the personal user a screening view over the existing pilot panel that is explicitly
**descriptive**, never `VERIFIED`, and never a claim of tradability or eligibility.

1. New read-only surface (endpoint + page) that ranks the panel by technical features
   already produced for B.1, over a chosen session date.
2. Every row must show its evidence state: which features were `AVAILABLE` for that row
   and which were `WARMUP`/`UNAVAILABLE`. A row must never be silently complete.
3. The page header must carry the literal limitations: personal/unverified data,
   descriptive only, no confirmed trading status, no corporate-action coverage, no
   guaranteed session completeness.
4. Sorting/filtering must operate on `AVAILABLE` values only; a warmup row sorts to the
   end and says why.

## Read first

`src/IdxStockIntelligence.Application/ScreenerEvidenceV02Evaluators.cs` (existing
evaluators and feature states), `src/IdxStockIntelligence.Api/Program.cs`,
`frontend/src/ScreenerPage.tsx` (existing page — decide reuse vs a separate diagnostics
page and say why), `docs/SOURCE_USAGE_POLICY_AND_CAPABILITY_GATING_V0_1_PROPOSAL.md`
(readiness vs diagnostics separation).

## Do not

- Do not reuse, rename or repurpose the `VERIFIED` / `MARKET_ELIGIBLE` / `DATA_READY`
  states. This surface is diagnostics; it must not produce those tokens.
- Do not call the screener admission path, the capture path, or any candidate promotion.
- Do not add thresholds, scores or recommendations that are not already defined somewhere
  reviewed; if a notion is missing, show the raw feature and leave policy out.
- No schema change, no new persistence, no provider call.

## Acceptance

- Tests prove: a warmup row is never sorted as if complete; the header limitations are
  present; no readiness/verification token can be produced by this surface.
- Full root `dotnet test` passes; report totals. `cd frontend && npm run build && npm test`
  passes.
- `git diff --check` clean; no push.
- Report exactly which existing evaluators you reused and which new code you added, so the
  owner can check nothing was silently promoted.
