# ExChart

Status: ready-for-agent

ExChart draws charts of the data an application shows in ExGrid, ExSheet or ExPivot, or hands over
directly, as its own SVG ([ADR-0250](../../adr/0250-exchart-is-a-chart-of-the-familys-data-in-three-packages-drawn-as-its-own-svg.md)
to [ADR-0255](../../adr/0255-two-javascript-entries-the-charts-pointer-and-the-3d-surface.md)). This spec
synthesises those ADRs; it decides nothing. The research and proof of concept it rests on are on the
branch `research/excharts` (`docs/research/excharts-charting-from-the-grid.md`, `spikes/exposure-chart`).

## Milestones

| | What | Tickets |
|---|---|---|
| M1 | `ExChart.Engine`: the series model, Series Kind, scales and ticks, Thinning, panel layout, the scene, CSV — layer 1 only | 01–07 |
| M2 | `ExChart`: the components that skip their render, the tooltip and its listener, the legend, Chart Filters, Visual Tokens, accessibility — layers 2 and 3 | 08–14 |
| M3 | The grid bridge: chart a selection, series in columns and in rows, add series, refuse a stale selection, a Diff in a panel of its own by default (moved from ticket 06), a chart that follows its rows, a click that shows the cell (ADR-0258); the Chart Dock is the Wrapper's, so M6's | 15–20 |
| M4 | The 3D surface (ADR-0274) and the Interpolation Helpers (ADR-0257); its colour bar and centre, the Consumer's triangle mesh and its logarithmic axes (ADR-0310 to ADR-0312); the FX volatility pages are M6's | 30–37 |
| Scatter | Observation series, log axes, merged marks, the fit helpers, the nearest point and the rectangle, the CDS Showcase (ADR-0290, ADR-0291) | 50–59 |
| M5 | Measured on both hosts against the proof of concept's readings | to be written |
| M6 | `ExChart.MudBlazor` with its Chart Dock (ADR-0258), the Docs Site pages and Examples, Change Notes, the package smoke and the release; the SVG, PNG and CSV files ADR-0252 writes on a click, through the thirteenth JavaScript entry (ADR-0282), with CH-9 to CH-14 (ADR-0283) | to be written |

## What the first version holds (ADR-0250)

Line, points and step charts on dates, numbers and categories (the exposure profile, the CDS curves,
the rate curve), the 3D surface with the FX volatility pages, scatter with fitted lines (ADR-0290), and the MudBlazor
Wrapper.

## Roadmap lines

ExChart is a product of the Docs Site and the Release Notes since its Chart section was added
(ADR-0280), so an open ticket's `Roadmap:` line names it: `Roadmap: ExChart — …`. A ticket a Consumer
would not care about keeps `Roadmap: internal`.
