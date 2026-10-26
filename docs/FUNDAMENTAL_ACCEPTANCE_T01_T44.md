# Fundamental T01–T44 implementation acceptance table

**Scope.** Maps every scenario in
[the frozen contract](FUNDAMENTAL_EVIDENCE_BINDING_V0_1_CONTRACT.md) §15 (T01–T35) and
§25.2 (T36–T44) to an implemented test or to an explicit gap. T01–T44 remain
*specifications*: a mapping is not a claim that the contract as a whole is satisfied, and
this table does not reinterpret any expected behaviour.

**Method.** The four fundamental test classes were read in full, then run:

```
dotnet test --filter "FullyQualifiedName~Fundamental"
  total: 50  failed: 0  succeeded: 50  skipped: 0
```

States are assigned by reading assertions, not by matching method names — several names
are looser than the scenario they cite, and those are called out.

| State | Meaning |
|---|---|
| IMPLEMENTED | A test asserts the scenario's stated behaviour at the unit/fixture level |
| PARTIAL | A test asserts part of it; the specific missing element is named |
| GAP | No test asserts it; the blocking dependency is named |

"Fixture level" matters: E1–E5 are pure fixture layers with **no parser, no persistence and
no source admission**. A `PARTIAL` here is not a defect report against frozen semantics;
it is the honest boundary of what is currently proven.

## T01–T35 (§15)

