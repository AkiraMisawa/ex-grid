# ExSheet.Xlsx

ExSheet's **`.xlsx` support**: a workbook read into a **Workbook Document**, beside an **Open
Report** of what the file held that was not carried, and a Workbook written back to a file. It
references no component, so a server converts files with no Blazor in the process; the file itself
is read and written by `ExGrid.Data.Xlsx`.

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

- **Cell Formats cross both ways** at cell, row and column level — Number Formats the engine reads,
  horizontal alignment, the Font's colour and emphasis, solid fills and borders — with column
  widths in Excel's own unit. A theme colour is carried as the RGB value the file's theme gives it.
- **Saving writes the Workbook as it stands**: every Sheet, each Formula with the Value it shows,
  Excel's prefixes put back, and the workbook marked to be calculated in full when Excel opens it,
  so a program that does not calculate still reads the Values the user saw. Each **Linked Table**
  is written as an Excel Table on a worksheet of its name, holding its rows as they stand, so that
  `Positions[PV]` keeps working in Excel. What cannot be written faithfully is refused by name, and
  nothing is written.

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

## Saving a workbook

```csharp
// Written beside the file and moved over it, so a refused Workbook leaves no empty or partial book.xlsx.
var temporary = "book.xlsx.tmp";
try
{
    await using (var file = File.Create(temporary))
        await XlsxWorkbookWriter.WriteAsync(workbook, file);
    File.Move(temporary, "book.xlsx", overwrite: true);
}
catch (XlsxSaveException refused)
{
    Console.WriteLine(refused.Message);   // Sheet 'Trades', cell B2 (=C2+1): The cell shows #CIRC! …
}
finally
{
    File.Delete(temporary);
}
```

**The Workbook is read whole before the write first yields**, so the file is the Workbook as it
stood when the call was made; the package is held in memory, compressed, and handed to the stream
asynchronously, which an ASP.NET response body accepts.

- **Every Formula is written with its Value**, `RAND()`'s included, and as a dynamic array over the
  cells it spills into, so Excel computes it as ExSheet does.
- **Each Linked Table is written as an Excel Table** of its name, on a worksheet of its name after
  the Sheets: its columns' names in a header at A1, its rows below as they stand when the call is
  made. A Formula that reads it is written as it is (`SUM(Positions[PV])`), so it reads the table in
  Excel. **The file opens in Excel, and opens in ExSheet only once ExSheet reads Excel Tables**:
  nothing in a file can say that a table is the application's data, so the worksheet is not read
  back as a Linked Table, and a Formula that reads an Excel Table is refused on opening.
- **Refused by name, with nothing written:** a Linked Table that has not answered (its Formulas
  show `#GETTING_DATA`), one that holds no rows (an Excel Table holds at least one, and its Formulas
  would answer otherwise), more rows than a worksheet holds under its header (1,048,575) or more
  columns than its 16,384, and one whose name is already a Sheet's, one a worksheet cannot have or
  one Excel gives no Table (`A01`); a Formula that reads a Linked Table or a column the Workbook does
  not declare, a column whose name begins or ends with a space or holds `@`, a tab, a line break or
  one of `, : . " { } $ ^ & * + = - < > /` (until Excel is asked how a file stores one), or a
  Linked Table's name on its own (`=SUM(Positions)`, `#NAME?` in ExSheet and the table in Excel); a
  cell that shows `#CIRC!` (a file cannot hold it,
  and Excel would show 0), a Formula Excel
  would not take (longer than its 8,192 characters, a text constant over 255, calls nested past
  64 levels, a call of more than 255 arguments, a name a `LET` binds or a `LAMBDA` takes called as
  a function), text longer than its 32,767, half of a surrogate pair, a Sheet's name a file cannot
  carry, more cells than `XlsxSaveOptions.MaxCells`, and more uncompressed bytes than
  `XlsxSaveOptions.MaxUncompressedBytes`. Both limits are by default the most a file is read with,
  so what is written opens again; a Sheet formatted on many rows and many columns counts a cell
  wherever a formatted row crosses a formatted column that records a part the row does not.
