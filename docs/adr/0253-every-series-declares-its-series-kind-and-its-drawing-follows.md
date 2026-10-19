# Every series declares its Series Kind, and its drawing follows from it

*Decided with the user, 2026-10-19. The user: required (2026-10-19); steps with open and filled ends (U5,
2026-10-13). Found by building: research note §4.*

A line between two points claims the quantity passed through every value between them. That is true
of a level and false of an amount per period. CVA by bucket drawn as a line jumped wherever the
bucket width did, and read as a profile the calculation never saw.

**Every series declares a Series Kind**, and a series without one is refused, not drawn with a
default:

| Series Kind | Means | Drawn as | Axis from zero |
|---|---|---|---|
| **Level** | a quantity at an instant (EPE, a zero rate, a spread) | points, joined by a line or not | declared per series |
| **Rate over an interval** | constant on (tᵢ₋₁, tᵢ] or [tᵢ, tᵢ₊₁) (a hazard rate, an instantaneous forward under log-linear discount factors) | a step: filled end where it takes the value, open where it does not, no riser | declared per series |
| **Amount per period** | a total over its interval (CVA by bucket, a cash flow) | a step over its interval (bars later), never a line | always |

- **A line joins only neighbours in the data.** A series over two stretches of rows is not drawn
  across the rows between them.
- **A missing value is a gap, never a zero.** The tooltip says "(no value)"; a date where the series
  has no point says "—".
- **Nothing is drawn beyond the data.** No spline, no extrapolation. Where the Consumer draws an
  interpolation between quotes it hands the interpolated points in, and declares the convention as a
  footnote the chart shows.
