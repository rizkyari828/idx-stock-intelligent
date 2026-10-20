# Fundamental Evidence Binding V0.1 — freeze decision package

Status: **DECISION_PACKAGE — NOT FROZEN**. Prepared 2026-10-20 WIB on `main` at
`a0275c1`, clean worktree; no fetch. Candidate unchanged since `1fffb58`.

This package exists to support **one human decision**. It does not freeze the
candidate, does not approve it on the owner's behalf, does not authorize a build
and does not admit any source. The candidate remains
[PROPOSAL — NOT FROZEN](FUNDAMENTAL_EVIDENCE_BINDING_V0_1_PROPOSAL.md); nothing
in this file changes it.

## 1. Candidate under decision

| Item | Value |
|---|---|
| Document | [FUNDAMENTAL_EVIDENCE_BINDING_V0_1_PROPOSAL.md](FUNDAMENTAL_EVIDENCE_BINDING_V0_1_PROPOSAL.md) |
| Proposed policy identity | `fundamental-evidence-v0.1.0` (new, additive) |
| Exact candidate sections | §§18–22 schemas/keys/numeric/period/PIT/store; §23 synthetic examples; §24 D01–D09; §25 findings and test reconciliation |
| Proposed scope | **statement-only** (D04); typed corporate-action events excluded |
| Scenario inventory | T01–T35 mapped to the candidate; T36–T44 new specifications; X01–X15 synthetic examples |
| Internal readiness label | `READY_FOR_FREEZE_REVIEW` (§25.3) |
| Freeze status | not frozen; no production type, migration or reader added |

## 2. Internal validation performed (D1)

Bounded, source-grounded check of the claims this decision depends on. It is a
**same-line internal review**, not the independent review contemplated by D2: no
second verified model route or independent reviewer was used, so the D2 condition
is unmet and no independent approval is claimed.

| Claim the decision relies on | Checked artifact | Result |
|---|---|---|
| Exact numeric domain is the existing `EvidenceDecimal`, not a new numeric type | `src/IdxStockIntelligence.Application/ScreenerEvidenceV02Payloads.cs` (regex, 200-character limit, decimal 96-bit coefficient path, string-only JSON converter) | Consistent with N01/D02 as described; no wider domain introduced |
| Persistence must follow the sequential migration chain | `Migrations/0007…0011`: each file guards on its predecessor version (0007 requires 6, 0011 requires 10) | Consistent with F12/D09 wording; any provisional `0012` must follow `0011`, and `0007–0011` are absent operationally (carried-forward OPS-1 report) |
| The new family is additive, not a reinterpretation of frozen V0.2 | `docs/SCREENER_EVIDENCE_V0_2_CONTRACT.md` §1/§3 admission matrix covers market-evidence dimensions only; no fundamental family is defined there | Consistent with D01/F02: a fundamental family must be added under its own identity, and V0.2 enums/claims may not be re-read |
| Frozen V0.1/V0.2 behaviour is not modified by the candidate | Candidate text and diff scope | Documentation only; no code, contract or migration change in this package |

Not performed in this pass, and therefore not certified: a line-by-line
re-derivation of §§18–20 key and equality algorithms, golden canonical-byte
proof, DB concurrency/restore acceptance, parser byte budgets, and any live
source check. Those are later slice scopes, not freeze prerequisites, but they
mean "reviewed internally" is not "proven".

## 3. Findings and their disposition

Per §25.1: F01–F03, F05–F08, F10–F12 are RESOLVED in the candidate; F14 is
DEFERRED; three items remain open and are the reason this package asks for
decisions rather than silently closing the review.

| Finding | Disposition | Consequence for freeze |
|---|---|---|
| F04 statement-only scope (D04) | REQUIRES_APPROVAL | Freeze is only valid for the statement-only scope; typed events need a separate schema and review |
| F09 canonical FCF boundary (D05) | REQUIRES_APPROVAL | Reported FCF stays source-native; only a disabled DERIVED token is proposed; no formula or equivalence is approved |
| F13 external permitted route | STILL_UNRESOLVED | Source-independent schema may be reviewed; production remains `HOLD_FOR_EVIDENCE` |
| F15 OPS archive-count uncertainty | STILL_UNRESOLVED | Editorial only; both reported counts stay unverified; non-blocking for the contract |
| D07 formulas / D08 classification | DEFERRED | Must freeze as disabled capability, never as usable accounting or investment policy |

## 4. Decisions requested

Each row is a separate decision. Approval of one row does not imply another.

