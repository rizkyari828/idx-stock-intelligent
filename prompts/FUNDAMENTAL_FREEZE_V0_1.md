# Coding Agent Prompt — freeze Fundamental Evidence Binding V0.1

```text
Cheapest sufficient (use this):
DeepSeek
Model: DeepSeek Flash

Escalate only on a concrete failure:
Codex
Model: Sol
Thinking: Medium
```

Why Flash is enough here: the contract is already reviewed and approved, every edit
is mechanical (`git mv`, status header, freeze record, six reference updates), the
acceptance below is machine-checkable, and the change is a local commit with no push
— so a bad run costs one `git revert` or one re-run, not a corrupted contract.

- **Flash** is the default: cheapest, and the failure mode is visible in the diff.
- **Pro / Sol Medium** only if Flash's diff touches a normative line, misses a
  reference, or the run finds a semantic conflict. Re-run with the higher tier; do
  not hand-patch the bad attempt.
- **Astra / Sol High** are not justified for this task at any point.
- Do not loop: one pass, one commit, then verify. No re-reads of unrelated docs, no
  web research, no second opinion request.

Report the model and effort actually used. If the runtime does not expose them, write
`UNKNOWN` — never infer or upgrade the label.

Freeze the already-approved candidate. Do not redesign, implement, deploy or admit
anything.

## Read first

- `AGENTS.md`
- `docs/AGENT_EXECUTION_RULES.md`
- `docs/FUNDAMENTAL_FREEZE_DECISION_PACKAGE_V0_1.md` (§§2–8 and the §9 record)
- `docs/FUNDAMENTAL_EVIDENCE_BINDING_V0_1_PROPOSAL.md` (§§18–25 are the text being
  frozen; §§1–17 are historical rationale)
- `docs/SCREENER_EVIDENCE_V0_2_CONTRACT.md` (frozen neighbour; read, do not
  reinterpret)

## Authorization already recorded

```text
P1 statement-only scope:  APPROVED
P2 FCF boundary:          APPROVED
P3 exact contract bundle: APPROVED
P4 freeze:                APPROVED (this prompt is its execution)
P5 build E1–E5:           NOT AUTHORIZED
```

If the §9 record, the candidate file or the working tree disagree with this block,
stop and report the mismatch. Do not reconcile it by editing the record.

## Task

1. Inspect actual state: branch, HEAD, status, diff, ahead/behind. Expected clean
   tree at or after `03829f2`, nothing pushed.
2. Promote the candidate with `git mv`:
   `docs/FUNDAMENTAL_EVIDENCE_BINDING_V0_1_PROPOSAL.md` →
   `docs/FUNDAMENTAL_EVIDENCE_BINDING_V0_1_CONTRACT.md`.
3. Rewrite only the status block at the top:
   - `FROZEN` with the date, policy identity `fundamental-evidence-v0.1.0`, and the
     statement-only scope;
   - the decision reference (package §9, recorded 2026-10-20 WIB) and the commit
     frozen from;
   - which sections are normative (§§18–22 plus the accepted D01–D03, D06, D09
     dispositions), which are rationale (§§1–17), which are non-normative (§23
     examples), and which are review record (§§24–25).
4. Add one short `Freeze record` section stating:
   - frozen: the exact schemas, keys, numeric domain, period/PIT rules, admission
     behaviour and logical storage boundaries;
   - explicitly not frozen: typed corporate-action events (D04 scope limit), FCF and
     other formulas (D07), classification/valuation policy (D08), physical DDL,
     `0012` numbering and deployment (D09), source admission (F13);
   - amendment rule: additive version only, no reinterpretation of frozen V0.1/V0.2
     or of this text by implementation convenience.
5. Update every reference so none points at a `PROPOSAL — NOT FROZEN` file:
   `docs/PERSONAL_FIRST_MASTER_V1.md` (D/E rows and the E handoff opening),
   `docs/FUNDAMENTAL_SOURCE_FEASIBILITY_GATE_V0_1.md`,
   `docs/SOURCE_USAGE_POLICY_AND_CAPABILITY_GATING_V0_1_PROPOSAL.md`, and the
   decision package's status block.
6. Change no normative word. If you find a semantic contradiction, dead reference or
   missing decision, stop and report it instead of fixing it quietly.

## Boundaries

- Documentation only: no `src/`, `tests/`, `collectors/`, `scripts/`, migration,
  registry, DB, evidence, provider, network, subscription or contact.
- Do not freeze D07/D08, do not authorize `0012`, do not start E1, do not admit a
  source, do not claim an independent review that did not happen.
- No push. One focused commit: `docs: freeze fundamental evidence binding v0.1`.

## Acceptance

- `grep -rn "FUNDAMENTAL_EVIDENCE_BINDING_V0_1_PROPOSAL" .` returns nothing.
- No `PROPOSAL — NOT FROZEN` text remains attached to the frozen file or to any link
  that resolves to it.
- Proof that the normative body is untouched: paste `git diff -M --stat` and
  `git diff -M -U0 | grep '^@@'`. Every hunk must sit in the status header (top of
  file), the new freeze-record section, or a reference line in the four documents.
  **Any hunk inside §§18–22 is a failed run** — stop, do not patch it.
- `git diff --check` clean; no non-documentation file touched.
- Runtime suites are not required for a documentation-only change: say so explicitly
  rather than claiming tests passed.

## Report

Give the final HEAD, ahead/behind, worktree state, files moved and edited, the exact
one-sentence frozen scope, confirmation that P5 was not executed, and any gap you
refused to fix.
