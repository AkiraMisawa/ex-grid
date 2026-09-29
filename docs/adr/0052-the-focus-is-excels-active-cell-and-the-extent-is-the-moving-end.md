# The Focus is Excel's active cell; the Extent is the end that moves

*(Decided with the user, 2026-09-27, after the first Windows run put ExSheet beside a real Excel —
`verification/2026-09-27-windows-excel/behaviours.md`. It changes
[ADR-0012](./0012-anchor-focus-and-keyboard-navigation.md), for ExGrid as a whole and not only for
ExSheet.)*

ADR-0012 made the **Focus** the *moving* end of range extension and the **Anchor** the fixed end.
Typing, the Cell Editor, and later the Name Box and the Formula Bar
([ADR-0051](./0051-formula-entry-completion-point-mode-and-the-formula-bar.md)) all act on the
Focus. Excel does it the other way round. After Shift+arrow or Shift+click, Excel's **active
cell**, which typing enters, the Name Box names and the Formula Bar edits, **stays at the fixed
end**. Only the far corner moves. Beside Excel, more than half of the disagreements the behaviours
run found came from this one difference. It also exists in ExGrid: select a range with Shift+↓,
type, and ExGrid writes to a different cell than Excel would.

## Decision

**ExGrid takes Excel's model.**

- **The Focus is Excel's active cell.** Typing enters it, the Cell Editor opens on it, the Name Box
  names it, and the Formula Bar edits it. It is the cell `aria-activedescendant` points at
  ([ADR-0033](./0033-the-accessibility-surface-is-owned-by-the-root-not-by-cells.md)), and
  Enter/Tab cycling moves it within the Selection without changing the Selection.
- **The Extent is the end that moves** when a range is extended: by Shift+arrow, Shift+click,
  Ctrl+Shift+arrow, or a drag. While extending, the grid keeps the Extent in view, as Excel does.
- **The Anchor is retired as a term.** The end that stays fixed while extending is the Focus.

Both were on the table: this model for ExGrid as a whole, or Excel's model for ExSheet alone, by a
declaration. The first was chosen. "Excel-like operability" is the product's claim, ExGrid is not
yet released, and two models side by side would split every later decision about keys, selection
and editing into two.

## The rules, as Excel showed them

*(Settled with the user, 2026-09-27, from the second Windows run's Part B,
`verification/2026-09-27-windows-excel-2/active-cell.md`, where every gesture was a real key or
mouse input to Excel 16.0.20326. The first draft of this ADR left these open, and kept ADR-0012's
behaviour until they were observed. Each rule names the case it comes from. "After pressing Enter,
move selection" was on, direction Down, which is Excel's default and the only setting ExGrid
has.)*

**Extending.**

- **Shift+arrow, Shift+click, Ctrl+Shift+arrow, Shift+Home/End, Shift+PageUp/PageDown and a drag
  move the Extent. The Focus does not move** (cases 1, 2, 11). A drag's Focus is where the button
  went down, whichever way it then moves: D5 → B2 leaves D5 active.
- **The grid keeps the Extent in view while extending, not the Focus** (cases 1, 11). The view
  scrolls as soon as the Extent reaches its edge, and Shift+PageDown moves the view a page with the
  Extent, leaving the Focus off screen. A move back inside the view scrolls nothing. *(Not on an
  axis the range spans end to end: see "What the user's run settled", 2026-09-29.)*
- **After Enter or Tab has moved the Focus inside a range, Shift+arrow moves the edge opposite the
  Focus** (case 3). With A1:C3 and A3 active, Shift+→ moves the right edge (A1:D3) and Shift+↓ the
  top edge (A2:D3). With B1 active, Shift+↑ moves the bottom edge. **On an axis where the Focus is on
  neither edge, the key changes nothing**: with B2 active in A1:C3, Shift+← leaves A1:C3. The Extent
  is therefore stored, and recomputed from the Focus when cycling moves it, never inferred from the
  Selection alone.
