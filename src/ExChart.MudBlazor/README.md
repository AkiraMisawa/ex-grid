# ExChart.MudBlazor

[ExChart](https://www.nuget.org/packages/ExChart), charts of the data an application shows in
ExGrid, inside a MudBlazor application:

- **`MudChartChrome`** draws the chart's toolbar and context menu with MudBlazor's own controls:
  Chart Filters, Reset range and View as table as buttons, the CSV, SVG and PNG downloads as a
  `MudMenu`, and Chart Filters' range in MudBlazor's inputs (a date picker on a date axis, a select
  of the categories on a category axis) with the axis switch.
- **`MudChartDock`** places the charts made from a grid's selection in a panel beside the grid or
  below it, never over it, as tabs. The grid's context menu opens a chart straight into a new tab,
  "Add to chart" offers every open tab, and a click on a point shows its cell in the grid.
- **`MudChartChrome.GridChrome`** is the grid's `MudGridChrome`, wording the selection bridge's
  commands in ExChart's words.
- **`mud-ex-chart.css`** maps ExChart's Visual Tokens onto MudBlazor's palette and follows the theme,
  dark mode included. The theme's Primary, Secondary and Tertiary colour the first three series, and
  the rest keep ExChart's palette. Error, Warning, Success and Info never colour a series: a red line
  on a risk screen reads as a loss whatever it is.

The legend, the tooltip and the plot are ExChart's own under either Chrome, and the chart behaves the
same. The Chrome draws and calls back; ExChart decides what every command and typed range means.

> **It ships at the same version as ExChart,** and depends on exactly that version and on the
> matching `ExGrid.MudBlazor`, so upgrade them together. While the version is `0.x`, the API may
> change from one version to the next; each version's
> [Release Notes](https://akiramisawa.github.io/ex-grid/release-notes) say what changed.

## Install

```sh
dotnet add package ExChart.MudBlazor
```

Add `--prerelease` for a prerelease. This brings in ExChart, ExGrid.MudBlazor and MudBlazor 9
(`[9.6.0,10.0.0)`). Set MudBlazor up as usual (`AddMudServices()`, its stylesheet and script,
`<MudThemeProvider />` and `<MudPopoverProvider />`), then add the stylesheets in the head:

```csharp
builder.RootComponents.Add<ExGrid.Components.ExGridStylesheet>("head::after");
builder.RootComponents.Add<ExGrid.MudBlazor.MudExGridStylesheet>("head::after");
builder.RootComponents.Add<ExChart.Components.ExChartStylesheet>("head::after");
builder.RootComponents.Add<ExChart.MudBlazor.MudExChartStylesheet>("head::after");
```

## A chart from the grid's selection

```razor
<MudChartDock TRow="Trade" Bridge="_bridge">
    <MudExGridPaper>
        <ExGrid.Script.ExGrid @ref="_bridge.ScriptGrid" TRow="Trade" Rows="_rows" Columns="Columns"
                              ContextCommands="_commands" ContextCommandLabel="_label"
                              Chrome="MudChartChrome.GridChrome" />
    </MudExGridPaper>
</MudChartDock>
```

`_bridge` is ExChart's `GridChartBridge<Trade>`, `_commands` its `Commands` and `_label`
`GridChartCommands.Label`, each held in a field. The grid and the charts are drawn in the browser by their
Script Cores. The dock takes the bridge's events, so the page writes no code to show a chart.

## A chart on its own

```razor
@using ExChart.Script

<MudExGridPaper>
    <ExChart Definition="_definition" Title="Exposure" Chrome="MudChartChrome.Default" />
</MudExGridPaper>
```
