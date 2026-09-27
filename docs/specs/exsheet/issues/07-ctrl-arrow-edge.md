# 07: Ctrl+arrow stops where the data ends

Status: done

**What to build:** The second declaration: a Consumer-supplied edge answer, asked synchronously on Ctrl+arrow and
Ctrl+Shift+arrow. ExSheet answers it from the Sheet's blanks, as Excel does: to the end of the
current block, to the start of the next one, or to the Sheet's edge.

**Blocked by:** 02

- [x] Without the answer, Ctrl+arrow goes to the grid's edge as before (ADR-0012)
- [x] With it, the Focus moves to the answer and Ctrl+Shift+arrow extends to it (ADR-0050)
- [ ] ExSheet's answer matches Excel's over blocks, gaps and an empty column (layer 1 table)

## Comments

The core half, 2026-09-27. `ExGrid.DataEdge` (`Func<CellPosition, GridDirection, CellPosition>?`,
null by default) is asked synchronously on Ctrl+arrow and Ctrl+Shift+arrow — never on Home or
End, which resolve to the same transitions and keep ADR-0012's meaning. `GridSelection` gains
`MoveToEdge` and `ExtendToEdge` overloads taking the answer; an answer off the Focus's line,
behind it or outside the grid is refused by name, and answering the Focus itself moves nothing.
Whole columns and rows stay whole when extended to it. No JavaScript (DC-15). Layer 1:
`EdgeAnswerTests`; layer 2: `EdgeAnswerWiringTests`.

What remains: ExSheet's answer over the Sheet's blanks and its layer 1 table against Excel (the
unticked criterion) belong to the ExSheet stream. Real Ctrl+arrow keys through the capture
listener are layer 3's, under SH-18.

