# IDX Stock Intelligence — Bootstrap Documentation

Personal, local-first Indonesian stock intelligence system.

## Decision

Use a **new sibling repository** for the stock project and reuse the existing `local-ai-infra` repository as shared AI runtime infrastructure.

Recommended layout:

```text
~/Developer/...
├── local-ai-infra/
└── idx-stock-intelligence/
```

Do not copy or clone `local-ai-infra` inside the stock repository.

`local-ai-infra` remains reusable infrastructure.  
`idx-stock-intelligence` owns market data, portfolio logic, risk rules, replay, and stock-domain decisions.

The stock system must still work when AI is unavailable.

> **Current phase: PHASE 0 — DATA & RIGHTS FEASIBILITY SPIKE.**
>
> **ZERO-COST PILOT MODE:** a fixed 10-equity + IHSG manual collector now admits only independently confirmed-session bars into append-only PostgreSQL revisions. There is no trading recommendation. See [pilot commands and limits](docs/ZERO_COST_PILOT.md).

Run collection through `bash scripts/pilot-eod.sh --from YYYY-MM-DD --to YYYY-MM-DD`
after independently recording the completed session. [Hardening results](docs/PILOT_HARDENING.md)
and the [Phase 0 exit checklist](docs/PHASE0_EXIT_CHECKLIST.md) document durable
failure reports, offline reproduction and the **0/10** real-session soak gate.

The system is EOD-first, local-first, and aims for zero-paid-data sources where feasible. Daily operation must not depend on manual market-file downloads. Trustworthy chronology, reproducibility, and explicit uncertainty matter more than feature count.

## Technology and boundaries

- .NET 10 owns orchestration, canonical validation, feature calculations, chronology, and later decision logic.
- PostgreSQL stores canonical history and revisions.
- Python performs bounded fetch/download/archive/parse/normalize work and emits a versioned manifest.
- The local filesystem stores immutable raw artifacts; datasets are never committed.
- Deterministic logic comes before any LLM explanation.
- `local-ai-infra` is optional external shared infrastructure for a later phase. Phase 0 has no AI dependency.

The complete conceptual sibling layout is:

```text
Microservices/
├── aiagent_content/
├── local-ai-infra/
└── idx-stock-intelligence/
```

There are no cross-repository source imports, copied infrastructure, or Git submodules.

## Local validation

Requirements: .NET SDK 10, Python 3.12+, and optionally Docker Compose for PostgreSQL.

```bash
dotnet build IdxStockIntelligence.slnx
dotnet run --project tests/IdxStockIntelligence.Tests
PYTHONPATH=collectors/python/src python3 -m unittest discover -s collectors/python/tests -v
docker compose config
```

Copy `.env.example` to ignored, untracked `.env` and choose a local password before starting PostgreSQL. The manual Python pilot requires the locally loaded EODHD token; the .NET worker validates archived evidence and persists revisions without contacting a provider.

Review `docs/PHASE0_DATA_SPIKE.md`, `docs/DATA_SOURCE_MATRIX.md`, and `docs/DATA_SPIKE_RESULT.md` before source experimentation. No source may be marked PASS without rights and technical evidence. The private Free-plan prospective pilot is authorized within its documented limits; production-feed promotion remains uncleared. **2022-present remains BLOCKED_BY_ENTITLEMENT** and does not block the prospective pilot.
