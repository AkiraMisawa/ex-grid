Product: ExChart
Kind: Added
ADR: 0320, 0370

`@ex-grid/chart` reads a Chart Spec: `readChartSpec` takes the spec's text and a lookup from a Data Handle to a table (`ChartData`, with `chartDataOf` as the reference table) and answers a `ChartSpecReading` of the chart or the 3D surface, or throws a `ChartSpecRefusedError` with the same path and the same words as `ChartSpecRefusedException`. Both readers run the same cases under `tests/cases/chart/spec*.json`. A decimal no JavaScript number writes digit for digit is refused rather than rounded. The package now depends on `@ex-grid/data`, whose JSON reader it reads the spec with. It is published with 0.3.0.
