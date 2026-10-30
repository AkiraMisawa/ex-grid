# 08: The `ExChart` package draws the scene as SVG components that skip their render

Status: done

**Why:** [ADR-0252](../../../adr/0252-a-chart-renders-as-svg-elements-in-components-that-skip-their-render.md), [ADR-0256](../../../adr/0256-a-series-look-is-chosen-by-three-appearance-keys.md), ADR-0027/0028.

- [x] `dotnet/src/ExChart` references `ExChart.Engine` and `ExGrid` and nothing else; text width is ExGrid's glyph estimate at the chart's Density
- [x] `<ExChart>` renders a Chart Definition as SVG elements: one component per panel's axes and grid lines, one per series, each with a hand-written `ShouldRender`; no `MarkupString`
- [x] A series' SVG node count does not grow with its number of points (CH-3)
- [x] Each colour key takes a slot in order of first appearance, hidden series included; ten slots by default, a theme may declare more; past the last, colours repeat and a note says so (CH-6)
- [x] Colours, line patterns and marker sizes are Visual Tokens in `ex-chart.css`; geometry is emitted inline from the resolved metrics
- [x] The scene's notes are written under the chart

**Blocked by:** 01–07.

## Comments

- 2026-10-19: The chart is told no Device Pixel ratio (ExGrid's `matchMedia` entry is ExGrid's own), so Thinning counts two device pixels a CSS pixel: every peak is kept up to a 2× display, and the path stays bounded.
- 2026-10-19: A level's points are marked only when its marker key asks, or when they are an Interpolated series' quotes; a step's ends are always marked (ADR-0253). 121 monthly points with a marker each read as a bead string, not a line.
- 2026-10-19: The parts are internal components opened from C#, so they stay out of the public API.
