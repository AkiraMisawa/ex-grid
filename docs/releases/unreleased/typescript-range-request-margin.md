Product: ExGrid
Kind: Added
ADR: 0183

`@ex-grid/core`'s grid takes `rangeRequestMargin`, the Range Request Margin: at M rows it calls its source's `needRange` for the rows on screen as soon as those rows and M rows either side are not all in the Window, so a source of your own that reads ahead is asked before the Viewport runs out of rows. It is 0 unless given, as in Blazor. It is published with 0.3.0.
