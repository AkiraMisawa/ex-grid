# 09: The legend hides a series and sits where the Consumer puts it

Status: done

**Why:** [ADR-0271](../../../adr/0271-the-legend-hides-a-series-and-sits-where-the-consumer-puts-it.md), [ADR-0272](../../../adr/0272-a-chart-holds-no-view-state-it-raises-what-the-user-changed.md).

- [x] Top (default), bottom, left or right, each with start, centre or end, or none; laid out from the metrics, never over the plot, no pixel offsets
- [x] With panels split, each panel has its own legend
- [x] A click hides or shows the series; a hidden entry stays, faded; no other series changes its look; the axes are rescaled
- [x] Raised as an event with the key and whether it is hidden; taken back as the set of hidden keys
- [x] Hiding one series renders that series and the axes it rescales, and no other series (CH-2)
- [x] A hidden key that names no series is written on the chart (CH-5)

**Blocked by:** 08.

## Comments

- 2026-10-19: CH-2 is tested where hiding rescales no axis. A series on an axis that hiding rescales moves, so it renders with the axis; every other series does not.
