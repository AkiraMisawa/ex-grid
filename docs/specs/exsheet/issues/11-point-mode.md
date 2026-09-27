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

2026-09-27, ExGrid core half: two declarations switch Point on. `ExGrid.PointAt` is a
`Func<string, int, bool>`, the synchronous predicate. `ExGrid.ReferenceText` is a
`Func<SelectionRange, string>`, and is required with `PointAt` (a predicate without it is refused
by name). The capture-phase `keydown` message now carries the editor's `value` and
`selectionStart`, read from the surface that holds DOM focus at the moment the key is forwarded
(for a held key, after the keys before it were typed). This is the only change to `ex-grid.js`
(DC-24, inspected in `ShippedStylesheetTests`). `OnKeyAsync` takes the carried text over as the
uncommitted text and decides from it. An arrow in Overwrite where `PointAt` answers true starts
pointing from the edited cell, and the outline is a one-range `GridSelection` of its own. Further
arrows, and Home/End, move the outline while the text is still what the core wrote. Each move
replaces the Reference the core wrote before, including one in the middle of the text. A click on
a cell points in any editing state and does not commit, Shift+click extends, and the drag extends.
The viewport's `mousedown` default is prevented while pointing is declared, so the editor keeps
DOM focus. Typing anything ends pointing: Point gives way to Overwrite, whose next arrow points
afresh where allowed. F2 moves Point → Caret, and Caret → Point where the predicate answers true
(Overwrite otherwise, as before). The outline is `ex-point` in both overlay layers, and its Focus
is revealed. The Selection and the Focus never move. `CellEditMode.Point` tells a Chrome's editor.
Layer 2 (`PointModeTests`) covers DC-19's `=` ↓ ↓ → `A3`, a click, Shift+click, the drag, F2, the
Formula Bar (mid-text), commit and reveal, and DC-1 without the predicate. It also covers DC-20's
decision rule: the key's own text decides, both ways.

**What remains.** ExSheet wiring: pass `FormulaEntry.PointAt` (as a bool) and `ReferenceText`.
Layer 3: real keys and mouse (DC-19), and DC-20 on the Server host with 150 ms injected. **One
criterion is blocked on a decision.** Shift+arrow extending from the keyboard is implemented in the
core (`OnPointKey` handles `Shift+Arrow*`, tested through `OnKeyAsync`), but the gate never
forwards it. `overwriteKeys` in `ex-grid.js` does not claim `Shift+ArrowDown`, and DC-24 permits no
JS change beyond the text and caret. A real Shift+↓ therefore selects text in the input. The
proposal is a `point` set in the gate (Overwrite's keys plus the four Shift+arrows), told while an
outline stands, recorded as an ADR-0051/0021 note with DC-24 amended. Shift+click extends
today. Also limited: the core cannot set the DOM caret (the same gap as ticket 10), so after a
Reference written in the middle of the text the browser shows the caret at the end. The next
arrow still replaces the right span, but a character typed next lands at the end.

2026-09-27, ExGrid core, second round (ADR-0051's second round, DC-31's C# side): the blocked
criterion is unblocked in the core. While the edit is in Point the gate is told `point`, which
claims Overwrite's keys and the four Shift+arrows, so a real Shift+↓ reaches `OnPointKey` and
extends the outline instead of selecting text. After a Reference is written (by a key, a click
or the drag), the grid tells the listener to place the caret after it (`setCaret`), so a
character typed next lands after the Reference even mid-text. The caret a click points from is
the one the listener reported with the last input, never one inferred; with none reported yet,
a click does not point. Layer 2: `CaretTests`, `PointModeTests` (the gate is now told `point`).
**What remains:** layer 3 with real keys and mouse (DC-19, DC-31: Shift+arrows extend, the
caret after a mid-text Reference), DC-20 on the Server host, and the ExSheet wiring. One gap is
left and returned as a proposal: a caret moved without an input (← / → in Caret, a click in the
text) is not reported, so a click on a cell right after it points from the caret of the last
input or key.
