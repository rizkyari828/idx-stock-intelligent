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
