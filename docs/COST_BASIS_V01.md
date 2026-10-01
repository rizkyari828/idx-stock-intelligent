# Decision: weighted-average cost — V0.1

Status: accepted, 2026-10-01. Scope: IDR portfolio-management accounting.
This is a position-management policy, not a tax-accounting policy.

For held shares Q and invested cost C, average cost is C/Q. An empty position has
C=0 and average cost null. Use .NET decimal arithmetic; retain precision during
projection and round only display. No FIFO lots or mutable cost cache.

| Event | Position | Cash | Realized P&L |
|---|---|---|---|
| BUY q at p, fees f | Q += q; C += q×p+f | −(q×p+f) | unchanged |
| SELL q at p, fees f | remove cost C×q/Q; Q -= q | +(q×p−f) | +(q×p−f)−removed cost |
| CASH_DEPOSIT amount a, fees f | unchanged | +(a−f) | unchanged |
| CASH_WITHDRAWAL amount a, fees f | unchanged | −(a+f) | unchanged |

Buy fees enter cost basis; sell fees reduce realized proceeds. A partial sale
preserves remaining weighted-average cost. A full close removes the entire
remaining cost exactly (avoids decimal division residue), resets Q/C to zero,
and leaves cumulative realized P&L. Reopening starts a new average cost while
retaining realized P&L. Overselling is rejected. Average buy cost is never
technical support, a recommendation or a reason to change mandate.

Synthetic acceptance: deposit 1,000,000; buy 13 lots at 100 with fees 1,300 →
1,300 shares, invested cost 131,300, average 101, cash 868,700. Sell 300 at 120
with fees 300; correct original buy price to 110 → 1,000 shares, invested cost
111,000, average 111, realized P&L 2,400, cash 891,400. Buy 100 outside-universe
shares at 50 with fees 50 → cash 886,350. Entire result is reproduced from facts.
