# Configurable Source Usage & Feature Capability Policy V0.1

Status: **PROPOSAL — NOT FROZEN**. SCP-1–SCP-4 design bundle, 2026-10-09 WIB.

## 1. Scope and non-goals

Propose a small, typed usage gate around the existing analytical core: PERSONAL
today, possible SAAS later. Configuration expresses requested use; documentary
permission and applicable evidence determine whether it can proceed. This document
does not grant rights, admit providers, freeze contracts or authorize implementation.

Baseline inspected: clean `main`, HEAD
`50744a4f558ed573718013cb81526407cc53cd56`, four ahead / zero behind the locally
recorded `origin/main`; no fetch. Only this proposal is changed. Source findings
below are retained assessments, not newly verified terms or legal opinions.

Boundaries and authorities:

- [Execution rules](AGENT_EXECUTION_RULES.md) govern execution unchanged.
- [Screener Evidence V0.2](SCREENER_EVIDENCE_V0_2_CONTRACT.md) and
  [persisted binding](SCREENER_EVIDENCE_V0_2_PERSISTED_BINDING.md) remain frozen.
- [Research V0.1](RESEARCH_EVALUATION_V0_1_CONTRACT.md),
  [decision snapshots](DECISION_SNAPSHOT_V0_1_CONTRACT.md) and
  [portfolio ledger](PORTFOLIO_LEDGER.md) keep their existing meanings.
- [Automated Level-1 architecture](LEVEL1_AUTOMATED_SOURCE_ARCHITECTURE.md),
  especially its operational disposition, preserves Level-1 BLOCKED and independent
  portfolio/research development. The [source stack](LEVEL1_SCREENER_SOURCE_STACK.md)
  records unresolved exact claims.
- [Fundamental feasibility](FUNDAMENTAL_SOURCE_FEASIBILITY_GATE_V0_1.md) remains
  BLOCKED; Phase-2 production remains HOLD_FOR_EVIDENCE.
- [Frozen fundamental contract](FUNDAMENTAL_EVIDENCE_BINDING_V0_1_CONTRACT.md)
  §§18–25 is FROZEN (2026-10-20 WIB). Its §21 current retained-use
  gate is compatible with this proposal, not replaced by it.
- Relevant [runbook](PRODUCT_SLICE_RUNBOOK.md) entries are the Level-1 operational
  disposition, external-source review/backlog and Phase-2 fundamental reviews.

No collector, parser, adapter, migration, SQL execution, production configuration,
provider switching, SaaS infrastructure, contact or source activation is included.
No new readiness enum, licensing platform, plugin marketplace, dynamic loading,
arbitrary scripting, unused provider interfaces, policy language, tenant system,
billing service or microservice is proposed. Zero mandatory paid infrastructure
remains the personal goal; a catalog entry is not evidence of a free entitlement.

## 2. Existing architecture reuse and actual gaps

Paths here are actual repository paths inspected at the baseline. Implemented
machinery does not establish operational data availability. No operational database
was inspected; retained deployment reports are not fresh schema verification.
The available machine-wide Graphify graph covers local infrastructure, not these
stock-source paths; source reads corroborate this bounded review.

| Existing component | Actual behavior to reuse | Additive gap / limit |
|---|---|---|
| `src/IdxStockIntelligence.Infrastructure/Migrations/0001_phase0_foundation.sql` | `source` stores identity, display name, `terms_status`, terms URI and review time; `ingestion_run` and `raw_artifact` retain source/hash/parser provenance | Registry vocabulary is only UNKNOWN/ALLOWED/DISALLOWED; it has no operation/profile rights, retained terms versions or capability declarations |
| `src/IdxStockIntelligence.Application/ScreenerEvidenceV02Evaluators.cs` | `ScreenerSourceAdmission.Admit` enforces claim/authority-tier limits; validity checks known/retrieved/published clocks | Tier admissibility is evidence quality, not automation, retention or redistribution permission |
| `src/IdxStockIntelligence.Application/BoundedEvidenceIngestion.cs` | Explicit trusted `BoundedEvidenceSource` mappings, provider-neutral typed rows, bounded bytes/rows, chronology/hash/identity/lineage checks | Normalization boundary is for Screener evidence, not a ready Fundamental importer or universal provider framework |
| `src/IdxStockIntelligence.Infrastructure/BoundedEvidenceIngestionService.cs` | Requires one installed reviewed mapping and registry ALLOWED; authenticates archived bytes; appends semantic rows transactionally | No adapter installed by default; coarse permission check cannot establish per-operation rights; existing checks must remain |
| `src/IdxStockIntelligence.Infrastructure/RawArtifactArchiver.cs` | Content-addressed file storage without overwriting a destination; ingestion separately authenticates hash/length | Archive identity is not external truth or retention permission; no rights-aware deletion protocol exists |
| `src/IdxStockIntelligence.Infrastructure/ScreenerEvidenceV02AsOfReader.cs`, `ScreenerEvidenceV02ReadinessService.cs`, `ScreenerEvidenceV02TechnicalService.cs` | Persisted binding, cutoff selection, independent dimension/readiness/technical evaluation | Exact mandatory external source premises remain unmet; the new gate cannot repair them |
| `src/IdxStockIntelligence.Application/ScreenerEvidenceV02Candidates.cs` and Infrastructure V0.2 capture/candidate stores | Retained capture validation and descriptive technical qualification, separate from investment decisions | Implemented machinery is not an operational candidate stream; Research may not call promotion |
| `src/IdxStockIntelligence.Infrastructure/ScreenerService.cs`, Application `ScreenerEvaluator.cs`, `ScreenerTechnicalArithmetic.cs` | Existing V0.1 API uses fixed local references, selected bars and shared arithmetic; capture/replay retain their policy | Public `/api/screener` is not proof of a complete V0.2 deployment or a selectable Research/Verified product mode |
| `src/IdxStockIntelligence.Infrastructure/ResearchService.cs`, Application `ResearchEvaluation.cs`, `ResearchDelivery.cs` | Read-only repeatable-read, committed V0.1 captures/Outcomes; pinned dataset identity, bounded pages/export; unresolved observations retained | This is a specific frozen Research dataset, not a generic Fundamental/strategy research engine; source-use closure is additional work |
| Application `PortfolioLedger.cs`, `ProductMarketState.cs`, `PortfolioExchange.cs`; Infrastructure `PortfolioDatabase.cs` | Ledger replay, reconciliation, thesis and exchange exist; `ProductValuation` supports retained-price valuation with coverage/null totals | Operation/profile rights checks are missing on sourced market enrichment/export; the existing exchange format need not be rebuilt |
| `src/IdxStockIntelligence.Infrastructure/StockDatabase.cs`, `frontend/src/StocksPage.tsx` | Retained stock history and price chart exist; detail exposes dates and provenance | Chart is not a missing implementation; rights checks and a consistent indicative mode label would be additive |
| `src/IdxStockIntelligence.Api/Program.cs` | Loopback HTTP only, bounded bodies, same-origin JSON writes, no permissive CORS; market, snapshot, research and portfolio/export routes | No typed deployment/rights/capability gate; local access is not SaaS authorization or tenant isolation |
| `collectors/python/src/idx_stock_collector/pilot.py`, `pilot_daily.py`; `pilot/universe.json`, `collection-policy.json`, `screener-reference.json`, `sessions.json`, `instrument-sessions.json` | Fixed Pilot validation, budget/cadence and retained reference configuration already exist | These files govern their current purposes, not source rights; no general provider-switch configuration is implemented |

Reuse Python for authorized fetch/format parsing, .NET for canonical policy and
financial meaning, PostgreSQL for canonical history, and the archive for authorized
original bytes. Extend these boundaries; do not replace the layers. Fundamental
typed binding, admission, parser and persistence remain proposed/missing. Broad
investment intelligence and broker/foreign-flow processing are not implemented by
the inspected path. Existing ratios/technical arithmetic are not permission checks.

