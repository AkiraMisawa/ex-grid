# 01: The matches in view are marked

Status: ready-for-agent
Roadmap: ExGrid — While searching, every matching cell on screen is marked, the current match apart.

**Why:** [ADR-0332](../../../adr/0332-find-reaches-every-product-replace-is-a-write-asked-of-the-consumer-and-the-matches-in-view-are-marked.md) §4.

- [x] A match function on the grid, defaulting to the reference rule; none and an own OnFind: no marks
- [x] Every painted match marked, the current apart, kept as rows scroll in; cleared when the panel closes
- [x] A Visual Token for each; nothing per cell to JavaScript; rows still skip their render
- [ ] Both Chromes; layer 2 and layer 3; Docs Site and Change Note *(both Chromes, layer 2, the Docs Site and the Change Note are done; the layer-3 tests in `find.spec.mjs` are written and have not yet run)*

**Blocked by:** None (can start immediately).
