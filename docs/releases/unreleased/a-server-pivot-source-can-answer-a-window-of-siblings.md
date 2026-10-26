Product: ExPivot
Kind: Added
ADR: 0370

A row field's `PivotLevelRule` may carry an `Order` (`PivotLevelOrder`), and `PivotLevels` a `Window` (`PivotLevelWindow(limit, starts)`): a sibling set ordered by a value with more than `limit` siblings is then answered as a window, the siblings asked for with those tied at either edge, a margin each side, and a rest cell a column group for the siblings beyond each margin, built with `PivotAnswerBuilder.AddWindow` and `AddRest`. A window not where it was asked, a set past the limit answered whole, or a side without its rest cell is refused by name, and `PivotJson` carries the question's order and window and the answer's `windows` and `rests`. A set sorted by its labels is never windowed. The bundled sources answer windows, and `@ex-grid/pivot-engine` has the same, to the last bit; its cube refuses an answer with windows by name until it lays one out.
