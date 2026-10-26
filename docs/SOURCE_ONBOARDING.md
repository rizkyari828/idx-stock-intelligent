# Adding a data source without a refactor

Goal: a new provider should cost **data + a small adapter**, not a redesign. The
repository already separates the three things that change at different speeds:
acquisition (Python), rights/admission (config + registry), and canonical evidence
(.NET). A new source plugs into one of them, not all of them.

## Tier 1 — configuration only (no code)

Applies when the provider emits claims the repository already models (OHLC, instrument
identity, corporate action, fundamental filing facts).

1. Register the identity: a `source` row with `terms_status`, or a
   `SourceCatalogEntry` in the usage configuration.
2. Declare capabilities with their evidence reference (`OHLC`, `INSTRUMENT_IDENTITY`,
   `CORPORATE_ACTION`, `FUNDAMENTAL_FILINGS`, …). A capability without an evidence
   reference is not a claim.
3. Record the permission decision: `decisionId`, `termsReference`, `reviewedAt`,
   `validFrom`, `validUntil` and explicit per-operation rules. Both interval bounds are
   required; unknown or conditional fails closed.
4. Add one `SourceSelection` per capability so a caller cannot smuggle in another
   source.
5. Prove it: `scripts`/tests already exercise this path. `SourceOnboardingTests` adds a
   brand-new synthetic provider (`future-provider`) purely through JSON and asserts
   `USAGE_PERMITTED`; a disabled or undeclared source is still denied.

That is the whole lift: no `.cs` change, no migration, no new endpoint.

## Tier 2 — a small adapter (one new file, bounded)

Applies when the provider's transport differs but the claims already exist.

1. Add `collectors/python/src/idx_stock_collector/<provider>.py` that fetches, archives
   the original bytes, and emits the versioned manifest. Bypass techniques are out of
   scope permanently: no stealth browsers, no clearance-token reuse.
2. Reuse the archive + bounded ingestion path; do not write canonical rows directly.
3. Map provider fields to the existing claim vocabulary through a convention record
   (source concept → metric/unit/period), not by editing the decoder.
4. Add fixtures and a focused test; keep the network path injectable.

The .NET side stays untouched unless the provider introduces a claim family the
contracts do not have.

## Tier 3 — a new claim family (additive contract, deliberate)

Only when the data genuinely does not fit an existing family (for example a typed
corporate-action event the fundamental statement-only contract excludes). That needs a
new versioned contract with its own review and freeze — the path `fundamental-evidence-v0.1.0`
took. It is intentionally slow; it is not a refactor of the existing code.

## What never changes when a source is added

- Archive-before-parse, content hashes and receipt chronology.
- Evidence envelopes, revision series and point-in-time visibility.
- Fail-closed admission: unknown permission, expired interval, unresolved restriction
  or missing capability always denies.
- The rule that a usage permit is not an admission, and an admission is not source
  truth.

## Checklist

```text
[ ] source id + scope id chosen (stable, no URLs)
[ ] terms/rights decision recorded with evidence and both interval bounds
[ ] capability list + evidence references
[ ] permissions per required operation, explicit only
[ ] selections mapped one capability at a time
[ ] collector added only if the transport differs
[ ] fixtures + focused test (no live network in tests)
[ ] docs updated; no frozen contract reinterpreted
```
