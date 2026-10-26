Product: ExChart
Kind: Added
ADR: 0370

`@ex-grid/chart` fits a curve as `ExChart.Engine.Fitting` does: `fitLeastSquares`, `fitTheilSen` (refused past `theilSenLimit`, 3,000 points) and `fitPolynomial`, each in the plotted space or on ln y and ln x (`FitSpace`), return a `FittedCurve` with its coefficients, R², Note and `valueAt`, and `series(id, name)` hands it in as a Level series with values only over the range it was fitted on. Both engines run the same cases under `tests/cases/chart/fitting.json`; in the plotted space they answer the same coefficients to the last bit, and on a logarithm they may differ there, as each language's `ln` and `exp` do. It is published with 0.3.0.
