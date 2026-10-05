# Agent Execution Rules V0.1

These repository rules govern execution across milestones. Read them through
[`AGENTS.md`](../AGENTS.md), alongside the existing architecture and
[AI development rules](../.ai/RULES.md). Frozen subsystem contracts govern product
semantics; this document does not replace or reinterpret them. An explicit task
may authorize scope beyond historical phase defaults. If applicable instructions
or contracts conflict, report the conflict before dependent work.

## Repository workflow

- Before work, inspect `git status`, branch, exact HEAD, recent history,
  ahead/behind counts, unstaged/staged diffs and `git diff --check`.
- Report actual state rather than assuming the prompt baseline. Stop if unrelated
  working-tree changes exist; never reset, revert or overwrite user work.
- Identify scope, locate relevant files/symbols and read the smallest sufficient
  context. Expand only when needed; do not recursively ingest the repository or
  generated datasets, raw artifacts, logs, binaries, `bin/` or `obj/` by default.
- Read relevant frozen contracts and runbook sections before substantive changes.
  Historical implementation notices and test totals are not current verification.

## Frozen-contract discipline

- Frozen V0.1 semantics are immutable unless the task explicitly authorizes a new
  version. Future semantic changes must be additive and versioned.
- Implementation convenience never permits reinterpreting policy, chronology,
  identity, missingness, evidence selection, dataset identity or transport rules.
- Stop and report a contract blocker when requested work cannot preserve the
  frozen contract. Do not quietly change schemas or relax safety to make it pass.
- Keep factual documentation corrections separate from semantic redesign and
  obtain the task's authorization before editing a frozen contract.

## Architecture continuity

- Python fetches/parses; .NET owns canonical production logic; PostgreSQL stores
  canonical history, revisions, ledger and snapshots. Local AI remains optional.
- Missing data is UNKNOWN, never zero. Stale values retain their original date;
  corrections append revisions. Never invent bars for suspension or no-trade.
- Preserve the distinction between pre-IPO absence, holiday, no-trade, suspension
  and missing-provider-row. Average buy price is not technical support; a losing
  swing must not silently become INVEST.
- Deterministic decisions precede LLM explanation. No automatic production-rule
  promotion; AI may not invent facts or override hard rules.
- Without explicit task scope, do not build UI, realtime services, broker-flow or
  theme engines, autonomous learning agents, notifications, microservices,
  Redis/Kafka, authentication or an AI recommendation system.

## Point-in-time discipline

- Distinguish economic/effective date, publication time, retrieval time,
  admission/known time and recording time. Follow each contract's cutoff rules.
- Never leak future facts into historical decisions or backdate later-acquired
  evidence into earlier system knowledge, even when its effective date is older.
- Resolve request clocks once at the designated boundary. Do not substitute now
  for retained original cutoffs during replay.
- Recording timestamps are not database commit timestamps. Do not claim exact
  historical MVCC visibility from domain chronology.

## Historical immutability

- Committed prospective captures and terminal Outcomes remain immutable. Do not
  repair old snapshots, anchors or results using newly discovered evidence.
- Preserve retained identities and original chronology. Do not overwrite retained
  verification evidence/history to improve results or introduce verification
  persistence where the contract forbids it.
- New evidence may support a new prospective observation or authorized revision;
  it does not retroactively improve an old immutable artifact.

## Evidence and provenance

- Missing required evidence fails closed using the subsystem's specified
  missingness/failure semantics. Absence is not positive proof unless explicitly
  defined as such by the contract.
- Hashes establish retained identity/integrity, not external truth. Authenticate
  required identity, bytes and provenance; never silently substitute evidence.
- Current mutable references are not automatically historical evidence. Exact
  retained-input replay and as-of revision selection are distinct operations;
  use the relevant contract, never a generic latest-data fallback.
- Do not speculate about provider fields, units, permissions, adjustments or
  revision semantics. Report unsupported or unverified facts explicitly.

## Provider and data-source discipline

