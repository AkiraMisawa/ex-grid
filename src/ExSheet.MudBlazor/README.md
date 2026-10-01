# ExSheet.MudBlazor

[ExSheet](https://www.nuget.org/packages/ExSheet) inside a MudBlazor application:

- **`MudSheetChrome`** — ExSheet's Format Cells drawn as a `MudDialog`, with MudBlazor's colour
  picker for More Colours, and every seam of the grid filled by
  [ExGrid.MudBlazor](https://www.nuget.org/packages/ExGrid.MudBlazor)'s `MudGridChrome`.
- **`mud-ex-sheet.css`** — lays out the dialog's contents and draws the palette in MudBlazor's
  palette variables.

What Format Cells offers and what OK sets do not change with the Chrome: ExSheet decides the tabs,
the categories, the palette and the line styles, and the same choices set the same Cell Format
under either Chrome. The dialog is page-level and modal; while it is open the keys are its own,
and when it closes the keyboard is the Sheet's again.

> **This is a prerelease (`0.x`).** ExSheet is built alongside ExGrid and is not part of its
> release. This package depends on exactly the ExSheet and ExGrid.MudBlazor versions it ships
> with, so upgrade them together.

## Install

```sh
dotnet add package ExSheet.MudBlazor --prerelease
```

This brings in ExSheet, ExGrid.MudBlazor and MudBlazor (9.0 or newer). Set MudBlazor up as usual:

- `builder.Services.AddMudServices()`
- MudBlazor's stylesheet and script
- `<MudThemeProvider />`, `<MudPopoverProvider />` and `<MudDialogProvider />` in your layout.
  Format Cells is a `MudDialog`; without a dialog provider it is refused by name when it first
  opens.

Then add the stylesheets, ExGrid's and ExSheet's as for any Sheet, and the two MudBlazor ones:

```html
<link rel="stylesheet" href="_content/ExGrid/ex-grid.css" />
<link rel="stylesheet" href="_content/ExSheet/ex-sheet.css" />
<link rel="stylesheet" href="_content/ExGrid.MudBlazor/mud-ex-grid.css" />
<link rel="stylesheet" href="_content/ExSheet.MudBlazor/mud-ex-sheet.css" />
```

## A Sheet on a paper

```razor
@using ExGrid.MudBlazor
@using ExSheet.Components
@using ExSheet.Engine
@using ExSheet.MudBlazor

<MudExGridPaper>
    <ExSheet @bind-Document="_document" Chrome="MudSheetChrome.Default" ViewportHeight="480" ViewportWidth="900" />
</MudExGridPaper>

@code {
    private SheetDocument? _document;   // null: an empty Sheet in the current culture
}
```

Format Cells opens from Ctrl+1, from "Format Cells…" in the Context Menu, and from
`OpenFormatCellsAsync()`. To word or decorate the grid's own seams, hand `MudSheetChrome` a
`MudGridChrome` of your own:

```csharp
private static readonly MudSheetChrome Chrome = new() { Grid = new MudGridChrome { Label = Words } };
```

## More

The seam is recorded in
[ADR-0063](https://github.com/AkiraMisawa/ex-grid/blob/main/docs/adr/0063-a-sheets-cell-format-is-document-data-painted-on-white-paper.md),
and why it is a package of its own in
[ADR-0019](https://github.com/AkiraMisawa/ex-grid/blob/main/docs/adr/0019-one-repository-many-packages.md).

MIT licensed.
