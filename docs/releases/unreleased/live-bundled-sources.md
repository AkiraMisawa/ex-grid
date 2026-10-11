Product: ExGrid
Kind: Added
Docs: exgrid/data#live-updates-and-the-change-highlight
ADR: 0141

The bundled sources take live data by Row Key. `GridSource.From(rows, rowKey, clock)` applies a `GridChangeBatch` whole, or a whole new list through `ReplaceAll`, gathers changes over 250 ms, and marks the cells whose text changed; `GridSource.Fetch` with a Row Key reads again when told `NotifyChanged()`, and drops a Selection when the server's order token moves (#64).
