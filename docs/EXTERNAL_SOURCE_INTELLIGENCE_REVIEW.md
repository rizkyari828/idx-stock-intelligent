# External Source Intelligence & Free-First Data Architecture Review V0.1

Research date: **2026-10-08, Asia/Jakarta**. Strategy/source review only.

## 1. Decision and scope

**B — EXTERNAL_DISCOVERIES_IMPROVE_RESEARCH_BUT_NOT_OPERATIONAL_FEASIBILITY.**

The three supplied repositories reveal useful IDX internal routes and Stockbit
field mappings. They do **not** materially close Level-1 admission gates. Five
additional repositories were deeply inspected out of nine screened, including the
user's subsequent `nichsedge/idx-bei` request. Several improve the research design:
`idx-bei` adds a publication-bearing disclosure index and a broader capital-action
catalogue; `idxlens` and `idx-mcp` demonstrate structured filing extraction;
`stockbit-mcp` exposes financial-period and currency clues beyond the supplied
fundamental project. None establishes a stronger **admissible, free, unattended**
operational stack.

Phase-2 parsing/discovery feasibility improves materially at the **concept** level.
End-to-end free operational feasibility remains unproved. A conditional preferred
concept is an official disclosure index plus official filing attachments, with
XBRL first and validated XLSX second. This refines the existing official-facts
architecture; it does not justify treating Stockbit as authoritative or changing
our application stack. An endpoint's existence, a repository's HTTP-200 claim,
and permission to operate it are three different things.

- EODHD role: **EODHD_DECISION_STILL_PENDING**.
- Stockbit role: **ROLE D — NOT_OPERATIONALLY_SUITABLE** under current requirements.
- **No IMPLEMENTATION_CANDIDATE is admitted by this review.**
- Zero mandatory monetary cost remains the target. No paid product is justified
  merely because these free routes remain blocked; this bounded search does not
  prove every practical free alternative impossible.
- No architecture/runbook edit: the existing source strategy and immediate gate
  remain valid. Frozen V0.2 semantics, FullIdx gating and soak remain unchanged.

### Actual initial repository state

`main`, HEAD `3fa99cb0993f85cc88cd7906b8c86e0f0b98145f`, clean working tree,
15 ahead / 0 behind the locally recorded `origin/main`. No fetch was performed.
The prompt's `a192055...` baseline had already advanced through the EODHD JK
semantics review. That newer work was preserved.

### Existing architecture and governing evidence

Retain .NET 10 canonical logic, PostgreSQL canonical history, existing Python
acquisition boundaries, immutable originals, typed evidence/revisions, PIT/as-of
readers, readiness, technical evaluation, candidate capture and Outcome verification.
No Deno/SQLite/Sheets/Go/Python-only application architecture is proposed.

Read alongside [source stack](LEVEL1_SCREENER_SOURCE_STACK.md),
[automated architecture, especially 5A.2e and 5A.2f](LEVEL1_AUTOMATED_SOURCE_ARCHITECTURE.md),
[V0.2 contract](SCREENER_EVIDENCE_V0_2_CONTRACT.md),
[persisted binding](SCREENER_EVIDENCE_V0_2_PERSISTED_BINDING.md) and
[runbook](PRODUCT_SLICE_RUNBOOK.md). This review does not amend those contracts.

The new evidence narrows any earlier reading of “no free JSON route” to
**no verified permitted free operational route**. Repository code clearly contains
internal JSON routes; it does not prove an authorized public API or complete
evidence coverage. This factual refinement does not open the current gate.

## 2. Evidence method and bounded search

Labels used below:

- **R — repository implementation/claim:** pinned code was read; behavior described
  is static inspection, not execution. README/specimen assertions remain R.
- **P — provider documentation:** first-party pages independently opened during
  this review. A public page does not grant automation or validate a hidden API.
- **I — inference/recommendation:** a proposed use or conclusion from R/P, with
  unresolved premises stated. No market facts are admitted from this document.

All three required repositories and five additional shortlisted repositories had
actual source read. Additional discovery searched IDX/BEI acquisition, security
master/status/calendar/actions, Stockbit/flow, and filing/XBRL parsers. Repeated
wrappers, UI/tutorial projects and static/manual datasets were deprioritized.
Nine additional repositories were screened, five shortlisted: within the maximum
ten/five. The user-requested extension continued the existing review.

