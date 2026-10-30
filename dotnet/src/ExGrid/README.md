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

> **The API may still change.** While the version is `0.x`, it may change from one version to the
> next; each version's [Release Notes](https://akiramisawa.github.io/ex-grid/release-notes) say what
> changed. A prerelease, a version with a suffix such as `-rc.1`, passed every test layer in CI on
> the commit it was built from, and its Release Notes say what the project's
> [Definition of Done](https://github.com/AkiraMisawa/ex-grid/blob/main/docs/definition-of-done.md)
> still owed. A version without a suffix is published only once the Definition of Done is signed off
> on it.

## Requirements

- **.NET 10 or newer.** The package targets `net10.0` and loads unchanged in newer applications.
- **Chrome or Edge.** They are the supported browsers; Safari and Firefox are out of scope.
- **An interactive render mode.** The grid handles keys, the pointer and the clipboard. It is
  verified under WebAssembly. Under Blazor Server every browser test also runs, against a
  Server host with a simulated round trip, but support is not declared until the last runs it
  owes are in.

## Install

```sh
dotnet add package ExGrid
```

Add `--prerelease` for a prerelease, a version with a suffix such as `-rc.1`.

Place the stylesheet component in the head. In a Blazor Web App, in `App.razor`:

```razor
@using ExGrid.Components

<head>
    ...
    <ExGridStylesheet />
    <HeadOutlet />
</head>
```

In a standalone WebAssembly application, as a root component in `Program.cs`, beside `HeadOutlet`
(not inside a `<HeadContent>`, which a page with one of its own would displace):

```csharp
builder.RootComponents.Add<HeadOutlet>("head::after");
builder.RootComponents.Add<ExGrid.Components.ExGridStylesheet>("head::after");
```

The grid loads its own script module; there is nothing else to include.

The script and the stylesheet are minified, with source maps, and the grid asks for the files it
was built with (ADR-0123): at the fingerprinted address .NET's static assets give each file in a
Blazor Web App, and otherwise at the plain address with the file's content hash as `?v=`. A new
version is therefore a new address, and a normal reload after an upgrade brings the new files, on
GitHub Pages too. A standalone application's first visit can show the grid unstyled for a moment
until its stylesheet arrives. A page that still holds another version's script shows "This page
holds another version of the grid's script. Reload the page." in place of the rows, and another
version's stylesheet shows a notice of its own. The
[Installation](https://akiramisawa.github.io/ex-grid/getting-started) page says more.

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
elsewhere, `GridSource.Fetch` asks your function for each range of rows the grid needs, with the
Sorts and the Filter in force, and reads `readAheadRows` ahead so the next Window can land before
the Viewport runs out of rows.

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
