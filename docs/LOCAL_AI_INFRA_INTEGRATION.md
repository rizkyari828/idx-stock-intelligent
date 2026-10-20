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

Install path: [`scripts/install-ai-infra-mcp-bundle.sh`](../scripts/install-ai-infra-mcp-bundle.sh)
renders an installable bundle (absolute paths) from the path-free template in
[`tools/dsh-mcp-ai-infra/`](../tools/dsh-mcp-ai-infra/), using the documented
`@deepseek-ai/dsh-mcp-client` configuration: `serverName: ai-infra`, `transport:
stdio`, explicit `env.LAI_REPO` (DSH scrubs ambient environment for stdio servers)
and `failOnStartupError: false`, so a broken optional dev tool can never stop the
harness from booting. Installing the rendered bundle into the `desktop` profile is a
GUI/`plugin_manager` action, because that profile is application-managed.

Verified: [`scripts/test_ai_infra_mcp.py`](../scripts/test_ai_infra_mcp.py) covers the
handshake, notification silence, tool inventory, argument mapping, fail-closed
`LAI_REPO`, protocol-error handling for unknown tools, and malformed-line recovery.
A live exchange against the real sibling returns `check` and `route` output.

Not integrated and not claimed: cross-model routing enforcement (the harness model is
chosen per session, not per task), verified result handoff or cache reuse, and DSH
session import into infra telemetry. Those need acceptance of the sibling's
still-unverified handoff; until then the product keeps working with the sibling
absent.