- **With several ranges, the one holding the Focus extends, from its own corner** (case 5). Ctrl+click
  D5 onto A1:B2, then Shift+↓, gives A1:B2,D5:D6. ADR-0012's re-anchoring rule ("a Shift+arrow from
  a range the Anchor is not in starts a new range") is withdrawn: the Focus is always in the range
  that extends. *(Observed only for the range added last. After Enter has cycled the Focus into an
  earlier range, "the range holding the Focus" is this ADR's reading, not an observation.)*

**Moving the Focus without changing the Selection.**

- **Enter, Tab, Shift+Enter and Shift+Tab cycle as ADR-0012 says**: Enter down the columns, Tab
  across the rows, both wrapping to the first cell after the last, the Shift forms backwards (case
  4). Starting from C3 (C3:A1 selected from C3), Enter wraps to A1.
- **Ctrl+. (period) moves the Focus to the next corner of the range holding it, clockwise**
  (top-left, top-right, bottom-right, bottom-left), and only of that range (case 8). New to ExGrid.
- **Ctrl+Backspace scrolls the Focus into view and changes nothing else** (case 7). New to ExGrid.
- **Shift+Backspace collapses the Selection to the Focus** (case 7). New to ExGrid.

**Selecting a region.**

- **Ctrl+Space and Shift+Space select the whole columns or rows of the range holding the Focus, and
  the Focus does not move** (case 9), as ADR-0012 already says.
- **Ctrl+A does not move the Focus** (case 10), as SL-16 already says. Excel's first Ctrl+A inside
  data selects the current region and the second the whole sheet. ExGrid's Ctrl+A stays "select
  all", which for a display grid is the same thing. Whether ExSheet takes the current region is a
  separate question for ADR-0050, not this one.
- **A Heading click puts the Focus on the first visible row or column, the corner on the top-left
  visible cell, and a fill leaves it on the source's first cell** (the first run, recorded above).

**Ctrl+click on a cell already selected** (case 6).

- **The cell is subtracted, as ADR-0012 says, and the Focus stays inside the Selection.** Taking
  B2 out of A1:C3 (A1 active) leaves A3:C3, C2, A2 and A1:C1, with A1 still active. Shift+↓ then
  extends the fragment holding the Focus (A1:C1 to A1:C2).
- **Taking out the Focus's own cell moves the Focus to the next cell of what remains, in Tab
  order**: A1 out of A1:C3 leaves A2:C3 and B1:C1, with B1 active, and Shift+↓ extends B1:C1.
  *(Observed for the top-left cell only. Rewritten after the third run, below: "What the third run
  settled".)*
- **The only selected cell cannot be taken out**: Ctrl+click on B2 alone leaves B2 selected.
- **ADR-0012's detached state is withdrawn.** It existed because the Focus could stand on a
  deselected cell. Under this model it never does.

**Entering and clearing** (cases 12–14).

- **Typing enters the Focus only**, and Enter then cycles inside the Selection (case 12).
- **Ctrl+Enter fills every cell of the Selection**, and leaves the Selection and the Focus where they
  were (case 13).
- **Delete clears every cell of the Selection**, and leaves the Selection and the Focus (case 14).

**After a structural change** (case 15).

- **A paste that spills from one cell selects the pasted block with the Focus on its first cell, and
  does not scroll** to show the block (DC-8's "Anchor at its top-left" becomes the Focus).
- **Rows inserted over a selected range leave the Selection on the same addresses, and the Focus
  where it was in it** (B3:C4 selected from C4 keeps C4 active).

**The Name Box during a gesture** (cases 1 and 2). While a drag's button is down, Excel's Name Box
shows the size of the range, such as `4R x 3C`, and names the Focus again on release. It does the
same while Shift is held during keyboard extension (`3R x 2C`). **ExGrid takes the drag.** It already
holds the button's state ([ADR-0021](./0021-javascript-is-allowlisted-not-minimised.md)'s
capture-phase `mousedown`/`mouseup`). **It does not take the held Shift**, because telling when Shift
is released needs a `keyup` listener, which ADR-0021 does not allow, and the size would otherwise
stay on screen after the key is up. The size is offered to the Consumer through the Name Box's
label ([ADR-0051](./0051-formula-entry-completion-point-mode-and-the-formula-bar.md)).

What stays as it was: the cycling order through disjoint ranges (creation order, ADR-0012, still
not checked against Excel), and Ctrl+arrow's edges (ADR-0012, or the Consumer's edge answer under
ADR-0050).

## Consequences

- `CONTEXT.md`'s **Focus** is redefined, **Extent** is added, and **Anchor** is retired.
- Every criterion naming the Anchor (KB-*, SL-*, SR-2a, and DC-2/3/8/11/27) is rewritten when the
  implementation lands, in the same change as ADR-0012's text. They are not rewritten before,
  because the criteria describe what the code does today.
- Three keys are new to ExGrid (Ctrl+., Ctrl+Backspace, Shift+Backspace). None is one the browser
  uses, and none is taken from a cell editor: the capture-phase listener leaves them to an open
  editor, as it does every key it does not own while editing
  ([ADR-0010](./0010-chrome-seams-column-menu-editor-loading.md)).

## What the implementation settled *(2026-09-27)*

Readings the observation did not reach, taken as the closest to it, and asked of Excel in the
third Windows run (`docs/specs/exsheet/verify-on-windows-3.md`, Part B):

- **On an axis where the Focus is on neither edge, every extension is a no-op**, not only
  Shift+arrow: Ctrl+Shift+arrow, Shift+Home/End and Shift+PageUp/PageDown too. So after Ctrl+Space
  from C3, Shift+↓ does nothing, since C3 is inside a whole column.
- **Ctrl+. from a Focus that is not on a corner** goes to the corner that ends the Focus's edge
  going clockwise, or to the top-left from inside the range. A corner shared in a one-row or
  one-column range is skipped.
- **Taking out the Focus's own cell** moves the Focus to its Tab successor across ranges,
  wrapping. The fragments keep the order `SelectionRange.Subtract` gives, which is not Excel's
  listing, and that order is what cycling visits.
- **After a fill the Focus stays where it was inside the source.** That agrees with "the source's
  first cell" wherever Excel was observed. A source selected bottom-up keeps its bottom Focus.
- **The drag's size reaches the Consumer through `NameBoxSizeLabel`**, a second parameter beside
  ADR-0051's `NameBoxLabel`.

## What the third run settled *(2026-09-28, verification/2026-09-28-windows-excel-3/active-cell.md; decided with the user)*

Part B asked each reading above of Excel.

- **Agreed with the readings**: after Ctrl+Space from C3, Shift+↓ changes nothing and Shift+→ gives
  C:D; Ctrl+. from A2 (on the left edge) or B2 (inside) goes to A1 first, then clockwise; B4:B2 made
  from B4 and filled down by the handle keeps B4 active; and after Enter has cycled into an earlier
  range, Shift+↓ extends the range holding the Focus from the Focus, so "the range holding the
  Focus" is now observed, not read.
- **The cycling order through disjoint ranges** is creation order, as ADR-0012 said, now observed:
  Enter down each range's columns, Tab along its rows, then the next range in the order they were
  made, wrapping; Shift+Enter back the same way. **Tab after Enter continues from the cell Enter
  reached**, by rows: E4 in D4:E5, Tab, is D5.
- **A take-out resets the Focus, replacing the Tab-successor reading.** After any Ctrl+click that
  takes a cell out, **the Focus goes to the first remaining cell, by rows, of the range made last**,
  wherever cycling had moved it: B2, C3, C1 or A3 taken out of A1:C3 leaves A1 active, and A1 taken
  out leaves B1; with A1:B2 then D4:E5, taking A1 or B2 out leaves D4 active, and so does taking E5
  out after Enter had cycled the Focus to A1.
- **The fragments are listed bottom to top**, as Excel's `Selection.Address` lists them: B2 out of
  A1:C3 leaves A3:C3, C2, A2, A1:C1, in that order, where `SelectionRange.Subtract`'s own order was
  kept before. **Enter visits the fragment holding the Focus first, then the others in that order**,
  wrapping.
- **Read, not observed**: when a take-out removes the whole range made last (A1:B2, then Ctrl+click
  F6, then Ctrl+click F6 again), the latest range still standing takes its place, so the Focus goes
  to A1. Asked of Excel in the next Windows run.

## What the user's run settled *(2026-09-29, decided with the user)*

The user compared ExSheet with Excel (Microsoft 365, Windows) and with Google Sheets by hand, and
the two agreed on each point below.

- **An axis the range spans end to end is not scrolled for.** A Column Heading click with the view
  at the top puts the Focus on row 1, which is the range's top edge, so the Extent is the column's
  last row. Shift+→ then moved the Extent to the last row of the next column, and keeping it in view
  scrolled the Sheet to its bottom. Excel and Google Sheets both extend to C:D and leave the view
  where it was. **While extending, the grid keeps the Extent in view only on an axis the range
  holding the Focus does not span end to end**, judged on the range after the move. Whole columns
  scroll sideways and never down; whole rows scroll down and never sideways; the whole grid scrolls
  for neither. The Extent itself is unchanged: this is a rule about the view, not about the
  Selection. A plain ExGrid reaches the same state through Shift+click on a header or Ctrl+Space
  on row 1, and takes the same rule.
- **A whole column that stops being whole scrolls as any range does.** With C:C selected from C1,
  Shift+↑ moves the Extent from the last row to the one above it, leaving C1:C(last − 1), and the
  Sheet scrolls to the bottom to show it. Excel does the same, so nothing about the Extent changes.
- **A Heading drag shows its size beside the Headings, not in the Name Box.** While a drag over
  Column Headings or Row Headings covers one column or row, the Name Box names the Focus (`C3`,
  `A3`). Once it covers more, Excel empties the Name Box and shows the size (`10R x 16384C`) in a
  label at the Headings, which follows the drag. ExGrid takes it: the **Size Tip** is painted at the
  Heading the Extent is on, and the Name Box is empty while it shows. The text is the size label
  the Consumer already supplies for a selecting drag (`NameBoxSizeLabel`, DC-39); without one,
  nothing is painted. It follows the Extent's Heading, not the pointer's pixel, so it is redrawn
  only when the Extent moves to another column or row: no pointer position reaches C# and no
  JavaScript is added ([ADR-0021](./0021-javascript-is-allowlisted-not-minimised.md)). The Size Tip
  is painted inside the grid's box ([ADR-0040](./0040-a-popover-stays-inside-its-grids-box.md)),
  and it adds one class and its Visual Tokens to the presentation surface
  ([ADR-0029](./0029-the-presentation-surface-is-a-short-list-of-classes-and-tokens.md)).
  A drag over cells is unchanged: the size is in the Name Box, as above.
