# Isolated Extra-call entitlement probe

Implementation and tests used **zero EODHD requests/units**. No live probe was run.
This command is separate from the pilot collector. It performs no archival,
normalization, canonical ingestion, revisions, features or soak ledger writes.
Market responses are transient and discarded after shape/OHLC/date validation.
Output contains only allowlisted counters, sanitized request metadata and one verdict.
Do not redirect live output into tracked files. Soak stays **0/10**; FullIdx disabled.

## Offline rehearsal

```bash
bash scripts/eodhd-entitlement-probe.sh --daily-used 16 --daily-limit 20 \
  --extra-balance 500 --extra-probes 3 --dry-run
```

Reports ordinary remaining **4**, extra probes **3**, billable requests **7**, expected
extra after **497**, provider requests **0**. Its runtime verdict is TEST NOT ELIGIBLE
because dry-run does not execute an empirical test, even when the calculated plan fits.
Dry-run does not load `.env` or require PostgreSQL.

## Future live command for tonight — not executed by implementation

After the real pilot completes, run only under the separately authorized probe budget:

```bash
bash scripts/eodhd-entitlement-probe.sh --date 2026-09-25 --extra-probes 3
```

The wrapper requires ignored/untracked `.env`, loads it silently and checks token
presence only. Do not run concurrently with another collector/account user. The
command uses only verified BBCA.JK / ANTM.JK / GOTO.JK, round-robin one-date EOD
requests. An omitted date selects the latest prior ObservedTrading date already
known in the local session registry. Dates must be prior Jakarta dates within 330
days; the command cannot bypass tonight's pilot cutoff/completion gate to ingest data.

The zero-unit account request must report Free, daily limit 20, current UTC counter
date, integer usage and `extraLimit`. Unknown/stale counters fail closed; no reset
date is guessed. Live counter overrides are rejected. The plan requires sufficient
extra balance and a maximum **10 billable EOD requests**, including failed attempts.
Zero-unit before/after account checks are additional HTTP requests.

An operator-approved cap increase is explicit and limited to 12:

```bash
bash scripts/eodhd-entitlement-probe.sh --extra-probes 3 --hard-cap 12 --approve-cap-12
```

The two flags declare operator approval; they do not authorize a paid plan or purchase.
There are no retries, redirects, Bulk, Fundamentals, Technical, Calendar, News or
discovery endpoints. Requests have a 30-second timeout and 2 MiB response ceiling.
HTTP 200 alone is insufficient; an empty/warning/malformed/non-EOD response stops
the run and yields NOT VERIFIED. A quota-date change stops further billable requests.

## Counter reconciliation

Example: before usage 16/20, extra 500 → four ordinary + three overflow requests →
expected extra 497. **Do not force this result.** VERIFIED requires all seven valid
payloads, unchanged counter date/limit, exactly three fewer extra units and reconciled
usage. Both a capped ordinary counter of 20 and a total usage counter of 23 satisfy
the explicitly supported arithmetic conventions; other usage values are ambiguous.
If already at 20/20, only three requests are needed. Unknown preflight/cap/entitlement
returns TEST NOT ELIGIBLE; an attempted test with any failure/mismatch returns NOT VERIFIED.
Account reset/concurrent use can prevent verification; there is no automatic retry.

Exactly one runtime verdict is emitted:

- `EXTRA CALL BUFFER VERIFIED`
- `EXTRA CALL BUFFER NOT VERIFIED`
- `EXTRA CALL BUFFER TEST NOT ELIGIBLE`

Successful live verification exits 0; blocked/unverified exits 2. Successful offline
planning exits 0 with the non-empirical verdict explained above. Full account bodies,
credentials, URLs with tokens, exception details and market values are never output
or persisted. The probe supplies no deeper-history entitlement or production approval.

Offline validation: **42 Python tests and 21 .NET tests passed**; shell syntax
passed. Implementation dry-run matched 4 + 3 = 7 and expected 497. Zero provider
requests, no token loaded and no live account/payload saved during implementation.

## September 29 runtime status

The real pilot returned DEGRADED, so its completion gate blocked probe eligibility.
No probe preflight or live probe was executed. Fresh post-pilot Free counters were
16/20 on September 29, extra balance 500; arithmetic would require 4 ordinary +
3 extra = 7 requests, but does not clear the failed pilot gate. Extra balance remains
500; buffer functionality remains unproven. **EXTRA CALL BUFFER TEST NOT ELIGIBLE**.
See [actual pilot result](LOCAL_PILOT_WORKFLOW.md#september-29-prospective-execution--actual-result).
