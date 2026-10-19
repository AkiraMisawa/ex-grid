# 19: A click on a point shows its cell in the grid

Status: done

**Why:** [ADR-0258](../../../adr/0258-the-selection-bridge-follows-the-rows-it-was-made-from.md) (M3-Q6).

- [x] A press and a release at one position raise the series nearest the pointer and the position; a drag raises none
- [x] The bridge names the point's Row Key and column, scrolls the grid to it and puts the Focus there through the grid's own placement, dropped under another order
- [x] A row not among the grid's rows (filtered out) is refused by name
- [x] Nothing links on hover

**Blocked by:** 17.

Roadmap: internal