| ID | Scenario | State | Evidence / exact gap |
|---|---|---|---|
| T01 | Valid reported fact | IMPLEMENTED | `FundamentalEvidenceBindingTests.T01ValidFilingBindsEnvelopeAndValue`, `.T01ValidFactAndConventionDecode` |
| T02 | Valid derived fact (additive formula, 100 + 20 → 120) | GAP | No arithmetic/role-bound derivation test. Derived *payload* admission exists (T24/T43), the formula does not. Blocked on D07 (backlog A7) |
| T03 | Missing versus zero | IMPLEMENTED | `.T03ZeroNegativeAndSignTransformAreRetainedExactly` — `-0.00` → `0`, not a fill |
| T04 | Negative values | PARTIAL | Same test proves exact negation (`-12.5` NEGATE → `12.5`). A reported `-25` under IDENTITY, and the hyphen/missing-versus-negative distinction, are not asserted |
| T05 | Decimal precision | IMPLEMENTED | `.T05ScaleOutsideTheDeclaredRangeIsMalformed`, `T42NonZeroBeyondTwentyEightPlacesIsRejected`, `T36EquivalentDecimalsKeepDistinctPayloadHashes` |
| T06 | Currency mismatch | PARTIAL | `.T06ApplicabilityRejectsIncompatibleUnitAndMetric` blocks unit/metric mismatches. An IDR-versus-USD pair with equal metric is not asserted (no currency-pair fixture) |
| T07 | Unit mismatch (shares versus currency, share class) | PARTIAL | `.T07PeriodKindMustMatchTheMetricExpectation` and `T43FactKindGuards…` cover kind/applicability. Wrong-share-class / count-basis rejection is not asserted |
| T08 | Instant versus duration | IMPLEMENTED | `.T08InstantAndDurationAreNeverComparable` (also rejects `ANNUAL` with `cumulative=true`) |
| T09 | Quarter versus YTD | IMPLEMENTED | `.T09QuarterAndYtdRemainDistinct` |
| T10 | YTD quarter derivation (90 − 40 = 50) | GAP | Same blocker as T02: no additive difference-period derivation. Blocked on D07 |
| T11 | TTM lineage | PARTIAL | `.T11CoverageRejectsDuplicatesAndOverlaps` now rejects duplicate, overlap **and gap**, and accepts contiguous quarters (gap detection added 2026-10-26). Four-quarter TTM composition and "retain exact components" are still not asserted |
| T12 | Fiscal-year change, short/long transition, 53-week | IMPLEMENTED | `.T12FiscalTransitionsKeepExplicitDatesAndAreNeverAnnualised`: 371-day year admitted with its own dates, never annualised, never comparable to a 52-week year; inconsistent declared span rejected; `fiscalQuarter=4` keeps a non-December end |
| T13 | Consolidation | IMPLEMENTED | `.T13ConsolidatedAndStandaloneScopesStaySeparate` |
| T14 | Profit attribution | IMPLEMENTED | `.T14AttributionAndDimensionsMustAgree` |
| T15 | Industry concept (bank interest income) | IMPLEMENTED | `.T15NativeIndustryConceptIsNotBridgedToIndustrialRevenue`: a bank `InterestIncome` assertion is never comparable with industrial `Revenue`, and its native metric/definition are preserved |
| T16 | Audit/policy/taxonomy reissue | GAP | `AUDITED` appears only as a scope string; no audit-supersession, policy-change or taxonomy-change test. Backlog A5 |
| T17 | Publication cutoff | PARTIAL | `.T17AndT18VisibilityRespectsEveryClock` varies **knownAt** only. A known-before-cutoff assertion whose *publishedAt* is after the cutoff is not asserted |
| T18 | Late ingestion | IMPLEMENTED | Same test: `knownAt` after cutoff is invisible and does not backdate |
| T19 | Restatement 100 → 80 | PARTIAL | `.T19AndT37SeriesOrdinalsAndCancellationAreEnforced` proves ordinal/cancellation validity. Value selection of 100 at an earlier cutoff and 80 at a later one is not asserted (resolver returns identity, not values) |
| T20 | Comparative revision | GAP | No prior-period comparative test. The similarly named `T20VisibleCancelDoesNotFallBackToAnOlderRevision` tests cancellation, not comparatives |
| T21 | Same-URL replacement | GAP | No URL/artifact identity concept at this layer. `T21ReinstatementIsVisibleOnlyFromItsOwnKnownAt` does **not** test T21; it tests reinstatement visibility |
| T22 | Conflicting sources | IMPLEMENTED | `.T22EqualAuthorityConflictIsReportedNotResolvedByRecency` |
| T23 | Missing permission | IMPLEMENTED | `.T23AdmissionMustCarryTheClaimTierAndReviewWindow` |
| T24 | Unknown concept | IMPLEMENTED | `.T24SeriesIdentityMustBeDerivedExactly` (exact series derivation, unsupported branch rejected) |
| T25 | Insufficient profitability history | GAP | No trajectory/history-sufficiency test. Blocked on D08 (backlog A8) |
| T26 | Issuer/security identity (classes, ticker change, name collision) | PARTIAL | `.T26IdentityRequiresEveryScopeComponent` requires period/unit/accounting completeness. Share-class, ticker-change and name-collision cases are not asserted |
| T27 | Historical reproducibility (restore + repeat cutoff) | GAP | Requires persistence; none exists (backlog A1/A9). Resolver determinism is asserted, restore round-trip is not |
| T28 | Source scale/precision (`1.25` in millions, `decimals=-3`) | IMPLEMENTED | `.T28PowerOfTenScalingIsExact` |
| T29 | Operand roles (swapped minuend/subtrahend) | GAP | No role-bound operand test. `T29AndT30…` implements closure bounds, not operand roles or formula versions |
| T30 | Canceled premise | PARTIAL | `.T29AndT30PremiseClosureIsBoundedAndAcyclic` rejects a missing premise; the withdrawn-filing-after-dependent-fact case with preserved earlier replay is not asserted |
| T31 | Several facts per filing | GAP | No multi-fact-single-filing fixture; distinct series/keys per fact are not asserted |
| T32 | Event facts | IMPLEMENTED | `.T32StatementOnlyScopeRejectsDilutionAndUnsupportedKinds` |
| T33 | Bounds/security (JSON, cycles, archive paths, XXE, ZIP) | PARTIAL | JSON property and cycle/bound rejection is asserted (T36/T39). Archive-path, XML-entity and ZIP-expansion budgets are parser work, not implemented (backlog A6) |
| T34 | Admission history | PARTIAL | `.T23…` validates the admission snapshot's fields and window. Registry-status change after a retained admission, and replay rights, are not asserted |
| T35 | Late derivation/convention | PARTIAL | `.T35CorrectionAfterTheCutoffStaysInvisible` yields `PREMISE_INVALID`. The "derived absent before its real knownAt" and "identical refetch does not move knowledge" parts are not asserted |

## T36–T44 (§25.2)

