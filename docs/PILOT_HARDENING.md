# Prospective pilot hardening — 2026-09-28

## Current operational update — September 29

The subsequent [same-day EOD policy](SAME_DAY_EOD.md) replaces the H+1-only guard
with shared configured cutoff plus independent completed/open evidence. No live
run or provider request occurred; soak remains **0/10**. Current validation is
28 Python tests, 21 .NET tests and 14 offline restore groups. Earlier prior-date
observations below describe the policy before this change.

Subsequent [zero-provider boundary hardening](INSTRUMENT_BOUNDARIES.md) adds a generic
reference importer and as-of metadata history using the existing evidence table.
RAJA/VKTR are VERIFIED; PTRO/LPIN remain PARTIAL with effective UNKNOWN boundaries.
Tests now pass **26 Python, 20 .NET and 13 offline restore groups**. No price bars or
session proofs were added; soak is **0/10** and FullIdx remains disabled. The listing
table below preserves the earlier review, before these new knowledge revisions.

The [bootstrap completion](ZERO_COST_PILOT.md#september-29-bootstrap-completion)
used five ordinary units and completed the four equities and broad index response.
All eleven request windows now cover August 17–September 25; canonical history
remains PARTIAL: 55 admitted bars on five proved sessions, 22 additions today,
271 excluded evidence rows and all features WARMUP. Operation DEGRADED is explicit.
There is no eligible prospective session yet under the unchanged after-September-28
boundary and prior-Jakarta-date rule. **Soak remains 0/10**.

The extra-call test was ineligible and made no probes. No purchase, strategy,
universe expansion or 2022-present request occurred. Standard tests pass:
**26 Python, 16 .NET, 12 offline restore groups**. The old-result restore reference
now selects only instruments present in that original result, so later bootstrap
growth cannot invalidate its comparison. Both owned temporary databases were
removed. The offline soak report derives bootstrap request completeness from
validated full-window artifacts rather than hardcoding a pending bootstrap gate.

The sections below preserve the earlier September 28 observations.

The [September 29 local workflow update](LOCAL_PILOT_WORKFLOW.md) supplies standard
.NET discovery, dry-run, conservative canonical reuse, an explicit local ledger and
stronger offline reconstruction. Real soak remains **0/10**; no provider call was
made by that update. The observations below describe the earlier hardening run.

Scope remains BBCA, BBRI, ANTM, RAJA, VKTR, ENRG, PTRO, DSSA, GOTO, LPIN and
JKSE.INDX. No strategy, universe expansion, subscription, scheduler, model call or
2022-present retrieval was added. **Historical: BLOCKED_BY_ENTITLEMENT.**

## Bootstrap and request accounting

A zero-unit account check at 08:58 UTC reported Free, 20 daily units, 16 used on
2026-09-28 and **zero available below the 16-unit pilot ceiling**. The GMT reset had
not occurred; no pending EOD request was made. Five requests remain: VKTR, ENRG,
PTRO, DSSA and the broader JKSE.INDX window. Six equity responses cover the chosen
August 17–September 25 initial window; IHSG covers only September 23–25. Request
coverage is checked against manifest bounds rather than inventing sessions from row
counts. Resume revalidates archived bytes and regenerates normalization.

**Additional EOD units: 0.** One live account HTTP check used zero units; failure,
restore and operating-command checks made no provider calls. Normal daily collection
remains 11 EOD units, 24 HTTP requests including zero-unit account checks, with a hard
account ceiling of 16/20. No automatic retry, bonus quota or bulk endpoint is used.
After midnight GMT/07:00 Jakarta, the five-request resume and any other account usage
must fit under that ceiling. A failed account check now produces a completed FAILED
collection record and does not proceed to ingestion.

## Listing-boundary register

The machine-readable register is in `pilot/universe.json`, with symbol, listed_on,
reference, status, confidence and actual known_at. Dates below come from manually
reviewed primary identity evidence, not EODHD price availability or an ISIN effective
date. Original unknown instrument records are retained; migration 0003 appends the
new listing-evidence observations without rewriting canonical bar history.

| Security | Listing date | Evidence/status |
|---|---|---|
| BBCA | 2000-05-31 | [KSEI ordinary-share field](https://web.ksei.co.id/services/registered-securities/shares/lc/BBCA), VERIFIED/PRIMARY_SOURCE |
| BBRI | 2003-11-10 | [IDX company profile](https://idx.co.id/en/listed-companies/company-profiles/BBRI), VERIFIED/PRIMARY_SOURCE |
| ANTM | 1997-11-27 | [KSEI ordinary-share field](https://web.ksei.co.id/services/registered-securities/shares/lc/ANTM?setLocale=id-ID), VERIFIED/PRIMARY_SOURCE |
| RAJA | UNKNOWN | [KSEI field is absent](https://web.ksei.co.id/services/registered-securities/shares/lc/RAJA?setLocale=en-US), UNVERIFIED |
| VKTR | UNKNOWN | [Primary register](https://web.ksei.co.id/services/registered-securities/shares/lc/VKTR) unavailable in this review; no secondary date promoted |
| ENRG | 2004-06-07 | [KSEI ordinary-share field](https://web.ksei.co.id/services/registered-securities/shares/lc/ENRG?setLocale=id-ID), VERIFIED/PRIMARY_SOURCE |
| PTRO | UNKNOWN | [KSEI field is absent](https://web.ksei.co.id/services/registered-securities/shares/lc/PTRO), UNVERIFIED |
| DSSA | 2009-12-10 | [Issuer annual report](https://dssa.co.id/documents/Annual%20Report%202021.pdf), VERIFIED/PRIMARY_SOURCE |
| GOTO | 2022-04-11 | [Issuer listing announcement](https://www.gotocompany.com/news/press/goto-tercatat-di-papan-utama-bei), VERIFIED/PRIMARY_SOURCE |
| LPIN | UNKNOWN | [KSEI field is absent](https://web.ksei.co.id/services/registered-securities/shares/lc/LPIN), UNVERIFIED |

Unknown boundary means a missing older response is UNKNOWN, not an asserted provider
gap. Verified pre-listing absence is PRE_LISTING and never produces a bar. Neither
case blocks collecting prospective evidence. JKSE is an index, so its security
listing boundary is NOT_APPLICABLE.

## Exchange and instrument evidence

Exchange outcomes are separately recorded as EXCHANGE_OPEN, EXCHANGE_HOLIDAY,
EXCEPTIONAL_CLOSURE or UNKNOWN. Instrument outcomes are TRADED (provider-reported
accepted bar, still DEGRADED pilot quality), independently evidenced NO_TRADE or
SUSPENDED, MISSING_DATA where the listing boundary is known, or UNKNOWN.
Zero/flat OHLCV cannot establish NO_TRADE, SUSPENDED or an exchange closure.

`pilot/instrument-sessions.json` starts empty: no live instrument suspension/no-trade
facts have been invented. Explicit instrument proof requires matching identity/date,
independent reference and knowledge time. Future proof is invisible; known exchange
closure takes precedence. The register accepts only sourced NO_TRADE/Suspension
facts, not a bar-shape inference. Exceptional-closure behavior is tested synthetically;
no such closure was asserted for a real date here.

The session register now holds five independently confirmed completed sessions and
two holidays. Newly reviewed [August 24 closing report](https://www.bloombergtechnoz.com/detail-news/119362/ihsg-ditutup-di-6-501-saham-byan-jadi-pemberat)
and [August 26 closing report](https://economy.okezone.com/amp/2026/08/26/278/3238357/ihsg-hari-ini-ditutup-melemah-ke-level-6-405)
corroborate actual open sessions around the August 25 holiday. Existing September
23–25 close evidence and August 17/25 closure references remain unchanged.
All new proof has the actual review time, not a historical backdated known_at.

Offline revalidation admitted **12 additional bars**, six equities × two dates.
The normal database now holds **33 bars/revisions**, six equities × five sessions
plus three IHSG sessions. Neither holiday has a canonical bar. Unknown other dates
are not promoted. The recent uninterrupted accepted sequence is still only three
sessions; feature values remain null/WARMUP. Calendar completeness is not claimed.

## Durable run and revision behavior

Each worker invocation writes a new operation-id summary, preserving prior reports.
The combined command records collection start/end, run/operation ids, requested
instruments, HTTP count/bytes, reserved quota, observed account counter delta where
known, per-symbol fetch outcomes/manifests, canonical additions, corrections,
rejected evidence, unavailable observations, features, warnings and safe account
fields. A counter delta includes any other use of that key; reservations remain the
local conservative request budget. No token, account body or exception value is logged.

Exit codes: **0 SUCCEEDED; 2 DEGRADED; 1 FAILED**. Valid partial evidence may be
ingested under an explicit DEGRADED outcome. A bad/missing batch entry or malformed
artifact fails before persistence. Database failures roll back the transaction and
leave a durable FAILED report; unknown final commit state is not reported as zero.
Summary-writing/finalization errors therefore require replay/reconciliation, not
blind acceptance. A local lock serializes workers; PostgreSQL locks protect appends.

`pilot_revision_evidence` exposes **first_seen_at** as the first canonical known_at
for each instrument/date, alongside each immutable retrieved_at/artifact/revision.
The first raw observation remains in archived manifests even before canonical
admission. Content changes append; identical content does not. A newly retrieved
A→B→A reversion appends. **Replaying an older fetched A after newer B now preserves B**:
both the shared application store and PostgreSQL compare retrieval chronology and
report/ignore stale evidence. Raw evidence is retained. This fixes a demonstrated
cached-replay defect without redesigning the revision model.

## Local controlled failures and reproduction

Run after building, with the local Compose PostgreSQL service available:

```sh
set -a
source .env
set +a
python3.13 scripts/check_pilot_restore.py
```

The check creates and removes only its own uniquely named database. Fixture payloads
and reports stay ignored. It uses the preserved bootstrap and frozen independent
session register from commit `3024e03`, migrations and current deterministic code.
It makes **zero provider requests** and cannot count toward the soak.

Eleven check groups passed:

1. Fresh database reproduces the selected 21 prior bars and hashes/provenance links.
2. Prior three-session warm-up feature results match exactly.
3. Identical replay adds no revision.
4. Unknown sessions and both holiday padding dates create no bars.
5. A simulated provider failure is explicit and creates no zero bar.
6. A missing instrument entry is FAILED with no transaction mutation.
7. A duplicate provider date is FAILED with no transaction mutation.
8. Malformed payload is FAILED with no transaction mutation.
9. Changed historical row appends once; replay is idempotent; stale old evidence cannot revert it.
10. First-seen/retrieval/revision chronology remains ordered.
11. A forced failure after an earlier insert rolls back all inserts; repaired rerun succeeds and replay adds nothing.

Python mocks separately exercise transport/account failure, malformed response,
quota enforcement and secret-bearing response suppression. .NET golden checks cover
EMA20/50 seeding, Wilder ATR14, prior-current exclusion, volume-basis changes,
benchmark alignment, missing/unknown continuity breaks and warm-up/null behavior.
**18 Python tests, 16 .NET tests, full build with zero warnings/errors.**

Reproduction limitations: real bars have insufficient confirmed history for numeric
feature reproduction, so only actual WARMUP/null results were reproduced. Synthetic
golden values are tested separately. Fresh re-ingestion has new canonical knowledge
time; it does not reconstruct the old first_seen/known_at timestamps. A complete
temporal restore requires a PostgreSQL backup plus retained independent evidence
snapshots. Every new worker summary now preserves the session/instrument evidence
used; it does not depend on a live provider response to explain its inputs.

## One operating command and ten-run preparation

From the repository root, invoke the script (or use its absolute path from another
directory); it loads only the ignored,
untracked local `.env`, checks token presence without displaying it, selects installed
Python 3.12+ and performs fetch → archive → validate → ingest → features → summary.
No dependencies are installed and the worker does not receive the provider token.
Start the existing PostgreSQL Compose service first if necessary.

```sh
# After quota reset: five missing bootstrap requests, if account headroom permits.
bash scripts/pilot-eod.sh --from 2026-08-17 --to 2026-09-25 \
  --resume data/collector-output/pilot/1f7cc972-68ee-41cd-9bce-eded7efda103.json

# For a future completed date, first record independent completed-session proof.
# Same-day requires the configured safe cutoff and explicit independent completion proof.
bash scripts/pilot-eod.sh --from YYYY-MM-DD --to YYYY-MM-DD

# Zero-network replay/reprocessing; never counts as a prospective run.
bash scripts/pilot-eod.sh --from 2026-08-17 --to 2026-09-25 \
  --resume data/collector-output/pilot/1f7cc972-68ee-41cd-9bce-eded7efda103.json --offline
```

The offline one-command check returned **DEGRADED/exit 2**, accurately identifying
unfinished bootstrap; it used zero requests/units. Repeating admitted evidence is
idempotent. The runtime operation report includes unique-session soak progress.
Bootstrap, offline replay, partial/failed runs and isolated test databases are never
eligible. Qualifying live runs require complete fetch evidence, independently
confirmed future sessions, acceptable per-instrument outcomes and retrieval after
the recorded completed-session proof. The same market date counts once.

**Real prospective soak: 0/10. Ready to begin: YES after reset and session proof.
Ready to exit Phase 0: NO.** See [exit checklist](PHASE0_EXIT_CHECKLIST.md).
Next work is the bounded bootstrap resume and ten actual future EOD runs, using
MEDIUM reasoning. Strategies, 2022-present backfill and subscription purchase remain
outside scope.
