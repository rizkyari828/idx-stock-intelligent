# Personal store — phase 3: UI surfaces

```text
Cheapest sufficient (use this):
DeepSeek
Model: DeepSeek Flash

Routing note: rated NORMAL/BALANCED (qwen3.6 via Ollama, not installed) → run on Flash.
```

Depends on phase 2 (`GET /api/personal/bars`).

## Instruction from the owner

Reuse the existing UI template and components; do **not** redesign. Optimise only for
"easy to use at a glance". Every source file already exists — extend, don't fork.

## Task

1. **A distinct `PERSONAL` badge** — visually different from the existing `DEGRADED`
   badge, because they mean different things: `DEGRADED` is canonical-but-unconfirmed
   quality; `PERSONAL` is *not canonical at all*. Tooltip: "Personal capture — no
   independent session proof; not a canonical bar."
2. **Personal sessions card** on the existing Data Health page: latest personal session
   date, symbol count, and a one-line explanation. Place it beside the existing health
   cards, same visual language.
3. **Stocks view**: when an instrument has no canonical bar for the newest session but the
   personal store has one, show that price with the `PERSONAL` badge and its date. When a
   canonical bar exists, canonical wins and the personal value is not shown. A price is
   never shown without its source badge.
4. Keep it quiet: no new dependency, no new design system, no modal, no extra page.

## Read first

`frontend/src/ui.tsx`, `frontend/src/dataHealth.tsx`, `frontend/src/StocksPage.tsx`,
`frontend/src/api.ts`, `frontend/src/health.test.tsx` (test pattern), phase 2 response shape.

## Do not

- Do not alter the existing `DEGRADED` badge semantics or reuse its styling for `PERSONAL`.
- Do not display a personal price where a canonical bar exists.
- Do not add charts, sorting, filters or settings in this phase.
- No backend changes; consume the phase-2 endpoint as-is.

## Acceptance

- `cd frontend && npm run build && npm test` passes (baseline 105 tests) with new tests:
  `PERSONAL` badge renders for a personal-only instrument; it is absent when canonical
  exists; the Data Health card shows the personal session date; an empty store renders an
  explicit "no personal capture yet" state.
- Report which existing components you reused, and confirm no dependency was added.
- `git diff --check` clean; commit scoped; no push.
