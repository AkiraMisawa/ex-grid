# 10: The report's Change Highlight keeps times, not reports

Status: ready-for-agent

**What to do:** build the Change Highlight part of [ADR-0161](../../../adr/0161-expivots-live-redraw-makes-the-next-report-from-the-last.md).
- ExPivot hands the grid one `CellChangedAt` for as long as a history lasts.
- The history holds the time each cell's painted text last changed, by row key and value column, and no
  report.
- The times are set when a data redraw is laid out:
  - the rows on a changed path are compared with the previous report's painted text;
  - a row new to the report is marked whole;
  - a redraw laid out afresh under data compares every row, in slices (PV-40).
- A mark ends with its time.

**Blocked by:** 09. Until 11 builds the changed paths, compare every row on every data redraw: the rule is
the same, and 11 narrows it.

## Done when

- [ ] PV-46 passes (§29), with a fake `TimeProvider`; PV-36 still passes
- [ ] After a burst of data then quiet, the history holds no report, and marks end on time
- [ ] Layer 1 and 2 green; `/pivot-live`'s layer-3 spec passes locally

## Comments
