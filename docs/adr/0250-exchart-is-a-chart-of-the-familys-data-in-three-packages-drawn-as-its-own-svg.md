# ExChart is a chart of the family's data, in three packages, drawn as its own SVG

*Decided with the user, 2026-10-19, in the ExChart project thread, from the research note
`docs/research/excharts-charting-from-the-grid.md` (on the branch `research/excharts`) and its proof of
concept `spikes/exposure-chart`.*

**ExChart draws charts of the data an application already shows in ExGrid, ExSheet or ExPivot, and
of data it is handed directly.** It is a product of the family, beside ExSheet and ExPivot, in the
same repository and released as separate packages (ADR-0019).

## The packages

```
ExChart.Engine      pure logic, depends on nothing in or outside the family
                    the series model, Series Kind, scales, ticks and their labels, steps, gaps,
                    decimation, panel layout, the CSV writer, and the chart as a scene of shapes
ExChart             the Razor components; depends on ExChart.Engine and ExGrid
                    <ExChart>, its legend, tooltip and Chart Filters, and the bridge that turns a
                    grid selection into a Chart Definition
ExChart.MudBlazor   the Wrapper; depends on ExChart and ExGrid.MudBlazor
```

- **The Engine knows no grid.** A Consumer who wants a chart and no grid takes `ExChart`, whose
  `<ExChart>` takes a Chart Definition built by hand; the grid arrives as a dependency but is never
  rendered. The Engine's tests need no renderer (layer 1).
- **The direction is enforced by project references**, as for every product (ADR-0017).
- **Analytics belong to the Consumer** (the user, 2026-10-13): a bootstrap, an interpolation of a
  curve, a hazard rate are computed outside every ExChart package and handed in as data. ExChart
  draws what it is given and the convention the Consumer declares for it.

## Drawn as its own SVG

*The user, 2026-10-19: "keep external dependencies as few as possible".*

ExChart writes SVG from C#. It wraps no charting library, and it takes no NuGet or npm dependency
beyond what ExGrid already takes (`Microsoft.AspNetCore.Components.Web`).

- **ADR-0021 gives a library no reason to exist here.** Drawing SVG is something Blazor does. What
  a browser alone can do — hear the pointer, turn a 3D view at the frame rate — is allowlisted
  entry by entry (ADR-0255), not imported wholesale.
- **Numbers and dates are formatted by the code that formats the cells**, so a tooltip and a cell
  show the same digits.
- **Appearance is Visual Tokens** (ADR-0027/0028): no colour is a C# parameter. A Consumer declares
  a series' slot, never its colour.
- **The output is deterministic**, so the scene is pinned by layer-1 tests rather than screenshots.

Rejected: wrapping ApexCharts or a similar library. It would add a dependency the user does not
want, put colours and formats in a second system, and move most of the chart into JavaScript the
allowlist exists to keep out.

## The proof of concept is rebuilt, not ported

*The user, 2026-10-19.* `spikes/exposure-chart` stays on `research/excharts` as the reference for
what was found; no file of it is moved into `src/`. It has no tests, renders its drawing as one
`MarkupString` (ADR-0252), has three unrelated input shapes (ADR-0251), and passes colours from C#.

## The first version

- Line, points and step charts, on dates, numbers and categories: the exposure profile, the CDS
  curves and the rate curve pages of the proof of concept.
- The 3D surface (ADR-0255) and the FX volatility pages it is drawn for — the vol table, the 3D
  surface, the term structure and the smile on category axes, and the deposits and forwards (the
  user, 2026-10-19: 3D alone would have nothing to show).
- The MudBlazor Wrapper (user, 2026-10-19).
- Scatter and fitted lines (the user, 2026-10-19, after this ADR: ADR-0290, ADR-0291).
- Later: bars, waterfall, a heat map in the grid's own cells (an ExGrid decision), ExPivot's
  pivot chart, chart state in a Saved View.
