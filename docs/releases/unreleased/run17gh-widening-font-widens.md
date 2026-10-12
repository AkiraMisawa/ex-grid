Product: ExSheet
Kind: Behaviour
Docs: exsheet/formatting#cell-formats
ADR: 0071

Format Cells' OK, under either Chrome, and `SetCellFormatAsync` now widen a column you have not sized whose numbers show `####` when they set any part of the Font (bold, italic, underline, strikethrough or a colour), as Excel's Font tab does, in the same undo step; before, only a Number Format widened. Code that sets a Font through `SetCellFormatAsync` over numbers will see such columns widen, and a Fill, Borders or an alignment still widen nothing.
