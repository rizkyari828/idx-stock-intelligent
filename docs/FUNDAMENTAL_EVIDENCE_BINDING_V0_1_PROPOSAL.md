# Fundamental Evidence Binding V0.1

Status: **PROPOSAL — NOT FROZEN**

Policy identity proposed: `fundamental-evidence-v0.1.0` (new, additive).
Date: 2026-10-09 WIB. Baseline: `main` at `b8cea49`, clean worktree.
Independent P2-R1/R2/R3 review baseline: `49c4069`; findings in §16,
historical decisions in §13 and readiness in §17. P2-R4 baseline: `897af2d`,
clean `main`, 2 ahead / 0 behind local `origin/main` (no fetch).
Current exact candidate: §§18–22; synthetic examples: §23; current decisions:
§24; freeze-review package: §25. **READY_FOR_FREEZE_REVIEW** of the proposed
statement-only scope, subject to the explicit approvals in §24. This is not approval, freeze or build permission.
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
P2-R4 resolves class redundancy: the class labels below are derived descriptions,
not serialized fields or additions to the frozen V0.2 enum. Exact schemas in §18
replace the preliminary field shapes in §§4–5; §19 defines identity and validity.

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
| `DilutionObservation` | DEFERRED | No V0.1 typed period | Excluded from the proposed statement-only candidate; notices/potential instruments remain authorized retained originals, without scalar promotion. D04 scope approval required. |
| `CashDividend` | REPORTED statement total | DURATION | Only a filing's period-bound statement assertion with an exact declared/paid concept definition; event notices/entitlements excluded. |
| `DividendPerShare` | REPORTED statement assertion | DURATION | Only an explicitly period-bound disclosure and share class/basis; event distributions and local derivation deferred. |
| `OtherReportedConcept` | REPORTED | as reported | Source-native concept preservation for industry-specific statements (for example bank interest income) until a reviewed convention maps it; required `sourceConcept.label`. |

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

Existing class tokens are not renamed or reinterpreted. Fundamental class is a
pure function: FinancialFiling → FILING_RECORD; FundamentalConvention →
SOURCE_CONVENTION; FundamentalFact + REPORTED → REPORTED_FILING_FACT;
FundamentalFact + DERIVED → DERIVED_FILING_CALCULATION. The function determines
validation below; storing another enum would create redundant state. None uses
V0.2 PointObservation expiry at a financial period end.

## 4. Payload and value semantics

P2-R4 replaces the preliminary field sketch with the complete proposed grammar
in §18. Root payload remains operation/completeness/value; CANCEL has null value.
Claim class is derived rather than stored. §18.2 defines exact decimal admission,
§18.3 the common envelope, and §§18.5–18.7 the three claim schemas. §19 defines
logical identity, equality, withdrawal and bounded premise resolution.

Numeric equality is exact; lexical spellings remain distinct retained content.
Missing assertions yield UNKNOWN, explicit reported zero remains zero, and a
positive INAPPLICABLE assertion is nonnumeric. Unrepresentable source values stay
in retained originals/candidate diagnostics, outside canonical arithmetic. No
extra arithmetic framework, implicit FX, rounding or latest-value fill is proposed.

### 4.1 Filing and convention bindings

FinancialFiling retains document lineage separately from each fact's series.
FundamentalConvention has bounded MAPPING, ENTITY_SECURITY and FORMULA_REFERENCE
variants (§18.7), with exact original evidence and versions. Formula references
are not a formula language or approval of any accounting calculation. Both filing
and convention schemas are complete proposed bindings rather than production code.

## 5. Period model

The exact Period schema is in §18.4; validation and comparison prerequisites are
in §20. INSTANT and DURATION are the only admitted period types. Event notices
are excluded from this proposed V0.1 scope, pending D04 approval. A source-reported
trailing concept remains source-native; local TTM requires separately approved
formulas and exact contiguous input coverage.

Same-period identity uses exact dates and period semantics; historical trends
compare different dates under an approved comparability policy. Fiscal-year
labels never replace exact boundaries, and year-end/audit/taxonomy labels alone
prove neither equivalence nor a supersession relationship.

## 6. Industry, statement scope, consolidation and audit

### 6.1 Industry and accounting taxonomy

- The accounting taxonomy is retained through each fact's exact filing/mapping
  (`format.taxonomyFamily`, `format.taxonomyVersion`); taxonomy identity is never guessed from a
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
and its exact proposed representation is in §18.7 (D03; not approved).

## 7. Currency, unit and scale

- Every applicable fact declares its numeric unit:
  `MONETARY|SHARES|PER_SHARE` (the metric vocabulary has no ratio metric in V0.1).
- `MONETARY` and `PER_SHARE` require a three-uppercase-letter `currencyCode`.
  A fact whose currency is unknown fails the currency requirement and is retained
  as an unbindable/unadmitted candidate rather than assumed to be IDR.
- Share counts never carry a currency. Per-share facts do. The share denominator
  and its basis are explicit (`shareBasis`, §18.4).
- `sourceConcept.unitRef` and contextRef are artifact-scoped identifiers, not universal
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
  correction ID. Exact proposed canonical encoding/equality is specified in §19 (D01).
- **Corrected filing:** a new revision in the same series with its own `knownAt`;
  prior cutoffs resolve the original revision.
- **Restated financial statements:** new facts for the same economic periods, each
  with the restating filing's `knownAt`. The restated values do not replace the
  original facts for earlier cutoffs; both remain replayable.
- **Comparative-period restatements:** the filing asserts new facts for its
  comparative periods (including possibly different values than the original
  filing); those facts bind to their own periods and to the new filing, with
  `comparativeOf` linking the presentation pair. Comparatives never
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
  a newer premise or resurrect a canceled predecessor. §19 specifies bounded
  dependency traversal and failure behavior; future fixtures remain unimplemented.

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
  not silently rewrite historical admission proof. §21 defines fail-closed replay-use handling
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

## 13. P2-R1/R2 historical decision register

This table preserves the P2-R1/R2 assessment; the current P2-R4 register in §24
replaces its open technical status. All recommendations remain **NOT APPROVED**. They are
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
| P2-R4 — close bounded contract decisions and prepare exact freeze-review text (completed design bundle; §§18–24) | Resolve D01–D04 and design aspects of D06/D09; existing frozen V0.2 is a compatibility constraint; fundamental contract remains proposal until separately approved | Repository evidence and explicitly synthetic contract examples; no live acquisition | All three schemas, logical/equality keys, clocks, scopes, numeric limits, cancellation and bounded traversal have explicit valid/invalid examples; no must-fix ambiguity; review T01–T35 below | Codex Astra / High | COMPLETE as proposed design; independent freeze review and explicit approvals remain |
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

## 17. Historical P2-R3 freeze readiness and review outcome

At the P2-R3 baseline: **NEEDS_TARGETED_REVISION** (current P2-R4 assessment: §25).
P2-R1 independent review, P2-R2 semantic refinement
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

## 18. P2-R4 exact binding candidate (2026-10-09 WIB)

**PROPOSAL — NOT FROZEN.** P2-R4A technical resolution and P2-R4B schemas below
replace earlier preliminary sketches, not frozen contracts. Proposed scope:
statement financial facts, filing identity and bounded supporting conventions.
Corporate actions, dividend/issuance notices, entitlement/payment events and
DilutionObservation are excluded pending explicit D04 approval. Statement share
counts and period-bound CashDividend/DividendPerShare remain supported. Source
event originals may be retained only under separate acquisition permission; they
do not become OtherReportedConcept statement facts by fabricating a period.
A future additive event contract must define event identity, dates, terms,
security applicability and revisions independently; no event placeholder fields
are reserved in this schema.

### 18.1 Schema notation, serialization and failures (S01)

The field tables are the complete schema. Every listed property is **required
and nonnull** unless its type ends in `?`; `?` means required property permitting
JSON null, **not omission**. No additional properties are allowed, recursively.
This stricter fixed-shape candidate is new-family behavior, not a change to V0.2.
All table fields inherit the validation, revision and PIT rules here. `[]` is an
ordered array unless declared a set. A set must be unique and already sorted by
the canonical JSON of each member, ordinal UTF-16 comparison. A null identity
component means unknown; it never proves cross-record economic equivalence.

| Type | Exact representation / validation |
|---|---|
| Code | JSON string, 1–200 UTF-16 code units, no control characters, no leading/trailing whitespace; ordinal, case preserved; no U+001F |
| Text | Same as Code but 1–2000 code units; no normalization or trimming |
| UUID | Nonzero lowercase hyphenated UUID D string |
| Hash | 64 lowercase hexadecimal SHA-256 characters |
| Date | Exactly yyyy-MM-dd, valid Gregorian date in 0001–9999 |
| Instant | Valid UTC yyyy-MM-ddTHH:mm:ss[.1–7 digits]Z, Gregorian range 0001–9999, seconds 00–59; compare actual instants; no offsets/leap seconds |
| Int32 / Int64 | JSON integer lexeme without decimal/exponent/leading plus, in signed 32/64-bit range, further restricted below |
| Bool | JSON true or false only |
| Decimal | JSON string satisfying §18.2 |
| enum(...) | Case-sensitive string containing exactly one listed token |
| Ref | Object {evidenceId: UUID, revisionNumber: Int64 > 0}; exact retained ASSERT, correct claim/kind, never latest; both fields authenticate the same record |
| EntityKey | Object {namespace: Code, code: Code}; stable evidenced namespace/code, never inferred ticker/name identity |
| Artifact | Object {artifactId: UUID, sha256: Hash, byteLength: Int64 >= 0, locator: Text}; ID/hash/length must match existing raw_artifact and authenticated local bytes; locator identifies exact retained content, never a live lookup |

All Ref fields are dependency edges except the explicitly non-governing presentation
link. All Artifact fields must authenticate bytes and local rooted paths under
the existing archive safety pattern; hash alone does not authenticate issuer truth.
All objects/arrays are bounded: canonical payload ≤65,536 UTF-8 bytes, complete
record ≤131,072 bytes, JSON depth ≤16, each array ≤256. Reject before unbounded
allocation; no truncation. Input transport must enforce the same record byte cap.
Dependency graph bounds are separate (§19). Parser XML/ZIP budgets are a future
source-adapter requirement, not authorization to parse now.

Strict UTF-8 JSON; reject duplicate/unknown/missing properties, comments, trailing
content, invalid Unicode, numeric enums and coercion. Canonical object properties
sort ordinally, arrays preserve declared order. Reuse the existing
ScreenerEvidenceJson canonical writer (compact Utf8JsonWriter default escaping,
no BOM/newline), after strict typed validation. Hash exact stored canonical UTF-8
payload; preserve Decimal and Instant literal spellings. Require stored bytes to
equal their canonicalization and hash, never hide mismatch by reconstructing a
new payload. Future golden-byte fixtures must pin this existing writer behavior;
SDK/writer upgrades require byte-compatibility checks. Complete record exports
use the same writer; recordedAt is included but not part of factual equality.

policyId = fundamental-evidence-v0.1.0; schemaVersion = 1 and
payloadSchemaVersion = 1 exactly. Unknown versions/tokens do not fall back to a
current decoder. Additive future versions must retain the old reader/identities;
new tokens or optional shapes require an explicitly approved version, not a
silent schema expansion.

Proposed family-specific diagnostics (not frozen V0.2 additions):
FUNDAMENTAL_MALFORMED (shape/type/illegal combination),
FUNDAMENTAL_BINDING_UNSUPPORTED (policy/version or excluded claim/metric),
FUNDAMENTAL_BOUND_EXCEEDED, FUNDAMENTAL_INPUT_UNAVAILABLE (missing exact bytes/ref),
FUNDAMENTAL_INTEGRITY_FAILURE (hash/length/identity mismatch),
FUNDAMENTAL_NUMERIC_UNREPRESENTABLE, FUNDAMENTAL_SOURCE_INADMISSIBLE,
FUNDAMENTAL_CHRONOLOGY_INVALID, FUNDAMENTAL_PREMISE_INVALID,
FUNDAMENTAL_INCOMPARABLE, FUNDAMENTAL_CANCELLED and FUNDAMENTAL_CONFLICTING.
Validate in that order: byte/depth bounds; strict syntax/types; supported versions;
conditional schemas/numbers; integrity/ref binding; admission; chronology;
revision/premise validity; requested comparability; resolution. Within one stage
report unique diagnostic tokens sorted ordinally, plus offending JSON pointers.
Reject atomic admission on any error. Reader failure of a potentially decisive
candidate is explicit; do not silently select an older row. Quality is computed,
never stored: missing/invisible/cancelled/invalid → UNKNOWN with diagnostics;
incomplete support → PARTIAL; incompatible survivors → CONFLICTING; admitted
complete assertion → VERIFIED for its exact scope, not an audit of financial truth.

### 18.2 Exact numbers (N01)

