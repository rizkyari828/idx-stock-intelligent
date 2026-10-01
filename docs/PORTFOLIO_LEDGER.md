# Portfolio ledger contract — V0.1

Ledger facts come from USER or BROKER_RECORD sources. Collection membership and
provider entitlement never determine whether a holding exists. The .NET domain
owns facts; Application owns deterministic projection; Infrastructure owns
parameterized PostgreSQL access; Api exposes product contracts; React renders them.

`portfolio` identifies an IDR account, name, creation/knowledge timestamp and an
explicit `allow_negative_cash` flag (false by default). No balances are stored.
`portfolio_event` is append-only and preserves stable UUID, portfolio UUID,
existing instrument UUID (trades), BUY/SELL/CASH_DEPOSIT/CASH_WITHDRAWAL, trade date,
server recording/knowledge timestamp, ledger sequence, quantity with SHARES unit,
price, fees, cash amount, external reference, source, note and supersedes UUID.
Trades require registered EQUITY identity, positive integral shares, positive
price, nonnegative fees and no standalone cash amount. Cash events carry cash
amount/fees with no instrument, price or quantity. All amounts are IDR; no FX or
short positions. Monetary inputs are decimal, not binary floating point. Amounts
are bounded at 10^12 and quantities at 10^9 shares. Dates span 1900..Jakarta today.

Input LOTS converts explicitly using 100 shares per IDX equity lot: 13 → 1300.
Persistence and accounting always use SHARES. Instrument metadata registration
uses the existing instrument/history tables, including instruments outside the
collector. It may fill UNKNOWN type, but cannot silently change a known type or
rename an established symbol. Existing IDs should be supplied when registering
collector instruments; never create a replacement identity for a known instrument.

## Idempotency and corrections

Within a portfolio, `(source, external_reference)` identifies an import when
present. Event UUID independently identifies a submission. Repeating the same
canonical fact returns the original event with `duplicate=true`, including when
the input unit differs but canonical shares agree. Reusing either identity with
different content fails; a correction needs a fresh UUID and import reference.

Correction is one replacement event with `supersedes` pointing to the current
leaf of the original event's chain in the same portfolio and instrument. The
original remains immutable. Exactly one replacement may supersede an event;
further corrections supersede the replacement. No special reversal accounting
entry is required. All known replacements are resolved before filtering trade
dates; a moved-date correction never resurrects the original. Same-day economic
ordering retains the root event's sequence even when its correction was recorded
after a sale. History API ordering instead reflects recorded ledger order.

Reads independently accept market/trade `through` date and knowledge `cutoff`.
Only events known by cutoff participate; before-correction reads reconstruct the
original. Effective events sort by trade date, root sequence, UUID. Replaying
identical inputs gives identical shares, cost, P&L and cash. A per-portfolio row
lock serializes writes, import retries, correction validation and thesis versions.
Every proposed append replays the entire bounded ledger in the transaction; a
negative intermediate cash balance or oversell rolls back. Explicitly configured
negative cash is returned with `negativeCash=true`.

## Thesis journal

`thesis_version` stores portfolio/instrument UUID, version, stable thesis UUID,
INVEST/FAST_SWING/LONG_SWING, text, server recording/knowledge timestamp,
supersedes, optional invalidation note and recorded active state. Each change or
inactivation appends a new version. Latest version as-of cutoff is authoritative;
old `active=true` means active when recorded, not still current. The API never
changes mandate because prices change. A thesis requires a ledger event for its
instrument and remains queryable after a full close.

## Boundaries

Append-only database triggers protect portfolio configuration, events and theses.
Foreign keys reference existing instrument identity; no mutable position truth
or projection cache exists. Future DIVIDEND/SPLIT/RIGHTS/TRANSFER/FEE_ADJUSTMENT
support requires explicit event/schema/projection changes. No speculative handler
framework is introduced. See [cost policy](COST_BASIS_V01.md),
[data states](PRODUCT_DATA_STATE.md) and [runbook](PRODUCT_SLICE_RUNBOOK.md).
