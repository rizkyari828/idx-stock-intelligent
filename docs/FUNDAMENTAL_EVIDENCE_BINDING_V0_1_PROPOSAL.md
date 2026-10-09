# Fundamental Evidence Binding V0.1

Status: **PROPOSAL — NOT FROZEN**

Policy identity proposed: `fundamental-evidence-v0.1.0` (new, additive).
Date: 2026-10-09 WIB. Baseline: `main` at `b8cea49`, clean worktree.
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
| `src/IdxStockIntelligence.Application/ScreenerEvidenceV02.cs` (`EvidenceFact<T>`, `EvidenceQuality`, `SourceAuthorityTier`, `EvidenceChronology`, `EffectiveInterval`) | Evidence value/quality/authority/chronology separation; quality is reconstructed, never persisted as timeless truth. |
| `src/IdxStockIntelligence.Infrastructure/BoundedEvidenceIngestionService.cs` and `BoundedEvidenceIngestion.cs` | Bounded artifact size, bounded row counts, explicit failure reasons, no truncation, no silent provider fallback; a future fundamental adapter would be a trusted adapter in the same shape. |
| `src/IdxStockIntelligence.Infrastructure/ScreenerEvidenceV02AsOfReader.cs` | Deterministic as-of selection by cutoff; revision-series resolution; explicit retained-input diagnostics. |
| `source` registry (`0001`) | Provider-neutral source identity and `terms_status ∈ {UNKNOWN, ALLOWED, DISALLOWED}` admission gate; no source may become authoritative without explicit review. |
| Screener V0.2 conflict precedence (`SCREENER_EVIDENCE_V0_2_CONTRACT.md` §13) | Deterministic ordering by scope, cutoff visibility, authority tier, specificity, correction chronology, revision identity, then CONFLICTING. The fundamental reader should apply the same ordering logic to fundamental facts. |
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

## 3. Claim vocabulary

Three envelope-level claim tokens are proposed (not frozen). Each stored record has
one token; typed metric identity lives in the payload.

| Proposed claim token | Meaning | Typical class |
|---|---|---|
| `FinancialFiling` | A retained filing/report identity and its publication chronology (document identity, reporting entity, statement family, taxonomy/version, period(s) it covers, original/amended status). Holds no financial values. | `FILING_RECORD` (new class token, §3) |
| `FundamentalFact` | One reported or locally derived value for one metric, one period, one statement scope and one filing lineage. | `REPORTED_FILING_FACT` or `DERIVED_FILING_CALCULATION` (new class tokens) |
| `FundamentalConvention` | Reviewed mapping/convention facts: source concept → canonical metric, statement family, unit/scale semantics and parser/mapping version. | `SOURCE_CONVENTION` semantics in the new namespace |

Metric vocabulary (typed `metric` token; required minimum set from the Phase-2
decision):

| Metric | Default fact kind | Period type | Notes |
|---|---|---|---|
| `Revenue` | REPORTED | DURATION | Not universal; see §6. |
| `OperatingProfit` | REPORTED | DURATION | Not universal; bank statements may not present it. |
| `NetIncome` | REPORTED | DURATION | Applies across statement families. |
| `EarningsPerShare` | REPORTED | DURATION | Requires a share basis (basic/diluted; §4). |
| `OperatingCashFlow` | REPORTED | DURATION | Cash-flow statement. |
| `CapitalExpenditure` | REPORTED | DURATION | Capital expenditure sign convention must be explicit; no assumption. |
| `FreeCashFlow` | **DERIVED only** | DURATION | Never an authoritative reported fact; requires formula identity and exact input facts. |
| `TotalAssets` | REPORTED | INSTANT | Balance sheet. |
| `TotalLiabilities` | REPORTED | INSTANT | Balance sheet; may itself be derived by the source. |
| `TotalEquity` | REPORTED | INSTANT | Statement scope must distinguish owners' equity from total equity where the source does. |
| `CashAndCashEquivalents` | REPORTED | INSTANT | Exact source concept retained. |
| `ShortTermDebt` | REPORTED | INSTANT | Source definition retained; no local maturity heuristics. |
| `LongTermDebt` | REPORTED | INSTANT | Same. |
| `TotalDebt` | REPORTED or DERIVED | INSTANT | If derived, `derivation` is required; the basis (reported total vs sum of components) is explicit. |
| `SharesOutstanding` | REPORTED or DERIVED | INSTANT or DURATION | Share basis required (§4); weighted-average vs period-end must be explicit. |
| `DilutionObservation` | REPORTED | INSTANT/EVENT | Potential dilution facts (instruments, potential shares, terms reference); no dilution percentage is computed in V0.1. |
| `CashDividend` | REPORTED | EVENT/DURATION | Declared/paid dividend facts; governing notice identity retained. |
| `DividendPerShare` | REPORTED or DERIVED | EVENT/DURATION | Per-share amount with currency and share basis; total/per-share reconciliation is not assumed. |
| `OtherReportedConcept` | REPORTED | as reported | Source-native concept preservation for industry-specific statements (for example bank interest income) until a reviewed convention maps it; required `sourceMetricLabel`. |

