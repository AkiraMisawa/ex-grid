# 10: The pointer entry, the tooltip and the guide

Status: done

**Why:** [ADR-0255](../../../adr/0255-two-javascript-entries-the-charts-pointer-and-the-3d-surface.md) (eleventh entry), [ADR-0270](../../../adr/0270-a-charts-tooltip-shows-every-series-at-the-pointers-position.md).

- [x] `ex-chart.js`: one `mousemove` listener per chart reports the position under the pointer only when it changes, and when it leaves; the positions' pixel bounds come from C# with the drawing's version; a report against an older version is dropped
- [x] The tooltip lists every visible series of the pointer's panel at that position, through the series' format: the nearest N by height, then "and M more series", never taller than the plot
- [x] A step names its interval, a thinned pixel its range, an interpolated value says so; a series with no point at the position says so rather than reading between points
- [x] The guide crosses every panel
- [x] A move to another position renders the tooltip and the guide and nothing else (CH-1); the listing matches CH-4
- [x] The script is imported at its versioned address and refused when its stamp is not this build's (ADR-0123)

**Blocked by:** 08.

## Comments

- 2026-10-19: The report of a new position carries the pointer's height, so "the nearest N" is chosen by the height at which the pointer reached the position; a move within one position is not reported.