Verified repository behavior: EvidenceDecimal in
`src/IdxStockIntelligence.Application/ScreenerEvidenceV02Payloads.cs` uses
`-?(0|[1-9][0-9]*)(\.[0-9]+)?`, literal length ≤200, invariant decimal.TryParse,
then a BigInteger cross-product to reject any rounding or loss. Reuse that type.
Canonical admissible value is exactly `sign * coefficient / 10^scale`, with
coefficient 0..79228162514264337593543950335 and scale 0..28. Maximum absolute
value is 79228162514264337593543950335; smallest nonzero magnitude is 1e-28.
There is no blanket 28-digit *literal* limit: 29-digit coefficients can fit,
and trailing zero fractions beyond 28 places can be admitted if exact and ≤200
characters. -0 is admitted and numerically equals 0; its spelling remains retained.
1.0 and 1.00 compare equal but may hash differently.

Reject exponents, grouping, whitespace, parentheses, plus signs, NaN/Infinity,
JSON numbers, non-ASCII digits and any nonrepresentable result. A parser may
interpret source accounting parentheses only under an exact reviewed mapping;
canonical literal still uses minus. Negative financial values are allowed; a
mapping may impose a metric-specific evidenced constraint, never generic positivity.
Missing amount is not zero. INAPPLICABLE has amount=null and a positive reason.
An unsupported original literal remains in the archived source, not an admitted
numeric FundamentalFact or a new arbitrary-precision observation class.

MAPPING defines only exact power-of-ten normalization and identity/negation
of sign: amount = sourceLiteral × 10^scalePower × sign. scalePower is an Int32
in -28..28; sign is IDENTITY or NEGATE. Both original numeric value and result
must pass EvidenceDecimal, and the transformation must be mathematically exact.
No implicit rounding, FX or double application. Retain the source lexical text
even where a parser supplies a separate valid sourceLiteral. XBRL decimals is
accuracy metadata, not this exponent; resolved units/context belong to the exact
artifact. A source value outside these limits is intentionally unsupported V0.1.
Exact-result checks must detect overflow and inexact decimal division/multiplication
before admission; ordinary decimal arithmetic that silently rounds is insufficient.
No new arithmetic library is required now; future formula implementation must
leave a focused exactness check and may reuse the same integer cross-product
pattern. D07 defines no production formula or rounding policy in this candidate.

### 18.3 Common record envelope (E01)

Column `Key` identifies identity participation: R = record/write identity,
S = revision-series identity, E = economic comparison identity, P = provenance
only. Unless explicitly stated otherwise, fields are immutable; a correction
appends a new revision, an S/E change requires cancel + new identity. Every clock
affects visibility only as specified in §21; no nested field may override it.

| Field | Type / bounds | Meaning, validation, Key / revision / PIT |
|---|---|---|
| evidenceId | UUID | R; immutable retained record identity; exact retries require all fields/content equal |
| policyId | literal above | S; policy namespace; unsupported value rejects |
| schemaVersion | Int32 = 1 | P; envelope decoder binding, no fallback |
| payloadSchemaVersion | Int32 = 1 | P; typed payload binding, no fallback |
| claim | enum(FinancialFiling, FundamentalFact, FundamentalConvention) | S; determines schema and derived class |
| scopeKind | enum(REPORTING_ENTITY, SOURCE) | S/E; SOURCE only for conventions; others REPORTING_ENTITY |
| subject | EntityKey | S/E; reporting-entity key for filing/fact; convention uses {namespace: "source", code: sourceId} |
| sourceId | Code | S/P; exact existing source.source_id; local formulas/mappings use a separately reviewed local source row, not an invented provider |
| sourceReference | Code | S; stable native or explicitly local assertion-lineage reference; never URL-derived; §19 binds its meaning |
| assertionKey | Code | S; stable evidenced document/convention/individual assertion identity; §19 defines derivation |
| revisionSeriesId | string, 1–400 code units | S; exactly sourceId + U+001F + "fundamental-evidence-v0.1.0/" + claim + "/" + assertionKey; the only permitted control char is this separator |
| revisionNumber | Int64 > 0 | R; local append ordinal starting at 1, consecutive within series; not publisher revision number |
| supersedesRevisionNumber | Int64? > 0 | P; null only at revision 1; later revision names immediately preceding retained revision, same identity |
| lineageEvidence | Artifact? | P; null only for first revision; correction/withdrawal proof bound to new record; never URL similarity |
| authorityTier | enum(T1, T2, T3, T4, T5) | P; must equal scope grant; reported filing/facts T1–T3; DERIVED T4; conventions T1–T3 under reviewed local/document authority; T5 never sole admitted fact |
| publishedAt | Instant? | P; evidenced exact publication of this assertion; null if unknown/date-only or local derived |
| publicationDate | Date? | P; source-stated date if instant unknown; null with publishedAt present; never converted to midnight |
| publicationEvidence | Artifact? | P; required if either publication field nonnull; retained supporting proof, not proof merely from file metadata |
| receipt | Receipt? | P; required for filing, REPORTED fact, documentary convention and all CANCEL records; DERIVED uses null; Receipt below |
| knownAt | Instant | P; actual local validated admission/computation time, ≥ receipt and all governing premise clocks |
| recordedAt | Instant | P; actual local persistence time ≥ knownAt; diagnostic, not commit/MVCC time or replay cutoff |
| admission | Admission | P; exact immutable scoped decision snapshot, §21; never equivalent to current registry flag |
| payload | Payload | P; exact typed assertion/cancellation below |
| payloadSha256 | Hash | P; SHA-256 of stored canonical payload; compare before typed reconstruction |

Receipt = {ingestionRunId: UUID, artifactId: UUID, retrievedAt: Instant}.
All three fields identify the existing raw_fetch_observation composite key
(ingestion_run_id, raw_artifact_id, fetched_at); verify its source/bytes and
actual receipt, with knownAt ≥ retrievedAt. This is not a fictitious observation
UUID. Every REPORTED fact's receipt must authenticate its filing artifact;
later parsing uses real later knownAt. Identical refetch is a new observation,
not a reason to rewrite the first admission.
Receipt.retrievedAt must equal the retained PostgreSQL observation instant
exactly; it cannot claim finer precision than that observation contains.

Admission = {decisionId: UUID, evidence: Artifact, reviewedAt: Instant,
route: Code, allowedClaims: set of claim tokens, authorityTier: tier token,
acquisitionAllowed: Bool, retentionAllowed: Bool, replayAllowed: Bool,
validFrom: Instant, validTo: Instant?}. These eleven required fields are an
immutable local review snapshot of the existing source's exact route/claim/use
permission, backed by evidence; not a second source registry. validTo=null means
explicitly evidenced no expiry, not unknown; otherwise validTo≥validFrom, inclusive.
reviewedAt≤knownAt, claim∈allowedClaims, and grant tier equals envelope tier.
Admission knownAt must fall in [validFrom,validTo]; imported receipt must also
fall in that window, with reviewedAt≤receipt.retrievedAt. Prior lawful collection
cannot be invented by a later review. Every supporting artifact must have been
actually retained by the referencing record's knownAt; grant evidence by reviewedAt.
No inferred permission: unknown fields/rights cannot be true. Imported bytes
require acquisitionAllowed; local calculation route LOCAL_CALCULATION requires
retention/replay rights and separately approved formula, not external acquisition.
DecisionId may be shared only with byte-identical Admission and same sourceId.

Payload = {operation: enum(ASSERT,CANCEL), completeness: enum(FULL,PARTIAL),
value: claim object?}. All three required. ASSERT requires object; CANCEL requires
null, FULL, explicit supersedes and lineageEvidence, and cannot be revision 1.
PARTIAL never relaxes fields; it records incomplete evidence for the named fact,
not complete publisher coverage or a negative assertion. Cancellations inherit
the target class/subject/key and must have its permitted authority, not a lower
tier override. Publication for a cancellation is its withdrawal publication,
not a copied target timestamp.

### 18.4 Shared financial bindings (B01–B05)

Every row inherits S01's required/null/immutable/PIT rules. Field names separated
by commas each have the stated type/rule; object shapes are closed.
B01 = Period; B02 = Unit; B03 = ShareBasis; B04 = Accounting;
B05 = Format and SourceConcept. These identifiers name validation groups, not
additional fields or versions.

| Object / fields | Type, meaning and validation | Identity / revision / PIT |
|---|---|---|
| Period.periodType | enum(INSTANT,DURATION) | E; exact temporal kind |
| Period.startDate | Date?; null INSTANT, required nonnull DURATION ≤endDate | E; no invented event duration |
| Period.endDate | Date | E; actual economic boundary, not knowledge |
| Period.fiscalYear | Code?; source-declared label | Comparison constraint; not end-year inference |
| Period.fiscalStart, fiscalEnd | Date?; both null or both nonnull; fiscalStart≤start/end≤fiscalEnd | Comparison constraint; evidenced year boundaries, transitional years retained |
| Period.fiscalQuarter | Int32? in 1..4; nonnull only QUARTER or YTD | Comparison constraint; requires fiscal boundaries and declaration |
| Period.periodKind | enum(INSTANT,QUARTER,YTD,ANNUAL,TTM,OTHER) | E; INSTANT iff periodType INSTANT; TTM only DERIVED |
| Period.cumulative | Bool; false INSTANT/QUARTER/TTM; true YTD; ANNUAL true; OTHER source-defined | E; ANNUAL requires fiscal bounds and exact start/end equal those bounds; YTD start=fiscalStart |
| Format.kind | enum(XBRL,XLSX,OTHER) | P; actual format, OTHER source-native only |
| Format.schemaId, schemaVersion | Code | P; actual documented schema/layout, no label guessing |
| Format.taxonomyFamily, taxonomyVersion | Code?; both null or both nonnull; XBRL both required | P/comparison constraint, not automatic economic identity |
| Unit.kind | enum(MONETARY,SHARES,PER_SHARE) | E; metric-compatible; OtherReportedConcept supports only these numeric units |
| Unit.currencyCode | string? matching [A-Z]{3}; nonnull MONETARY/PER_SHARE, null SHARES | E; evidenced currency identifier; no default/FX |
| ShareBasis.countBasis | enum(POINT_IN_TIME,PERIOD_END,WEIGHTED_AVERAGE,UNKNOWN) | E; POINT_IN_TIME/PERIOD_END requires INSTANT for share counts; WEIGHTED_AVERAGE requires DURATION |
| ShareBasis.calculationBasis | enum(NOT_APPLICABLE,BASIC,DILUTED,UNKNOWN) | E; EPS requires BASIC/DILUTED, never inferred from countBasis |
| ShareBasis.quantityBasis | enum(ISSUED,OUTSTANDING,TREASURY,EPS_DENOMINATOR,UNKNOWN) | E; distinguish issued/outstanding/treasury and EPS adjustments |
| ShareBasis.shareClass | EntityKey? | E; exact class identity; null means unknown, blocks per-share/security use |
| ShareBasis.securityBinding | Ref? to ENTITY_SECURITY convention | P; exact effective mapping when security applicability claimed; null permits entity-level fact only |
| Accounting.statementFamily | enum(GENERAL_CORPORATE,BANK,INSURANCE,OTHER,UNKNOWN) | Comparison constraint, must agree with filing and mapping when known |
| Accounting.consolidation | enum(CONSOLIDATED,STANDALONE,UNKNOWN) | E; independent scopes; no implicit group=parent |
| Accounting.group | EntityKey? | E; required when CONSOLIDATED, null STANDALONE; UNKNOWN cannot establish equivalence |
| Accounting.attribution | enum(TOTAL,PARENT_OWNERS,NONCONTROLLING,NOT_APPLICABLE,UNKNOWN) | E; NetIncome/TotalEquity must state known attribution for comparisons |
| Accounting.operations | enum(ALL,CONTINUING,DISCONTINUED,NOT_APPLICABLE,UNKNOWN) | E; preserve profit scope |
| Accounting.auditStatus | enum(AUDITED,REVIEWED,UNAUDITED,UNKNOWN) | P/comparison constraint; no automatic correction precedence |
| Accounting.standard, standardVersion, policyBasis, restatementBasis | Code? each | Comparison constraints; unknown prevents calculations requiring them; exact known values must match unless future approved bridge exists |
| Accounting.statementScope | enum(PRIMARY_STATEMENTS,NOTES_TO_FINANCIALS,SUPPLEMENTARY) | E; mapping must authorize exact scope |
| Accounting.dimensions | set of {axis: Code, member: Code}; unique axis; explicit complete resolved material dimensions | E; unknown material dimensions force PARTIAL/native candidate, never silently dropped |
| SourceConcept.namespace, code | Code each | Native E for OtherReportedConcept; qualified concept or stable layout field; locator alone not economic identity |
| SourceConcept.label | Text | P; original label, never identity |
| SourceConcept.locator | Text | P; exact XML node or sheet/cell in filing's artifact |
| SourceConcept.contextRef, unitRef | Code? each; required XBRL, null otherwise | P; artifact-scoped references, resolved against exact bytes |
| SourceConcept.decimals | Code?; XBRL only, "INF" or -?(0\|[1-9][0-9]*) integer string in -1000..1000 | P; accuracy metadata, not scale |
| SourceConcept.sourceLexical | Text? | P; original source text; null only INAPPLICABLE |
| SourceConcept.sourceLiteral | Decimal? | P; mapped pre-scale numeric literal, null only INAPPLICABLE |

