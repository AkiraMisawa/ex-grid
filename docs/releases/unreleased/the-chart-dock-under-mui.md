Product: ExChart
Kind: Added
ADR: 0258, 0281, 0390

`@ex-grid/chart-mui` is ExChart for React's MUI Wrapper: `muiChartChrome` draws a chart's toolbar, Chart Filters and context menu in MUI's components, and `MuiChartDock` is the Chart Dock in MUI's tabs beside or below the grid, over `@ex-grid/chart-react`'s `useChartDock`. Its stylesheet maps the chart's Visual Tokens onto a MUI theme made with CSS variables, so the primary and secondary colours lead the series in light and dark; a theme without CSS variables is refused by name. It is published with 0.3.0.
