Product: ExChart
Kind: Added
ADR: 0370

`@ex-grid/chart` interpolates as `ExChart.Engine.Interpolation` does: `interpolateLinear` and `interpolateNaturalCubicSpline` return an `Interpolant1D`, whose `series(id, name)` hands the curve in as an Interpolated Level series with one Segment named by its method, and `interpolateBilinear` and `interpolateTriangles` return an `Interpolant2D`, whose `surface(options)` samples it as `SurfaceDefinition.sampled` does. None has a value outside the points it was made from. Both engines run the same cases under `tests/cases/chart/interpolation.json` and answer the same values to the last bit. The package also exports `tenor`, which writes a tenor in years as the market does (6M, 1Y, 2.5Y). It is published with 0.3.0.
