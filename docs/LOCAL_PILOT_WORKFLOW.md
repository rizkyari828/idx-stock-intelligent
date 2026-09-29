# Local pilot workflow — 2026-09-29

## Subsequent bounded operational update

The earlier offline task below used zero provider calls. A subsequent authorized
bootstrap resume used **five units** to finish VKTR, ENRG, PTRO, DSSA and the broad
JKSE response, with six archives reused. Request coverage is COMPLETE for all
eleven; canonical history remains PARTIAL (55 bars on five independent sessions),
and all features remain WARMUP. See [bootstrap results](ZERO_COST_PILOT.md#september-29-bootstrap-completion).

Real prospective soak is still **0/10**. September 28 has independent closing
evidence but is not strictly after the soak baseline; September 29 was not yet
completed and is refused as today's Jakarta date. No operational prospective
dry-run or real soak collection occurred. No extra-call probes were eligible.
The first possible session is September 29, subject to actual completion/proof,
with collection on a later Jakarta date and sufficient normal quota.

The offline report now derives bootstrap request completeness from a complete
fixed-panel batch, window/provenance/parser checks and verified raw hashes.
Legacy raw archives are reparsed with the current strict parser while their
immutable manifests retain the original archival version. The report no longer
lists a hardcoded pending bootstrap gate. Remaining reported gates are
listing boundaries and the ten-run soak; broader Phase 0 gates remain uncleared.
The last ledger attempt is an actual DEGRADED BOOTSTRAP attempt, not a soak session.
The restore comparison now scopes the original 21-bar result to its original
available instruments, excluding newly bootstrapped instruments from that old
reference. **26 Python, 16 .NET and 12 offline restore groups pass**; no live
provider requests were used by tests. FullIdx remains NOT ENABLED.

The remaining sections preserve the earlier offline workflow validation.

This task made **zero EODHD HTTP requests and zero billable requests**. The live
panel remains ten equities + JKSE.INDX. No strategy, purchase, backfill, scheduler
or universe expansion was enabled. Real prospective observations remain **0/10**.

## Standard test discovery

`global.json` already selected Microsoft.Testing.Platform, but the xUnit executable
used its native runner entry point. Add `UseMicrosoftTestingPlatformRunner=true`
to the test project, as prescribed by [xUnit's MTP documentation](https://xunit.net/docs/getting-started/v3/microsoft-testing-platform).
Package versions remain unchanged.

Validation: **25 Python tests, 16 .NET tests and 12 offline restore check groups**
passed; solution build had zero warnings/errors. Dry-run and the local soak report
were exercised directly. Controlled failures were excluded from the soak.

```sh
dotnet test
# Existing restored dependencies, without package restore:
dotnet test --no-restore
# RTK's filtered invocation returned zero tests; preserve the normal CLI:
rtk proxy dotnet test --no-restore
PYTHONPATH=collectors/python/src python3.13 -m unittest discover -s collectors/python/tests -v
```

All 16 .NET tests execute through the standard solution command. A temporary,
uncommitted controlled failing test was executed: 17 discovered, one failed,
exit 2. The probe was removed and the ordinary suite rerun. No dependencies were
added or upgraded.

## Offline planning and live operation

```sh
# No account/market requests, token loading, archival, ingestion or ledger write.
bash scripts/pilot-eod.sh --from 2026-09-28 --to 2026-09-28 --dry-run
bash scripts/pilot-eod.sh --soak-report
# After independent completed-session proof is recorded, on the following day:
bash scripts/pilot-eod.sh --from YYYY-MM-DD --to YYYY-MM-DD
# Explicit re-fetch, still subject to the normal 16/20 account ceiling:
bash scripts/pilot-eod.sh --from YYYY-MM-DD --to YYYY-MM-DD --refresh
```

Explicit start/end dates are mandatory for collection/planning. Dates must be
ordered, prior to today's Jakarta date and no older than 330 days. Today's date is
conservatively refused even after market close; the example September 29 dry-run
was refused with exit 1 on September 29. This preserves the completed-date rule.
CLI/configuration failures return 1; operations return 0 success, 2 degraded,
1 failed. Help/report/planning return 0 when valid.

Dry-run queries only local canonical state through the .NET owner, Docker IPC and
psql's local Unix socket. It does not restore/build packages or access a provider.
Unavailable local state is explicitly UNKNOWN; it never claims zero stored bars.
The installed worker must be built for KNOWN state. The actual September 28 plan
found 33 canonical bars over five known dates, no session proof for September 28,
and all 11 symbols fetch-required (maximum 11 EOD units). Quota was not queried.
These counts are runtime observations, not live-run or soak evidence.

Live orchestration preflights canonical database availability before any provider
request. A failed preflight leaves a FAILED collection/operation report and reserves
zero units. Collection and worker errors are not silently accepted: worker exit
codes must agree with reported status. Console output gives status, quota
reservations, canonical additions, soak progress and the durable report path.
Provider tokens are removed from worker environments; exceptions containing
credential URLs and raw account responses are not printed.

## Reuse and restart rules

Automatic reuse applies only to a single completed session with a previous clean
SUCCEEDED operation, no warnings, unchanged independent session register, same
database and matching latest canonical content hash/retrieval state. Requested
window coverage, provider identity, current parser version, raw byte length/hash,
retrieval after session proof, and one actual row must all verify. A later
degraded/failed attempt invalidates older clean reuse. Any uncertainty requires
fetch; it does not create a candle.

Current pilot bars carry unresolved semantics warnings. Those degraded results
do **not** satisfy automatic clean reuse. Consequently current real evidence
remains fetch-required; no correctness gate was weakened to reduce quota.
The clean-state fixture proves that when all 11 entries qualify, rerunning makes
zero EOD and zero account requests. `--refresh` bypasses automatic reuse and cannot
be combined with explicit resume/seeds. Bootstrap resume and offline archival
replay retain their existing meanings and never count toward the soak.

No automatic retry is introduced. On failure, inspect the durable operation,
repair local state, and rerun deliberately. Identical canonical values add no
revision; stale archives cannot undo a newer correction. Reserved quota includes
uncertain transport attempts. Partial valid evidence may still be ingested with
explicit DEGRADED status.

## Local prospective ledger

Each new ignored `*.operation.json` includes a ledger record: run id, session date,
start/completion times, mode, requested symbols, validated successful/failed
fetches, reserved quota units, raw hashes, canonical additions, revision additions,
rejections, feature results, warnings, final SUCCESS/DEGRADED/FAILED status and soak
eligibility. Unknown database results remain null/UNKNOWN, never fabricated zero.
The report assigns chronological attempt numbers and shows the last attempt.
Operation files are the ledger; no additional mutable database or service exists.

Legacy records are not fabricated into new ledger rows. Their existing worker
summaries still support the original soak eligibility calculation. Failed,
partial/degraded, bootstrap, fixture, temporary-database and offline runs never
count. The existing policy requires a successful live DAILY run after September
28 with all eleven fresh requests, independently confirmed session outcomes and
retrieval following completed-session proof, in the normal database. Warm-up alone
does not disqualify an otherwise complete run. One market date counts once.
New ledger final status SUCCESS maps existing internal SUCCEEDED; statuses/gates
have not been relaxed. Current report: **0/10**, last ledger run **none**.

## Restore and reproduction

```sh
dotnet build IdxStockIntelligence.slnx --no-restore
# Existing local Compose service and ignored bootstrap evidence must be available:
python3.13 scripts/check_pilot_restore.py
```

The selected prior result uses the locally retained bootstrap batch/raw evidence
and frozen session snapshot from commit 3024e03. The test uses migrations/current
deterministic code and creates/removes two uniquely named owned databases; it does
not call a provider or mutate the normal database.

Twelve check groups pass: exact selected 21 bars and canonical content/provenance,
prior WARMUP/null feature results, idempotency, holiday/unconfirmed exclusion,
explicit source failure, missing instrument, duplicate/malformed evidence,
correction/stale replay, knowledge chronology, transaction rollback/retry, and
a second clean reconstruction of the full correction sequence. Raw provenance
links are rehashed, byte lengths checked, revisions numbered contiguously and
knowledge/retrieval/session-proof times ordered.

Original first_seen/known_at are not reproducible by re-ingestion: new ingestion
has new knowledge time. Exact temporal restoration requires a database backup
plus raw and independent-evidence snapshots. Numeric live features remain
unreproducible while real history is in warm-up; committed synthetic golden tests
cover the numeric feature invariants separately.

## Future universe input and remaining gates

The normalized instrument configuration already enters at the collector boundary,
not the domain. Current configuration is checked against the committed fixed pilot
identities/metadata and ceiling; optional universe_mode must be Pilot.
`FullIdx` is documented future work and **NOT ENABLED**. A future approved
security-master adapter can supply the same normalized instrument contract; live
count/identity guards, quota/runtime policy and rights gates must first be reviewed.
No hundreds of entries or provider-specific domain fields were added.

Remaining gates: five bootstrap requests, four unresolved listing boundaries,
10 actual prospective sessions, unresolved segment/adjustment/corporate-action
and chronology/calendar evidence, production rights and sustainable full-universe
operation. Historical 2022-present remains BLOCKED_BY_ENTITLEMENT.
Operational readiness is conditional on independent proof for a completed date,
healthy local database and sufficient normal quota; this offline task does not
establish those live prerequisites or clear Phase 0.
