# Architecture Freeze Candidate — V0.1

## Architecture

```text
Permitted / public sources
        |
        v
Python collectors/parsers
        |
        v
Raw local archive + normalized batch
        |
        v
.NET 10 application
        |
        +--> PostgreSQL
        +--> Feature Engine
        +--> Risk / Strategy Policies
        +--> Portfolio Maintenance
        +--> Immutable Decision Snapshots
        +--> Historical Replay
        +--> Outcome Evaluation
        +--> Optional Local AI Explanation
```

## V0.1 technical features

Keep:

- prior rolling high / low
- EMA20
- EMA50
- ATR14 / ATR%
- volume ratio vs prior 20 sessions
- liquidity
- relative performance vs IHSG

Defer:

- EMA9
- MFI
- CMF
- OHLCV-proxy weekly VWAP
- broker-flow dependency
- HAKA/HAKI
- order book
- theme discovery

## State model

### Eligibility
- ELIGIBLE
- INELIGIBLE
- DATA_BLOCKED

### Setup
- NONE
- WATCH
- CONFIRMED
- FAILED

### Thesis Health
- INTACT
- CHALLENGED
- INVALID
- UNKNOWN

### Action
- HOLD
- REVIEW
- REDUCE
- EXIT

### Modifiers
- TRAIL
- EXTENDED
- EVENT_RISK
- EXECUTION_CONSTRAINED

## Strategy model

### FAST_SWING
First executable policy.

### LONG_SWING
Separate slower advisory/shadow policy.

### INVEST
Thesis-review + exposure monitoring first. If fundamentals are insufficient, return `FUNDAMENTAL_REVIEW_REQUIRED`.

## Dependency direction and Phase 0 execution

```text
permitted source (not selected yet)
             |
             v
bounded Python process -- raw artifact + versioned manifest
             |
             v
.NET Worker -> Application validation/chronology -> PostgreSQL
```

Project dependencies are `Domain <- Application <- Infrastructure <- Worker`. Domain never depends on PostgreSQL, Python, a provider, Ollama, or `local-ai-infra`. Raw bytes live under an ignored data root; PostgreSQL holds provenance, canonical records, revisions, and availability chronology. There is no Python HTTP service.

## Non-negotiable data principles

1. Missing means UNKNOWN, not zero.
2. Stale values retain their source timestamp.
3. Corrections append revisions.
4. Replay uses only information available at its cutoff.
5. Candles are never fabricated.
6. Pre-listing absence is not a data gap.
7. Suspension is not a flat tradable candle.
8. Holiday, no-trade, suspension, and missing-provider-row are distinct.
9. Provider units, segments, adjustments, timestamps, and revisions must be explicit.
10. Raw provenance, parameters, timestamps, and hashes are retained.
11. Provider fallback is explicit and attributable.
12. Deterministic decisions precede LLM explanations.

## Separate V0.1 dimensions

The state groups above remain separate types; do not create a giant combined enum. FAST_SWING, LONG_SWING, and INVEST are separate economic decision models, not indicator-weight presets. Phase 0 has no strategy engine. Later, FAST_SWING is the first executable policy; LONG_SWING starts shadow/advisory; INVEST starts with thesis review and exposure monitoring.

## Feature boundary

Phase 0 may implement only prior rolling high/low, EMA20, EMA50, ATR14/ATR%, volume ratio against the previous 20 sessions, liquidity, and relative performance versus IHSG. This bootstrap defers feature code until real provider semantics and fixtures are approved.

EMA9, MFI, CMF, approximate weekly VWAP, broker flow, HAKA/HAKI, order book, and theme discovery remain deferred.

## Phase 0 project responsibilities

- Domain: stable instrument identity, sessions, bars, quality/availability states, source references, and invariants.
- Application: idempotent ingestion decisions and strict as-of revision queries.
- Infrastructure: filesystem artifact archiving and PostgreSQL migration assets.
- Worker: composition root and future bounded collector orchestration.
- Python collector: fetch/download/archive/parse/normalize only; currently a provider-neutral manifest contract.

## Migration strategy

SQL migrations are ordered immutable files under `src/IdxStockIntelligence.Infrastructure/Migrations`. Apply them transactionally with a reviewed PostgreSQL migration runner in a later Phase 0 task. Never edit an applied migration; add a new one. PostgreSQL is canonical; raw artifact content stays outside it and is referenced by local URI plus SHA-256.
