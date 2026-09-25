# ExGrid.MudBlazor

[ExGrid](https://www.nuget.org/packages/ExGrid) inside a MudBlazor application:

- **`MudExGridPaper`** — a Material surface around the grid, with MudBlazor's palette, type and
  `Dense`, `Hover` and `Striped` options, following the theme (dark mode included).
- **`MudGridChrome`** — the grid's seams filled with MudBlazor's own controls: the column menu
  and the Context Menu, the filter panel, the Cell Editor and the loading bar.
- **`mud-ex-grid.css`** — maps the grid's Visual Tokens onto MudBlazor's palette variables.

The grid's behaviour does not change: the Chrome renders and calls back, and the core decides
what every choice means. The same filter chosen under either Chrome gives the same result.

> **This is a beta (`0.x`),** released together with ExGrid at the same version. It depends on
> exactly that ExGrid version, so upgrade the two together.

## Install

```sh
dotnet add package ExGrid.MudBlazor --prerelease
```

This brings in ExGrid and MudBlazor (9.0 or newer). Set MudBlazor up as usual:

- `builder.Services.AddMudServices()`
- MudBlazor's stylesheet and script
- `<MudThemeProvider />` and `<MudPopoverProvider />` in your layout

Then add both grid stylesheets:

```html
<link rel="stylesheet" href="_content/ExGrid/ex-grid.css" />
<link rel="stylesheet" href="_content/ExGrid.MudBlazor/mud-ex-grid.css" />
```

## A grid on a paper

```razor
@using ExGrid
@using ExGrid.Components
@using ExGrid.MudBlazor

<MudExGridPaper Elevation="1" Hover="true" Striped="true">
    <ExGrid TRow="Trade" Source="_source" Columns="_columns" Chrome="_chrome" />
</MudExGridPaper>

@code {
    private static readonly MudGridChrome _chrome = new();

    private readonly InMemoryGridSource<Trade> _source = GridSource.From(Trade.Sample());

    private readonly GridColumn<Trade>[] _columns =
    [
        new("Book", ColumnType.Text, t => t.Book),
        new("Notional", ColumnType.Number, t => t.Notional),
        new("TradeDate", ColumnType.Date, t => t.TradeDate),
    ];
}
```

The paper passes its options to every grid inside it. A parameter set on the grid itself wins.
`MudGridChrome` takes MudBlazor's words where MudBlazor has them, and `Label` and `Icon` let
you name or decorate the rest.

## More

What a Wrapper may and may not do is recorded in
[ADR-0030](https://github.com/AkiraMisawa/ex-grid/blob/main/docs/adr/0030-what-a-design-system-wrapper-owns-and-what-it-may-not-touch.md),
and the criteria it is held to are in §23 of the
[Definition of Done](https://github.com/AkiraMisawa/ex-grid/blob/main/docs/definition-of-done.md).

MIT licensed.
