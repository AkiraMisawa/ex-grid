# 101: Inside over whole rows sets every vertical side

Status: ready-for-agent

**What to build:** the fourteenth Windows run's case 14. Inside over rows 3:4 set the line between the
two rows, the lines between columns, XFD's right **and A's left**. Excel records it as a row format: row 3
holds left, right and bottom, and row 4 left, right and top. ADR-0071 read A's left as not set.
ADR-0071, "What the fourteenth Windows run settled", ticket 57's model.

**Blocked by:** None (can start immediately)

- [ ] **Inside (and Inside vertical) over whole rows sets A's left**, as it sets every other vertical
      side and XFD's right. `CellFormatAt` answers it on A3 and A4.
- [ ] **Inside over the whole Sheet is unchanged** (case 15 confirmed it), and so are an outline over
      whole rows (the twelfth run's case 14) and Inside over whole columns.
- [ ] **The Sheet Document round-trips it**, and one undo step takes it back.
- [ ] **Layer 1/2.**

## Comments