Required distinctions:

- **Directly reported fact:** `factKind = REPORTED`, an identified source concept
  value as asserted by the filing; authority is that of the filing.
- **Derived calculation:** `factKind = DERIVED`, `SourceAuthorityTier.T4Derivation`,
  formula identity and exact input facts required; FCF is always derived.
- **Missing fact:** no admitted assertion exists; the reader returns UNKNOWN or
  INAPPLICABLE as appropriate; absence is never materialized as a stored zero or
  empty value.
- **Inapplicable fact:** a positive, source-grounded assertion that the concept is
  not defined/applicable for this statement family, period or scope
  (`applicability = INAPPLICABLE` with required reason text). INAPPLICABLE is not
  missing and is never zero.

Existing class tokens are not renamed or reinterpreted. The proposal adds new
fundamental-namespace class tokens (`FILING_RECORD`, `REPORTED_FILING_FACT`,
`DERIVED_FILING_CALCULATION`) because V0.2's five classes describe the temporal
behavior of market/control facts and do not describe filing-asserted financial
statements. Whether a new token is justified versus reusing `POINT_OBSERVATION`
is explicitly listed as an open question (§14).

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
| `metric` | yes | One token from §3; `OtherReportedConcept` requires `sourceMetricLabel`. |
| `factKind` | yes | `REPORTED` or `DERIVED`; `DERIVED` requires `derivation`. |
| `applicability` | yes | `APPLICABLE` or `INAPPLICABLE`; INAPPLICABLE requires `inapplicabilityReason` text and no numeric value. |
| `amount` | ASSERT + APPLICABLE | Exact canonical decimal string; the reported (or derived) value in the declared unit. Never a float, never rounded, never a sentinel. |
| `unit` | ASSERT + APPLICABLE | `{kind: MONETARY / SHARES / PER_SHARE, currencyCode?: 3-letter, ...}`; monetary and per-share require `currencyCode`. |
| `shareBasis` | metric-dependent | Required for `EarningsPerShare`, `SharesOutstanding`, `DividendPerShare` and dilution facts: `BASIC / DILUTED / WEIGHTED_AVERAGE / PERIOD_END / OTHER`; `OTHER` requires an explicit label. |
| `period` | yes | The period binding object from §5. |
| `consolidation` | yes | `CONSOLIDATED / STANDALONE / UNKNOWN`; never inferred (§6). |
| `auditStatus` | yes | `AUDITED / REVIEWED / UNAUDITED / UNKNOWN`; never inferred (§6). |
| `statementScope` | yes | `PRIMARY_STATEMENTS / NOTES_TO_FINANCIALS / SUPPLEMENTARY`; canonical metrics require `PRIMARY_STATEMENTS` unless a reviewed convention says otherwise. |
| `reportingEntity` | yes | Stable reporting-entity identity text retained from the source; issuer-to-entity binding is an open question (§14). |
| `taxonomy` | yes | `{family: opaque text, version: opaque text}`; the accounting taxonomy actually used. |
| `xbrl` | no | Source metadata: `{taxonomy, concept, unitRef, contextRef, decimals?, scale?}` as opaque retained metadata. |
| `filingEvidenceId` | yes | Exact retained `FinancialFiling` evidence UUID; the fact is not decodable without it. |
| `derivation` | for DERIVED | `{formulaId: opaque canonical local id, inputEvidenceIds: sorted unique exact retained FundamentalFact UUIDs}`; `knownAt` of the derived fact must be ≥ the latest input `knownAt`, and all inputs must satisfy the same cutoff and premise-visibility rule. |
| `comparativeOfEvidenceId` | no | Exact retained UUID of the corresponding current-period fact when this fact is a comparative reported in the same filing; a link, never a replacement. |
| `eventDates` | for event-like facts | Optional `{declaredDate?, effectiveDate?, paymentDate?}` factual dates from the governing notice; period scope remains the statement period. |
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
  question (§14).