The inspected `PortfolioDatabase.ReadMarketAsync` and `StockDatabase.HistoryAsync`
SQL select canonical revisions by instrument/date/cutoff and return source
provenance; they do not enforce a selected source ID or operation rights. That is
a concrete additive integration gap before multi-provider use, not evidence that
provider switching already exists. Validate the explicit binding against selected
inputs and reject ambiguity; never treat the latest cross-provider row as a
fallback or rewrite existing revision identities to accommodate a new provider.
Any needed selection/persistence contract change requires its own review. The
current chart breaks lines at null rows; do not assume it proves complete missing-
session coverage merely because no null was returned by a retained-row query.

## 3. Deployment profiles — SCP-1

| Profile | Intended use | Necessary permission scope | Deployment boundary |
|---|---|---|---|
| PERSONAL | Single-user local research and private portfolio; no general redistribution | Personal/noncommercial use plus each actual acquisition, retention, processing, display or export operation | Local-first and zero mandatory paid infrastructure; existing loopback security remains |
| SAAS | Future commercial service with multiple consumers | Commercial and SaaS/multi-user use, applicable display/derived-output/export/redistribution rights, plus acquisition/storage rights | Not currently supported; authentication, isolation and deployment hardening require a separate future milestone |

Neither profile grants rights. PERSONAL does not authorize prohibited scraping.
SAAS is an intended-use classification, not an activation flag. A future commercial
service does not always distribute originals, but commercial processing and display
still need applicable rights. Export/redistribution apply to the actual output.

A PERSONAL→SAAS transition needs a fresh dataset-by-dataset, operation-by-operation
eligibility review. Existing personal originals, admitted facts, derived outputs,
snapshots and caches keep their provenance and restrictions. Newly licensing a
provider does not automatically license previously acquired datasets. No bulk
promotion, rebranding or changed mode label creates commercial eligibility.

## 4. Source usage permission model — SCP-1

Keep registry `terms_status` unchanged. Add a separately versioned, typed usage
policy linked by `sourceId` and exact route/terms scope. An existing ALLOWED registry
row remains necessary wherever current ingestion requires it, and insufficient on
its own. UNKNOWN/DISALLOWED never become ALLOWED through an enabled flag, catalog
entry, operator assertion without evidence, or this proposal. No registry row is
inserted/updated here.

Proposed classifications, **not registry enum values**:

| Classification | Meaning | Runtime consequence |
|---|---|---|
| EXPLICIT_PERMISSION | Retained applicable evidence positively grants this scope/operation | Eligible only within scope, established interval and restrictions; other gates still apply |
| EXPLICIT_PROHIBITION | Applicable evidence explicitly prohibits this scope/operation | Deny; enabled/configuration cannot override |
| CONDITIONAL_PERMISSION | Grant is conditional, e.g. prior written consent or attribution | Deny until every applicable condition has retained evidence of satisfaction; then permit only the proven scope |
| UNKNOWN_PERMISSION | Applicable grant, scope, duration or condition cannot be established | Deny the affected operation; never treat silence or visibility as permission |

Small typed dimensions:

| Key | Question answered |
|---|---|
| `personalUse` / `commercialUse` | Is this intended use permitted for this dataset and user/service scope? |
| `automatedAcquisition` | Is this exact unattended transport/method permitted? |
| `historicalRetrieval` / `recurringRetrieval` | Are this history window and recurring cadence permitted? |
| `retainOriginal` | May original bytes be kept now, for this duration and location? |
| `processDerived` | May the proposed parsing/calculation create these outputs? |
| `internalAnalysis` | May retained inputs/outputs be read, analysed or replayed? |
| `display` | May this content be shown to the intended consumer? |
| `export` | May these originals/facts/derived fields be copied out in this form? |
| `redistribute` | May this output be delivered to others in the proposed scope? |
| `saasConsumption` | May multiple service users consume it under this commercial service? |

For unattended historical recurring collection, all three acquisition dimensions
apply. A retained-only read does not require a fresh acquisition grant, but does
require current lawful retention and internal use, processing if recalculated,
and display/export rights as applicable. A private screen is display to the owner;
its scope may differ from public display. Private export is not automatically
redistribution, but export rights are still checked separately. Explicit file or
JSON download/export responses require export rights, including Research
`EXPORT_JSON`; an ordinary display API response is checked for its actual audience
and use rather than classified as export solely because its encoding is JSON.

Proposed retained record (design shape, no DDL): policy version/decision ID;
`sourceId`; upstream provider and route/endpoint/version; dataset/instrument/history
and consumer scope; terms title/version or retained-content identity; documentary
evidence reference/URI and integrity identity where lawfully retained; actual
`reviewedAt`; reviewer/decision reference; classifications for relevant operations;
allowed/restricted output forms; established effective interval or honestly unknown;
typed restrictions/condition evidence; unresolved restrictions; superseded/withdrawal
references. Do not invent a terms effective date from the review date. A source's
website URL alone does not authenticate the exact terms reviewed.

Keep conditions finite and specific: attribution, access-date notice, licensed
account/entitlement, permitted instruments/windows, request/unit ceiling, retained
duration, display audience and output class. If a condition cannot be evaluated
with trustworthy inputs, deny pending review; do not introduce executable rules or
a general policy language. Permission evidence and capability metadata are trusted
operator-reviewed inputs. Public requests may express intent, not change grants,
authority tiers, source semantics or condition satisfaction.

Use two separate clocks: analytical `cutoff` for financial evidence and
`executionAt` for present use eligibility. Historical admission/terms snapshots
explain what was authorized then; they do not guarantee permission to replay now.
Review must actually precede execution and established permission validity must
cover the operation. No generic invented expiry or perpetual grandfathering.
Unknown effective scope or continuing validity for the requested operation denies
that operation until supported. A documented surviving-use grant may apply after
account termination only to the exact rights and datasets it proves.

| Change | Proposed safe behavior |
|---|---|
| Expiry or withdrawal | Stop affected acquisitions, jobs and future uses; check whether any retained-use survival grant applies rather than guessing |
| Terms change | Retain the former decision; mark affected grants pending scoped review; no silent carryover to new terms |
| Commercial prohibition | PERSONAL may continue only within its separately proven rights; SAAS processing/display of affected data is denied |
| Unknown retention | Stop new storage and analytical use of affected retained bytes; restrict access pending resolution; metadata retention itself needs an applicable basis |
| Source disabled | Stop new acquisition/ingestion/read/calculation/output through that selection; do not fall back. Preserve only lawfully retainable history |
| PERSONAL→SAAS | Recheck every input and derived output under commercial scope; reject restricted legacy personal data |

Rights changes do not rewrite financial history, original cutoffs, immutable captures
or terminal Outcomes. Current use can be permission-blocked even when the old result
was reproducible. Disablement is not deletion authorization. Where an actual deletion
duty conflicts with immutable archive/replay requirements, stop affected use, record
the conflict and require a separately approved retention/deletion procedure. Neither
silent deletion nor indefinite prohibited retention is an acceptable resolution.
This unresolved issue can block onboarding a route whose obligations cannot be met;
an archive hash does not override them.

## 5. Explicit source capability catalog — SCP-2

Use a small explicit catalog with stable source IDs and reviewed route versions.
Data families are OHLC, volume, instrument identity, trading status, trading calendar
(scheduled/completed facts distinguished), corporate actions, fundamental filings,
historical financial data, benchmark/index, and optional flow/ownership. Only an
evidence-backed family/field/scope is supported; unspecified is unavailable, not
true. Supported feed delivery is separate from Verified claim admissibility.

Per supported capability retain: evidence reference; endpoint/file/field version;
instruments and stable-identity scope; demonstrated history bounds and gaps;
frequency and cadence; raw/adjusted basis; currency/unit/scale and volume basis;
revision/cancellation support; native publication clock/timezone and unknowns;
coverage/completeness limits; retrieval ceilings; upstream source provenance;
installed adapter/parser identity if any. Unknown publication time stays unknown;
a document title or parser output does not prove original history or chronology.
No broad provider-level `supportsEverything` flag.