Unknown statement family, policy, audit or attribution is retained honestly;
none is positive evidence of compatibility. INAPPLICABLE still binds a Unit and
Period describing the positively excluded concept; Unit is not inferred from a
missing value. Canonical monetary metrics use MONETARY, EarningsPerShare and
DividendPerShare use PER_SHARE, SharesOutstanding uses SHARES. OtherReportedConcept
can use any declared supported unit. Canonical instant/duration metric restrictions
are those in §3; CashDividend/DividendPerShare are DURATION only. SharesOutstanding
requires ShareBasis; EPS requires WEIGHTED_AVERAGE + EPS_DENOMINATOR and known class.
DividendPerShare requires evidenced class/count basis and NOT_APPLICABLE
calculationBasis (no automatic EPS-denominator substitution). Other metrics have
shareBasis=null unless source-native share/per-share units require it. There is
no negative-share generic policy here: reject only if a reviewed mapping says so.
NetIncome/TotalEquity attribution may be TOTAL, PARENT_OWNERS, NONCONTROLLING
or UNKNOWN, never NOT_APPLICABLE; UNKNOWN cannot satisfy an attribution request.

### 18.5 FinancialFiling ASSERT (FILING01)

Envelope scope REPORTING_ENTITY; subject is the statement reporting entity.
Class FILING_RECORD. Payload value has exactly these fields:

| Field | Type / rules | Identity / revision / PIT |
|---|---|---|
| documentKey | Code | S; stable source-namespaced document lineage; not URL/hash; assertionKey = §19 hash of subject + documentKey |
| artifact | Artifact | P; exact one retained document/container; must match receipt artifactId; attachments require their own bound records |
| reportingName | Text | P; source name, not stable identity |
| legalEntity | EntityKey? | P; evidenced legal entity, null if unknown; group subject may differ |
| statementFamily | statement-family enum above | P/comparison constraint; never inferred sector |
| format | Format | P; exact original schema/taxonomy/layout |
| coveredPeriods | set of Period, 1..256 | P; every reported fact Period must appear exactly; this is observed document coverage, not complete publisher history |
| documentRole | enum(ORIGINAL,CORRECTION,RESTATEMENT,UNKNOWN) | P; CORRECTION/RESTATEMENT requires lineageEvidence and supersedes; UNKNOWN never claims complete lineage |
| revisionCoverage | enum(OBSERVED_ONLY) | P; finite local versions only; no claim to recover unseen publisher originals |

A correction revision may change artifact, documentRole, coveredPeriods and
provenance without changing document identity. Facts are separate assertions;
filing correction does not update their values or silently migrate their refs.
If no stable documentary lineage can be proved, use a separately documented
local assertion key and UNKNOWN role, preserve both versions, and do not merge
by URL/date/value. Unknown document lineage blocks claimed restatement selection.

### 18.6 FundamentalFact ASSERT (FACT01)

Envelope scope REPORTING_ENTITY; subject is the financial reporting entity, not
an instrument UUID. Class derives from factKind. Every field below is required;
nullable fields use null in the opposite branch.

| Field | Type / rules | Identity / revision / PIT |
|---|---|---|
| metric | enum of admitted §3 tokens excluding DilutionObservation | E; no new metric from label; FreeCashFlow DERIVED only, DividendPerShare REPORTED only in this scope |
| factKind | enum(REPORTED,DERIVED) | S/E; distinct assertion family, T4 only DERIVED |
| applicability | enum(APPLICABLE,INAPPLICABLE) | Value equality, correction may change; DERIVED APPLICABLE only |
| inapplicabilityReason | Text? | Required only INAPPLICABLE, otherwise null; positive source proof |
| amount | Decimal? | APPLICABLE nonnull; INAPPLICABLE null; factual numeric equality, not identity |
| unit | Unit | E; even INAPPLICABLE needs declared excluded concept unit |
| period | Period | E; exact economic scope, §20 |
| accounting | Accounting | E components and comparison constraints per §18.4; no ticker substitution |
| shareBasis | ShareBasis? | E components; required for SharesOutstanding, EarningsPerShare, DividendPerShare and native share/per-share facts |
| definitionId | Code | E; reviewed semantic concept definition; distinguishes paid/declared dividends, debt/capex components; mappings may align sources only with same evidenced definition |
| sourceConcept | SourceConcept? | REPORTED required; DERIVED null; exact original locator/lexical/unit/context |
| filing | Ref? to FinancialFiling | REPORTED required; DERIVED null; subject, period, receipt and family must bind |
| convention | Ref to MAPPING or FORMULA_REFERENCE | REPORTED mapping; DERIVED formula; exact version/source/type/scopes must match |
| comparativeOf | Ref? to FundamentalFact | Non-governing presentation link only, REPORTED only; same filing, different period, same metric/definition/entity/scopes; points to retained current-period fact |
| derivation | Derivation? | DERIVED required; REPORTED null; exact roles/versions/chronology below |

Derivation = {formulaId: Code, formulaVersion: Code, calculatedAt: Instant,
inputs: array of Input, 1..32}. Input = {role: Code, fact: Ref}. Roles are unique,
sorted ordinally, and equal the formula convention's role set. formulaId/version
must equal exact convention. calculatedAt is actual local calculation time,
≤knownAt and ≥all premise knownAt values; no source publication or receipt of its
own. Each Ref must name APPLICABLE complete input facts, exact immutable revisions;
same fact in different roles only if formula explicitly permits it. Formula
definition determines each role's metric/basis/unit/period constraints and output
unit/currency; lack of an approved, executable definition rejects admission.
This is a lineage schema, not authorization of FCF/debt/TTM calculations. Synthetic
lineage examples below use a clearly isolated test-only formula.

REPORTED mapping sourceId/format/native concept/definitionId/target metric/period
type/unit and all declared scopes must match; sign/scale transform must reproduce
amount exactly. Raw fact source equals filing source; provider statements cannot
borrow T1 simply by linking an issuer filing. Local corrections to a bad mapping
use new convention evidence and actual knownAt; no retrospective admission.

### 18.7 FundamentalConvention ASSERT (CONV01)

Envelope SOURCE scope and subject {namespace:"source",code:sourceId}; class
SOURCE_CONVENTION. value = {conventionId: Code, version: Code,
kind: enum(MAPPING,ENTITY_SECURITY,FORMULA_REFERENCE), support: set of Artifact
1..16, mapping: Mapping?, entitySecurity: EntitySecurity?, formula: Formula?}.
Exactly one branch is nonnull, matching kind. conventionId/version/kind are S;
support is provenance, all refs must be visible/authorized at convention knownAt.
assertionKey is §19 hash of conventionId/version/kind. A new version is a new
series; a correction of the same version explicitly supersedes, never latest map.

| Mapping field | Type / validation | Identity / revision / PIT |
|---|---|---|
| appliesToSourceId | Code | S; exact existing raw-fact source, not automatically envelope local review source |
| format | Format | S; exact documented schema/version/taxonomy |
| parserId, parserVersion | Code each | P; exact extraction/lexical interpretation implementation; mapping version binds both, never current parser fallback |
| conceptNamespace, conceptCode | Code each | S; exact qualified concept/stable layout field |
| metric | admitted metric token | E target; OtherReportedConcept retains native identity |
| definitionId | Code | E target; definition supported in immutable support |
| periodType | enum(INSTANT,DURATION) | Scope constraint; actual permitted period type |
| statementFamily | statement-family enum | Scope constraint; UNKNOWN never maps BANK to industrial concepts |
| statementScope | statement-scope enum | Scope constraint; explicit primary/notes/supplementary authorization |
| unit | Unit | E target; resolve scoped original unit, not unitRef spelling |
| scalePower | Int32 -28..28 | Exact N01 normalization exponent, correction requires new convention record |
| sign | enum(IDENTITY,NEGATE) | Exact sign transformation; no hidden capex convention |

Mapping identity includes appliesToSourceId/format/conceptNamespace/conceptCode;
changing any under the same conventionId/version requires cancel + new series.
Identity-preserving definition/scale correction still needs new admitted facts;
earlier facts are never rewritten. OtherReportedConcept needs a reviewed native
identity/unit/period mapping, even though canonical financial meaning is unknown.
Unknown/unresolved source concepts remain originals/candidates; partial numeric
mapping is not a canonical assertion.

| EntitySecurity field | Type / validation | Identity / revision / PIT |
|---|---|---|
| issuer, legalEntity, reportingEntity | EntityKey each | S/E; separately evidenced issuer/legal/reporting identities; no name-derived equality |
| consolidationGroup | EntityKey? | S/E; null standalone, group reporting relation explicitly proved |
| instrumentId | UUID | S/E; exact existing instrument row, not issuer_name |
| security, shareClass | EntityKey each | S/E; tradable security vs economic share class; several instruments/classes require separate mappings |
| effectiveFrom, effectiveTo | Date, Date? | E interval inclusive; to≥from; null open only if evidenced; interval correction does not silently replace another mapping's scope |
| references | set of {kind: enum(TICKER,ISIN), value: Code, from: Date, to: Date?} | P; interval within mapping; ISIN syntax [A-Z]{2}[A-Z0-9]{9}[0-9], syntax alone never proves identity; ticker ordinal; no current-symbol fallback |

At each requested effective date/cutoff, issuer/security relationship must be
visible, unwithdrawn, unconflicted and cover that date. Issuer/group facts remain
entity-level unless an exact mapping and separately approved consumption policy
authorize their use for that security/share class. Relationship does not allocate
issuer totals to every instrument, create valuation formulas, or prove EPS basis.
Changing entity/security/class identity needs cancel + new mapping; date corrections
require evidenced same mapping identity and retain earlier cutoff history.

| Formula field | Type / validation | Identity / revision / PIT |
|---|---|---|
| formulaId, formulaVersion | Code each | S/E; stable approved definition/version, no mutable current formula |
| definition | Artifact | P; exact retained mathematical/component/sign/period/unit/result constraints and algorithm, no opaque URL-only approval |
| approvalId | Code | P; exact explicit accounting/build approval record bound by definition/support; synthetic fixture approval is fixture-only |
| roles | set of Code, 1..32 | E; exact named roles; constraints in definition; no unordered UUID substitution |
| repeatedInputAllowed | Bool | E constraint; otherwise two roles cannot reference same fact |
| resultMetric | admitted metric token | E; agrees with fact metric; no new investment threshold |
| resultDefinitionId | Code | E; exact output meaning, matches fact definitionId |
| resultUnit | Unit | E; agrees exactly, no implicit conversion |
| resultPeriodType | enum(INSTANT,DURATION) | E constraint; actual dates/coverage validated by definition and §20 |

Formula references do not embed a new expression language. The separately approved
definition must be exhaustive enough for an independently written deterministic
implementation, including exact result representability. No production formula
is supplied or approved by this document. Absent such an approval the schema can
round-trip a candidate, but production derived admission is blocked (D07).

## 19. Identity, equality, revision and premise algorithm

### 19.1 Exact identity projections (ID01)

Let C(x) be S01 canonical JSON and H(x) lowercase SHA-256 of UTF-8 C(x).
assertionKey is exactly H of the following object, including all nulls:

- FinancialFiling: {subject, documentKey}. sourceReference=documentKey.
- FundamentalConvention: {conventionId, version, kind}. sourceReference is
  conventionId + "/" + version + "/" + kind (must fit Code; otherwise reject).
  Kind-specific S fields must remain invariant within that series.
- FundamentalFact: {subject, sourceReference, factKey}, where factKey is the
  exact economic projection below. SourceReference is a stable **individual
  assertion** reference, not the common filing/document reference. If the source
  lacks such an ID, retain `local:` + H({documentKey, conceptNamespace,
  conceptCode, factKey}) as the explicit local lineage. Cross-document correction
  may reuse it only with lineageEvidence proving the same assertion. Without
  such proof create a separate local reference and keep the alternatives.

FactKey = {metric, factKind, nativeConcept, definitionId, periodKey,
accountingKey, unit, shareKey}. nativeConcept is null for canonical metrics,
otherwise {namespace,code} from SourceConcept (OtherReportedConcept REPORTED
only). periodKey={periodType,startDate,endDate,periodKind,cumulative}.
accountingKey={consolidation,group,attribution,operations,statementScope,dimensions}.
shareKey is null without ShareBasis, otherwise {countBasis,calculationBasis,
quantityBasis,shareClass}. Reporting subject and policy/claim participate in the
outer series. SecurityBinding, filing ref, convention UUID/version, audit status,
taxonomy, recorded/knowledge clocks and amount do not define economic identity.
DefinitionId changes *do* change identity. Calculation lineage is retained in
DERIVED sourceReference (local H of formulaId/version + named exact inputs), so
different formula/input revisions cannot silently overwrite each other.

