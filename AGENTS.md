# AGENTS.md — IDX Stock Intelligence

Read this before coding.

## Mission

Build the smallest trustworthy personal IDX EOD decision-support system.

Correctness, chronology, reproducibility, and low operating cost are more important than feature count.

## Mandatory reading order

1. `README.md`
2. `docs/ARCHITECTURE.md`
3. `docs/PHASE0_DATA_SPIKE.md`
4. `docs/LOCAL_AI_INFRA_INTEGRATION.md`
5. `.ai/RULES.md`
6. task-specific prompt

Do not recursively read the entire repository unless necessary.

## Default current phase

PHASE 0 — DATA & RIGHTS FEASIBILITY SPIKE.

Do not build unless explicitly requested:

- web UI
- realtime services
- broker-flow engine
- theme engine
- autonomous learning agents
- Telegram notifications
- microservices
- Redis/Kafka

## Core rules

- Python fetches/parses.
- .NET owns canonical production logic.
- PostgreSQL stores canonical history, revisions, ledger, and snapshots.
- Local AI is optional.
- Missing data is UNKNOWN, never zero.
- Stale data keeps its original date.
- Corrections create revisions.
- Historical replay is strictly as-of.
- Never invent bars for suspension or no-trade.
- Average buy price is not technical support.
- A losing swing must not silently become INVEST.
- No automatic production-rule promotion.

## Efficient working rules

- Identify exact scope, then search for symbols/files before reading code.
- Inspect the smallest relevant context and expand only when blocked.
- Do not read generated datasets, raw artifacts, logs, binaries, `bin/`, or `obj/`.
- Never speculate about provider fields, units, permissions, adjustment behavior, or revision semantics.
- Avoid scope creep and broad abstractions.
- Add or update tests for important invariants and run them before claiming completion.
- Preserve user work and never commit secrets or market datasets.

Unless explicitly instructed, also do not build authentication or an AI recommendation system.

Additional invariants: pre-IPO absence is not a gap; holiday, no-trade, suspension, and missing-provider-row are distinct; provider fallback is never silent; deterministic decisions precede LLM explanation.