| Candidate | Evidence-backed catalog scope from retained reviews | Unsupported / unproved boundary and current disposition |
|---|---|---|
| EODHD (`eodhd`, existing pilot identity) | EOD OHLC/volume and index feed are documented and earlier pilot machinery exists; endpoint-specific conventions/history/limits must be retained | Qualifying personal use is scoped, not an admitted V0.2 price guarantee. JK genuine/non-synthetic/zero-volume behavior, units and continuity remain unresolved; no automatic corporate-action/control/fundamental authority. Commercial rights and surviving retention not established |
| IDX official, per route | Operative identity/listing/board/status/calendar/action artifacts and reporting/disclosure material are documented candidates; website and separately licensed service are distinct routes | Website scraping/crawling prohibited in retained assessment. Complete per-session controls, coverage/history, original format/revisions, permitted unattended route and retention not demonstrated. No production activation |
| KSEI, per document/export route | Security/master metadata, notices, service calendar and action material observed in retained review | A master Status is not exchange TradingStatus; schedule is not completion. Complete action windows, identity history and automated retention rights unresolved; a fixture/inspector is not a production adapter |
| Issuer financial disclosures, per issuer/route | Annual/interim filing indexes and some statement links observed; prospective original FinancialFiling/fact source | No blanket issuer rights or all-field/all-history claim. Publication/revision/units and original files need validation. BBCA/ANTM/GOTO permissions unknown; DSSA prior-consent condition; BFIN copying restriction with filing scope unresolved |
| User-provided files | Explicit bounded file types/fields may be supported by an existing importer or a future separately reviewed parser; user's own ledger input is already supported | Upload does not grant upstream data rights or authority. Require ownership/authorization evidence, original provider identity and format validation; unknown imported market-data rights deny use |
| Research fixtures | Clearly synthetic, authored/authorized fixtures can model the declared shapes in isolated research/test scope | Not market evidence; never Verified admission, a real provider grant or operational readiness. A copied real dataset is not synthetic and keeps upstream restrictions |
| Future licensed provider | Placeholder candidate only; exact signed route/scope, semantics and installed adapter required before capabilities become supported | No provider selected, no plan purchased, no capability or personal/commercial grant established |

EODHD quantitative/history claims remain those of the retained [source ledger](LEVEL1_SCREENER_SOURCE_STACK.md),
not a fresh entitlement check. Reuse fixed Pilot ceilings; a new catalog cannot
enable FullIdx, bulk calls, paid plans or budgets beyond authorization.

Repository roles, reusing [pinned review cards](EXTERNAL_SOURCE_INTELLIGENCE_REVIEW.md)
§3 and [fundamental reuse reconciliation](FUNDAMENTAL_SOURCE_FEASIBILITY_GATE_V0_1.md)
§6; pins/license details remain there rather than being duplicated:

| Repository | Catalog role, not independent data authority | Retained reuse boundary |
|---|---|---|
| NeaByteLab/IDX-API | Collector/discovery technical reference wrapping IDX | Attachment/disclosure envelope questions; not a financial-original parser; retrieval not admissible |
| NeaByteLab/IDX-UI | UI/current-ratio technical reference wrapping IDX | No needed runtime/UI reuse; no filing provenance or admitted acquisition |
| noczero/idx-fundamental-analysis | Secondary metric/collector reference | Stockbit/browser extraction, numeric/missingness flaws and unresolved code-license evidence; inspected pipeline not recommended |
| lugassawan/idxlens | Parser reference; conditional isolated file-handling reuse candidate | ZIP/inline-XBRL/XLSX clues; incomplete financial contexts/units, no financial PDF parser; concealment acquisition excluded |
| lovanto/idx-mcp | XML/context parser reference; conditional reuse candidate | Instant/duration/concept ideas need exact dimensions/units/correction validation; WAF-avoidance acquisition and stale cache unsuitable |
| INo-xious/stockbit-mcp | Secondary Stockbit collector/shape reference | Authenticated internal API requires applicable authorization; no original filing revision chain |
| b2b-web-id/IDX-XBRL-exploreR | Conceptual parser/research example reference | Fact/context/label joins only; code-copy grant unresolved; manual sample does not authorize production collection |
| nichsedge/idx-bei | Collector/disclosure specification reference wrapping IDX | IDs/publication/attachments useful questions; impersonation/denial rotation excluded, overwrite/failure-to-empty semantics unsuitable |

None becomes a `source` row merely because its code is public. A licensed parser
component still takes data from the original provider. Code-license review is
separate from upstream permission and evidence admission. No copying, execution,
new survey or repository refresh occurred in this bundle.

## 6. Source selection and effective configuration — SCP-2

Proposed trusted configuration comprises profile, supported application modes,
requested features, enabled source IDs and **explicit family→source/route/version
bindings**. It references reviewed usage decisions, capability metadata and existing
evidence/admission identities; it does not contain unrestricted permission booleans.
Credentials remain in existing secret handling, never in the catalog/API response.
Unknown IDs/enums, duplicate bindings and incompatible versions fail validation.

Effective operation:

```
feature requested
AND deployment/profile implementation permitted
AND every required source selected and enabled
AND every applicable source operation permitted for this profile/output
AND required capability supported in the exact requested scope
AND applicable evidence gate satisfied
```

This is evaluated for actual inputs, not just the displayed provider name. For a
ledger-only operation the market-source set is empty; no Level-1 source is required.
User-owned input authorization and normal ledger validation still apply. For an
existing API returning several families, either deny the response when a required
input/output is ineligible or use already-defined optional/null behavior; do not
silently remove required evidence from a frozen dataset and recalculate it.

| Family | Explicit selection rule |
|---|---|
| Prices / volume | Choose endpoint/field/version and instrument scope. Validate currency, unit, adjustment, quantity and correction conventions separately; one cannot infer volume rights/units from price |
| Identity | Select authoritative identity intervals and exact stable-ID bindings; ticker/suffix is insufficient |
| TradingStatus | Select governing/admitted scoped control facts; no bar/master-status substitute or negative-search clearance |
| Calendar / completion | Bind schedule/exception and independent completion evidence separately by exchange/session; weekday or bar is not completion |
| Corporate actions | Select event and coverage evidence for the window; absence of events does not establish coverage |
| Fundamental filings/history | Choose issuer/filing and convention/mapping premises with exact original/revision references, period/dimensions and rights; no substituting vendor ratios for official originals |
| Benchmark / RS | Independently select benchmark identity/feed and align cleared sessions with stock window; missing benchmark affects RS/context only |
| Flow / ownership | Separate explicit optional binding; no inferred broker activity from volume or ownership snapshot |
| User inputs / fixtures | Keep user authorization and original upstream identity; fixtures remain visibly synthetic and isolated |

Multiple approved sources may supply distinct required claims (e.g. price, calendar
and action facts). This is explicit evidence composition with traceable premises,
not silent feed blending. Research source comparisons show separate labelled
series; they do not choose a winner or produce Verified clearance. Where the frozen
resolver defines authority/revision precedence, preserve it unchanged.

Switching a family requires a new explicit binding review, rights/semantics
compatibility and applicable admission. Restart affected feature history where the
frozen comparability/recurrence rules require; no joining incompatible windows,
rescaling by guess, reassignment of old source IDs, or rewriting captures. Disabled,
denied or missing bindings fail closed; never probe another provider automatically.

## 7. Research and Verified boundaries — SCP-3

RESEARCH and VERIFIED are proposed **application usage modes**, not replacements
for `EvidenceQuality`, `marketEligibility`, `featureState`, existing Research policy
IDs or frozen readiness states. Mode selection does not alter persisted facts or
the identity of existing responses. Legacy `MarketQuality.VERIFIED` from canonical
VALID bars is not a declaration that the complete V0.2 funnel passed.

RESEARCH can inspect legally retained history, charts, indicators, fundamentals,
portfolio state, diagnostics, source comparisons and explicitly experimental
strategies only to the extent implemented and permitted. No research path obtains
blocked material, invents facts, changes INVEST mandates or calls candidate
promotion/automatic trading. Existing committed-outcome research remains governed
by its own frozen population, arithmetic and transport contract.

