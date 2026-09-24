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
> This repository is a foundation only. It does not yet fetch market data or make trading recommendations.

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
dotnet test IdxStockIntelligence.slnx
python3 -m unittest discover -s collectors/python/tests -v
docker compose config
```

Copy `.env.example` to `.env` and choose a local password before starting PostgreSQL. The worker currently performs only a readiness check; it does not access a provider.

Review `docs/PHASE0_DATA_SPIKE.md`, `docs/DATA_SOURCE_MATRIX.md`, and `docs/DATA_SPIKE_RESULT.md` before source experimentation. No source may be marked PASS without rights and technical evidence. Do not fetch market data until this bootstrap is reviewed.
