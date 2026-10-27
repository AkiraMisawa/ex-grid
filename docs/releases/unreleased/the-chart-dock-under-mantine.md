Product: ExChart
Kind: Added
ADR: 0258, 0281, 0390

`@ex-grid/chart-mantine` is ExChart for React's Mantine Wrapper: `mantineChartChrome` draws a chart's toolbar, Chart Filters and context menu in Mantine's components, and `MantineChartDock` is the Chart Dock in Mantine's tabs beside or below the grid, drawing the state `@ex-grid/chart-react`'s new `useChartDock` holds from the bridge's events. Its stylesheet maps the chart's Visual Tokens onto the Mantine theme, so the primary colour leads the series in light and dark. It is published with 0.3.0.
