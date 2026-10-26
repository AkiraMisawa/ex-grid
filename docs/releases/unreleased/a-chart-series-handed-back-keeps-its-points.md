Product: ExChart
Kind: Fixed
ADR: 0251, 0370

In `@ex-grid/chart`, a series taken from a definition and handed back changed, such as a surface's slice renamed (`{ ...chart.series[0], name }`), kept no points: the empty `bars` it carries replaced them. Its points now stand, as C#'s `with` keeps them. It is published with 0.3.0.
