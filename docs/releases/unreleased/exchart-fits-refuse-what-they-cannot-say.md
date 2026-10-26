Product: ExChart
Kind: Behaviour
Docs: exchart/scatter
ADR: 0291, 0257

A fit (`Fit` in ExChart.Engine, `fitLeastSquares`, `fitTheilSen` and `fitPolynomial` in `@ex-grid/chart`) now refuses what it used to hand back quietly wrong: sums that overflow (R² was NaN), a Theil–Sen pair whose slope is no finite number, and a polynomial far from zero whose coefficients in powers of x no longer give the fitted curve, whose refusal names the centre to fit about. A least-squares R² is never below zero, so a constant's Note no longer says "R² = -0.00". The interpolation helpers now refuse a position or a value that is not a finite number, by its place, where one value that was no number used to empty every interval around it. A Consumer who handed such values in now catches an `ArgumentException` (a `TypeError` in TypeScript) where it drew a wrong or empty curve.
