# 05: Find reaches the column headers

Status: ready-for-agent
Roadmap: ExGrid, ExPivot — Find stops on a column header or a column label that matches, and marks it.

**Why:** [ADR-0333](../../../adr/0333-find-reaches-the-column-headers-and-marks-a-header-it-finds.md).

- [ ] ExGrid searches its visible column headers and Header Group labels in the order of ADR-0333 §3, wrapping, forward and backward; the Consumer's request is unchanged
- [ ] A header match is the current match: current Match Mark, scrolled into view horizontally, the panel says it was found in a header, the Focus does not move; the next step continues from it
- [ ] Every header in view that matches wears the Match Mark
- [ ] A selection as the scope searches no header; Replace never writes a header
- [ ] `FindsHeaders` (default on); ExSheet turns it off, so its column letters are not searched
- [ ] ExPivot's column labels and value captions are found; the "not searched yet" sentences on its pages and in ticket 03 are removed
- [ ] Both Chromes; layer 1, 2 and 3; Docs Site pages for ExGrid and ExPivot; Change Notes (ExGrid behaviour change, ExPivot added)

**Blocked by:** 01, 02, 03.
