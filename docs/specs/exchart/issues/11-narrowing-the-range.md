# 11: The user narrows the range by dragging across the plot

Status: done

**Why:** [ADR-0273](../../../adr/0273-the-user-narrows-the-range-by-dragging-across-the-plot.md), [ADR-0272](../../../adr/0272-a-chart-holds-no-view-state-it-raises-what-the-user-changed.md).

- [x] A drag across the plot draws its band from the pointer entry's reports and narrows to it; its ends snap to positions
- [x] A double-click or the reset button returns the whole range
- [x] Raised as an event in the axis' values; taken back as a parameter, applied to new data as it is
- [x] A drag begun against an older drawing is refused when it ends; a range in which no series has a point is written on the chart (CH-5)
- [x] The y axis is rescaled to what is in range, under the zero rules

**Blocked by:** 10.
