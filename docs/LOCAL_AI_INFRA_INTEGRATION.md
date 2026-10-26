# Reusing `local-ai-infra`

## Recommendation

Yes, reuse the existing `local-ai-infra`.

Do not fork it for this project and do not move stock-specific code into it.

Keep two repositories:

```text
local-ai-infra/
idx-stock-intelligence/
```

## Responsibilities

### `local-ai-infra`

Reusable AI capabilities:

- Ollama/Qwen provider runtime
- bounded tool execution
- observability/tracing
- escalation mechanisms
- reusable agent/runtime contracts

### `idx-stock-intelligence`

Stock-domain application:

- market-data ingestion
- canonical market history
- instrument identity
- portfolio ledger
- screener rules
- FAST_SWING / LONG_SWING / INVEST policies
- portfolio maintenance
- historical replay
- outcome evaluation
- learning candidates

## Integration stages

### Phase 0
No AI dependency.

Prove data collection first.

### Phase 1
Use a narrow adapter:

```text
Stock Engine
   |
   | EvidenceBundle
   v
Local AI Adapter
   |
   v
local-ai-infra
   |
   v
Qwen/Ollama
```

AI may explain a decision already produced by deterministic logic.

### Later
AI may draft research hypotheses from historical outcomes.

It must never directly modify production strategy.

## Rule

`local-ai-infra` may propose.  
`idx-stock-intelligence` decides.

A later location-independent adapter may discover the external sibling through an uncommitted `LAI_REPO` setting and exchange an `EvidenceBundle`. No absolute developer path belongs in source or canonical data. There are no cross-repository source imports.

The integration must degrade safely: if the AI runtime is absent, slow, or wrong, canonical ingestion and deterministic behavior continue unchanged.

## Dev-time agent integration (V0.1)

The product adapter above stays deferred. A smaller, separate integration exists for
development-time tooling: [`scripts/ai-infra`](../scripts/ai-infra), a **read-only
facade** over the sibling CLI. It is not a product dependency; the application never
calls it.

- Discovery uses the uncommitted `LAI_REPO` setting (absolute path). Without it every
  command fails closed with exit status 2.
- Exposed: `check`, `route --task`, `plan --objective`, `report`, `span`. These are
  deterministic and offline, except `span`, which appends one sanitized span to the
  sibling's own telemetry store.
- Refused on purpose: `orchestrate run`, `agent-run`, model pulls, bootstrap and
  profile apply. This repository must never launch inference workers or mutate
  shared infrastructure. Unknown commands exit 64 before any sibling process starts.
- Verified by [`scripts/test_ai_infra_adapter.py`](../scripts/test_ai_infra_adapter.py):
  fail-closed discovery, unchanged argument passthrough, and proof that refused
  commands never reach the sibling.

### MCP bridge (stdio)

[`scripts/ai_infra_mcp.py`](../scripts/ai_infra_mcp.py) exposes the same facade over
newline-delimited JSON-RPC (MCP stdio), standard library only, with five tools:
`ai_infra_check`, `ai_infra_route`, `ai_infra_plan`, `ai_infra_report`,
`ai_infra_span`. It maps typed arguments to facade argv lists — never a shell string
— so the facade keeps sole ownership of refusals. `ai_infra_plan` defaults to
read-only; no tool can launch a model or reach `orchestrate run`/`agent-run`.

A sixth tool, `ai_infra_dispatch`, routes one objective through the sibling
orchestrator for **this repository only** (`--workspace` is the repository root,
`--max-ai-calls` defaults to 1). It is **dry-run unless the human sets
`AI_INFRA_ALLOW_MODEL_LAUNCH=1`** in the server environment; only then does it pass
`--confirm`. An agent cannot spend tokens on its own, and `--read-only` is opt-in
because a mutating objective is genuinely invalid under a read-only plan.

`--backend` selects `auto|fake|opencode|codex`. `fake` is confirmed without the human
flag because it launches no model, so the entire orchestration path can be exercised
for free. State defaults to `logs/ai-infra-state` inside this repository (already
ignored): the orchestration store, result cache and evidence DB stay out of git and
inside the workspace sandbox, while the sibling keeps owning their schemas.

Verified end to end on 2026-10-26 with `--backend fake`, through both the facade and
the live MCP tool: run 1 planned one `BALANCED` node, executed it as `SIMULATED`, and
wrote the cache; run 2 reported `CACHE reuse from <run>/T1`, `executed 0 real AI
calls`, `used 0` budget. That proves routing, dispatch, result handoff and cache reuse
without a paid call. A changed MCP server script needs the server process restarted;
the client reconnects automatically and respawns it.

Confirmed-run preflight: before a confirmed non-fake dispatch the facade reads the
configured localhost `baseURL` (opencode config or `OPENCODE_CONFIG`) and refuses to
start when that port is closed, so a dead proxy fails in a second with the exact
endpoint instead of retrying silently for minutes. `AI_INFRA_SKIP_PREFLIGHT=1`
bypasses it; dry runs never preflight.

Remaining training gap: the routed student model is `ollama/qwen3.6:27b-coding`, and
this machine reports `local_inference: NOT_CONFIGURED_BY_MACHINE_POLICY` with
`provider_availability.ollama: false`. A real (non-fake) run therefore still stops at
`NOT_CONFIGURED_BY_MACHINE_POLICY` until Ollama is installed or the role mapping is
changed in the sibling. Handoff/cache themselves are no longer unverified; they are
exercised end to end by the fake backend.

Install path: [`scripts/install-ai-infra-mcp-bundle.sh`](../scripts/install-ai-infra-mcp-bundle.sh)
renders an installable bundle (absolute paths) from the path-free template in
[`tools/dsh-mcp-ai-infra/`](../tools/dsh-mcp-ai-infra/), using the documented
`@deepseek-ai/dsh-mcp-client` configuration: `serverName: ai-infra`, `transport:
stdio`, explicit `env.LAI_REPO` (DSH scrubs ambient environment for stdio servers)
and `failOnStartupError: false`, so a broken optional dev tool can never stop the
harness from booting. Installing the rendered bundle into the `desktop` profile is
one command (verified 2026-10-26 on DSH 0.2.1-rc.3):

```bash
dsh plugin --profile desktop add "$HOME/.dsh/local-bundles/ai-infra-mcp"
```

That writes the profile dependency and appends the bundle; the tools appear as
`mcp__ai-infra__*` once the app picks the profile up (a running GUI may need a
reload). `--dump-config` still refuses the app-managed `desktop` profile, so list the
tools from a session instead.

Verified: [`scripts/test_ai_infra_mcp.py`](../scripts/test_ai_infra_mcp.py) covers the
handshake, notification silence, tool inventory, argument mapping, fail-closed
`LAI_REPO`, protocol-error handling for unknown tools, and malformed-line recovery.
Live calls against the sibling (2026-10-26, sibling `6a5ef44`) returned `check`,
`route` and a dry-run `dispatch` result from inside a DSH session.

Not integrated and not claimed: cross-model routing enforcement (the harness model is
chosen per session, not per task), verified result handoff or cache reuse, and DSH
session import into infra telemetry. Those need acceptance of the sibling's
still-unverified handoff; until then the product keeps working with the sibling
absent.
