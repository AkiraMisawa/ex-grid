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
| **Level** | a quantity at an instant (EPE, a zero rate, a spread) | points, joined by a line or not | yes, unless the series declares otherwise |
| **Rate over an interval** | constant on (tᵢ₋₁, tᵢ] or [tᵢ, tᵢ₊₁) (a hazard rate, an instantaneous forward under log-linear discount factors) | a step: filled end where it takes the value, open where it does not, no riser | declared per series |
| **Amount per period** | a total over its interval (CVA by bucket) | a step over its interval, its height the amount per year, never a line | always |

*2026-10-19:* a fourth kind, **Observation** — one thing measured on two quantities, drawn as an
unjoined marker on two value axes — was added for scatter (ADR-0290).

- **A line joins only neighbours in the data.** A series over two stretches of rows is not drawn
  across the rows between them.
- **A missing value is a gap, never a zero.** The tooltip says "(no value)"; a date where the series
  has no point says "—".
- **Nothing is drawn beyond the data.** No spline, no extrapolation. ExChart holds no interpolation
  logic; a curve between quotes is the Consumer's, and is said as such (ADR-0257).

## An amount per period is drawn per year

*Grilled 2026-10-19 (Q6).* A step's height is the amount divided by its interval's length in years,
and the axis says so ("CVA / year"); the tooltip gives both, "4–5Y bucket: 300 (1,200 a year)". Drawn
undivided, buckets that widen from months to quarters to years make a constant risk jump twelvefold
where only the width changed — what the proof of concept showed. No undivided option is offered: with
equal widths it draws the same shape, and with unequal widths it misleads. An amount at an instant (a
cash flow on its date) is not an amount per period; it waits for bars.

## A level's axis starts at zero unless the series says it does not

*Grilled 2026-10-19 (Q8).* An exposure read from anywhere but zero turns a small change into a large
one, so a Level axis includes zero by default. A series read away from zero — a zero rate between 3.0%
and 3.6%, a volatility between 9% and 12% — declares so, and an axis that does not reach zero shows a
break mark at its foot. Excel's automatic rule (no zero when the minimum is at least 5/6 of the
maximum) was rejected: it guesses.
