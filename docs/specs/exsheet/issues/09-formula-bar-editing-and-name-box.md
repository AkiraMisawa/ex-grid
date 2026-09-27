# 09: Editing in the Formula Bar, and the Name Box moves the Focus

Status: done

**What to build:** The Formula Bar becomes the Cell Editor's second surface: one uncommitted text shown in two
places, and a commit from either commits once. The fourth ADR-0050 declaration arrives here: the
Consumer can ask the grid to place the Selection and the Focus. The Name Box uses it, so a user
types `D200` and lands there.

**Blocked by:** 08

- [x] Typing in the bar updates the cell's editor and the other way round; one commit, one Edit Intent (ADR-0051)
- [x] Escape from either cancels once
- [x] The Name Box moves the Selection and the Focus and scrolls to them (ADR-0050, item 4)
- [x] A placement requested under an older Row Sequence Version is dropped (ADR-0011)
- [x] Keys typed in the bar are captured by the root's listener (ADR-0018)

## Comments

The core half, 2026-09-27.

- **The bar is the Cell Editor's second surface.** Its text field carries the editor's
  `ex-editor` class, so the root's existing capture-phase listener gates its keys as it gates the
  editor's: Enter and Tab commit once, Escape cancels once, F2 switches the mode (ADR-0018). No
  JavaScript was added. A press into the bar opens Caret on the opening text when the Focus cell
  edits, and the keyboard stays in the bar. Otherwise the field is read-only. Typing in either
  surface updates the other on that render, under the built-in Chrome and `ExGrid.MudBlazor`'s.
- **The Name Box** is an input in a form, so its Enter is the form's implicit submission.
  `OnNameBoxEntered` (`EventCallback<string>`) hands the typed text to the Consumer. A press into
  the Name Box commits an open edit first, and a Reject holds the editor. Escape abandons what was
  typed there.
- **`ExGrid.PlaceSelectionAsync(SelectionRange range, CellPosition focus, int rowSequenceVersion)`**
  is the fourth ADR-0050 declaration, and returns `Task<bool>`. It commits an open edit, places
  the range with Anchor and Focus on `focus`, turns the page under a pager, reveals the Focus and
  lets the extent be announced once it settles. A request under a version other than the grid's
  is dropped and returns false (ADR-0011). A range outside the grid, or a Focus outside the range,
  throws. `GridSelection.Place` is the pure half.

Layer 1: `PlacementTests`. Layer 2: `FormulaBarEditingTests`, and one test in
`MudGridChromeTests`.

What remains:

- ExSheet's wiring: resolving `D200` into a position is the ExSheet stream's.
- Layer 3 for DC-22: real keys typed in the bar reaching the listener, on both hosts and both
  browsers. One case to watch there. A key held behind a mode change while the bar holds DOM
  focus (F2 then typing within a Server round trip) is typed by the listener into the *first*
  `.ex-editor` in the markup, which is the cell's surface. It lands at that surface's caret, not
  at the bar's. The text stays one text, but the caret position can differ. Fixing that would
  change `ex-grid.js`, which ADR-0051 does not allow, so it needs a decision if layer 3 shows it.
- DC-25 (two grids, one declaring) is layer 3's.


The Chrome seam, 2026-09-27 (ADR-0051 Consequences, ADR-0010/0030). Before this change, the bar's
two fields were painted with built-in markup under every Chrome. Each field is now a seam:

- **`IGridChrome.NameBox(NameBoxContext)`** takes `Text`, `TextChanged`, `Focused` and
  `Blurred`.
- **`IGridChrome.FormulaBarText(FormulaBarTextContext)`** takes `Text`, `ReadOnly`,
  `Focused`, `TextChanged` and `FocusRequest`.

Both are default interface methods that return null, so a Chrome that paints neither field
keeps the built-in inputs unchanged. The core still owns the boxes: `div.ex-name-box` inside
the form whose implicit submission is Enter, and the bar's `div.ex-editor.ex-formula-bar-text`.
Because the bar's box wears `ex-editor`, the capture listener still gates its keys, and no
JavaScript changed. The core also still owns the keys and the meaning of the text.
`ExGrid.MudBlazor` fills both seams with bare inputs (`MudNameBox`, `MudFormulaBarText`),
named through `MudExGridWords.NameBox` and `MudExGridWords.FormulaBar`.

One existing test changed its selector, not its assertions: `MudGridChromeTests`'
`The_formula_bar_and_the_chrome_editor_agree` now types into Mud's control inside the box.
Layer 2: `FormulaBarChromeTests`, plus three tests in `MudGridChromeTests`.
