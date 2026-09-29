# Phase 0 prospective pilot exit checklist

Reviewed 2026-09-28. Scope: the existing ten equities + JKSE.INDX, private Free-plan
collection. **Phase 0 exit: NO.** Historical 2022-present remains
**BLOCKED_BY_ENTITLEMENT**; no purchase or historical backfill is proposed.

Offline workflow revalidated September 29: standard .NET discovery, local ledger,
dry-run and stronger restore checks are documented in [LOCAL_PILOT_WORKFLOW.md](LOCAL_PILOT_WORKFLOW.md).
Real observations remain **0/10** and FullIdx is **NOT ENABLED**.

- [x] All ten pilot equities bootstrap response coverage complete — five-unit September 29 resume; full-window raw evidence verified. Canonical calendar/history remains PARTIAL.
- [x] JKSE benchmark bootstrap response coverage complete — broad August 17–September 25 response verified September 29; five independently confirmed canonical sessions.
- [x] Raw evidence immutable — content addressing, exact bytes/hash and integrity tests.
- [x] Session validation safe — independent proofs, exceptional closure support, unknown remains unknown.
- [x] Canonical ingestion idempotent — repeated response/reprocessing adds no duplicate revision.
- [x] Revisions append-only — database triggers, correction/reversion tests and stale-archive guard.
- [x] Listing boundaries handled — eight verified, PTRO/LPIN PARTIAL with effective UNKNOWN; versioned, append-only as-of reference import. See [boundary hardening](INSTRUMENT_BOUNDARIES.md).
- [x] Feature warm-up correct — null/WARMUP, chronological confirmed-session windows, aligned benchmark.
- [x] Failure behavior explicit — eight requested local scenarios, atomic rollback, durable FAILED/DEGRADED reports.
- [x] Selected prior bar/provenance and warm-up result reproduced in a fresh database — temporal/full numeric restore limitations remain below.
- [ ] Ten real completed prospective EOD runs — **0/10**; no bootstrap, offline replay or fixture counts.
- [x] Provider quota sustainable for declared panel — 11 normal units, account ceiling 16/20, no bonus/bulk/purchase.
- [x] No secret/raw artifact committed — `.env`, artifacts, fixture payloads and runtime reports remain ignored/untracked.

Each checkbox is limited to the evidence above; it does not clear full-universe,
corporate-action, source upstream, market-segment, numeric historical-feature or
complete point-in-time identity/calendar gates. Fresh re-ingestion preserves bar
values/provenance but records new canonical knowledge time; a full chronology
restore requires a PostgreSQL backup and retained independent evidence snapshots.

The ten-run gate counts unique completed market dates **after 2026-09-28**, only for
successful live DAILY runs in the normal database. All eleven requests must succeed,
all outcomes must be admitted bars or independently proven NO_TRADE/SUSPENDED states,
and retrieval must follow recorded completed-session proof. Actual request evidence
must exist; warm-up alone does not fail a otherwise valid run. Repeated runs of one
date count once. Fixtures/restore databases and partial/failed runs do not count.

**Ready to begin the manual ten-run soak: YES**, after quota reset and independent
proof for the chosen completed date. **Ready to exit Phase 0: NO.** Next bounded work:
observe ten actual eligible future sessions; bootstrap responses are now complete;
resolve remaining semantics before any strategy implementation. Use MEDIUM reasoning.

See [hardening evidence and commands](PILOT_HARDENING.md).