Preserve per result/field: source and upstream identity, original evidence/revision
references, through/economic period, exact knowledge cutoff, publication if known,
retrieval/known clocks, quality/freshness, missing inputs, history gaps, currency/unit
and adjustment basis, formula/version/assumptions, and an **INDICATIVE / RESEARCH**
label. Stale values retain their real dates. Unsupported numeric conventions or
missing required operands yield null/unavailable, never zero or guessed units.
Exploratory comparisons of incomplete data must expose incompleteness and avoid
turning survivorship or later knowledge into historical truth.

VERIFIED requires the applicable existing contract gates: source admission,
identity/type/listing, board/mechanism, TradingStatus, scheduled/completed session,
GenuinePriceObservation, action coverage/comparability, required history and PIT/
revisions. Source permission is necessary in addition to those gates. Preserve
BLOCKED/UNKNOWN consequences and exact frozen enum vocabulary; there is no new
financial readiness state called permission-ready.

V0.2 §§12–16 already allow independently supported diagnostic features while
eligibility is unproved. These diagnostics cannot advance the funnel:
`setupEvaluated=false`, no TECHNICAL_EVALUATED/DECISION_CANDIDATE promotion. Optional
volume, benchmark/RS and market context stay feature-local; missing optional data
does not globally block stock-only features. Conversely RESEARCH is not authority
to relax recurrence/comparability in existing Verified calculators. A materially
different experimental formula requires separate labelled research semantics;
the present proposal approves none.

## 8. Feature capability matrix — SCP-3

Permission shorthand: **U** = applicable personal or commercial use; **R** = current
lawful retention + internal analysis; **D** = derived processing; **V** = display;
**X** = export; **Z** = redistribution when delivered to others; **S** = commercial
and SaaS consumption. Acquisition permissions apply only if new acquisition is
requested; all actual input providers are checked. User-owned ledger authorization
is not a grant over imported market data.

The permission/availability columns describe proposed eligibility over the listed
existing or future features; the typed gate is not implemented today. The current
implementation column records code separately, not new operational acceptance.
**All SAAS entries are currently unavailable** because
SaaS deployment/security is deferred; the last column records the additional
future rights requirement. Modes refer to feature use, never promote an existing
V0.1 endpoint to V0.2 certification.

| # / capability | Required families / permissions | Evidence and missing-data behavior | Mode | Current implementation | PERSONAL / exact limit | Future SAAS requirement / current reason |
|---|---|---|---|---|---|---|
| 1 Portfolio ledger | User transactions/theses; authorized private input, U/R/V | Ledger identity, chronology, exact arithmetic/idempotency; invalid events rejected; prices not required | RESEARCH; same ledger usable alongside VERIFIED | Implemented replay, append, reconciliation, thesis, import/export | Available for authorized records; no Level-1 dependency; unknown third-party input rights still denied | User/account authorization and isolation; currently unsupported deployment |
| 2 Portfolio valuation | Ledger + retained close/identity; U/R/D/V | Existing ProductValuation price/cutoff/quality checks; stale date/age retained; absent or ineligible price gives null valuation, PARTIAL coverage, no invented total | RESEARCH; not a Verified signal | Implemented canonical retained-price enrichment/valuation | Conditional on permitted usable bars; ledger quantities/cost remain visible if market enrichment unavailable | S plus all price/display/derived rights; legacy personal bars not eligible automatically |
| 3 Technical charts | Retained OHLC/identity; U/R/V, D for transformations | Actual observations, chronology, gaps/quality visible; no synthetic candles or gap bridging; insufficient observations retain table/empty chart | RESEARCH | Stock history and PriceChart implemented | Conditional on retained-use/display rights and usable rows; source policy gate not implemented | S/display rights for every series; current SaaS unsupported |
| 4 Indicative technical indicators | Scoped price/window; optional quantity/benchmark; U/R/D/V | Defined arithmetic/seeds, basis and available operands; null/WARMUP for missing history; diagnostics do not advance funnel | RESEARCH | Shared arithmetic and V0.2 diagnostic path exist; general mode/config not implemented | Conditional; denied rights or unsupported basis prevents calculation; no research candidate | S/derived/display rights; rights and calculation correctness independently required |
| 5 Verified screener evaluation | Mandatory V0.2 control/price/action/identity families; U/R/D/V | Frozen admission, eligibility, completion, genuine price, comparability, PIT/history; unchanged DATA_BLOCKED/UNKNOWN and reasons | VERIFIED | V0.2 binding/readiness/technical machinery implemented; public screener uses V0.1 | BLOCKED for operational V0.2: exact source permissions and mandatory evidence unmet | S plus full input/output rights; same mandatory evidence and deferred service security |
| 6 Candidate promotion | Exact retained V0.2 technical capture and premises; U/R/D/V | Frozen capture identity, eligible/data-ready technical qualification; no promotion from diagnostics/unadmitted inputs | VERIFIED only | Promote and capture/candidate stores implemented | BLOCKED: no admissible operational Level-1 path; descriptive technical candidate is not investment advice/execution | S for original and derived capture; same evidence plus deferred service security |
| 7 Fundamental statement research | Permitted original filing/facts/convention; U/R/D/V where parsing | Exact entity/period/dimensions, currency/unit, original revision/pub/known provenance; missing fact remains unknown | RESEARCH | Binding is proposal only; no production fundamental parser/store/reader | BLOCKED production acquisition; isolated authorized/synthetic development can proceed after scope/contract review | S plus filing/derived/display rights; originals/history and parser validation still required |
| 8 Fundamental derived calculations | Compatible admitted/labelled facts + approved formula; U/R/D/V | Exact units/periods, missing/zero-denominator handling, formula/input refs; no undefined TTM/FCF/valuation formula | RESEARCH; future verified-derived contract only if separately approved | Deferred formulas; no completed fundamental calculation pipeline | Unavailable until formula approval/implementation and permitted compatible inputs; no automatic ratio promotion | S plus derived/output rights; no commercial redistribution by default |
| 9 Investment intelligence | Requested mixed evidence families/approved investment rules; U/R/D/V | Missing fundamentals/assumptions block the affected assessment; deterministic meaning precedes explanation | RESEARCH experiments only; VERIFIED investment rules unapproved | Thesis tracking exists; broad classifier/recommendation system deferred | Broad intelligence unavailable: unapproved meaning and missing inputs/implementation; thesis remains usable | Separate commercial feature and rights/security approval; currently deferred |
| 10 Corporate Action analysis | Event/identity; coverage for clearance; U/R/D/V | A real notice may support research inspection; complete admitted coverage required for clearance; missing ≠ no action | RESEARCH; VERIFIED clearance only under frozen rules | V0.2 event/coverage/comparability machinery exists; no admitted automated route/full analysis UI | Research conditional on authorized retained notices; verified clearance BLOCKED by rights/coverage premises | S plus event/derived/display rights and same coverage requirements |
| 11 Benchmark / relative strength | Stock + benchmark prices, identity and aligned windows; U/R/D/V | RS20/60 frozen arithmetic and aligned cleared history in Verified use; missing benchmark affects RS/context only | RESEARCH; applicable VERIFIED optional diagnostics | Arithmetic and optional feature gates implemented; operational evidence unresolved | Conditional; BENCHMARK_MISSING/history/basis rights can make only RS unavailable | S and rights for both feeds/derived display; current SaaS unsupported |
| 12 Broker / foreign flow | Exact optional flow/ownership feed and units; U/R/D/V | Explicit participant/aggregation/unit/history meaning; no volume-derived broker inference | RESEARCH initially; verified flow contract absent | Deferred; no inspected production path | Unavailable: source rights/semantics and implementation absent; no Level-1 dependency imposed elsewhere | S plus feed/derived/output license; separately scoped future work |
| 13 Data export | Actual exported ledger, originals, facts or derived results; U/R/X; D if derived, Z if redistributed | Preserve identity/provenance/limits; frozen Research dataset bytes/preimage unchanged; deny ineligible export as a whole | RESEARCH or permitted VERIFIED artifacts; export grants no certification | Portfolio JSON/CSV and Research JSON export implemented; fine-grained usage checks missing | Own ledger export supported; sourced/derived export conditional, separately checked from display | S/X/Z as applicable for every input/output, user authorization; currently unsupported deployment |
| 14 Future SaaS API access | Every actual requested family; S/R/V plus D/X/Z as applicable | Server checks all input/output rights and feature-specific evidence; never expose credentials or personal-restricted datasets | RESEARCH and VERIFIED only after separate deployment approval | Not implemented; local API is not SaaS | Not a PERSONAL service feature | Unavailable: SaaS hardening and scoped commercial grants absent; catalog flag cannot enable it |

