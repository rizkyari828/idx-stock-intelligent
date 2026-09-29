# ZERO-COST PILOT MODE

Latest update: [prospective hardening](PILOT_HARDENING.md) and [Phase 0 exit checklist](PHASE0_EXIT_CHECKLIST.md).
The one-command operator, durable failure reports, stale-archive correction guard,
listing evidence and additional confirmed sessions supersede initial implementation
details below. **Bootstrap request coverage is complete as of September 29;
canonical history remains partial and the real soak count is 0/10.**

## September 29 bootstrap completion

The existing August 17–September 25 window was resumed with exactly five EOD
requests: VKTR, ENRG, PTRO, DSSA and JKSE.INDX. Each returned 30 valid normalized
rows. The six full-window equity archives were verified and reused without fetch.
All eleven instruments now have preserved full-window responses: BBCA/BBRI have
28 rows each; the other eight equities and benchmark have 30 each. COMPLETE here
means validated request/response coverage, not a fully admitted canonical series.

Five new immutable artifacts were archived with token-free manifests. Collection
SUCCEEDED, while canonical operation remained DEGRADED/exit 2. It added **22 bars
and revisions, zero corrections**, for **55 canonical bars/revisions**: eleven
instruments × the five independently confirmed dates (August 24/26, September
23/24/25). The expanded index response added two dates; the four equities added
five dates each. No other dates were promoted.

The full batch contains 326 rows: 55 admitted, 271 rejected/unconfirmed evidence
rows (18 holiday rows and 253 rows without confirmed sessions). Observation counts
are 55 AVAILABLE, 22 CLOSED and 363 SESSION_UNCONFIRMED; closed outcomes include
instruments with no provider holiday row. Zero holiday bars were stored. No
SOURCE_ERROR, malformed OHLC, duplicate provider dates or fabricated bars were
observed. All eleven feature states are WARMUP with three consecutive sessions.
Canonical completeness therefore remains PARTIAL for every instrument.

Accounting: **5 units**, matched by the effective account counter delta; 12
collector HTTP requests, 19,084 response bytes, 10.594 seconds summed HTTP elapsed.
Two separate zero-unit usage checks bracketed the work. No prospective collection
or entitlement probes were made. The buffer test was not eligible under its
current-day usage/budget requirements; no extra calls were consumed.