- Never silently fall back to another provider. Respect authorized source, scope,
  usage ceilings, terms and retention rights; public visibility does not imply
  automation permission.
- No paid-plan activation without explicit authorization. Do not bypass
  authentication/anti-bot controls or use unsupported scraping. A source-access
  or entitlement blocker is not permission to bypass restrictions.
- Make provider calls, units and cost explicit. No provider/network recovery in
  a read-only or retained-evidence task unless explicitly authorized by its scope.

## Research boundaries

- Use committed Outcomes only; unresolved remains unresolved. Outcome preview
  is not committed analytical truth.
- Verification status is independent of Research membership. Never silently
  filter returns or denominators by a verification verdict.
- No historical backfill with current fundamentals/reference facts. Preserve
  deterministic arithmetic, exact numeric representability and explicit missingness.
- Follow the Research contract for population, metrics, dependence, identity,
  pagination and export; do not duplicate those definitions here.

## Scope control and fix / no-fix gate

- Implement the smallest sufficient change. Reuse existing helpers and native or
  standard-library capabilities before adding code or dependencies.
- No unrelated refactors, speculative abstractions or infrastructure for later.
  Never create code merely to produce an expected commit.
- Before fixing an operational problem, classify its cause where relevant:
  implementation defect; configuration defect; collector/parser defect; missing
  but supported evidence; external source/entitlement blocker; unsupported
  current-version semantics; or corruption/integrity failure.
- Only a cause requiring an internal change justifies an implementation fix.
  Supported missing evidence requires an authorized evidence operation, not
  invented clearance. An external blocker can correctly yield no production change.
- Trace the affected flow and callers before fixing the shared root cause.

## Database and operational safety

- No migration, index or schema change unless explicitly within milestone scope.
  Never edit an applied migration; authorized changes are additive/versioned.
- Do not add persistence or caching merely to simplify transport. Read-only work
  remains read-only; do not mutate Outcomes as a side effect of reads/verification.
- Never fabricate operational market facts, captures or Outcomes. Synthetic
  acceptance fixtures belong in isolated, owned disposable storage with cleanup.
- Operational smoke must be bounded and authorized. Fingerprint protected tables,
  archives and files before/after when operational proof is required. If no safe
  invocation exists, report the skip; do not create an out-of-scope endpoint.
- Do not alter FullIdx gating, soak qualification or provider limits to make
  acceptance pass. Fixtures do not count as real operational evidence.

## Proportional testing

- Production changes require relevant focused checks for important invariants.
  Shared semantic/persistence changes require broader regression and applicable
  disposable database/HTTP acceptance.
- Use standard discovery: root `dotnet test` (`rtk proxy dotnet test`) and the
  Python discovery command in `AGENTS.md`; an executable runner is not a substitute.
  Use existing owned disposable harnesses for database acceptance.
- Documentation-only work needs content/diff review and `git diff --check`, not
  unrelated expensive suites. No-change/external-blocker reviews need sufficient
  diagnostics, not artificial code or fixtures.
- Task-specific acceptance requirements remain binding. Never claim unexecuted
  tests; distinguish passed, failed, skipped and not-applicable checks.

## Commit and reporting discipline

- Commit only justified work when authorized. Review unstaged and staged diffs;
  stage only intended files. Never commit secrets, market datasets, generated
  artifacts or unrelated changes.
- Before committing, inspect `git diff --cached --stat`, the exact staged diff
  and `git diff --cached --check`. Use a milestone-scoped commit message.
- Do not push unless the user explicitly authorizes it. After a commit, inspect
  status, exact HEAD, recent history and ahead/behind counts.
- Report actual observations, blockers and uncertainty. Distinguish retained
  historical reports from fresh checks and disposable proof from operational proof.
- State executed/skipped tests, provider calls/units when relevant, files changed,
  commit identity, working-tree state and ahead/behind counts. Do not claim working
  functionality when required evidence remains unavailable.
- Keep volatile baselines, symbols, counts, routes, allowances, commit messages
  and roadmap order in task prompts/runbook entries, never in global rules.
