# Coding Agent Prompt — Phase 0

Implement ONLY the Data & Rights Feasibility Spike.

Read:

- README.md
- AGENTS.md
- .ai/RULES.md
- docs/ARCHITECTURE.md
- docs/PHASE0_DATA_SPIKE.md
- docs/LOCAL_AI_INFRA_INTEGRATION.md

## Constraints

- .NET 10 main application
- PostgreSQL
- Python allowed for retrieval/parsing
- local files for raw artifacts
- no web UI
- no LLM dependency
- no microservices
- no Redis/Kafka
- no realtime architecture
- no broker-flow/theme engine

## Implement only enough to test

- source metadata
- ingestion-run manifest
- raw artifact archive + hash
- stable instrument identity
- exchange session/calendar
- OHLCV revisions
- IHSG history
- validation
- idempotent ingestion
- correction/revision handling
- missing-session recovery
- stale/degraded states
- EMA20/EMA50
- ATR14
- prior rolling high/low
- volume ratio
- relative performance vs IHSG
- strict as-of replay test
- reconciliation report

## Deliverables

- runnable spike
- tests
- `docs/DATA_SOURCE_MATRIX.md`
- `docs/DATA_SPIKE_RESULT.md`
- known gaps
- runtime/request/storage measurements
- explicit GO / SMALLER_GO / STOP conclusion

Do not implement later product phases.

Before marking work complete, run .NET and Python tests, validate Compose if changed, inspect Git status, confirm no secrets or datasets are tracked, and record evidence/unknowns in the Phase 0 docs. A technical response is not proof of usage rights.

Python remains a bounded fetch/parse process that emits a versioned manifest; it is not a web service. Missing data is never zero, corrections append revisions, and as-of queries never see future availability.