- `FreeCashFlow` never carries `factKind = REPORTED`. `TotalDebt` and
  `SharesOutstanding` state whether they were reported directly or derived.
- No zero substitution, no interpolated or carried-forward values, no "latest
  known ratio" materialization.

## 5. Period model

`period` object (proposed):

| Field | Required | Semantics |
|---|---|---|
| `periodType` | yes | `INSTANT` (balance-sheet point) or `DURATION` (flow period). |
| `startDate` | DURATION | Inclusive fiscal period start. |
| `endDate` | yes | Instant date or inclusive duration end. |
| `fiscalYear` | yes | Source-declared fiscal year of the period. |
| `fiscalQuarter` | no | 1–4 when the source declares one; absent for annual/other periods. |
| `periodKind` | yes | `FY / QUARTER / HALF / NINE_MONTHS / YTD / OTHER`; `OTHER` requires a label. |
| `cumulative` | yes | `true` for cumulative year-to-date values, `false` for standalone-period values; `INSTANT` is never cumulative. |
| `ttm` | DERIVED only | TTM facts are derived; they must list their exact component periods/facts in `derivation`. |

Rules to prevent incompatible comparisons:

1. The period key is `(periodType, startDate, endDate, cumulative)`. Readers must
   not compare facts across different period keys as if they were the same period.
2. Quarterly standalone profit must never be silently compared with cumulative
   YTD profit; `cumulative` and `periodKind` make the difference explicit.
3. YTD/quarter differencing, aggregation or annualization is a DERIVED operation
   with an explicit formula identity and exact input facts; it is not a reader
   convenience.
4. TTM facts identify their underlying source periods/facts and are never presented
   as reported facts.
5. Comparative-period facts reported inside a filing are ordinary facts for their
   own periods with their own filing binding; `comparativeOfEvidenceId` records the
   reconciliation link without overwriting the original period facts.
6. Periods with different fiscal calendars, currencies, statement scopes,
   consolidation or reporting entities are not comparable without an explicit
   reviewed convention.
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
- Reporting entity, consolidation, statement scope, taxonomy and audit status are
  part of the fact's identity for comparison; differing scopes are not
  disagreements and must not be resolved as conflicting values.

## 7. Currency, unit and scale

- Every applicable fact declares its numeric unit:
  `MONETARY|SHARES|PER_SHARE` (the metric vocabulary has no ratio metric in V0.1).
- `MONETARY` and `PER_SHARE` require a three-uppercase-letter `currencyCode`.
  A fact whose currency is unknown fails the currency requirement and is retained
  as an unbindable/unadmitted candidate rather than assumed to be IDR.
- Share counts never carry a currency. Per-share facts do. The share denominator
  and its basis are explicit (`shareBasis`, §4).
- `xbrl.unitRef` is retained exactly as the source reports it and is the
  authoritative binding to the source unit definition; canonical `unit.kind` and
  `currencyCode` must agree with the reviewed convention for that `unitRef`.
  No currency is ever inferred from magnitude, symbol, locale or a provider
  default.
- `decimals` is precision/accuracy metadata, not a multiplier. `scale` is source
  display/transformation metadata; neither may be applied implicitly. Any
  transformation must live in a retained parser/mapping version and be
  reproducible from the immutable original.
- Monetary amounts are exact decimal values in the reported currency. No foreign
  exchange conversion, no purchasing-power or inflation adjustment, and no unit
  rescaling is defined in V0.1. Cross-currency comparisons require a future
  reviewed convention and explicit conversion evidence.
- Source precision is preserved. The exact reported numeric value, including its
  lexical scale, is retained; arithmetic later uses exact decimal semantics only.

## 8. Publication and PIT chronology

Clocks remain separate and are never conflated:

| Clock | Meaning | Rule |
|---|---|---|
| Period start/end | Economic/fiscal period the fact describes | From the source context; not a knowledge clock. |
| Effective/applicability date | Event or applicability date where relevant (for example a dividend payment date) | Factual source date, not knowledge. |
| `publishedAt` | Original publication/release time of the filing or notice | Retained when the source provides it; never required to be present, never invented. |
| `retrievedAt` | Local retrieval time of the exact retained bytes | `retrievedAt` is the earliest possible admission boundary. |
| `knownAt` | Local admissible knowledge time | `knownAt >= retrievedAt`; a fact is never knowable before its own knowledge boundary. |
| `recordedAt` | Local persistence/execution time | Recording is not a database commit timestamp and is not domain knowledge. |
| Correction `knownAt` | Knowledge boundary of an amendment/restatement | Prospectively applies only from that boundary. |

