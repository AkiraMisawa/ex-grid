# 30: A Linked Table's columns outlined in the grid that shows them

Status: ready-for-agent

**What to build:** ADR-0057, "A structured reference is outlined by whoever shows its table", and
ADR-0049's note of the same day. ExSheet tells its Consumer which Linked Table columns the Formula
being edited reads, and in which colour. ExGrid outlines the columns a Consumer asks for. `/sheet`
connects the two for its positions grid.

**Blocked by:** Tickets 27 (the keys) and 28 (the colours the core tells).

- [ ] ExGrid: a list of columns, each with a colour, that a Consumer gives. The grid outlines each
      column's body across all its rows in that colour, in the selection overlay. An empty list
      outlines nothing; without the list nothing changes (DC-50)
- [ ] ExSheet: a notification to its Consumer of each Linked Table column read and its colour,
      raised when that changes and emptied when the edit ends (SH-31)
- [ ] `/sheet` passes the notification to its positions grid (SH-31)
- [ ] Layer 3 on both hosts: typing `=SUM(Positions[PV])` outlines the positions grid's PV column in
      the colour `Positions[PV]` wears in the editor; Escape removes it; two Sheets on a page each
      outline only through their own Consumer's wiring (SH-31, DC-25)