Existing ledger-only reads should remain useful even when market enrichment is
blocked. A future implementation may use existing null/coverage fields for optional
valuation; it must not return restricted source prices in the same response. Where
current endpoint contracts require inseparable restricted fields, deny that endpoint
or design a separately reviewed version, rather than changing frozen output meaning.

## 9. Server enforcement points — SCP-3

One typed evaluator in the canonical .NET layer should produce the effective
operation decision from trusted policy/capability inputs. Integrate it at actual
boundaries, not as a replacement financial calculator. Python acquisition needs
the same reviewed grant/scope before network access; no second divergent rights
engine or Python-owned canonical financial policy is proposed. Exact transport
of that decision is an implementation-review question, not a new daemon here.

| Boundary | Proposed check | Existing behavior preserved |
|---|---|---|
| Acquisition | Before any discovery/download, require enabled explicit route, actual entitlement, automated/history/recurring rights, bounded scope/cadence/units and storage permission for intended bytes | Existing Pilot/FullIdx, budget, entitlement and no-bypass controls; no acquisition from an API read |
| Ingestion | Before retaining bytes/semantic commit, check source/dataset/profile/operation rights, exact mapping capability, applicable current registry gate; then existing typed binding/admission checks | BoundedEvidenceIngestion validation and transactional batches; allowed rights do not admit an invalid row |
| Storage | Retain only within supported duration/location/output constraints; associate original provider/rights decision with existing artifact/receipt/evidence identity | Content integrity and append-only revisions; no undocumented automatic purge |
| Read / calculation | Check present retained-use rights for every required source and derived premise; then run the correct feature/economic cutoff gate | As-of/revision selection and calculation semantics unchanged; no live provider recovery |
| API | Check operation/profile/audience before materialization and immediately before releasing output; service calls enforce it independently of UI | Loopback-only deployment and same-origin/bounds remain; no toggle exposes public SaaS |
| Export | Check export and actual redistribution/commercial scope independently, including derived inputs; no partial silent stripping of frozen Research datasets | Existing portfolio formats and Research identity/cursor/limits remain |
| Frontend | Display requested/effective availability, mode, public reasons and provenance; disable unavailable actions for clarity | UI hiding is supplementary; crafted requests still denied by server |

Snapshot a reviewed policy version per operation; if a change/withdrawal is detected
before acquisition, storage or output release, fail closed and re-evaluate rather
than allowing a stale grant. Long-running jobs must stop on known revocation at
their next operation boundary. Caches must be gated on each delivery, not just at
creation. Exact synchronization/version retention is still an open design decision;
no claim of an implemented atomic revocation protocol is made.

Do not insert the new usage decision into frozen capture hashes, Research preimages
or financial readiness enums. Record usage decisions outside frozen financial
artifacts if an approved implementation needs an audit receipt. Existing response
contracts are either delivered unchanged when eligible or denied at the operation
boundary. A new capability/status API may report usage reasons separately, after
its transport is reviewed; it cannot accept permissions supplied by a browser.

## 10. Provenance and derived-data rights — SCP-3

For original→normalized→calculated→displayed/exported chains retain upstream
source/route, artifact hash and receipt, evidence IDs/revisions, original clocks,
scope/convention, formula and input references, and applicable usage-decision IDs.
The catalog's collector/parser role does not replace the provider. Derivation
permission is not automatically display, export or redistribution permission.

All required inputs must permit the actual operation. A mixed summary, indicator,
ratio, portfolio analytic, cache or future SaaS report inherits relevant input
restrictions unless retained applicable evidence establishes an exception. Aggregation,
paraphrase, an LLM explanation, changing file format, or omitting the provider name
does not establish that exception. Unknown derived commercial/display/export rights
deny those uses, independently of mathematical correctness.

An authorized user ledger may be exported independently of sourced market enrichment
using its existing ledger exchange format. Do not bundle prices/ratios whose export
rights are absent. A synthetic fixture's clearly authored data needs no grant from
the real provider it models, but cannot be relabelled as authentic market evidence.

Existing Research V0.1 membership is independent of verification verdict. Usage
gating must not silently drop restricted or unverified rows, rerank observations,
change denominators, recalculate cohort statistics, rewrite preimages/dataset IDs,
or substitute new Outcomes. When the required provenance/rights closure cannot be
proved for a frozen dataset, deny delivery of that dataset; return a separate usage
failure. Approved new research populations would require their own explicit contract.

Rights can change at execution time without changing the historical numerical
result. Preserve that distinction in replay: current permission-blocked is not an
Outcome correction. Metadata or original retention is conditional on actual lawful
retention; the unresolved deletion conflict in §4 remains a separate blocker.

## 11. Illustrative configurations — SCP-4

These five JSON blocks are **synthetic design examples**, not files to install,
production-schema definitions or real permission/admission evidence. `example.*`
source IDs and `synthetic-*` decision references identify authored test scenarios.
They do not exist in the live registry. `catalog` and `usageEvidence` abbreviate
trusted separately reviewed records, not public writable permission assertions.
Omitted permissions are UNKNOWN; omitted family selections are unselected. All
examples specify no fallback, no network acquisition and no real provider grant.
Exact semantics/intervals below are invented solely for the fixture scenario.
Each `synthetic-*` decision reference resolves, in this illustrative scenario,
to a complete fixture record with retained synthetic terms/scope evidence,
reviewed at 2026-10-08T02:00:00Z and valid for the depicted use from that instant
until 2026-10-10T02:00:00Z. Execution is 2026-10-09T02:00:00Z. The fixture record
proves only the explicitly listed operations/conditions over the stated synthetic
dataset; all other grants remain unknown. D's withdrawal supersedes its former
grant. This abbreviation is not permission to accept incomplete real records.

### A. PERSONAL research with owned ledger and synthetic retained prices

```json
{
  "exampleOnly": true,
  "profile": "PERSONAL",
  "mode": "RESEARCH",
  "featuresRequested": ["portfolioLedger", "technicalCharts"],
  "enabledSources": ["example.authored-fixture"],
  "familySelection": {"ohlc": "example.authored-fixture", "instrumentIdentity": "example.authored-fixture"},
  "fallback": "NONE",
  "acquisitionRequested": false,
  "catalog": {"example.authored-fixture": {"kind": "SYNTHETIC", "capabilities": ["ohlc", "instrumentIdentity"], "scope": "synthetic-equity/2026-09", "basis": "RAW", "currency": "IDR", "priceUnit": "IDR_PER_SHARE", "revisionSupport": "FIXTURE_APPEND_ONLY"}},
  "usageEvidence": {"example.authored-fixture": {"decisionRef": "synthetic-owned-A", "personalUse": "EXPLICIT_PERMISSION", "retainOriginal": "EXPLICIT_PERMISSION", "internalAnalysis": "EXPLICIT_PERMISSION", "display": "EXPLICIT_PERMISSION", "export": "EXPLICIT_PROHIBITION", "redistribute": "EXPLICIT_PROHIBITION"}},
  "evidence": {"synthetic": true, "cutoff": "2026-10-09T02:00:00Z", "sourceRefsRetained": true, "verifiedAdmission": false},
  "output": {"label": "INDICATIVE / RESEARCH", "exportRequested": false}
}
```

Expected: owned ledger works without market evidence. A chart may display actual
synthetic rows with provenance/quality/history labels; no promotion or export.
Synthetic identity never enters the production Verified pipeline. A chart does
not require calculation permission unless a transformation is requested.

