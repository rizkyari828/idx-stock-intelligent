# Phase 0 — Data & Rights Feasibility Spike

## Goal

Prove that core EOD data can be collected automatically, preserved, validated, and replayed before building the full app.

No web UI.  
No AI dependency.  
No realtime stack.

## QA instruments

- BBCA
- BBRI
- ANTM
- RAJA
- VKTR
- ENRG
- PTRO
- DSSA
- GOTO
- one low-liquidity ordinary share

Add adverse cases for:

- IPO
- suspension/no-trade
- ticker change
- delisting if available
- split
- dividend
- rights issue

## Historical target

2022 -> present where available.

## BLOCKER acceptance tests

1. Automated access route is allowed for intended personal/local use.
2. OHLCV retrieval works unattended.
3. IHSG retrieval works unattended.
4. Raw source artifacts and fetch parameters are preserved.
5. Provider units/segments/adjustment semantics are documented.
6. Stable instrument IDs exist.
7. IPO/listing boundaries are correct.
8. Holiday, suspension, no-trade, and missing-provider-row are distinguished.
9. Recent data reconciles with an independent source.
10. Corporate actions do not create fake technical crashes/profits.
11. EMA20/50, ATR, rolling ranges, volume ratio, and relative strength are reproducible.
12. Historical replay cannot see future data.
13. Re-running the same batch is idempotent.
14. Corrections create revisions.
15. Missing sessions are detected and backfilled.
16. 403/429/timeouts/schema changes create explicit degraded states.
17. Full eligible-universe retrieval fits sustainable request/runtime limits.
18. Ten consecutive EOD trading-session runs need no manual market-file downloads.
19. A selected prior decision can be reproduced after restore.

## Optional

- EMA9
- MFI
- CMF
- weekly VWAP validation
- foreign flow
- ownership/free float
- broker history
- insider extraction
- historical fundamentals

Optional failure must not block price-based V0.1.

## Experiment discipline

The question is whether trustworthy, permitted, automatable, reproducible IDX EOD data can be collected without manual daily downloads. QA uses BBCA, BBRI, ANTM, RAJA, VKTR, ENRG, PTRO, DSSA, GOTO, and one systematically selected low-liquidity ordinary share. Target history is 2022 to present where legitimately supported.

Each experiment records source/terms evidence, exact request parameters, retrieval timestamp, response status, raw hash/path, parser version, normalized row counts, validation failures, duration, and request/storage cost. A source remains UNKNOWN until evidence supports another status; technical success never substitutes for permission.

## Additional blocker gates

Together with the acceptance tests above, the reviewed gates explicitly require fetch parameters and hashes, independent reconciliation, safe corporate-action behavior, automatic gap backfill, explicit degraded states, sustainable full-universe operation, and ten consecutive unattended EOD trading sessions. These form the complete 20 Phase 0 gates.

## Exit outcomes

- GO: every blocker passes with documented evidence.
- SMALLER_GO: a deliberately reduced useful dataset passes all gates applicable to its declared scope.
- STOP: permission, quality, reproducibility, or sustainable automation is not feasible.

Current outcome: **NOT EVALUATED**. Bootstrap review must happen before any provider request.
