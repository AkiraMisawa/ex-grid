# A fitted line is the Consumer's, and says its method and coefficients

*Decided with the user, 2026-10-19, in the ExChart project's scatter grilling (Q2, Q5). It applies
ADR-0257's rule for interpolated series to regression: "the Consumer computes, and the chart says how".*

A line through a scatter is a claim made by a method. Least squares and Theil–Sen draw different lines
through the same names whenever one name sits far off; a chart that shows the line without the method
looks equally right either way. Read by a person checking what an AI computed, the method and its
numbers are what they check.

## ExChart fits nothing on its own

**A fitted line is a series the Consumer hands in**: a Level, as points or as a function (ADR-0257).
No option on a scatter makes the chart compute a line.
*(2026-10-28, [ADR-0420](./0420-a-chart-drawn-in-the-browser-takes-format-codes-and-the-consumers-own-script-in-place-of-a-dotnet-function.md): drawn by the Script Core in the browser, a fitted curve crosses as its method, space and
coefficients, and the Script Core's twin makes the same function.)*

**The Engine offers generic fits as helpers**, in `ExChart.Engine.Fitting`, beside the interpolation
helpers of `ExChart.Engine.Interpolation` (ADR-0257) and in their shape: a helper takes numbers and
returns a result that names its method. Calling one is the Consumer computing; the helper only saves
writing the arithmetic.

- **Ordinary least squares** and **Theil–Sen** (the median of the pairwise slopes, and the median
  intercept), in the first version. Theil–Sen is the one a single defaulted name in a credit scatter
  does not drag.
- **Least squares of degree M**, a polynomial (the user, 2026-10-19, for the Docs page's curve-fitting
  example after PRML's first chapter: degrees 1, 3 and 9 through ten noisy points of sin 2πx, the last
  passing through every point). Its Note gives the degree, R² and n rather than M + 1 coefficients.
- **A fit carries its method, slope, intercept, R² and the number of points**, and the space it was
  fitted in when that is not the plotted one ("on ln y vs ln x").
- **Its Note is one line**: "OLS: y = 41.2x + 88.5, R² = 0.63, n = 212". A line handed in from a fit
  puts that line in the chart's Notes (ADR-0257), on screen and in the SVG and PNG.

## Drawn over the data's range only

**A fitted line is drawn between the smallest and the largest x it was fitted on.** Drawing it to the
plot's edges is extrapolation (ADR-0253: nothing is drawn beyond the data), allowed only as a Segment
the Consumer declares extrapolated, which is drawn distinctly and named (ADR-0257). Excel's trendline
reaches the plot's edges by default; that was rejected.

**On a log axis a fit is drawn as the function it is.** Fitted on ln y and ln x, it is straight on log
axes; fitted linearly, it is curved there. Handed in as a function, it is sampled per device pixel
(ADR-0257), so either is drawn correctly.

## Not in the first version

**Confidence and prediction bands.** A band rests on assumptions (normal errors, independence) the
chart cannot vouch for, and reads as reassurance. A Consumer who wants one hands in its two edges as
two series and says in the Notes what they are.
