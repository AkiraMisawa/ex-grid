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

**ExChart holds no interpolation logic** (ADR-0250, the user's rule of 2026-10-13). It draws the
points it is given and joins neighbours with a straight segment or a step (ADR-0253); that join is a
way of drawing points, not a claim about values between them.

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