| Additional repository | Depth | Reason / result |
|---|---|---|
| [lugassawan/idxlens](https://github.com/lugassawan/idxlens) | Deep | Filing discovery, XBRL/XLSX extraction, cache and format selection; acquisition excluded |
| [lovanto/idx-mcp](https://github.com/lovanto/idx-mcp) | Deep | IDX taxonomy/context parsing; acquisition excluded |
| [INo-xious/stockbit-mcp](https://github.com/INo-xious/stockbit-mcp) | Deep | Broader financial and corporate-action routes; no permission/PIT closure |
| [b2b-web-id/IDX-XBRL-exploreR](https://github.com/b2b-web-id/IDX-XBRL-exploreR) | Deep | Older but relevant fact/context joins; manual acquisition |
| [nichsedge/idx-bei](https://github.com/nichsedge/idx-bei) | Deep, user-added | Disclosure index, capital-event types and source verification spec |
| [pwguler/stockbit-cli](https://github.com/pwguler/stockbit-cli) | Screen only | Similar Stockbit/Yahoo routes and advertised browser/TLS impersonation; no stronger admission case |
| [mpandudc/idx-market-mcp](https://github.com/mpandudc/idx-market-mcp) | Screen only | Market/flow wrapper breadth, no demonstrated stronger rights or PIT provenance |
| [Danieldd28/idx-indonesia-stock-exchange](https://github.com/Danieldd28/idx-indonesia-stock-exchange) | Screen only | Advertised Cloudflare bypass and duplicate IDX source catalogue |
| [nelsonjambi/idx-report-extractor](https://github.com/nelsonjambi/idx-report-extractor) | Screen only | Local PDF extraction, not an established unattended acquisition feed |

Screen-only dispositions are based on the public repository page/description;
they are not implementation audits. Full review cards below cover all eight
reviewed repositories: the three required ones plus the five additional
shortlisted ones.

## 3. Pinned repository review cards

Dates are Git commit dates, not provider data freshness. “Material activity” is
the latest relevant code change found in the inspected history, not a guarantee
of ongoing maintenance. Code licences below grant no upstream data rights.

### R1 — NeaByteLab/IDX-UI

Pinned commit: [`28cbf7a4ce35d15e0f1ad5537f7394a643c316bc`](https://github.com/NeaByteLab/IDX-UI/tree/28cbf7a4ce35d15e0f1ad5537f7394a643c316bc).
Implementation anchors: [client](https://github.com/NeaByteLab/IDX-UI/blob/28cbf7a4ce35d15e0f1ad5537f7394a643c316bc/src/server/services/Client.ts),
[screener](https://github.com/NeaByteLab/IDX-UI/blob/28cbf7a4ce35d15e0f1ad5537f7394a643c316bc/src/server/services/Screener.ts),
[summary](https://github.com/NeaByteLab/IDX-UI/blob/28cbf7a4ce35d15e0f1ad5537f7394a643c316bc/src/server/services/Summary.ts).
Also inspected Fetcher, Bootstrap, Date and server startup/cron code.

| Review field | Finding |
|---|---|
| REPOSITORY | NeaByteLab/IDX-UI |
| LAST MATERIAL ACTIVITY | 2026-07-07 router migration/refactor; tip is documentation, latest service change formatting; March 15 changed day-range fetching |
| LANGUAGE / STACK | TypeScript, Deno, React, Drizzle/SQLite; code licence MIT |
| UPSTREAM DATA SOURCE | IDX website internal JSON |
| AUTOMATION METHOD | Browser-like headers/cookies; startup fetch and hourly cron; all-stock summary by date |
| AUTHENTICATION | Website session cookies, not a documented public API key |
| MANUAL DEPENDENCY | Intended unattended fetching; challenge/session recovery is not an assured supported route |
| FREE / PAID | No monetary charge shown in code; free operational permission not established |
| IMPORTANT ENDPOINTS | Stock screener `get`; `GetStockSummary?date=YYYYMMDD` |
| IMPORTANT FIELDS | status, corpAction, corpActionDate, notation, umaDate, Remarks, OHLC, Volume/Value/Frequency, Bid/Offer and quantities, ForeignBuy/Sell, ListedShares, raw `TradebleShares` |
| HISTORICAL SUPPORT | Bootstrap loops two years of calendar dates; current screener is not historical reference evidence |
| CORRECTION / REVISION HANDLING | Upsert by code or code/date overwrites; recent overlap can find changes but preserves no immutable revision chain |
| USEFUL CONCEPT | One shared daily artifact can cover the universe; bounded recent overlap |
| PERMISSION / TERMS RISK | IDX website automation restriction; cookie and undocumented schema dependency |
| LEVEL-1 RELEVANCE | Broad observation and metadata clues, no independent status/session admission |
| PHASE-2 RELEVANCE | Current normalized screener metrics only |

Classification: **RESEARCH_LEAD_ONLY**.

R: the client visits the homepage, obtains cookies, checks an index route and
clears cookies on 403. The scheduled range includes offsets -5 through +2,
including future dates. Bootstrap requests calendar dates with delays/retries;
there is no authoritative session enumeration. The summary date can fall back
to the requested date and empty results simply return. Host-local date handling
is not an explicit Asia/Jakarta convention. None is suitable evidence of a
holiday, a completed session, or a positive trading status.

### R2 — NeaByteLab/IDX-API

Pinned commit: [`910b8db70893b93920a1bba331d00a1a245907c6`](https://github.com/NeaByteLab/IDX-API/tree/910b8db70893b93920a1bba331d00a1a245907c6).
Implementation anchors: [Trading](https://github.com/NeaByteLab/IDX-API/blob/910b8db70893b93920a1bba331d00a1a245907c6/src/Trading/index.ts),
[Company](https://github.com/NeaByteLab/IDX-API/blob/910b8db70893b93920a1bba331d00a1a245907c6/src/Company/index.ts),
[Market](https://github.com/NeaByteLab/IDX-API/blob/910b8db70893b93920a1bba331d00a1a245907c6/src/Market/index.ts),
[client](https://github.com/NeaByteLab/IDX-API/blob/910b8db70893b93920a1bba331d00a1a245907c6/src/Client.ts).
Types, Backend/Sync modules and Cron were also inspected.

| Review field | Finding |
|---|---|
| REPOSITORY | NeaByteLab/IDX-API |
| LAST MATERIAL ACTIVITY | 2026-03-10 HTTP API/cron sync (`89494b74...`); March 28 tip documentation |
| LANGUAGE / STACK | TypeScript/Deno, Drizzle/SQLite; MIT |
| UPSTREAM DATA SOURCE | IDX website internal JSON/statistics routes |
| AUTOMATION METHOD | HTTP with browser headers/cookies; daily/monthly/company batch synchronization |
| AUTHENTICATION | Website session, no independently documented open API credential |
| MANUAL DEPENDENCY | Intended automatic batch; unsupported session lifecycle remains fragile |
| FREE / PAID | Public-site access assumed; no evidenced free automation grant |
| IMPORTANT ENDPOINTS | Full catalogue in section 4, including summary, calendar, status notices, financial reports, actions and statistics |
| IMPORTANT FIELDS | BoardCode, DTCreate, Status, Remarks, DelistingDate, nonregular quantities, share counts, prevValueCA, dilution |
| HISTORICAL SUPPORT | Date/month/year parameters and some paginated history; Cron sample hardcodes 2026-01-01 to 2026-03-10 |
| CORRECTION / REVISION HANDLING | Upserts replace code/date or code/year/period records; no immutable publication/revision lineage |
| USEFUL CONCEPT | Best supplied endpoint-discovery catalogue; shared bulk observations and explicit field mapping |
| PERMISSION / TERMS RISK | IDX website restrictions; wrapper names can imply semantics absent upstream |
| LEVEL-1 RELEVANCE | Many exact research targets; no completed eligibility/control package |
| PHASE-2 RELEVANCE | Financial-report attachment discovery and normalized ratio clues |

Classification: **RESEARCH_LEAD_ONLY**.

R: `previousAdjusted` is assigned `item.Previous`, the same input as `previous`.
It is not an independently supplied adjustment field. `DTCreate` is renamed
`updatedAt`, with no documented finality semantics. Raw summary `DelistingDate`
is declared but dropped by the mapper. The relisting mapper derives
`recordsTotal` from the returned array length. These local transformations must
not be treated as exchange guarantees. Cron catches errors and continues; an
apparently finished batch is not a complete evidence checkpoint.

### R3 — noczero/idx-fundamental-analysis

Pinned commit: [`038a44c9f247e13f2e43ff3bca266bdc4be03890`](https://github.com/noczero/idx-fundamental-analysis/tree/038a44c9f247e13f2e43ff3bca266bdc4be03890).
Implementation anchors: [Stockbit provider](https://github.com/noczero/idx-fundamental-analysis/blob/038a44c9f247e13f2e43ff3bca266bdc4be03890/providers/stockbit.py),
[API client](https://github.com/noczero/idx-fundamental-analysis/blob/038a44c9f247e13f2e43ff3bca266bdc4be03890/services/stockbit_api_client.py),
[token acquisition](https://github.com/noczero/idx-fundamental-analysis/blob/038a44c9f247e13f2e43ff3bca266bdc4be03890/services/stockbit_token_fetcher.py),
[parser helpers](https://github.com/noczero/idx-fundamental-analysis/blob/038a44c9f247e13f2e43ff3bca266bdc4be03890/utils/helpers.py).
IDX/YFinance providers, fundamental schema and persistence models also inspected.

| Review field | Finding |
|---|---|
| REPOSITORY | noczero/idx-fundamental-analysis |
| LAST MATERIAL ACTIVITY | 2026-07-30 token stripping, explicit reauthentication error and Camoufox provider changes |
| LANGUAGE / STACK | Python, requests/pandas/Camoufox/yfinance, SQLAlchemy, spreadsheet integrations; no code licence identified in inspected files |
| UPSTREAM DATA SOURCE | IDX DOM, authenticated Stockbit Exodus, Yahoo via yfinance |
| AUTOMATION METHOD | Stealth browser for IDX; tokenized HTTP for Stockbit; local browser token bootstrap |
| AUTHENTICATION | Access/refresh token pair, persisted user agent; refresh endpoint and interactive login fallback |
| MANUAL DEPENDENCY | Initial interactive login and later browser re-bootstrap on failed/revoked refresh; server path raises StockbitReauthRequiredError |
| FREE / PAID | Feature/account entitlement unverified; no unconditional free API grant found |
| IMPORTANT ENDPOINTS | `/keystats/ratio/v1/{ticker}?year_limit=10`; `/company-price-feed/v2/orderbook/companies/{ticker}`; POST `/stream/v3/symbol/{ticker}` and pinned stream; `/login/refresh` |
| IMPORTANT FIELDS | Broad ratios, current/TTM financials, latest market feed, stream created_at |
| HISTORICAL SUPPORT | year_limit parameter is not proof of a retained ten-year fact series; yfinance provider requests only period=1d |
| CORRECTION / REVISION HANDLING | Current metric model and local timestamps; no demonstrated original-filing/restatement chain |
| USEFUL CONCEPT | Metric inventory and separation of provider acquisition; token refresh failure is made explicit |
| PERMISSION / TERMS RISK | Stockbit consent requirement; browser/credential bootstrap; IDX stealth acquisition excluded |
| LEVEL-1 RELEVANCE | Latest observations only; cannot substitute for independent controls or 50-session evidence |
| PHASE-2 RELEVANCE | Rich secondary metric catalogue, insufficient authoritative provenance |

Classification: **REJECT_FOR_PRODUCTION** for the inspected pipeline.

R: key statistics use positional `closure_fin_items_results` and nested indexes.
Missing indexes, blanks and hyphens can become zero. Parentheses are removed
without preserving accounting negatives; M/B values become floats. These are
specific reasons not to copy the parser. The IDX DOM mapper labels a shares cell
`market_cap`, illustrating why field names cannot authenticate units. Stream
`created_at` becomes `posted_at`; that is not financial-fact publication time.
No credentials, tokens or browser profiles were accessed in this review.

### R4 — lugassawan/idxlens

Pinned commit: [`116501eaa0d9e5e8358fbb4a911e530ba9015e3a`](https://github.com/lugassawan/idxlens/tree/116501eaa0d9e5e8358fbb4a911e530ba9015e3a).
Implementation anchors: [filing discovery](https://github.com/lugassawan/idxlens/blob/116501eaa0d9e5e8358fbb4a911e530ba9015e3a/internal/idx/financial_report.go),
[format selection](https://github.com/lugassawan/idxlens/blob/116501eaa0d9e5e8358fbb4a911e530ba9015e3a/internal/cli/analyze.go),
[XBRL](https://github.com/lugassawan/idxlens/blob/116501eaa0d9e5e8358fbb4a911e530ba9015e3a/internal/xbrl/reader.go),
[XLSX](https://github.com/lugassawan/idxlens/blob/116501eaa0d9e5e8358fbb4a911e530ba9015e3a/internal/xlsx/reader.go),
[registry/ETag](https://github.com/lugassawan/idxlens/blob/116501eaa0d9e5e8358fbb4a911e530ba9015e3a/internal/idx/registry.go).
Auth/client, downloader and service extractor were also read.

| Review field | Finding |
|---|---|
| REPOSITORY | lugassawan/idxlens |
| LAST MATERIAL ACTIVITY | 2026-03-22 helper refactoring at inspected tip |
| LANGUAGE / STACK | Go CLI, chromedp, excelize, PDF/layout and XML parsing; Apache-2.0 |
| UPSTREAM DATA SOURCE | IDX financial-report attachments; GitHub-maintained issuer presentation registry |
| AUTOMATION METHOD | Ticker/year/period report discovery then downloads; local extraction |
| AUTHENTICATION | Chromium acquisition of cf_clearance cookies; browser automation concealment flags |
| MANUAL DEPENDENCY | Browser/session recovery unresolved; existing local files can mask failed fresh acquisition |
| FREE / PAID | Code/local parsing free; permitted free IDX machine retrieval not established |
| IMPORTANT ENDPOINTS | `/primary/ListedCompany/GetFinancialReport` with kodeEmiten/year/periode/reportType/range; attachment File_Path |
| IMPORTANT FIELDS | File_Name/Path/Type/Size, Emiten_Code, Report_Year/Period; XBRL concept/context/unit; spreadsheet labels/headers |
| HISTORICAL SUPPORT | Explicit year/period lookup, filename metadata and fallback request metadata; no complete period/revision index demonstrated |
| CORRECTION / REVISION HANDLING | Atomic replacement of named local files, not immutable versions; registry ETag is unrelated to IDX filing revision identity |
| USEFUL CONCEPT | Official attachment → retained original → structured parser; format-aware extraction |
| PERMISSION / TERMS RISK | Current cookie acquisition excluded; attachment visibility alone is not an automation grant |
| LEVEL-1 RELEVANCE | Limited; potential share/action cross-checks from reports, not daily eligibility |
| PHASE-2 RELEVANCE | Strong parsing/discovery concept; actual parser is not a complete financial admission engine |

Classification: **RESEARCH_LEAD_ONLY**.

R: `ListReports` requests `indexFrom=0,pageSize=1000`; it flattens attachments
without proving complete pagination. Downloads use temporary files/rename, which
prevents partial writes but can replace a previous filing at the same filename.
`analyze` selects **XBRL > XLSX > PDF**; PDF switches to **presentation** extraction.
The financial PDF extractor explicitly returns unsupported. Therefore this is
not a proven generic PDF-financial fallback. Failed fetching can reuse local
files without establishing freshness.

R: ZIP parsing finds `.xbrl/.xml/.htm/.html`; inline facts retain context/unit
references, but these references are not a full resolved period/unit/dimension
model. The inspected fact recognizer is not general support for standalone
IDX-specific concepts. XLSX parsing assumes first-row column headings and
first-column labels, parses floats, and skips unparsable cells. Filename/request
ticker/year/period is useful routing metadata, not proof of accounting period,
consolidation, publication or currency. Use the concept, not these shortcuts.

R: ETag/If-None-Match/304 handling is for
`raw.githubusercontent.com/lugassawan/idxlens/main/registry/presentations.json`.
It is **not evidence of ETag support on IDX financial-report indexes or files**.
The presentation registry is curated external metadata, not an authoritative
complete issuer disclosure watcher. Conditional HTTP can reduce load if an
authorized upstream supports it; it cannot prove event completeness.

### R5 — lovanto/idx-mcp

Pinned commit: [`42638e7cfa4209ab501c114c151532668b2ec42f`](https://github.com/lovanto/idx-mcp/tree/42638e7cfa4209ab501c114c151532668b2ec42f).
Read [financial retrieval](https://github.com/lovanto/idx-mcp/blob/42638e7cfa4209ab501c114c151532668b2ec42f/internal/idx/financial.go),
[XBRL parser](https://github.com/lovanto/idx-mcp/blob/42638e7cfa4209ab501c114c151532668b2ec42f/internal/xbrl/xbrl.go)
and internal/fetcher/fetcher.go.

| Review field | Finding |
|---|---|
| REPOSITORY | lovanto/idx-mcp |
| LAST MATERIAL ACTIVITY | 2026-07-05 index constituents (`466deece...`), same-day merge tip |
| LANGUAGE / STACK | Go/MCP, XML parsing; MIT |
| UPSTREAM DATA SOURCE | IDX GetFinancialReport and XBRL attachments |
| AUTOMATION METHOD | HTTP retrieval, ZIP/instance parsing and caching |
| AUTHENTICATION | TLS/browser impersonation aimed at WAF avoidance, including 403 retry |
| MANUAL DEPENDENCY | No routine manual file step intended; prohibited acquisition technique remains disqualifying |
| FREE / PAID | No direct fee shown; permitted zero-cost source rights unresolved |
| IMPORTANT ENDPOINTS | GetFinancialReport, discovered instance.zip/instance.xbrl |
| IMPORTANT FIELDS | IDX/DEI taxonomy concepts, context instant/start/end; selected financial accounts |
| HISTORICAL SUPPORT | Ticker/year/period lookup and comparative contexts |
| CORRECTION / REVISION HANDLING | Financial ZIP cache keyed code/year/period and cached forever; restatements can be missed |
| USEFUL CONCEPT | Resolve filing concepts against period contexts, not positional metric arrays |
| PERMISSION / TERMS RISK | WAF-avoidance implementation excluded; no data permission inherited from MIT |
| LEVEL-1 RELEVANCE | Secondary fundamentals/share context only |
| PHASE-2 RELEVANCE | Stronger IDX namespace/context example, incomplete financial fact coverage |

Classification: **RESEARCH_LEAD_ONLY**.

R: the small selected account set does not supply every required cash-flow/FCF
fact. Excluding dimensional contexts is not a general rule for choosing
consolidated statements. A locally named numeric-IDR field does not replace
reading the filing's unitRef. The parser does not establish the full currency,
scale, audit identity, publication and revision contract.

### R6 — INo-xious/stockbit-mcp

Pinned commit: [`39bc146030aa33bb75bfc964b3d83673b04b2e20`](https://github.com/INo-xious/stockbit-mcp/tree/39bc146030aa33bb75bfc964b3d83673b04b2e20).
Read [financial statements](https://github.com/INo-xious/stockbit-mcp/blob/39bc146030aa33bb75bfc964b3d83673b04b2e20/src/core/financial.ts),
[corporate actions](https://github.com/INo-xious/stockbit-mcp/blob/39bc146030aa33bb75bfc964b3d83673b04b2e20/src/core/corpaction.ts),
keystats, HTTP client and auth/session code. Trading/account tools were not run.

| Review field | Finding |
|---|---|
| REPOSITORY | INo-xious/stockbit-mcp |
| LAST MATERIAL ACTIVITY | Source change 2026-09-25 (`34091338...`); September 28 dependency-merge tip |
| LANGUAGE / STACK | TypeScript/Node/MCP, schema validation; MIT |
| UPSTREAM DATA SOURCE | Stockbit authenticated internal API |
| AUTOMATION METHOD | Tokenized HTTP, bounded ranges and caches |
| AUTHENTICATION | Access/refresh tokens; revoked session requires login again |
| MANUAL DEPENDENCY | OCCASIONAL_MANUAL_REAUTH failure path; frequency not established |
| FREE / PAID | Actual feature entitlement unverified; free API permission not demonstrated |
| IMPORTANT ENDPOINTS | `/findata-view/company/financial`; `/corpaction/{actionType}`, `/corpaction?date=`, `/corpaction/status`, stock_conversion |
| IMPORTANT FIELDS | data_tables, currency/default_currency, rounding_value; CA type/date/status structures |
| HISTORICAL SUPPORT | Financial statement/report selectors; CA date iteration has a 31-day local bound |
| CORRECTION / REVISION HANDLING | Cache/response normalization, not an authoritative original-filing version chain |
| USEFUL CONCEPT | Explicit unknown-shape/truncation handling and broader statement metadata than R3 |
| PERMISSION / TERMS RISK | Stockbit consent requirement; undocumented authenticated API |
| LEVEL-1 RELEVANCE | Action/market cross-check research only |
| PHASE-2 RELEVANCE | Refines provenance questions; prevents incorrectly claiming Stockbit exposes no period/currency fields |

Classification: **RESEARCH_LEAD_ONLY**; this is not operational authorization.

R: statement queries carry data_type/report_type/statement_type selectors.
The wrapper preserves currency and rounding metadata but removes html_report;
its omission cannot prove that upstream never supplies provenance. CA comments
include repository-reported observations and older unverified notes. They are
not independent provider specifications or live results from this review.

### R7 — b2b-web-id/IDX-XBRL-exploreR

Pinned commit: [`6d9652a2d2add83d8de424d761b1e92acc9bc8c5`](https://github.com/b2b-web-id/IDX-XBRL-exploreR/tree/6d9652a2d2add83d8de424d761b1e92acc9bc8c5).
Read [BBRI example](https://github.com/b2b-web-id/IDX-XBRL-exploreR/blob/6d9652a2d2add83d8de424d761b1e92acc9bc8c5/instance/BBRI/BBRI.R) and README.

| Review field | Finding |
|---|---|
| REPOSITORY | b2b-web-id/IDX-XBRL-exploreR |
| LAST MATERIAL ACTIVITY | 2021-10-27 BBRI 2020 sample (`a9fdab7f...`); tip gitignore update |
| LANGUAGE / STACK | R, XBRL and dplyr; no code licence identified in inspected files |
| UPSTREAM DATA SOURCE | Manually obtained IDX XBRL/local exported tables |
| AUTOMATION METHOD | Local fact/element/context/label joins; extraction invocation partly commented out |
| AUTHENTICATION | None for local analysis; acquisition authorization not established |
| MANUAL DEPENDENCY | MANUAL_DATA_DEPENDENCY |
| FREE / PAID | Local example has no required paid service; source rights unresolved |
| IMPORTANT ENDPOINTS | No unattended filing-discovery feed implemented |
| IMPORTANT FIELDS | Facts, concept labels, context start/end, comparative periods |
| HISTORICAL SUPPORT | One filing's comparative periods, not a longitudinal publication archive |
| CORRECTION / REVISION HANDLING | No demonstrated original/revised filing chain |
| USEFUL CONCEPT | Period/context joins for Indonesian XBRL |
| PERMISSION / TERMS RISK | Local example does not authorize automated upstream access; code licence unresolved |
| LEVEL-1 RELEVANCE | Negligible |
| PHASE-2 RELEVANCE | Secondary conceptual reference; display labels must not become stable metric keys |

Classification: **SECONDARY_REFERENCE**.

### R8 — nichsedge/idx-bei

Pinned commit: [`afdb5b039b1e437e73cf67a126032523ee0b5770`](https://github.com/nichsedge/idx-bei/tree/afdb5b039b1e437e73cf67a126032523ee0b5770).
Read [API verification spec](https://github.com/nichsedge/idx-bei/blob/afdb5b039b1e437e73cf67a126032523ee0b5770/docs/API_VERIFICATION_SPEC.md),
[corporate actions](https://github.com/nichsedge/idx-bei/blob/afdb5b039b1e437e73cf67a126032523ee0b5770/python/src/idx/scrapers/corporate.py),
[announcements](https://github.com/nichsedge/idx-bei/blob/afdb5b039b1e437e73cf67a126032523ee0b5770/python/src/idx/scrapers/news.py),
[Python client](https://github.com/nichsedge/idx-bei/blob/afdb5b039b1e437e73cf67a126032523ee0b5770/python/src/idx/core/client.py),
[Go ingestion](https://github.com/nichsedge/idx-bei/blob/afdb5b039b1e437e73cf67a126032523ee0b5770/pkg/ingest/ingest.go).
Company/member/financial scraper mappings were also inspected.

| Review field | Finding |
|---|---|
| REPOSITORY | nichsedge/idx-bei |
| LAST MATERIAL ACTIVITY | 2026-10-06 REST features at tip; relevant Python/ingestion change October 3 (`5419be1c...`); verification spec self-dated August 2026 |
| LANGUAGE / STACK | Python curl_cffi, Go TLS client and analytics/storage components; MIT; none adopted |
| UPSTREAM DATA SOURCE | IDX `/primary` internal routes; other app sources outside this review's selected scope |
| AUTOMATION METHOD | Browser/TLS impersonation, rotations on denial, JSON snapshots and daily partitions |
| AUTHENTICATION | Website/browser fingerprints and cookie jar rather than a documented permitted free API |
| MANUAL DEPENDENCY | Intended unattended; bypass-dependent route is unacceptable regardless of apparent success |
| FREE / PAID | No direct fee shown for these routes; data automation rights not granted |
| IMPORTANT ENDPOINTS | GetIssuedHistory, GetAllAnnouncement, stock/broker/index summaries, profiles/detail, GetApiDataPaginated, GetBrokerSearch |
| IMPORTANT FIELDS | caType, KodeEmiten, TanggalPencatatan, JenisTindakan, JumlahSaham/SetelahTindakan; disclosure Id/PublishDate/Code/Attachments |
| HISTORICAL SUPPORT | Action date filters in spec; code uses empty dates and length=9999. Announcement page parameters; no complete incremental watcher shown |
| CORRECTION / REVISION HANDLING | Current action JSON and same-date partitions replace prior files; no immutable correction chain or durable publication checkpoint |
| USEFUL CONCEPT | Wider capital-event catalogue and publication-bearing official disclosure index with original attachment filenames |
| PERMISSION / TERMS RISK | Explicit curl_cffi/uTLS WAF avoidance excluded; spec's HTTP-200 claims are not provider permission |
| LEVEL-1 RELEVANCE | Better CA/share-history research questions; no action completeness, status or session gate closure |
| PHASE-2 RELEVANCE | Strongest new filing-index concept when combined with authorized attachment retrieval and structured parsing |

Classification: **RESEARCH_LEAD_ONLY**.

R: the spec lists 13 action filters; actual `CA_TYPES` adds `companyListing` and
`partialDelisting`, yielding 15. Code calls each once with `start=0,length=9999`,
does not reconcile returned totals across pages, and records a failed/malformed
category as count zero with an empty list. That is unsafe evidence of no action.
Count/schema warnings are useful health concepts but do not repair incomplete
coverage. A large length parameter is not a completeness guarantee.

R: the disclosure spec shows `ItemCount`, `PageCount`, `PageNumber`, `PageSize`,
`Id`, `AnnouncementNo`, `PublishDate`, `Code`, `Title`, `Jenis`, and attachment
`FullSavePath`, `PDFFilename`, `OriginalFilename`, `IsAttachment`. The actual
function requests one page with keywords/page/language; the spec's date filters
are not exposed by that function. No complete watcher, correction/tombstone
semantics, stable ordering guarantee or revision ledger was established.
Repository specimen rows/counts are **not independently verified market facts**.

## 4. IDX endpoint intelligence and semantic limits

These are **R routes**, principally on `https://www.idx.co.id`; `/primary` is the
prefix unless shown otherwise. They were inspected in code, not called here.
P independently confirms the relevant public products/pages, not every hidden
route or its response schema. R8's “legacy route replaced” assertions remain
repository claims until independently specified by IDX.

| Route / family | Actual implementation clue | Evidence use and unresolved premise |
|---|---|---|
| `/support/stock-screener/api/v1/stock-screener/get` | R1 current universe with status, corpAction/date, notation, umaDate | Status meaning, effective interval and publication dictionary unknown |
| `TradingSummary/GetStockSummary?date=YYYYMMDD` | R1/R2/R8 shared daily OHLC/activity, Remarks, shares, foreign/nonregular fields | Finality, units/segments and state decoding need authority; row existence is not eligibility |
| `ListedCompany/GetTradingInfoDaily?code=` | BoardCode, IDStocksSummary, DTCreate and latest market values | BoardCode may describe a different concept from ListingBoard; timestamp is not completedAt |
| `ListedCompany/GetTradingInfoSS?code=&start=&length=` | Historical replies and IDs; previousAdjusted locally duplicates Previous | Useful history lead; neither adjustment semantics nor revision sequence proved |
| `StockData/GetSecuritiesStock?start=&length=&code=&sector=&board=` | Code, Name, Shares, ListingDate, ListingBoard and totals | Current identity/listing snapshot; historical applicability and security-type taxonomy require specification |
| `Home/GetCalendar?range=m&date=` | id, title/code, Jenis/type, description, location, Step, start/date, AgendaTahun | Event/agenda calendar; no proven OPEN/CLOSED sessions or exception-complete amendments |
| `ListedCompany/GetCompanyProfiles` / `GetCompanyProfilesDetail` | Company listing board, profile Status, directors/shareholders/subsidiaries | Company status is not proven security trading status; current ownership is not PIT history |
| `ListedCompany/GetAnnouncement` / `GetProfileAnnouncement` | Issuer announcements and attachment clues | No complete transition index established; do not assume equivalent to R8 route |
| `NewsAnnouncement/GetAllAnnouncement` | R8 publication-bearing paginated disclosure envelope and original attachments | Strong filing-index concept; full coverage, timezone, amendments, deletion/replacement and permitted access unresolved |
| `ListedCompany/GetFinancialReport` | kodeEmiten/year/periode/reportType=rdf; Report_Year/Period; attachment File_ID/Name/Path/Size/Type/Modified | Period-specific file discovery; File_Modified is not necessarily first public publication |
| `ListingActivity/GetIssuedHistory` | R2 issuer lookup; R8 caType/date filters and capital/share-change fields | Broader share/action ledger; listing date is not automatically ex-date/effective price-adjustment date |
| `Home/GetRelistingData?pageSize=&indexFrom=` | Activities with code/name/listing date | Relisting is not automatically resumption after suspension; wrapper total is array length |
| `Home/GetSuspendData?resultCount=` | Kode, Judul, Date, Info_Type, Data_Download | Notice lead, no complete cursor, checkpoint, enum or resumption closure proved |
| `DigitalStatistic/GetApiDataPaginated` | LINK_LISTING, LINK_DELISTING, LINK_DIVIDEND, LINK_FINANCIAL_DATA_RATIO, LINK_STOCK_NEW_LISTING, LINK_RIGHT_OFFERING, LINK_STOCK_SPLIT | Explicit event/statistic families; not exhaustive action coverage or PIT fact history |
| `TradingSummary/GetBrokerSummary?date=&start=&length=` | IDFirm, FirmName, Volume, Value, Frequency | Broker-wide aggregate; no demonstrated per-stock buy/sell counterparty matrix or investor identity |
| `ExchangeMember/GetBrokerSearch?option=&license=&start=&length=` | R8 exchange-member directory | Code/name/licence context, not stock-level broker positions |
| `DigitalStatistic/GetApiData` foreign/domestic aliases | LINK_TABLE_DAILY_TRADING_INVESTOR_FOREIGN / DOMESTIC; year/month base64 query | Aggregated trading context; mapping arithmetic is not official unit/segment documentation |
| `TradingSummary/GetIndexSummary`, `Home/GetTradeSummary` | Daily index and current market aggregates | Benchmark candidates, not proof exchange E completed |
| `DigitalStatistic/GetApiData` market/industry aliases | LINK_DAILY_IDX_INDICES, LINK_DPS_JCI_SECTORAL_MOVEMENT, LINK_LIST_TRADING_SUMMARY_INDUSTRY_CLASSIFICATION | Industry Shares/MCap/Volume/Value/Freq/PER/PBV/Members; aggregate context, not constituent eligibility |
| Gainer/loser statistics | prevValueCA, dilution | Positive adjustment clues; no complete causal action ledger or currency continuity proof |

### TradingStatus, board and completeness

**IDX screener `status` semantics: UNKNOWN.** No official dictionary was found
in the provider pages/targeted search inspected. R1 merely passes the nullable
value through. “Listed,” active company, register state, trading permission and
observed activity remain distinct. Neither quotation nor nonzero trading nor
absence from a suspension list proves affirmative TradingStatus.

Positive authoritative bootstrap + complete suspension/resumption transitions +
dated reconciliation checkpoints is a sensible acquisition concept. These
repositories do not demonstrate that package. IDs and record totals do not prove
gapless delivery; recent-result limits and page-number lists can miss transitions.
Need authoritative covered scope, effective time/session, ordering/gaps, all
resumptions/amendments and periodic full reconciliation. Under frozen V0.2,
TradingStatus remains exact-session T1/T2 SESSION_FACT evidence; raw event replay
does not acquire that type merely by implementation choice.

ListingBoard/PapanPencatatan is a stronger **listing-board** clue than an ambiguous
BoardCode. A main/development/acceleration label, a special-monitoring notation,
UMA flag, and regular/nonregular market segment are not interchangeable. Need
dated board membership plus governing mechanism rule and exceptions; no free
admitted PIT board/mechanism route was established. Do not decode Remarks or
status characters using field position guesses.

### Calendar, exceptional closures and completion

R2 GetCalendar maps an agenda; no OPEN/CLOSED session schema, authoritative
exception coverage or amendment history was demonstrated. P IDX holiday pages
remain useful annual schedule leads, with exceptional closures and changes needing
their own originals. Weekday arithmetic can plan requests, never establish a session.

Daily stock/index summaries, DTCreate, retrieval time, a finished batch and ordinary
market hours do not prove session E actually completed. V0.2's independent
CompletedSession still requires affirmative completion and its supported nonnull
completedAt. No new completion artifact with documented semantics was found.

### Identity, listing, delisting and relisting

Security master, dated listing/delisting statistics, profile attachments and
KSEI's master are promising complementary identity sources. Historical records
may support effective listing changes when authenticated; a current snapshot
cannot reconstruct when our system knew them. Preserve ticker/security identity,
effective dates and revision chronology. Raw DelistingDate being dropped by R2
does not mean IDX lacks the field; a null field does not prove indefinite listing.
Full free unattended coverage and source admission remain unresolved.

### Corporate actions and comparability

R8 improves the catalogue beyond dividends/splits: BuybackSaham, PrivatePlacement,
stockSplit, reverseStock, hmetd, tanpaHmetd, dividenSaham, sahamBonus, ipo, waran,
gabungUsaha, kurangModal, konversiSaham, companyListing and partialDelisting.
P [IDX's action page](https://www.idx.id/id/perusahaan-tercatat/aksi-korporasi/)
independently shows these broad categories and **ESOP/MSOP**, absent from that
15-item code list. This is evidence against treating the wrapper enumeration as
an exhaustive authoritative universe. The page does not document every API filter.

`JumlahSaham` and `JumlahSahamSetelahTindakan` improve share-change research;
`TanggalPencatatan` is a listing-date clue, not a universal economic/ex-date.
Buyback authorization, executed repurchase, treasury stock, listed shares,
outstanding shares and EPS weighted-average shares are different quantities.
Do not compute dilution from a current total without the appropriate prior basis.

`corpAction`/`corpActionDate` are useful positive change/reconciliation triggers.
No dictionary proves their action taxonomy, date meaning, lookback, null meaning,
cancellation treatment or complete window coverage. Splits, dividends, rights,
share histories, disclosures and KSEI can be composed; no single endpoint is
required. But unioning incomplete feeds cannot establish absence of omitted events.
Need complete covered action types/window, all pages and amendments/cancellations,
applicable dates/ratios and reconciliation against an authoritative checkpoint.
Cash dividends are not made complete by a share-capital-event endpoint.

**PriceComparability = CLEARED remains unavailable from these discoveries alone.**
Neither `previousAdjusted`, prevValueCA nor dilution is a substitute for covered
event evidence. No failed/empty category may become “no action.”

## 5. Independent provider verification and permission ledger

These significant URLs were actually opened. Findings are limited to accessible
content; no market endpoint, authenticated account or protected-service bypass
was tested. Existing detailed artifact findings in 5A.2b–5A.2f are carried forward
explicitly, not presented as new downloads.

| Provider / checked sources | P finding | Remaining limit / operational decision |
|---|---|---|
| IDX [terms](https://www.idx.id/id/syarat-penggunaan/) and [data services](https://www.idx.id/id/produk/layanan-data-bei/) | Website scraping/crawling restricted; system-to-system data services exist | No free permitted internal-API/attachment automation grant established; no paid service shown to close all exact controls |
| IDX [stock list](https://www.idx.id/id/data-pasar/data-saham/daftar-saham/), [summary](https://www.idx.id/id/data-pasar/ringkasan-perdagangan/ringkasan-saham/), [suspensions](https://www.idx.id/id/berita/suspensi/), [holiday schedule](https://www.idx.id/id/berita/jadwal-libur-bursa/) | Public page shells/filters and categories accessible | Dynamic records/dictionaries and live API semantics not independently verified |
| IDX [disclosures](https://www.idx.id/id/perusahaan-tercatat/keterbukaan-informasi/) and [corporate actions](https://www.idx.id/id/perusahaan-tercatat/aksi-korporasi/) | Disclosure/announcement/financial-report tabs; action categories | Supports product/data-model relevance, not GetAllAnnouncement publication timezone, completeness or permissions |
| IDX [XBRL](https://www.idx.id/id/perusahaan-tercatat/xbrl/) | Official taxonomy, financial statements, entity information, industry entry points and downloadable materials | Establishes structured filing concept; no complete public revision feed or automatic acquisition licence demonstrated |
| KSEI [downloads](https://web.ksei.co.id/data/download-data-and-user-guide?setLocale=en-US) | Master and corporate-action downloads independently visible | No new files fetched; prior combined-export size/row-bound and permission/completeness gaps remain |
| Stockbit [terms](https://stockbit.com/terms), [help](https://help.stockbit.com/id/), [Basic features](https://help.stockbit.com/id/category/fitur-basic-stockbit-18t2ol6/) | Automation/data extraction requires prior express written consent; feature help is public | Personal display/reformatting rights are not a machine acquisition/retention licence; no consent obtained |
| Stockbit [Key Stats](https://help.stockbit.com/id/article/key-stats-bagaimana-cara-menggunakan-fitur-keystats-1d6uovg/) and [Financials](https://help.stockbit.com/id/article/financials-bagaimana-cara-menggunakan-fitur-financials-1rqhcmb/) | Broad metrics and quarter/annual/TTM/interim views | Feature existence does not establish original publication/revision provenance or supported API |
| Stockbit [Basic/Pro comparison](https://help.stockbit.com/id/article/apa-bedanya-fitur-stockbit-basic-dan-stockbit-pro-1186o4d/) and [Pro payment](https://help.stockbit.com/id/article/bagaimana-cara-melakukan-pembayaran-stockbit-pro-n6fe42/) | Paid offerings exist; older comparison and newer Basic feature categorization differ | Actual free-account entitlement not verified; do not claim all financial data is paid or permanently free |
| Stockbit [Bandar Detector help](https://help.stockbit.com/id/article/bandar-detector-pengenalan-teori-ilmu-bandarmology-11kn0hl/) | Broker-flow analysis is a product feature | Broker aggregates do not identify end investors or prove intent |
| EODHD [EOD docs](https://eodhd.com/financial-apis/api-for-historical-data-and-volumes), [terms](https://eodhd.com/financial-apis/terms-conditions), [source disclosure](https://eodhd.com/financial-apis/our-data-sources-and-data-partners) | Documented EOD acquisition and qualifying private storage/analysis support prior 5A.2f | No new JK-specific authenticity/date or exact currency/unit proof; actual entitlement untouched |
| BCA [financial reports](https://www.bca.co.id/id/tentang-bca/hubungan-investor/laporan-presentasi/laporan-keuangan) and [terms index](https://www.bca.co.id/id/Syarat-dan-Ketentuan) | Issuer financial-report archive with period/audit labels exists | Landing page inspected, not report bodies; banking-terms index supplies no explicit automated IR acquisition grant |
| Yahoo [terms](https://legal.yahoo.com/us/en/yahoo/terms/otos/index.html) and [API terms](https://legal.yahoo.com/us/en/yahoo/terms/product-atos/apitnc/index.html) | Attempts did not yield readable terms | No rights conclusion drawn from search snippets; no demonstrated supported permitted JK route |

Repository MIT/Apache licences above concern code only. Missing identified code
licence is not public-domain status. Neither public attachment URLs nor copying a
third-party mirror launders the upstream data permission problem. No mirror was
admitted as an authoritative or licensed substitute.

## 6. Price, benchmark and optional market context

| Source | Permission / automation | History, revisions, PIT, units | Current role |
|---|---|---|---|
| EODHD | Documented API, free credential within limits; qualifying private storage justified by prior review | Bounded range history and local retained revisions feasible; JK genuineness/date and exact raw OHLC currency/unit continuity unproved | **EODHD_DECISION_STILL_PENDING**; preserve conditional T3 price lead |
| IDX internal summaries | Exchange-origin route clue; website automation restriction, cookies/undocumented interfaces | Shared daily/historical rows; genuine/no-trade semantics, units and retained revision clocks not authenticated here | Research lead; no replacement admission |
| Stockbit | Consent/entitlement/auth gaps, occasional interactive re-bootstrap | Inspected orderbook is latest feed; richer history elsewhere cannot be assumed; no sufficient native PIT revision evidence | Not operationally suitable |
| Yahoo/yfinance | No permitted supported route verified in this review | R3 asks one day; no proven 50-session history, adjustment/unit/genuineness or revision model in that code path | Research/cross-check concept only, no automatic fallback |
| Other wrappers / normalized IDX statistics | Same IDX/Stockbit upstream gates | More mapped fields, not independent provenance or better rights | No stronger free operational provider found |

Preserve 5A.2f's **two mandatory unresolved price families**: (1) JK source,
date/no-trade/synthetic/substitution conventions; (2) exact raw OHLC currency,
per-share unit/scale and historical continuity. Generic raw-as-traded language
does not prove the first. Native vendor correction IDs are **not** universally
required: retain original bytes and append locally observed revisions with honest
knownAt and source/row/artifact bindings. Missed intermediate versions remain a
limitation, never reconstructed history.

Optional volume/segment uncertainty does not become a new universal price blocker.
EODHD's documented split-adjusted volume does not satisfy the current raw quantity
basis for volume-dependent features. Leave those features UNAVAILABLE. Benchmark
requires independently admitted aligned observations. ForeignBuy/Sell, Value,
Frequency, bid/offer and nonregular aggregates are useful optional liquidity/flow
leads only after units, sides, timing and market coverage are established. Broker
summary is not demonstrated per-security broker accumulation or beneficial-owner
identity. Defer rotation/bandarmology and do not infer investor intention.

## 7. Stockbit fundamentals, provenance and automation verdict

R3 supplies a broad current/TTM metric inventory: PE/forward PE/earnings yield,
P/S, PBV, P/CF, P/FCF, EV/EBIT, EV/EBITDA and PEG; EPS, revenue/cash/book/FCF
per share; ROA/ROE/ROCE/ROIC, coverage, turnover and cash conversion cycle;
gross/operating/net margins and revenue/gross-profit/net-income growth; revenue,
gross profit, EBITDA, net income, cash/assets/liabilities/equity/working capital,
short/long/total/net debt, CFO/CFI/CFF/capex/FCF; dividend/TTM dividend/payout/yield/
ex-date; shares, market cap and enterprise value. This is rich **capability**, not
verified period-level authoritative provenance.

| Provenance question | R/P evidence | Verdict for authoritative Fundamental Facts |
|---|---|---|
| Original issuer/IDX filing reference | Not retained by R3; R6 removes a presentation blob, so upstream absence cannot be proved | Not established |
| Reporting period | R3 metric labels include TTM/quarter; R6 has statement/report selectors; P describes period views | Partial capability, not normalized retained fact-period identity |
| Quarter/year identity | year_limit=10 and period selectors are clues; no original dated fact series verified | Not sufficient |
| Publication date | Stream created_at is a news clock; no linked financial publication shown | Not established |
| Known-at timestamp | Local retrieval/storage clocks can be retained prospectively | Cannot recover when original/revised values were publicly known |
| Restatement marker | No authenticated marker/relationship in inspected normalized model | Not established |
| Revision history | Current values/caches and generic timestamps | No demonstrated append-only original/revised filing lineage |
| Consolidated vs standalone | Not resolved by R3; selector enums in R6 lack independent semantics | Not established |
| Audited vs unaudited | Not retained in inspected normalized fact model | Not established |
| Currency | R6 exposes currency/default_currency | Real clue; fact-level binding still needed |
| Unit/scaling | R6 rounding_value; R3 parses textual magnitudes | Display rounding/magnitude is not a verified per-fact unit/scale |
| Source attribution | Stockbit identified as provider, original filing not demonstrated per fact | Insufficient authoritative source chain |

**Stockbit cannot presently supply admitted PIT-safe authoritative financial
facts.** This is a finding about the evidence reviewed, not an assertion that the
provider possesses no additional metadata internally.

R3 implements interactive browser bootstrap → access/refresh tokens → automated
refresh → possible interactive recovery. The server failure path explicitly
requires local login/token synchronization. **Recurring manual reauthentication
is a supported failure mode; its frequency and inevitability for a particular
account are unknown.** No official supported personal API credential flow,
refresh-lifetime guarantee or rate-limit contract was found. An ordinary API key
can also be revoked; the relevant difference is that this architecture explicitly
depends on browser extraction/re-bootstrap rather than a documented automation
facility. Never claim indefinite availability for any provider.

P terms require prior written consent for automated gathering. No consent or
actual free entitlement was obtained. Therefore **ROLE D — NOT_OPERATIONALLY_SUITABLE**
applies now, including under the no-routine-manual requirement. A future approved
normalized cross-check role would require resolving access, rights and chronology;
this review does not simultaneously assign Role B or C operationally.

## 8. Fundamental-source comparison and preferred concept

This comparison challenges, rather than assumes, “official first.” A properly
licensed free normalized provider with full original filing links, publication
history and revision metadata could reduce parser cost substantially. No such
provider was established here. Official files have stronger potential provenance,
but are not automatically complete, machine-readable, correct or permitted to
retrieve automatically.

| Criterion | Option 1: official IDX/issuer + XBRL/XLSX | Option 2: Stockbit normalized API | Option 3: other free normalized provider found |
|---|---|---|---|
| Authority | Original issuer/exchange filing, when authenticated | Secondary normalized provider | IDX statistical ratios are official aggregates; Yahoo is secondary; neither demonstrated as full filing-fact source |
| PIT publication chronology | R8 PublishDate is promising; timezone, first-public meaning and original linkage need proof; local knownAt stays separate | Financial publication history not demonstrated | No stronger publication/revision evidence found |
| Reporting-period identity | Filing DEI/context, start/end/instant and year/period index can be reconciled | UI/selector/metric periods available, retained fact identity incomplete | IDX quarter/year query clues; Yahoo path inspected only prices |
| Revisions/restatements | Retain each filing/index version; replacement at same URL and comparative restatements must be detected | No authenticated revision lineage established | Current ratios/observations are not an original/revised filing archive |
| Consolidated/standalone | Must read explicit entity/dimension/report scope, never infer solely from absence of dimensions | Not verified | Not verified |
| Units/currency | XBRL unitRef/scale and XLSX statement headers can provide explicit evidence; actual mapping validation required | Currency/rounding clues, insufficient per-fact binding | Aggregate field labels insufficient |
| Automation | Machine parsing technically feasible; permitted discovery/download route unresolved | Token refresh works as code concept; interactive recovery path | Internal IDX/Yahoo access not admitted |
| Permission | IDX restriction; issuer-specific automation/retention rights not established | Written consent required, not obtained | No stronger rights demonstrated |
| Manual dependency | Could be fully automated with authorized index and attachments; manual downloads excluded | OCCASIONAL_MANUAL_REAUTH | No qualifying unattended fact route established |
| Fragility | Taxonomy versions, partial XLSX/PDF coverage, moving pagination/files, missed corrections | Tokens, undocumented schemas, positional parsing, provider normalization changes | Same upstream/schema/coverage limitations; wrapper is not independent redundancy |

**Conditional preferred free-first Phase-2 acquisition concept:**
`GetAllAnnouncement` (if authorized and its semantics verified) + official
attachments, reconciled with `GetFinancialReport`'s ticker/year/period listing,
then XBRL/XLSX parsing into a future typed FundamentalRevision using our existing
architecture. An issuer's authorized filing index may supply the same role;
the IDX-specific route must not become a mandatory application dependency.

GetAllAnnouncement alone is a broad disclosure index, not necessarily a complete
XBRL/XLSX index: the specimen demonstrates PDF links. Do not assume every financial
announcement contains structured attachments or can be joined by filename.
Require a supported issuer/report/announcement identity link to the financial
report index. The public IDX disclosure page confirms the product categories,
not this cross-endpoint join. No defensible free automated access grant to that
combined route was found in this review.

### What the two user-added priorities improve

| Question | Improvement | Operational result |
|---|---|---|
| Automated action completeness | R8 names more action categories and gives date/page/count clues | Not established: incomplete type coverage, failure→empty and no verified exhaustive checkpoint |
| Shares/dilution history | Post-action share totals plus dated filing facts are better research inputs | Need share definition, effective date, execution/cancellation and complete revisions; no admitted history |
| Listing/board metadata | Profiles/security directory and capital events provide explicit fields | Current board is not historical mechanism evidence; no gate closure |
| Official filing change detection | Announcement ID + PublishDate + attachment identity is a better polling/reconciliation concept | Page churn, amended files and omissions unresolved; registry ETag is not filing revision proof |
| PIT-safe FundamentalRevision ingestion | Structured concepts/contexts plus publication index can support a future mapping | No actual admissible input package; original/revised chronology still must be proved |
| Avoiding Stockbit as authoritative source | Yes: a concrete primary-filing concept improves the alternative | Stockbit can be omitted entirely; no dependency on it is necessary for the proposed fact layer |

### Candidate Phase-2 layers (design only)

| Layer | Candidate sources and rules |
|---|---|
| AUTHORITATIVE FACTS | Permitted issuer/IDX financial originals, linked disclosure/index metadata and complete revisions; XBRL preferred, validated XLSX next. Retain reported facts, original bytes and source relationships in future typed FundamentalRevision. No generic PDF facts inferred by AI. |
| NORMALIZED / DERIVED PROVIDER DATA | Compute margins, growth, FCF and valuation deterministically from admitted facts and compatible admitted prices. Stockbit/IDX ratios may later cross-check only with permission and comparable periods/definitions; currently none is admitted. |
| MARKET / FLOW CONTEXT | Conditional EODHD T3 observations; permitted exchange activity/foreign flow; broker flow deferred. Do not convert adjusted volume into raw shares or infer ownership from flow. |
| CATALYST / NEWS CONTEXT | Original issuer/IDX disclosures and permitted issuer announcements, retaining publication/receipt clocks. Dividends/actions require governing notices; news/Stockbit streams are context, not authoritative financial truth. |
| AI EXPLANATION | Explain deterministic facts, uncertainty and classifications with evidence links. No invented facts, missing-value fill, authoritative PDF extraction by assertion, or rule override. |

Ownership needs dated shareholder-register/disclosure evidence and beneficial-owner
semantics; current profile names or broker codes are insufficient. Shares/dilution,
dividends and corporate actions reconcile financial filings with their governing
event records; valuations bind to a specific admitted price date and share basis.

Minimum fact inventory remains revenue, operating profit where meaningful, net
income, EPS, CFO, FCF, assets, equity, debt, cash, shares, dilution, margins, growth
and dividends. Different industries need suitable taxonomy concepts: bank income
must not be forced into a generic industrial sales concept. FCF needs an explicit
definition and capex sign/basis; it is not assumed equal to a provider label.

Each retained fact needs stable concept/metric identity, issuer/security identity,
reporting period and duration/instant, period type, consolidation/dimensions,
audit status when applicable, currency/unit/scale, filing identity, publication
evidence, retrievedAt/knownAt and revision lineage. XBRL decimals is precision,
not automatically a multiplication factor. Preserve exact numeric representation
and missingness, not floats/zero substitution from example parsers.

Quarter/YTD differencing requires compatible cumulative periods and revision
bases; TTM must identify its component periods. Later-restated comparatives cannot
replace previously known values in earlier decisions. A filing obtained today
may support prospective analysis of earlier economic periods but cannot be
backdated into historical system knowledge.

CONSISTENTLY_PROFITABLE, IMPROVING_PROFIT, PROFIT_TURNAROUND,
DETERIORATING_PROFIT, LOSS_TURNAROUND_ATTEMPT, PERSISTENT_LOSS and VOLATILE need
an explicitly designed future rule and sufficient comparable multi-period data.
No thresholds are invented here. Until those facts/rules are admitted, use
INSUFFICIENT_DATA. Ten-year query parameters or one current TTM snapshot do not
establish this history.

## 9. Requirement challenge and unnecessary complexity

| Requirement / design choice | Classification | Reason and boundary |
|---|---|---|
| Affirmative TradingStatus | KEEP | Prevents listed-but-ineligible securities being treated as executable; price activity is not permission |
| Independent exchange session evidence | KEEP | A missing price row cannot distinguish holiday, outage, suspension or no-trade |
| Completed-session evidence | KEEP | Published observations do not prove finality; current nonnull completion-clock semantics unchanged |
| Corporate-action completeness | KEEP | Positive events plus closed-window coverage are necessary for absence claims; composition is allowed |
| Price comparability | KEEP | Adjustment/capital discontinuities can invalidate technical recurrences |
| Volume-unit certainty | MAKE_OPTIONAL | Required by volume-dependent features, not a universal price-only gate; unknown stays unavailable |
| Observation market-segment certainty | MAKE_OPTIONAL | Enforce for segment-dependent quantity/liquidity comparisons; do not confuse with mandatory instrument mechanism |
| Board/mechanism | KEEP | Dated membership, rule and exceptions establish applicability, not a current board label alone |
| PIT chronology | KEEP | Effective, publication, receipt, known and recording clocks cannot be conflated |
| Source revision retention | KEEP | Append changed originals/facts; preserve already captured decisions |
| One source/artifact per claim | SIMPLIFY | A single authenticated original can support several independently justified claims; no duplicate fetch required |
| Provider-native revision IDs everywhere | SIMPLIFY | Locally observed content/source revisions can suffice where bindings permit; never relabel them provider publication IDs |
| Universal full-universe historical bootstrap | DEFER | Prove bounded BBCA/control coverage first; FullIdx and wider backfills remain gated |
| Broker/rotation/news AI features | DEFER | Optional future context does not close mandatory source gaps |
| A different completion representation or transition-derived status type | REVISIT_IN_V0_3 | Only if a permitted authoritative source exposes a materially different evidence form; additive design review required, no relaxation authorized here |

The over-engineering risk is multiplying providers, per-ticker reference polls,
parser frameworks or native-ID requirements before source permission and semantics
are known. The correctness controls themselves are not shown unnecessary by
repositories that omit them. Reuse retained artifacts, existing revision/readiness
boundaries and one bounded source proof; do not add services, schemas or a runner.

## 10. Strongest conditional free-first Level-1 stack

There is **no currently complete admitted zero-cost unattended stack** in this
review. The following is the best conditional composition, not a claim of readiness.
The route table supplies SOURCE TYPE, AUTHORITY, AUTOMATION, ACCOUNT REQUIRED,
COST and PERMISSION; the claim table supplies CLAIM, SOURCE, PIT SUPPORT,
RECONCILIATION, FALLBACK and CURRENT FEASIBILITY. Read them together for each row.

| Route | Source type / authority | Automation / account / cost | Permission and current manual-dependency classification |
|---|---|---|---|
| L1 IDX controls | Original exchange master, status, board rules, schedules and final bulletins; T1/T2 only when authenticated under the binding | Desired permitted shared snapshot/index/events; no public-site account assumed; free route unproved, licensed service conditional/possibly paid and not selected | Current website path restricted: **NOT_OPERATIONALLY_ACCEPTABLE**; a separate written permitted route would be required |
| L2 KSEI master/actions | Official registrar/depository records for their covered claims, not exchange trading permission | Desired dated exports plus covered event index; public page has no login, no displayed charge; no admitted automated feed | Automation/retention/completeness unresolved: **NOT_OPERATIONALLY_ACCEPTABLE** pending exact scoped proof |
| L3 issuer originals | Original issuer reports/action disclosures | Desired authorized index/files, no assumed account or price; issuer-specific | Free automatic retention/access not established: **NOT_OPERATIONALLY_ACCEPTABLE** as current final route |
| L4 EODHD | Documented vendor API, T3 observations | Bounded scheduled HTTP; free account/key required; zero mandatory monetary cost within qualifying plan and ceilings | Private active-account retention ALLOWED_JUSTIFIED in 5A.2f; **AUTOMATED_WITH_FREE_CREDENTIAL** describes transport, not completed semantic admission |
| L5 IDX internal observations | Exchange-origin summary/statistics route clues, no authority upgrade merely from hostname | Shared daily JSON technically represented in code; browser session dependency, no free public API contract | Website restriction and schema/semantic gaps: **NOT_OPERATIONALLY_ACCEPTABLE** |
| L6 Stockbit | Authenticated normalized/market provider | Account/access+refresh tokens; actual free entitlement unknown | Consent absent; auth path **OCCASIONAL_MANUAL_REAUTH**, overall **NOT_OPERATIONALLY_ACCEPTABLE** |
| L7 Yahoo | Third-party observations via wrapper | Public/internal endpoints in code; supported free automated contract not established | Terms access incomplete and semantics unresolved: **NOT_OPERATIONALLY_ACCEPTABLE** as final source |

No row promises indefinite availability. Local parsing of already authorized,
automatically acquired originals can be **FULLY_AUTOMATED**; this is not an
acquisition route. R7 and routine manual PDF/CSV handling are
**MANUAL_DATA_DEPENDENCY** and excluded. Research-time inspection is not production
manual collection.

| CLAIM | SOURCE | PIT SUPPORT required / observed | RECONCILIATION | FALLBACK | CURRENT FEASIBILITY |
|---|---|---|---|---|---|
| Identity | L1 security master + L2 dated master | Dated code/ISIN/type applicability; current snapshots alone insufficient | Shared scope/count/key reconciliation and retained changes | Block affected identity; no ticker guess | Promising reference data, automatic route/admission unresolved |
| Security type | L1/L2 governing classification | Explicit ordinary-equity/type mapping with effective interval | Compare authenticated classifications; quarantine conflict | Exclude unsupported type | Dictionary/route unresolved |
| Listing/delisting | L1 events/master + L2/L3 originals | Effective and publication dates; current nulls not historical proof | Full master against dated listing/delisting/relisting events | Block ambiguous interval | Better endpoint leads, no complete admitted PIT source |
| TradingStatus | L1 positive exact-session status | T1/T2 SESSION_FACT; no activity inference | Authoritative positive bootstrap, complete transitions, final checkpoint | Fail closed | Mandatory blocker unchanged |
| Board/mechanism | L1 membership + governing rules/exceptions | Effective dated mapping, mechanism distinct from segment | Board snapshot/event reconciliation and rule version | Fail closed where required | Mandatory blocker unchanged |
| Calendar | L1 annual schedule + amendments | Applicable schedule originals known by cutoff | Daily permitted amendment/exception checkpoint | No weekday-derived authority | Schedule concept useful, automated admission unresolved |
| Exceptional closures | L1 governing notices/checkpoint | Exact date/scope and publication/effect clocks | Compare schedule and complete exception stream | Pending/blocked, never normal-session guess | No new complete free source |
| Session completion | L1 final completion artifact | Independent affirmative E completion and supported completedAt | Match governing final artifact to session and revision | Pending/blocked | No defensible new route |
| OHLC | L4 conditional T3 | Raw/as-traded proof partly established; two JK premises unresolved | Bounded overlap, exact row/artifact bindings, append changed versions | Missing/blocked, no silent Yahoo/IDX switch | Conditional; no new price admission |
| Benchmark | L4 independently admitted benchmark, or future permitted L5 index | Aligned genuine dates/conventions independently proved | Same bounded revision reconciliation | Optional RS unavailable | Unadmitted optional observation |
| Volume/value | L5 or separately authenticated L4 fields | Share/monetary unit, adjustment and scope proof per feature | Compare like-for-like units/bases; no inferred traded value | Optional features unavailable | EODHD split-adjusted quantity does not satisfy current raw basis |
| Liquidity | Derived from admitted volume/value/price, L4/L5 as applicable | Common units, dates and segment/basis | Recompute only from retained compatible facts | Optional liquidity unavailable | No new source clearance |
| Foreign flow | L5 permitted summaries; L6 only if independently cleared later | Buy/sell definition, units, scope and original date | Shared day totals/checkpoints, not investor identity inference | Omit optional context | Useful research, no operational route |
| Corporate actions | L1 events/disclosures + L2 + L3 governing originals | Closed relevant window, effective dates, cancellations/revisions and all relevant types | Action index ↔ event originals ↔ share changes; authenticate coverage | Comparability not CLEARED | Broader composition concept; completeness/permission still blocked |

### Cadence, load, bootstrap and failure detection

Reuse the existing conditional schedule: 08:00 WIB control checks, 19:00 final
target-session evidence and eligible EOD acquisition, one bounded next-day catch-up,
weekly recurrence/reference/action reconciliation, dated monthly master releases
and new annual schedule/rule versions. These are engineering choices, not publisher
SLAs; no job is installed. Prefer shared universe artifacts and change indexes
over per-ticker loops. Do not copy hourly eight-date polls or unscoped historical
queries that assume a 9999-row limit proves completeness. Exact official permitted
cadence/rate limits remain unproved.

EODHD stays at the prior **16 local / 20 free units per day** ceilings. Eleven
panel requests cost 11 units; a full same-day retry costs 22 and is excluded.
Quota reset is 07:00 WIB. A bounded history window can replace that day's narrow
request, rather than double the panel. No purchased units, account rotation,
welcome-bonus reliance or 100-unit bulk request. No market-data API requests ran here.

Price history alone cannot bootstrap 50 cleared completed sessions. R1's two-year
calendar loop lacks historical status/board/action control evidence. If complete
authorized historical controls are unavailable, a future prospective warmup is
possible only once all required daily sources are admitted; no historic current
state backfill or invented bars. This review does not start that process.

| Failure / fragility | Detection and response for a future permitted adapter |
|---|---|
| Permission/terms change or account revocation | Disable source pending scoped review; no alternate host, account or browser evasion |
| Cookie challenge / token expiry | Fail explicitly; a manual re-bootstrap path cannot be the final no-manual solution |
| Rate limit / transient error | Reserve attempts within ceilings, honor supported retry semantics; no denial-as-empty or unbounded retries |
| Schema drift / unknown enums / parser omissions | Quarantine affected facts; preserve original bytes; unknown is not zero |
| Page truncation / changing totals / moving index | Bounded overlap, stable IDs where documented, all-page accounting and authoritative checkpoint; counts alone insufficient |
| Same URL changed / historical correction | Retain original and new bytes, append revisions at honest knownAt; cache must not hide restatements |
| Stale local cache / ETag / unchanged bytes | Preserve original scope and clocks; unchanged transport state cannot extend coverage or finality |
| Missing session/period/action type | Keep readiness blocked or affected optional feature unavailable; no silent provider fallback |
| Multi-source disagreement | Retain both observations and governing authority/scope; do not choose a convenient value or majority vote |

## 11. Exact next research targets and implementation boundary

At most three immediate research targets; this is not another broad repository survey.

1. **IDX control evidence, one scoped specification/permission inquiry:** obtain
   an authorized free-machine route or explicit terms decision for the BBCA
   exact-session positive status/control package. Ask for the screener `status`
   dictionary, distinction between BoardCode and listing board, positive bootstrap
   plus complete suspension/resumption checkpoint, and independent final session
   evidence. Endpoint names from this review make the existing inquiry concrete;
   they do not justify probing restricted routes. No inquiry was sent here.
2. **EODHD JK, the existing two-premise answer:** endpoint-specific genuine/no-trade/
   synthetic/date/source convention and exact raw IDR-per-share unit/currency
   continuity. Preserve 5A.2f's precise questions; do not reintroduce universal
   native-revision-ID or optional-volume blockers. No support message sent.
3. **One BBCA corporate-action closed-window package:** verify permitted automatic
   KSEI retrieval and coverage, reconciled to the IDX issued-history/disclosure
   action taxonomy. Require all relevant types, effective dates, amendments and
   cancellations, complete pagination/export bounds and a defensible coverage
   assertion. R8's 15 categories and failures-to-empty cannot certify it.

The preferred Phase-2 disclosure-index/XBRL concept is documented for later scope;
it is not a fourth immediate research programme. Its authorization, index-to-file
identity, publication timezone and replacement-history proof remain prerequisites.

**Exact current implementation candidates: none.** Conditional future boundaries
are listed only to make the intended source roles concrete:

| Future boundary, not admitted | Source role / evidence | Conditional cadence / reconciliation |
|---|---|---|
| Existing proposed EODHD JK Price Evidence Adapter | T3 genuine price/convention observations | Bounded daily/range requests and overlap revisions after the two premises/admission close |
| Authorized IDX control mapping | T1/T2 identity/status/board/session facts from a permitted package | Shared dated checkpoint + complete transitions; final session reconciliation |
| Covered KSEI/IDX action mapping | Action facts plus covered window for derived comparability | Bounded event/index acquisition and weekly/window reconciliation |
| Future official-filing acquisition/parser boundary | Original disclosures/financial statements → typed FundamentalRevision | Permitted index polling, identity-linked changed attachments, period and restatement reconciliation; cadence determined by source allowance |

No adapters, parser framework, migrations, registries, Daily Runner or new
dependencies were created. Existing .NET/PostgreSQL and provider-neutral evidence
boundaries remain the implementation destination.

## 12. Research activity and validation record

- Anonymous public GitHub: **26 successful HTTP requests** (eight tip metadata
  requests, eight bounded source archives, ten relevant-history requests).
  One earlier sandbox DNS attempt failed before retrieval. Source copies stayed
  under a task-owned temporary directory; no external application code executed.
- Public web searches and provider documentation/help/terms reads were nonzero;
  no exact aggregate web-tool HTTP count is claimed. Some dynamic pages yielded
  shells; Yahoo terms and a Stockbit free-Pro help attempt were inaccessible.
- **Market-data API calls 0; billable provider units 0; paid activation/cost 0.**
  No IDX internal endpoint or authenticated Stockbit endpoint was called. No
  credentials used, login performed, accounts/API keys created or anti-bot bypass
  run. No email/support/licensing messages sent.
- Source/code licences and upstream rights evaluated separately; repository
  specimens, record counts and self-reported live verification were not admitted
  as independently authenticated market facts.
- Documentation-only scope: this file is the only intended repository change.
  No production code, migration, source registry or frozen contract change; no
  operational DB connection/mutation, FullIdx operation or soak alteration.
  These statements describe actions and the reviewed diff, not a fresh DB audit
  or a rerun of prior operational fingerprints.
- Proportional validation is content/source-link review, scope inspection, exact
  unstaged/staged diff review and `git diff --check`. Full test suites and provider
  execution are deliberately not required for this documentation-only decision.
  The final commit/status check is reported in the accompanying task report.

Limits: live endpoint responses, authenticated entitlements, original filing
contents and actual end-to-end unattended operation were not tested. The material
risk remains that permission or incomplete control/publication/revision coverage
prevents the proposed free source concepts from becoming operational evidence.

---

## Phase-2 entry and fundamentals readiness (2026-10-08 WIB)

**Decision: `GO_FOR_BOUNDED_DESIGN`, with production acquisition and ingestion on
`HOLD_FOR_EVIDENCE`.** Fundamental Intelligence is an independent evidence family;
bounded requirements/contract design may continue while Level-1 remains BLOCKED,
but no production filing acquisition, parser, adapter or ingestion is authorized.
This is a planning decision only — not a frozen contract, not a Phase-2
implementation authorization, and not a claim of operational readiness. No
thresholds are invented and no Stockbit integration is proposed.

Baseline: `main`, HEAD `2a4f015` (this bundle's Task 2 commit), clean worktree.
This section appends to this file alone and reuses the pinned-repository findings
above rather than repeating them.

### Dependency assessment

| Question | Finding |
|---|---|
| A. Requires complete Level-1 readiness? | **No.** Fundamentals are a separate evidence family with their own claims (financial facts) and do not depend on TradingStatus/session/action gates. |
| B. Developable independently with its own admitted evidence? | **Design: yes.** Production build: no, until an authorized acquisition route exists. |
| C. Requires selected shared infrastructure? | **Yes.** Immutable originals, provenance, revision/PIT chronology, provider-neutral typed evidence and the existing evidence store are shared and designed to be reused. |
| D. Must wait for authorized source acquisition before implementation? | **Yes** for any acquisition/parser/ingestion. Bounded design and a proposed additive contract may proceed. |

Development feasibility, data-acquisition feasibility, evidence admission and
operational readiness are distinct; only the first is currently satisfied.

### Fundamental source strategy

Retain the conditional concept from section 8: official issuer/IDX financial
original -> authorized filing discovery -> immutable original -> XBRL preferred,
validated XLSX second -> typed financial facts -> PIT/revision handling ->
Fundamental Intelligence. `GetAllAnnouncement`/`GetFinancialReport` remain
discovery concepts, not authorized routes; Stockbit remains non-authoritative
secondary cross-check only; official files are not assumed to be completely or
permissibly machine-retrievable. No GitHub survey is repeated.

### Fundamental capability requirements (assessment targets only)

Minimum facts: revenue, operating profit where meaningful, net income, EPS, CFO,
capex, FCF, assets, equity, cash, debt, shares outstanding, dilution, margins and
growth, plus valuation inputs and dividend evidence. Each fact needs stable concept
identity, issuer/security identity, reporting period (duration/instant), period
type, consolidation/dimensions, audit status, currency/unit/scale, filing identity,
publication evidence, retrievedAt/knownAt and revision lineage. Bank versus
non-bank statements must not be forced into one generic concept; consolidated
versus standalone, audited versus unaudited, restatements and historical revisions
must be explicit. Exact numeric representation and missingness are preserved; no
float/zero substitution.

### Profitability trajectory

The existing project does not define rules or thresholds for
CONSISTENTLY_PROFITABLE, IMPROVING_PROFIT, PROFIT_TURNAROUND, DETERIORATING_PROFIT,
LOSS_TURNAROUND_ATTEMPT, PERSISTENT_LOSS, VOLATILE or INSUFFICIENT_DATA. Until a
future explicit design and sufficient comparable multi-period data exist, the
correct behavior is `INSUFFICIENT_DATA`; no thresholds or classifier are invented
here.

### Existing architecture reuse

Reuse (conceptually, no mapping frozen): `RawArtifactArchiver` content-addressed
original retention; archive-before-parse ingestion; provider-neutral typed evidence
binding and immutable revision series; PIT/as-of readers and cutoff/knownAt
visibility; readiness/diagnostic evaluators. A future fundamental claim vocabulary
would be an **additive** payload version, not a reinterpretation of V0.2.

### Missing requirements before implementation

- An authorized, permitted filing-acquisition route (discovery + attachments).
- A fundamental evidence claim/binding design (proposed, reviewed, frozen later).
- Filing identity/publication model, period/duration model, restatement lineage,
  taxonomy mapping, bank/non-bank concept handling.
- Retention and revision rights.

### First bounded slice (if pursued)

A design-only deliverable: **Phase-2 Fundamental Evidence Binding V0.1** — a
proposed additive claim schema and PIT/revision model for review. It must not
freeze a contract, implement acquisition, or require blocked Level-1 sources.
Acceptance gates: reviewed proposal, explicit unresolved-source list, no production
code, no contract freeze without separate approval.

### Validation

Documentation only; append to this file alone. No acquisition, parser, adapter,
contract freeze, DB change or Stockbit integration; network/provider use **0**.
`git diff --check` clean; commit message
`docs: assess phase2 fundamentals readiness`; no push.
