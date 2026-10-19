# ExChart

Charts of the data an application shows in [ExGrid](https://www.nuget.org/packages/ExGrid), drawn as
SVG elements Blazor diffs, so a hover renders the tooltip and nothing else. A chart is defined in the
data's own terms with [ExChart.Engine](https://www.nuget.org/packages/ExChart.Engine): every series
declares its Series Kind, and is drawn as what it is.

- A tooltip lists every series of the pointer's panel at the pointer's position.
- A click on a legend entry hides or shows its series; the application places the legend.
- A drag across the plot narrows the range; Chart Filters type it.
- An accessible summary is generated from the definition, and "View as table" shows the data.

ExChart holds no view state: it raises each thing the user changes as an event and takes it back as a
parameter, so the application keeps what it wants.
