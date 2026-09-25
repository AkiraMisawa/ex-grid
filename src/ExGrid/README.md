# ExGrid

An Excel-like grid for Blazor, built for reading money and risk numbers:

- virtualised on both axes (a million rows and a hundred columns keep the same DOM)
- pinned columns and Header Groups
- rectangular selection, the keyboard, and copy and paste as a spreadsheet user expects them
- filter panels, a column menu and a Context Menu that a design system can replace

The grid neither holds nor executes. The data, sorting, filtering and edits belong to your
application, the Consumer. The grid displays what it is given and tells you what the user asked
for. Where it cannot do something correctly, it refuses rather than guess: a copy is never
truncated, and a number that does not fit shows `####`.

> **This is a beta (`0.x`).** Every test layer passes in CI on the commit it was built from,
> but the API may still change between betas, and the project's
> [Definition of Done](https://github.com/AkiraMisawa/ex-grid/blob/main/docs/definition-of-done.md)
> has not been signed off. What it still owes is listed in
> [`docs/implementation-status.md`](https://github.com/AkiraMisawa/ex-grid/blob/main/docs/implementation-status.md).

## Requirements

- **.NET 10 or newer.** The package targets `net10.0` and loads unchanged in newer applications.
  (`0.1.0-beta.1` targeted `net8.0`; every later version targets `net10.0`.)
- **Chrome or Edge.** They are the supported browsers; Safari and Firefox are out of scope.
- **An interactive render mode.** The grid handles keys, the pointer and the clipboard. It is
  verified under WebAssembly. Under Blazor Server every browser test also runs, against a
  Server host with a simulated round trip, but support is not declared until the last runs it
  owes are in.

## Install

```sh
dotnet add package ExGrid --prerelease
```

Add the stylesheet to the page that hosts your application (`wwwroot/index.html`, or
`App.razor` in a Blazor Web App):

```html
<link rel="stylesheet" href="_content/ExGrid/ex-grid.css" />
```

The grid loads its own script module; there is nothing else to include.

## A first grid

```razor
@using ExGrid
@using ExGrid.Components

<ExGrid TRow="Trade" Source="_source" Columns="_columns" />

@code {
    // Kept in fields. A new source or column list on every render would make every row
    // render again.
    private readonly InMemoryGridSource<Trade> _source = GridSource.From(Trade.Sample());

    private readonly GridColumn<Trade>[] _columns =
    [
        new("Book", ColumnType.Text, t => t.Book),
        new("Notional", ColumnType.Number, t => t.Notional),
        new("TradeDate", ColumnType.Date, t => t.TradeDate),
    ];
}
```

`GridSource.From` is the bundled in-memory source. It sorts and filters a list you already hold,
and its behaviour is the reference for what sorting and filtering mean. When the rows live
elsewhere, you push a `Window` of rows instead and answer the grid's `OnRangeNeeded`,
`OnSortChanged` and `OnFilterChanged`.

Two rules keep the grid fast:

- **A row repaints when its instance changes.** Rewriting a row object in place does not repaint
  it. Replace the instance instead.
- **Give the grid the same column list across renders.** Build it once, as above.

## Design systems

The menus, the filter panel, the Cell Editor and the loading indicator are seams (`IGridChrome`).
The built-in Chrome needs nothing extra. [ExGrid.MudBlazor](https://www.nuget.org/packages/ExGrid.MudBlazor)
fills them with MudBlazor's controls.

## More

The specification is a set of decision records, one per decision, with its reasons:
[`docs/adr/`](https://github.com/AkiraMisawa/ex-grid/tree/main/docs/adr). The terms used above —
Consumer, Window, Chrome, Header Group — are defined in
[`CONTEXT.md`](https://github.com/AkiraMisawa/ex-grid/blob/main/CONTEXT.md).

MIT licensed.
