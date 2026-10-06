# Screener Evidence V0.2 — persisted binding contract

Binding frozen: **2026-10-06, Asia/Jakarta**. Policy:
`screener-evidence-v0.2.0`; envelope schema **1**; domain payload schema **1**.

This is the normative storage-to-domain binding supplement to
[the frozen evidence contract](SCREENER_EVIDENCE_V0_2_CONTRACT.md). It fixes the
Slice 3B reconstruction blocker without changing source authority, PIT visibility,
validity, price comparability, eligibility, technical formulas or the funnel.
It authorizes no reader, provider, migration execution or V0.1 conversion in this
documentation milestone. The parent contract governs economic/product meaning.

## 1. Inspection and storage decision

Slice 1 provides `EvidenceFact<T>`, `TradingStatusEvidence`, `DailyBar` and
`GenuinePriceObservation`. Slice 2 provides 17 `EvidenceClaim` members, five
`EvidenceClass` members, admission/validity/resolution/status/price/comparability
evaluators. Slice 3A canonicalizes arbitrary JSON objects; its immutable envelope
does not retain class, scope kind or payload version. It therefore cannot yet
reconstruct typed facts. Mechanical examples `{"symbol":"TEST"}` and `{"v":1}`
are not semantic evidence.

Decision: **ADDITIVE_SCHEMA_CHANGE_REQUIRED**. Before typed writes/reads, add only
these immutable envelope fields to `screener_evidence_record`:

| Field | Typed meaning |
|---|---|
| `evidence_class` | One of the five durable class tokens in section 2 |
| `payload_schema_version` | Positive domain binding version; supported value 1 |
| `scope_kind` | `INSTRUMENT` or `EXCHANGE` |
| `scope_exchange_id` | Nonempty stable local exchange UUID; contextual exchange for instrument scope, exchange identity for exchange scope |

`subject_id` remains the nonempty scope subject UUID. For `INSTRUMENT` it is the
stable instrument ID; for `EXCHANGE` it must equal `scope_exchange_id`. Exchange
IDs are retained local identities, never guessed from a ticker/provider suffix.
No exchange registry table, free specificity score, quality column, derived-result
column, new index or convenience denormalization is required by this binding.

Also extend the future claim vocabulary/check constraint with **TradingStatus**:
the existing enum has Suspension/Reopening but lacks the already-frozen affirmative
target-session TRADING fact. Append it after the existing 0..16 enum values; do not
renumber them. This fills a representation gap, not a new tradability rule.
`PriceComparability` remains an existing enum token but is rejected for base-fact
writes/semantic reads as specified in section 8.
TradingStatus admits only T1/T2 authoritative exact-session facts, matching the
existing TradingStatusEvidence constructor and parent status contract; it does
not inherit the T1-only Suspension/Reopening notice rule or admit bar-derived T3.

The additive migration must leave all old rows unchanged: four null binding fields
mean legacy/unbound, not class/version/scope defaults. Require either all four
binding fields present and internally valid, or all four null for retained legacy
rows. New production writes must supply all four. Never update immutable rows to
guess missing meaning, manufacture knowledge, or convert generic payloads.
Unknown positive payload versions may remain retained but are not decodable by
the version-1 reader. Validate new class and scope tokens with database constraints;
claim/class compatibility and typed payload validation belong at the write/read
boundary. Do not edit migration 0007. No migration is created here.

## 2. Durable evidence class and scope

| Stored token | Existing Slice 2 class |
|---|---|
| `CONTINUING_EFFECTIVE_STATE` | `ContinuingState` |
| `POINT_OBSERVATION` | `PointObservation` |
| `VERSIONED_RULE` | `VersionedRule` |
| `SESSION_FACT` | `SessionFact` |
| `SOURCE_CONVENTION` | `SourceConvention` |

Class is admitted source semantics, not computed freshness. The importer must
retain which class the source supports; neither claim nor timestamp determines it.
A roster observation and an effective governing board act can carry the same board
value but different classes. Source authority alone proves neither continuation
nor completeness.

