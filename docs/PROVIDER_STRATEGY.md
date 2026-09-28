# Phase 0 provider selection gate

Reviewed 2026-09-28. Decision: select **Scenario C as the conditional full-universe target**, and **Scenario A as the immediate free experiment**. EODHD is the first historical/daily price candidate; Index Alpha is the optional broker/foreign candidate. This selects roles and experiments, not a canonical production feed. No purchase, provider account creation, market-data API request, backfill, scheduler, or source-code change occurred.

The security-master and calendar results remain PARTIAL, and complete historical all-IDX identity remains BLOCKED. Historical 2022 coverage is not a prerequisite for a narrower prospective system. A successful small price panel does not clear the other Phase 0 gates.

## Role assignment

| Role | Selected path | Condition before canonical use |
|---|---|---|
| Historical bootstrap, approximately 2022 onward | EODHD paid All World EOD; BLOCKED for execution today | JK sample depth, volume/segment semantics, retained-use permission, delisted population and identity evidence |
| Prospective daily EOD | EODHD JK whole-exchange EOD, conditional primary; Invezgo Advance fallback candidate | Rights, complete eligible-universe reconciliation, final publication time, independent price comparison, corrections and adverse cases |
| IHSG | EODHD only if its own index search/catalogue identifies the real Composite price index; otherwise Invezgo `COMPOSITE` after API rights clarification, or licensed IDX Indices | EODHD identifier is unverified; never assume Yahoo's code works. No authorized benchmark feed has passed |
| Broker / foreign enrichment | Deferred; Index Alpha Starter for shortlisted stocks after retention/redistribution clarification | No broker-flow engine; daily records require single-day queries |
| Fundamentals / ownership / events | Deferred; Invezgo API candidate, or contracted IDX Data Reference / issuer evidence | Publication versus period dates, corrections, rights and archival permission |
| Reconciliation | Manual official IDX observations and issuer/KSEI event notices; later an independently licensed feed | Manual spot checking only; website access does not grant recurring collection rights. Verify upstream independence |

If EODHD fails IDX provenance or tape reconciliation, stop its promotion and test the Invezgo/official licensed route. Record provider choice explicitly; never silently substitute a provider or splice incompatible adjustment bases. A cheaper plan is not a quality guarantee.

## Classification rules and candidate matrix

`PASS` is confined to the stated dimension and scope. `PARTIAL` means documented capability with incomplete IDX-specific or empirical evidence. `BLOCKED` means no established usable route for that dimension in the reviewed offering, not proof that a custom product cannot exist. Rights `UNCLEAR` never authorizes retrieval. No provider is approved for canonical ingestion.

Cost bands are local planning labels, not scores: FREE = no recurring charge; LOW = up to US$40 or Rp600,000 monthly in the quoted currency; MEDIUM = above LOW up to US$100 or Rp1,500,000; HIGH = above those thresholds; UNKNOWN = no reliable applicable quote. These thresholds are not exchange-rate conversions. Taxes, card FX fees, exchange entitlements and any negotiated license are additional unless explicitly included.

| Provider / offering | Rights | Historical OHLCV | Prospective EOD | IDX coverage | Delisted coverage | IHSG | Corporate actions | Request budget | Cost |
|---|---|---|---|---|---|---|---|---|---|
| EODHD All World EOD | PASS for registered nonprofessional private storage/analysis while entitled; post-cancellation UNCLEAR | PARTIAL | PARTIAL | PARTIAL | PARTIAL | PARTIAL | PARTIAL | PASS on paid arithmetic; free full universe BLOCKED | LOW |
| Invezgo Advance API | UNCLEAR | PARTIAL, regular plans only last two years | PARTIAL | PARTIAL | BLOCKED | PARTIAL | PARTIAL | PASS on daily arithmetic with bounded enrichment; bootstrap PARTIAL | LOW |
| GOAPI Stock IDX | UNCLEAR; canonical use BLOCKED pending upstream grant | PARTIAL, depth/page size unknown | PARTIAL technically, rights-blocked | PARTIAL | BLOCKED | PARTIAL, identifier/history unknown | BLOCKED | PARTIAL, quota/batch ceiling unknown | UNKNOWN |
| Index Alpha | UNCLEAR for durable archive/upstream rights | BLOCKED, no documented OHLCV feed | BLOCKED for OHLCV | PARTIAL for enrichment only | BLOCKED | BLOCKED | BLOCKED | PASS on paid shortlist arithmetic; free panel PARTIAL | FREE / LOW paid |
| IDX Edge PRO, named candidate | UNCLEAR | BLOCKED | BLOCKED | BLOCKED | BLOCKED | BLOCKED | BLOCKED | BLOCKED | UNKNOWN |
| Twelve Data Pro XIDX | UNCLEAR for permanent evidence archive; cancellation archive BLOCKED | PARTIAL | PARTIAL | PARTIAL | BLOCKED | PARTIAL, exact identifier unknown | PARTIAL | PASS on paid arithmetic | HIGH |
| IDX licensed EOD / historical / reference services | UNCLEAR until individual contract | PARTIAL; open/history specification needed | PARTIAL | PARTIAL, official all-board product advertised | PARTIAL, complete historical population unproven | PARTIAL | PARTIAL | PARTIAL, delivery specification unknown | UNKNOWN, institutional catalogue is HIGH |

