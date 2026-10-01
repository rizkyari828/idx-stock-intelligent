# Daily H+1 pilot operator workflow

Normal collection runs the following morning, for a completed **prior Asia/Jakarta
date**. It does not automatically collect today's session, discover proof, reset
quota, retry, refresh, or expand the fixed 10-equity + JKSE.INDX panel.

## Normal operation

1. Start the existing Docker Desktop/runtime and PostgreSQL container. Preserve
   its existing named volume; never create an empty replacement to pass preflight.
2. Review independent full-day closing evidence and record it separately in
   `pilot/sessions.json`. Use the existing `ObservedTrading`, `AnnouncedClosed`, or
   `ExceptionalClosure` statuses. Open-session evidence requires `completed_at`
   and `known_at`, with `completed_at <= known_at <= collection_started_at`.
   Record actual observation time; never backdate knowledge or use EODHD as proof.
3. Plan without loading the provider token or calling any provider endpoint:

   ```sh
   bash scripts/pilot-daily.sh --dry-run
   ```

4. Review the candidate, pending sessions, independent proof, database identity,
   panel, maximum 11 units, and authoritative soak count. Only `ELIGIBLE` permits
   collection. Then execute once:

   ```sh
   bash scripts/pilot-daily.sh
   ```

5. Review the existing operation report, fetch/canonical/feature results, account
   reconciliation, and soak. A completed run remains `SUCCESS`, `DEGRADED`, or
   `FAILED` under the current pipeline policy. There is no whole-run retry.

## Selection and catch-up

The latest successful single-date DAILY operation must agree with all 11 current
canonical records in the expected `idx_stock_intelligence` database. A partial,
failed, offline, or missing report cannot establish this baseline. Canonical rows
ahead of that successful baseline block automatic selection for operator review.

Walk calendar dates forward from that baseline. Skip only independently proven
closures; **unproven dates, including weekends, are not inferred closed or open**.
The first prior Jakarta date with independent completed/open proof is selected.
Missing/conflicting/incomplete proof stops at that date, even when a later date
has valid proof. The plan also lists proven pending sessions for catch-up review.
Each invocation processes at most one date, oldest first. Today's proof does not
make today's date eligible in this H+1 workflow.

An already successful canonical session is not collected again, including a
successful pilot that retains the existing semantics warning. Once all prior
dates are accounted for, the decision is `NO_NEW_SESSION`; no account call occurs.
There is no refresh switch in daily mode. Explicit recovery/refresh belongs to a
separately reviewed dated pilot procedure.

## Preflights, quota, and exit codes

Planning reads canonical state through the existing .NET `pilot-state` command,
Docker IPC, and PostgreSQL. It requires `KNOWN`, the expected database, and the
11 configured instruments. It performs no build, package restore, provider call,
archival, ingestion, or ledger write. Selection for real runs happens under the
existing collector lock, so another collector cannot race date resolution.

The existing live pilot shell loads ignored/untracked `.env` before local planning;
provider requests require all local gates to pass. An eligible real run uses the
normal production pipeline. Its safe account checks require the Free plan, current **UTC quota date**, known
daily used/limit and Extra balance, and enough ordinary allowance for the complete
panel under the existing **16/20** ceiling. Extra balance is verified but not spent
by daily mode. Each subsequent market request rechecks current counters.

Stale lazy-reset counters stop collection with zero market calls. The daily command
does **not** make a surprise rollover request. Use a separately authorized bounded
rollover procedure if required, then review the plan again. Jakarta session dates
and UTC account dates intentionally use their respective clocks.

| Result | Exit code |
|---|---:|
| Eligible dry-run / no new session / successful collection | 0 |
| Blocked local state, invalid configuration, account failure, failed collection | 1 |
| Degraded collection | 2 |
| Waiting for independent session proof | 3 |

Soak uses the existing `soak_progress`/operation ledger only. The daily wrapper
does not maintain another counter. Inspect it separately with:

```sh
bash scripts/pilot-eod.sh --soak-report
```

## Optional advanced same-day operation

The existing explicit dated command remains available. Same-day collection still
requires independent completion proof and `SAFE_EOD_CUTOFF = 19:00 Asia/Jakarta`:

```sh
bash scripts/pilot-eod.sh --from YYYY-MM-DD --to YYYY-MM-DD --dry-run
# After reviewing all same-day gates, use the same command without --dry-run.
```

FullIdx remains disabled. No strategy or production-rule promotion is implied.
