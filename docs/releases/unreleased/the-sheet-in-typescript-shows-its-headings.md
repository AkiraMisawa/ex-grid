Product: ExSheet
Kind: Added
ADR: 0050

`@ex-grid/sheet` shows the Row Headings, the row numbers, beside its rows, and the Column Headings above them, as
the Blazor ExSheet does; `showRowHeadings: false` and `showColumnHeadings: false` leave either off, and the Sheet
still addresses `A1` without them. `pinnedColumnCount` pins leading columns, the Sheet's frozen panes (ADR-0046).
