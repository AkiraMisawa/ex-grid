# 11: Point mode

Status: ready-for-agent

**What to build:** The fourth editing state. While the Consumer's synchronous predicate says the caret stands where
a Reference can go, arrows and clicks move a pointing outline, painted in the selection overlay,
and the Consumer's Reference text is written at the caret. Shift extends to a range, an operator
ends pointing, and F2 toggles between it and Caret. The capture-phase `keydown` message carries the
editor's text and caret, and the decision is made from them (ADR-0021's note).

**Blocked by:** 03, 09

- [ ] `=` ↓ ↓ writes `A3`; Shift+↓ writes a range; a click writes the clicked cell (ADR-0051)
- [ ] Typing a constant, the arrows still commit and move (ADR-0012)
- [ ] F2 switches between moving the caret and pointing
- [ ] The Selection and the Focus do not move while pointing
- [ ] Pointing works from the Formula Bar
- [ ] Layer 3 on the Server host with a delayed circuit: fast typing never points where the text forbids it

## Comments

2026-09-27, engine half: the Consumer's answers are pure functions over the text and the caret.
`FormulaEntry.PointAt(text, caret)` is the Point predicate: a Reference can go at the caret after
the Formula's `=`, an operator other than `%`, `(` or `,`, outside text in quotes and brackets,
and with nothing after the caret that it would run into; it returns the insertion site.
`FormulaEntry.PointedReferenceAt(text, caret)` returns the span of the Reference ending at the
caret in such a place, which a further arrow key replaces while the component is pointing (only
the component knows it is pointing: typed by hand, `=A1` is not pointed, and `PointAt` says no).
`FormulaEntry.ReferenceText(range)` writes `A3` or `B7:C9` from the top-left
(`FormulaEntryTests`). **What remains is the component's:** the fourth editing state, the
pointing outline, Shift extending, an operator ending it, F2, the Selection and Focus staying
put, pointing from the Formula Bar, and the layer 3 test with a delayed circuit.
