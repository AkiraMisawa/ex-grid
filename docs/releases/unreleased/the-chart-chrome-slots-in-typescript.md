Product: ExChart
Kind: Added
ADR: 0281, 0390

ExChart's Script Core takes a `chrome` of two slots, the toolbar with Chart Filters and the context menu's contents, as the C# `IChartChrome` does, and `@ex-grid/chart-react`'s `ExChart` takes them as render functions drawn through portals. The commands, their order, what a typed range reads as and the menu's frame stay the chart's, so a Consumer's own design system draws them and decides nothing. It is published with 0.3.0.
