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

> **Current development: first product vertical slice — portfolio ledger, thesis journal,
> canonical valuation, thin .NET API and React UI.** Operational Phase 0 H+1 validation
> continues independently at **1/10**; FullIdx remains **DISABLED**.
>
> [Run API/UI and synthetic acceptance](docs/PRODUCT_SLICE_RUNBOOK.md) ·
> [Ledger contract](docs/PORTFOLIO_LEDGER.md) · [Cost policy](docs/COST_BASIS_V01.md) ·
> [Product data states](docs/PRODUCT_DATA_STATE.md)
>
> Verification: **34 .NET tests, 61 Python tests, frontend test/build, 2 synthetic
> HTTP/PostgreSQL/React acceptance tests and 14 offline restore groups pass.**
>
> This milestone uses **zero EODHD billable units**. No live panel expansion,
> screener, composite score, trading recommendation or AI accounting is implemented.
>
> **ZERO-COST PILOT MODE:** a fixed 10-equity + IHSG manual collector now admits only independently confirmed-session bars into append-only PostgreSQL revisions. There is no trading recommendation. See [pilot commands and limits](docs/ZERO_COST_PILOT.md).

Normal H+1 operation uses `bash scripts/pilot-daily.sh --dry-run`, then
`bash scripts/pilot-daily.sh` after review and independent session proof. The
[daily operator runbook](docs/DAILY_PILOT_WORKFLOW.md) covers selection, catch-up,
database/account gates, and optional dated same-day collection. [Hardening results](docs/PILOT_HARDENING.md)
and the [Phase 0 exit checklist](docs/PHASE0_EXIT_CHECKLIST.md) document durable
failure reports and offline reproduction. The September 30 successful pilot
advanced the real-session soak to **1/10**; the local ledger remains authoritative.

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

Requirements: .NET SDK 10, Python 3.12+, Node 22.12+ for the UI, and Docker Compose for local PostgreSQL.

```bash
dotnet build IdxStockIntelligence.slnx
dotnet test
PYTHONPATH=collectors/python/src python3 -m unittest discover -s collectors/python/tests -v
docker compose config
```

The standard test command uses the .NET 10 Microsoft Testing Platform selected in
`global.json`. The xUnit project explicitly enables its MTP runner; no test
executable workaround or dependency upgrade is required. With RTK, use
`rtk proxy dotnet test` to preserve the MTP invocation.

Offline planning and run tracking require no provider token:

```bash
bash scripts/pilot-eod.sh --from YYYY-MM-DD --to YYYY-MM-DD --dry-run
bash scripts/pilot-daily.sh --dry-run
bash scripts/pilot-eod.sh --soak-report
```

Dates must be independently confirmed completed sessions within the last 330 days.
Same-day collection requires the configurable **19:00 Asia/Jakarta** cutoff and
explicit independent completion evidence; see [same-day policy](docs/SAME_DAY_EOD.md). See
[local workflow procedures and validation](docs/LOCAL_PILOT_WORKFLOW.md) for
reuse/refresh rules, the prospective ledger, restore checks and current **1/10**
soak status. FullIdx collection is **NOT ENABLED**.

Copy `.env.example` to ignored, untracked `.env` and choose a local password before starting PostgreSQL. The manual Python pilot requires the locally loaded EODHD token; the .NET worker validates archived evidence and persists revisions without contacting a provider.

Review `docs/PHASE0_DATA_SPIKE.md`, `docs/DATA_SOURCE_MATRIX.md`, and `docs/DATA_SPIKE_RESULT.md` before source experimentation. No source may be marked PASS without rights and technical evidence. The private Free-plan prospective pilot is authorized within its documented limits; production-feed promotion remains uncleared. **2022-present remains BLOCKED_BY_ENTITLEMENT** and does not block the prospective pilot.