- A filing retrieved today cannot become known to the system at its historical
  financial period end. `publishedAt` may be older than `knownAt` (the filing
  existed before the system knew it), but historical decisions must not see it
  before `knownAt`.
- A derived fact's `knownAt` must be at least the maximum input `knownAt`.
- Every referenced premise (filing, input facts, conventions) must have been
  legitimately known at the referencing record's `knownAt` as well as visible at
  the query cutoff, reusing the V0.2 premise-visibility rule.
- Deterministic historical as-of reconstruction: readers select records with
  `knownAt <= cutoff` (and retrieval visibility within cutoff where retained),
  resolve the applicable revision series, apply cancellations, then reconstruct
  typed values for the exact period/scope requested. No "latest row wins" and no
  current-state fallback.
- Later restatements never rewrite what the system previously knew. Replay uses
  the same retained original inputs and the same cutoff, and must be
  deterministic across restores.

## 9. Revision and restatement handling

- Originals are immutable. A correction, amendment, replacement or restatement
  appends a new record; nothing is updated in place.
- Revision series identity reuses the V0.2 namespace pattern
  (`canonical(sourceId, sourceReference)`), where `sourceReference` is the
  retained filing/document or notice identity. Records of the same filing lineage
  carry `revisionNumber`/`supersedesRevisionNumber`; the series is never inferred
  from a URL string match alone.
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
  when filing identity is established, with `knownAt` at the replacement
  retrieval. The original bytes remain retained and referenced.
- **Changed structured facts:** value/period/unit changes are new revisions;
  differences in lexical scale alone are not factual changes but are still
  retained as new content if the bytes changed.
- **Missing revision lineage:** if the original/revised relationship cannot be
  established from retained evidence, the record must be marked with an explicit
  lineage status (`COMPLETE|INCOMPLETE`; proposed field on the filing fact) and
  the history is treated as incomplete. Missing lineage is never repaired by
  guessing equivalence from dates, matching values or URL patterns.
- **Source disagreements:** both retained; deterministic precedence (scope, cutoff,
  authority, specificity, chronology, revision) applies as in V0.2 §13; otherwise
  the affected fact is `CONFLICTING`. Source authority never overrides chronology.
- No adjustment factor, restated EPS recomputation or share-adjustment
  normalization is defined in V0.1; those are separate reviewed derivations.

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
- **Comparability conditions:** identical period keys; explicit cumulative vs
  standalone semantics; same consolidation, statement scope, audit status,
  reporting entity and currency; explicit handling of restatements; no silent
  mixing of annual/quarterly/YTD/TTM facts.
- **Industry differences:** bank/non-bank statement families may not supply the
  same concepts; a classifier must state its applicability and return
  INAPPLICABLE/INSUFFICIENT_DATA rather than forcing generic metrics onto banks.
- **Missing design decisions:** the loss/profit definition and sign convention,
  period count and windowing, turnaround vs volatility distinction, treatment of
  near-zero values, restated-history policy, and threshold/rule values. None are
  invented here.
- **Future validation requirements:** deterministic property tests for each state,
  boundary and missingness case; as-of replay tests; restatement tests; and
  walk-forward evaluation before any production promotion, consistent with
  `.ai/RULES.md`.
- **Current correct behavior:** where the required facts/rules do not exist,
  `INSUFFICIENT_DATA`. No score, confidence value or threshold is produced.

## 13. Open questions

Explicit unresolved design questions (each requires separate review):

1. Separate `fundamental_evidence_record` table versus another additive
   persistence shape; confirm during contract review.
2. Are new class tokens (`FILING_RECORD`, `REPORTED_FILING_FACT`,
   `DERIVED_FILING_CALCULATION`) justified, or should filing facts map to an
   existing class with an explicit convention?
3. Reporting entity identity model: how an issuer legal entity binds to a stable
   instrument ID without guessing from names; is an issuer registry needed?
4. Canonical decimal type and behavior for values beyond exact decimal
   representability; whether unrepresentable facts remain retained observations.
5. XBRL context/dimension handling: consolidated vs standalone detection,
   dimensional/segment facts, period aspects, and duplicate-fact reconciliation.
