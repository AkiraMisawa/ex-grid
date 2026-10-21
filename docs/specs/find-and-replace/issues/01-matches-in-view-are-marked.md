# 01: The matches in view are marked

Status: ready-for-agent
Roadmap: ExGrid — While searching, every matching cell on screen is marked, the current match apart.

**Why:** [ADR-0332](../../../adr/0332-find-reaches-every-product-replace-is-a-write-asked-of-the-consumer-and-the-matches-in-view-are-marked.md) §4.

- [ ] A match function on the grid, defaulting to the reference rule; none and an own OnFind: no marks
- [ ] Every painted match marked, the current apart, kept as rows scroll in; cleared when the panel closes
- [ ] A Visual Token for each; nothing per cell to JavaScript; rows still skip their render
- [ ] Both Chromes; layer 2 and layer 3; Docs Site and Change Note

**Blocked by:** None (can start immediately).
