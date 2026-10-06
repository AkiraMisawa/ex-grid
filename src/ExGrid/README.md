# ExGrid

An Excel-like grid for Blazor, built for reading money and risk numbers:

- virtualised on both axes (a million rows and a hundred columns keep the same DOM)
- pinned columns and Header Groups
- rectangular selection, the keyboard, and copy and paste as a spreadsheet user expects them —
  Delete, Ctrl+D / Ctrl+R and Ctrl+Z included, with your application keeping the history
- Ctrl+F that searches every row, not only the painted ones
- Excel's status-bar figures — Average, Count, Sum and the rest — over every selected cell, asked of whoever holds the data
- filter panels, a column menu, a Context Menu and a find panel that a design system can replace

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
<link rel="stylesheet" href="_content/ExGrid/ex-grid.min.css" />
```

The grid loads its own script module; there is nothing else to include.

The script and the stylesheet are minified, with source maps. Whether a browser gets the new ones
after an update is decided by how your application serves static files (ADR-0123). .NET's static
assets fingerprint them, so a new version is a new URL. To get that, reference the stylesheet
through the fingerprint, and keep the import map, through which the grid's script is resolved:

```razor
@* App.razor in a Blazor Web App, with app.MapStaticAssets() *@
<link rel="stylesheet" href="@Assets["_content/ExGrid/ex-grid.min.css"]" />
<ImportMap />
```

```html
<!-- wwwroot/index.html in a standalone WebAssembly app, with
     <OverrideHtmlAssetPlaceholders>true</OverrideHtmlAssetPlaceholders> in the project -->
<link rel="stylesheet" href="_content/ExGrid/ex-grid.min#[.{fingerprint}].css" />
<script type="importmap"></script>
```

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

For data that changes while it is read, give the source a Row Key:
`GridSource.From(trades, t => t.TradeId)` takes Change Batches (`Apply`) and whole new lists
(`ReplaceAll`, shown in the list's order), and `GridSource.Fetch(..., rowKey: t => t.TradeId)`
reads its Window again when told `NotifyChanged()` — or `NotifyChanged(bookedIds)`, naming the
rows that were added. Either gathers the changes, keeps the selection while only values change,
and answers the grid's `CellChangedAt` with the cells whose text changed.

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