Envelope `effective_from`/`effective_to` define the inclusive application interval.
Point observations require a finite proven interval; an exact point has equal ends.
They never acquire continuation from a new retrieval. Session facts require equal
ends and an exact session identity. Continuing states may have a null end only when
source semantics affirm continuation. Versioned rules/conventions may have a null
end only when the retained source defines an effective-until-replaced version.
Corporate-action event scope is its exact effective date; coverage scope is finite.

Historical event dates inside values are factual dates, not alternative scope:
for example, a point roster observed today can explicitly report an older listing
date. For continuing listing/delisting/event assertions, the event date equals
`effective_from`. A fact is never knowable before its own knowledge boundary.

Specificity is derived **only** from durable scope: `INSTRUMENT` maps to Slice 2
`Specificity = 1`; `EXCHANGE` maps to `0`. Equal scope kinds tie. Do not prefer a
shorter interval, newer date, session fact, larger payload or particular class as
an additional specificity rule: the parent contract freezes instrument-specific
over market-wide, not such extra rankings. Date applicability is evaluated before
specificity. Cross-exchange facts never compete. A request must establish its
exchange relationship from retained visible identity/scope evidence, not today's
mutable registry. If that relationship is unproved, exchange-wide applicability
is unproved; fail closed without guessing an exchange ID.

## 3. Common payload grammar and validation

The exact version-1 root is:

```json
{"operation":"ASSERT","completeness":"FULL","value":{}}
```

`value` is the claim-specific object below, not an arbitrary object. All three
root properties are required. `operation` is `ASSERT` or `CANCEL`;
`completeness` is `FULL` or `PARTIAL`. No payload contains shadow copies of class, version,
subject/evidence identity, scope interval, authority, source identity, recording/
knowledge/publication/retrieval clocks or evidence-record revision/lineage. These
are envelope facts. Nested references to *other* retained evidence/bar revisions
are permitted only in the named binding fields below and do not override them.

`FULL` is a durable, attributable source/import assertion that the named fact or
coverage is complete within its declared scope. It is never a synonym for VERIFIED.
`PARTIAL` records expressly incomplete coverage/components. It cannot establish
absence, continuity or clearance. Missing retained evidence is neither a FULL
negative assertion nor evidence of completeness.

For ASSERT, required properties must exist and have their specified nonnull type,
except fields explicitly marked nullable below. PARTIAL does not relax the typed
grammar: it describes incomplete supporting proof/coverage for an identified fact,
not permission to invent a missing factual component. Missing properties,
unknown properties or wrong types are malformed regardless of completeness.
Optional properties may be omitted; omission and explicit null have the same typed
meaning. Null never becomes zero, false, an empty string or a default enum.

For CANCEL, `value` is null and completeness is FULL. Require an explicit
`supersedes_revision_number` naming an earlier ASSERT in the exact same namespaced
series, claim, subject, class and exchange scope. Cancellation is a retained
authoritative disposition, not a negative factual value. At a visible cancellation
boundary it terminates the referenced fact in the applicable scope, preserves
earlier-cutoff replay, and yields UNKNOWN/EVIDENCE_CANCELLED unless another fact
covers. No cancellation by provider ID, missing row, inferred absence or bare
revision-number coincidence. An unavailable cancellation target is an explicit
retained-input failure. Cancellation handling must not be represented by passing a
null factual candidate to `ScreenerEvidenceResolver`.

Decoder rules are exact and shared across claims:

- Validate supported policy/envelope/payload-version binding before interpretation.
  No current/latest/compatible-looking decoder fallback.
  Unknown class/version is unsupported binding; a known class forbidden for the
  claim, invalid scope kind or inconsistent envelope/value scope is malformed.
- UTF-8 JSON object, case-sensitive camelCase property names; reject duplicate
  properties recursively, unknown properties, trailing content, comments, numeric
  enums, NaN/Infinity and coercion. Use the existing canonical object-key ordering.
- Tokens use exactly the spellings below. IDs are nonempty UUIDs in lowercase
  hyphenated `D` format. Dates are `yyyy-MM-dd`; instants are UTC ISO-8601 strings
  with `Z` and at most seven fractional digits. Never locale-parse.
