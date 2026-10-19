# The legend hides a series on a click, and sits where the Consumer puts it

*Decided with the user, 2026-10-19 (M2 grilling Q2 and Q3). The user asked for ApexCharts' freedom of
placement; Q3 settled how far it goes.*

**A click on a legend entry hides or shows that series.** Its Appearance Keys do not move (ADR-0256),
so no other series changes colour. A hidden entry stays in the legend, faded, so it can be shown
again. The axis range is recomputed without it. With panels split, each panel has its own legend.

**The Consumer places the legend**: a position — top (the default), bottom, left or right — and an
alignment — start, centre or end — or no legend at all. That covers the placements ApexCharts'
`position` and `horizontalAlign` are used for.

- **No legend floating over the plot.** It would hide the lines and values under it (spine rule 1).
- **No pixel offsets.** A pixel passed from C# is the pairing ADR-0027/0028 removed; where the legend
  sits is laid out from the resolved metrics.
- **The user does not move the legend.** Placement is the page's design; what the user changes is
  which series are shown, and that is raised as ADR-0272 says.

What is hidden reaches the files as everything the chart shows does: SVG, PNG and CSV leave a hidden
series out (grilling Q5, M1).
