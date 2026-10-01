# Product market data state — V0.1

The application contract is provider-agnostic. Ledger and thesis facts work with
no market observation. Canonical prices enrich holdings through read-only queries;
HTTP requests never contact a provider or launch a shell.

`MarketState` independently preserves stable instrument ID, display symbol,
instrument type, original market/session date, retrieved_at, canonical known_at,
requested knowledge cutoff, revision, freshness, completeness, quality,
provenance/source, RAW_CLOSE price basis, nullable price, feature readiness/value
map and explicit unavailable reason. Display symbol uses existing symbol history
as effective on `through`, falling back to issuer name if no history exists.
Symbol master currently has effective dates but no knowledge timestamps; display
labels are current reference metadata, not strict point-in-time identity evidence.
Stable ID remains canonical.

| Dimension | States / meaning |
|---|---|
| Operation | SUCCESS / FAILED: request execution only |
| Freshness | CURRENT if source market date equals `through`; otherwise STALE; absent UNKNOWN |
| Completeness | COMPLETE / PARTIAL / UNKNOWN; single latest observation does not establish series completeness |
| Quality | VERIFIED (canonical VALID), DEGRADED (canonical DEGRADED/STALE), REJECTED, UNKNOWN |
| Feature availability | AVAILABLE / WARMUP / UNAVAILABLE, nullable decimal and unavailable reason per feature |
| Valuation availability | AVAILABLE / UNAVAILABLE, reason when absent |
| Portfolio coverage | COMPLETE only when every open holding is priced; otherwise PARTIAL |

These dimensions never imply one another. A successful request can return
DEGRADED, UNKNOWN or WARMUP data. VERIFIED is canonical validation status, not
an independent rights approval. CURRENT is date equality, not trading-session
inference. Age is explicitly calendar days; stale prices retain their date and
may produce visibly qualified valuations. Rejected/unknown quality is not valued.
Market queries choose the latest session/revision with session <= through,
known_at <= cutoff and retrieval <= cutoff; no fallback to a different provider.

An unpriced holding keeps its known quantity and cost. Valuation fields are null
with `NO_CURRENT_MARKET_PRICE`. `pricedMarketValue` is explicitly a subtotal;
`totalMarketValue` and `totalEquity` are null under PARTIAL coverage. Cash remains
known. Closed positions don't enter open-holding valuation coverage.

Existing PilotFeatures implements ATR14, EMA20/50, prior ranges, volume ratio
and relative performance. This portfolio slice does not project technical
features; it returns UNAVAILABLE / NOT_PROJECTED_IN_PORTFOLIO_SLICE per feature.
This must not be confused with actual warm-up. ATR% is not exposed and liquidity
is not implemented; neither is needed for accounting. No screener is built.
A future screener envelope must add universe ID/version and coverage denominator
alongside the same market date, cutoff, per-feature availability and quality.
No composite score or action recommendation belongs in these contracts.

## Calendar closure

Default Saturday/Sunday is deterministic Weekend/CLOSED_BY_CALENDAR. Independent,
already-known explicit observed trading overrides the weekend default and still
requires the usual completion gates. Announced/exceptional closures require
source evidence. Unproven weekdays remain UNKNOWN; no weekday-open assumption.
Conflicting or not-yet-known evidence blocks collection, including on weekends.
Python H+1 skips known closures; .NET admission/features honor the same override.
Both languages read `tests/fixtures/calendar-policy.json`; planner tests also
exercise Friday → Monday and explicit Saturday opening. This change does not
advance the operational soak or alter provider allowance.