- Prices/ratios are invariant decimal strings with grammar
  `-?(0|[1-9][0-9]*)(\.[0-9]+)?`, exactly representable as .NET decimal; no exponent,
  grouping, whitespace or silent rounding. Counts/revision numbers are JSON
  integers exactly representable in their specified signed integer type.
- Codes/references/session IDs are ordinal, nonempty text, at most 200 characters,
  with no control characters or leading/trailing whitespace. No implicit casing,
  trimming or vendor-code interpretation. Source-native codes remain opaque.
- Hashes are 64 lowercase SHA-256 hex characters. Sets are unique and sorted by
  ordinal canonical ID; ordered sequences retain their declared order.
- Maximum canonical payload: 64 KiB UTF-8, depth 16, any array at most 256 entries.
  Bound excess is an explicit failure, never truncation. These are safety bounds,
  not a permission to discard evidence or infer incomplete coverage as complete.
- Verify stored canonical payload and persisted payload_sha256 agree. Hashing a
  reconstructed object must not hide a stored-hash mismatch. Retain raw artifact
  identity/hash/length from exact local metadata when required; no provider/file
  replacement, live evidence, or equivalent-looking archive substitution.

Malformed data returns explicit `PERSISTED_EVIDENCE_MALFORMED`; unsupported binding
returns `PERSISTED_EVIDENCE_BINDING_UNSUPPORTED`; unbound legacy rows return
`PERSISTED_EVIDENCE_UNBOUND`; excessive size returns
`PERSISTED_EVIDENCE_BOUND_EXCEEDED`; lost referenced premises return
`PERSISTED_EVIDENCE_INPUT_UNAVAILABLE`. These are bounded reader/codec failures,
not additions to EvidenceQuality or permission to ignore a potentially decisive
candidate. No fallback to another revision after a required candidate fails.

## 4. Exact claim schemas

Class abbreviations: C = CONTINUING_EFFECTIVE_STATE, P = POINT_OBSERVATION,
V = VERSIONED_RULE, S = SESSION_FACT, F = SOURCE_CONVENTION. I = INSTRUMENT,
X = EXCHANGE scope. Property types and equality rules are normative; all unlisted
properties are prohibited. All rows inherit section 3 validation/failure behavior.

