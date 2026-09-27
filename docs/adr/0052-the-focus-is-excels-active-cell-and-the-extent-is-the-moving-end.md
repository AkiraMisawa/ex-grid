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

## What is not decided yet

Excel's answers are known where they were observed:

- Shift+arrow and Shift+click leave the active cell on the fixed end.
- A Heading click puts it on the first visible row or column.
- The corner puts it on the top-left visible cell.
- After a fill, it is on the source's first cell.
- While pointing, the Name Box names the pointed cell.

**Where they have not been observed, nothing is decided.** Examples are Shift+arrow after Enter
has cycled the active cell inside a range, Ctrl+click, Shift+Backspace, and Ctrl+. The implementation
**keeps ADR-0012's behaviour until those answers are in**
(`docs/specs/exsheet/verify-on-windows-2.md`, Part B). This ADR then gains the rules, and ADR-0012
and the Definition of Done's keyboard and selection criteria are rewritten against them in one
change.

## Consequences

- `CONTEXT.md`'s **Focus** is redefined, **Extent** is added, and **Anchor** is retired.
- Every criterion naming the Anchor (KB-*, SL-*, and DC-2/3/11/27) is rewritten when the
  implementation lands. It is not rewritten before, because the criteria describe what the code
  does today.
