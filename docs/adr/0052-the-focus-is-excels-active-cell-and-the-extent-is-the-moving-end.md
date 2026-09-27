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
  Extent, leaving the Focus off screen. A move back inside the view scrolls nothing.
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
  *(Observed for the top-left cell only.)*
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
