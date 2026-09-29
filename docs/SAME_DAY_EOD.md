# Safe same-day EOD collection — September 29, 2026

The previous collector and worker required `requested_date < today_in_Jakarta`.
They now permit prior completed sessions **or safe same-day EOD**. This change
used **zero EODHD requests/units**, added no real session proof and performed no
live collection. Prospective soak remains **0/10**; FullIdx remains disabled.

## Configuration and eligibility

`pilot/collection-policy.json` contains `SAFE_EOD_CUTOFF`, initially **19:00**.
Both Python and .NET read this file. Change its strict `HH:mm` value without
changing application code. It is a conservative Phase 0 operational cutoff,
not an exchange rule or proof that provider data is final.

All clocks use explicit **Asia/Jakarta** conversion from an aware UTC instant.
Policy functions accept a clock value directly for deterministic tests. Future
dates and dates outside the existing 330-day window remain rejected.

Live collection requires a unique, independent HTTPS session proof known before
collection starts. Prior dates require reviewed `ObservedTrading` closing evidence.
Same-day dates additionally require the cutoff to be reached and `completed_at`
in the versioned session registry. That timestamp must establish completed-session
evidence for the requested Jakarta date and satisfy:

`completed_at <= known_at <= collection_started_at`.

The reference must actually support completion/open status; a timestamp or AI
answer alone does not. Do not invent a closing time. A source publication timestamp
that establishes the session has already ended may serve as completed-at evidence.
Record the actual review time as known_at. Evidence from EODHD cannot authorize
its own fetch. Duplicate/conflicting proof, unknown status, holiday/exceptional
closure and weekends cannot authorize a live market fetch. A live multi-date
request requires eligible open-session proof for every requested date; narrow the
window rather than fetching unconfirmed/closed dates. Offline archival replay
remains separate and can inspect excluded dates without making provider calls.

The session registry schema remains date/status/reference/known_at, with optional
completed_at. Older reviewed prior-date closing references remain supported.
Use timezone-bearing ISO timestamps; completed_at is mandatory for same-day proof.
Python and the worker both honor `IDX_PILOT_SESSIONS` when set.

## Dry-run and tonight's commands

```sh
bash scripts/pilot-eod.sh --from 2026-09-29 --to 2026-09-29 --dry-run
# Run only after the plan is ELIGIBLE and normal quota/database prerequisites hold:
bash scripts/pilot-eod.sh --from 2026-09-29 --to 2026-09-29
```

Dry-run reports Jakarta time, configured cutoff, known proof state, eligibility and
reason per date. Before cutoff: `SAFE_EOD_CUTOFF_NOT_REACHED`. After cutoff without
proof: `SESSION_PROOF_REQUIRED`; open proof without explicit completion:
`COMPLETED_SESSION_EVIDENCE_REQUIRED`. Satisfied conditions produce
`ELIGIBLE_SAME_DAY_COMPLETED_SESSION`. A blocked plan lists no provider requests
and maximum EOD units zero. Dry-run loads no token and makes zero account/provider
calls, writes no raw artifact, performs no ingestion and adds no ledger entry.

Actual local September 29 dry-run at **10:41 WIB** was NOT_ELIGIBLE, before cutoff,
with UNKNOWN_SESSION and no September 29 proof. Canonical state remained 55 bars.
Tonight's workflow is now supported conditionally: wait until 19:00 WIB or the
configured cutoff, independently review a legitimate completed/open closing source,
record its proof/timestamps, then dry-run. Confirm healthy PostgreSQL and sufficient
ordinary quota for the fixed eleven requests under the unchanged 16/20 ceiling.
No real September 29 fetch was attempted by this task.

## Admission and replay guarantees

Eligibility authorizes only an attempted fetch. Exact raw archival, strict parsing,
normalization, independent session validation and .NET canonical/revision rules
still apply. Missing/zero-volume/inconsistent rows cannot manufacture a session or
bar. Malformed/schema failures retain the existing FAILED/DEGRADED behavior; quota
reservations, no automatic retries and credential suppression are unchanged.

The worker checks live eligibility at the recorded collection start, rather than
only its later ingestion clock. For a same-day artifact it also checks the actual
retrieval timestamp against cutoff/completed proof. A too-early artifact cannot
become valid by waiting until evening or replaying it on the next day; future-dated
rows relative to retrieval also fail. Current configuration is revalidated on
ingestion; the applied policy is preserved in the operation summary. Same-day
eligibility does not change soak admission, bar revision chronology or as-of evidence.

## Validation

**28 Python tests, 21 .NET tests and 14 offline restore groups pass**, with zero
provider calls. Synthetic clocks cover prior-date proof, before/exactly-at/after
cutoff, unknown/closed sessions, missing completion, future/late-known/provider
proof, UTC/Jakarta date boundaries and changed cutoff configuration. Dry-run and
blocked-live tests prohibit provider I/O. Disposable-database replay rejects an
early same-day artifact and accepts the eligible replay path without changing
canonical rows. Build has zero warnings/errors; real soak remains **0/10**.