### B. PERSONAL with a separately permitted hypothetical provider

```json
{
  "exampleOnly": true,
  "profile": "PERSONAL",
  "mode": "RESEARCH",
  "featuresRequested": ["indicativeIndicators"],
  "enabledSources": ["example.personal-provider"],
  "familySelection": {"ohlc": "example.personal-provider"},
  "fallback": "NONE",
  "acquisitionRequested": false,
  "catalog": {"example.personal-provider": {"capabilities": ["ohlc"], "scope": "synthetic-equity/50-retained-sessions", "basis": "RAW", "currency": "IDR", "priceUnit": "IDR_PER_SHARE", "revisionSupport": "SCOPED_APPEND_ONLY"}},
  "usageEvidence": {"example.personal-provider": {"decisionRef": "synthetic-scoped-personal-B", "reviewedAt": "2026-10-08T02:00:00Z", "validFrom": "2026-10-08T02:00:00Z", "validUntil": "2026-10-10T02:00:00Z", "personalUse": "EXPLICIT_PERMISSION", "commercialUse": "EXPLICIT_PROHIBITION", "retainOriginal": "EXPLICIT_PERMISSION", "internalAnalysis": "EXPLICIT_PERMISSION", "processDerived": "EXPLICIT_PERMISSION", "display": "CONDITIONAL_PERMISSION", "condition": "ATTRIBUTION", "conditionEvidenceRef": "synthetic-attribution-B", "export": "EXPLICIT_PROHIBITION", "redistribute": "EXPLICIT_PROHIBITION", "saasConsumption": "EXPLICIT_PROHIBITION"}},
  "executionAt": "2026-10-09T02:00:00Z",
  "evidence": {"synthetic": true, "sourceRefsRetained": true, "formulaVersionRetained": true, "historyCompleteForRequestedFormula": true, "verifiedAdmission": false},
  "output": {"label": "INDICATIVE / RESEARCH", "exportRequested": false}
}
```

Expected: only supported indicative calculations run while the scoped rights and
attribution condition hold; evidence/arithmetic validation remains required. No
export, unattended download or SaaS use. This example is neither EODHD approval nor
a real licensed route; acquisition permissions intentionally remain unknown.

### C. Future SAAS using commercially eligible synthetic scenario data only

```json
{
  "exampleOnly": true,
  "profile": "SAAS",
  "mode": "RESEARCH",
  "featuresRequested": ["technicalCharts"],
  "enabledSources": ["example.commercial-provider"],
  "familySelection": {"ohlc": "example.commercial-provider", "instrumentIdentity": "example.commercial-provider"},
  "fallback": "NONE",
  "acquisitionRequested": false,
  "catalog": {"example.commercial-provider": {"capabilities": ["ohlc", "instrumentIdentity"], "scope": "synthetic-licensed-dataset-C", "basis": "RAW", "currency": "IDR", "priceUnit": "IDR_PER_SHARE"}},
  "usageEvidence": {"example.commercial-provider": {"decisionRef": "synthetic-commercial-C", "commercialUse": "EXPLICIT_PERMISSION", "saasConsumption": "EXPLICIT_PERMISSION", "retainOriginal": "EXPLICIT_PERMISSION", "internalAnalysis": "EXPLICIT_PERMISSION", "display": "EXPLICIT_PERMISSION", "export": "EXPLICIT_PROHIBITION", "redistribute": "EXPLICIT_PROHIBITION", "scope": "LICENSED_USER_DISPLAY_ONLY"}},
  "legacyPersonalDatasetEligible": false,
  "deploymentSupported": false,
  "evidence": {"synthetic": true, "sourceRefsRetained": true, "verifiedAdmission": false},
  "output": {"exportRequested": false}
}
```

Expected **today**: UNSUPPORTED_PROFILE even though the hypothetical scoped display
grant is positive. After a separately approved SaaS milestone, only exact licensed
display to eligible users could proceed; originals/export/general redistribution
remain denied. `deploymentSupported` is descriptive trusted implementation state,
not a user-editable switch to claim isolation/security exists. No personal dataset
may be substituted. Any display scope requiring redistribution must independently
prove that right; this hypothetical grant only covers licensed-user display.

### D. Enabled source with revoked or unknown use rights

```json
{
  "exampleOnly": true,
  "profile": "PERSONAL",
  "mode": "RESEARCH",
  "featuresRequested": ["technicalCharts"],
  "enabledSources": ["example.revoked-provider"],
  "familySelection": {"ohlc": "example.revoked-provider"},
  "fallback": "NONE",
  "acquisitionRequested": false,
  "catalog": {"example.revoked-provider": {"capabilities": ["ohlc"]}},
  "usageEvidence": {"example.revoked-provider": {"decisionRef": "synthetic-withdrawal-D", "supersedes": "synthetic-previous-grant-D", "personalUse": "EXPLICIT_PERMISSION", "retainOriginal": "UNKNOWN_PERMISSION", "internalAnalysis": "EXPLICIT_PROHIBITION", "display": "UNKNOWN_PERMISSION", "withdrawalKnownAt": "2026-10-09T01:00:00Z"}},
  "executionAt": "2026-10-09T02:00:00Z",
  "evidence": {"synthetic": true, "historicalCutoff": "2026-10-08T02:00:00Z", "sourceRefsRetained": true},
  "output": {"exportRequested": false}
}
```

Expected: PERMISSION_PROHIBITED for current internal use, with unknown retention
recorded separately; no acquisition/read/output or fallback. Earlier analytical
cutoff does not bypass withdrawal. Preserve only lawfully retainable provenance;
stop use and resolve retention obligations separately, with no automatic deletion.

### E. Permitted retained source, Verified feature still blocked

```json
{
  "exampleOnly": true,
  "profile": "PERSONAL",
  "mode": "VERIFIED",
  "featuresRequested": ["verifiedScreener"],
  "enabledSources": ["example.permitted-price-only"],
  "familySelection": {"ohlc": "example.permitted-price-only"},
  "fallback": "NONE",
  "acquisitionRequested": false,
  "catalog": {"example.permitted-price-only": {"capabilities": ["ohlc"], "scope": "synthetic-price-window-E", "basis": "RAW", "currency": "IDR", "priceUnit": "IDR_PER_SHARE"}},
  "usageEvidence": {"example.permitted-price-only": {"decisionRef": "synthetic-personal-E", "personalUse": "EXPLICIT_PERMISSION", "retainOriginal": "EXPLICIT_PERMISSION", "internalAnalysis": "EXPLICIT_PERMISSION", "processDerived": "EXPLICIT_PERMISSION", "display": "EXPLICIT_PERMISSION"}},
  "evidence": {"synthetic": true, "sourceRefsRetained": true, "verifiedAdmission": false, "tradingStatus": "UNKNOWN", "completedSession": "UNKNOWN", "priceComparability": "UNRESOLVED"},
  "output": {"exportRequested": false}
}
```

Expected: usage permission passes only for selected retained prices. Missing control
bindings/capabilities prevent a complete operation; native evidence also cannot
establish DATA_READY, `setupEvaluated=true` or promotion. Capability status can report
CAPABILITY_MISSING and applicable diagnostic native reasons (STATUS_UNKNOWN,
SESSION_UNCONFIRMED, PRICE_UNRESOLVED); no admitted evidence is fabricated. The
isolated fixture demonstrates gates, not production readiness. Test T05 separately
isolates allowed-but-unadmitted evidence with all declared capabilities present.

## 12. Deterministic failure behavior — SCP-3

The following are **proposed usage-layer reason codes**, not additions to frozen
financial enums/reasons. Report the denied operation and a safe source alias/data
family, not tokens, account IDs, connection strings, filesystem paths, secret terms
attachments or billing details. Operator diagnostics can reference a local review
decision securely. No network retry/fallback is implied by any reason.

