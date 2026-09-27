# 09: Editing in the Formula Bar, and the Name Box moves the Focus

Status: ready-for-agent

**What to build:** The Formula Bar becomes the Cell Editor's second surface: one uncommitted text shown in two
places, and a commit from either commits once. The fourth ADR-0050 declaration arrives here: the
Consumer can ask the grid to place the Selection and the Focus. The Name Box uses it, so a user
types `D200` and lands there.

**Blocked by:** 08

- [ ] Typing in the bar updates the cell's editor and the other way round; one commit, one Edit Intent (ADR-0051)
- [ ] Escape from either cancels once
- [ ] The Name Box moves the Selection and the Focus and scrolls to them (ADR-0050, item 4)
- [ ] A placement requested under an older Row Sequence Version is dropped (ADR-0011)
- [ ] Keys typed in the bar are captured by the root's listener (ADR-0018)

## Comments
