# Phase 0 Data Spike Result

## Latest decision — EODHD empirical validation, 2026-09-28

**CAN WE PROCEED TO A 10-SYMBOL 2022-PRESENT HISTORICAL SPIKE? NO.** Recent access works, but the observed account is free (documented past-year history), ANTM/GOTO include exchange-holiday rows, JK volume units/segment are unresolved, independent volume reconciliation is incomplete, and no IHSG identifier was verified. This is an experimental result, not canonical production approval. No historical backfill, purchase, plan change, scheduling, indicators or strategies were performed.

### Entitlement and secret boundary

After confirming `.env` is Git-ignored and untracked, the user-authorized shell loaded it with `set -a; source .env; set +a`. Only token presence was checked; an exact-match tracked-file scan was clean. The token was never printed or stored in evidence. `/api/user` was inspected in memory: `subscriptionType=free`, `dailyRateLimit=20`, initial `apiRequests=0` with the unset date `1970-01-01`, final `apiRequests=8` dated `2026-09-28`. `extraLimit=500` was unchanged: no bonus units were consumed. The documented [usage endpoint costs zero units](https://eodhd.com/financial-apis/api-limits); its complete response was deliberately not archived because account responses may contain credentials or personal details. Only allowlisted plan/counter fields were retained locally. No account identity was committed.

### Verified instruments and scope

The one-unit JK active-symbol response contained 924 entries and confirmed the panel below. Symbols were constructed from returned Code + Exchange, not guessed suffixes.

| Symbol | Provider name | Exchange / currency / type | ISIN | Rows in requested range |
|---|---|---|---|---:|
| BBCA.JK | Bank Central Asia Tbk | JK / IDR / Common Stock | ID1000109507 | 28 |
| ANTM.JK | PT Antam (Persero) Tbk | JK / IDR / Common Stock | ID1000106602 | 30 |
| GOTO.JK | GoTo Gojek Tokopedia PT | JK / IDR / Common Stock | ID1000166903 | 30 |

Every equity request used `from=2026-08-17`, `to=2026-09-25`, `period=d`, `order=a`, `fmt=json`. The first BBCA row is August 18; ANTM/GOTO begin August 17; all end September 25. The window has 30 weekdays, of which August 17 and August 25 are independently documented exchange closures. Each sample contains all 28 candidate open weekdays; this is a bounded calendar comparison, not a complete observed-session/suspension ledger.

### Exact request accounting

All ten HTTP responses were 200/application-json. There were eight billed requests and two zero-unit usage checks. The final account counter confirms **8 quota units**, below the hard 12-unit ceiling. No retries, redirects, bulk endpoints or guessed-symbol EOD probes were used. The client used a 30-second timeout, a 2 MiB response ceiling, secret-free provenance, and refused to archive any response containing the token.

| Request | API path (token omitted) | Units | HTTP | Response-body bytes | Elapsed seconds |
|---|---|---:|---:|---:|---:|
| usage-before | `user` | 0 | 200 | 292 | 1.088 |
| jk-active | `exchange-symbol-list/JK` | 1 | 200 | 136,964 | 1.485 |
| bbca-eod | `eod/BBCA.JK` | 1 | 200 | 3,129 | 1.131 |
| antm-eod | `eod/ANTM.JK` | 1 | 200 | 3,300 | 1.039 |
| goto-eod | `eod/GOTO.JK` | 1 | 200 | 2,970 | 0.948 |
| bbca-repeat | `eod/BBCA.JK` | 1 | 200 | 3,129 | 1.146 |
| ihsg-search | `search/Jakarta` | 1 | 200 | 2 | 1.084 |
| jk-delisted | `exchange-symbol-list/JK` | 1 | 200 | 14,163 | 0.895 |
| ihsg-search-name | `search/IHSG` | 1 | 200 | 2 | 1.389 |
| usage-after | `user` | 0 | 200 | 292 | 0.925 |
| **Total** | **10 HTTP requests** | **8** | | **164,243** | **11.130** |

These byte counts measure response bodies with identity encoding, including the two unarchived usage bodies; HTTP/TLS header/wire overhead was not instrumented and must not be presented as measured. Network/read elapsed time sums to 11.130 seconds. First request began `2026-09-28T07:47:20.678118Z`; the final one began `07:50:00.665888Z` and took 0.925 seconds, giving approximately 160.913 seconds between first start and final completion, including interactive review gaps. Rate-limit headers reported 1,200 HTTP requests/minute for this key; daily units are a separate budget.

### Raw evidence and reproducibility

The existing `archive_payload` content-addressed mechanism preserved the eight data/discovery responses as **six unique files / 160,528 bytes** under ignored `data/raw/eodhd-spike-20260928/`. Token-free manifests and the request/inspection records are under ignored `data/collector-output/eodhd-spike-20260928/`. Each data manifest records provider, sanitized URI/parameters, UTC retrieval time, SHA-256, byte length, original inspector version `eodhd-spike-inspect-1`, HTTP status and content type. Decimal validation subsequently used the committed experimental parser `eodhd-experimental-1`; its version is recorded in `inspection.json`. Retrieval/knowledge time was not backdated to bar dates. No downloaded dataset or generated manifest is tracked.

| Response | SHA-256 | Bytes |
|---|---|---:|
| jk-active | `b26278e229fb2537cd36303bc03b6d5aee121d5c22fa1c99f02223c78d0e1608` | 136,964 |
| bbca-eod | `71398b6697c94b4f7321d0f128c9b9d097df767b3b35f7ade50d2b1c0ba22593` | 3,129 |
| antm-eod | `c6de9f9f3c8019283aa6c11a4bea127d188faaae6cca08ce8ca3e0912e7de839` | 3,300 |
| goto-eod | `574e851f3c074305e7d50a736235bfc6db9f3c01f067d0ea2357804b2ecd207e` | 2,970 |
| bbca-repeat | `71398b6697c94b4f7321d0f128c9b9d097df767b3b35f7ade50d2b1c0ba22593` | 3,129 |
| ihsg-search | `4f53cda18c2baa0c0354bb5f9a3ecbe5ed12ab4d8e11ba873c2f11161202b945` | 2 |
| jk-delisted | `d379fca0e58db3c60bc1f3f5c308cec358a91fa7ebf15bb2bbc1dc0337a0828e` | 14,163 |
| ihsg-search-name | `4f53cda18c2baa0c0354bb5f9a3ecbe5ed12ab4d8e11ba873c2f11161202b945` | 2 |

The BBCA repeat used identical parameters, returned identical 3,129 bytes/hash, parsed identically, and referenced the original artifact. Separate fetch observations were retained; no duplicate raw blob or canonical bar was created. Both empty index searches independently returned `[]` and also share one content-addressed artifact. Local re-archival of each sample again left the unique-file count at six. This demonstrates archive/parse idempotency only, not a provider correction SLA or long-term revision policy.

### Actual schema and validation

All 88 original panel rows contain exactly `date`, `open`, `high`, `low`, `close`, `adjusted_close`, `volume`. There were **zero nulls, zero duplicate dates, zero invalid OHLC bounds**, and ascending provider order. OHLC were integer JSON prices in these samples; BBCA adjusted close has up to four decimal places, while ANTM/GOTO adjusted values were integers. Volume is a nonnegative JSON integer. Dates are date-only `YYYY-MM-DD`, with no timestamp, timezone, market segment, currency, publication/availability time or revision identifier in the EOD payload; currency/exchange come from discovery. Turnover and trade frequency are absent, therefore UNKNOWN rather than zero. No additional provider fields were observed.

A minimal, unwired experimental Python parser preserves raw versus adjusted close as separate exact Decimals, rejects missing/malformed required fields and invalid bounds, rejects duplicate provider dates, sorts deterministically and inserts no dates. Original payload bytes remain outside the parser in the immutable archive. It does not infer tradability, corporate-action causes or market segment, and does not convert provider volume into canonical exchange volume. Neither .NET domain nor scheduler changed.

### Session and OHLC finding

BBCA omits both holiday dates. ANTM and GOTO instead have **four flat zero-volume holiday rows**:

| Provider date | ANTM O=H=L=C | GOTO O=H=L=C | Volume, both |
|---|---:|---:|---:|
| 2026-08-17 | 3,070 | 50 | 0 |
| 2026-08-25 | 3,190 | 50 | 0 |

[Panin's August 17 notice](https://pans.co.id/publikasi/libur-proklamasi-kemerdekaan-1) and [August 25 notice](https://pans.co.id/publikasi/libur-maulid-nabi-muhammad-s-a-w) independently identify both dates as exchange holidays. These provider rows cannot be treated as actual trading sessions. They remain raw evidence; no canonical ingestion or silent blanket deletion occurred. Zero volume alone does not establish holiday, suspension or no-trade. The 28 remaining candidate dates have no observed gaps, but finalization and instrument status are still unproved.

### Raw versus adjusted prices

On eight BBCA dates (August 18–28, excluding closures), adjusted close differs from raw close. Example August 18: raw `close=6300`, `adjusted_close=6275.6757`; difference `-24.3243` IDR. On September 25 both are 6250. All sampled ANTM/GOTO raw and adjusted closes are equal; equality does not collapse their concepts. [EOD documentation](https://eodhd.com/financial-apis/api-for-historical-data-and-volumes) describes unadjusted OHLC, split/dividend-adjusted close, split-adjusted volume, and adjustment-history recomputation. The sample demonstrates separate values, not verified corporate-action calculations or raw pre-split volume. Raw close was never overwritten and adjusted close never promoted as exchange price.

### Volume semantics and tiny manual reconciliation

JK shares-versus-lots remains **UNKNOWN**, not inferred from magnitude. The daily endpoint documents split-adjusted volume; raw historical traded volume, regular-market-only versus broader trading, and the JK upstream remain unverified. No split-event window was requested. Value/frequency were absent.

One dated reference comparison was performed by manually inspecting the public [IDNFinancials BBCA display](https://www.idnfinancials.com/id/bbca/pt-bank-central-asia-tbk), as surfaced by a timestamped search-index snapshot for **2026-09-25 16:55**. The refreshed live page had already advanced to September 28 intraday and was not mixed with the September 25 comparison. No IDX pages were scraped, no private broker endpoint was used, and no reference dataset was downloaded. The publisher disclaims approximately 15-minute delayed informational data. Its exact upstream, market segment and frozen-snapshot durability were not established; it is a reference observation, **not proven independent feed validation**.

| BBCA, 2026-09-25 | EODHD | Reference display | EODHD minus reference |
|---|---:|---:|---:|
| Open | 6,225 | 6,225 | 0 |
| High | 6,275 | 6,275 | 0 |
| Low | 6,200 | 6,200 | 0 |
| Close | 6,250 | 6,250 | 0 |
| Volume | 89,447,400 | 894,474, labelled shares | **88,552,926** |

Volume differs by exactly 100×. The display label conflicts with any automatic lot-to-share explanation, so no conversion or correction was applied. This could reflect display units/formatting or feed differences; cause remains UNKNOWN. OHLC reference agreement is useful but narrow; volume reconciliation is unresolved. ANTM/GOTO and a verified independent final session observation still need reconciliation.

### IHSG and delisted discovery

`search/Jakarta?type=index&limit=50` and `search/IHSG?type=index&limit=50` both returned empty arrays. **IHSG = NOT VERIFIED**; no index prices or guessed identifier were requested. This does not prove EODHD lacks all IHSG coverage.

`exchange-symbol-list/JK?delisted=1&type=common_stock` returned 96 entries. One discovered example is **SCBD.JK**, Pt Danayasa Arthatama Tbk, IDR/Common Stock, ISIN `ID1000085608`. The response exposes Code/Name/Country/Exchange/Currency/Type/Isin but **no listing or delisting dates**. The list also includes `AGRO-W` labelled Common Stock with null ISIN, so the type filter alone does not prove an ordinary-share universe. [EODHD's delisted documentation](https://eodhd.com/financial-apis/delisted-stock-companies-data-2) describes subsequent normal EOD queries, but SCBD historical access was not empirically tested and listing/delisting chronology is NOT VERIFIED. Discovery is PARTIAL, not a survivorship-completeness claim. No old-date probe or delisted backfill was made under the free entitlement.

### Request/cost extrapolation — conditional, not authorization

The sample verifies one recent date-range EOD request per stock, charged one unit; it does not verify 2022 access, historical completeness or delisted prices. [Current free pricing](https://eodhd.com/pricing) documents only the past year. Extra quota does not establish a deeper-history entitlement.

| Designed workload | HTTP / units | Qualification |
|---|---|---|
| 900–1,000-stock 2022-present bootstrap | 900–1,000 EOD + four discovery = **904–1,004** | Conditional on legitimate deeper entitlement; a verified IHSG history would add one. No per-day loop/pagination is established for ordinary EOD range requests |
| Prospective equity EOD, per symbol | **900–1,000/day** | Exceeds observed free 20/day; verified IHSG would add one |
| 22-session per-symbol month | **19,800–22,000** | Equities only, excludes retries/actions/revision checks; verified IHSG adds 22 |
| Existing paid JK price-bulk design | **1 HTTP / 100 units/day; 22 / 2,200 monthly** | Documented only; not called because a single request exceeds this spike ceiling; optional verified index adds 1/day |
| Current three-stock bounded daily design | **3/day; 66/month** | Arithmetic fits ordinary free quota, but not production-approved given session/semantic issues |

Measured original panel size is 9,399 bytes / 88 provider rows ≈106.8 bytes/row including JSON framing. Applying that provisional density to the earlier 1,066,500–1,185,000-bar capacity estimate gives approximately **114–127 MB** of uncompressed EOD JSON, before discovery, actions, manifests, revisions and repeats. This is a sample-based storage estimate, not a measured historical download or a guarantee of row format. The four holiday rows are not counted as confirmed sessions. Observed EOD latency is 0.948–1.131 seconds per original request; sequential scaling would suggest roughly 14–19 minutes for 900–1,000 requests, but future latency/rate limits and long-response size are untested. No runtime SLA is inferred. No money was spent or plan upgraded; future paid operation still needs the earlier rights, entitlement and quality gates.

### Verdict and exact next action

| Dimension | Verdict | Evidence / remaining condition |
|---|---|---|
| Access | PASS for recent experiment | Authenticated free discovery and range requests succeeded; 2022 access unverified |
| Rights | PARTIAL overall | Earlier private entitled-use grant remains; no new contractual/upstream or post-cancellation evidence |
| BBCA/ANTM/GOTO coverage | PARTIAL | All found and recent rows returned; ANTM/GOTO include closures; no historical proof |
| OHLC semantics | PARTIAL | Bounds pass and one dated reference agrees; exchange-session/finalization handling unresolved |
| Volume semantics | PARTIAL | Integer/split-adjusted documented; JK units/segment/raw history and reference discrepancy unresolved |
| Adjusted-price semantics | PARTIAL | Separate values verified; action calculations/event history not tested |
| IHSG | NOT VERIFIED | Two name searches empty; no identifier guessed |
| Delisted coverage | PARTIAL | Inactive discovery/example works; dates, historical access and completeness unverified |
| Idempotency | PASS for experiment | One identical repeat, archive deduplication and deterministic parsing |
| Reconciliation | PARTIAL | One reference OHLC agreement, unresolved 100× volume difference; feed independence unverified |
| Operational suitability | BLOCKED for 2022/full-universe use | Free history/quota limits plus session, volume, benchmark and provenance gates |

**NO** to the 10-symbol 2022-present spike and to full IDX backfill. Exact next action: resolve JK volume units/market segment and holiday-row policy with authoritative provider evidence, obtain a legitimate deeper-history entitlement without assuming bonus quota grants it, verify a genuine IHSG symbol, and compare one final dated BBCA OHLC/volume observation whose units/upstream are explicit. A separately authorized ≤6-unit recent-panel recheck can then reassess these blockers; do not start it or purchase anything automatically. Next reasoning/model level: **MEDIUM** for that bounded recheck; adjustment/replay policy would be a separately scoped HIGH task.

### Validation

`python3.13 -m unittest discover -s collectors/python/tests -v`: **12 passed**, including valid/malformed/missing fields, separate closes/exact decimal, duplicate dates, deterministic sorting/no fill, zero-volume evidence and existing archive/KSEI checks. System `python3` is 3.9.6 and initially failed existing `zip(strict=True)` tests; the repository requires Python 3.12+, so the supported installed 3.13 interpreter was used without changing unrelated code. `dotnet run --project tests/IdxStockIntelligence.Tests/IdxStockIntelligence.Tests.csproj --no-restore`: **10 passed**, zero errors/failures/skips. The configured `dotnet test --solution IdxStockIntelligence.slnx --no-restore` discovered zero tests (exit 5), so the existing xUnit executable was run directly; test configuration was left unchanged. Token-safe staged-file scanning, ignored-artifact verification and `git diff --check` are required before the focused commit. No push.

## Earlier provider selection gate — 2026-09-28 (before empirical validation)

The role recommendation and full request/cost/evidence model are in [PROVIDER_STRATEGY.md](PROVIDER_STRATEGY.md), with dimension/field classifications in [DATA_SOURCE_MATRIX.md](DATA_SOURCE_MATRIX.md). This supersedes the older price-provider next-action recommendation below, while preserving every original security-master/calendar observation and measurement.

- **Historical bootstrap:** EODHD All World EOD is the first candidate; execution remains BLOCKED pending IDX-specific semantics, samples, rights and historical identity/session evidence. No historical download occurred.
- **Prospective EOD:** conditionally select EODHD JK bulk at US$19.99/month as the smallest full-universe target after validation. Invezgo Advance (Rp499,900/month) is the fallback test candidate after API rights clarification. No production provider is approved.
- **IHSG:** discover an actual EODHD Composite price-index identifier before requesting it; none was verified here. Invezgo documents `COMPOSITE`, and licensed IDX Indices is the official fallback; neither has passed a response/retention test.
- **Enrichment:** deferred, with Index Alpha Starter (currently Rp200,000/30 days) selected for bounded broker/foreign shortlist work after rights clarification; Invezgo for broader statements/ownership/events if required later.
- **Reconciliation:** manual official IDX/issuer/KSEI sample observations first, independently licensed feed later. No web scraping or claim that similarly sourced vendors are independent.

### What this gate established

Provider-owned documentation/prices/terms were rechecked for EODHD, Invezgo, GOAPI, Index Alpha, Twelve Data and official IDX services. The exact IDX Edge PRO API offering could not be verified and is excluded pending a provider-owned URL/specification. Previously screened Alpha Vantage/marketstack/Stooq were checked for any reason to promote them; no validated IDX/IHSG route replaced the shortlist. Public documentation retrieval failures (including 403s for the IDX catalogue and OpenAPI specifications) are recorded as limitations, not bypassed.

Rights findings materially constrain selection: EODHD permits entitled nonprofessional private storage/analysis, but post-cancellation use is not explicitly settled. Invezgo general bot restrictions conflict with its API integration/caching guidance. GOAPI discloses Yahoo/Google/MSN/MarketWatch upstream without a discovered applicable upstream grant. Index Alpha's durable archive/upstream rights are unresolved. Twelve Data requires deletion after termination/expiry. Therefore no unconditional paid bootstrap-and-cancel architecture is approved.

Sizing estimates (not executed requests): 900–1,000 equities, approximately 1,185 sessions through 2026-09-28, or 1.07–1.19 million bars. EODHD prices need **904–1,004 HTTP requests/units** including one IHSG history and three discovery requests, plus additional delisted names if verified; adding per-symbol split/dividend history gives **2,704–3,004**, plus three per additional historical name. This is a symbol-range model, not stock×day. One daily stock bulk plus IHSG needs **2 HTTP / 101 units**; including split/dividend bulks needs **4 HTTP / 301 units**. Actual completeness, bytes, latency and JK entitlement remain unmeasured.

Index Alpha's two datasets for 20/50/100 names need 2/2/4 HTTP batches but 40/100/200 quota units/day, or 880/2,200/4,400 over a 22-session month. Historical ranges aggregate rather than return daily observations. Invezgo regular plans stop at two rolling years; expensive per-date counts are documented only as a known-route model, not as proof that an efficient custom/history export cannot exist.

The storage plan is modest: approximately 1–3 GB canonical/provenance/index capacity plus raw evidence, revisions and backups; reserve 5–10 GB initially and measure actual payloads. These are explicit bytes-per-row estimates, not benchmark results. No big-data infrastructure is needed.

### Concrete cost decision

| Scenario | Recommendation | Current limitation |
|---|---|---|
| Zero cost | EODHD free recent BBCA/ANTM/GOTO panel, optional verified IHSG; Rp0 recurring | One-year history / 20 units per day; no full-universe daily service, bulk, 2022 bootstrap or full shortlist enrichment |
| Bootstrap-only payment | Conditional EODHD US$19.99 for one month, then free reduced panel or separately permitted paid daily source | Written post-cancellation retained-use grant required; no established free full-universe daily replacement |
| Low-cost continuous target | Conditional EODHD US$19.99/month + optional Index Alpha Rp200,000/30 days | Rights/semantics/empirical gates still apply; broad enrichment deferred |

Prices are provider billing currencies, before unverified taxes/FX/license additions. No subscription was bought or authorized automatically. Historical 2022-present inadequacy does not block the separately scoped prospective experiment.

### Tiny empirical spike and architecture boundary

**YES in design, not executable with current credentials:** use a legitimate free EODHD account/token supplied outside Git, verify ordinary free entitlements, and retrieve about 20–30 recent completed sessions for BBCA/ANTM/GOTO. IHSG and a recent delisted case are optional only after actual symbol discovery. Cap the experiment at 12 one-unit requests including bounded retries; no paid quota/upgrade or full history. No relevant token was present in the current environment; secret files/stores were not searched. No market-data response, new raw hash, empirical discrepancy or passed price gate exists from this task.

Targeted inspection confirmed generic raw manifest, canonical bar/source fields and revision/as-of logic; no provider-named canonical fields. The helper does not redact credentials or automatically capture HTTP headers/status, so the next spike must use token-free manifest URIs/parameters and retain HTTP evidence separately. Missing turnover/adjustment/segment concepts must remain raw research evidence until a later approved semantic design. Historical data downloaded now cannot be assigned past system knowledge dates.

**Exact next action:** supply `EODHD_API_TOKEN` through the process environment outside Git and execute only the free panel in the strategy document; first confirm account/JK entitlement, then preserve raw evidence and independently reconcile samples. Do not start bulk backfill. Draft provider clarification questions are recorded, but none were sent. Reasoning for the bounded next task: **MEDIUM**; later corporate-action/as-of implementation: **HIGH**.

## Status as of 2026-09-24

- Overall foundation: **BLOCKED** for unattended canonical ingestion pending rights clarification and source/semantics repair.
- Security master: **PARTIAL** technical feasibility; rights `UNCLEAR`.
- Market calendar: **PARTIAL** holiday-announcement feasibility; rights `UNCLEAR`; full session status unproven.
- No OHLCV, IHSG, universe backfill, production provider, or strategy work was performed.

## Security Master & Calendar Spike

### Rights and access gates

[IDX terms](https://www.idx.co.id/id/syarat-penggunaan) explicitly disallow web scraping/crawling, even where noncommercial quotation is allowed with attribution and access date. Commercial use/redistribution requires prior written permission. IDX profile, listing-activity, and holiday pages were researched without automated collection. A website or discoverable endpoint is not an approved data feed.

KSEI exposes an explicit [monthly master download](https://web.ksei.co.id/archive_download/master_securities) and [holiday notice PDF](https://web.ksei.co.id/files/1767843003_Penyesuaian_Pengumuman_Hari_Libur_dan_Cuti_Bersama_PT_KSEI_Ta....pdf). Both direct files returned HTTP 200 without login during a single bounded test. KSEI's disclaimer endpoint returned HTTP 500 during review; no explicit permission for recurring automated retrieval, local raw retention, or redistribution was established. Status remains **UNCLEAR**. No rate limit was found; absence of a published limit is not unlimited permission. Stop routine fetching until rights are clarified.

### Retrieval and preserved evidence

Exactly two file GETs were made after two HEAD size checks. Each GET was capped at 2 MiB, timed out after 30 seconds, and not retried. Raw bytes are in Git-ignored `data/raw` via the existing version-1 manifest contract. Request parameters were empty; no authentication, cookies, browser automation, CAPTCHA, private API, or workaround was used.

| File | Request / source | Fetched UTC | HTTP / type | Bytes | SHA-256 | Local raw URI | Header evidence |
|---|---|---|---|---:|---|---|---|
| KSEI master snapshot | [StatisEfek20260831.txt.zip](https://web.ksei.co.id/Download/StatisEfek20260831.txt.zip) | 2026-09-24 15:26:49 | 200 / application/zip | 155,962 | `8044caf8d83a609f90e3b74ddee5c95dc6e852efd0f40ecdb93f8ea7460dc9a8` | `80/8044caf8d83a609f90e3b74ddee5c95dc6e852efd0f40ecdb93f8ea7460dc9a8.zip` | Last-Modified 2026-08-31 23:46:07 GMT; ETag `"2613a-65a6067f732b3"` |
| KSEI amended 2026 holiday notice | [PDF](https://web.ksei.co.id/files/1767843003_Penyesuaian_Pengumuman_Hari_Libur_dan_Cuti_Bersama_PT_KSEI_Ta....pdf) | 2026-09-24 15:26:49 | 200 / application/pdf | 1,036,672 | `ca444520365cb1d8a74eed4c0e9c72c5718333e9a2745b1002bf6256c454a96e` | `ca/ca444520365cb1d8a74eed4c0e9c72c5718333e9a2745b1002bf6256c454a96e.pdf` | Last-Modified 2026-01-08 06:58:41 GMT; ETag `"fd180-647daf0598cce"` |

The archive-only manifest parser version is `archive-only-1`. The KSEI text inspection version is `ksei-master-inspect-1` and produces research-only candidate records. Raw artifacts are not tracked in Git. HTTP headers were noted here; automated HTTP metadata capture is still a follow-up item.

### SECURITY MASTER — PARTIAL

The ZIP contains one pipe-delimited text member dated 2026-08-31 with 28 named columns. The local parser inspected 3,655 well-formed records and detected two malformed physical lines (633–634) that split one non-equity description. It identified 982 `Type=EQUITY` candidates, 538 structured warrants, 11 warrants, and other debt/crowdfunding classes. EQUITY is a provider category, **not yet proof that every row is an ordinary share**. There were no duplicate equity codes or ISINs within this one snapshot. Ninety-one EQUITY rows have a blank `Listing Date`, including BBRI; these remain unknown, never inferred or zero-filled.

`Isin Code` is a plausible security-level key, but stability through ticker changes, issuer mergers, delisting, and reissuance has not been tested. `Status=ACTIVE` denotes an undefined KSEI record status here; it must not be interpreted as an unsuspended, tradable share. `Sector` exists, but versioned classification meaning is unverified. No board/segment or ticker-history field was established. The file includes a snapshot population, not a demonstrated historical delisted universe. A strict canonical ingestion must reject/quarantine malformed records and unresolved required dates rather than silently repair them.

**Required now:** verified security identity, code, class, listing boundary, and current listed population. **Optional later:** sector, board, issuer linkage, delisting and symbol-event history. **Unavailable/unverified:** complete delisted population, historical symbol/board/sector states, security-type coverage for rights/ETFs, and explicit ongoing-use permission. Future all-market historical research is **BLOCKED** for survivorship bias until those gaps are resolved or explicitly scoped to surviving securities only.

### MARKET CALENDAR — PARTIAL

The KSEI notice dated 2026-01-08 amends an earlier 2025-10-01 notice and references IDX announcement Peng-00171/BEI.POP/09-2025. It lists 22 weekday closures for 2026, including 2026-01-01 and the exchange-specific 2026-12-31 closure. The notice says schedules may change with IDX or Bank Indonesia announcements. These are announced closures of KSEI operations; they are useful corroboration, not a full observed IDX trading-session ledger.

The Friday 2026-01-02 is treated in a test as an **explicit fixture** for a trading session; the holiday PDF itself does not prove the exchange actually traded that day. Saturday 2026-01-03 is a weekend. An unobserved weekday remains `UNKNOWN`, not automatically `TRADING`. The .NET calendar evidence store keeps duplicate evidence idempotent, returns dates in order, and rejects conflicting evidence pending a revision policy. This validates chronology behavior with fixtures but is not ingestion from the PDF. The PDF was archived but no production PDF parser was added. Exceptional exchange closures, instrument suspensions, and no-trade days are not supplied by this document.

### Separate gate assessment

| Gate | Security master | Market calendar |
|---|---|---|
| Direct access | Succeeded once | Succeeded once |
| Parser/normalizer | Partial; malformed rows and missing dates detected | PDF content reviewed; no automated source parser |
| Semantics | Instrument type and candidate ISIN understood partly | Announced weekday closures understood; actual sessions unproven |
| Rights for unattended personal/local automation | UNCLEAR | UNCLEAR |
| Historical adequacy | Monthly snapshot observed; delisted/ticker history unverified | One 2026 revision tested; earlier years and exceptional updates unverified |

## Request and maintenance cost

Measured transfer: 2 GETs, 1,192,634 bytes total; curl transfer times 0.221 and 0.463 seconds (0.684 seconds summed). Two HEAD size checks preceded them. There was also one failed KSEI disclaimer GET (HTTP 500). Search/browser research requests are not included in these transfer figures. These measurements cover only two small files; no sustainable cadence or long-term storage cost was measured. Maintaining a licensed feed would require monitoring file publication, column drift, amendments, identity changes, and exception notices.

## Outcome and next gate

Neither source is PASS. The immediate next step is to obtain written or clearly documented KSEI terms for personal automated downloading and archiving, plus an authorized IDX calendar/security-master route. Then validate historical snapshots, ticker/delisting events, and an actual observed trading-session source before canonical PostgreSQL ingestion. OHLCV remains a later experiment.

## Rights, identity, and actual-session follow-up (2026-09-24)

No additional market-data files were retrieved and the original spike measurements remain unchanged. This was manual source/policy discovery only. No collector or provider was adopted.

### KSEI rights

**Documented:** the public [master index](https://web.ksei.co.id/archive_download/master_securities) offers dated downloads; the [AKSes terms](https://akses.ksei.co.id/disclaimer) prohibit automated/systematic collection without written permission for the separate AKSes facility. [K-CASH terms](https://www.ksei.co.id/id/ketentuan-dan-kebijakan) and [website privacy policy](https://www.ksei.co.id/files/Kebijakan_Privasi_Website_KSEI-final.pdf) do not license the master archive. **Inferred:** the monthly presentation suggests a feasible prospective snapshot cadence, but does not authorize one or establish publication SLA. **Unknown:** personal unattended download, repeated retrieval, local raw/historical retention, redistribution, rate/frequency guidance, official API, old-archive completeness, and correction semantics. The archive disclaimer and robots guidance were not reliably retrievable. Rights remain **UNCLEAR**.

Written questions to KSEI (not sent):

1. May an individual, for noncommercial personal analysis without redistribution, automatically download the public `master_securities` files on a scheduled basis? Does this differ from manual download?
2. May original downloaded files and parsed records be retained locally indefinitely to reconstruct historical security identity? If not, what retention limit applies?
3. What download frequency, request-rate limit, access window, identification, or attribution does KSEI require? Is monthly retrieval after publication acceptable?
4. Is there an official API, bulk file, or machine-readable delivery route and publication/change notice for this dataset? Are corrections or replaced monthly files announced?
5. Are there restrictions on derived internal analytics or displays even with no redistribution? What additional permission would sharing raw files or derived data require?
6. Does the archive include 2022 onward and securities later delisted? Are effective dates, old/new ticker and ISIN mappings, type/board changes, and suspension status published elsewhere with permitted automated access?

### Historical identity and exchange sessions

[IDX listing activity](https://www.idx.co.id/id/perusahaan-tercatat/aktivitas-pencatatan) exposes new listing, delisting, and relisting categories, but its public page cannot be crawled under [IDX terms](https://www.idx.co.id/id/syarat-penggunaan). [KSEI announcements](https://web.ksei.co.id/publications/ksei-announcements) visibly include dated registration-cancellation and issuer-name-change examples; [ISIN announcements](https://web.ksei.co.id/publications/isin-announcements) provide dated identifier events. Neither page has demonstrated complete 2022-present ticker, type, board, delisting, or suspension chronology, and index publication date is not necessarily event effective date. The single 2026-08-31 master snapshot remains insufficient to reconstruct a point-in-time all-IDX universe. A current/delisted-population reconciliation and dated old/new code/ISIN event sample are still required.

[IDX holiday page](https://www.idx.co.id/id/tentang-bei/jadwal-libur-bursa) and the tested KSEI amended notice establish announced holidays, not observed exchange sessions. Historical annual schedules, all amendments, exceptional full-market closures, and a licensed actual-session evidence source remain unverified. Exchange `EXCHANGE_OPEN`, `EXCHANGE_HOLIDAY`, and `EXCEPTIONAL_CLOSURE` must be backed by dated evidence; instrument `TRADED`, `NO_TRADE`, `SUSPENDED`, and `MISSING_DATA` are separate. Absence of a price bar proves none of these states.

### Decision gates after follow-up

| Dataset | Access | Rights | Semantics | Historical depth | Automation | Result |
|---|---|---|---|---|---|---|
| Security master | PASS for one KSEI ZIP | UNCLEAR | PARTIAL; 91/982 EQUITY listing dates blank, status meaning unverified | BLOCKED for complete 2022 delisted/ticker history | Technically partial; rights-blocked | **PARTIAL** technical, **BLOCKED** canonical unattended use |
| Exchange calendar | PASS for one KSEI holiday PDF | UNCLEAR | PARTIAL, announced closures only | BLOCKED for observed 2022-present sessions/exceptions | Rights-blocked; no source parser | **PARTIAL** holiday research, **BLOCKED** actual-session ledger |

A forward-running EOD system beginning with new licensed/authorized collection is **PARTIAL** today: prospective snapshots and holiday evidence look technically feasible, but rights and observed-session/identity semantics still block unattended canonical operation. Historical all-IDX research back to about 2022 is **BLOCKED**: no permitted complete delisted universe, dated identity-change ledger, or actual-session chronology has been established. This does not make the older backfill a prerequisite for a narrower forward-only launch.

[IDX Data Services](https://data.idx.co.id/) offers licensed EOD/historical categories; its [2026 catalogue](https://www.idx.id/media/auobyarx/idx-catalogue-pricelist-updated-2026.pdf) lists corporate actions and suspension/unsuspension reports, but does not prove a complete point-in-time security master or individual affordability. [TradingHours XIDX](https://www.tradinghours.com/markets/idx) advertises licensed historical/irregular calendars via [API or files](https://www.tradinghours.com/data), pending XIDX coverage and personal-use contract verification. [OpenFIGI](https://www.openfigi.com/api/documentation) offers free identifier mapping and its [allocation rules](https://www.openfigi.com/docs/figi-allocation-rules.pdf) describe FIGI continuity through ticker changes, but neither historical IDX coverage nor a delisted-universe feed was demonstrated. These are candidates or reconciliation sources only.

**Next bounded experiment:** obtain written KSEI rights clarification and ask IDX Data Services for individual eligibility, 2022-present point-in-time security/event and actual-session specifications, sample records with publication/effective dates, retention terms, and total cost. Separately request an XIDX 2022-present coverage/exception sample and license quote from TradingHours. Do no unattended collection while rights remain unclear. The next research/contact task fits **MEDIUM** reasoning; implementing validated as-of identity/replay later may need HIGH.

## OHLCV and IHSG feasibility screen (2026-09-24)

This follow-up is source/rights/semantics research only. No new market-data GET, API-key registration, collector, technical indicator, or 2022 backfill occurred. The earlier raw-artifact evidence and its counts remain unchanged. The detailed candidate gate matrix is in [DATA_SOURCE_MATRIX.md](DATA_SOURCE_MATRIX.md).

### Rights and source distinction

[IDX website terms](https://www.idx.co.id/id/syarat-penggunaan) still prohibit scraping/crawling its public pages. The separate [IDX Data Services](https://data.idx.co.id/) route is licensed, not automatically free for personal use. Its [2026 catalogue](https://www.idx.id/media/auobyarx/idx-catalogue-pricelist-updated-2026.pdf) advertises Equity EoD and EoD Indices products, but the Professional equity product lists high/low/close/volume/value/frequency **without open**; Basic has less. The Indices product also omits open. Ask IDX for a personal-use contract, feed specification, complete OHLC/turnover fields, retention, history, and costs before testing.

[yfinance](https://github.com/ranaroussi/yfinance) is Apache-licensed software, not a Yahoo market-data license. Yahoo publishes [BBCA.JK](https://finance.yahoo.com/quote/BBCA.JK/), [GOTO.JK](https://finance.yahoo.com/quote/GOTO.JK/), and [IDX COMPOSITE `^JKSE`](https://finance.yahoo.com/quote/%5EJKSE/), but [Yahoo terms](https://legal.yahoo.com/in/en/yahoo/terms/otos/index.html) prohibit automated collection without express prior permission. No Yahoo/yfinance retrieval was made; personal viewing does not clear unattended collection or raw retention.

[EODHD terms](https://eodhd.com/financial-apis/terms-conditions) explicitly permit registered nonprofessional users to store, manipulate, and analyze data privately for noncommercial personal investment, while forbidding redistribution/display to others. This makes it the leading **test candidate**, not an approved canonical source. [EODHD JK listing](https://eodhd.com/exchange/JK) establishes provider-side IDX symbols. The free plan is limited to one year/20 calls per day; [paid All World EOD](https://eodhd.com/pricing-quantpedia) is advertised at US$19.99/month. An API token/account is required and none was available in scope. The direct EOD endpoint and whole-exchange bulk endpoint are documented, but no JK response was retrieved.

[Twelve Data](https://twelvedata.com/exchanges) lists XIDX EOD only at Pro level; [individual pricing](https://twelvedata.com/pricing) lists US$229/month monthly, so it is not a zero-cost route. Its [terms](https://twelvedata.com/terms) permit internal processing/storage during permitted subscription periods, restrict caching and third-party data, and require deletion within 30 days after termination; this conflicts with an assumed indefinite raw archive unless clarified. [Alpha Vantage](https://www.alphavantage.co/documentation/) has a global daily API but no verified IDX symbol here and [25 free requests/day](https://www.alphavantage.co/support/); [marketstack](https://marketstack.com/pricing) has 100 free requests/month and no verified XIDX symbol here. Neither supports an approximately full-universe free EOD plan on current evidence. No provider-origin Stooq IDX coverage or suitable rights were found.

### Stock and IHSG semantics

[EODHD EOD documentation](https://eodhd.com/financial-apis/api-for-historical-data-and-volumes) describes date, OHLC as traded, adjusted close adjusted for splits/dividends, and volume adjusted for splits. [Its bulk documentation](https://eodhd.com/financial-apis/bulk-api-eod-splits-dividends) labels volume as shares traded. This apparent split-adjusted-versus-raw ambiguity must be resolved on JK split dates; neither label proves exact historical exchange-tape equivalence. EOD row has no value/frequency; regular versus negotiated/cash market inclusion, rights-issue treatment, correction/revision notices, and JK volume units are not established. The API returns `YYYY-MM-DD` trading date; exchange timezone/actual session mapping still needs verification. Adjusted closes may be recomputed after new dividends, so raw responses must be versioned rather than silently overwritten.

[Twelve Data support](https://support.twelvedata.com/en/articles/5179064-are-the-prices-adjusted) says daily prices are split-adjusted; dividends can be adjusted client-side using separate endpoints. This is not a raw-as-traded series. IDX-specific rights issues, volume units, turnover, board scope, corrections, and IHSG symbol remain unverified.

[IDX defines IHSG](https://www.idx.co.id/id/produk/indeks) as a price-performance index, not a dividend-total-return index. Its [Daily IDX Indices](https://www.idx.co.id/id/data-pasar/laporan-statistik/digital-statistic/monthly/stock-price-index/daily-idx-indices) publishes daily Composite Index closes visible for **manual** spot checks, not an authorized automated feed. Yahoo's `^JKSE` display establishes an identifier only, while Yahoo automation is blocked. EODHD/Twelve Data IHSG API identifiers, daily history, revisions, session alignment, and any index adjustment behavior are unverified. The IDX licensed Indices product is a candidate, but its catalogue does not list open. Stock OHLCV suitability must not be transferred to IHSG.

### Future adverse-case panel (design only)

- Split: [PBID 1:4, new nominal trading 31 May 2024](https://www.ksei.co.id/Announcement/Files/PBID_MCONV_20240603_ENG.pdf). Compare pre/post ex-date OHLC and volume in raw and adjusted variants.
- Reverse split: [NETV 2 old shares to 1 new share, October 2024](https://www.ksei.co.id/Announcement/Files/172783_ksei_24744_jku_1024_202410181515.pdf). Check adjustment factor, effective session, and odd-lot handling.
- Cash dividend: [BBCA March 2025](https://www.ksei.co.id/Announcement/Files/BBCA_DIV_20250324_ENG.pdf). Compare unadjusted close and adjusted close around ex-date.
- Rights issue: [MINA July 2025](https://web.ksei.co.id/Announcement/Files/MINA_RIGHT_20250710_ID.pdf). Check whether vendor back-adjusts price/volume, including later revisions; do not assume it does.
- IPO boundary: [GOTO first listed 11 April 2022](https://www.gotocompany.com/news/press/goto-tercatat-di-papan-utama-bei). Absence before listing is not a gap.
- Suspension: choose a security and exact dates from an authorized suspension/unsuspension source before testing; no candidate was verified in this screen. Never create a synthetic zero-volume bar.

### Request-budget and retrieval decision

The prior KSEI snapshot contained **982 EQUITY candidates**, not a proven eligible ordinary-share universe; it is only a sizing proxy. At approximately 982 names/session, EODHD's per-symbol EOD route would need approximately 982 HTTP requests and 982 calls, or its [whole-exchange bulk route](https://eodhd.com/financial-apis/bulk-api-eod-splits-dividends) one request costing 100 calls, plus a separate IHSG request if supported. [Published limits](https://eodhd.com/financial-apis/api-limits) are 20 calls/day free, at least 100,000 paid calls/day, and 1,000 HTTP requests/minute. Thus free cannot run the full panel even via bulk; paid quota is arithmetically sufficient, but JK bulk completeness, actual bytes, runtime, EOD publication lag, and corrections have **not** been measured. Twelve Data charges one credit per symbol even in a batch: approximately 982 credits/session; XIDX requires Pro and its quoted 610+ credits/minute implies at least two minute windows at the lowest Pro tier, before retry/overhead. Alpha Vantage free 25/day and marketstack free 100/month are insufficient for one full daily universe. Do not extrapolate measured transfer bytes or runtime from unrelated venues.

The tiny BBCA/ANTM/GOTO/IHSG retrieval was **not performed**: Yahoo automation lacks permission; IDX requires a license; Twelve Data XIDX is paid; EODHD requires a registered API key not available for this task and IHSG symbol is unverified. Therefore there are no new raw bytes/hashes to archive, no observed schema or 20–30-session panel, and no independent OHLCV/turnover discrepancy comparison. This is an explicit unresolved experiment, not a passed gate. Manual IDX index-close examples can be used for future IHSG reconciliation, but not as automated collection or as evidence of stock-bar agreement.

### Decision

Prospective stock EOD: **PARTIAL**. EODHD is the primary *experimental* candidate for private collection; its rights and request arithmetic are promising, but exact JK bar semantics, actual API quality, independent reconciliation, source revisions, and ten-session unattended operation are unproven. IHSG: **PARTIAL discovery / BLOCKED for unattended canonical use** until an authorized identifier/route and session alignment are confirmed. 2022-present backfill: **BLOCKED** as an all-IDX as-of research universe, due both price-source history not tested and the pre-existing historical identity/calendar blockers; these do not prevent a narrower prospective experiment.

Next bounded step: obtain a personal EODHD key outside the repository (or an explicitly licensed IDX test feed), verify terms/retention and IHSG code, then retrieve only roughly 20–30 recent sessions for BBCA/ANTM/GOTO and IHSG into the existing raw-artifact mechanism. Compare at least O/H/L/C/volume against a genuinely independent authorized source, and probe the documented adverse corporate-action dates later in a separate bounded experiment. No production collector or canonical schema change is warranted yet. The next source/semantics trial needs **MEDIUM** reasoning; any corporate-action accounting or point-in-time replay implementation needs **HIGH**.
