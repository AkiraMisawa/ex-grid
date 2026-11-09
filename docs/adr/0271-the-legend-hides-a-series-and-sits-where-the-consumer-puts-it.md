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

*Note of 2026-11-09, as a Consumer toggled two series from the keyboard:* **an entry is a button on the
keyboard as well.** Tab reaches it, and Enter or Space hides or shows its series, as a click does. A toggle
changes the legend's markup, so the legend is drawn again (ADR-0252), and the entry that held the keyboard
left with the old drawing: the keyboard fell to the page's body, and a second Enter reached nothing. Now a
legend drawn again under the keyboard, for any reason — the toggle, the Consumer's hidden keys, new data —
hands it to the entry for the same series, in whichever legend that entry now stands. An entry gone with its
series hands it to the entry drawn in its place, or, its legend gone with its panel, to the first entry left,
as a closed Chart Dock tab hands it to the tab shown in its place and never to the page's body (ADR-0410's
note of 2026-10-28). Only a keyboard the drawing dropped is handed back: one a listener moved elsewhere as
the entry left stays where it was put, and a chart that did not hold the keyboard takes none.
