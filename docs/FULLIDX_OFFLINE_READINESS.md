# FullIdx offline readiness — September 29, 2026

This opt-in benchmark uses synthetic EOD data only. **FullIdx production remains
DISABLED**, production universe mode remains Pilot, and the real soak stays **0/10**.
Zero EODHD endpoints, account queries or billable units; no token load or purchase.

## Reproduce safely

```bash
dotnet restore scripts/OfflineScale --ignore-failed-sources
python3 scripts/check_offline_scale.py --equities 900
```

Supported sizes: 100, 250, 500, 900. Each run creates two uniquely named
`idx_scale_...` databases, drops only those databases in `finally`, and verifies the
normal database's complete price/revision signature did not change. An abrupt
process/host crash may leave owned databases for explicit operator cleanup.
Reports, synthetic archives and SQL dumps live under ignored `data/` locations.
The large benchmark is deliberately excluded from ordinary CI/test discovery.
The generation/ingestion/features process has a 30-minute stop limit; database
statements retain the existing 60-second timeout and backup/restore commands have
180-second limits. These bounds are safety ceilings, not acceptance guarantees.

## Pipeline and limits

Deterministic `SYN0000` onward plus SYNTHETIC_BENCHMARK, 250 weekday sessions from
January 6, 2025. These are synthetic completed-session proofs, not an IDX calendar.
Prices are positive with ordered OHLC, positive deterministic volume and explicit
synthetic units/basis/segment. Each symbol's price slope and level differ. Fixture
knowledge/retrieval clocks are January 1/2/3, 2026, labelled synthetic rather than
claims of historical provider availability.

The harness calls real `RawArtifactArchiver`, `DailyBar`, `PilotValidation`,
`PilotValidation.ContentHash`, `PilotDatabase.Persist`/`ReadRevisions`,
`DailyBarRevisionStore` and `PilotFeatures.Calculate`. It bypasses only the fixed
pilot command's orchestration/panel guard, without changing that guard. PostgreSQL
schema, constraints, append-only triggers and current indexes are unchanged.
Measured metadata-extraction overhead justified caching run identity/knowledge once
per ingestion batch; revision guards and row values remain unchanged. The worker now
passes own/benchmark histories to the unchanged feature engine after measured exact
equivalence. The legacy persistence method hardcodes source_id `eodhd`; inside these
isolated databases its synthetic artifacts have synthetic.example URIs, explicit
synthetic parameters and synthetic parser metadata. No fixture is real EODHD data.

Generated normalized records feed the existing .NET admission path directly;
the Python provider/parser and live account/network layer are not exercised. Thus
this proves local core scale, not provider throughput or broad-universe ingestion
through the guarded production CLI.

## Correctness checks

All six feature families leave WARMUP at 250 sessions. Whole-history and scoped
own/benchmark calls to the same feature engine must match exactly; three selected
symbol isolation checks prevent cross-symbol contamination. A missing own session
breaks continuity (9-session WARMUP); a missing benchmark date makes relative
performance null. One-percent historical corrections append, stale archives and
identical corrections append nothing, prior-cutoff features remain identical and
only affected symbols change latest features. The database rejects updates.

Backup/restore compares every revision value, provenance and knowledge clock, then
independently recomputes prior features in the restored database and compares their
hash. Existing pilot restore checks remain separate from this synthetic benchmark.

## Request arithmetic and preflight

`idx_stock_collector.budget.preflight` is pure provider-independent arithmetic;
ELIGIBLE means counters cover the maximum reserved cost, **not collection permission**.
It is tested offline and is not wired into live collection.
Unknown/invalid counters raise an error (fail closed). Reserve every possible attempt
before networking: `(equities + benchmark) × (1 + retries) × units_per_attempt`.
No Bulk API assumption or refund of failed requests. Extra consumption is the maximum
units minus supplied ordinary daily remaining, floored at zero; the final extra
balance must meet the configured reserve. The total must also fit the run ceiling.

| Equities | Units without benchmark | Units with benchmark | Extra without / with benchmark, assuming 20 ordinary remaining |
|---|---:|---:|---:|
| 100 | 100 | 101 | 80 / 81 |
| 250 | 250 | 251 | 230 / 231 |
| 500 | 500 | 501 | 480 / 481 |
| 900 | 900 | 901 | 880 / 881 |
| 924 | 924 | 925 | 904 / 905 |

Examples use synthetic balances only. Universe 924 + benchmark 1, ordinary remaining
7, extra balance 1000, ceiling 925, reserve 82: **ELIGIBLE**, maximum 925 units,
918 extra. Reserve 83 or ceiling 924: **BUDGET_BLOCKED**. With one retry per symbol,
maximum attempts/units become 1850; a 925-unit ceiling blocks that plan. A synthetic
500-unit extra balance plus 20 ordinary remaining cannot cover 900 + benchmark.

## Future concurrency/retry design — not implemented networking

Initial settings for a separately authorized collector: max concurrency **2**,
request timeout **30 seconds**, retry count **0**, retry backoff **5 seconds** if a
later experiment explicitly permits one retry; a second retry is not planned.
Per-run ceiling defaults to the explicit panel's no-retry maximum, minimum extra
reserve **100 units**. These are proposed bounds, not measured performance promises.
Rate pacing must use a separately verified plan-specific requests/minute limit;
unknown rate/quota/reset counters block a run. Concurrency never overrides pacing.

