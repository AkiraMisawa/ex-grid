# The user narrows the range by dragging across the plot

*Decided with the user, 2026-10-19 (M2 grilling Q10).*

**A drag across the plot selects an x range, and the chart narrows to it**; a double-click or the
reset button returns it to the whole range. The range can also be typed under Chart Filters, in the
axis' own values (dates, numbers, categories).

- The drag's band is drawn from the positions the pointer entry already reports (ADR-0255): it adds no
  listener and no measuring. Its ends snap to positions, so the range is always one the data has.
- The narrowed range is raised as an event and taken back as a parameter (ADR-0272); a drag begun
  against an older drawing is refused.
- Narrowing never changes a value: the y axis is rescaled to what is in range, under ADR-0251's and
  ADR-0253's rules for zero.

## Considered

- **Typed only**: no pointer work, but nobody narrows by reading a curve and typing dates.
- **A navigator under the chart** (Highcharts Stock): shows where the window sits, at the cost of
  drawing every series twice. Not in the first version.
