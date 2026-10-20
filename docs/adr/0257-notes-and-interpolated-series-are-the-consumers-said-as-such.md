# Notes and interpolated series are the Consumer's, and the chart says them as such

*Decided with the user, 2026-10-19 (grilling Q4). Asked along the way: does ExChart hold interpolation
logic (no), must a Consumer split quotes from interpolated values (only to show the difference), what
if the analytics expose no function, and what of a curve whose method changes along it.*

The same rate curve draws saw-toothed forwards under linear zero rates and stepped ones under
log-linear discount factors. A chart that does not say which convention made it can look right and be
wrong.

## Notes

The Chart Definition carries **Notes**: plain lines the Consumer writes ("Zero rates, continuously
compounded, ACT/365F"; "Forward delta, no premium adjustment"). They are shown under the chart and
written into its SVG and PNG files. They are not written into the CSV, which Excel would then fail to
read; the CSV says what it holds through its columns instead (ADR-0254).

## Interpolated series

**ExChart never interpolates on its own** (ADR-0250, the user's rule of 2026-10-13). It draws the
points it is given and joins neighbours with a straight segment or a step (ADR-0253); that join is a
way of drawing points, not a claim about values between them.

*The rationale was replaced once, 2026-10-19.* This first read "ExChart holds no interpolation logic".
The user then asked whether the chart could offer interpolation as a helper, after a survey of 3D charts
found Consumers rebuilding the same general methods; the answer kept the rule — interpolation is the
Consumer's — and moved the line from *holds* to *runs*, under the four conditions of "Helpers" below.

- **Marking a series Interpolated is optional.** A Consumer that hands one series has the chart draw
  it, and claims nothing.
- **A Consumer that wants the difference shown hands two series** with the same colour key: the quotes
  (markers) and the curve, declared Interpolated (a line). The legend shows one entry; the tooltip says
  "5Y 3.12% (quoted)" or "5.5Y 3.18% (interpolated)".
- **Two ways to hand a curve in.** As points, the usual way: the Consumer evaluates its curve on a
  grid. Or as a function `valueAt(t)` over a range, which ExChart calls at about one position per
  device pixel of the plot as it draws, so zooming or resizing keeps the detail. The function is the
  Consumer's interpolation; ExChart only chooses where to ask. A remote analytics service is asked for a
  grid once and handed in as points, never called per pixel.
- **Segments.** A series may declare Segments, each a range with the method's name and whether it is
  extrapolated ("0–2Y linear on zero rates", "2–30Y log-linear on discount factors", "30–40Y flat,
  extrapolated"). The tooltip names the method at its position; an extrapolated segment is drawn
  distinctly and named in the legend; a sample is always taken at each boundary and at each quote, and
  no line is joined across a boundary, where a forward may jump; the segments are added to the Notes.
- **When the analytics expose only their pillars**, only the pillars are drawn: as markers, or joined
  with a note that the lines join pillars and are not the curve. Writing an interpolation in the
  application to fill the gap is the Consumer's to do and to vouch for; it draws a different curve
  from the analytics' whenever the method differs. A forward drawn from pillars alone is drawn as
  points.

## Helpers (2026-10-19)

`ExChart.Engine` carries general interpolation methods in a namespace of their own,
`ExChart.Engine.Interpolation`, as ExGrid carries `GridSource.From` beside a grid that sorts nothing
(ADR-0001). Four conditions keep them the Consumer's:

1. **The chart never calls them.** A Consumer calls one explicitly and hands its result in — a curve as
   a sampled series, a surface as a function z(x, y) (ADR-0274). No interpolation sits on the drawing's
   path.
2. **The result carries its method's name** — "linear", "natural cubic spline", "bilinear", "linear on
   triangles (P1)" — and handing it in writes that name in the Notes and the tooltip, through the
   series' Segment or the surface's method. A Consumer cannot forget to say it.
3. **Only general methods**: linear, bilinear, natural cubic spline, P1 on a triangle mesh. A
   finance-specific choice — SVI, total-variance interpolation, delta or strike space — stays in the
   Consumer's analytics; a helper that claimed the right way to build a volatility surface would blur
   whose result it is.
4. **No extrapolation.** Outside the points a result was made from it has no value, and the chart draws
   a gap or a hole there, as it draws a missing value (ADR-0274).