Economic resolution key = {subject,factKey}; sourceId/sourceReference are omitted
only for competition across proved identical economic claims. Source-native
concept namespaces prevent label-based cross-source merging. Unknown components
may group candidates for diagnostics but cannot establish VERIFIED agreement or
calculation comparability. Different known entity/attribution/share/period keys
are separate facts, not conflicts. Filing resolution uses exact sourceId/subject/
documentKey; cross-source documents are separate provenance without a separately
proved identity relationship. Convention resolution requires exact requested
sourceId/conventionId/version/kind and kind-specific S fields, never latest version.

Two admitted fact candidates agree only if economic keys match, applicability
matches, and applicable amounts have exact decimal equality; INAPPLICABLE also
requires equal reason. Compare fiscal boundaries/labels, accounting standard/
policy/restatement basis and audit constraints before declaring comparability;
different or unknown required constraints yield INCOMPARABLE, not automatic
equal values. Equal amounts do not repair mismatched definitions. Source/native
locators, spelling, archive IDs and clocks remain separate provenance. Filing
assertions agree if subject/documentKey and artifact hash/length, coveredPeriods,
format and documentRole agree; conventions agree only with equal typed branch
contents (excluding support provenance). Preserve all agreeing provenance,
including separate numeric spellings. Hash collisions/inconsistent retained
key preimages fail integrity validation rather than merging records.

### 19.2 Revision and cancellation (REV01)

Within each series, revision 1 is ASSERT, null supersedes/lineageEvidence.
Revision n>1 names n-1; require previous record retained, same source/claim/
subject/assertionKey and invariant S/E components, nondecreasing knownAt and
previous record visible at new knownAt. Append is atomic with a unique
(revisionSeriesId,revisionNumber); no concurrent branches/duplicate ordinals.
New bytes or later receipt alone never prove supersession. Changing an identity
component requires explicit CANCEL of the mistaken identity plus a new series.
An identity-preserving corrected value is a new ASSERT with exact documentary
lineage. An explicitly evidenced reinstatement after CANCEL can append ASSERT
in the same series; it needs fresh admission and lineage, never automatic fallback.

At cutoff select visible series head by consecutive local ordinal; unavailable
predecessor/invalid linkage fails closed. A visible CANCEL withdraws the lineage's
active assertion and prevents resurrection of predecessors. CANCEL targets the
immediately preceding ASSERT; consecutive CANCEL is invalid. If the head is a
new ASSERT after a cancellation, validate its explicit reinstatement proof.
Cancellation does not alter prior-cutoff state or delete any bytes/record.

**Filing revisions are independent of fact revisions.** Many facts share a filing
ref but have different FactKeys/series. A revenue correction cannot supersede
assets or profit. Conservatively, an exact governing filing/convention/fact ref
must still be its series' active ASSERT at the query cutoff. A correction of a
governing filing therefore makes dependent old refs unavailable for **new**
selection until explicitly readmitted, even for unchanged values. It does not
silently move them to the replacement filing. This availability cost is an
explicit proposed trade-off; unrelated facts' identities/values remain retained,
and prior cutoff replay still succeeds. A fact correction alone affects only its
own series and derived dependents, not sibling facts. Withdrawal has the same
no-substitution rule. Exact retained-input replay must use the original retained
decision cutoff, not today with an exemption for old premises.

### 19.3 Deterministic premise closure and conflicts (PREM01)

For a requested fact, traverse filing/convention, ShareBasis.securityBinding,
and derivation.inputs refs. Follow all governing refs recursively; Artifact/support/
admission references authenticate exact bytes but are not fundamental-record
edges. comparativeOf is presentation-only: authenticate target identity, same
filing and visibility at creation, but do not recursively use its validity as a
numeric premise or let it supersede either fact. All actual dependency edges
must be visible and active at referencing knownAt and at query cutoff.

Sort outgoing edges by field name then role then evidenceId ordinal; DFS with
visiting/visited sets detects cycles and shares nodes. Maximum path length 16
edges and 256 unique fundamental records including root; each fact ≤32 input
roles; never exceed the array/payload bounds. Duplicate ref via different roles
is permitted only when formula allows it, counts once toward node limit and keeps
both roles. A missing exact UUID/revision, cyclic graph, withdrawn/superseded
premise, unpermitted replay or bound excess prevents selection, with explicit
diagnostic; never truncate closure, substitute latest or use the failed node as
zero. Premises with same knownAt are allowed only with acyclic retained commit
order/atomic dependency admission; recordedAt is not an ordering workaround.

Resolution order: exact requested subject/key/period/basis → visible active
series heads → authenticated admitted complete premises → applicable authority
→ typed equality. Lower tier numbers outrank higher ones only among otherwise
comparable admissible assertions; DERIVED and REPORTED are separate keys here.
No invented scope-specificity score: a request specifies exact scope; a broad
or unknown scope cannot beat it. Equally authoritative unequal survivors yield
CONFLICTING; later receipt/publication/UUID never selects between them. T5 may
corroborate but is not an admitted sole assertion. Preserve disagreement and
all provenance, including lower-tier observations. For numerically equal survivors
only, representative order is knownAt descending, revisionNumber descending,
revisionSeriesId ordinal, evidenceId ordinal; this does not erase other refs.
Any potentially decisive PARTIAL candidate or failed candidate/premise remains
an explicit partial/failure result and blocks an affirmative fallback. Filtering
to complete inputs must not discard an unresolved higher/equal-authority assertion
to manufacture VERIFIED from a convenient lower/older record.

## 20. Financial period and dimension compatibility

P01 validates Period independently of labels. INSTANT has null start/fiscalQuarter,
periodKind INSTANT, cumulative=false. DURATION has start≤end and a permitted
duration kind. ANNUAL exactly matches declared fiscalStart/end. QUARTER is a
source-declared standalone fiscal quarter with fiscalQuarter and both fiscal
boundaries; YTD is cumulative from fiscalStart with declared quarter when present.
OTHER preserves explicit source duration when fiscal classification is unknown;
do not invent a quarter. Comparative fact keeps its own Period and the new filing's
knowledge clocks, not the clocks of the earlier economic period. An annual
comparative may have different audit/policy metadata; presentation link is not
restatement proof. Source-reported TTM is OtherReportedConcept with OTHER duration
and its reviewed source definition; TTM periodKind is local DERIVED only.

P02: same-period competition needs exact periodKey. Historical trends need a
separately approved comparison policy for different dates; V0.1 does not approve
growth formulas or annualize irregular years. Fiscal-year changes, short/long
years and 52/53-week years keep explicit dates/fiscal identity. FiscalQuarter=4
does not mean calendar Q4. Missing fiscal identity prevents alignment-dependent
calculation, not honest admission of an OTHER duration fact.

P03: YTD-to-quarter is allowed only after D07 formula approval. Exact role-bound
inputs must be additive flow concepts with same fiscal start, a shorter nested
earlier duration and longer later duration; result starts the day after shorter
end and ends at longer end. Both share known entity/consolidation/attribution/
operations/dimensions/definition/currency/unit/policy/restatement basis. Same
fiscal calendar and coherent original/restated operands are required; mixed
audit status needs an approved compatibility definition, not implicit acceptance.
Q4 annual-minus-nine-month uses the same rules. Never subtract EPS, weighted-average
shares, stocks or arbitrary ratios merely because dates nest.

P04: local TTM composition needs approved formula, four proved standalone fiscal
quarters, contiguous inclusive dates (next.start = previous.end + one day), no
duplicate/overlap/gap, exact full trailing fiscal-year coverage and common P03
basis. 52/53-week coverage follows evidenced fiscal quarters, not 365-day guess.
Annual+YTD or five-quarter alternatives are outside this candidate unless a
separate approved formula proves nonoverlapping decomposition; no blanket
sum/annualization is defined. Inputs refer exact revisions and one coherent
restatement basis. Source-reported TTM is not local TTM lineage.

DIM01: entity, consolidation, attribution, continuing/discontinued scope and
material dimensions are economic identity. Total consolidated profit and
parent-attributable profit cannot substitute. Group is distinct from parent legal
entity; standalone vs consolidated never merge. Audit/taxonomy/policy/standard/
statement-family fields are preserved comparison constraints; absence means
unknown. Exact known basis must agree for arithmetic; cross-version bridges are
deferred to separately reviewed convention/formula definitions, not inferred.
Bank/insurance source concepts stay native without an exact reviewed mapping;
sector/name cannot authorize industrial Revenue or OperatingProfit mapping.

SEC01: total outstanding (quantityBasis OUTSTANDING), issued and treasury shares
are distinct. Period-end/point-in-time counts are instant; weighted averages are
durations. Basic EPS denominator = BASIC + WEIGHTED_AVERAGE + EPS_DENOMINATOR;
diluted EPS denominator changes calculationBasis to DILUTED and retains the exact
evidenced dilution definition. Neither is period-end total outstanding shares.
An EPS assertion must bind the corresponding class/basis even when the filing
does not separately disclose a numeric denominator; no denominator value is
invented. Security use requires exact visible ENTITY_SECURITY interval and
separately approved downstream policy. Matching issuer_name, current ticker or
ISIN syntax is never identity proof.
ShareBasis.securityBinding, when nonnull, must match subject/reportingEntity,
consolidation group and shareClass and cover the entire reported fact period
(instant date for INSTANT); a later security request also needs coverage at its
own effective date. Inconsistent declared bindings reject the fact rather than
leaving a misleading association available to consumers.

## 21. Admission, clocks and retained-use rights

ADM01: before acquisition/admission, existing registry must allow the scoped
source and retained Admission must prove claim/route/tier/acquisition/retention
rights. An ALLOWED flag alone is insufficient. Filing/reported assertions T1–T3
need source-grounded proof for that tier; local mapping review is T2 only when
explicitly admitted, never a presumed promotion. DERIVED is T4 from admitted
inputs and approved formula. No source is admitted by these synthetic schemas.
Local source rows are an implementation/deployment dependency, not inserted here.

PIT01: economic period ≠ publication ≠ receipt ≠ local knownAt ≠ recordedAt.
Every record requires actual knownAt and recordedAt≥knownAt. Documentary receipts
must precede knownAt; publication may be earlier/later, but later publication
blocks visibility. Local derived publishedAt/publicationDate/publicationEvidence
and receipt are null; calculatedAt≥all input and formula knownAt and ≤knownAt.
Governing premises must be visible at referencing knownAt; publicationDate alone
does not establish a publication instant. Unknown publication is honestly null;
publicationDate requires retained date evidence, cannot establish intraday public
availability, and is not an invented midnight cutoff. Source timezone/date parsing
requires a reviewed source route; ambiguous zone cannot become publishedAt.

PIT02: Visible(record,C) iff knownAt≤C, receipt.retrievedAt≤C when nonnull,
and publishedAt≤C when nonnull. Apply §19 active-head/premise/admission checks
after visibility. recordedAt does not gate retained local-knowledge replay;
records were actually persisted ≥knownAt, but this does not claim historical
MVCC existence at C. Later parsing, new mapping or derivation has actual later
knownAt. Restore preserves all original clocks/versions. No network/current
mapping/registry-history guess in historical reconstruction.

RIGHTS01: admission snapshot proves historical admission, not perpetual future
use. New acquisitions stop if current registry/route rights cease. Replay at an
old decision cutoff also requires a separately supplied, explicit **current**
retained-use authorization for that source/decision at execution time. The query input is
{sourceId: Code, authorization: Admission} per source, plus executionAt: Instant;
the trusted operator must attest the authorization is currently operative, with
reviewedAt≤executionAt in its valid interval and retentionAllowed/replayAllowed
true. Missing/currently revoked authorization fails closed. It must
authenticate retained terms and allow retention/replay at execution time; unknown,
expired or withdrawn rights fail SOURCE_INADMISSIBLE, never infer grandfathering.
This is a use gate, not a hidden economic cutoff or revised historical grant.
If permission changes, repeatable historical selection is conditional on lawful
retained use; report permission-blocked rather than pretending result changed
financially. No automatic deletion is designed; any required deletion/retention
operation needs separate authorized procedure. A current ALLOWED/DISALLOWED flag
alone cannot establish historical or continuing replay rights. Route-specific
terms, amendments, coverage and revocation evidence remain external blockers.

## 22. Additive logical persistence candidate (STORE01)

