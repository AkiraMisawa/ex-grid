Product: ExChart
Kind: Added
Docs: exchart/grid
ADR: 0258

`GridChartBridge` charts a grid's selection from the grid's context menu, and the chart follows the rows it was made from: a recalculated value is redrawn, a deleted row becomes a gap with a note, and a click on a point moves the grid's Focus to its cell.