At execution time, September 29 was not a completed prior Jakarta date.
[September 28 has independent closing evidence](https://periskop.id/pasar-modal/20260928/ihsg-hari-ini-ditutup-melemah-6147),
but does not satisfy the soak boundary strictly after September 28. No eligible
date, operational soak dry-run or real soak run was asserted. **Real soak: 0/10**.
The first possible date is September 29, only after actual completion/proof and
with collection on a later Jakarta date; this is a future prerequisite, not an
invented observation. FullIdx and 2022-present collection remain disabled/blocked.

Implemented and exercised 2026-09-28. This is private, fixed-panel prospective
collection with conservative canonical admission, not a full IDX screener or
trading system. **2022-present remains BLOCKED_BY_ENTITLEMENT.** That historical
blocker does not block this recent-window/prospective pilot.

## Fixed universe

`pilot/universe.json` contains stable local instrument identities:

| Symbol | Pilot role |
|---|---|
| BBCA.JK | Liquid large-cap bank |
| BBRI.JK | Liquid large-cap bank |
| ANTM.JK | Metals/resource exposure |
| RAJA.JK | Energy infrastructure |
| VKTR.JK | Newer listing/electric mobility |
| ENRG.JK | Energy/resource exposure |
| PTRO.JK | Resource services |
| DSSA.JK | Diversified energy exposure |
| GOTO.JK | Newer technology listing |
| LPIN.JK | Lower-volume case in the observed recent panel |
| JKSE.INDX | Verified Jakarta Composite benchmark |

The first nine equities retain the representative panel; LPIN fills the previously
unspecified lower-liquidity slot. All ten appeared in the previously archived JK
active common-stock catalogue. Lower-volume qualification is sample-relative:
LPIN's median provider-native count was 91,950 over this window versus 157,453,850
for BBRI. This is neither turnover nor an execution/liquidity score.
Six listing dates now have primary evidence; RAJA, VKTR, PTRO and LPIN remain
explicitly null. Null is not an invented listing date: missing rows with an unknown listing
boundary cannot be classified as provider gaps. Configured known pre-listing dates
are excluded by validation. No universe discovery is performed during a run.

## Entitlement and budget

[EODHD limits](https://eodhd.com/financial-apis/api-limits) specify 20 Free units/day,
one unit per symbol EOD request, zero for the account usage endpoint, and reset at
midnight GMT (07:00 Jakarta). Reset is lazy: yesterday's dated counter can remain
visible until the first charged request. The collector accounts for that documented
date behavior; future/malformed accounting fails closed.

Normal collection: **10 equities + one index = 11 units**, 24 HTTP requests including
account checks before/after and before each EOD request. The hard account ceiling
is **16 units/day**, leaving four ordinary units untouched and five above normal
collection for explicitly initiated validation/retries. There are no automatic
retries, bulk endpoints, paid calls, upgrades, or use of extra/bonus quota.
The account must still report Free and a 20-unit daily limit. A local lock prevents
concurrent collectors on this machine; other machines using the key must coordinate.
The request reservation ledger is written before network I/O, including uncertain
failures; HTTP transport timeout is 30 seconds, response cap is 2 MiB, and redirects
are refused. Account checks store only allowlisted entitlement/counter fields, not
the account response or credentials.

Free advertises approximately one year of history. The CLI deliberately restricts
all windows to **completed Jakarta dates within 330 days**. This is a conservative
client boundary, not a claim about the exact provider cutoff. The bootstrap chosen
here is **2026-08-17 through 2026-09-25**, not the maximum permitted year.

## Actual initial run

Batch: `data/collector-output/pilot/1f7cc972-68ee-41cd-9bce-eded7efda103.json`.
Data and generated summaries remain Git-ignored.

| Instrument | Evidence available | Initial request window |
|---|---|---|
| BBCA | Reused 28 rows | Full bootstrap window |
| ANTM, GOTO | Reused 30 rows each | Full bootstrap window, including holiday padding |
| BBRI | New 28 rows | Full bootstrap window |
| RAJA, LPIN | New 30 rows each | Full bootstrap window, including two zero-volume rows |
| JKSE.INDX | Reused three rows, September 23–25 | Partial window; must be expanded on resume |
| VKTR, ENRG, PTRO, DSSA | QUOTA_DEFERRED/SOURCE_ERROR | Pending quota reset |

The successful collection used **3 new quota units / 8 HTTP requests / 10,781
response bytes / 7.269 seconds summed HTTP elapsed**. The counter moved **13 → 16**;
earlier same-day experiments account for the initial 13. Bytes include the five
account checks. Earlier diagnostic checks used zero units and are not included in
these batch totals; the initial rejected lowercase-plan check made one zero-unit
request. No extra market-data calls were made after the ceiling was reached.

The database admitted **21 bars**: seven instruments × September 23, 24 and 25.
Initial ingestion appended 21 revisions; identical replay appended **zero**.
PostgreSQL rejected an attempted evidence update inside a rolled-back verification
transaction. **Zero canonical bars** exist on the two known holiday dates.
The batch summary records 21 AVAILABLE, 22 CLOSED, 385 SESSION_UNCONFIRMED and 12
SOURCE_ERROR instrument/date observations. Other weekdays and weekends lack an
explicit proof here and remain unconfirmed; no prices were fabricated.

## Evidence and canonical boundary

Python fetch → existing content-addressed immutable archive → sanitized manifest
and decimal-preserving normalization → .NET raw SHA/length/value verification →
independent session proof → append-only PostgreSQL `daily_bar_revision` → features
and ignored local summary. The worker verifies all normalized rows against original
JSON and rejects omitted, duplicate or changed evidence values. Failed requests do
not become zero-priced bars. A valid empty response has no invented rows.

Each artifact preserves provider, token-free URI/parameters, original retrieval
timestamp, SHA-256, bytes and parser version. Every retrieval remains in the ignored
collector ledger; PostgreSQL `raw_fetch_observation` retains admitted batch retrieval
manifests even when identical content already exists. Identical rows retain their
existing revision; freshly retrieved changed content appends, including an A→B→A
reversion. Older cached evidence cannot revert a newer response. Database
triggers reject updates/deletes to bars, raw provenance and retrieval observations.
New migration 0002 extends the foundation without editing migration 0001.

Canonical known time is ingestion time, at/after retrieval and independently recorded
session knowledge. Old market dates do **not** make this evidence available in past
historical replay. Raw close and adjusted close are separate. OHLCV remain provider
values; volume is never multiplied/divided by 100. Equity metadata records sampled
share-scale corroboration, provider-documented split adjustment and **UNKNOWN market
segment**; IHSG volume units remain UNKNOWN. Split/rights-issue accounting and JK
segment-wide semantics are not cleared. Canonical pilot quality is DEGRADED, not a
production-feed PASS; the generic canonical types do not embed EODHD identifiers.

`pilot/sessions.json` is a small independent evidence register, not a calendar
derived from EODHD rows. It records September 23–25 completed sessions from dated
[September 23 close](https://pasardana.id/news/2026/9/23/ditutup-ke-level-6-374-ihsg-rabu-berhasil-menguat-1-56-persen),
[September 24 close](https://pasardana.id/news/2026/9/24/ditutup-di-level-6-298-ihsg-kamis-melemah-1-20-persen),
and [September 25 weekly close report](https://www.idnfinancials.com/news/69483/ihsg-falls-3-09-in-a-week-amid-idr3-15tn-outflows).
The two Panin closure references are preserved in the register. Its `known_at` is
when evidence was recorded, not publication or market date. Conflicting proofs,
future knowledge and provider-hosted calendar references are rejected. New dates
need independent completed-session proof; otherwise collection may archive them
but canonical admission stays SESSION_UNCONFIRMED. Zero/flat rows do not establish
holidays, suspension or no-trade, and zero-volume rows are not admitted.

## Feature readiness

Features use canonical raw OHLC/close, latest visible revision at an explicit cutoff,
and independently confirmed sessions visible at that cutoff. Known closures and
weekends are skipped. Unknown weekdays or missing confirmed bars break continuity;
they are never bridged or filled. Split/dividend discontinuities remain a limitation
of this raw-price pilot; there is no corporate-action-normalized research series.

- EMA20/EMA50: initial period SMA, then alpha 2/(period+1).
- ATR14: 14 true ranges (15 bars) as seed, then Wilder alpha 1/14.
- Prior high/low: previous 20 valid sessions, excluding the current bar.
- Volume ratio: current exact count / prior-20 mean; null when metadata bases differ.
  A same-basis ratio does not resolve segment or absolute-unit uncertainty.
- Relative performance: 20-session stock price return factor / index price return
  factor − 1; all 21 benchmark dates must align with the stock's valid dates.

Insufficient warm-up yields **null**, never zero. The actual accepted instruments
currently have only three confirmed consecutive sessions and all requested feature
values are null/WARMUP. Four uncollected instruments have MISSING feature histories
and SOURCE_ERROR observation outcomes. A missing latest confirmed session returns
STALE with the original last market date. Features remain AVAILABLE_PILOT after
warm-up, not trading recommendations. Golden fixture checks cover EMA/ATR seeds,
current-bar exclusion, holiday padding, gaps and benchmark alignment.

## Manual local commands

From the repository root, with Python 3.12+ and .NET 10 installed:

```sh
# .env must stay ignored AND untracked; never display it.
git check-ignore -q .env || exit 1
if git ls-files --error-unmatch .env >/dev/null 2>&1; then exit 1; fi
set -a
source .env
set +a
test -n "$EODHD_API_TOKEN" || exit 1
docker compose up -d postgres

# After the next GMT quota reset: fetch only pending full-window requests (five).
PYTHONPATH=collectors/python/src python3 -m idx_stock_collector.pilot \
  --from 2026-08-17 --to 2026-09-25 \
  --resume data/collector-output/pilot/1f7cc972-68ee-41cd-9bce-eded7efda103.json

# Pass the emitted batch path into .NET; this makes no provider calls.
dotnet run --project src/IdxStockIntelligence.Worker -- pilot <batch-path>

# Daily EOD: choose a completed Jakarta date after provider publication.
# First record independently verified session proof and actual known_at.
# Substitute the chosen YYYY-MM-DD; omit --resume for a fresh daily retrieval.
PYTHONPATH=collectors/python/src python3 -m idx_stock_collector.pilot \
  --from YYYY-MM-DD --to YYYY-MM-DD
dotnet run --project src/IdxStockIntelligence.Worker -- pilot <emitted-batch-path>

PYTHONPATH=collectors/python/src python3 -m unittest discover -s collectors/python/tests -v
dotnet test
```

The new resume batch preserves valid cached responses, fetches the four missing
equities and the broader IHSG window, and explicitly defers work if other usage
consumes the budget. Its batch path is printed once; summaries are next to it.
On this machine Python 3.13 was used because the system `python3` is older than the
project requirement. A machine-local PostgreSQL password was added to ignored
`.env`; the existing Compose service was started. No new Python/.NET dependencies
or tracked secrets were added. No scheduler or model call is involved.

## Verification and next bounded task

All **17 Python** and **15 .NET** tests pass. Tests cover immutable/idempotent raw
archival, quota ceiling and lazy reset, credential-bearing response suppression,
entitlement warnings, row invariants, canonical duplicates/corrections/reversions,
as-of visibility, holiday/unknown/zero exclusion, pre-listing absence, adjusted close
separation, feature warm-up/continuity and IHSG alignment. Local PostgreSQL replay
and mutation rejection passed separately.

**Ready for manual prospective daily collection: YES**, with explicit incomplete
calendar/identity semantics and no numeric feature-readiness claim. Completing all
initial requests is pending the quota reset. Next: resume five initial-window
requests, extend independent session evidence without using provider rows as proof,
then observe ten completed daily runs and their errors, revisions and publication
lag. Keep normal daily collection at 11 units and coordinate any bootstrap to stay
at/below 16 account units. MEDIUM reasoning is appropriate. Do not start strategies,
full-universe screening, a paid plan, or 2022-present backfill.