Propose one `fundamental_evidence_record` relation; no SQL or DB mutation here.
It contains exactly E01's envelope fields (camelCase maps to snake_case), with
subject, lineage_evidence, publication_evidence, receipt, admission and payload as
closed canonical JSON values; string/enum columns text, UUID uuid, versions int,
revision ordinals bigint, authoritative Instant clock literals **text**,
publication_date date, hash char(64).
Nullable columns correspond exactly to nullable fields. Payload is retained
canonical **text**, not JSONB as the sole byte representation; projections may
be derived for queries but cannot replace authenticated payload text/hash.
Subject and nested data remain schema-validated; no generic entity registry or
wide accounting model is added. Numeric amounts stay lexical strings, not the
price table's numeric(20,6).
PostgreSQL timestamptz has microsecond precision; it must not be the sole
representation of the permitted seventh fractional digit. Preserve exact
Instant literals/tick comparisons in the trusted reader/writer. Any timestamp
query projection is only a conservative prefilter and must not exclude a
potentially decisive record by rounding. The Receipt's existing composite-key
timestamp still authenticates exactly to the existing observed instant. No
existing V0.2 column/clock semantics are changed by this additive recommendation.

Required write constraints: evidence_id primary key; unique series+revision;
policy/version/claim/scope checks; same-series predecessor authentication and
consecutive ordinals under transaction serialization; immutable update/delete
rejection; atomic admission of dependency refs and canonical hash; source_id FK
to existing source; receipt composite reference to raw_fetch_observation;
all Artifact IDs to existing raw_artifact with source/hash/length authentication;
fundamental Ref IDs/revisions to this same family only. Nested refs may be checked
by the trusted atomic writer rather than inventing a second premise relation;
this does not relax DB-facing validation. Exact physical DDL/trigger/index design
belongs to separately authorized P2-I2 and must prove these constraints in owned
disposable storage before deployment. No fundamental FK into Screener tables.

Filing/fact/convention identity and revision history are immutable records in
this relation, not duplicate mutable current-state tables. Idempotent retry of
the same evidenceId requires identical complete envelope, canonical content and
hash, including original recordedAt; a new execution time does not rewrite it.
Different evidenceId with same series/revision is rejected even if numeric value
matches. Multiple source/hash-equivalent facts reuse existing archive identity
and separate observed receipts, honoring raw_artifact(source_id,content_sha256)
uniqueness. Missing exact original bytes or a failed hash never falls back to URL.

PIT queries must retrieve all potentially decisive candidates for requested
subject/economic key and cutoff, their complete visible revision predecessors,
and bounded premise closure. A LIMIT/latest query must not erase a conflict,
cancelled head or high-authority failure. If the candidate set exceeds 256 root
records for one requested economic key, return BOUND_EXCEEDED (no paging around
the resolver); dependency closure is independently bounded. Query ordering is
deterministic, and auth/typed comparison occurs after exact retained row selection.
Indexes are performance choices, not permission to weaken complete selection.

Repository migrations 0007→0008→0009→0010→0011 each require the predecessor.
0001/0002 supply source/archive/receipt foundation. Provisional 0012 follows that
chain, with separately authorized checksum/version/backup/restore preflight and
disposable acceptance; no skipping operationally absent 0007–0011 or deploying
them as a side effect. OPS-1 absence remains a carried-forward report, not a fresh
DB observation. A different migration topology requires its own explicit design
approval. D09 deployment remains unapproved even if this logical contract is
later accepted. Existing migrations and source registry are unchanged.

## 23. Bounded synthetic examples and validation recipes

All names, IDs, dates, values, permissions and originals here are **synthetic**.
They establish no issuer fact or production source admission. The fixture registry
contains only fixture-source-A, fixture-source-B and fixture-local with ALLOWED
fixture-only grants; the real registry is untouched. Fixture artifact IDs use
00000000-0000-4000-8000-n padded to 12 digits.
Their hashes/lengths below are computed from these exact UTF-8 fixture originals
(no trailing newline); paths/metadata are synthetic, never operational. Ingestion
run 60 belongs to source A, 61 to source B, 62 to fixture-local; receipts must
use the matching source's run. Artifact 10/11/12/13 belongs to A, 14 to B and
82 to A, and 80/81/83/84/85/90 to fixture-local. Source-reference and permission evidence may refer
other retained local documentation, but receipt provenance may not cross sources.
These are binding examples, not an implemented XBRL parser or production taxonomy.
Artifacts 10/12/14/80/81/83/84/85/90 are retained by 2025-01-01 in their isolated
fixture graphs; correction 11/82 and withdrawal 13 are first retained on
2025-02-01. No future proof is available to the base January assertions.

Artifact 10 (11 replaces Revenue 100.00 with 80.00; 12 omits the Revenue node;
14 replaces Revenue 100.00 with 120 under source B):

```xml
<xbrli:xbrl xmlns:xbrli="http://www.xbrl.org/2003/instance" xmlns:iso4217="http://www.xbrl.org/2003/iso4217" xmlns:f="fixture-concepts"><xbrli:context id="ctx-annual"><xbrli:entity><xbrli:identifier scheme="fixture">reporting-A</xbrli:identifier></xbrli:entity><xbrli:period><xbrli:startDate>2024-01-01</xbrli:startDate><xbrli:endDate>2024-12-31</xbrli:endDate></xbrli:period></xbrli:context><xbrli:unit id="unit-idr"><xbrli:measure>iso4217:IDR</xbrli:measure></xbrli:unit><f:Revenue contextRef="ctx-annual" unitRef="unit-idr" decimals="0">100.00</f:Revenue><f:OperatingProfit contextRef="ctx-annual" unitRef="unit-idr" decimals="0">20</f:OperatingProfit><f:scope>GENERAL_CORPORATE STANDALONE ALL AUDITED fixture-standard/1 fixture-policy-1 original PRIMARY_STATEMENTS FY2024</f:scope></xbrli:xbrl>
```

Other exact fixture originals:

- Artifact 13: `Synthetic issuer withdrawal of annual-A-2024; received 2025-02-01.`
- Artifact 80: `Synthetic reviewed fixture definitions: Revenue=fixture-revenue; OperatingProfit=fixture-operating-profit; identity issuer-A/legal-A/reporting-A/security-A/class-A/instrument-100 from 2024-01-01 with SYNTH-A, no expiry; exact parser fixture-parser/1; source A and B separate. Fixture-only approval, no production authority.`
- Artifact 81: `FIXTURE_ONLY_NOT_ACCOUNTING_APPROVAL: fixture-only-add/1 roles left and right; distinct complete same-entity annual-period same-accounting IDR amounts 100 and 20; exact sum 120, no rounding; output Revenue/fixture-lineage-output for lineage checking only, no accounting interpretation.`
- Artifact 82: `Synthetic issuer correction annual-A-2024: artifact 11 revises artifact 10, Revenue 80.00 replaces 100.00; received 2025-02-01.`
- Artifact 83: `Synthetic exact publication evidence: annual-A-2024 published 2025-01-10T00:00:00Z.`
- Artifact 84: `Synthetic date-only publication evidence: annual-A-2024 published on 2025-01-01; time and zone unknown.`
- Artifact 85: `Synthetic local lexical correction review: same Revenue assertion and filing, canonical amount 100.0 exactly equals original sourceLiteral/sourceLexical 100.00; no economic value change.`
- Artifact 90: `FIXTURE_ONLY scoped permission for fixture-source-A, fixture-source-B and fixture-local; claims FinancialFiling/FundamentalConvention/FundamentalFact; acquisition, retention and replay allowed from 2025-01-01 without expiry; local calculation separately fixture-approved. No production permission.`

The reviewed fixture metadata binds schema fixture-xbrl/1 and taxonomy
fixture-general/1 to these synthetic contexts, units, scopes and definition IDs.
Changed scenario periods/metrics/lexical values require corresponding changes to
the isolated fixture original and regenerated hash/length/locator proof before
constructing the record; no unchanged hash may pretend to authenticate changed
source bytes. When X02/X03/X08/X13/X14 changes source assertions, regenerate the
original from its exact prescribed facts/scopes (context IDs remain artifact-local).
This does not introduce any source fact or parser behavior into production.
For X10's share-unit rejection, first construct a valid SharesOutstanding clone:
SHARES/null currency, INSTANT at 2024-12-31 with null startDate, periodKind INSTANT
and cumulative=false; ShareBasis PERIOD_END/NOT_APPLICABLE/OUTSTANDING/class-A
with null securityBinding, corresponding INSTANT mapping, unitRef unit-shares
and contextRef ctx-instant. Then mutate only fact.unit to MONETARY/IDR. Missing
shareBasis is a separate malformed case, not the claimed unit-only rejection.


Notation for keeping 15 cases bounded: `clone X with replacements` is a fixture
construction recipe, **not wire JSON**. Clone deeply, replace only the named
paths (all other fields retain their explicit base values), then recompute
assertionKey/revisionSeriesId/payloadSha256 with §19/S01. References name the exact
constructed UUID/revision. A new assertion uses revision=1/null supersedes/null
lineage; a correction preserves assertionKey, names predecessor and supplies
artifact(82) as synthetic correction proof. Recompute keys on valid identity
changes only; the rejected cases deliberately preserve the stated bad binding.
No omission/defaulting rule is added to the production schema by this notation.
Each X case runs in an isolated fixture graph; mutually exclusive variants do
not coexist unless the row explicitly tests conflict. New record IDs are
00000000-0000-4000-8000-(10000 + 100 × case number + construction ordinal),
padded to 12 digits; ordinal starts at 1 in row order (filing, conventions,
reported facts, derived fact/cancel). All Ref fields remap to those exact IDs;
revision numbers follow the explicit recipe, never arbitrary new UUIDs. Artifact
IDs keep the declared fixture IDs. Bare dates used as clocks below mean
T00:00:00Z. A changed numeric value changes amount, sourceLiteral and sourceLexical
together unless the row deliberately tests an invalid binding. A changed metric
changes matching conceptCode/label, mapping target/definition and exact mapping
ref together; sourceConcept.locator must point to that exact changed concept
node rather than retain the Revenue locator. Quarter/YTD filing documentKeys
are fixture-q1-2025 and
fixture-ytd-q2-2025 respectively. X12 uses the EPS fact/ShareBasis from X14;
X13's two amounts are both 100.00 to prove equal numbers do not erase attribution.

Complete X01 closure is the following three records. It covers all required/null
fields of FinancialFiling, MAPPING convention and REPORTED FundamentalFact. Pretty
layout is for review; canonical stored JSON uses S01 compact writer. Artifact
IDs/hashes/lengths and Receipt composite observations must exist in the fixture
metadata. Publication is honestly unknown (all three publication fields null),
and every reference is known before the fact's admission.

**X01.Filing**

```json
{
  "evidenceId": "00000000-0000-4000-8000-000000000001",
  "policyId": "fundamental-evidence-v0.1.0",
  "schemaVersion": 1,
  "payloadSchemaVersion": 1,
  "claim": "FinancialFiling",
  "scopeKind": "REPORTING_ENTITY",
  "subject": {
    "namespace": "fixture",
    "code": "reporting-A"
  },
  "sourceId": "fixture-source-A",
  "sourceReference": "annual-A-2024",
  "assertionKey": "252dc2c806e39ad77770b54412ffe23ff73fd6c9e23fb292b844fac47219396a",
  "revisionSeriesId": "fixture-source-A\u001ffundamental-evidence-v0.1.0/FinancialFiling/252dc2c806e39ad77770b54412ffe23ff73fd6c9e23fb292b844fac47219396a",
  "revisionNumber": 1,
  "supersedesRevisionNumber": null,
  "lineageEvidence": null,
  "authorityTier": "T1",
  "publishedAt": null,
  "publicationDate": null,
  "publicationEvidence": null,
  "receipt": {
    "ingestionRunId": "00000000-0000-4000-8000-000000000060",
    "artifactId": "00000000-0000-4000-8000-000000000010",
    "retrievedAt": "2025-01-02T00:00:00Z"
  },
  "knownAt": "2025-01-03T00:00:00Z",
  "recordedAt": "2025-01-03T00:00:00Z",
  "admission": {
    "decisionId": "00000000-0000-4000-8000-000000000070",
    "evidence": {
      "artifactId": "00000000-0000-4000-8000-000000000090",
      "sha256": "e92931add933d7009c8ebb66e0e749809447406c45e6c6a69f4bf08af0ae0b12",
      "byteLength": 297,
      "locator": "fixture-content"
    },
    "reviewedAt": "2025-01-01T00:00:00Z",
    "route": "FIXTURE_ONLY",
    "allowedClaims": [
      "FinancialFiling",
      "FundamentalConvention",
      "FundamentalFact"
    ],
    "authorityTier": "T1",
    "acquisitionAllowed": true,
    "retentionAllowed": true,
    "replayAllowed": true,
    "validFrom": "2025-01-01T00:00:00Z",
    "validTo": null
  },
  "payload": {
    "operation": "ASSERT",
    "completeness": "FULL",
    "value": {
      "documentKey": "annual-A-2024",
      "artifact": {
        "artifactId": "00000000-0000-4000-8000-000000000010",
        "sha256": "a010c83d017e12c916deef9659f7b947ec5f69615971e0ea96cc03fb929ed9dc",
        "byteLength": 798,
        "locator": "fixture-content"
      },
      "reportingName": "Synthetic Reporting A",
      "legalEntity": {
        "namespace": "fixture",
        "code": "legal-A"
      },
      "statementFamily": "GENERAL_CORPORATE",
      "format": {
        "kind": "XBRL",
        "schemaId": "fixture-xbrl",
        "schemaVersion": "1",
        "taxonomyFamily": "fixture-general",
        "taxonomyVersion": "1"
      },
      "coveredPeriods": [
        {
          "periodType": "DURATION",
          "startDate": "2024-01-01",
          "endDate": "2024-12-31",
          "fiscalYear": "FY2024",
          "fiscalStart": "2024-01-01",
          "fiscalEnd": "2024-12-31",
          "fiscalQuarter": null,
          "periodKind": "ANNUAL",
          "cumulative": true
        }
      ],
      "documentRole": "ORIGINAL",
      "revisionCoverage": "OBSERVED_ONLY"
    }
  },
  "payloadSha256": "9ff4b797beeb2cd9217a22208786807290229e622801adca19f506cce5460ab0"
}
```

