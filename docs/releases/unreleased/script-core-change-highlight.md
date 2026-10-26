Product: ExGrid
Kind: Added
ADR: 0068, 0350

ExGrid drawn by the Script Core (`@ex-grid/core`) paints the Change Highlight: `cellChangedAt(row, column)` answers when a value cell's shown value last changed, in epoch milliseconds, and the cell wears `ex-changed` until that time plus `changeHighlightDuration` (one second unless given), taken away in one step by one timer that paints again only the rows whose marks ended.
