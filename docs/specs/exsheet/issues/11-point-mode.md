# 11: Point mode

Status: done

**What to build:** The fourth editing state. While the Consumer's synchronous predicate says the caret stands where
a Reference can go, arrows and clicks move a pointing outline, painted in the selection overlay,
and the Consumer's Reference text is written at the caret. Shift extends to a range, an operator
ends pointing, and F2 toggles between it and Caret. The capture-phase `keydown` message carries the
editor's text and caret, and the decision is made from them (ADR-0021's note).

**Blocked by:** 03, 09

- [x] `=` ↓ ↓ writes `A3`; Shift+↓ writes a range; a click writes the clicked cell (ADR-0051)
- [x] Typing a constant, the arrows still commit and move (ADR-0012)
- [x] F2 switches between moving the caret and pointing
- [x] The Selection and the Focus do not move while pointing
- [x] Pointing works from the Formula Bar
- [x] Layer 3 on the Server host with a delayed circuit: fast typing never points where the text forbids it

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

2026-09-27, ExSheet wiring: ExSheet passes `PointAt`, which is `FormulaEntry.PointAt(text, caret)
is not null`, and `ReferenceText`, which is `FormulaEntry.ReferenceText` of the pointed
`SelectionRange` read as a `CellRange` (`SheetFormulaAids`). Both are static delegates held in
fields. Layer 2, in `FormulaEntryWiringTests`: `=` ↓ ↓ writes `A3` while the Name Box stays on
`A1`, and Enter commits `=A3`. Shift+→ Shift+↓ write `B2:C3`. A click on C4 writes `C4`. F2
twice returns to pointing. The Formula Bar points (`=SUM(B1)`). A constant, and `=1+2` after an
operand, commit and move. **Still open.** The first criterion's Shift+↓ reaches the core only
through `OnKeyAsync`: the gate's `point` set (ADR-0051, second round) is the core's to add, and
until it lands a real Shift+↓ selects text in the input. Layer 3 with a delayed circuit (DC-20)
is also open.

2026-09-27, ExGrid core, third round (ADR-0051's last second-round bullet, ADR-0021's newest
note, DC-24/DC-31): the gap above is closed. The editor listener now also reports the caret
position on `selectionchange` while DOM focus is in an `.ex-editor` surface inside this
instance's root and reporting is on. The event fires on the document only, so the handler checks
the instance's root, is removed (and its pending frame cancelled) on dispose, sends at most one
report per animation frame, and never repeats the last report sent; a caret the core placed is
not reported back. `OnEditorCaretAsync` takes a caret-only report (text unchanged) as the new
caret position: completion is asked again at it, and a click or arrow points from it. A caret
moved away from a pointed Reference ends the outline, as typing does (Point gives way to
Overwrite). While a placement the core asked for is in flight, a report of the browser's other
caret in that text is not taken. Opening an edit — the cell editor by a key or F2, or the
Formula Bar — places the caret at the end of the opening text with `setCaret`, after the render
that shows it and once the surface has DOM focus. Declarations off: nothing is placed or
reported (DC-1). Layer 2: `CaretTests` (opening placement in all three ways and undeclared,
caret-only moves for completion and for a click, the outline ending, a report crossing a
placement, no render for a report with nothing to answer) and `ShippedStylesheetTests` (the
listener's shape: scoped, disposed, coalesced, deduplicated, no layout read).
**What remains:** layer 3 with real keys and mouse — ← / → in Caret then a click on a cell, a
click inside the text then a click on a cell, a click into the Formula Bar's text, the caret
after opening — under both Chromes and on the Server host.

2026-09-27, layer 3 (ticket 18), run locally under xvfb with Playwright's Chromium (build 1194; this machine has neither Google Chrome nor Edge, so the committed config's `chrome` and `msedge` projects are CI's to run), against the WebAssembly host and the Server host behind the latency proxy. `declarations.spec.mjs`: `=` ↓ ↓ writes `=F4` with
the outline over F4 and the Focus still on F2; real Shift+↓ and Shift+→ extend it to `=F4:G5` and
the input selects no text; an operator ends pointing; a click writes `=C3`, Shift+click `=C3:C4`;
F2 from pointing gives Caret, whose arrows move the caret; a Reference written mid-text lands at
the caret with the caret after it; from the Formula Bar F2 points, and a press in the bar's text
ends pointing (DC-19/31/34). DC-20 at 150 ms on the Server host: `=1` ↓ commits and moves, `=1+` ↓
points. Green on both hosts under the built-in Chrome. Under `ExGrid.MudBlazor`'s Chrome the
pointing-from-the-bar test is left failing by name (`test.fail`): a press into the bar leaves DOM
focus in the Mud Cell Editor, not the bar (see ticket 18).