| ID | Scenario | State | Evidence / exact gap |
|---|---|---|---|
| T36 | Canonical bytes/write identity | IMPLEMENTED | `FundamentalCanonicalTests`: `.T36PropertyOrderDoesNotChangeCanonicalBytes`, `.T36ArrayOrderIsPreserved`, `.T36EquivalentDecimalsKeepDistinctPayloadHashes`, `.T36DeclaredHashMustMatchCanonicalPayload`, `.T36EnvelopeRejectsAMismatchedPayloadHash`; plus `FundamentalEvidenceBindingTests.T36UnknownDuplicateAndMissingPropertiesAreRejected`, `.T36NumericStringsAreStrictAndNegativeZeroIsAdmitted` |
| T37 | Revision chain concurrency/reinstatement | PARTIAL | `.T19AndT37…` rejects a gap, cancel-as-revision-1 and cancel-without-lineage. Duplicate ordinal, branch, cross-identity supersedes and lower-authority cancel are not asserted; no transactional/concurrency proof (would need persistence) |
| T38 | Premise correction availability | GAP | No sibling-fact readmission test (filing rev2 invalidating old refs while a revenue-only rev2 does not invalidate profit) |
| T39 | Closure boundaries (256 nodes / 16 edges) | PARTIAL | Cycle, missing node and the 17-edge rejection are asserted. The 256-node bound, shared-node-counted-once role retention and unapproved repeated input are not |
| T40 | Publication and rights boundary | GAP | Date-only publication, expired/revoked replay rights and unknown-zone parsing are not asserted |
| T41 | Entity/calendar unknowns | PARTIAL | `.T41UnknownScopeBlocksComparisonInsteadOfGuessing` blocks UNKNOWN accounting. Name/ISIN-syntax collision and changed security class are not asserted |
| T42 | Numeric domain boundaries | IMPLEMENTED | `.T42MaximumCoefficientIsAcceptedAndBeyondIsRejected`, `.T42NonZeroBeyondTwentyEightPlacesIsRejected`, `.T42DomainCheckAgreesWithEvidenceDecimal` |
| T43 | Unsupported branches | IMPLEMENTED | `T43ConventionNeedsExactlyOneMatchingBranch`, `.T43ApplicabilityAndDerivationBranchesAreExclusive`, `.T43RecordAndArrayBoundsRejectBeforeAdmission`, `.T43FactKindGuardsStaySeparationOfConcerns` |
| T44 | Clock precision round-trip | PARTIAL | `.T44InstantLiteralSpellingSurvivesCanonicalisation` preserves the literal. Visibility at the exact 7-digit tick after **persistence/restore** is not provable without storage (backlog A1/A9) |

## Tally

| State | Count | IDs |
|---|---:|---|
| IMPLEMENTED | 18 | T01, T03, T05, T08, T09, T12, T13, T14, T15, T18, T22, T23, T24, T28, T32, T36, T42, T43 |
| PARTIAL | 15 | T04, T06, T07, T11, T17, T19, T26, T30, T33, T34, T35, T37, T39, T41, T44 |
| GAP | 11 | T02, T10, T16, T20, T21, T25, T27, T29, T31, T38, T40 |
| **Total** | **44** | |

The 11 gaps cluster on three blockers rather than on unimplemented code:

- **Product approval**: T02, T10, T25 need approved formulas and classification policy
  (D07/D08, backlogs A7/A8). No internal work moves these.
- **Fiscal and taxonomy semantics**: T16 remains (T12/T15 closed 2026-10-26 under backlog A5).
- **Identity, roles and persistence**: T20, T21, T27, T29, T31, T38, T40 need artifact/URL
  identity, role-bound operands or actual storage (backlogs A1/A9 and A6). T21 and T29 are
  the two where a similarly named test exists but does **not** implement the scenario.

The 15 partials mostly lack one negative case each (a gap variant, a currency pair, a
cross-identity supersedes) rather than a whole capability.

## Not claimed

- Not a claim that 18 scenarios are "done" in any product sense; they are proven at fixture
  level only.
- Not a re-run of the historical counts in older documents: the 50-test figure is this
  session's filtered run, nothing more.
- No frozen behaviour was changed, and no test was added or modified for this table.