| # | Decision | Recommendation | If approved | If declined |
|---|---|---|---|---|
| P1 | Accept **statement-only** V0.1 scope (D04) | Approve as proposed | Freeze covers issuers' statement facts and conventions only; events excluded and documented as a gap | Candidate must be re-scoped to typed events first — a new schema and a new review, not a small edit |
| P2 | Accept **FCF semantic boundary** (D05): reported FCF is source-native; canonical FCF is DERIVED and disabled | Approve as proposed | FCF token freezes as a reserved, non-enabled capability | Freezing the FCF token is deferred; the rest of the candidate can still freeze without it |
| P3 | Accept the exact contract bundle: D01 wire/identity keys, D02 numeric bounds, D03 entity/security bindings, D06 ADM/PIT/rights, D09 logical store | Approve as proposed | The exact schemas and algorithms become frozen text | Targeted revision; the affected section returns to proposal state |
| P4 | Authorize the **freeze itself** (separate from P1–P3) | Authorize after P1–P3 are confirmed | Candidate becomes frozen V0.1; additive versioning rules then apply | Everything stays proposal; E1–E5 remain blocked |
| P5 | Authorize the **bounded build** E1–E5 (separate from P4) | Authorize only after the frozen text exists | Implementation may begin with the narrow slice order in §6 | E1–E5 stay CONDITIONAL; no production fundamental type is added |

Approving P1–P3 without P4 is coherent: it records the exact text you accept while
leaving freeze and build unauthorized. Approving P4 without P5 is also coherent
and is the lower-risk path.

## 5. Recommendation

**`READY_FOR_USER_APPROVAL`** for this **statement-only** candidate, conditional
on the decisions in §4, and subject to the two caveats below.

This is not `NOT_READY`: no known internal correctness gap is left to
implementation discretion, and the open items are owner scope decisions (D04,
D05), deferred capability (D07, D08) or external evidence (F13), none of which is
a defect in the proposed text.

This is not `NEEDS_TARGETED_REVISION` either, because the proposed resolutions in
§24 are internally consistent with the repository facts checked in §2.

Caveats that belong in the approval record: (a) independent review was not
performed, so P3 is an owner acceptance of an internally-reviewed text;
(b) F15 remains an editorial uncertainty and is not evidence of a data problem.

## 6. What approval would not do

- No source admission, no acquisition route, no provider activation; F13 and the
  Level-1 disposition are untouched.
- No migration authored or deployed; `0007–0011` stay undeployed and the
  provisional `0012` stays provisional.
- No parser, adapter, reader, registry row or `DATA_READY` claim.
- No accounting formula, classification rule, threshold or valuation policy
  (D07/D08 remain disabled).
- No change to frozen Screener V0.1/V0.2, Research, Decision Snapshot or Outcome
  semantics.

## 7. Carry-forward items for the freeze text

1. Conservative inactive-premise readmission interval: approving the text also
   approves that corrected facts may be unavailable in some historical windows.
   This is deliberate, not a bug; it must be stated in the frozen text.
2. `0012` numbering and any DDL: separate design and separately authorized
   deployment (OPS-1 reports `0001–0006` applied, `0007–0011` absent).
3. F15: keep both archive counts as unverified report descriptions; no recount is
   authorized by this decision.
4. D09 storage acceptance (bounds, concurrency, restore) remains a later,
   separately authorized slice.

## 8. After approval — bounded slice order

Unchanged from the [implementation handoff](PERSONAL_FIRST_MASTER_V1.md):
E1 three typed bindings/envelope, E2 exact numerics, E3 canonical bytes, E4 pure
fixture admission, E5 revision/PIT/premise fixtures. Each slice is separately
scoped, standard root .NET discovery plus focused deterministic checks; no parser,
no table, no source adapter inside E1–E5.

## 9. Decision record (to be completed by the owner)

```text
DATE (WIB):
P1 statement-only scope:      APPROVED / DECLINED / REVISED
P2 FCF boundary:              APPROVED / DECLINED / REVISED
P3 exact contract bundle:     APPROVED / DECLINED / REVISED
P4 freeze authorization:      APPROVED / DECLINED
P5 bounded build E1–E5:       APPROVED / DECLINED
Notes / accepted deviations:
```

## 10. Validation and boundary of this package

Documentation only. No contract text, production code, test, migration, registry,
DB or evidence change; no network or provider call; no push. `git diff --check`
clean; the surrounding checkpoint records this run's re-verification of the
existing suite (full .NET discovery 1,203 total / 999 passed / 204 skipped /
0 failed; focused policy and diagnostic cases plus the Python diagnostic test
pass) as evidence that the recorded baseline still holds, not as acceptance of
the fundamental candidate.