| Condition | Proposed outcome |
|---|---|
| Source disabled | SOURCE_DISABLED; stop affected operation; keep independent owned ledger usable |
| Applicable permission unknown / missing decision | PERMISSION_UNKNOWN; no affected bytes/calculation/output |
| Explicit prohibition / withdrawn operation | PERMISSION_PROHIBITED; flags and historical cutoff cannot override |
| Entitlement / established permission interval expired | PERMISSION_EXPIRED; require new review, not automatic account recovery |
| Capability unsupported / family unselected | CAPABILITY_MISSING; do not probe or substitute another provider |
| Required evidence unadmitted | Usage gate may pass; applicable native admission/readiness fails unchanged, e.g. EVIDENCE_SOURCE_INADMISSIBLE / PRICE_NOT_ADMITTED |
| Stale evidence | Use native freshness rules; permitted research retains stale date/label where supported; Verified scope failure stays EVIDENCE_STALE/STATUS_STALE/etc.; no universal age limit |
| Conflicting sources | Native conflict precedence for Verified; unresolved affected dimension remains CONFLICTING. Research may show separate labelled observations, not silently reconcile them |
| Incomplete history | Required calculation null/WARMUP/UNAVAILABLE with native reason; partial inspection remains labelled if rights allow; no fabricated rows |
| Unsupported profile or service deployment | UNSUPPORTED_PROFILE; malformed profile is configuration-invalid before execution; SAAS cannot bypass local security |
| Export unpermitted | EXPORT_NOT_PERMITTED plus underlying permission classification; display permission does not grant download |

For stable public diagnostics, validate configuration first, then feature request,
supported deployment, enabled binding, usage rights, capability and applicable
evidence. Within usage rights prioritize explicit prohibition, expired eligibility,
then unknown/unsatisfied condition. For multiple affected sources use stable
source-ID and operation-key ordering. Export denial remains EXPORT_NOT_PERMITTED
with a non-sensitive underlying reason. Conditions with no proven satisfaction
deny as PERMISSION_UNKNOWN. These ordering choices need review before implementation.

Do not compute/serialize restricted values merely to produce a helpful diagnostic.
If rights fail, return a usage denial outside the frozen financial result. When
rights pass and evidence fails, the existing calculator may return its blocked
result and permitted diagnostics exactly as the applicable contract permits.
Feature-local failure does not disable unrelated permitted families or ledger work.

## 13. Deterministic future test specification — SCP-4

**Specifications only: no tests implemented or run for these future gates.** Use
authored fixture sources, fixed execution/cutoff clocks and zero provider calls.
Keep existing standard .NET/Python discovery for later implementation. Tests should
exercise observable decisions and absence of side effects, not mirror each branch.

| ID | Fixed scenario | Required assertion |
|---|---|---|
| T01 | Personal-only grant, SAAS request in a future supported-deployment fixture | Commercial/SaaS use denied; neither existing personal bytes nor derived cache delivered |
| T02 | Source enabled, unattended method explicitly prohibited, historical/recurring grants otherwise positive | No discovery/download call; PERMISSION_PROHIBITED; no raw write |
| T03 | Required permission UNKNOWN or absent | PERMISSION_UNKNOWN; no read/calculation/output side effect |
| T04 | Source disabled, positive rights and supported capabilities | SOURCE_DISABLED; no acquisition/read/fallback; independent ledger remains usable |
| T05 | Rights all positive, complete declared family catalog, required evidence unadmitted | Existing V0.2 admission/readiness failure unchanged; setup not evaluated; no promotion |
| T06 | RESEARCH request over prohibited/unknown retained-use source | Same rights denial as applicable Verified use; indicative label does not grant access |
| T07 | Display allowed, export denied; direct portfolio/research download request | EXPORT_NOT_PERMITTED, zero export bytes; eligible display result unchanged |
| T08 | Derived ratio/indicator/cache with two source inputs | Both source/artifact/revision/formula refs retained; one denied input blocks derived delivery; aggregation is no rights exception |
| T09 | Explicit selected-source switch, incompatible convention or unadmitted new provider | No fallback/old source reassignment/window stitching; require reviewed binding/admission; original capture unchanged |
| T10 | Historical admission positive, current withdrawal/expiry and old query cutoff | Future read/replay/output denied; no rewrite/delete of captures/Outcomes; known change before release prevents delivery |
| T11 | Permitted stale point observation and old completed-session fact | Research preserves original date/STALE for the point; old completed-session fact does not expire solely by age; Verified uses native scope rules |
| T12 | UI hides export, caller invokes API/service directly | Server denies without UI state; safe reasons expose no credentials/paths |
| T13 | PERSONAL dataset copied into commercial deployment with new provider grant | Dataset-specific surviving/commercial rights required; copy/mode switch alone cannot authorize original or derived output |
| T14 | Existing V0.2 golden eligibility/readiness/candidate cases with usage gate passed | Exact enum/reasons, seeds, recurrence, optional-feature locality, cutoff/revision/funnel behavior unchanged |
| T15 | Conditional attribution/prior-consent grant, missing or wrong-scope proof | Denied; only exact satisfied condition/interval can permit; enabled flag not proof |
| T16 | Rights-denied member of a frozen committed-outcome Research dataset | Entire required delivery denied; membership/denominators/preimage/dataset bytes not silently changed; verification remains independent |
| T17 | Retention obligation incompatible with immutable replay | No automatic deletion or prohibited continued use; review conflict exposed and source onboarding/use blocked as required |
| T18 | Benchmark rights/history absent but stock-only dependencies valid | Only RS/context unavailable; optional absence does not globally DATA_BLOCK stock-only features |

Later changes at shared permission/persistence boundaries need relevant regression
and owned disposable HTTP/database acceptance under the execution rules. This
documentation milestone has no production logic requiring those suites.

## 14. Minimal implementation slices and roadmap — SCP-4

Statuses below are planning labels, not readiness enums or authorization. Model/
effort suggestions describe the review shape of these bounded tasks, not claims
about currently available model products. No slice is implemented by this proposal.

| Slice / status | Objective and dependencies | Contracts / permission boundary | Acceptance and deterministic checks | Suggested model / effort |
|---|---|---|---|---|
| SCP-I1+I2 — CONDITIONAL | One typed deployment/usage decision and explicit capability catalog; consolidate policy/catalog rather than separate frameworks. Depends on review of §16, then explicit implementation scope | Approved additive source-policy contract; existing registry/source admission/binding unchanged. Authored fixtures need no external provider activation; real catalog grants need documentary rights | Strict unknown/prohibition/condition/interval behavior; explicit family/version binding, no public override; T01–T06/T09/T13/T15 | Sol / High for bounded implementation; Astra / High for policy/semantic review |
| SCP-I3+I5 — CONDITIONAL | One server evaluator integrated into selected existing acquisition/ingestion/read/API/export boundaries, plus minimal read-only capability exposure. Depends on I1+I2; integrate a bounded existing feature first | Review usage-denial transport separately from frozen result schemas; preserve local security and existing registry gate. Live operations need route-specific rights/operational authorization, not just code | Direct service/API denial, no unauthorized fetch/write/output, deterministic reasons, configuration cannot enable SaaS/FullIdx; T02–T07/T10/T12/T14/T18 | Sol / High; Astra / High review where shared contract boundaries are touched |
| SCP-I4 — CONDITIONAL | Reuse existing stock/portfolio/Research readers and arithmetic; attach indicative provenance/usage gating at permitted new delivery boundaries. Depends on I1+I2 and selected I3+I5 service integration | Frozen Research population/identity/transport remain unchanged; lineage/usage metadata additive outside frozen preimages; Fundamental extension requires its own approved binding/formulas | T08/T11/T13/T16; existing chart/ledger work not rebuilt; no candidate promotion; rights-denied required Research dataset denied intact | Sol / High; Astra / High for Research identity and financial semantics review |
| SCP-I6 — DEFERRED | Future SaaS deployment hardening only when product/commercial need exists; depends on proven scoped commercial grants and prior slices | Separate auth, isolation, audience, service deployment and output contracts; commercial retention/display/export and legacy-data eligibility must be proved | Isolated-user/direct-API acceptance and T01/T12/T13; no local profile toggle to public service; no tenant/billing infrastructure now | Astra / High for scoped design review; Sol / High for subsequently authorized bounded implementation |