**X01.Mapping**

```json
{
  "evidenceId": "00000000-0000-4000-8000-000000000002",
  "policyId": "fundamental-evidence-v0.1.0",
  "schemaVersion": 1,
  "payloadSchemaVersion": 1,
  "claim": "FundamentalConvention",
  "scopeKind": "SOURCE",
  "subject": {
    "namespace": "source",
    "code": "fixture-local"
  },
  "sourceId": "fixture-local",
  "sourceReference": "fixture-revenue-map/1/MAPPING",
  "assertionKey": "7f117108dcac6bbbc0d4f7b1715131e5e67365831e9aea2cacbbf373796dcc36",
  "revisionSeriesId": "fixture-local\u001ffundamental-evidence-v0.1.0/FundamentalConvention/7f117108dcac6bbbc0d4f7b1715131e5e67365831e9aea2cacbbf373796dcc36",
  "revisionNumber": 1,
  "supersedesRevisionNumber": null,
  "lineageEvidence": null,
  "authorityTier": "T2",
  "publishedAt": null,
  "publicationDate": null,
  "publicationEvidence": null,
  "receipt": {
    "ingestionRunId": "00000000-0000-4000-8000-000000000062",
    "artifactId": "00000000-0000-4000-8000-000000000080",
    "retrievedAt": "2025-01-02T00:00:00Z"
  },
  "knownAt": "2025-01-03T00:00:00Z",
  "recordedAt": "2025-01-03T00:00:00Z",
  "admission": {
    "decisionId": "00000000-0000-4000-8000-000000000072",
    "evidence": {
      "artifactId": "00000000-0000-4000-8000-000000000090",
      "sha256": "e92931add933d7009c8ebb66e0e749809447406c45e6c6a69f4bf08af0ae0b12",
      "byteLength": 297,
      "locator": "fixture-content"
    },
    "reviewedAt": "2025-01-01T00:00:00Z",
    "route": "FIXTURE_ONLY",
    "allowedClaims": [
      "FinancialFiling",
      "FundamentalConvention",
      "FundamentalFact"
    ],
    "authorityTier": "T2",
    "acquisitionAllowed": true,
    "retentionAllowed": true,
    "replayAllowed": true,
    "validFrom": "2025-01-01T00:00:00Z",
    "validTo": null
  },
  "payload": {
    "operation": "ASSERT",
    "completeness": "FULL",
    "value": {
      "conventionId": "fixture-revenue-map",
      "version": "1",
      "kind": "MAPPING",
      "support": [
        {
          "artifactId": "00000000-0000-4000-8000-000000000080",
          "sha256": "057ebf9096253171d5fd109380b06b17a316ad6c2fd919c616ca8da44aa05d79",
          "byteLength": 323,
          "locator": "fixture-content"
        }
      ],
      "mapping": {
        "appliesToSourceId": "fixture-source-A",
        "parserId": "fixture-parser",
        "parserVersion": "1",
        "format": {
          "kind": "XBRL",
          "schemaId": "fixture-xbrl",
          "schemaVersion": "1",
          "taxonomyFamily": "fixture-general",
          "taxonomyVersion": "1"
        },
        "conceptNamespace": "fixture-concepts",
        "conceptCode": "Revenue",
        "metric": "Revenue",
        "definitionId": "fixture-revenue",
        "periodType": "DURATION",
        "statementFamily": "GENERAL_CORPORATE",
        "statementScope": "PRIMARY_STATEMENTS",
        "unit": {
          "kind": "MONETARY",
          "currencyCode": "IDR"
        },
        "scalePower": 0,
        "sign": "IDENTITY"
      },
      "entitySecurity": null,
      "formula": null
    }
  },
  "payloadSha256": "fa85d07c1201ab4e36397be87068bcc4aad1db1c0a1c9ab0cedbcec4d953e9f8"
}
```

**X01.Fact**

```json
{
  "evidenceId": "00000000-0000-4000-8000-000000000003",
  "policyId": "fundamental-evidence-v0.1.0",
  "schemaVersion": 1,
  "payloadSchemaVersion": 1,
  "claim": "FundamentalFact",
  "scopeKind": "REPORTING_ENTITY",
  "subject": {
    "namespace": "fixture",
    "code": "reporting-A"
  },
  "sourceId": "fixture-source-A",
  "sourceReference": "native-revenue-2024",
  "assertionKey": "d88954be57d48b1632c2a076aba0e813eb731b881a65c25bac2ccdbf6a2ff57f",
  "revisionSeriesId": "fixture-source-A\u001ffundamental-evidence-v0.1.0/FundamentalFact/d88954be57d48b1632c2a076aba0e813eb731b881a65c25bac2ccdbf6a2ff57f",
  "revisionNumber": 1,
  "supersedesRevisionNumber": null,
  "lineageEvidence": null,
  "authorityTier": "T1",
  "publishedAt": null,
  "publicationDate": null,
  "publicationEvidence": null,
  "receipt": {
    "ingestionRunId": "00000000-0000-4000-8000-000000000060",
    "artifactId": "00000000-0000-4000-8000-000000000010",
    "retrievedAt": "2025-01-02T00:00:00Z"
  },
  "knownAt": "2025-01-04T00:00:00Z",
  "recordedAt": "2025-01-04T00:00:00Z",
  "admission": {
    "decisionId": "00000000-0000-4000-8000-000000000070",
    "evidence": {
      "artifactId": "00000000-0000-4000-8000-000000000090",
      "sha256": "e92931add933d7009c8ebb66e0e749809447406c45e6c6a69f4bf08af0ae0b12",
      "byteLength": 297,
      "locator": "fixture-content"
    },
    "reviewedAt": "2025-01-01T00:00:00Z",
    "route": "FIXTURE_ONLY",
    "allowedClaims": [
      "FinancialFiling",
      "FundamentalConvention",
      "FundamentalFact"
    ],
    "authorityTier": "T1",
    "acquisitionAllowed": true,
    "retentionAllowed": true,
    "replayAllowed": true,
    "validFrom": "2025-01-01T00:00:00Z",
    "validTo": null
  },
  "payload": {
    "operation": "ASSERT",
    "completeness": "FULL",
    "value": {
      "metric": "Revenue",
      "factKind": "REPORTED",
      "applicability": "APPLICABLE",
      "inapplicabilityReason": null,
      "amount": "100.00",
      "unit": {
        "kind": "MONETARY",
        "currencyCode": "IDR"
      },
      "period": {
        "periodType": "DURATION",
        "startDate": "2024-01-01",
        "endDate": "2024-12-31",
        "fiscalYear": "FY2024",
        "fiscalStart": "2024-01-01",
        "fiscalEnd": "2024-12-31",
        "fiscalQuarter": null,
        "periodKind": "ANNUAL",
        "cumulative": true
      },
      "accounting": {
        "statementFamily": "GENERAL_CORPORATE",
        "consolidation": "STANDALONE",
        "group": null,
        "attribution": "NOT_APPLICABLE",
        "operations": "ALL",
        "auditStatus": "AUDITED",
        "standard": "fixture-standard",
        "standardVersion": "1",
        "policyBasis": "fixture-policy-1",
        "restatementBasis": "original",
        "statementScope": "PRIMARY_STATEMENTS",
        "dimensions": []
      },
      "shareBasis": null,
      "definitionId": "fixture-revenue",
      "sourceConcept": {
        "namespace": "fixture-concepts",
        "code": "Revenue",
        "label": "Synthetic revenue",
        "locator": "/xbrli:xbrl/f:Revenue[1]",
        "contextRef": "ctx-annual",
        "unitRef": "unit-idr",
        "decimals": "0",
        "sourceLexical": "100.00",
        "sourceLiteral": "100.00"
      },
      "filing": {
        "evidenceId": "00000000-0000-4000-8000-000000000001",
        "revisionNumber": 1
      },
      "convention": {
        "evidenceId": "00000000-0000-4000-8000-000000000002",
        "revisionNumber": 1
      },
      "comparativeOf": null,
      "derivation": null
    }
  },
  "payloadSha256": "443d28676c99c033880df457d00db0c22b414ce8b7c205398b4c1832d5314c9c"
}
```