A future collector must hold one account-wide reservation, reserve before each
attempt and consume the reservation even when the response fails. If retries are
explicitly enabled, preflight reserves their full worst-case cost. A one-unit request
then costs at most two units with one retry, otherwise one. Stop on auth/entitlement,
quota/429, schema or session-validation errors; no retries of those failures. A
permitted transient retry waits at least the backoff and respects Retry-After;
if the wait exceeds the configured run deadline, stop. Persist a sanitized attempt
ledger before sending; crash recovery cannot silently release spent/uncertain quota.
Multi-machine collection requires shared account coordination before deployment.

Extra-call runtime behavior, ordinary plan history/features and provider rights
remain separate gates; arithmetic does not establish any of them. No live collector
settings or production configuration were changed.

## Observed results

| Equities (+1 benchmark) | Original bars | Insert s | Read revisions s | Canonical resolve s | Scoped features s | Backup / restore s | Database MiB |
|---|---:|---:|---:|---:|---:|---|---:|
| 100 | 25,250 | 6.159 | 1.849 | 0.016 | 0.026 | 0.334 / 0.485 | 21.02 |
| 900 | 225,250 | 180.876 | 8.642 | 0.162 | 0.201 | 0.972 / 2.158 | 142.32 |


Measured pre-change 100-equity baseline: insertion **103.079 s**, whole-history
features **1.294 s** versus scoped histories **0.027 s**, with identical outputs.
The metadata extraction change reduced the same profile's insertion to **6.873 s**
(single subsequent measurement, not a statistical speed guarantee). No index change
was necessary. The 900-equity + benchmark result is **PASS**, with 225,250 original bars and
225,259 revisions after nine corrections; PostgreSQL size **149,231,283 bytes**
(142.32 MiB). Profile generation/archive/admission took 1.015 s. Reading all revisions
took 8.642 s, then canonical resolution took 0.162 s. Original whole-history feature
calls took 118.416 s; the scoped rerun took 0.201 s with exact equality.

| Stage, 900 equities + benchmark | Observed seconds |
|---|---:|
| Insert, 5,000-row transactions | 180.876 |
| Scoped as-of features, prior view | 1.159 |
| Scoped latest features | 0.224 |
| Recompute only nine affected symbols | 0.0045 |
| Append nine corrections | 1.242 |
| Stale evidence plus duplicate correction processing | 2.025 |
| 18 indexed prior/latest price queries including Docker/psql startup | 2.583 |
| Backup | 0.972 |
| Restore | 2.158 |
| Restored revision read plus prior/latest feature recomputation | 9.915 |

Exact restored revision/provenance/chronology signatures and both feature views
matched. The normal database retained its exact signature and **55 revisions**.
The point as-of plan used `daily_bar_revision_as_of_idx` (0.074 ms server execution).
The whole-table count used a parallel sequential scan (118.170 ms), expected when
all rows are required. No missing lookup index was demonstrated.

100 and 900 equities were tested; 250 and 500 were not database-benchmarked. These
are individual observations on the current local MacBook/Docker setup, not medians
or CI thresholds. Peak memory, concurrent writers and real provider throughput
were not measured. No claim extends to a 2022-present all-universe backfill.
**Architecture redesign required: NO for this tested local scope.**

The next likely optimizations, if needed, are limiting each chunk's repeated
instrument/artifact metadata and replacing all-history JSON export with scoped
revision reads. Feature resolution for one symbol needs only own/benchmark history.
The global ingestion lock still serializes writers by design; no parallel database
writer or aggressive network implementation was added.

Current production collector deliberately rejects panels beyond 11 instruments and
FullIdx mode. A future 50-equity + benchmark live experiment is **not runnable yet**:
it needs separate authorization, a bounded collector change preserving FullIdx
production guard, independently verified identities/completed session, validated
rate and fresh local quota counters. A no-retry one-date panel costs at most 51
units, hence up to 31 Extra units with 20 ordinary remaining. Extra-balance runtime
behavior remains unverified; no successful offline math establishes entitlement.
Soak remains **0/10**. No backfill or paid plan follows from this benchmark.

Validation: **21 .NET tests, 32 Python tests, 14 existing offline restore/replay
check groups, final 100/900 synthetic benchmarks PASS**. Benchmark build: zero
warnings/errors. No new dependencies, migrations, indexes, strategies or networking.

Ignored measured evidence:
- `data/collector-output/scale/idx_scale_0743cfa176e544fdb7aab277b946dd25/result.json` (pre-change 100 baseline)
- `data/collector-output/scale/idx_scale_4d151104a0444d538c5aaa45df5faf7d/result.json` (first post-change 100)
- `data/collector-output/scale/idx_scale_7f6e3cc319bf48a58e8bff1014154a42/result.json` (final 100)
- `data/collector-output/scale/idx_scale_726d75e4de0d40fb97c2072efab6d690/result.json` (900)
- `data/collector-output/pilot/idx_pilot_check_c881a203bcd34d71bde9f06277ddd452/result.json` (existing restore checks)

Owned benchmark databases were dropped after successful validation. No benchmark
fixture or offline replay advances real soak count. Next model: **MEDIUM**.
