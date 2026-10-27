# Personal unproven-session ingestion — finding and options (2026-10-27)

**Status: OPEN — decision required. No code change was kept.** This records why the
unattended daily run cannot yet collect a session that has no independent completion
proof, what was tried, and what it cost. It exists so the next attempt does not repeat
the same dead end.

## The question

Can `scripts/pilot-auto.sh` collect a trading day whose session has no entry in
`pilot/sessions.json`, so the owner needs no manual proof step?

**Answer as of today: no, not without changing three independent layers.** The invariant
"a session must be independently proven before its bars become canonical" is enforced
three times over, and every layer refuses separately.

| # | Layer | Location | Refusal |
|---|---|---|---|
| 1 | Python collector | `collectors/python/src/idx_stock_collector/pilot.py` — `collection_eligibility`, raised at the `collect()` guard | `SESSION_PROOF_REQUIRED` |
| 2 | .NET session policy | `src/IdxStockIntelligence.Application/CompletedSessionPolicy.cs` — `Reason`, called by the worker | `SESSION_PROOF_REQUIRED` |
| 3 | .NET bar validation | `src/IdxStockIntelligence.Application/ScreenerEvidence.cs:110` (reached through `PilotValidation.Validate`) | `SESSION_UNCONFIRMED` — the bar is not accepted |

The worker also skips the eligibility re-check only when `mode == "OFFLINE_REPLAY"`
(`src/IdxStockIntelligence.Worker/Program.cs`), which means replay of retained evidence,
not fresh collection.

## What was tried (and reverted)

A personal opt-in was implemented through layers 1 and 2 and then removed, because layer 3
still rejects the rows, so the change delivered no usable result while weakening two gates
without evidence it would work:

- `collection_eligibility(..., allow_unproven=)` → `ELIGIBLE_UNPROVEN_PERSONAL`, with
  weekends, future dates, cited holidays and conflicting proofs still refused.
- `CompletedSessionPolicy.Reason(..., allowUnproven)` → same label; worker read
  `unproven_personal_sessions` from the batch, wrote `UNPROVEN_SESSION_PERSONAL` into the
  summary warnings, and excluded such runs from soak.
- 1 Python test + 1 .NET test, both passing.

Layer 3 returned `SESSION_UNCONFIRMED` for every row, so `accepted_rows: 0`.

## Observed results and cost

| Run | Outcome | Units | Bars |
|---|---|---|---|
| 1st (Python gate only) | `FAILED` before fetch | 0 | 0 |
| 2nd (gate 1 opened) | fetched 11/11, worker `ArgumentException` | 11 | 0 |
| 3rd (gates 1–2 opened) | `DEGRADED`, 5 fetched / 6 quota-deferred, all rejected | 5 | 0 |
| **Total** | | **16** | **0** |

The third run also hit the local ceiling: 16 units already used, so only 5 of 11 symbols
could be fetched and the rest were deferred as `OFFLINE_OR_QUOTA_DEFERRED`. The day's
allowance is exhausted; a further attempt must wait for the UTC-day reset.

## Options

| Option | What it means | Cost / risk |
|---|---|---|
| **A. Open layer 3 too** | accept `SESSION_UNCONFIRMED` bars under an explicit `UNPROVEN_PERSONAL` quality status; keep identity/OHLC/normalisation checks intact | changes the canonical admission rule at its deepest point; needs design + tests, and cannot be verified today (quota spent) |
| **B. Separate personal store** | leave canonical admission untouched; write unproven rows to a distinct table/status read only by personal surfaces | cleanest separation, but a real design slice (schema, reader, UI switch) |
| **C. Automate proof** | acquire a permitted, automatable per-session completion reference, removing the need for the opt-in | correct long-term; needs a source whose terms allow automation, and may not be free |
| **D. Manual proof** | one `pilot/sessions.json` entry per trading day, as done for 2026-10-23 | safe, ~2 minutes per session, not unattended |

## Recommendation

Prefer **B** over **A**. Weakening the canonical admission path to admit unproven bars
contradicts the reason that path exists, and it would be the third deliberate relaxation
of the same invariant. A separate personal store keeps "what the system can prove" and
"what the owner chose to collect" distinguishable — which is also what makes the
`DEGRADED` label meaningful.

Short term, **D** keeps the pipeline producing data: one cited closing reference per
trading day, exactly as retained for 2026-10-23 in `pilot/sessions.json`.

## Not claimed

- No performance, correctness or security claim about any option; none was implemented.
- The 16 units produced no canonical bars and no soak progress (soak remains 2/10).
- Nothing in this document changes a contract, a gate, or operational data.
