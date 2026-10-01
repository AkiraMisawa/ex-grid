# 87: `sheet-vs-excel` item 26 after a Number Format widens the column

Status: ready-for-agent

**What to build:** a test ticket 48 found stale. Since ticket 58, the page's *Format selection as #,##0.00*
(`SetCellFormatAsync`) widens a column still at the standard width, as a formatting key does. So item 26 of
`sheet-vs-excel.spec.mjs`, which expected `####`, now sees the number. That follows ADR-0071's reading that
`SetCellFormatAsync` widens. The fourteenth Windows run (case 8) observes it for Format Cells.

**Blocked by:** None (can start immediately)

- [ ] **Item 26 checks what Excel does for its own case.** If its point is `####`, set the column's width
      first, as a user would, so nothing widens. If its point is the format, expect the widening.
- [ ] **Say which in the test's comment**, and cite ADR-0071's reading and SH-26.