| Example / constructed changes from X01 | Expected result / exact validation rule |
|---|---|
| X01 annual reported | Above complete closure at cutoff 2025-01-05T00:00:00Z yields Revenue 100.00, unknown publication acknowledged; FILING01/FACT01/PIT02 |
| X02 quarter/YTD | Two independent clones: Q1 period 2025-01-01..2025-03-31, FY2025 bounds 2025-01-01..2025-12-31, quarter=1, kind QUARTER/cumulative=false, amount/sourceLiteral/sourceLexical=40; YTD period 2025-01-01..2025-06-30, same fiscal bounds, quarter=2, kind YTD/cumulative=true, amount=90. Give distinct sourceReferences native-q1-2025/native-ytd-q2-2025 and separate filing IDs/coveredPeriods. Receipt/known/recorded all moved to 2025-08-01/02/03 respectively; context metadata matches. | Both admissible separately; no direct quarter=YTD equality; P01/P03. Quarter difference requires D07, never performed here |
| X03 comparative | Clone filing with coveredPeriods FY2023 annual + FY2024 annual (canonical set order); use new filing ID. Current FY2024 fact is known 2025-01-04; comparative clone has FY2023 bounds/dates/label, sourceReference native-comparative-2023, amount=80 with contextRef ctx-comparative-2023, known/recorded=2025-01-05, comparativeOf exact current fact ref, and same new filing ref. | Separate FY2023 fact with 2025 knowledge; presentation link neither revises old 2023 facts nor makes it visible in 2023; FACT01/PIT01/REV01 |
| X04 corrected filing | X01 filing revision 2/new evidence UUID, supersedes=1, role CORRECTION, artifact(11), receipt artifact(11) retrieved 2025-02-01, known/recorded 2025-02-02, lineageEvidence artifact(82), unchanged documentKey. X01 revenue revision 2/new UUID, supersedes=1, amount/sourceLiteral/sourceLexical=80, filing exact rev2, receipt artifact(11), same retrieval, known/recorded 2025-02-03, lineageEvidence artifact(82), same individual sourceReference. | Same filing series, separate fact series. Cutoff Jan 5 returns 100; Feb 2 old fact has inactive filing premise; Feb 4 returns 80. No fallback during readmission interval; REV01/PREM01 |
| X05 same URL replacement | X04 artifacts 10 and 11 have different bytes/hash but fixture original_uri for both = https://fixture.invalid/report. With X04's retained lineage, use those revisions; without lineage, do not construct revision 2: retain distinct local documentKey/reference with role UNKNOWN and separate series. | URI is metadata, absent from key. Changed bytes alone cannot supersede; missing proof on an attempted rev2 rejects REV01. Both originals remain retained |
| X06 several facts/one filing | Clone X01 mapping for OperatingProfit/fixture-operating-profit with its own conventionId; clone fact metric/source concept to OperatingProfit, definitionId accordingly, amount/sourceLiteral/sourceLexical=20, sourceReference native-operating-profit-2024, exact mapping ref. Use X01 filing and receipt. | Revenue and profit have distinct FactKeys/series. A revenue-only lexical metadata rev2 (amount=100.0, original sourceLiteral/sourceLexical=100.00 retained, lineageEvidence artifact(85)) referencing the still-active original filing leaves profit 20; changing the filing head instead requires readmission of both per explicit PREM01 trade-off |
| X07 multi-filing derived lineage | Use X01 revenue and X06 profit, but give profit an independently retained second filing documentKey annual-B-2024, artifact(12), exact receipt/ref, same entity/period. Add FORMULA_REFERENCE below. Derived fact clone: metric Revenue, factKind DERIVED, amount=120, definitionId fixture-lineage-output, sourceConcept/filing/comparativeOf=null, convention exact formula ref; derivation fields below. sourceId fixture-local, tier T4, receipt/publication fields null; known/recorded 2025-01-06, sourceReference local:H({formulaId,formulaVersion,inputs}). | Valid **fixture-only** lineage/number case under the explicitly synthetic formula below; two filings reached through role-bound inputs, no fake single filing. This does not approve any financial Revenue/FCF formula or production result. FACT01/PREM01/N01 |
| X08 missing/zero | Missing case has no fact record. Zero clone changes amount/sourceLiteral/sourceLexical to 0; independent sourceReference native-zero. Inapplicable clone sets applicability INAPPLICABLE, amount=null, inapplicabilityReason fixture concept not applicable, sourceLexical/sourceLiteral=null, retains positive synthetic source proof, same declared unit/period. | UNKNOWN vs explicit 0 vs positively inapplicable, never interchangeable. FACT01/N01; deleting required amount instead of null is S01 malformed |
| X09 conflicting sources | Clone X01 filing/fact to fixture-source-B with artifact(14), source-B receipt run 61, its grant and own native reference; clone mapping appliesToSourceId to B under distinct conventionId/ref. Same economic key/definition/basis, amount/sourceLiteral/sourceLexical=120, sourceReference native-revenue-B-2024. Keep both T1 with valid closure; B knowledge 2025-01-05. | At Jan 6 equal-authority 100/120 is FUNDAMENTAL_CONFLICTING; later B receipt/UUID cannot choose. ID01/PREM01 |
| X10 invalid unit/currency | First mutate X01 unit.currencyCode=null with MONETARY. Second mutate currencyCode=USD while exact map/retained unit definition still IDR. Third mutate SharesOutstanding fact's unit to MONETARY. | Respectively FUNDAMENTAL_MALFORMED B02; FUNDAMENTAL_INTEGRITY_FAILURE/FACT01 unit binding; metric-unit MALFORMED B02. No FX or default currency |
| X11 withdrawn filing | X01 filing rev2/new UUID, CANCEL/FULL/value=null, supersedes=1, preserved key/subject/series, lineageEvidence artifact(13), withdrawal receipt artifact(13) at 2025-02-01, known/recorded 2025-02-02. | Jan 5 fact still reproducible; Feb 3 dependent fact UNKNOWN/PREMISE_INVALID and filing CANCELLED. No predecessor resurrection; REV01/PREM01 |
| X12 issuer/security mismatch | Add ENTITY_SECURITY below, attach exact convention ref to a share-bound fact for class-A. Request instrument-B UUID 00000000-0000-4000-8000-000000000101 (different security/class) or mutate fact subject to reporting-B while filing/mapping remain A. | Security request FUNDAMENTAL_INCOMPARABLE SEC01; inconsistent fact/filing subject FUNDAMENTAL_INTEGRITY_FAILURE FACT01. No name/ticker association |
| X13 parent vs total | Two NetIncome clones with their exact NetIncome mapping, same period/definition but accounting CONSOLIDATED/group group-A; attribution TOTAL vs PARENT_OWNERS. New distinct sourceReferences and keys; each amount/source literal bound to source. Filing reports both scopes, with exact contextRef ctx-total and ctx-parent respectively. | Coexist as distinct economic facts; total cannot feed parent request. DIM01/ID01; label or identical amount cannot merge |
| X14 basic vs diluted | Two EarningsPerShare clones and corresponding exact PER_SHARE/IDR mapping. Both ShareBasis WEIGHTED_AVERAGE/EPS_DENOMINATOR/class-A with ENTITY_SECURITY ref; calculationBasis BASIC vs DILUTED. Separate sourceReference/key, same annual duration; exact contextRef ctx-basic/ctx-diluted, unitRef unit-idr-per-share resolving IDR per share. | Distinct EPS bases. Mutate BASIC countBasis to PERIOD_END with DURATION: MALFORMED SEC01. Period-end outstanding shares cannot stand in for either denominator |
| X15 as-of | X01 fact at cutoff 2025-01-03T23:59:59Z invisible; Jan 4 exact boundary visible. Variant retains earlier receipt/known but adds publishedAt=2025-01-10T00:00:00Z and publicationEvidence artifact(83), publicationDate=null; at Jan 5 invisible. Date-only variant publicationDate=2025-01-01, publishedAt=null, publicationEvidence artifact(84) stays visible only from knownAt. | PIT02 inclusive boundaries; unknown/date-only publication does not invent midnight or public-as-of proof. Late mapping/derivation remains invisible before actual admission; recordedAt alone is not cutoff |

X12/X14 ENTITY_SECURITY convention value (all other envelope fields follow X01
Mapping with distinct conventionId/sourceReference/evidence UUID, SOURCE scope,
fixture-local T2 and actual knownAt no later than the referencing fact):

```json
{
  "conventionId": "fixture-identity-A",
  "version": "1",
  "kind": "ENTITY_SECURITY",
  "support": [
    {
      "artifactId": "00000000-0000-4000-8000-000000000080",
      "sha256": "057ebf9096253171d5fd109380b06b17a316ad6c2fd919c616ca8da44aa05d79",
      "byteLength": 323,
      "locator": "fixture-content"
    }
  ],
  "mapping": null,
  "entitySecurity": {
    "issuer": {
      "namespace": "fixture",
      "code": "issuer-A"
    },
    "legalEntity": {
      "namespace": "fixture",
      "code": "legal-A"
    },
    "reportingEntity": {
      "namespace": "fixture",
      "code": "reporting-A"
    },
    "consolidationGroup": null,
    "instrumentId": "00000000-0000-4000-8000-000000000100",
    "security": {
      "namespace": "fixture",
      "code": "security-A"
    },
    "shareClass": {
      "namespace": "fixture",
      "code": "class-A"
    },
    "effectiveFrom": "2024-01-01",
    "effectiveTo": null,
    "references": [
      {
        "kind": "TICKER",
        "value": "SYNTH-A",
        "to": null,
        "from": "2024-01-01"
      }
    ]
  },
  "formula": null
}
```

X07 formula convention value uses only an isolated arithmetic fixture: left
and right are two exact same-unit numbers, output is their mathematical sum,
with matching entity/annual period/accounting basis and exact N01 result. Left
is the fixture Revenue 100; right is fixture OperatingProfit 20. This deliberately
has **no accounting interpretation**; it exercises roles, multi-filing traversal
and exact 120 only. Fixture artifact(81) is the definition locator and fixture
approval ID; production approval validation must reject that fixture ID. No
financial formula, threshold or source permission is established. D07 remains
unapproved; a real derived fact needs a separately approved meaningful definition.

```json
{
  "conventionId": "fixture-lineage-formula",
  "version": "1",
  "kind": "FORMULA_REFERENCE",
  "support": [
    {
      "artifactId": "00000000-0000-4000-8000-000000000080",
      "sha256": "057ebf9096253171d5fd109380b06b17a316ad6c2fd919c616ca8da44aa05d79",
      "byteLength": 323,
      "locator": "fixture-content"
    }
  ],
  "mapping": null,
  "entitySecurity": null,
  "formula": {
    "formulaId": "fixture-only-add",
    "formulaVersion": "1",
    "definition": {
      "artifactId": "00000000-0000-4000-8000-000000000081",
      "sha256": "144c820c0307235701de026f2e30d78b0f9424986692131c2af81cade1d5f6b3",
      "byteLength": 285,
      "locator": "fixture-content"
    },
    "approvalId": "FIXTURE_ONLY_NOT_ACCOUNTING_APPROVAL",
    "roles": [
      "left",
      "right"
    ],
    "repeatedInputAllowed": false,
    "resultMetric": "Revenue",
    "resultDefinitionId": "fixture-lineage-output",
    "resultUnit": {
      "kind": "MONETARY",
      "currencyCode": "IDR"
    },
    "resultPeriodType": "DURATION"
  }
}
```

X07 Derivation has formulaId fixture-only-add, formulaVersion 1,
calculatedAt 2025-01-06T00:00:00Z, inputs sorted roles left/right, each with the
exact constructed reported fact UUID/revision. Its fixture T4 Admission shares
the explicit eleven-field grammar, uses its own decisionId, LOCAL_CALCULATION
route and acquisitionAllowed=false. X12/X14 ShareBasis has countBasis
WEIGHTED_AVERAGE, calculationBasis BASIC or DILUTED as specified,
quantityBasis EPS_DENOMINATOR, shareClass {namespace:fixture,code:class-A},
securityBinding exact ENTITY_SECURITY Ref. Its effective interval covers the
fact period and requested security date. Entity facts without that relationship
remain entity-level; this example does not define a valuation rule.

Validation boundary: constructor expansion, closed field shapes, generated IDs/
hashes, numeric values, periods, lineage and expected rejection paths are checked
as **documentation examples**. No production decoder, persistence, provider
parser or formula tests are implemented or claimed. The constructed fixture
metadata is explanatory, never persisted to the operational archive/database.

## 24. P2-R4 decision resolution matrix

Technical completion means an exact **proposed** rule exists, not approval or
implementation. Repository facts are the inspected EvidenceDecimal/record/
resolver behavior, frozen V0.2 contracts and SQL predecessor guards (§§2/14/18).
New-family schemas and algorithms are design recommendations. No product owner,
independent reviewer or source has accepted them in this bundle.

| ID | Current status / proposed resolution | Evidence / rationale | Remaining uncertainty | Approval requirement / freeze impact |
|---|---|---|---|---|
| D01 | TECHNICAL_DESIGN_COMPLETED; E01/FILING01/FACT01/CONV01, derived class, ID01/REV01/PREM01 | Existing envelope/canonical writer and distinct source-namespaced series; X01/X04–X07/X11 | Independent review of keys, conservative inactive-premise rule and concrete golden-byte fixtures | Explicit acceptance of exact schemas/algorithms/bounds during separate freeze; no hidden implementation-defined field; not a live-source blocker |
| D02 | TECHNICAL_DESIGN_COMPLETED; existing exact EvidenceDecimal domain, N01, unsupported values retained outside arithmetic | Actual grammar/200-character/BigInteger equality checks; no price-column precision reuse | How often future source literals exceed limits is unknown; no expansion inferred | Approve bounded domain/rejection in freeze; wider arithmetic requires separate version, not default infrastructure |
| D03 | TECHNICAL_DESIGN_COMPLETED; EntityKey + bounded ENTITY_SECURITY, orthogonal ShareBasis/accounting identity | 0001 instrument/issuer_name is not legal registry; X12–X14 | Actual issuer/group/security mappings need permitted evidence; unknown basis blocks use | Approve minimum bindings/unknown handling in freeze; real relationships require separate source admission; downstream valuation still deferred |
| D04 | RECOMMENDED_BUT_UNAPPROVED; statement-only V0.1 as explicitly bounded in §18 | Events cannot honestly serialize as mandatory statement scalar/period; T32 | Owner may require typed events now, which would need a separate exact event schema and new review | Explicit scope approval **required before freeze**; this candidate is ready to review only on that proposed scope. No dividend/dilution event coverage claimed |
| D05 | RECOMMENDED_BUT_UNAPPROVED; canonical FCF DERIVED only; company/provider-reported definitions source-native | Distinct issuer/provider assertions vs our comparable calculation; FACT01/native concept mapping | No equivalence or FCF component definition established | Explicit acceptance of this semantic boundary before freezing the FCF token; broader reported canonical variants need separate definition/version. No formula approved |
| D06 | TECHNICAL_DESIGN_COMPLETED for generic ADM/PIT/RIGHTS; EXTERNAL_PRODUCTION_DEPENDENCY for routes | Actual V0.2 publication visibility and mutable registry; exact retained Admission/current-use gate | Publisher permission, timezone, revisions, retention/revocation and coverage unresolved | Approve generic fail-closed rules in freeze; source-specific admission and lawful acquisition/use separately required; external permission does not block reviewing this source-independent schema |
| D07 | DEFERRED_WITHOUT_BLOCKING_BASE_BINDING; formula-reference/roles/exact-result obligations complete, no production formula | Multi-input lineage cannot be unordered; fixture-only X07 proves no accounting policy | All financial formulas/sign/components/result precision and compatibility bridges remain unapproved | Approve each formula independently before any derived financial admission/build. Reserved DERIVED grammar may freeze as a disabled capability; do not claim usable FCF/TTM/debt math |
| D08 | DEFERRED_WITHOUT_BLOCKING_V0_1; no classifier/valuation policy | §12/T25 require history and separately approved rules; evidence cannot choose overlapping categories | Lookback, overlap, thresholds, revised-history and valuation policy all open | Separate product/investment approval; no evidence-contract freeze blocker if classification/integration stays outside scope |
| D09 | TECHNICAL_DESIGN_COMPLETED for logical immutable storage; DEPLOYMENT_UNAPPROVED | STORE01 and actual 0007–0011 guards; archive/source/receipt reused | Physical DDL/acceptance and target operational state not checked; migration number remains provisional | Approve logical persistence boundaries in freeze; later authorize P2-I2 DDL and separately deployment. Must account for absent operational 0007–0011 before 0012; no silent chain bypass |

