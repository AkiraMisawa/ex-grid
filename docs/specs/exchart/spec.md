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
| M3 | The grid bridge: chart a selection, series in columns and in rows, add series, refuse a stale selection, a Diff in a panel of its own by default (moved from ticket 06) | to be written |
| M4 | The 3D surface and the FX volatility pages | to be written |
| M5 | Measured on both hosts against the proof of concept's readings | to be written |
| M6 | `ExChart.MudBlazor`, the Docs Site pages and Examples, Change Notes, the package smoke and the release; the SVG, PNG and CSV files ADR-0252 writes on a click, through the thirteenth JavaScript entry (ADR-0282), with CH-9 to CH-14 (ADR-0283) | to be written |

## What the first version holds (ADR-0250)

Line, points and step charts on dates, numbers and categories (the exposure profile, the CDS curves,
the rate curve), the 3D surface with the FX volatility pages, and the MudBlazor Wrapper.

## Roadmap lines

The Docs Site and the Release Notes know four products (ExGrid, ExSheet, ExPivot, Data), and both refuse
another until it has a heading of its own. Until M6 makes ExChart one, these tickets carry
`Roadmap: internal`; that ticket rewrites each open ticket's line to name ExChart.
