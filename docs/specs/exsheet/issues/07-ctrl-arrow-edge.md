# 07: Ctrl+arrow stops where the data ends

Status: ready-for-agent

**What to build:** The second declaration: a Consumer-supplied edge answer, asked synchronously on Ctrl+arrow and
Ctrl+Shift+arrow. ExSheet answers it from the Sheet's blanks, as Excel does: to the end of the
current block, to the start of the next one, or to the Sheet's edge.

**Blocked by:** 02

- [ ] Without the answer, Ctrl+arrow goes to the grid's edge as before (ADR-0012)
- [ ] With it, the Focus moves to the answer and Ctrl+Shift+arrow extends to it (ADR-0050)
- [ ] ExSheet's answer matches Excel's over blocks, gaps and an empty column (layer 1 table)

## Comments
