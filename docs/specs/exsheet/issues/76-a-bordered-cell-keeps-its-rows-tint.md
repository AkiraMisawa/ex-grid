# 76: A bordered cell keeps a group or total row's tint, and a pinned cell's stripe

Status: ready-for-agent

**What to build:** a fix found by ticket 47. ADR-0050 item 15 draws a cell's dashes, its double lines,
the pixel past the gridline and a neighbour's Fill as background layers on the cell. A group or total
row's tint, and a pinned cell's stripe tint, are background images on the same cell. On a cell that
has any of those layers, the lines replace the tint, so the row looks like an ordinary row there.
ExSheet uses neither, but an ExGrid Consumer may use both.

**Blocked by:** None (can start immediately)

- [ ] On a cell with a per-cell appearance, a group or total row's tint and a pinned cell's stripe
      still show, beneath the lines and above the cell's Fill, if it has one.
- [ ] Layer 2 markup and a layer-3 pixel check, with the test named after ADR-0050 item 15 and the
      ADR of the tint it keeps.
- [ ] P1–P9 hold. Re-measure in `spikes/render-bench` if the change adds a layer to every cell.
