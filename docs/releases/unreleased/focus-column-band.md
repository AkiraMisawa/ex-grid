Product: ExGrid
Kind: Added
Docs: exgrid/selection#which-row-and-column-am-i-on-
ADR: 0008

`highlightFocusColumn` (`HighlightFocusColumn` in Blazor) paints a band down the Focus's column, beneath the
Selection, as `highlightFocusRow` does across its row; with both on they cross as Excel's Focus Cell does. Its colour
is `--ex-focus-column-fill`, which is `--ex-focus-row-fill` unless set, so every Wrapper's palette already covers it.
