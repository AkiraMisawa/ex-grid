# ExGrid.MudBlazor

[ExGrid](https://www.nuget.org/packages/ExGrid) inside a MudBlazor application:

- **`MudExGridPaper`** — a Material surface around the grid, with MudBlazor's palette, type and
  `Dense`, `Hover` and `Striped` options, following the theme (dark mode included).
- **`MudGridChrome`** — the grid's seams filled with MudBlazor's own controls: the column menu
  and the Context Menu, the filter panel, the find panel, the Cell Editor and the loading bar.
- **`mud-ex-grid.css`** — maps the grid's Visual Tokens onto MudBlazor's palette variables.

The grid's behaviour does not change: the Chrome renders and calls back, and the core decides
what every choice means. The same filter chosen under either Chrome gives the same result.

> **Released together with ExGrid, at the same version.** It depends on exactly that ExGrid version,
> so upgrade the two together. While the version is `0.x`, the API may change from one version to
> the next; each version's [Release Notes](https://akiramisawa.github.io/ex-grid/release-notes) say
> what changed.

## Install

```sh
dotnet add package ExGrid.MudBlazor
```

Add `--prerelease` for a prerelease, a version with a suffix such as `-rc.1`.

This brings in ExGrid and MudBlazor 9: the package takes `[9.6.0,10.0.0)`, any 9.x from 9.6.0.
An application that references an earlier 9.x is stopped at restore (NU1605, an error by
default) and moves MudBlazor up within 9; one on MudBlazor 10 is warned (NU1608, a warning unless
the application treats it as an error). Set MudBlazor up as usual:

- `builder.Services.AddMudServices()`
- MudBlazor's stylesheet and script
- `<MudThemeProvider />` and `<MudPopoverProvider />` in your layout

Then add both grid stylesheets, as components in the head: `<ExGridStylesheet />` and
`<MudExGridStylesheet />` in `App.razor`'s `<head>` in a Blazor Web App, or root components in a
standalone application's `Program.cs`:

```csharp
builder.RootComponents.Add<ExGrid.Components.ExGridStylesheet>("head::after");
builder.RootComponents.Add<ExGrid.MudBlazor.MudExGridStylesheet>("head::after");
```

Each links its file at the address of the one the package was built with, so a normal reload after
an upgrade brings the new files.

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