No speculative storage change is necessary for this document or initial pure
evaluator. Trusted versioned local policy/catalog records can be proposed first;
historical usage audit persistence, if required, needs a separately approved
additive design. Do not mutate existing migration files or frozen artifacts.

Smallest useful sequence:

1. Continue existing authorized ledger/reconciliation/thesis and retained-only
   inspection. Validate actual input rights; do not require complete Level-1 for
   ledger or all research. A rights problem affects the sourced enrichment, not
   independent user records.
2. Review this policy's open decisions and approve a narrowly versioned I1+I2
   contract before implementation. Begin fixture-only policy work and one existing
   permitted personal feature; broader source onboarding stays blocked.
3. Integrate I3+I5 for that feature and export, then I4 provenance/indicative display.
   Share the evaluator; do not rebuild the chart, valuation or frozen Research
   engine. Apply gates to sibling/direct-service callers, not just one UI route.
4. In an independent track, perform final Fundamental binding review and obtain
   explicit freeze approval. Source-independent pure binding/serialization/period/
   revision tests and synthetic design work need not wait for IDX permission,
   but still require approved contract and implementation scope. Fundamental
   acquisition/adapters/production load remain BLOCKED until a permitted route,
   original coverage and semantics exist. Deferred TTM/FCF/investment formulas
   are not included by a statements-only freeze.
5. Reopen only the exact source questions in §15 when new permission/evidence is
   available and contact is explicitly authorized. Admission and operational
   deployment are separate subsequent gates. Do not add a scheduler or paid plan
   to compensate for missing source truth.
6. Keep I6 deferred. Commercial enablement later requires actual scoped grants,
   per-dataset eligibility and service hardening; it need not delay useful permitted
   personal development now.

Source-independent proposal/final-review preparation is READY. I1–I5 remain
CONDITIONAL on design approval and explicit implementation scope, not on an IDX
scraping exception when using authored fixtures. Real Level-1/fundamental acquisition
is BLOCKED by external permissions/evidence. SaaS hardening and broad investment/
flow features are DEFERRED. These distinct statuses must not be collapsed.

## 15. Current unresolved permissions and external dependencies

Retained evidence only; consult the dated ledgers for exact terms, pins and failed
checks. No current rights were reverified or providers contacted in this bundle.

| Source/dependency | Retained blocker | Evidence required to reopen |
|---|---|---|
| IDX website automation | Scraping/crawling prohibited; public noncommercial use is conditional and does not authorize archive automation | Written exact-route exception or separate authorized delivery service; actual personal/commercial, retention, derived/display/export scope |
| Separate IDX financial Data Reference / older TICMI route | Service availability does not prove free individual entitlement, formats/history, quota or durable rights | Scoped offer/agreement and original coverage/revision/publication terms; zero-fee status cannot be assumed |
| KSEI control/action/identity candidates | Retained-use/automated delivery permission and complete action-window/identity semantics unresolved | Exact lawful route, retention and authoritative scope/coverage/correction proof; not generic approval of master Status |
| EODHD JK | Scoped qualifying private-use terms do not close genuine/non-synthetic/zero-volume/unit/continuity premises or SaaS rights | Endpoint/version semantic proof and exact grant; actual account entitlement/limits; surviving retention and commercial scope if requested |
| Issuers | BBCA/ANTM/GOTO applicable acquisition/retention unknown; DSSA consent prerequisite; BFIN restriction with filing-specific scope unresolved | Issuer-specific permission for recurring/history/original archive/private parsing/continued use and exact originals/metadata; no blanket issuer rule |
| Stockbit wrappers | Applicable authorization for authenticated/internal extraction absent; secondary metrics lack official-original lineage | Authorized documented route and scoped data-use rights, then evidence semantics; GitHub MIT is insufficient |
| Parser reuse | Code-license/dependency notices and exact selected-file correctness review; some grants unresolved | Verify retained pin/license before copying, validate original period/context/unit/revision meaning; keep acquisition methods excluded |
| Dataset operational admission | Current retained assessments report no complete mandatory admitted stack; no live registry/schema audit performed here | Separate authorized source admission and deployment/evidence audit; configuration is not a substitute |

No permission inquiry was sent. Existing targeted drafts remain prepared only.
Internal policy code cannot create an absent external grant or authoritative fact.
Missing adapters are internal work after prerequisites, not independent legal
blockers. A manual original or user upload is not an unattended route and cannot
launder upstream restrictions.

## 16. Open design decisions and review gates

These are recommendations, not user/reviewer acceptance or a frozen policy:

| Decision | Proposed smallest choice | Remaining review |
|---|---|---|
| P01 Policy location/version | Additive trusted typed records beside existing configuration, linked to registry/source IDs; no enum replacement | Exact serialization, decision identity/version and bounded record limits before I1+I2 |
| P02 Conditional/effective rights | Finite conditions with retained satisfaction; unknown/expired/changed scope denies | Who authenticates/reviews grants, continuing validity, revocation evidence, and exact execution interval semantics |
| P03 Audience/output scope | Distinguish owner display, licensed-user display, private export and redistribution; check actual operations | Rights evidence must decide whether particular derived/display delivery also constitutes redistribution; no blanket exception |
| P04 Research labels/quality | Additive indicative delivery/provenance; do not replace native quality or V0.2 diagnostic rules | Exact new output envelope and contract boundaries; experimental formula definitions separately reviewed |
| P05 Current-use vs historical replay | Keep analytical cutoff and current execution rights separate; do not rewrite financial history | Policy-version synchronization, audit receipt retention and revocation checks across long-running operations/caches |
| P06 Retention/deletion conflict | Stop affected use and block incompatible onboarding; separate authorized procedure | Lawful metadata retention, original/backup/cache deletion, legal hold and replay limitations unresolved; no automatic purge designed |
| P07 Research rights closure | Deny an inseparable frozen dataset rather than filter/recalculate it | How retained snapshot/Outcome input manifests establish complete source/derived lineage without changing frozen preimages |
| P08 Provider switching | Explicit per-family binding/admission; existing native conflict/comparability rules govern | Versioned binding identity, historical scope and feature warmup acceptance; no switching implemented |
| P09 API denial transport | Usage denial outside frozen financial result; safe stable reason ordering | Exact error contract/status codes and read-only capability exposure reviewed before I3+I5 |
| P10 SaaS activation | Unsupported now; separate commercial/security milestone | Explicit product need, dataset rights and auth/isolation review; no tenant/billing design now |

Fundamental freeze approval is independent of this proposal. Do not silently accept
its deferred formulas, Investment classifier or source admissions while reviewing
P01–P10. No frozen Screener V0.2, Fundamental proposal or source registry is edited.

## 17. Recommended next execution and validation boundary

Next: a bounded review of P01–P10 and exact additive policy/catalog shape, followed
by explicit scope for SCP-I1+I2 with authored fixtures. Then integrate one existing
personal feature and its direct API/export paths. Maintain the independent
Fundamental final-freeze-review track; keep prohibited acquisition and full Level-1
operation blocked. No provider call/contact is authorized by this recommendation.

This bundle completes SCP-1 (profiles/rights/change behavior), SCP-2 (catalog/selection),
SCP-3 (mode/feature/enforcement/provenance/failure design) and SCP-4 (examples/tests/
minimal slices/sequence) as a **proposal**, not implementation or acceptance.

Validation for this documentation-only change: actual baseline/source inspection,
document/reference/example review, gating consistency review, full and staged diff
review, and whitespace checks. Future T01–T18 are specifications, not executed tests.
No .NET/Python suites, database smoke, migrations or operational registry/entitlement
checks were run for this proposal. No source activated/contacted, original market
file downloaded, secret added, frozen contract changed, parser/collector executed
or production configuration altered. Provider/API calls: **0**; network calls:
**0**; billable provider units/paid-plan activations: **0/0**. Prior milestones'
network findings are reused, not counted as fresh validation. One focused local
documentation commit is intended; no push/fetch/history rewrite.