Remaining explicit approval decisions: statement-only scope (D04), FCF semantic
boundary (D05), exact wire/identity/numeric/entity/PIT/withdrawal/bounds/current-use
and logical storage contract (D01/D02/D03/D06/D09), including the readmission
availability trade-off. D07 formulas and D08 rules remain disabled/deferred,
not silently approved by schema freeze. Independent freeze review and actual
freeze are separate decisions; neither occurs here. Deployment, source access
and implementation require their own explicit authorization.

## 25. P2-R4C independent freeze-review package

### 25.1 F01–F15 reconciliation

Historical finding text remains in §16. Disposition below means the **candidate**
addresses the technical issue; it does not certify implementation or approve the
proposal. External/editorial uncertainty is not relabelled as resolved.

| Finding | Disposition | Specific current evidence / remaining boundary |
|---|---|---|
| F01 | RESOLVED | ID01 separates filing documentKey from per-fact economic/assertion keys; REV01 prevents key sliding; X04/X06 |
| F02 | RESOLVED | E01 plus three complete schemas and closed shared objects; complete X01 and branch values; S01 rejects undefined fields |
| F03 | RESOLVED | PIT01/PIT02 include receipt/publication/real knowledge and premise clocks; X15 and T17/T18/T35 |
| F04 | REQUIRES_APPROVAL | P01–P04 exact period/coverage constraints; D04 statement-only scope explicitly awaits approval; no invented event period |
| F05 | RESOLVED | Orthogonal ShareBasis and SEC01 distinguish basic/diluted weighted-average vs period-end; X14 |
| F06 | RESOLVED | Accounting/DIM01 preserve parent/total/consolidation/material dimensions and unknown basis; X13 |
| F07 | RESOLVED | ENTITY_SECURITY names legal/issuer/reporting/group/instrument/security/class separately, historical intervals and no valuation assignment; X12 |
| F08 | RESOLVED | FACT01 role-bound exact input revisions, multi-filing closure, inactive-premise rejection; X07/X11. Financial formulas remain D07, not a lineage-schema gap |
| F09 | REQUIRES_APPROVAL | Source-native reported FCF preserved; canonical FCF derived-only boundary remains D05 recommendation, no formula/equivalence approval |
| F10 | RESOLVED | N01 exact existing numeric domain, source scale/decimals/unit separation, overflow rejection; X08/X10, T05/T28 |
| F11 | RESOLVED | §2/E01/STORE01 reuse archive/registry/receipt/writer patterns; frozen market reader/ingester are not generic imports; no second framework |
| F12 | RESOLVED | STORE01 and §14 document sequential 0007–0011 prerequisites before provisional 0012; no DDL/deployment claims |
| F13 | STILL_UNRESOLVED | External permitted route, scope/tier/retention/replay proof absent; ADM01/RIGHTS01 define required behavior, cannot manufacture evidence. Production HOLD_FOR_EVIDENCE |
| F14 | DEFERRED | §12/D08/T25 retain INSUFFICIENT_DATA without separately approved classification/history; no thresholds/priority invented |
| F15 | STILL_UNRESOLVED | §17 OPS editorial scope/count uncertainty retained; no recount/DB audit; nonblocking for exact contract review |

### 25.2 Existing test reconciliation and missing cases

All 35 existing scenarios remain **specifications, not implemented/executed
production tests**. Mapping below distinguishes exact binding coverage from later
formula/parser/rule work. X cases are documentation fixtures, not proof of a
working decoder/store/source. P2-I1/I2 still need approved contract/build scope.

| Scenario | Exact candidate coverage / future acceptance boundary |
|---|---|
| T01 | FACT01/FILING01/MAPPING/ADM01, complete X01; future round-trip/admission |
| T02 | FACT01 Derivation/CONV01/PREM01, X07 fixture-only; financial formula test waits D07 |
| T03 | N01/applicability, X08 missing/zero/inapplicable |
| T04 | N01 preserves negative; accounting parentheses require exact parser mapping |
| T05 | N01 actual coefficient/scale/200-char bounds; exact equality/hash vs overflow/inexact result |
| T06 | Unit/N01/DIM01, X10; no implicit FX |
| T07 | Unit/ShareBasis/SEC01, X10/X14; incompatible bases rejected |
| T08 | Period/P01 instant null start vs duration, no coercion |
| T09 | P01/P02/X02 quarter vs YTD remain distinct |
| T10 | P03 complete compatibility/difference interval; financial formula execution deferred D07 |
| T11 | P04 exact fiscal-quarter coverage/no overlap/gap/duplicate; formula deferred |
| T12 | P01/P02/P04 retain actual fiscal transitions/53-week boundaries; no automatic growth |
| T13 | Accounting/ID01/DIM01 separate standalone/group |
| T14 | Accounting attribution/SEC01, X13; downstream EPS/trajectory policy deferred |
| T15 | MAPPING/nativeConcept/DIM01; unknown bank concept cannot map by label |
| T16 | ID01/DIM01/REV01 preserve audit/taxonomy/policy constraints without automatic supersession |
| T17 | PIT02 publication cutoff, X15 exact future publication |
| T18 | PIT01/PIT02 actual receipt/knownAt, late original invisible earlier |
| T19 | REV01/X04 same-series original/correction plus conservative readmission interval |
| T20 | FACT01 comparativeOf/P01, X03; presentation does not supersede original |
| T21 | FILING01/ID01/REV01, X05 hash/URL distinction and unresolved lineage |
| T22 | ID01/PREM01, X09 equal-authority conflict, no recency choice |
| T23 | E01 Admission/ADM01; ALLOWED flag alone insufficient, no fixture grant promotion |
| T24 | MAPPING/Format/nativeConcept; unresolved concept retained outside canonical admission |
| T25 | §12/D08; no rule/history means INSUFFICIENT_DATA, rule tests deferred |
| T26 | ENTITY_SECURITY/SEC01/ID01, X12/X14; historical symbol not current lookup |
| T27 | STORE01/PIT02 exact retained refs/cutoff/rights; restore proof future disposable acceptance |
| T28 | N01/MAPPING/SourceConcept exact scale and scoped context/unit; decimals not multiplier |
| T29 | Derivation/FORMULA_REFERENCE/PREM01 exact roles and input revisions; no UUID-set math |
| T30 | REV01/PREM01, X11; inactive premise never substituted, earlier replay preserved |
| T31 | ID01/REV01, X06 distinct metrics/periods coexist per filing |
| T32 | §18 explicit proposed event exclusion, D04 approval still required; no fabricated period/scalar |
| T33 | S01/PREM01 byte/depth/array/closure bounds; actual XML XXE/ZIP expansion tests deferred source adapter with explicit budgets |
| T34 | E01 Admission/RIGHTS01/STORE01 retained historical decision vs current lawful-use gate |
| T35 | PIT01/Derivation/Receipt actual late clocks, refetch does not move first knownAt |

Additional future specifications identified during exact-schema review:

| New case | Input / expected assertion |
|---|---|
| T36 Canonical bytes/write identity | Reordered properties canonicalize identically; lexical 1.0/1.00 agree numerically but have different payload hashes; unknown/duplicate/missing nullable properties reject; mismatched stored canonical bytes/hash fail integrity; idempotent recordedAt cannot change |
| T37 Revision chain concurrency/reinstatement | Duplicate series ordinal, branch, gap, cross-identity supersedes, lower-authority cancel and consecutive cancel reject atomically; explicit authorized reinstatement is visible only from its new knownAt |
| T38 Premise correction availability | Filing rev2 without readmitted sibling facts invalidates old governing refs at later cutoff; revenue-only fact rev2 with unchanged filing does not invalidate profit; missing corrected fact never resurrects old number |
| T39 Closure boundaries | 256 unique nodes/16 edges maximum accepted, 257/17 rejected; cycles reject; shared node counted once with both roles retained; unapproved repeated input rejects |
| T40 Publication and rights boundary | Date-only publication remains null instant; exactly-at-cutoff clocks visible; expired/currently revoked/unknown replay rights fail use without rewriting old admission or economic cutoff; unknown zone cannot parse into instant |
| T41 Entity/calendar unknowns | Name/ISIN-syntax collision and changed security class never associate facts; unknown group/attribution/fiscal basis may retain source assertion but cannot satisfy financial comparison; open mapping interval needs positive evidence |
| T42 Numeric domain boundaries | ±maximum coefficient, 1e-28, -0, reducible trailing zeros >28 places and exactly 200 characters accepted when representable; 201 chars, nonzero 1e-29, exponent, oversized coefficient and rounded parse rejected; exact scale normalization once only |
| T43 Unsupported branches | Dilution event and event DividendPerShare reject; statement DividendPerShare requires evidence/class; OTHER-format canonical mapping rejects; source-native concept definition mismatch and fixture-only formula approval reject production admission |
| T44 Clock precision round-trip | knownAt ending .0000001Z remains invisible at the preceding .0000000Z cutoff and visible exactly at its own tick after persistence/restore; a microsecond projection cannot round it into earlier visibility; Receipt never invents precision absent from its retained observation |

These are documentation specifications only. Actual parser-specific budgets,
golden .NET canonical byte fixtures, DB concurrency/restore tests and approved
financial formula cases belong to separately authorized implementation slices.

### 25.3 Readiness and independent Astra handoff

**READY_FOR_FREEZE_REVIEW** of this exact proposed **statement-only** candidate.
P2-R4A, P2-R4B and P2-R4C are COMPLETE as bounded documentation/design tasks.
Reason: three closed schemas, explicit version/serialization/numeric domain,
separate keys, deterministic revision/withdrawal/conflict/premise rules and
PIT/period/entity/storage behavior are now specified and tied to examples/tests.
There is no known internal correctness gap left to implementation discretion.
The scope is a defined recommendation, not an approved reduction. If typed event
coverage is required for V0.1, this readiness does not apply: targeted event design
is needed before freeze. D04/D05 and exact-contract choices must be explicitly
accepted or revised; reviewer/owner may still find defects. No freeze occurs here.
External permissions remain a separate production blocker, not a manufactured
reason to prevent reviewing source-independent schema. F15 remains editorial.

Concise independently reviewable handoff for **Codex Astra / High** (package only;
no agent launched, task/message dispatched or reviewer approval claimed):

1. Review baseline `897af2d` and this proposal's P2-R4 diff. Start at §§18–22
   (final schemas/keys/numeric/period/PIT/store), then §23 (15 example recipes),
   §24 (D01–D09) and §§25.1–25.2 (F01–F15 and T01–T44).
2. Changes: eliminate redundant class storage; close all three typed bindings;
   reuse exact EvidenceDecimal limits; separate document/fact keys; add bounded
   identity/security mapping, role-bound exact lineage, withdrawal/readmission
   and scoped admission/current-use rules; define statement-only scope.
3. Corroborate repository facts in EvidenceDecimal, ScreenerEvidenceJson and
   ScreenerEvidenceRevisionSeries, ScreenerEvidenceValidity.Visible/resolver,
   raw archive/BoundedEvidenceIngestionService, frozen V0.2 contract §3/§5/§13
   and persisted binding §3/§5, migrations 0001/0002 and predecessor guards
   0007–0011. Do not assume V0.2 reader/validity can consume this family unchanged.
4. Challenge remaining correctness risks: source-reference/key collision and
   cross-filing lineage; exact numeric/source-scale failure cases; unknown material
   dimensions vs comparable equality; premise correction/readmission availability;
   current replay-use authorization; bounds/concurrency/restore; serializer bytes.
   Synthetic fixture-only formula is not valid production accounting approval.
5. Explicit approvals: D04 scope, D05 FCF boundary, D01/D02/D03/D06/D09 exact
   contract and trade-offs. D07 formulas and D08 classifications/valuation remain
   deferred; D09 deployment and live source admission unapproved. No financial
   thresholds or formulas may be inferred from the fixture examples.
6. Production blockers: no authorized unattended filing route, source-scoped
   rights/authority/retention/replay/publication/taxonomy samples, no source registry
   activation; operational migration absence is a carried-forward OPS-1 report.
   Level-1 BLOCKED/Options A+B and Phase-2 HOLD_FOR_EVIDENCE remain unchanged.
7. Recommended disposition: independently review the candidate, then request
   explicit acceptance or targeted changes. Keep PROPOSAL — NOT FROZEN until
   separately authorized freeze. Do not repeat broad provider research, acquire
   data, deploy, implement or contact anyone as part of this review.

### 25.4 Validation and execution boundary

Only this existing proposal changes. No competing proposal/handoff document,
production code, implemented tests, migrations, registry activation, operational
DB access/mutation, archive recount, Docker restart, credential access or provider
acquisition. Network/provider requests/billable units/paid cost: **0/0/0/0**.
Validation is full documentation-diff/content review, references/anchors,
finding/decision/scenario enumeration, synthetic example construction/manual
semantic checks, frozen-contract/migration/source-file immutability and
git diff --check. Runtime suites are skipped for documentation-only scope.
Final commit/status observations belong in the task report, not assumed here.
