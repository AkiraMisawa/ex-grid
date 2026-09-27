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
