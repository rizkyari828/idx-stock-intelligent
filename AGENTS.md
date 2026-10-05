# AGENTS.md — IDX Stock Intelligence

Build the smallest trustworthy personal IDX EOD decision-support system.
Correctness, chronology, reproducibility and low operating cost precede feature count.

## Mandatory reading order

Before substantive work, read:

1. [Execution rules](docs/AGENT_EXECUTION_RULES.md).
2. `README.md`, `docs/ARCHITECTURE.md`, `docs/PHASE0_DATA_SPIKE.md`,
   `docs/LOCAL_AI_INFRA_INTEGRATION.md` and `.ai/RULES.md`, in that order.
3. The task-specific prompt, milestone-relevant frozen contracts and relevant
   sections of `docs/PRODUCT_SLICE_RUNBOOK.md`.

The execution rules govern how work is performed; frozen contracts govern product
meaning. Existing architecture and AI guardrails continue to apply. Historical
phase notes do not veto explicitly authorized milestone scope.

## Required execution discipline

- Inspect actual branch, HEAD, status and diff; do not trust an expected baseline.
- Stop on unrelated dirty changes. Never overwrite, reset or revert user work.
- Preserve frozen semantics; semantic changes require an explicitly authorized
  additive version. Implementation convenience is not authorization.
- Never silently broaden scope or add speculative infrastructure.
- Never fabricate market/evidence facts or backdate acquired knowledge.
- Follow the execution rules for evidence, operational safety, proportional
  testing, diff review, scoped commits and factual reporting.
- Never push unless the user explicitly authorizes it; never commit secrets or
  market datasets.

Canonical .NET discovery: `dotnet test` from the root (`rtk proxy dotnet test`).
Python: `PYTHONPATH=collectors/python/src python3 -m unittest discover -s collectors/python/tests -v`.
Never substitute an executable runner for standard discovery. Offline pilot
procedures: `docs/LOCAL_PILOT_WORKFLOW.md`.