6. XBRL `unitRef` → canonical unit/currency mapping convention ownership and
   versioning; mixed-currency filers.
7. Share basis semantics per source (weighted average, period end, issued, fully
   diluted) and how share-count changes are represented without adjustment.
8. Dividend model details: total vs per-share reconciliation, declared vs paid
   dates, interim/final kinds, withholding, and governing notice identity.
9. Dilution observation scope: which instruments/terms are retained and whether a
   potential-dilution percentage is ever deterministically defined.
10. `TotalDebt`, `TotalLiabilities` and `TotalEquity` reported-vs-derived basis
    conventions and component reconciliation.
11. Restatement lineage when a replacement filing has a different document
    identity, URL or filing index entry; how lineage incompleteness is recorded.
12. Comparative restatement linkage and whether `comparativeOfEvidenceId` is
    sufficient for reconciliation.
13. Filing identity and publication-time semantics per authorized source route
    (timezone, amended-filing publication, index first-seen meaning).
14. Retention/permission scope for original filings and derived facts; which
    source is admissible at T1/T2 for which claim scope.
15. Whether canonical metrics need industry-specific addenda (for example bank
    interest income) in V0.1 or a later additive version.
16. Mapping of the future INVEST thesis-review boundary and valuation inputs; not
    defined here.
17. Minimum evidence for `FinancialFiling` itself (what makes a filing identity
    admissible), and how it relates to `raw_artifact`/`ingestion_run`.
18. How `FundamentalConvention` versions and corrections are reviewed and frozen.

## 14. Proposed implementation boundaries

Nothing in this document is implemented by this milestone. If this proposal is
reviewed and later authorized for freezing, the following **future** work would be
required, in order:

1. Contract review and explicit freeze decision for a `fundamental-evidence-v0.1.0`
   contract (separate authorization; this proposal is not that contract).
2. An additive migration (proposed name `0012_fundamental_evidence_v01.sql`, not
   created here) with immutable append-only storage, typed binding fields,
   revision series and write/read constraints, following the existing migration
   conventions. No frozen migration is edited.
3. Application types and a strict version-1 codec/reader with the failure taxonomy
   and PIT/revision resolution described above; no fallback decoders.
4. A trusted bounded adapter/framework entry point only after an authorized,
   permitted acquisition route and parser mapping exist; XBRL preferred, validated
   XLSX fallback, Stockbit non-authoritative.
5. Focused validation on owned disposable PostgreSQL storage; no operational
   import, no provider calls, no registry activation without separate
   authorization.

Explicitly deferred: acquisition/discovery, parser implementation, taxonomy
mapping data, profitability classifier, valuation rules, UI/API exposure, Daily
Runner integration, and any schema or registry deployment.

## 15. Future acceptance requirements

If the proposal is later frozen and implemented, acceptance would require at
minimum:

- Strict codec tests for every claim/metric, including duplicate/unknown
  properties, numeric grammar, decimal scale, hash mismatch, malformed binding,
  unsupported versions, cancellation and missing premises.
- Exactness tests: no float conversion, no zero substitution, no rounding, no
  silent rescaling, and explicit failure for unrepresentable values.
- Period tests: instant vs duration, annual/quarterly/YTD/cumulative, comparative
  linkage, TTM component identification, and rejection of incompatible period
  comparison.
- Statement-scope tests: consolidated vs standalone, audited vs unaudited,
  reporting entity, statement scope and taxonomy family; inapplicable metrics
  distinct from missing metrics.
- Industry tests: bank concepts preserved without forcing generic revenue/
  operating profit; unmapped taxonomy concepts retained as source-native facts.
- PIT tests: `knownAt >= retrievedAt`, no backdating, premise visibility at the
  referencing `knownAt`, deterministic replay at historical cutoffs, and
  restatement/correction behavior that preserves earlier knowledge.
- Revision tests: original/amended/restated lineage, same-URL replacement,
  missing-lineage marking, conflict retention without latest-row selection.
- Integration tests against the existing immutable archive/revision architecture
  in owned disposable storage; V0.1/V0.2 regression discovery unchanged.
- Explicit confirmation that frozen V0.2 contracts, operational evidence and the
  Level-1 blocker state are unchanged.

Until those conditions are separately authorized and satisfied, the correct
operational behavior is: no acquisition, no ingestion, no classification, and
`INSUFFICIENT_DATA` wherever fundamental evidence would be required.
