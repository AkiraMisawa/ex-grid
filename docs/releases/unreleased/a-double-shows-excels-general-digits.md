Product: ExGrid
Kind: Behaviour
ADR: 0006

A `double` in a column with no `Format` now shows at most 15 significant digits, as Excel's General does, so
`0.1 + 0.2` shows `0.3` rather than `0.30000000000000004`. Sorting and filtering already compared at those 15
digits, so the cell now reads as they do. Only the shown text moves: copy's raw `text/html` carries the value whole,
and the editor, the Formula Bar and Replace keep the whole number, so an edit committed unchanged writes back the same
value. A negative zero shows `0`. A `decimal` is unchanged, and ExSheet,
which writes its own General, is not affected. The Script Core's `number` reads the same way.
