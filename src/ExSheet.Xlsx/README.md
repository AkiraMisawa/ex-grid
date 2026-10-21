# ExSheet.Xlsx

ExSheet's **`.xlsx` support**: a workbook read into a **Workbook Document**, beside an **Open
Report** of what the file held that was not carried. It references no component, so a server
converts files with no Blazor in the process; the file itself is read by `ExGrid.Data.Xlsx`.

- **Every worksheet becomes a Sheet**, in the file's order, and the worksheet shown is the Sheet
  shown.
- **Constants and Formulas are carried**: numbers, text, booleans and Error Values; Formulas with
  Excel's stored `_xlfn.`, `_xlws.` and `_xlpm.` prefixes taken off, shared formulas expanded into
  each cell's own, and a spilled array as its anchor's Formula. **No stored Value is taken**:
  opening the document computes every Value again.
- **A workbook on the 1904 date system** has its date serials moved to 1900's, which changes no
  date.
- **Anything that would change a Value is refused by name**, naming the Sheet, the cell and what it
  held, and nothing opens: a Formula the engine cannot read, a reference to a defined name, to
  another workbook or to an Excel Table, a legacy array formula, and every file the file layer
  refuses.
- **The Open Report** lists what was not carried — merged cells, conditional formatting, notes,
  hyperlinks, frozen panes, hidden rows and columns, row heights, defined names and the rest — and
  the cells whose Formulas name a function ExSheet does not support, which open as `#NAME?`. The
  package shows nothing; the application decides how to tell the user.

Cell formats and column widths are not carried yet: every cell opens in General.

> **ExSheet.Xlsx ships with ExSheet, at the same version.** While the version is `0.x`, the API
> may change from one version to the next; each version's
> [Release Notes](https://akiramisawa.github.io/ex-grid/release-notes) say what changed.

## Requirements

- **.NET 10 or newer.** The package targets `net10.0`.
- **`ExSheet.Engine` and `ExGrid.Data.Xlsx`, at exactly this package's version.**

## Install

```sh
dotnet add package ExSheet.Xlsx
```

Add `--prerelease` for a prerelease, a version with a suffix such as `-rc.1`.

## Opening a workbook

```csharp
using System.Globalization;
using ExSheet.Engine;
using ExSheet.Xlsx;

await using var file = File.OpenRead("book.xlsx");
try
{
    var (document, report) = await XlsxWorkbookReader.ReadAsync(file, CultureInfo.GetCultureInfo("en-GB"));
    foreach (var item in report.NotCarried) Console.WriteLine(item.Description);
    foreach (var cell in report.UnsupportedFunctions)
        Console.WriteLine($"{cell.Sheet}!{cell.Cell} names {string.Join(", ", cell.Functions)}, and shows #NAME?");
    var workbook = Workbook.Open(document);
}
catch (XlsxOpenException refused)
{
    Console.WriteLine(refused.Message);   // Sheet 'Trades', cell B7 (=Rate*A7): The Formula reads the defined name …
}
```

**The culture is the application's.** A workbook stores its numbers and dates in one form in every
language, so no Value read from it depends on a culture; what does is how the user's typing is read
afterwards, and how Values are shown. The application declares it, as a CSV's encoding is declared.

`XlsxReadOptions` passes the file layer's limits, progress and pacing through: the read goes a batch
of cells at a time, reports its progress and yields between slices, and a cancellation token stops
it between them. See `ExGrid.Data.Xlsx` for the limits and what they count.
