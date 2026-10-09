# Fundamental Evidence Binding V0.1

Status: **PROPOSAL — NOT FROZEN**

Policy identity proposed: `fundamental-evidence-v0.1.0` (new, additive).
Date: 2026-10-09 WIB. Baseline: `main` at `b8cea49`, clean worktree.
Independent P2-R1/R2/R3 review baseline: `49c4069`; findings in §16,
open decisions in §13, readiness in §17. **NEEDS_TARGETED_REVISION**.
Related decision: [Phase-2 entry](EXTERNAL_SOURCE_INTELLIGENCE_REVIEW.md#phase-2-entry-and-fundamentals-readiness-2026-10-08-wib)
(`GO_FOR_BOUNDED_DESIGN`, production `HOLD_FOR_EVIDENCE`).

This document is a design proposal only. It authorizes no implementation, migration,
acquisition, parser, ingestion, source admission, registry activation or contract
freeze. It does not modify or reinterpret any frozen contract. It must be reviewed
under the repository execution rules before any part of it can be frozen.

## 1. Scope and goals

Goal: define an additive, provider-neutral typed evidence model for issuer-reported
financial facts so that future deterministic fundamental analysis can reuse the
existing immutable-evidence, provenance, revision and point-in-time (PIT)
architecture without reinterpreting or extending frozen Screener Evidence V0.2
semantics.

In scope for this proposal:

- A typed fundamental claim/fact vocabulary.
- Exact numeric and missingness semantics.
- Reporting-period and period-type binding.
- Industry/statement/consolidation/audit dimension representation.
- Currency, unit, scale and source-concept identity.
- Publication and PIT chronology.
- Revision/restatement lineage.
- Source authority and admission conditions.
- Compatibility with frozen V0.2.
- Profitability-trajectory prerequisites.
- Open questions and future implementation/acceptance boundaries.

Out of scope (must not be implemented from this proposal):

- Acquisition, discovery, download or parsing of any filing (XBRL, XLSX or PDF).
- Any migration or table creation.
- Any source admission, permission claim or registry change.
- Any profitability classifier, threshold, score or valuation rule.
- Any change to frozen `screener-evidence-v0.2.0` claims, readers or precedence.
- Any use of Stockbit as an authoritative source.

The proposal inherits, without reinterpretation, the repository non-negotiable
principles: missing means UNKNOWN and never zero; exact numeric representation;
corrections append revisions; replay uses only information known at its cutoff;
no fabricated facts and no backdated knowledge; deterministic logic precedes any
LLM explanation. It also inherits the [Phase-2 entry
decision](EXTERNAL_SOURCE_INTELLIGENCE_REVIEW.md#phase-2-entry-and-fundamentals-readiness-2026-10-08-wib):
fundamentals are an independent evidence family, but no production acquisition is
authorized until an authorized, permitted source route exists.

## 2. Existing architecture reuse

The model reuses existing provider-neutral patterns rather than creating a
parallel evidence framework:

| Existing component | Reused pattern |
|---|---|
| `src/IdxStockIntelligence.Infrastructure/RawArtifactArchiver.cs` | Content-addressed immutable original retention before any parse; SHA-256 identity and byte length are authoritative integrity facts. |
| `ingestion_run` / `raw_artifact` / `raw_fetch_observation` (`0001`, `0002` migrations) | Archive-before-parse provenance; original receipt/retrieval chronology; one authenticated original can support multiple independently justified facts (per the Phase-2 review's SIMPLIFY decision). |
| `src/IdxStockIntelligence.Application/ScreenerEvidenceV02Records.cs` | Immutable record envelope conventions: canonical JSON payload, payload SHA-256, namespaced revision series (`ScreenerEvidenceRevisionSeries.Canonical`), supersession, append-only discipline. Generalization to a fundamental namespace is proposed, not performed. |
| `src/IdxStockIntelligence.Application/ScreenerEvidenceV02.cs` (`EvidenceFact<T>`, `EvidenceQuality`, `SourceAuthorityTier`, `EvidenceChronology`, `EffectiveInterval`) | Value/quality/authority/chronology separation. Quality is reconstructed. EffectiveInterval is market-date applicability, not automatically a financial reporting period. INAPPLICABLE is not an existing EvidenceQuality value. |
| `src/IdxStockIntelligence.Infrastructure/BoundedEvidenceIngestionService.cs` and Application `BoundedEvidenceIngestion.cs` | Bounded input, archive authentication and atomic semantic admission patterns. Current implementation accepts ScreenerEvidenceRecord and UniverseSnapshot: it is not an unchanged fundamental importer. |
| `src/IdxStockIntelligence.Infrastructure/ScreenerEvidenceV02AsOfReader.cs` | Deterministic as-of selection by cutoff; revision-series resolution; explicit retained-input diagnostics. |
| `source` registry (`0001`) | Provider-neutral source identity and `terms_status ∈ {UNKNOWN, ALLOWED, DISALLOWED}` admission gate; no source may become authoritative without explicit review. |
| Screener V0.2 conflict precedence (`SCREENER_EVIDENCE_V0_2_CONTRACT.md` §13; persisted binding §5; `ScreenerEvidenceResolver`) | Reuse no-latest-wins and same-logical-series correction principles. Different financial periods/scopes must first be separated; later knowledge alone does not beat an unrelated conflicting fact. |
| `0007`/`0008` append-only triggers and write-time validation | Immutability and typed binding discipline: all-or-nothing binding fields, strict version binding, no latest-row fallback, no legacy promotion. |

Persistence recommendation (to be confirmed during contract review, not frozen here):

- **Recommended:** a new additive `fundamental_evidence_record` table with its own
  policy identity `fundamental-evidence-v0.1.0`, modeled on the
  `screener_evidence_record` envelope and using a fundamental `claim` vocabulary.
  Rationale: the V0.2 table's `policy_id` check, claim enum, and
  `INSTRUMENT`/`EXCHANGE` scope kinds are frozen screener semantics; adding
  fundamental claims to that table would change a frozen storage contract and mix
  two evidence families with different subject/period semantics.
- **Rejected:** extending `screener_evidence_record` claims or policy identity.
  The parent contract and persisted binding forbid renumbering, reinterpreting or
  silently broadening V0.2.
- **Rejected:** a second reader/writer framework. The fundamental store reuses the
  same envelope grammar, decoder-failure taxonomy, revision-series pattern,
  archive ingestion boundary and PIT rules; only the claim vocabulary and period/
  concept bindings differ.

No schema is created by this proposal.

**ALREADY_IMPLEMENTED:** archive, source registry, exact `EvidenceDecimal`,
Screener record/codec/admission/resolver/reader, portfolio ledger/valuation and
stored Outcome-based Research. `RawArtifactArchiver` writes without overwrite;
the bounded ingestion service additionally authenticates retained length/hash.
Do not describe the archiver alone as proving provider authenticity or enforcing
all ingestion limits. `ScreenerEvidenceJson.Canonicalize` alone is not the strict
typed decoder. `ScreenerSourceAdmission` switches over frozen market claims.

**FROZEN_CONTRACT:** Screener V0.2 and its persisted binding. **EXISTING_PROPOSAL:**
all fundamental claims/storage below. **RECOMMENDED_CHANGE:** the bounded
fundamental-specific mapping and semantic corrections in this review.
**DEFERRED:** investment rules and delivery integrations. **EXTERNAL_BLOCKER:**
authorized filing acquisition and production admission. No generic accounting
framework or second archive/source registry is proposed. Shared-code extraction
is an implementation decision only if it demonstrably reduces duplication without
changing existing semantics.

## 3. Claim vocabulary

Three envelope-level claim tokens are proposed (not frozen). Each stored record has
one token; typed metric identity lives in the payload.

| Proposed claim token | Meaning | Typical class |
|---|---|---|
| `FinancialFiling` | A retained filing/report identity and its publication chronology (document identity, reporting entity, statement family, taxonomy/version, period(s) it covers, original/amended status). Holds no financial values. | `FILING_RECORD` (new class token, §3) |
| `FundamentalFact` | One reported assertion or local calculation for one metric/period/scope, with exact original or derived-input lineage; a derivation may span filings. | `REPORTED_FILING_FACT` or `DERIVED_FILING_CALCULATION` (proposed class tokens) |
| `FundamentalConvention` | Reviewed mapping/convention facts: source concept → canonical metric, statement family, unit/scale semantics and parser/mapping version. | `SOURCE_CONVENTION` semantics in the new namespace |

Metric vocabulary (typed `metric` token; required minimum set from the Phase-2
decision):

| Metric | Default fact kind | Period type | Notes |
|---|---|---|---|
| `Revenue` | REPORTED | DURATION | Not universal; see §6. |
| `OperatingProfit` | REPORTED | DURATION | Not universal; bank statements may not present it. |
| `NetIncome` | REPORTED | DURATION | Retain total profit versus parent/owner-attributable profit and continuing/discontinued scope; never merge by label alone. |
| `EarningsPerShare` | REPORTED | DURATION | Requires a share basis (basic/diluted; §4). |
| `OperatingCashFlow` | REPORTED | DURATION | Cash-flow statement. |
| `CapitalExpenditure` | REPORTED | DURATION | Capital expenditure sign convention must be explicit; no assumption. |
| `FreeCashFlow` | DERIVED canonical calculation | DURATION | Formula/version, capex definition/sign and role-bound inputs required. Company/provider-reported FCF is preserved separately as source-native OtherReportedConcept; it is not denied existence. |
| `TotalAssets` | REPORTED | INSTANT | Balance sheet. |
| `TotalLiabilities` | REPORTED | INSTANT | Balance sheet; may itself be derived by the source. |
| `TotalEquity` | REPORTED | INSTANT | Statement scope must distinguish owners' equity from total equity where the source does. |
| `CashAndCashEquivalents` | REPORTED | INSTANT | Exact source concept retained. |
| `ShortTermDebt` | REPORTED | INSTANT | Source definition retained; no local maturity heuristics. |
| `LongTermDebt` | REPORTED | INSTANT | Same. |
| `TotalDebt` | REPORTED or DERIVED | INSTANT | If derived, `derivation` is required; the basis (reported total vs sum of components) is explicit. |
| `SharesOutstanding` | REPORTED or DERIVED | INSTANT or DURATION | Share basis required (§4); weighted-average vs period-end must be explicit. |
| `DilutionObservation` | REPORTED, conditional scope | INSTANT or separate event applicability | Potential instruments/shares/terms are not necessarily one scalar amount. Event wire shape is unresolved D04; no fabricated period or percentage. |
| `CashDividend` | REPORTED | DURATION for statement total; separate event applicability for notice | Declared, paid and shareholder cash received are different claims; governing notice identity required. Event schema unresolved D04. |
| `DividendPerShare` | REPORTED or DERIVED | DURATION or separate event applicability | Currency, share class and entitlement basis required; no total/per-share reconciliation assumed. Event schema unresolved D04. |
| `OtherReportedConcept` | REPORTED | as reported | Source-native concept preservation for industry-specific statements (for example bank interest income) until a reviewed convention maps it; required `sourceMetricLabel`. |

Required distinctions:

- **Directly reported fact:** `factKind = REPORTED`, an identified source concept
  value as asserted by the filing; authority is that of the filing.
- **Derived calculation:** `factKind = DERIVED`, `SourceAuthorityTier.T4Derivation`,
  formula version and exact role-bound input facts required. Canonical FCF is
  derived; a company-reported FCF assertion describes that company's definition,
  and a provider-reported FCF assertion describes the provider's definition.
  Preserve those as source-native concepts with original authority/provenance,
  never relabel them as our calculation or automatically comparable to each other.
  A normalized provider response without the required filing/premise binding stays
  a secondary retained candidate, not an admitted filing-backed FundamentalFact.
- **Missing fact:** no admitted assertion exists; the reader returns UNKNOWN.
  Absence is never proof of INAPPLICABLE and never becomes a stored zero.
- **Inapplicable fact:** a positive, source-grounded assertion that the concept is
  not defined/applicable for this statement family, period or scope
  (`applicability = INAPPLICABLE` with required reason text). INAPPLICABLE is not
  missing and is never zero.

Existing class tokens are not renamed or reinterpreted. The proposal adds new
fundamental-namespace class tokens (`FILING_RECORD`, `REPORTED_FILING_FACT`,
`DERIVED_FILING_CALCULATION`) because V0.2's five classes describe the temporal
behavior of market/control facts and do not describe filing-asserted financial
statements. Whether a new token is justified versus reusing `POINT_OBSERVATION`
is explicitly listed as an open question (D01, §13). Removing redundant class
tokens is preferable if claim + factKind already determine all validity behavior;
that is RECOMMENDED — NOT APPROVED, not a new application enum decision.

## 4. Payload and value semantics

Reuse the V0.2 payload grammar shape (proposed, version 1):

```json
{"operation":"ASSERT","completeness":"FULL","value":{}}
```

- `operation ∈ {ASSERT, CANCEL}`; `completeness ∈ {FULL, PARTIAL}`.
- For `ASSERT`, `value` is the typed claim object below; required properties must
  exist with the specified type, and optional properties may be omitted or null.
  Null never becomes zero, false, an empty string or a default enum.
- For `CANCEL`, `value` is null, `completeness = FULL`, and an explicit
  `supersedes_revision_number` must name an earlier ASSERT in the exact same
  namespaced revision series, claim, subject and scope. Cancellation terminates
  the referenced fact only from the cancellation boundary and preserves earlier
  replay; it is never a negative factual value.
- Strict decoding rules are inherited from
  `SCREENER_EVIDENCE_V0_2_PERSISTED_BINDING.md` §3: case-sensitive camelCase,
  duplicate/unknown property rejection, UTF-8, lowercase hyphenated UUIDs,
  `yyyy-MM-dd` dates, UTC ISO-8601 with `Z` and at most seven fractional digits,
  invariant decimal strings `-?(0|[1-9][0-9]*)(\.[0-9]+)?`, ordinal opaque text
  for source-native codes, 64-lowercase-hex hashes, unique sorted sets, bounded
  payload (proposed 64 KiB, depth 16, arrays ≤ 256) and explicit failure reasons
  (`..._MALFORMED`, `..._BINDING_UNSUPPORTED`, `..._UNBOUND`, `..._BOUND_EXCEEDED`,
  `..._INPUT_UNAVAILABLE`). No coercion, no numeric enums, no NaN/Infinity,
  no trailing content.

`FundamentalFact.value` (proposed fields):

| Field | Required | Semantics |
|---|---|---|
| `metric` | yes | One token from §3; OtherReportedConcept requires source concept identity as well as its display label. |
| `sourceConcept` / `conventionEvidenceId` | REPORTED / mapped facts | Exact artifact-scoped concept or cell locator, not just a translated label; exact retained convention version for every canonical mapping. Wire shapes remain D01. |
| `factKind` | yes | `REPORTED` or `DERIVED`; `DERIVED` requires `derivation`. |
| `applicability` | yes | `APPLICABLE` or `INAPPLICABLE`; INAPPLICABLE requires `inapplicabilityReason` text and no numeric value. |
| `amount` | ASSERT + APPLICABLE | Exact canonical decimal string; the reported (or derived) value in the declared unit. Never a float, never rounded, never a sentinel. |
| `unit` | ASSERT + APPLICABLE | `{kind: MONETARY / SHARES / PER_SHARE, currencyCode?: 3-letter, ...}`; monetary and per-share require `currencyCode`. |
| `shareBasis` | metric-dependent | Distinct aspects: basic/diluted calculation basis; weighted-average/point-in-time count basis; issued/outstanding/treasury treatment; applicable share class/security. Basic weighted-average is expressible without choosing only one token. Exact bounded representation is D03. |
| `period` | statement facts | §5 INSTANT/DURATION only. Event-only claims need separate applicability/identity and must not fabricate fiscal dates; D04 blocks their wire schema. |
| `accountingBasis` | comparison-dependent | Explicit profit/equity attribution, statement concept definition, material dimensions and policy basis; preserve source meaning. Unknown basis cannot satisfy a calculation requiring it. D03 defines exact bounded fields. |
| `consolidation` | yes | `CONSOLIDATED / STANDALONE / UNKNOWN`; never inferred (§6). |
| `auditStatus` | yes | `AUDITED / REVIEWED / UNAUDITED / UNKNOWN`; never inferred (§6). |
| `statementScope` | yes | `PRIMARY_STATEMENTS / NOTES_TO_FINANCIALS / SUPPLEMENTARY`; canonical metrics require `PRIMARY_STATEMENTS` unless a reviewed convention says otherwise. |
| `reportingEntity` | yes | Namespaced reporting identity and retained source identifier/name; no assumption a name is a stable legal ID. Issuer/security applicability is separate (§6.4, D03). |
| `taxonomy` | when applicable | Actual accounting taxonomy family/version, never an invented XBRL taxonomy for XLSX; a non-XBRL source instead needs its retained format/schema and mapping identity. Exact required/null combinations are D01. |
| `xbrl` | no | Source metadata: `{taxonomy, concept, unitRef, contextRef, decimals?, scale?}` as opaque retained metadata. |
| `filingEvidenceId` | REPORTED | Exact retained FinancialFiling evidence UUID. A multi-filing DERIVED result reaches all original filings through its exact inputs; it must not nominate one fictitious source filing. |
| `derivation` | DERIVED | Exact formula/convention version plus named operand roles bound to exact fact UUIDs; an unordered UUID set cannot define subtraction. No latest lookup. Bound and validate transitive inputs, reject cycles/duplicates where roles forbid them, and authenticate input/mapping visibility (§8). Exact wire schema is D01. |
| `comparativeOfEvidenceId` | no | Exact retained UUID of the corresponding current-period fact when this fact is a comparative reported in the same filing; a link, never a replacement. |
| `eventDates` | event-like candidates | Preserve declared/effective/record/ex/payment dates only where reported, with explicit meaning. These are not a financial duration or a knowledge clock; exact event scope is D04. |
| `termsReference` | no | Text pointing to retained factual terms within source provenance; never a URL to fetch during replay. |

Exact numeric semantics:

- The payload persists canonical decimal **strings**; comparison uses exact decimal
  value equality without tolerance (`1.0` equals `1.00`), following V0.2 §5.
  Payload SHA-256 covers lexical form, so lexically different but numerically
  equal facts may have different payload hashes; typed equality, not hash equality,
  decides factual agreement.
- `decimals` (XBRL precision) is retained as metadata and is **not** a simple
  multiplication factor. `scale` is retained as source metadata and is **not**
  applied implicitly. Any parser-applied transformation must be defined by a
  retained, versioned convention/mapping and must be reproducible from the
  retained original; silently rescaling is prohibited.
- Values that cannot be represented exactly by the chosen canonical decimal type
  must fail closed with an explicit reason rather than round. Whether to retain an
  unrepresentable high-precision fact as a non-arithmetic observation is an open
  question (D02, §13).
- The comparable canonical `FreeCashFlow` token remains DERIVED. Company/provider
  disclosures labelled FCF can be retained as REPORTED source-native concepts;
  neither label nor authority proves formula equivalence (D05). `TotalDebt` and
  `SharesOutstanding` state whether they were reported directly or derived.
- No zero substitution, no interpolated or carried-forward values, no "latest
  known ratio" materialization.

### 4.1 Minimum filing and convention bindings — exact schema still open

`FinancialFiling` needs source-namespaced document identity, exact retained
artifact and receipt provenance, reporting identity, covered periods/scopes,
document role and taxonomy where stated, and explicit original/corrected/restated
relationships where evidenced. Publication clocks belong in the envelope, not
duplicate mutable payload copies. One document may contain several periods,
scopes, languages or attachments; same bytes do not by themselves prove the same
filing identity. Link exact artifact/observation identities, not “latest at URL.”
Missing publication or lineage may permit bounded prospective facts with explicit
limitations, but cannot assert complete historical public availability.

`FundamentalConvention` needs a stable mapping/formula identity and version,
source concept plus applicable taxonomy/schema, accounting/period/entity scope,
canonical target, units/scale/sign transformation, exact parser/mapping version,
supporting evidence and local admission chronology. Definitions must be retained
and visible, not silently replaced by a current mapping. XLSX needs a documented
layout/schema and cell/sheet locator; never invent an XBRL taxonomy for it.

The final wire fields, required/null combinations, size limits, equality keys,
subject scope, cancellation targets and diagnostic names for all three claims
must be specified with valid/invalid examples before freeze (D01). The §4 table
is a semantic proposal, not a complete strict JSON contract. Reuse envelope
metadata; do not duplicate authority, clock or source IDs in every nested value.

## 5. Period model

`period` object (proposed):

| Field | Required | Semantics |
|---|---|---|
| `periodType` | yes | `INSTANT` (balance-sheet point) or `DURATION` (flow period). |
| `startDate` | DURATION | Inclusive fiscal period start. |
| `endDate` | yes | Instant date or inclusive duration end. |
| `fiscalYear` | when evidenced | Source-declared fiscal-year identity; never inferred from calendar end year. Missing label does not erase an explicit duration, but blocks a comparison needing fiscal alignment. |
| `fiscalQuarter` | no | 1–4 when the source declares one; absent for annual/other periods. |
| `periodKind` | DURATION | FY/QUARTER/HALF/NINE_MONTHS/YTD/OTHER as source/convention declares; not a surrogate for exact dates. An INSTANT does not pretend to be a flow period. TTM representation remains D01. |
| `cumulative` | yes | `true` for cumulative year-to-date values, `false` for standalone-period values; `INSTANT` is never cumulative. |
| `ttm` | locally DERIVED only | Marks a locally constructed trailing period with exact components in derivation. A source-reported trailing-period concept retains its source period/definition separately; it is not assigned local derivation lineage. Exact shape is D01. |

Rules to prevent incompatible comparisons:

1. Exact-period equality uses `(periodType, startDate, endDate, cumulative)`;
   INSTANT has no startDate, and DURATION requires startDate ≤ endDate. Equal
   period keys are necessary for competing values of the same-period claim,
   **not** for a historical trend. Growth/trajectory compares different dated
   periods only under explicit comparable granularity, fiscal alignment and basis.
2. Quarterly standalone profit must never be silently compared with cumulative
   YTD profit; `cumulative` and `periodKind` make the difference explicit.
3. YTD/quarter differencing, aggregation or annualization is a DERIVED operation
   with an explicit formula identity and exact input facts; it is not a reader
   convenience. Differencing needs the same fiscal start and compatible nested
   ends, additive flow concept, entity/consolidation/currency/unit/policy basis and
   coherent revision basis. Result dates cover only the difference interval.
   Do not subtract weighted-average shares, EPS or other non-additive metrics as
   though they were cash/profit flows. Q4 from annual minus nine-month data has
   the same guards, including audit/restatement compatibility.
4. Locally constructed TTM identifies exact nonoverlapping contiguous component
   periods and their input revisions; each interval contributes once. No mixing
   annual + overlapping YTD/quarters. A formula may construct a trailing interval
   only when its full coverage and metric additivity are proved. Do not sum
   balance-sheet stocks or average-share/EPS denominators by default. A source-
   reported TTM concept remains source-reported, not a locally derived result.
5. Comparative-period facts reported inside a filing are ordinary facts for their
   own periods with their own filing binding; `comparativeOfEvidenceId` records the
   reconciliation link without overwriting the original period facts.
6. Periods with different fiscal calendars, currencies, statement scopes,
   consolidation or reporting entities are not comparable without an explicit
   reviewed convention. Short/long fiscal years and 52/53-week changes retain
   exact boundaries and transition identity; no forced twelve-month label or
   invented quarter. Taxonomy/policy/audit differences need explicit compatibility
   assessment, not a blanket prohibition or automatic equivalence.
7. No financial calculation threshold (for example `p > 0` or growth cutoffs) is
   defined in this milestone.

## 6. Industry, statement scope, consolidation and audit

### 6.1 Industry and accounting taxonomy

- The accounting taxonomy is retained per fact (`taxonomy.family`,
  `taxonomy.version`) and per filing; taxonomy identity is never guessed from a
  document name.
- `statementFamily` is a reviewed classification retained with `FinancialFiling`
  and the mapping convention: proposed tokens `GENERAL_CORPORATE / BANK / INSURANCE /
  OTHER / UNKNOWN`. It is never inferred from the ticker, sector or a provider label.
- Canonical metric mapping happens only through a retained `FundamentalConvention`
  (source concept → metric) valid for a specific taxonomy family/version and
  statement family. Absent a reviewed mapping, the fact remains
  `OtherReportedConcept` with its source label; it is not forced into a generic
  metric.
- A source-native concept is identified by its namespace/schema plus original
  concept/locator and relevant dimensions; its label is display text. Different
  concepts with similar labels never collapse into one OtherReportedConcept key.
  Unknown concepts may remain retained candidates without canonical mapping.
- Bank statements must not be forced into industrial `Revenue` or `OperatingProfit`.
  Bank-specific concepts (for example interest income or financing income) remain
  preserved as `OtherReportedConcept` until a future reviewed convention defines
  canonical tokens. This proposal deliberately invents no bank metric semantics.
- Other materially different taxonomies (insurance, financing, syariah, other
  regulatory formats) follow the same rule: preserve source concepts, map only
  under reviewed conventions, and mark unmapped predicates as `UNKNOWN` for the
  canonical metric rather than substituting a lookalike.
- `NetIncome`, `TotalAssets`, `TotalLiabilities`, `TotalEquity`, `CashAndCash
  Equivalents` and share counts are expected to exist across statement families,
  but the reader must still require an explicit source concept mapping; expected
  existence is not an admission.
- Industry-specific future concepts may be added additively as new metric tokens
  and conventions; existing tokens are never renumbered or reinterpreted.

### 6.2 Consolidated and standalone statements

- `consolidation` is a required explicit token on every fact:
  `CONSOLIDATED|STANDALONE|UNKNOWN`.
- It is never inferred from the absence of a dimension, from the filing title or
  from publication location.
- Consolidated and standalone facts for the same metric/period are separate
  facts with separate logical keys; a reader must be told which one it needs.
- `statementScope ∈ {PRIMARY_STATEMENTS, NOTES_TO_FINANCIALS, SUPPLEMENTARY}`
  distinguishes primary-statement values from note disclosures. Canonical metrics
  default to primary statements; a note-only value must not silently replace a
  primary-statement value.

### 6.3 Audit status and reporting entity

- `auditStatus ∈ {AUDITED, REVIEWED, UNAUDITED, UNKNOWN}` is an explicit retained
  source property. It is never inferred from the publication location, the filing
  channel or the presence of an auditor's name string.
- `reportingEntity` retains the source-declared reporting entity. The security's
  issuer binding to that entity must be proved by retained evidence before facts
  are attributed to an instrument; a name match is not proof.
- Reporting entity, consolidation, material dimensions and attribution distinguish
  different economic scopes. Net profit attributable to owners is not total
  consolidated profit; owner equity is not equity including noncontrolling
  interests. Preserve continuing/discontinued scope where material. Notes may
  supply a mapped debt/capex fact only under an explicit convention; do not assume
  primary statements contain every required concept.
- Taxonomy version and audit status are retained provenance/comparability inputs,
  not sufficient by themselves to prove different economic claims. An audited
  reissue can correct an unaudited assertion only through an evidenced relationship;
  an auditor label alone neither supersedes it nor forbids all period comparisons.
  Policy or taxonomy changes need an explicit retained compatibility mapping or
  restated basis. Unknown scope never proves equivalence.

### 6.4 Reporting entity, issuer and tradable security

Financial statements describe a reporting entity/group; market prices and holdings
describe securities. Migration 0001's instrument + issuer_name + instrument_history
is not a legal-entity registry. Retain a stable namespaced reporting identity
with its supporting original identifiers, separate from ticker/ISIN/instrument ID.
Do not invent legal IDs or match entities by name alone.

A future valuation must bind that reporting entity/issuer to the exact security,
share class, share-count/earnings denominator and effective interval with evidence
visible at the decision cutoff. Several securities may refer to one issuer;
issuer totals must not be duplicated or divided by an unrelated security's shares.
Ticker changes resolve through retained instrument history and identity evidence,
not current symbols. A consolidated group is not simply its parent's standalone
legal entity. A bounded retained identity mapping is preferred to a new registry,
but its exact representation is RECOMMENDED — NOT APPROVED (D03).

## 7. Currency, unit and scale

- Every applicable fact declares its numeric unit:
  `MONETARY|SHARES|PER_SHARE` (the metric vocabulary has no ratio metric in V0.1).
- `MONETARY` and `PER_SHARE` require a three-uppercase-letter `currencyCode`.
  A fact whose currency is unknown fails the currency requirement and is retained
  as an unbindable/unadmitted candidate rather than assumed to be IDR.
- Share counts never carry a currency. Per-share facts do. The share denominator
  and its basis are explicit (`shareBasis`, §4).
- `xbrl.unitRef` and contextRef are artifact-scoped identifiers, not universal
  currency/period codes. Retain them and resolve their actual unit/context
  definitions, including numerator/denominator and relevant dimensions, from the
  exact filing. Canonical unit/currency must match that resolved definition and
  retained mapping; the same unitRef spelling in another file proves nothing.
  No currency is ever inferred from magnitude, symbol, locale or a provider
  default.
- `decimals` is precision/accuracy metadata, not a multiplier. `scale` is source
  display/transformation metadata; neither may be applied implicitly. Any
  transformation must live in a retained parser/mapping version and be
  reproducible from the immutable original.
- Monetary amounts remain in the source currency. Exact, documented source-scale
  normalization is different from FX or economic adjustment: retain original
  lexical value/unit, transformation evidence and the exact resulting amount.
  Never apply a display multiplier twice. No FX, inflation or arbitrary share-
  adjustment conversion is introduced. Cross-currency calculations remain blocked.
- Source lexical value and precision/rounding metadata stay retained even when
  canonical spelling differs after an authorized exact transformation. Display
  rounding is not numeric certainty and XBRL decimals is not a power-of-ten scale.
  Arithmetic must be exact; even representable operands may produce an overflow
  or nonterminating quotient. Reject an unrepresentable result unless a separately
  approved formula defines explicit rounding. No such rounding rule is invented.
- D02 recommends the already implemented EvidenceDecimal representability check
  for bounded V0.1, with original unrepresentable values retained outside canonical
  arithmetic. Do not silently inherit price-column precision or introduce a new
  arbitrary-precision framework before that choice is approved.

## 8. Publication and PIT chronology

Clocks remain separate and are never conflated:

| Clock | Meaning | Rule |
|---|---|---|
| Period start/end | Economic/fiscal period the fact describes | From the source context; not a knowledge clock. |
| Effective/applicability date | Event or applicability date where relevant (for example a dividend payment date) | Factual source date, not knowledge. |
| `publishedAt` | Publication/release time of this exact original or revised assertion | Retain evidenced timestamp/zone; absent or date-only publication cannot become invented midnight. Original and amendment publication are distinct. |
| `retrievedAt` | Actual receipt time of the exact retained bytes/observation used for admission | Earliest possible local admission boundary for that observation. Later identical refetches do not move earlier retained knowledge. Not a financial-period date. |
| `knownAt` | Local admissible knowledge time | `knownAt >= retrievedAt`; a fact is never knowable before its own knowledge boundary. |
| `recordedAt` | Local persistence/execution time | Recording is not a database commit timestamp and is not domain knowledge. |
| Correction `knownAt` | Knowledge boundary of an amendment/restatement | Prospectively applies only from that boundary. |

- A filing retrieved today cannot become known to the system at its historical
  financial period end. `publishedAt` may be older than `knownAt` (the filing
  existed before the system knew it), but historical decisions must not see it
  before `knownAt`.
- `knownAt >= retrievedAt` is correct for these explicitly **local** meanings.
  It is not a statement about when the public could first know the information.
  A late parse/new convention is admitted when actually established, never
  backdated to the raw download merely because bytes were already stored.
- A derived fact has no invented upstream retrieval/publication of its own.
  Its knownAt is its actual local derivation/admission boundary, at least all input
  and convention knownAt values. Exact input receipts remain on the inputs.
- Every referenced premise (filing, input facts, conventions) must have been
  legitimately known at the referencing record's `knownAt` as well as visible at
  the query cutoff, reusing the V0.2 premise-visibility rule.
- Deterministic historical as-of reconstruction: readers select records with
  `knownAt <= cutoff`, retrievedAt ≤ cutoff when present, and publishedAt ≤ cutoff
  when present, matching the actual V0.2 visibility predicate. They
  resolve the applicable revision series, apply cancellations, then reconstruct
  typed values for the exact period/scope requested. No "latest row wins" and no
  current-state fallback.
- Later restatements never rewrite what the system previously knew. Replay uses
  the same retained original inputs and the same cutoff, and must be
  deterministic across restores.
- Historical public-availability research and historical local knowledge are
  different queries. This proposal supports the latter; unknown original
  publication/revision history cannot support an asserted public-as-of backtest.
  recordedAt remains diagnostic execution metadata, not historical MVCC proof.

## 9. Revision and restatement handling

- Originals are immutable. A correction, amendment, replacement or restatement
  appends a new record; nothing is updated in place.
- Revision series reuse the source namespace pattern, not one series for every
  fact in a document. A FinancialFiling document lineage and each FundamentalFact
  logical assertion have distinct references/series. Minimum fact identity includes
  reporting subject, metric or native concept, exact period/event, consolidation,
  attribution/material dimensions, unit/share basis and applicable convention.
  Record kind participates so filing, convention and fact cannot collide.
  Revision is of one logical assertion, not a filing-wide metric row counter.
  Source-native IDs are preferred when they identify that assertion; otherwise a
  documented deterministic local key is retained, never mislabelled as a provider
  correction ID. Exact canonical encoding/equality is a freeze prerequisite D01.
- **Corrected filing:** a new revision in the same series with its own `knownAt`;
  prior cutoffs resolve the original revision.
- **Restated financial statements:** new facts for the same economic periods, each
  with the restating filing's `knownAt`. The restated values do not replace the
  original facts for earlier cutoffs; both remain replayable.
- **Comparative-period restatements:** the filing asserts new facts for its
  comparative periods (including possibly different values than the original
  filing); those facts bind to their own periods and to the new filing, with
  `comparativeOfEvidenceId` linking the presentation pair. Comparatives never
  mutate the original period facts.
- **Same-URL attachment replacement:** a changed content hash under an unchanged
  URL produces a new raw artifact and a new revision in the same filing series
  when filing identity is established, with knownAt no earlier than actual receipt
  and admission. Original bytes remain retained. Multiple facts may share one
  artifact; identical bytes fetched under different references are not permission
  to violate raw_artifact's existing source/hash uniqueness or invent one identity.
- **Changed structured facts:** a value correction within a proved logical
  identity is a revision. A changed period/entity/unit identity requires explicit
  cancellation of the mistaken assertion plus a new fact, not silent key sliding;
  differences in lexical scale alone are not factual changes but are still
  retained as new content if the bytes changed.
- **Missing revision lineage:** if the original/revised relationship cannot be
  established from retained evidence, the record must be marked with an explicit
  bounded lineage/coverage description on the filing. COMPLETE, if ultimately
  used, must name its scope and proof; it cannot promise every historical provider
  version. Local observed-version completeness differs from publisher history.
  Missing lineage is never repaired by
  guessing equivalence from dates, matching values or URL patterns.
- **Source disagreements:** both retained; scope/visibility precede authority and
  specificity. Correction chronology applies only to an admitted logical lineage,
  not unrelated sources or a convenient later number (V0.2 binding §5); otherwise
  the affected fact is `CONFLICTING`. Source authority never overrides chronology.
- No adjustment factor, restated EPS recomputation or share-adjustment
  normalization is defined in V0.1; those are separate reviewed derivations.
- A later filing's comparative presentation link is not a restatement link by
  itself. Preserve both assertions; use explicit original/revised relationships
  or a reviewed reconciliation policy before selecting one for a calculation.
  Never mix original YTD with revised annual figures merely because both are latest.
- Cancellation/retraction of a governing filing or convention must prevent its
  dependent facts from being newly treated as valid after that boundary. An old
  fact/derivation remains replayable at its original cutoff with exact inputs;
  a new as-of selection must check premise validity and cannot silently substitute
  a newer premise or resurrect a canceled predecessor. D01 must specify bounded
  dependency traversal and failure behavior with executable future fixtures.

## 10. Source authority and admission

The conditional Phase-2 source strategy is retained unchanged:

- **Primary candidate:** official issuer/IDX financial originals, retrieved from
  an authorized, permitted route.
- **Preferred parsing:** XBRL first (structured contexts/units/periods).
- **Fallback parsing:** validated XLSX when the source genuinely supplies it.
- **Stockbit:** optional secondary enrichment/cross-check only, authority tier T5;
  it is not an admitted authoritative source and must never be the sole basis for
  a VERIFIED fact.
- **No acquisition is implemented or authorized here.** Access permission, coverage
  completeness, retention rights and revision semantics for any filing route
  remain unresolved, and public visibility does not imply automation permission.
- Source admission uses the existing `source` registry and `terms_status` gate. A
  fundamental source is admissible only after a scoped review records
  `ALLOWED` with evidence; `UNKNOWN` remains blocking. No registry mutation is
  proposed.
- `terms_status` alone is not claim authority, complete permission history or a
  licence for every endpoint/purpose. Retain exact scoped permission/admission
  evidence and knownAt for the new family using existing provenance, with the
  specific acquisition/retention/derived-use limits. A current registry edit must
  not silently rewrite historical admission proof. D06 must resolve replay rights
  after revocation/termination; operational acquisition stops when not permitted.
  No duplicate registry or automatic T1 promotion is proposed.
- Authority tiers map as: T1 issuer/registrar/exchange filing originals for the
  exact claim; T2 a contract-authorized reference explicitly admitted for the
  claim scope; T3 an admitted provider's own documented observation; T4 local
  deterministic derivation from admitted T1–T3 facts; T5 secondary
  corroboration only. A normalized provider with original-filing links remains a
  provider until its claim-scope admission is reviewed; it is never promoted by
  convenience.
- A single authenticated original can support several independently justified
  claims (filing identity + facts + conventions) without duplicate retrieval.
- The future acquisition route must not become a mandatory dependency on a single
  provider; the evidence model is provider-neutral.

## 11. Compatibility with V0.2

- The binding is additive. Frozen `screener-evidence-v0.2.0`, its claim enum,
  envelope, persistence table, decoder rules, precedence, funnel, reason codes and
  readers are unchanged.
- No existing V0.2 class, claim or authority token is renamed, renumbered or
  reinterpreted. New fundamental class/metric/claim tokens live in the new
  namespace and are never written to V0.2 records.
- Fundamental facts do not participate in Screener `MARKET_ELIGIBLE`/`DATA_READY`
  gating, Screener readiness, candidate promotion or Outcome/Verification/Research
  identity. Existing V0.1/V0.2 captures, Outcomes, verification and research
  datasets remain readable exactly as before.
- Portfolio valuation and ledger behavior are unchanged; the ledger currently has
  no market or fundamental dependency, and this proposal adds none.
- Shared helpers may be generalized only where their semantics are already
  provider-neutral (JSON canonicalization, revision-series canonicalization
  pattern, bounded ingestion, archive retention, as-of selection). Generalization
  must not change V0.2 behavior; a separate fundamental reader/store is proposed
  rather than rewriting frozen V0.2 code paths.
- A future INVEST thesis-review flow may consume fundamental facts
  (`FUNDAMENTAL_REVIEW_REQUIRED` remains the correct behavior while evidence is
  insufficient), but that boundary is not defined here and requires a separate
  authorized design.

## 12. Profitability trajectory prerequisites

Evidence supports future analysis only when its specific inputs are compatible;
none of these rows defines an investment rule or a threshold:

| Future analysis | Necessary evidence beyond a metric name |
|---|---|
| Profit/revenue trends and margin quality | Comparable durations/fiscal calendars, explicit income attribution and revenue definition, consistent entity/policy/currency; zero/negative denominator policy still requires approval |
| Earnings and cash-flow quality | CFO, profit and capex with compatible consolidation/periods and component/sign definitions; cash conversion is not inferred from a provider ratio |
| Leverage and financial health | Same-instant assets/liabilities/equity/cash/debt with debt-component scope and maturity basis; total liabilities cannot stand in for interest-bearing debt |
| Share dilution | Dated outstanding/issued/treasury and potential-share facts, share class and event terms; weighted-average EPS shares are not period-end ownership dilution |
| Dividend sustainability | Distinguish declared/paid/entitled amounts and periods, payout share class, profit attribution and cash-flow basis; no automatic total/per-share reconciliation |
| Valuation and portfolio risk | Correct reporting-entity/security relationship, admitted price date/currency, eligible earnings/share denominator and separately approved valuation policy; existing ledger arithmetic unchanged |
| Historical financial trends | Retained original/revised periods and exact cutoff, coherent comparability policy and explicit missing history; no present-day fundamentals backfilled into earlier decisions |

Future categories `CONSISTENTLY_PROFITABLE`, `IMPROVING_PROFIT`,
`PROFIT_TURNAROUND`, `DETERIORATING_PROFIT`, `LOSS_TURNAROUND_ATTEMPT`,
`PERSISTENT_LOSS`, `VOLATILE`, `INSUFFICIENT_DATA` are **not designed or
implemented** here. Prerequisites for a future deterministic classifier:

- **Required facts:** at least `NetIncome` per comparable period; likely
  `OperatingProfit` where genuinely applicable, plus `EarningsPerShare` and share
  basis for per-share trajectories. All must be exact, sourced and period-bound.
- **Required historical periods:** multiple comparable periods (the count,
  granularity and whether annual or quarterly are open design decisions). A single
  TTM snapshot or a ten-year query parameter does not establish history.
- **Comparability conditions:** different historical dates with compatible fiscal
  granularity, cumulative/standalone semantics, entity, consolidation, attribution,
  currency/unit and accounting basis (§5). Audit/taxonomy transitions need explicit
  compatibility evidence; exact date equality would prevent every trend.
  No silent mixing of annual/quarterly/YTD/TTM or original/restated bases.
- **Industry differences:** bank/non-bank statement families may not supply the
  same concepts; a classifier must state its applicability and return
  INAPPLICABLE/INSUFFICIENT_DATA rather than forcing generic metrics onto banks.
- **Missing design decisions:** the loss/profit definition and sign convention,
  period count and windowing, turnaround vs volatility distinction, treatment of
  near-zero values, restated-history policy, and threshold/rule values. None are
  invented here.
- Categories may overlap: a business can be consistently profitable and improving;
  a turnaround can also be volatile. Mutually exclusive precedence versus multiple
  descriptive dimensions, near-zero/negative-base growth behavior, and minimum
  history are product/rule decisions D08, not properties inferable from the facts.
  Data applicability diagnostics remain separate from profitability categories;
  no new category or threshold is approved by this review.
- **Future validation requirements:** deterministic property tests for each state,
  boundary and missingness case; as-of replay tests; restatement tests; and
  walk-forward evaluation before any production promotion, consistent with
  `.ai/RULES.md`.
- **Current correct behavior:** where the required facts/rules do not exist,
  `INSUFFICIENT_DATA`. No score, confidence value or threshold is produced.

## 13. Open design decisions

All recommendations in this table are **RECOMMENDED — NOT APPROVED**. They are
planning decisions, not new application enums or implicitly frozen semantics.
The review resolves contradictions at the invariant level; it cannot manufacture
approval for the remaining bounded contract choices.

| ID / timing | Problem and available options | Recommended option / reason | Correctness implications / required approval or evidence |
|---|---|---|---|
| D01 — before freeze | Complete envelope, subject scope, three claim schemas, fact/filing series, equality, cancellation and class validity are not fully specified. Options: reuse redundant class labels or let claim/factKind determine behavior; bounded dedicated binding or generic framework | Dedicated additive fundamental binding; eliminate class duplication unless distinct validity behavior is demonstrated; exact role-bound premise references and stable fact keys | Owner approval of exact wire schemas, valid/invalid examples, bounded dependency closure and filing-cancellation semantics. Period end must not expire the availability of a historical filing as though it were a daily market fact. No production enum/table change now |
| D02 — before freeze | Numeric storage/operations: existing exact .NET decimal subset versus wider arbitrary-precision arithmetic | Reuse EvidenceDecimal grammar/representability for bounded canonical facts; retain unsupported source literals in originals/candidate diagnostics | Explicit approval of limits, overflow and unrepresentable-result behavior; inspect parser mappings without assuming all source facts fit. No implicit rounding, FX or price-column precision reuse |
| D03 — before freeze | Reporting identity, attribution, dimensions and share basis need exact bounded representation; options include retained scoped mapping or a new issuer registry, narrow dimensions or universal taxonomy model | Minimal retained entity/security relationship plus concept/dimension evidence; orthogonal share-count/calculation basis and parent/total attribution; no registry/framework by default | Owner approves keys and minimum fields for bank/non-bank samples, multiple share classes, unknown identity, historical mapping and valuation boundary. Real issuer mappings still require authorized source evidence |
| D04 — before freeze | Dilution/dividend notice events do not fit mandatory statement period/scalar fields. Options: freeze bounded statement facts first and defer event claims; or define exact separate event value shapes now | Bounded statement-fact V0.1, preserve raw/native event evidence but defer typed event calculations until an event addendum | Owner must explicitly approve narrowed typed V0.1 scope or complete event schemas. Do not claim all requested dividend/dilution coverage if deferred; aggregate reported dividend amounts remain distinct from notice/entitlement facts |
| D05 — before canonical FCF extension | Options: canonical FCF derived only; reported/derived variants with explicit definitions; alias all FCF labels | Keep canonical comparable FCF derived, preserve company/provider-reported FCF as source-native reported assertions with their definitions and authority | No FCF formula is approved here. A company report can establish what the company reported without proving comparability; provider-reported FCF remains secondary. Broader canonical variants require reviewed definition/equivalence evidence |
| D06 — before live admission; generic rules before freeze | Publication date-only/zone ambiguity, scoped permission, amendment identity, observed versus complete publisher history, retention after revocation | Retain honest local knowledge and bounded observed history; no invented publication instant or completeness; exact scoped admission evidence, existing registry | Source-independent rejection/unknown rules can freeze without an operational feed. Route-specific authority/permission/time semantics and any replay rights require publisher evidence and explicit admission; do not contact or activate now |
| D07 — before derived calculations | Formula definitions for FCF, total debt, quarterly flows, TTM, growth and margins; exactness versus rounded outputs | Admit formulas separately with named inputs, versions, sign/component definitions, comparability and exact-result policy | Accounting/product approval of each formula and applicable statement families. Debt is not total liabilities; capex scope cannot be guessed; bank metrics need applicable definitions. No universal formula or threshold approved |
| D08 — before investment classification/integration | Profit category overlaps, lookback, granularity, thresholds, revised-history policy; exclusive label versus multiple dimensions | Separate future deterministic rule policy from evidence model; preserve INSUFFICIENT_DATA until both rules and facts are admitted | Product/investment approval required. No near-zero, growth, loss or turnaround thresholds invented. Valuation must separately approve price-date, earnings/share denominator and entity/security applicability |
| D09 — before persistence freeze/deployment | Dedicated table shape, schema dependencies, idempotency/receipt identity and deployment order; options: normal additive chain or explicit independent migration design | Separate fundamental table referencing existing source/archive provenance; follow current chain unless independently reviewed otherwise; no V0.2 table extension | Exact DDL/reader contract and dependency proof required later. Current sequential path requires 0007→0008→0009→0010→0011 before provisional 0012. Deployment needs separate authorization/preflight, regardless of design approval |

Accepted trade-offs: finite observed revision coverage rather than a promise to
recover unseen publisher versions; nullable publication with honest local cutoff
and explicit limits; narrow canonical vocabulary plus source-native preservation;
optional enrichment outside authoritative facts; no generic accounting taxonomy
engine. Deferred implementation choices include physical indexes, batching,
parser library selection, cache scheduling and UI/API shape. They may not weaken
semantic bounds, exactness, security or immutable provenance.

## 14. P2-R3 development dependency map — planning only

Readiness labels below describe future tasks, not product states. No implementation
is authorized in this bundle. Recommended model/effort is **Codex Astra / High**
for these correctness-sensitive slices, using the user's task convention; simple
scaffolding can follow existing local-model routing after a contract is approved.
No model/agent was launched by this plan.

| Task ID / objective | Dependencies / required frozen contract | Required source evidence | Acceptance criteria / future test strategy | Model / effort | Current readiness |
|---|---|---|---|---|---|
| P2-R4 — close bounded contract decisions and prepare exact freeze-review text | Resolve D01–D04 and design aspects of D06/D09; existing frozen V0.2 is a compatibility constraint; fundamental contract remains proposal until separately approved | Repository evidence and explicitly synthetic contract examples; no live acquisition | All three schemas, logical/equality keys, clocks, scopes, numeric limits, cancellation and bounded traversal have explicit valid/invalid examples; no must-fix ambiguity; review T01–T35 below | Codex Astra / High | READY for design-only follow-up; separate request and explicit later freeze approval |
| P2-I1 — contract types, strict codecs, exact numbers and pure validation | Explicit fundamental contract freeze plus implementation authorization after P2-R4 | SOURCE-INDEPENDENT synthetic inputs only | All approved claims reject ambiguous/unknown/malformed combinations; exact numeric/identity round-trip; T01–T17, T23–T24, T26, T28–T29, T31–T33; standard test discovery | Codex Astra / High | CONDITIONAL; becomes READY only after approved contract/build scope |
| P2-I2 — additive persistence, filing identity/provenance, revisions and PIT reader | P2-I1; frozen storage/reader contract and D09 dependency choice; reuse existing archive/registry, not Screener records | SOURCE-INDEPENDENT disposable fixtures; no operational evidence activation | Append/idempotency/conflict/cancellation and multi-fact filings survive restore; bounded dependency authentication; T17–T22, T26–T27, T30–T35; owned disposable PostgreSQL and V0.2 regression | Codex Astra / High | CONDITIONAL; migration design/test distinct from operational deployment |
| P2-I3 — authorized filing acquisition, XBRL mapping and fact normalization | P2-I1/I2; D03/D06; frozen mapping/identity/admission requirements; explicit acquisition scope | SOURCE-DEPENDENT permitted original/index samples, scoped rights, publication/revision semantics, taxonomy/unit/context evidence | Bounded archive-before-parse; namespace/unit/context/dimension resolution; retain original lexical values; unknown mappings fail closed; duplicate XBRL facts reconciled without first/last-value choice; source-specific bank/non-bank fixtures plus later separately authorized bounded proof | Codex Astra / High | BLOCKED by authorized route/evidence; offline parser fixtures do not admit production facts |
| P2-I4 — validated XLSX alternative and normalization | P2-I3 evidence contract; demonstrated actual XLSX need, not speculative fallback; same frozen fact/mapping contract | SOURCE-DEPENDENT permitted workbooks and documented units/layouts/formulas/cached-value semantics | Same facts/periods/units as applicable originals; no label/column-position guessing; reject uncertain merged headers, stale formula values and ambiguous scales; focused fixture equivalence/malformed cases | Codex Astra / High | BLOCKED; may be omitted until a genuine source need exists |
| P2-I5 — selected deterministic derived calculations | P2-I1; approved D07 formulas and derivation contract; P2-I2 for persistence | SOURCE-INDEPENDENT formula fixtures possible; admitted inputs needed for production usefulness | Exact named operands and full lineage; incompatible dates/scopes/currencies or unrepresentable outputs rejected; T02/T05–T16/T29/T35; no thresholds or blanket FCF/debt formula | Codex Astra / High | CONDITIONAL; no live provider dependency for pure tests after approval |
| P2-I6 — profitability trajectory and financial-quality rules | P2-I2/I5; separately frozen D08 rule policy | Comparable admitted multi-period facts for production; synthetic boundary fixtures for logic only | Explicit category overlap/precedence, required history, missingness and restatement policy; T25 plus rule-specific boundary and PIT/walk-forward evaluation; no claims from unavailable facts | Codex Astra / High | DEFERRED |
| P2-I7 — valuation/portfolio consumption | P2-I2/I5; D03/D08; separate frozen valuation boundary, existing ledger unchanged | Admitted prices, appropriate earnings/share facts and identity/FX evidence if a future policy allows conversion | Correct issuer/security/share-class/date binding; no company-total duplication or weighted-average/period-end substitution; T06/T07/T13/T14/T26/T27 and ledger regression | Codex Astra / High | DEFERRED |
| P2-I8 — minimal API/frontend delivery | A proven useful read boundary from P2-I2 or I6/I7; separately authorized delivery contract | Stored admitted facts/diagnostics; no provider requests on read path | Show period, scope, basis, as-of provenance and missingness; no investment action from incomplete data; bounded read/serialization and accessibility checks | Codex Astra / High | DEFERRED |

This groups filing identity with persistence/PIT, and XBRL with source normalization,
rather than inventing a milestone for every noun. Implement the source-independent
core only after explicit contract/build approval; live source work remains blocked.
Do not automatically start any task above.

### Migration safety and actual dependency chain

Confirmed in repository SQL: 0007 requires schema version 6; 0008 requires 7;
0009 requires 8; 0010 requires 9; 0011 requires 10. Later tables also reference
technical capture/candidate predecessors. Migration 0001 supplies source,
ingestion_run, raw_artifact and instrument; 0002 supplies the version ledger and
raw_fetch_observation. This is **ALREADY_IMPLEMENTED** migration code, not a claim
that it is applied operationally.

The user-supplied OPS-1 result says operational 0001–0006 applied and 0007–0011
absent. Preserve that historical finding; no DB check was repeated. A provisional
`0012_fundamental_evidence_v01.sql` is only a future name. The current sequential
migration design would require the preceding chain and its preflight; direct
fundamental data dependencies do not themselves require Screener tables, but
bypassing the version chain would be a separate reviewed design, not an assumed
shortcut. Do not make an unnecessary fundamental FK into Screener storage.

Future deployment requires explicit authorization, target/version/checksum and
backup/restore preflight, exact dependency checks and owned disposable acceptance.
Do not edit existing migrations or deploy 0007–0011 as a side effect of this plan.
Migration-file implementation/testing and operational deployment are separate gates.

## 15. Deterministic future contract-test specification

All numeric examples below are **synthetic scenarios**, not market/accounting
facts. These tests are specified, not implemented or executed. The final frozen
schema must make each expected behavior unambiguous; unresolved decisions block
relevant tests/implementation rather than being silently assigned defaults.

| ID / scenario | Input conditions | Expected behavior | Requirement |
|---|---|---|---|
| T01 Valid reported fact | Permitted fixture source, exact filing/convention, resolved scope/period/unit, amount 100 | Reconstruct reported value with exact provenance at visible cutoff; VERIFIED means admitted assertion, not independent audit of issuer truth | §§3–4/10 |
| T02 Valid derived fact | Approved synthetic additive formula with role-bound compatible 100 and 20 inputs | Result 120 with formula/input versions and all filing lineage; no fake single filing | §§4/8; D07 |
| T03 Missing versus zero | One absent assertion; one explicit reported 0; one positive inapplicability assertion | UNKNOWN, numeric zero and inapplicability remain distinct; no zero fill | §3 |
| T04 Negative values | Reported -25 or explicitly parsed accounting-negative lexical value | Preserve negative exactly; missing/hyphen and cancellation are not negative amounts | §§3–4/7 |
| T05 Decimal precision | 1.0 and 1.00; oversized coefficient/fraction; exact inputs with unrepresentable division result | Equal typed values retain original spellings/hashes; reject loss/overflow or unsupported result, never implicit rounding | §§4/7; D02/D07 |
| T06 Currency mismatch | IDR amount versus USD amount, otherwise comparable | Block arithmetic/comparison needing common currency; no implicit FX | §7 |
| T07 Unit mismatch | Shares versus currency; per-share amount with wrong share class/count basis | Reject incompatible use; preserve distinct facts | §§4/6.4/7 |
| T08 Instant versus duration | Assets at date versus flow over date range; invalid instant startDate | Reject use as interchangeable periods and invalid shape | §5 |
| T09 Quarter versus YTD | Three-month standalone versus nine-month cumulative profit | No direct like-period comparison or summation as two quarters | §5 |
| T10 YTD quarter derivation | Same-start compatible six-month flow 90 and three-month flow 40; then a mismatched restatement/currency variant | Produce 50 only for the compatible additive case, with difference-period dates and input roles; incompatible case unavailable | §§5/9 |
| T11 TTM lineage | Four compatible contiguous nonoverlapping quarters; variants with duplicate, gap, overlap or stock/EPS concepts | Valid flow covers each interval once; retain exact components; reject others without explicit suitable formula | §5 |
| T12 Fiscal-year change | Non-December year end, then short/long transitional year or 53-week period | Preserve exact source dates/fiscal identity; no forced calendar-year quarter/TTM or automatic growth comparability | §5 |
| T13 Consolidation | Parent standalone and consolidated facts for same date/metric | Separate scopes; no merge, substitution or false conflict | §6 |
| T14 Profit attribution | Total group profit and profit attributable to owners | Distinct basis; only requested approved attribution feeds EPS/trajectory | §§4/6 |
| T15 Industry concept | Bank interest-income concept without industrial revenue mapping | Retain source-native identity; canonical industrial metric remains unknown/inapplicable only with positive basis | §6.1 |
| T16 Audit/policy/taxonomy | Unaudited original and audited reissue; changed accounting policy/taxonomy | Audit label alone does not supersede; explicit lineage/mapping required, preserved cutoffs and comparison limits | §§6/9 |
| T17 Publication cutoff | Known/received before cutoff but evidenced publication after cutoff | Invisible, matching V0.2 visibility; no publication time guessed if absent | §8 |
| T18 Late ingestion | Historical filing first retrieved/admitted today | Invisible to earlier local cutoff; period/publication do not backdate knowledge | §8 |
| T19 Restatement | Value 100 original, 80 corrected with proved lineage and later admission | Earlier cutoff 100, later eligible cutoff 80, both originals retained | §9 |
| T20 Comparative revision | New filing shows revised prior-period figure; current-period presentation link only | Comparative belongs to prior economic period with new knowledge time; presentation link alone cannot replace original; explicit reconciliation required | §9 |
| T21 Same-URL replacement | Same URL different bytes; with/without proved filing identity | New immutable artifact; same-series revision only with identity proof; otherwise retain unresolved lineage, no URL-based merge | §§4.1/9 |
| T22 Conflicting sources | Equally applicable/authoritative unrelated assertions 100 and 120, different retrieval times | CONFLICTING; later retrieval/UUID/order does not choose value | §§2/9 |
| T23 Missing permission | Registry UNKNOWN or no claim/route-scoped grant | Reject production admission; fixture authorization does not imply real source permission | §10 |
| T24 Unknown concept | Unknown namespace/version/dimension or reused display label | Retain original/native candidate; no canonical mapping or invented taxonomy | §§4.1/6/7 |
| T25 Insufficient profitability history | Valid single-period fact, missing periods, or no approved rule/overlap policy | INSUFFICIENT_DATA; no threshold, label or confidence invented | §12; D08 |
| T26 Issuer/security identity | Two share classes, ticker change, same issuer-name collision, consolidated group | Require exact effective/PIT relationship and denominator; no identity by name/current symbol or duplicated issuer totals | §6.4 |
| T27 Historical reproducibility | Restore retained inputs/versions and repeat exact cutoff | Same selected facts/diagnostics and canonical result; no network/current mapping/registry fallback to invent old truth | §§8–10 |
| T28 Source scale/precision | Source amount 1.25 explicitly in millions; unitRef name reused in another artifact; decimals=-3 | Approved exact mapping produces 1250000 once; resolve actual scoped units; decimals does not multiply value | §7 |
| T29 Operand roles | Same UUID set, swapped minuend/subtrahend; revised formula version | Distinct derivation identity; reject missing role/version; sorting UUIDs cannot determine formula semantics | §4 |
| T30 Canceled premise | Filing/convention withdrawn after dependent fact admitted | Earlier replay remains possible; later new selection cannot validate against withdrawn premise or resurrect predecessor | §9 |
| T31 Several facts per filing | Revenue, profit and assets, two periods, same source filing | Distinct fact keys/series coexist; correcting revenue does not supersede assets/profit | §9; D01 |
| T32 Event facts | Dividend declaration/payment and a dilution notice lacking fiscal duration | Do not invent quarter/date or scalar. Reject as unsupported typed event until D04 scope/schema approved; retain authorized original | §§3–5; D04 |
| T33 Bounds/security | Missing/unknown/duplicate JSON properties, cycles, too many premises, unsafe archive paths, XML external entities, excessive ZIP expansion | Fail explicitly without partial semantic admission, external fetch or truncation; reuse proven limits/patterns and define parser-specific budgets before implementation | §§2/4/14; D01 |
| T34 Admission history | Current registry status changed after prior retained scoped admission | Do not relabel old permission as current or current flag as historical proof; enforce approved replay-use rights separately | §10; D06 |
| T35 Late derivation/convention | Inputs old, formula admitted/computation first performed later; identical refetch also occurs | Persisted derived assertion absent before its real knownAt; refetch does not move old knowledge; retrospective calculation remains labelled a new calculation using old-cutoff inputs | §§4/8 |

Focused acceptance will also cover exact write idempotency versus semantic
numeric equality, negative/zero capex sign conventions, complete/partial payloads,
missing original bytes and authenticated hash mismatch. No frozen V0.2 reason
codes or quality enums are extended to satisfy these future tests.

## 16. P2-R1 independent review (2026-10-09 WIB)

Reviewed the complete original proposal at `49c406932b4c7d8b532b497953dd72b13882f70c`.
Baseline: `main`, clean, 1 ahead / 0 behind local `origin/main`; no fetch.
Findings below refer to that baseline, not defects claimed in production code.
No CRITICAL defect was established; HIGH findings can corrupt future identity,
comparability or chronology if implemented as originally described. Dispositions
are review labels, not application enums. Resolution references describe P2-R2.

| ID | Area / severity | Baseline evidence | Correctness impact | Recommendation / required decision | Classification / resolution |
|---|---|---|---|---|---|
| F01 | Revision identity / HIGH | §9 uses filing identity for all series; `0007_screener_evidence_v02.sql` uniquely indexes series + revision; binding §5 separates logical facts | Multiple metrics/periods in one filing collide or supersede unrelated facts; period corrections can leave stale facts | Separate filing lineage from fact logical identity and fact revision series; cancellation/new assertion for changed identity; D01 | MUST_FIX_BEFORE_FREEZE; invariant corrected in §9, exact encoding remains D01 |
| F02 | Missing claim bindings / HIGH | §§3–4 define only FundamentalFact fields; filing/convention and source-concept references are incomplete | Cannot authenticate two of three claims or deterministically bind non-XBRL concepts, mappings and premises | Minimum semantic contents in §4.1; complete exact schemas/equality and rejection examples in D01/D03 | MUST_FIX_BEFORE_FREEZE; targeted schema decision remains open |
| F03 | PIT / HIGH | §8 cutoff omits publication; actual `ScreenerEvidenceValidity.Visible` and SQL reader check publication; same-URL correction fixes knownAt to retrieval | Future publication can become visible; later parsing/admission can be backdated; derived facts have no retrieval of their own | Keep local knownAt ≥ actual receipt; publication cutoff and premise visibility; derived admission clock; §8 | MUST_FIX_BEFORE_FREEZE; resolved at invariant level |
| F04 | Periods / HIGH | §§3/5 mix EVENT with only INSTANT/DURATION; §12 requires identical dated keys for trends; YTD/TTM component rules underspecified | Event cannot serialize consistently; growth is impossible or overlapping flows double-count; EPS may be incorrectly summed | Separate period equality from comparison; event scope separate; nonoverlap/additive flows and fiscal transition guards; D04 | MUST_FIX_BEFORE_FREEZE; corrected §§3–5/12; bounded event wire scope open |
| F05 | Share denominator / HIGH | §4 single BASIC/DILUTED/WEIGHTED_AVERAGE/PERIOD_END choice | Cannot express basic weighted-average shares; period-end shares can wrongly replace EPS denominator | Orthogonal calculation/count basis and security/share-class applicability; §§4/6.4; D03 | MUST_FIX_BEFORE_FREEZE; corrected semantics; exact schema open |
| F06 | Accounting scope / HIGH | NetIncome lacks attributable scope; §§6/12 require taxonomy/audit identity equality | Parent/total profit can merge; audited reissue or taxonomy version becomes incomparable by accident | Preserve attribution, concept/policy/dimensions; audit/taxonomy are comparison constraints with reviewed bridges, not automatic distinct economic facts; §6 | MUST_FIX_BEFORE_FREEZE; resolved as explicit required semantics |
| F07 | Issuer/security / HIGH | §4 assumes stable reporting-entity text; migration 0001 has instrument + issuer_name, not issuer legal-entity registry | Names/tickers can identify wrong entity; multiple securities can duplicate issuer totals in valuation | Stable namespaced reporting identity plus retained effective/PIT security relationship; no registry by default; §6.4, D03 | MUST_FIX_BEFORE_FREEZE; identity requirement corrected; bounded binding approval open |
| F08 | Derived lineage / HIGH | §4 requires one filing even for DERIVED and only sorted input UUIDs | Multi-filing derivation is not expressible; subtraction operand roles and formula versions are not authenticated | Conditional filing reference; exact role-bound inputs and convention version, full transitive visibility and bounded acyclic closure; §§4/5/8 | MUST_FIX_BEFORE_FREEZE; corrected; exact schemas/formulas remain D01/D07 |
| F09 | Reported FCF / MEDIUM | §§3/4 say FCF can never be a reported fact | Discards a company's actual nonstandard FCF disclosure or confuses provider computation with our calculation | Keep comparable canonical FCF derived; preserve company/provider-reported FCF as source-native assertions with distinct provenance/definition; §3, D05 | SHOULD_IMPROVE; clarified; broader canonical promotion not approved |
| F10 | Numbers/units / HIGH | §7 forbids rescaling but §4 permits mapped transformations; type unresolved; unitRef treated as authoritative by itself | Display value can differ from economic amount; scoped XML reference mistaken for currency; silent overflow/rounding | Separate raw lexical value from exact mapped amount; resolve units/contexts in artifact; recommend existing EvidenceDecimal limits with explicit rejection; §§4/7, D02 | MUST_FIX_BEFORE_FREEZE; contradiction resolved; numeric bound approval pending |
| F11 | Reuse / MEDIUM | §2 implies bounded ingester and as-of reader can serve any evidence family | Existing classes are tied to UniverseSnapshot, EvidenceClaim, instrument/exchange scope and market-date validity | Reuse archive/codec/PIT patterns, not unchanged V0.2 claim reader/ingester; no generic framework mandate; §2 | SHOULD_IMPROVE; corrected implementation boundary |
| F12 | Migration / MEDIUM | §14 proposes 0012 without dependency/preflight detail; 0007–0011 each guard preceding version | Future deployment may skip prerequisites or unintentionally activate unrelated schema | Separate schema design from explicit deployment; document actual chain and provisional 0012 prerequisite; §14 | MUST_FIX_BEFORE_FREEZE for persistence plan; resolved planning omission, no SQL |
| F13 | Sources / HIGH | Prior external review and entry decision; source table's mutable terms_status is not a complete scoped permission history | Public filing presence or a flag can be mistaken for permission/authority or change historical proof | Preserve independent scoped admission evidence and exact permissions; no duplicate registry or source activation; §10, D06 | EXTERNAL_EVIDENCE_BLOCKER; remains unresolved for production, not all source-independent design |
| F14 | Profitability / MEDIUM | §12 has categories but no overlap/priority policy | Profitable and improving, turnaround and volatile can overlap; metrics alone cannot decide label | Explicit future rule-policy decision, no thresholds; §12, D08 | ACCEPTABLE_TRADE_OFF while deferred; always INSUFFICIENT_DATA without rules/history |
| F15 | OPS reporting / LOW | User reports 2,078 fingerprinted files vs >5,000 nonregistered; no matching count ledger found in relevant committed docs | Cannot substantiate common scope or safely publish a corrected count | Preserve both as unverified report descriptions; no operational recount; §17 | SHOULD_IMPROVE; editorial uncertainty retained |

Sound baseline choices retained: immutable originals, missing ≠ zero, explicit
instant/duration and consolidation, no automatic bank-to-industrial mapping,
source-namespaced revisions, separate future fundamental storage, no Stockbit
authority, and unchanged portfolio/Research/Screener behavior. A hash proves
retained identity, not external financial truth. No extra framework is warranted.

## 17. P2-R3 freeze readiness and review outcome

**NEEDS_TARGETED_REVISION.** P2-R1 independent review, P2-R2 semantic refinement
and P2-R3 readiness/planning are complete as documentation tasks. This is not
reviewer/user acceptance and not permission to freeze or build.

| Dimension | Assessment after refinement | Remaining gate |
|---|---|---|
| Financial vocabulary/accounting | Attribution, native concepts, share bases and reported-versus-derived FCF clarified | D03/D04 minimum admitted scope and exact fields; D05/D07 if broader calculations are included |
| Period model | Same-period identity separated from historical comparability; quarter/TTM lineage and fiscal transitions guarded | D01 exact period/TTM shape; D04 event boundary |
| Numbers/units | Exactness and source-scale normalization reconciled; no FX or implicit rounding | D02 approved representability/operations; mapping-specific evidence later |
| Entity/security identity | Financial subject separated from security/price/denominator | D03 exact retained relationship and identity keys |
| PIT/revisions | All visibility clocks, real local admission, distinct series and premise cancellation specified at invariant level | D01 exact schema, equality, correction/withdrawal fixtures and traversal limits |
| Source admission | Existing registry/archive retained; permission and authority separated | D06 source-independent admission/replay rules; operational route remains EXTERNAL_BLOCKER |
| Architecture/persistence | Additive store and family-specific semantics; generic framework rejected; actual migration chain documented | D09 final storage contract and separately authorized deployment preflight |
| Testability | 35 deterministic scenarios specified with expected behavior | Turn D01–D04 choices into exact schema examples before tests/code |
| Investment usefulness | Supports profitability, growth, margins, cash-flow/earnings quality, leverage/health, dilution, dividends, valuation and trends when required facts exist | Each needs compatible scope/periods and separately approved formulas/rules, not just a metric label; D07/D08 deferred |

Internal contract incompleteness prevents READY_FOR_FREEZE_REVIEW. External
permission alone would not prevent a source-independent contract review; it still
prevents operational acquisition/admission. The smallest next milestone is
**P2-R4 — resolve D01–D04 plus design aspects of D06/D09, then prepare exact
bounded freeze-review text and examples**. Approval must explicitly cover chosen
scope, schemas/identity/equality, numeric limits, PIT/cancellation, admission and
storage dependencies. Contract freezing then requires a separate explicit decision.
No accounting formula, profitability threshold or event-scope reduction is approved
merely by this recommendation.

The latest bundle explicitly distinguishes later source-independent core work
from source-dependent ingestion. After contract and implementation approval,
P2-I1/I2 and pure P2-I5 fixture work can proceed without a live provider. This is
a refinement of the earlier blanket “build blocked” planning language, not an
authorization to implement now. Live parsing/normalization/acquisition remains
BLOCKED until route/evidence admission. Synthetic fixtures never qualify as
production facts or operational soak evidence.

### OPS-1 documentation consistency only

The prompt reports 2,078 fingerprinted files under data/ and pilot/ and a separate
“more than 5,000 non-registered files” description. The relevant committed proposal,
external review and runbook contain no matching OPS-1 count ledger establishing
the scopes. Different roots, file classes, deduplication or observation times
could explain it, but none was verified. **Editorial uncertainty remains**; no
corrected count or claim that the scopes definitely differ is made. The historical
OPS-1 migration/source/integrity findings are carried forward as supplied, not
freshly confirmed. No Docker restart, archive recount or DB audit was performed.

### Validation boundary

Only this proposal is changed. Source paths, frozen-contract behavior and actual
migration dependencies were inspected; no provider survey was repeated. Network
requests/provider calls/billable units/paid cost for this bundle: **0/0/0/0**.
No credentials, source activation, schema/operational data mutation, production
implementation, FullIdx or soak change. Frozen V0.2 documents and existing
migrations remain untouched. Graph-first navigation returned the local-infra
graph rather than this repository's symbols, so direct source reads were used.

Validation consists of complete proposal/diff review, reference checks and
`git diff --check`; full runtime suites are not required for documentation-only
work. The future tests above are **not executed tests**. Actual final commit and
worktree/ahead/behind observations are reported with the task result. No push.