### EODHD

The [personal terms](https://eodhd.com/financial-apis/terms-conditions) permit registered nonprofessional private storage and analysis, prohibit redistribution, and allow monthly cancellation. They do **not explicitly settle continued use of retained raw/normalized data after cancellation**. Absence of a deletion clause is not a perpetual grant. Scenario B needs written confirmation before purchase; active-subscription private use has a narrower rights PASS.

[Pricing](https://eodhd.com/pricing) lists All World EOD at **US$19.99/month**, free at 20 units/day and one year of history, and paid at 100,000 units/day. [Limits](https://eodhd.com/financial-apis/api-limits) distinguish 1,000 HTTP requests/minute from billable units: symbol EOD, split history, dividend history and symbol lists cost one each; fundamentals cost ten; exchange bulk costs 100. Bulk is not a free-plan entitlement. Use monthly prices, not annualized discounts, for one-month bootstrap budgeting.

The [JK catalogue](https://eodhd.com/exchange/JK) shows 924 active tickers during this review. That is not the same population/date/type definition as the earlier 982 KSEI EQUITY candidates. Neither count proves complete ordinary-share coverage; reconcile identities rather than subtracting counts and declaring missing securities.

[EOD documentation](https://eodhd.com/financial-apis/api-for-historical-data-and-volumes) permits one symbol's entire date range in one response, without a documented daily-history pagination requirement. Prices are described as unadjusted OHLC; adjusted close incorporates splits/dividends; volume is split-adjusted. Value and trade frequency are absent. Adjusted history is recalculated after new dividends. The [bulk field description](https://eodhd.com/financial-apis/bulk-api-eod-splits-dividends) instead describes volume as shares traded: validate equivalence, units, regular-market scope and dated split cases before mapping. Rights-issue adjustments and correction notices/SLA are undocumented here.

[Delisted documentation](https://eodhd.com/financial-apis/delisted-stock-companies-data-2) supplies a separate `delisted=1` symbol list and regular history endpoints. This is a useful discovery route, **not evidence of complete JK delisted coverage or a point-in-time universe**; US rename history must not be transferred to IDX.

[Source disclosure](https://eodhd.com/financial-apis/our-data-sources-and-data-partners) names some contracted exchanges and also CFD/market-maker sources, but does not establish the JK upstream feed. Ask whether JK equities/IHSG represent actual exchange sessions and trades, not indicative synthetic quotes. [Bulk documentation](https://eodhd.com/financial-apis/bulk-api-eod-splits-dividends) explicitly warns that holiday/future-date requests can return a different session: inspect returned dates and retain them. Neither request date nor weekday arithmetic proves an actual session.

### Invezgo

[API usage rules](https://docs.invezgo.com/api-usage/) limit regular history to two rolling years; deeper/custom history requires Enterprise. [Developer pricing](https://docs.invezgo.com/getting-started/subscription/) is **Rp499,900 Advance / 30,000 requests per month / 250 per minute**, Rp999,000 Prime / 65,000, Rp1,999,900 Max / 145,000, and Rp4,000,000 Elite / 320,000. Consumer dashboard plans and consumer trials are not API entitlements.

[Endpoint reference](https://docs.invezgo.com/api-usage/endpoint/) includes stock/index lists, price summaries, broker activity, shareholder data, statements and events. [Batch documentation](https://docs.invezgo.com/api-usage/batch/) shows stock OHLC/volume/value/frequency, market selection defaulting to RG, and index `COMPOSITE`. Only the documented order-book and stock/index intraday-summary routes have this batch behavior: Max groups three codes; Elite ten; each batch costs one quota request. Do not transfer this batching to broker, financial-statement or ownership endpoints. A dated summary is not a demonstrated full history-in-one-call feed or final EOD SLA.

[General terms](https://invezgo.com/terms) allow personal saving but prohibit bot collection, while [API rate guidance](https://docs.invezgo.com/api-usage/rate-limit/) recommends local database caching and the Developer product invites automated integration. Ask for an API-specific grant overriding the bot restriction and defining long-term/post-expiry raw and normalized retention. No durable archival term, complete delisted universe, price adjustment policy, rights-issue transformation, volume-unit guarantee or correction feed was established. Event-calendar availability does not establish adjustment semantics.

### GOAPI

The provider's [Swagger](https://goapi.io/swagger/) and [application docs](https://app.goapi.io/docs/) index stock-history and multi-symbol price routes, but identify Yahoo Finance, Google Finance, MSN Money and MarketWatch upstream and 3–10 minute delay. This contradicts [real-time marketing](https://goapi.io/api-data-saham-indonesia/). API access through an intermediary does not demonstrate underlying automated-use and archival rights. [GOAPI terms](https://goapi.io/terms/) describe subscriptions and restrict duplication/exploitation; the [hub](https://goapi.io/hub/) advertises a stock trial without a verified numeric quota or applicable monthly price.

The fresh Swagger body was not extractable; its provider-origin indexed documentation was available. The public YAML returned 403. Range bounds, pagination, batch size, OHLC adjustment bases, turnover fields, 2022 coverage and IHSG time series are therefore unverified. Do not invent request counts, subscribe, or use this as canonical/reconciliation data pending a source-specific upstream grant and specification.

### Index Alpha

[Endpoint docs](https://indexalpha.id/id/docs/endpoints) document broker/foreign data from **2025-01-01**, RG/NG markets and updates at **19:00 Asia/Jakarta**. Multi-day requests aggregate the interval; they do **not** return daily histories. Daily enrichment requires `from=to`. No OHLCV or IHSG feed is documented.

[Limits](https://indexalpha.id/id/docs/limits) allow 50 tickers/batch, charge one quota unit per ticker per dataset, cap ranges at 366 days, and offer five free units/day. [Pricing](https://indexalpha.id/id/pricing) currently displays **Rp200,000/30 days Starter** (25,000 units), Rp350,000 Professional (100,000), and Rp750,000 Advanced (250,000), with higher crossed-out reference prices. Recheck checkout price before any future purchase. API/CSV availability supports feasibility but no accessible durable-storage, upstream-license or post-cancellation grant was established. Free registration is not authorization to archive indefinitely.

### IDX Edge PRO

The exact named offering could not be verified as a documented data API. Targeted searches and attempted publisher access did not establish a provider-owned API specification, price, quota or license; publisher retrieval failed. Do not equate the IDXEdge news publisher with IDX Data Services, or invent a PRO API based on the name. Exclude from the operational shortlist until a provider-owned product URL and contract are supplied.

### Twelve Data

[XIDX coverage](https://twelvedata.com/exchanges/xidx) is EOD with Pro+ access; its trial symbol is **PGAS**, not proof of BBCA/ANTM/GOTO entitlement. [Monthly Pro pricing](https://twelvedata.com/pricing) starts at **US$229**, with a lowest displayed 610-credit/minute option. [Historical guide](https://support.twelvedata.com/en/articles/5656039-how-to-get-historical-prices) supports date ranges / up to 5,000 records per symbol, enough arithmetically for this approximately 1,185-session window. Earliest IDX dates still need testing. [Batch guidance](https://support.twelvedata.com/en/articles/5203360-batch-api-requests) charges one time-series credit per symbol, not one per HTTP batch; the applicable batch/concurrency ceiling must be checked before grouping requests.

[Adjustment support](https://support.twelvedata.com/en/articles/5179064-are-the-prices-adjusted) describes daily prices as split-adjusted, with separate event endpoints; raw daily tape and IDX-specific rights/correction behavior are not established. The [terms, sections 12 and 16](https://twelvedata.com/terms) require deletion after termination/expiry, including a 30-day deletion requirement, and restrict retention by subscription/provider terms. This blocks a download-and-cancel durable archive without a written exception. Value/frequency and delisted IDX completeness are unverified.

### Official / licensed IDX

The fresh [IDX Data Services product page](https://www.idx.id/en/products/idx-data-services/) confirms direct/system-to-system and redistributor EOD, index and reference products. Individual eligibility, non-display personal analytics permission, retained-use rights and delivery costs require a contract. The [2026 catalogue](https://www.idx.id/media/auobyarx/idx-catalogue-pricelist-updated-2026.pdf) was discoverable through indexed provider-origin text, but fresh download returned 403. Treat its indexed institutional pricing as indicative, not an individual quote: indirect Professional non-group main is listed at Rp2,700,000/month, excluding unspecified connection/non-display/index/reference additions.

The prior catalogue review and indexed Professional specification list high/low/close/volume/value/frequency but **omit open**; Basic also omits high/low. Indices omit open too. Never substitute previous close for open. Complete OHLC, 2022 depth, delisted identity, suspension events, units, split/dividend/rights treatment, revisions and delivery/file counts must come from a reviewed specification. This is the authoritative fallback, not a proved low-cost individual solution.

### Previously screened alternatives

[Alpha Vantage support](https://www.alphavantage.co/support/) still quotes 25 ordinary free requests/day; special verified educational/open-source eligibility must not be assumed. IDX/IHSG symbols remain unproven. [Marketstack pricing](https://marketstack.com/pricing) still offers 100 requests/month and one year free, with Basic US$9.99/month / 10,000 requests / ten years globally; exchange-reference listings do not prove XIDX price coverage, retention or delisted history. Neither is selected ahead of an established IDX candidate. No usable primary-source Stooq IDX specification was recovered.

Yahoo/Google/MSN/MarketWatch remain outside canonical automated collection absent appropriate grants. KSEI/OpenFIGI/TradingHours retain their existing identity/calendar-only roles and unresolved gates in [DATA_SOURCE_MATRIX.md](DATA_SOURCE_MATRIX.md); they do not supply the missing OHLCV feed.

## Request budget: HTTP requests versus quota units

Sizing assumptions, not measurements: N = 900–1,000 eligible equities, S = 20/50/100 shortlisted stocks, D = additional historical delisted identities (unknown), T approximately 1,185 sessions through 2026-09-28, and 22 sessions/month for operating budgets. N is not a validated universe. Discovery, retries, revisions, holidays, source permissions and actual delivery are separate. No full requests were made.

### Initial bootstrap

| Provider / route | Price-history HTTP estimate | Billable usage / qualification |
|---|---|---|
| EODHD per-symbol date range | N + D + 1 IHSG = 901–1,001 + D | Same units; plus three discovery requests (JK active list, JK delisted list, index search) = **904–1,004 + D**. Complete split + dividend histories add 2(N+D): **2,704–3,004 + 3D** total units/HTTP. Entitlements/JK event coverage must pass |
| EODHD exchange-date bulk alternative | T stock requests + 1 index-history request = about 1,186 | About 118,501 units + discovery, before actions. More expensive than symbol history; at 100,000 units/day needs more than one quota day. JK historical bulk population untested |
| Invezgo dated summary, established route | Full 2022 request impossible on regular plans; at most approximately 500 recent sessions | Advance one-symbol/day route would be 450,000–500,000 stock calls for two years. Max batches: 150,000–167,000; Elite: 45,000–50,000. These are upper-plan single-date-route models, **not a claim that date-range history is unavailable**; OpenAPI/spec access was blocked, so an efficient series/database export needs provider confirmation |
| Twelve Data daily time series | N + D + 1 = 901–1,001 + D single-symbol requests, if dates exist | Same time-series credits; optional earliest-date probes add N+D+1. 5,000 rows exceeds T; no stock × day loop. Batch can reduce HTTP only; no invented batch ceiling |
| Index Alpha / IDX Edge PRO | No established 2022 OHLCV route | Not a bootstrap provider |
| GOAPI / official IDX | UNKNOWN | GOAPI range/pagination limits and IDX bulk-file packaging must be specified; do not assume N×T or one file |

EODHD's 100,000 paid daily units easily cover the proposed 3,004-unit known-universe bootstrap arithmetic in one quota day, but that is not permission to execute, a time guarantee, or completeness proof. D and unknown identity events can enlarge scope. A current universe alone is survivor-biased.

### Normal daily EOD cycle

| Route | HTTP/day | Units/day | Base 22-session month |
|---|---:|---:|---|
| EODHD paid JK price bulk + verified IHSG symbol | 2 | 101 | 44 HTTP / 2,222 units |
| EODHD price + bulk splits + bulk dividends + IHSG | 4 | 301 | 88 HTTP / 6,622 units; preferred event-aware budget if entitled |
| EODHD per-symbol fallback + IHSG | 901–1,001 | 901–1,001 | 19,822–22,022 HTTP/units; fallback must be explicit |
| Invezgo Advance single stock summaries + index summary | 901–1,001 | 901–1,001 | 19,822–22,022 of 30,000 monthly requests |
| Invezgo Max batches of three + separate index | 301–335 | 301–335 | 6,622–7,370; Rp1,999,900 plan, not a cost saving versus Advance |
| Invezgo Elite batches of ten + separate index | 91–101 | 91–101 | 2,002–2,222; Rp4,000,000 plan |
| Twelve Data time series, one latest complete bar/symbol | 901–1,001 unbatched | 901–1,001 credits | 19,822–22,022 credits; at 610/minute requires at least two windows; observe exact plan/concurrency rules |
| GOAPI / official IDX | UNKNOWN | UNKNOWN | Needs documented quotas, batching/delivery and finalization |

These are base cycles, not complete revision detection. For EODHD, four weekly checks of five previous exchange price dates plus IHSG dates add 40 HTTP / 2,020 units/month. Monthly active+delisted-list refresh adds 2 HTTP / 2 units. Thus an illustrative event-aware month is **130 HTTP / 8,644 units**, plus per-symbol full-history refreshes following adjustment events, exceptional recovery and contractually required rechecks. Reserve 20% capacity before adding those explicitly metered calls. This rolling check cannot detect every old correction: ask for a change feed or set a separately budgeted periodic full-history comparison. Historical snapshots remain immutable; new adjustments/revisions append evidence.

### Shortlist enrichment

Broker and foreign are separate datasets. Do not infer daily broker history from a range aggregate or treat broker turnover as the equity's canonical trade tape.

| S | Index Alpha broker + foreign HTTP/day with 50-symbol batches | Index Alpha units/day | Units / 22-session month | Invezgo broker-summary only calls/day, without undocumented batching |
|---:|---:|---:|---:|---:|
| 20 | 2 | 40 | 880 | 20 |
| 50 | 2 | 100 | 2,200 | 50 |
| 100 | 4 | 200 | 4,400 | 100 |

Index Alpha free five-unit quota cannot supply either dataset daily for a 20-name panel; two datasets allow at most two names/day with one spare unit. Starter 25,000/month accommodates all three shortlist sizes plus a 20% reserve, after rights approval. Backfilling *daily* enrichment uses 2×S×actual sessions since 2025, even if HTTP is batched: date-range responses aggregate rather than return a daily series.

For a future broader Invezgo shortlist, an explicit six-call/name refresh can comprise broker summary, BS/IS/CF statement requests, ownership and key statistics: 120/300/600 HTTP and units per refresh for S=20/50/100; daily repetition would consume 2,640/6,600/13,200 monthly units. This is a designed workload, not permission or evidence those datasets change daily. Prefer quarterly/event-driven statements and weekly ownership only when publication semantics are established; calendar fetches and a separate foreign feed add requests. No batching assumed. EODHD fundamentals would be S HTTP / 10S units per refresh but require a different paid entitlement (Fundamentals US$59.99/month or All-in-One US$99.99/month), and do not establish Indonesian broker coverage.

Invezgo Advance as a daily stock+IHSG source plus two per-name enrichment calls fits N=1,000/S=100 at 26,422 units/month, but adding 20% headroom gives 31,706.4 > 30,000: not robust at that cadence. At S=20 it is 22,902 base / 27,482.4 with reserve. Broad six-call refreshes for 100 names must be less frequent or separately supplied.

## Data size and local capacity

Planning arithmetic: four full years (2022–2025) plus 270/365 of 2026 gives approximately 4.74 years. At 250 sessions/year, T≈1,185; **900×T≈1,066,500 and 1,000×T≈1,185,000 bars**. End-2026 upper planning figure: 1.25 million for 1,000 names. These are capacity estimates, not a reconstructed calendar. Pre-IPO periods, delisted dates and no-trade/suspension states reduce actual bars; additional historical securities/revisions increase evidence.

Assume 150–400 bytes per normalized bar including row/provenance/index overhead: approximately **160–474 MB** for one version of the estimated panel. PostgreSQL numeric layout/index design may change that. Plan **1–3 GB** for canonical rows, indexes, provenance and some revisions rather than promising an exact size. WAL, backups and many revisions need extra space.

Assume uncompressed historical CSV 70–140 bytes/bar: **75–166 MB**; JSON 180–400 bytes/bar: **192–474 MB**, before manifests/event payloads and repeats. Compression savings depend on measured payloads. At 200–400 KB for one hypothetical 1,000-row daily JSON snapshot, 250 sessions add approximately 50–100 MB/year per snapshot version; repeated whole-history refreshes are much larger. These are transparent estimates, not extrapolated provider measurements. Reserve **5–10 GB plus backups** initially, then measure actual bytes. Existing local PostgreSQL and ordinary filesystem storage suffice at this scale; no big-data platform is justified. Enrichment, tick/order-book data and indefinite revision accumulation are outside this estimate.

## Three cost scenarios

| Scenario | Architecture and monthly cost | Lost capability / condition |
|---|---|---|
| A — zero cost | EODHD free, three equities plus IHSG only if confirmed/entitled; Rp0 recurring. Optional two-name Index Alpha trial only after archive rights clear | Recent year only; no full-universe daily sweep/bulk or 2022 bootstrap, no proven delisted/PIT research, value/frequency unavailable, no full-shortlist enrichment. Not a 900–1,000-name production replacement |
| B — bootstrap-only payment | One EODHD All World EOD month: US$19.99 once, then EODHD free small panel at Rp0 recurring, or a separately permitted daily provider at its quoted price | **Not approved today**: obtain explicit post-cancellation raw/normalized/derived retention rights. A one-time payment does not unlock a proven free full-universe daily source. Twelve Data is unsuitable without a retention exception |
| C — low-cost continuous | Conditional EODHD US$19.99/month for prices and entitled actions; optional Index Alpha Starter adds Rp200,000/30 days for broker+foreign shortlist | Cheapest documented role combination among verified IDX candidates, conditional on rights/quality gates. Defer broad fundamentals/ownership rather than buy them now. Manual official sample reconciliation has no subscription charge but costs time |

Scenario C alternatives: Invezgo Advance alone is Rp499,900/month for prospective prices/index and bounded broader shortlist work, after the contradictory rights terms are resolved; if its final EOD and budget semantics pass it can replace the two providers, not silently supplement them. EODHD + Invezgo costs US$19.99 + Rp499,900/month and is justified only when broader enrichment is actually required. Twelve Data starts at US$229/month before enrichment/retention exceptions. IDX institutional fees are not an affordable individual quote. GOAPI/IDX Edge PRO lack a verified applicable price/rights route and cannot be declared cheaper sustainable alternatives.

Amounts remain in provider billing currencies; no live IDR exchange rate or tax-inclusive checkout total was established. For any later paid decision, obtain current total cost and explicit permission first. No purchase is authorized by this document.

## Original tiny-spike proposal (superseded by empirical result below)

**YES** to a bounded EODHD free-account recent-panel experiment once a legitimate free key is supplied outside Git and the account's entitlements are checked. **NO** to executing an authenticated experiment now: no relevant token was present in the current environment. No credential store, `.env`, keychain or secret file was searched; no account was created. Needed credential: registered personal **`EODHD_API_TOKEN`**, passed only to the HTTP client. The public demo token's limited-symbol entitlement does not prove JK support and is not a substitute.

Proposed panel: BBCA.JK, ANTM.JK, GOTO.JK; example recent window **2026-08-17 through 2026-09-25** (approximately 20–30 completed sessions, validate actual dates). First inspect the account's zero-cost usage/entitlement endpoint. Then use these at most **ten one-unit data/discovery requests**, with an absolute 12-unit ceiling including bounded retries, below the ordinary free 20/day limit:

1. JK active list, JK delisted list, index search: three requests. Validate actual returned symbols; if an endpoint is not entitled, record that instead of paying/upgrading.
2. One date-range daily EOD response each for BBCA, ANTM and GOTO: three requests, not 3×30.
3. One verified IHSG symbol response only if an index actually maps to the Composite price index: one request; otherwise mark benchmark UNKNOWN and continue the equity experiment.
4. One recent-window delisted-symbol response only if the supplied list contains a verifiable eligible historical case within free history: one request. If not, record delisted coverage NOT TESTED; never invent a case or infer completeness from an empty list.
5. Repeat two of the equity requests to test artifact idempotency and byte/schema stability: two requests. Repeat responses may legitimately differ; do not claim a correction SLA from this.

Stop on 401/403/429, entitlement mismatch, unexpected scope, size >2 MiB/response or timeout >30 seconds. No paid credits, welcome-bonus assumption, paid historical dates, corporate-action backfill or subscription. A paid account token must not be used without separately confirming this request is allowed to consume its quota. Optional free Index Alpha needs its own Bearer key and rights clarification; optional Twelve Data needs an explicitly free XIDX trial entitlement (documented example PGAS only); optional Invezgo needs an explicitly API-enabled trial and the API grant; GOAPI is excluded pending upstream permission. None blocks completion of this research.

Archive exact response bytes with the existing `archive_payload` contract into ignored raw storage. Record source ID, redacted URI/parameters, UTC fetch/availability, response status/content type, byte length/hash, parser version and plan/quota observations. **Remove tokens from requested URI/query parameters and logs before constructing the manifest**; the current helper does not redact secrets or capture HTTP status/headers automatically. Preserve token-free request parameters in an accompanying research record. No canonical ingestion until units/segment/adjustment bases and missing-session states pass.

Compare dated OHLC/volume examples against an independent, authorized broker/official observation; official publicly visible samples may be checked manually, never bulk scraped. Record source/time/market/units and agreement/disagreement. If independent OHLC is unavailable, that gate remains NOT TESTED; source displays supplied by the same upstream are not independent. Probe PBID split, BBCA dividend, MINA rights, GOTO IPO and an authorized suspension/delisted case later under a separately permitted historical experiment; old event windows cannot be obtained through the one-year free price limit.

## Provider independence and chronology check

Inspected only `collectors/python/src/idx_stock_collector/contract.py`, `src/IdxStockIntelligence.Domain/Phase0Model.cs` and `src/IdxStockIntelligence.Application/DailyBarRevisions.cs` for this boundary. The raw manifest has generic source identity, request evidence and content hashes. `SourceReference` and `DailyBar` use generic identities, dates, prices, volume and provenance; no provider-named field exists in these canonical types. The revision store appends changed canonical content and queries by knowledge cutoff.

This is provider-independent, but not a ready adapter: the bar model requires OHLC and volume, and does not currently express adjusted close, turnover/frequency, adjustment basis or market-segment metadata. Preserve these in raw evidence pending a separately scoped semantic design; do not map split-adjusted volume into an undefined raw-volume convention or coerce absent fields to zero. Today's downloaded 2022 history was first known to this system today, not in 2022; do not backdate source availability. Provider independence does not remove chronology, identity or session blockers.

## Next action and stop boundary

The original credential blocker is resolved by loading the ignored repository-local `.env` at the user’s instruction. The bounded experiment below now governs the next action. In parallel, prepare (do not send without authorization) questions on JK upstream/market units, genuine IHSG identifier, delisted coverage, rights issues, final EOD/corrections, and post-cancellation raw/normalized/derived retention. Ask Index Alpha and Invezgo for their API-specific archival/upstream grants before their tests. No bulk backfill or paid purchase follows automatically from passing the small panel.

Next task reasoning: **MEDIUM** for bounded HTTP/provenance/schema validation; **HIGH** for later adjustment accounting, provider reconciliation policy or point-in-time identity/replay. LOW fits only clerical documentation. No local-AI infrastructure changes are needed.

## Bounded empirical result — 2026-09-28

**10-symbol 2022-present historical spike: NO.** The supplied account reports Free / 20 units per day; 10 HTTP requests consumed exactly 8 units, with 500 extra units unchanged. Recent symbols are confirmed, and 28 BBCA / 30 ANTM / 30 GOTO rows were archived. There were no nulls, duplicate dates or invalid OHLC bounds. BBCA's raw/adjusted closes differ on eight dates; the identical BBCA repeat reuses its content-addressed artifact and parses deterministically. This supersedes the original credential-missing status and its proposed two repeats: only ONE repeat was performed as authorized. No paid plan, full history or bulk endpoint was used.

ANTM/GOTO include August 17 and August 25 closure rows with flat OHLC and zero volume, while BBCA omits both. Thus provider dates are not automatically confirmed exchange sessions. A tiny dated BBCA reference comparison matches OHLC but reports volume 100× lower with a shares label; no conversion or independence claim is justified. JK shares/lots, market segment, raw historical volume, upstream and finalization remain unresolved. Value/frequency are absent. Separate-close preservation is empirically established, but split/dividend calculations are not.

Both index-name searches returned empty arrays: IHSG NOT VERIFIED, without guessing a symbol. Inactive JK discovery returned 96 entries including SCBD.JK; no listing/delisting dates or old prices were retrieved, and at least one warrant-style code is labelled Common Stock. Delisted discovery is PARTIAL, not a historical-universe guarantee. Six unique raw artifacts (160,528 bytes) and token-free manifests remain Git-ignored; the complete request ledger, hashes, reference provenance, conditional sizing and dimension verdicts are in [DATA_SPIKE_RESULT.md](DATA_SPIKE_RESULT.md#earlier-eodhd-empirical-validation--2026-09-28).

Access and experiment idempotency PASS only within this narrow scope. Panel/OHLC/volume/adjustment/delisted/reconciliation and overall rights remain PARTIAL; IHSG NOT VERIFIED; operational suitability for 2022/full-universe use BLOCKED. Prior contractual/private-use findings and conditional role assignments remain unchanged: requests succeeding do not authorize production.

Exact next action: obtain authoritative JK volume/segment and holiday-row semantics, an explicitly entitled deeper-history route, a verified IHSG identity and a final dated reference observation with explicit units/upstream. Reassess with a separately authorized ≤6-unit recent check, without purchasing or launching history automatically. Current free recent access and bonus quota do not prove 2022 entitlement. The historical bootstrap gate remains NO; next bounded validation uses MEDIUM reasoning.

## Semantic follow-up — 2026-09-28

The [latest empirical follow-up](DATA_SPIKE_RESULT.md#latest-decision--eodhd-semantic-follow-up-2026-09-28) used **five additional units**, below its six-unit ceiling. The actual INDX catalogue verifies **JKSE.INDX** as Jakarta Stock Exchange Composite Index. A three-day September 23–25 sample aligns with equity dates and its final close matches a published IHSG observation to two decimals. Earlier empty name searches did not establish absence. The conditional recent IHSG role now has identity/access evidence; 2022 coverage remains unproved.

BBCA/ANTM September 25 volumes reconcile exactly with dated references explicitly using lots and defining 100 shares per lot. This corroborates share-count scale without inferring unit from ratio; it does not certify EODHD JK market segment, raw split history or independent upstream. ANTM's two holiday rows carry forward the prior close and the August 25 overlap is reproducible; GOTO pads the same closures while BBCA omits them. Treat verified closures as retained non-session evidence, never infer status from flat prices alone, and do not generalize the pattern beyond tested cases.

One previously discovered SCBD.JK five-day historical probe returned HTTP 200 with a Free-plan one-year-limit warning, not bars. Current Free entitlement is empirically insufficient for 2022-present; extra call quota does not grant deeper history. Advertised paid history remains a separate unpurchased capability. Volume remains PARTIAL, bounded holiday behavior is understood, and recent IHSG identity/access is verified, but **the 10-symbol historical gate remains NO**. Authoritative JK volume/segment/upstream clarification and legitimate deeper-history entitlement remain next; no purchase or further experiment starts automatically.
