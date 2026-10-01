# 90: A pinned cell with lines keeps its row gridline

Status: ready-for-agent

**What to build:** a defect ticket 48 found. On the Sheet, a Pinned Column's cell paints its row's gridline
itself, as a background image, because its own ground covers the row's. A cell that draws a line layer
(`.ex-lined`, ADR-0050 item 15) replaces that background image, so the gridline goes. Ticket 81 kept a tint
under the lines with `--ex-tint`. The gridline needs the same.

**Blocked by:** None (can start immediately)

- [ ] **A pinned cell with lines on any side keeps its row gridline**, under the lines, where Excel
      would show the gridline.
- [ ] **Ticket 81's tint and ticket 85's single tint** still hold.
- [ ] **Tests:** layer 2 for the stylesheet; layer-3 pixels in a spec. CI runs them.
