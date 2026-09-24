# Data Source Matrix

No candidate is approved or selected. UNKNOWN means evidence has not yet been collected; it does not mean failure or zero availability.

| Dataset | Candidate source | Access method | Permission / terms status | Historical depth | Automation | Adjustment semantics | Revision semantics | Known limitations | Spike status |
|---|---|---|---|---|---|---|---|---|---|
| Security master | To research | Unknown | UNKNOWN | Unknown | Unknown | N/A | Unknown | Stable identity and symbol history required | NOT TESTED |
| OHLCV | To research | Unknown | UNKNOWN | Target 2022-present | Unknown | Unknown | Unknown | Units, boards, timestamps, zero-volume semantics unresolved | NOT TESTED |
| IHSG | To research | Unknown | UNKNOWN | Target 2022-present | Unknown | Unknown | Unknown | Calendar alignment unresolved | NOT TESTED |
| Market calendar/status | To research | Unknown | UNKNOWN | Unknown | Unknown | N/A | Unknown | Must distinguish holiday/suspension/no-trade/missing row | NOT TESTED |
| Corporate actions | To research | Unknown | UNKNOWN | Unknown | Unknown | N/A | Unknown | Split/dividend/rights/ticker-change coverage unresolved | NOT TESTED |
| Sector data | To research | Unknown | UNKNOWN | Unknown | Unknown | N/A | Unknown | Classification versioning unresolved | DEFERRED |
| Fundamentals | To research | Unknown | UNKNOWN | Unknown | Unknown | Restatement policy unknown | Unknown | Publication dates required for as-of use | DEFERRED |
| Ownership/free float | To research | Unknown | UNKNOWN | Unknown | Unknown | N/A | Unknown | Publication timing and units unresolved | DEFERRED |
| Foreign flow | To research | Unknown | UNKNOWN | Unknown | Unknown | Unknown | Unknown | Market/board aggregation unresolved | OPTIONAL |
| Broker flow | To research | Unknown | UNKNOWN | Unknown | Unknown | Unknown | Unknown | Outside V0.1 core | DEFERRED |

Change a row to PASS only when the corresponding experiment and evidence are linked from `DATA_SPIKE_RESULT.md`.
