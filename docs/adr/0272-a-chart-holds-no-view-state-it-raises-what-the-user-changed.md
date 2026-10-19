# A chart holds no View State: it raises what the user changed, and keeps it in the data's terms

*Decided with the user, 2026-10-19 (M2 grilling Q4 and Q11). The user found events the natural split
("C' feels the most natural"), after asking whether keeping a view is the component's job at all.*

A risk user who hides PFE99 and narrows the range to 2027–2030 every morning wants the chart to come
back that way. Storing it, naming it and sharing it are not a chart's job (spine rule 3).

**ExChart raises an event for each thing the user changes, and takes each back as a plain
parameter**, as ApexCharts and ECharts do:

| The user changes | Raised | Taken back as |
|---|---|---|
| a series hidden or shown (ADR-0271) | the series' key and whether it is hidden | the set of hidden series keys |
| the x range (ADR-0273) | the range in the axis' values | the range |
| the axis switched, proportional or categorical (ADR-0251) | the axis' mode | the mode |
| a 3D surface turned, zoomed or panned (ADR-0274) | angles, zoom and centre | the same |

There is no View State value, unlike ExGrid's and ExPivot's (ADR-0207): the Consumer keeps what it
wants, in the shape it wants.

## Kept in the data's terms

Every parameter above is held in the data's terms — series keys, axis values, angles — never in
indices or pixels. So:

- **When the data is replaced** (tomorrow's run arrives), the hidden keys and the range are applied to
  the new data as they are; nothing resets.
- **What no longer matches is said, not ignored.** A hidden key that names no series, or a range in
  which no series has a point, is written on the chart (spine rule 1).
- **A gesture begun against the old data is refused.** A drag that started before the data changed is
  dropped when it ends rather than applied to the new data: it carries the drawing's version, as the
  pointer's reports do (ADR-0255), and the version no longer holds (spine rule 6, ADR-0240). A
  Playwright run meets this at machine speed even where a hand rarely does.

## Considered

- **A View State value the chart reads and writes** (ADR-0207's shape): rejected by the user; the
  events already carry everything, and a second, serialisable shape is one more contract.
- **The Chart Definition saved with the view**: rejected; a series added to the Consumer's code next
  month would be missing from every saved view, an old chart shown quietly.
- **Reset on new data**: rejected; the user would narrow the range again every morning.