| EvidenceClaim | ASSERT `value`: required fields | Optional fields | Classes; scope | Typed equality within comparable claim/scope |
|---|---|---|---|---|
| StableIdentity | `sourceCode`: text | none | C/P; I, explicit identity interval | sourceCode; stable local instrument/exchange binding comes from envelope |
| SecurityType | `classification`: ORDINARY/INDEX/OTHER | none | C/P; I | exact classification; OTHER states an observed non-ordinary/non-index type, never guessed from suffix |
| Currency | `currencyCode`: three uppercase ASCII letters | none | C/P; I | exact currencyCode; no OTHER/UNKNOWN placeholder as a currency fact |
| ListingCoverage | `listingDate`: date | none | C/P; I | exact listingDate; missing date is malformed as this claim, never inferred from a roster/earliest bar |
| Delisting | `delistingDate`: date | none | C/P; I | exact delistingDate; missing notice is not a negative delisting fact |
| BoardRegime | `boardCode`: MAIN/DEVELOPMENT/ACCELERATION/NEW_ECONOMY/SPECIAL_MONITORING/OTHER | `otherCode`: text, required only for OTHER | C/P; I | boardCode plus otherCode; OTHER preserves source identity without declaring economic support |
| BoardChange | `changeId`: text, `fromBoard`: BoardValue, `toBoard`: BoardValue, `changeDate`: date | none | C; I, from changeDate | changeId/fromBoard/toBoard/changeDate |
| ExchangeRuleVersion | `ruleId`: text, `ruleVersion`: text, `mechanism`: CONTINUOUS/CALL_AUCTION/OTHER | `otherMechanism`: text, required only for OTHER | V; X/I, version interval | ruleId/ruleVersion/mechanism/otherMechanism; board identity alone never supplies mechanism |
| MechanismException | `exceptionId`: text, `exceptionType`: text, `mechanism`: CONTINUOUS/CALL_AUCTION/OTHER | `otherMechanism`: text, required only for OTHER | C; I/X | exceptionId/exceptionType/mechanism/otherMechanism; opaque exceptionType adds no supported-regime policy |
| Suspension | `status`: SUSPENDED, `noticeId`: text | none | C/P; I/X; C starts at suspension, P proves only its bounded scope | exact status; noticeId is retained provenance, not factual inequality |
| Reopening | `reopeningId`: text, `status`: TRADING, `reopeningDate`: date, `suspensionEvidenceId`: UUID | none | C; I/X, starts at reopeningDate | reopeningId/status/reopeningDate plus exact terminated suspension binding |
| ScheduledSession | `sessionId`: text, `schedule`: OPEN/CLOSED | none | S; X, exact date | exchange UUID from envelope, sessionId, date from scope, schedule |
| CompletedSession | `sessionId`: text, `completion`: COMPLETED/UNPROVED, `completedAt`: nullable instant | none | S; X, exact date | exchange/session/date/completion/completedAt |
| CorporateAction | tagged EventValue or CoverageValue defined in section 6 | only fields defined there | Event: P, I exact event date; Coverage: P, I finite window | section 6 |
| SourcePriceConvention | ConventionValue defined in section 7 | none | F; I/X, version interval | exact documented convention/capabilities, section 7 |
| GenuinePriceObservation | PriceValue defined in section 7 | none | S; I, exact date | section 7 |
| PriceComparability | **No base-fact payload** | none | none | derived result; reject as a persisted base input, section 8 |
| TradingStatus (required additive claim) | `status`: TRADING/SUSPENDED, `sessionId`: text | none | S; I/X, exact date | exchange/session/date/status; positive authoritative session fact, never a bar-derived status |

`BoardValue` is exactly `{ "boardCode": <board token> }` with optional `otherCode`
under the same OTHER-only rule. Unsupported board/security/mechanism support is
still determined by frozen policy; this encoding does not change allowed markets.
P may represent an explicitly bounded observation of a continuing subject; it
never makes that observation C. Exchange-wide status/exception applicability must
be positively sourced for the requested instrument, not inferred from a session
being open.

Required IDs/discriminators are never null: sourceCode, ruleId/ruleVersion,
exceptionId/exceptionType, changeId, noticeId, reopeningId, suspensionEvidenceId, sessionId, all tagged
variant types, eventId and all referenced evidence IDs. A nonnull value does not
upgrade the supporting proof/coverage of an incomplete assertion. CompletedSession
UNPROVED requires completedAt null; COMPLETED requires nonnull completedAt no later than envelope
knownAt and cutoff. FULL UNPROVED is still unproved completion, not VERIFIED.
An identified session with expressly unproved completion has PARTIAL completion
quality; an absent/invisible session has UNKNOWN. A closure/schedule alone never
supplies completedAt.

## 5. Equality, quality and status reconstruction

Resolve alternatives for one logical fact, not unrelated coexisting events. The
resolution key is claim + applicable subject/exchange scope, refined as follows:

