# AI Development Rules

## Quota-saving

Before reading code:

1. identify exact task scope,
2. locate symbols/files first,
3. read only relevant files,
4. expand context only when blocked.

Avoid whole-repo review unless necessary.

Do not feed generated binaries, datasets, logs, or unrelated files into AI context.

## Model routing

Use local Qwen through `local-ai-infra` for:

- boilerplate
- simple parser work
- DTO/schema work
- test scaffolding
- documentation cleanup
- straightforward refactors

Escalate to stronger hosted models for:

- architecture changes
- historical replay correctness
- point-in-time semantics
- corporate-action accounting
- risk-policy design
- data-provider semantics
- concurrency/idempotency/security issues

If local AI is uncertain, stop and escalate.

## Deterministic before AI

LLM may explain or propose.

LLM must not:

- invent market data,
- change hard risk rules,
- modify production strategy automatically,
- turn missing data into a confidence score,
- infer bandar intent without sufficient evidence.

## No scope creep

Phase 0 means data feasibility only.

No dashboard, authentication, Telegram, theme detection, broker intelligence, or generic workflow platform.

## Research discipline

One error is not a lesson.

Learning path:

```text
Observation
-> Candidate
-> Evaluation
-> Walk-forward
-> Shadow
-> Human approval
```

## Explicit routing and learning guardrail

Local Qwen or another low-cost agent is suitable for boilerplate, DTOs, simple parsers, unit-test scaffolding, documentation cleanup, and mechanical refactoring.

Use stronger reasoning for architecture, provider semantics, point-in-time correctness, historical replay, corporate-action treatment, portfolio accounting, concurrency/idempotency, and security.

**ERROR != LESSON.** One outcome is an observation, not an automatically learned rule.

```text
Observation -> Candidate -> Evaluation -> Walk-forward -> Shadow -> Human approval
```

There is no automatic strategy or production-rule promotion.