| Claim | Additional logical fact key |
|---|---|
| ExchangeRuleVersion | ruleId (the requested effective version is selected within this rule's retained scope/lineage) |
| MechanismException | exceptionId |
| BoardChange | changeId |
| Suspension | noticeId (combine resolved notices as status evidence afterwards) |
| Reopening | reopeningId |
| ScheduledSession / CompletedSession / TradingStatus | exchange/sessionId/exact date |
| CorporateAction EVENT | eventId |
| CorporateAction COVERAGE | coverageId |
| SourcePriceConvention | priceSourceId + endpoint + field + version |
| GenuinePriceObservation | described price source/endpoint/field/version + exchange/sessionId/exact date |
| Remaining factual claims | no additional key |

Separate keys yield a bounded ordered set of facts. For example, a split and a
bonus event are not conflicting merely because both use CorporateAction. An
amendment must preserve the logical fact identity and use retained lineage; a new
event is not an amendment. Canonical event identity must be explicitly retained
when aligning multiple source accounts; never invent equivalence from dates or
matching ratios. Cross-source notices without proved identity remain separate
events, not a fabricated supersession link. Source-convention versions are queried
by their explicit domain field/version binding, never by "latest version now".
Event/change/coverage IDs identify a logical assertion independently of its factual
terms. Corrections still require the frozen same-source/scope lineage relationship;
a changed date or interval cannot silently erase another scope's retained fact.
Changing an exact event/session/bar instrument/session/date identity requires an
explicitly retained cancellation/new fact, not endpoint sliding. These IDs never
grant supersession beyond the frozen scope/lineage rules.

Construct separate typed factual values and retained provenance. Resolver equality
uses the value projections in section 4, never JSON text/order, complete envelopes,
record UUIDs or arbitrary `JsonElement` equality. Strings use ordinal equality;
decimals use exact .NET decimal value equality without tolerance (1.0 equals 1.00);
instants compare exact UTC instants; UUIDs/dates/enums compare exactly; sets compare
canonical members structurally. Scope/claim/class constrain comparable candidates;
different classes are evaluated for their own validity before values compete.
Null components compare as unknown components, not positive equality proving truth.

Evidence ID, source reference, raw archive ID, known/retrieved/recorded timestamps,
record revision and lineage remain available for authentication and selected-input
replay but are not factual disagreement by themselves. A semantic duplicate can
retain multiple provenance records. Immutable write idempotency still requires
exact envelope/content identity, a different check from factual equality.
When the resolver returns a value equal across multiple survivors, preserve their
provenance; use its existing knownAt/revision/series representative order and
evidenceId ordinal only to order otherwise identical representatives. That final
ordering cannot pick between different factual values or erase conflicts.

EvidenceQuality is **never persisted as timeless truth**. Reconstruct chronology,
scope and class from envelope; apply visibility, source admission, durable
completeness/proof prerequisites, class validity and pure conflict resolution.
Invisible/missing/unproved prerequisites are UNKNOWN, explicit incomplete applicable
coverage is PARTIAL, exhausted observations are STALE, and irreconcilable equally
applicable asserted facts are CONFLICTING. A FULL assertion alone cannot bypass
source admission, exact scope, completion or price-authenticity proof. Use existing
Slice 2 helpers for their defined decisions; do not duplicate precedence in SQL.
The reader must retain diagnostic partial assertions rather than treating them as
complete resolver facts. A partial conflicting component cannot be silently used
to manufacture an affirmative verified status/clearance.
Evaluate visibility and class applicability before completeness: a future-effective
PARTIAL fact is UNKNOWN for an earlier date, and a proven but exhausted point is
STALE, not made applicable by its completeness token. No quality-based authority
tie-break is added by this binding.

Suspension reconstructs SUSPENDED only from admitted positive evidence. Reopening
reconstructs a positive TRADING transition only with its exact retained suspension
reference, same instrument/exchange applicability and correct original chronology.
Do not mutate/end-date the old suspension row. Before reopening knownAt it remains
invisible; after that boundary the latest applicable affirmative state transition
may terminate the suspension according to the frozen status evaluator. A correction
to either notice first resolves its own visible revision series. A bounded
suspension ending without affirmative reopening yields REOPENING_UNCONFIRMED;
scope expiry cannot invent TRADING. A standalone authoritative exact-session TRADING
fact uses TradingStatus, not Reopening with a fabricated suspension reference.

No hidden eligibility/readiness computation belongs in a base claim decoder.
Scheduled OPEN does not prove completion; completed exchange sessions do not prove
instrument TRADING; genuine price admission does not prove trading status.

## 6. Corporate action event and coverage values

EventValue has required properties:

- `kind`: `EVENT`;
- `eventId`: nonempty text, canonical action identity within the instrument, explicitly retained
  rather than guessed; source-native notice identity remains envelope provenance;
- `actionType`: SPLIT/REVERSE_SPLIT/RIGHTS_OR_SHARE_EVENT/
  BONUS_OR_SHARE_DISTRIBUTION/CONVERSION/MERGER_OR_REORGANIZATION;
- `effectiveDate`: date equal to exact envelope event scope;
- `ratio`: nullable `{ "newUnits": <decimal>, "oldUnits": <decimal> }`, both
  strictly positive, only the explicitly sourced unit ratio;
- `termsReference`: nullable text pointing to retained factual terms within source
  provenance, not a URL to fetch during replay.

Equality is instrument-qualified eventId, actionType, effectiveDate and typed ratio.
termsReference differences are provenance, not automatically contradictory terms;
contradictory explicitly retained ratios remain conflicting. Null ratio does not
mean 1:1. An identified positive break event does not need a fabricated ratio to
establish KNOWN_BREAK. Its type maps one-to-one to existing CorporateActionKind;
no adjustment factor, valuation, entitlement or accounting computation is added.
Do not classify unsupported action types into a break class by a heuristic.
An event remains a retained dated fact for any requested seed/window containing
its effectiveDate; it is not a continuing split "status" at every later date.

CoverageValue has required properties:

- `kind`: `COVERAGE`;
- `coverageId`: nonempty text identifying the retained logical coverage assertion;
- `coverage`: `COMPLETE` or `PARTIAL`;
- `eventEvidenceIds`: canonical set of exact retained EVENT evidence UUIDs.

Coverage covers exactly the finite envelope interval/instrument. COMPLETE is an
explicit authoritative completeness assertion for the contract's relevant action
coverage; an empty event set is positive no-event evidence only with that assertion,
not a database search that found no events. PARTIAL coverage or PARTIAL root
completeness cannot clear a window. Exact event references must be available,
authentic, visible at the requested cutoff and within the coverage scope. Coverage
equality compares coverageId, declared interval, coverage and the referenced typed event set, not differing UUIDs
for semantic duplicate events; all exact IDs remain retained for replay.
An unsupported relevant action or unidentified/missing event prevents complete
coverage from being established. No new cash-dividend or other action economic
interpretation is defined here.

## 7. Source convention and genuine price values

ConventionValue properties (all required):

- `priceSourceId`: opaque nonempty text identifying the price source being
  described, a factual target distinct from envelope provenance source_id;
- `endpoint`, `field`, `version`: opaque nonempty text identifying documented
  provider endpoint/price field/domain convention version;
- `priceKind`: `STOCK_RAW`, `INDEX_LEVEL` or `UNPROVED`;
- `continuity`: `RAW_AS_TRADED` or `UNPROVED`;
- `zeroVolumeMeaning`: `GENUINE_NO_EXECUTION`, `EXPLICIT_GENUINE_FLAG` or `UNPROVED`;
- `syntheticIdentification`: `EXPLICIT_MARKER`, `DOCUMENTED_NON_SYNTHETIC` or
  `UNPROVED`;
- `documentationReference`: nonempty text identifying retained source documentation.

priceSourceId describes whose price field the document is about; it does not
redefine who supplied the evidence. Envelope source_id remains the provenance
source of the convention document, and authority/publication/version scope cannot
be overridden by payload. Equality uses described source,
endpoint/field/version and the four semantic capability tokens, not documentation
reference formatting. UNPROVED is retained missing capability, not positive proof
of that capability. Evaluate each required capability separately: unproved zero-
volume semantics cannot globally reject a nonzero observation, and unproved volume
units never invalidate independently authenticated price. A convention change is
an append-only revision with its own
knownAt; no provider-specific zero-volume or adjustment behavior is presumed.

PriceValue has required properties:

- `sessionId`: nonempty text;
- `bar`: object with exactly `open`, `high`, `low`, `close` (decimal strings),
  `volume` (nonnegative Int64), `adjustedClose` (nullable positive decimal string),
  `volumeUnit`, `volumeBasis`, `marketSegment` (explicit opaque metadata text;
  unresolved metadata uses `UNKNOWN`, with no invented unit conversion);
- `barRevision`: positive Int64 revision of the retained source bar observation,
  **not** the evidence record's correction revision;
- `barContentHash`: SHA-256 of UTF-8 canonical `bar` object using existing canonical
  JSON rules, independent of root payload hash;
- `conventionEvidenceId`: exact retained SourcePriceConvention UUID;
- `completedSessionEvidenceId`: exact retained CompletedSession UUID;
- `syntheticOrCarryForward`, `placeholder`, `substituted`: each a tri-state token
  `YES`/`NO`/`UNPROVED`, positively observed or established by retained source
  convention semantics; no inference from price/volume magnitude;
- `zeroVolumeSemantics`: `NOT_APPLICABLE`, `EXPLICITLY_GENUINE` or `AMBIGUOUS`;
- `zeroVolumeProof`: `EXPLICIT_SOURCE_FLAG`, `DOCUMENTED_CONVENTION` or `NONE`,
  the durable basis for that classification, not an evaluator success flag.

Instrument/exchange/date and observation chronology come from envelope; no shadow
DailyBar instrument/date/source clocks are accepted. Envelope raw_artifact_id and
retrieved_at are mandatory for this claim. Build DailyBar.Source from envelope
source_id/raw_artifact_id/retrieved_at/known_at and exact raw-artifact SHA-256
metadata. DailyBar OHLC positivity/high-low invariants and all existing domain
invariants apply. The supplied bar hash must match the canonical retained bar.
Construct PriceObservationCandidate with barRevision/barContentHash and envelope
chronology, not evidence-record revision masquerading as a bar revision.

Resolve exact convention/session references under the same snapshot and cutoff;
check exchange/date/session/source/endpoint applicability; convention priceSourceId
must equal the price observation's envelope source_id. Missing or corrupt
references fail explicitly; visible unproved convention/completion does not admit
price. SourceConventionAdmitted and SessionCompleted booleans are derived from
those authenticated facts, never accepted as stored booleans.
Every required premise must have been legitimately known at the referencing
record's knownAt as well as visible at the query cutoff. Apply that requirement to
all exact premise references (including reopening/cancellation/action coverage),
not only price. A referenced proof learned
after that original admission boundary cannot retroactively repair the record.

YES rejection markers map to the corresponding frozen rejection. NO must be
supported by durable evidence; UNPROVED cannot map to false. For a zero-volume bar,
EXPLICITLY_GENUINE requires completed-session proof and either an actually observed
source flag whose documented semantics distinguish genuine no execution
(EXPLICIT_SOURCE_FLAG with EXPLICIT_GENUINE_FLAG capability), or the exact
convention's GENUINE_NO_EXECUTION capability (DOCUMENTED_CONVENTION). NONE cannot
support EXPLICITLY_GENUINE; otherwise retain AMBIGUOUS/fail closed. A nonzero bar
uses NOT_APPLICABLE/NONE; zero with NOT_APPLICABLE is malformed. AMBIGUOUS uses
NONE. Volume alone never proves any marker. Retain rejected candidates; they are
not admitted observations.
An UNPROVED rejection marker leaves price not admitted with PRICE_NOT_ADMITTED /
EVIDENCE_UNKNOWN; do not invoke the bool-based price helper with a fabricated NO.

Price factual equality compares instrument/exchange/session/date, described price
source/endpoint/field/version, all typed bar components, barRevision, rejection
markers, zero semantics and zero proof basis. Exact premise IDs/archive IDs/root
payload hash are provenance bindings; differing IDs for semantically identical authenticated
premises are not a factual conflict. Decimal lexical scale can yield different
transport hashes without different price facts. barContentHash is authenticated
against its own retained representation, not used instead of typed equality.
Never replace a selected bar revision or premise with latest compatible evidence.

## 8. PriceComparability is a derived result

Choose **B**: PriceComparability is a derived evaluator result, not a base evidence
claim or an external timeless CLEARED attestation. Parent section 10 requires
exact-window identity/currency/unit/convention/session/action/revision premises;
the existing PriceComparabilityRequest already expresses those premises. Persist
those source facts and bindings, then derive CLEARED/KNOWN_BREAK/UNRESOLVED for the
requested segment and cutoff. Do not persist the six request booleans as authority.

The existing EvidenceClaim.PriceComparability and permissive persistence/admission
path are an implementation representation gap: accepting arbitrary JSON under that
claim does not create legitimate comparability evidence. Future typed append/read
must explicitly reject it with PERSISTED_EVIDENCE_DERIVED_CLAIM, preserve any legacy
row mechanically, and never use it to clear a window. Do not remove/renumber the
enum or reinterpret old data. External coverage/convention attestations are their
respective factual claims, not a shortcut CLEARED payload. Computation and final
Screener orchestration remain later scope.

## 9. Versioning, legacy fixtures and implementation boundary

Payload version applies to this provider-neutral domain binding, across all claims;
claim selects its value schema. Future incompatible meaning requires an explicit
new payload version and decoder. Version 1 never reads a future version as version
1. New providers map to the same domain schema, not provider-specific versions.
Envelope schema remains 1; additive binding fields are explicitly required for
typed decoding. Legacy null-binding rows remain unbound and cannot be promoted by
matching JSON shape, guessing claim/class, or assigning a default payload version.

Existing generic Slice 3A fixtures may remain **test-only mechanical fixtures** in
owned disposable storage, explicitly exercising legacy/unbound round-trip rules.
Typed append/reader/semantic acceptance fixtures must use valid version-1 bindings.
Generic fixtures are never valid production semantic evidence.

Slice 3B is **unblocked at the design level** by this binding. Implementation must
first add the scoped migration, immutable record/store bindings and strict codecs,
including the affirmative TradingStatus claim; these are prerequisites, not work
completed here. A reader-only implementation against current migration 0007 is
still insufficient. SQL loads retained candidates; pure Slice 2 helpers decide
semantics after typed reconstruction. New codec/precondition adapters must preserve
their frozen behavior, not rewrite the evaluators or invent current evidence.

## 10. Scenario cross-check and future acceptance

These are design checks, not executed implementation tests.

| Scenario | Required binding/replay result |
|---|---|
| Continuing suspension | C + positive SUSPENDED + proved open scope stays applicable without an age TTL |
| Reopening/corrected transition | Exact suspension binding, positive TRADING, own knownAt and visible revision selection; suspension remains at the earlier cutoff |
| Point roster/board observation | P with finite proved interval never carries forward as C; later scope is stale/unavailable |
| Exact completed session | X + S + exact session/date + COMPLETED/completedAt proof; another date or UNPROVED cannot establish completion |
| Equally authoritative conflicting boards | Typed board disagreement without valid lineage remains CONFLICTING; JSON ordering/provenance formatting does not choose a winner |
| Correction learned later | Same namespaced lineage; prior revision selected before correction knownAt, newer correction only afterwards; both retained |
| Genuine zero-volume observation | Positive zero semantics + exact convention/completed-session proofs can admit; magnitude alone cannot |
| Synthetic carry-forward | Explicit YES marker is retained and rejected by frozen price admission |
| Split inside technical seed | Positive SPLIT event establishes KNOWN_BREAK for the containing segment; no invented ratio/adjustment; frozen restart/warmup rules remain |
| Partial corporate-action coverage | PARTIAL root/coverage or lost event premise cannot produce CLEARED |
| Convention revision | Explicit source/endpoint/field/version and correction chronology; future convention cannot repair earlier admission |
| Malformed/unknown payload version | Explicit bounded failure, no coercion/default decoder/evidence fallback |

Before implementation acceptance, add strict codec/equality tests for every claim
and variant, duplicate properties, numeric range/decimal scale, hash mismatch,
legacy rows, unsupported versions/derived claims, class/scope compatibility,
cancellation and missing/future premises. Exercise PIT/conflict/status/price
scenarios in the owned disposable PostgreSQL harness and retain standard Slice
1/2 and V0.1 regression discovery. No provider calls, operational evidence writes,
V0.1 conversions, historical backfill or Outcome/Research semantic changes.
