# ExGrid.Data.Xlsx

The cells of an **`.xlsx` workbook**, read and written as a stream, with **no dependency** outside
.NET: `System.IO.Compression` reads the zip, and `XmlReader` and `XmlWriter` its parts. It is the
file layer of the Ex family's `.xlsx` support: ExSheet maps a workbook to its own through
`ExSheet.Xlsx`, and a server converts files with no Blazor in the process.

- **The model is the file's own.** A number is its text, a Formula its stored text (with Excel's
  `_xlfn.` prefixes), a style an index into the styles part. Nothing is interpreted.
- **A worksheet is read as a stream**, a batch of cells at a time, never as a document. The shared
  strings are the one part held whole, because cells refer to them by index.
- **Reading works in slices**, reporting progress and yielding between them — the shared strings
  as `OpenAsync` reads them, then each worksheet — so a browser application keeps painting while a
  large file is read.
- **A file it cannot read faithfully is refused by name**, and nothing of it is read: not a zip,
  a workbook with macros (`.xlsm`), a binary workbook (`.xlsb`), an Excel 97–2003 workbook
  (`.xls`), an encrypted workbook, Strict Open XML, a part that declares a DTD, a part that is not
  well-formed, and a file past the size or cell-count limit.

> **`ExGrid.Data.Xlsx` ships beside `ExGrid.Data`, at the same version.** While the version is
> `0.x`, the API may change from one version to the next; each version's
> [Release Notes](https://akiramisawa.github.io/ex-grid/release-notes) say what changed.

## Requirements

- **.NET 10 or newer.** The package targets `net10.0`.
- **`ExGrid.Data`, at exactly this package's version.**

## Install

```sh
dotnet add package ExGrid.Data.Xlsx
```

Add `--prerelease` for a prerelease, a version with a suffix such as `-rc.1`.

## Reading

```csharp
using ExGrid.Data.Xlsx;

await using var file = File.OpenRead("book.xlsx");
using var book = await XlsxReader.OpenAsync(file, new XlsxReadOptions { Progress = progress }, token);

Console.WriteLine($"{book.Sheets.Count} sheets, '{book.Sheets[book.ActiveSheet].Name}' shown");
using var sheet = book.OpenSheet(0);
while (await sheet.ReadAsync())
{
    foreach (var cell in sheet.Cells)
        Console.WriteLine($"{cell.Reference}: {cell.Formula?.Text ?? cell.Text ?? cell.Value}");
}
```

`OpenAsync` reads the workbook part (the sheets in order, the one shown, the 1904 date system and
the defined names), the shared strings, the styles and the theme. `OpenSheet` then reads one
worksheet's cells: its column widths come first, and `OtherElements` lists what else it holds
(merged cells, conditional formatting, notes and the rest), none of which is read. What every file
carries and no sheet shows — page margins, phonetic settings, dismissed error marks, print settings
left at their defaults — is not listed, so an empty list is a worksheet that holds nothing else.

A stream that cannot seek — a browser's `IBrowserFile` stream, a request body — is copied into
memory first, as far as the size limit allows. An Excel 97–2003 or encrypted file is told apart
by its header and directory alone: only those sectors are read, and a stream that cannot seek is
read no further than the last of them.

### Limits

A workbook is untrusted input. Beyond either limit the file is refused by name, never read in part;
an application raises them for files it trusts.

| Option | Default | Counts |
|---|---|---|
| `MaxUncompressedBytes` | 1 GiB | every part's uncompressed bytes, as the zip declares them; a part is never read past what it declares |
| `MaxCells` | 10,000,000 | the cells of every worksheet, as they are read |

### Styles and colours

`Styles` holds the custom Number Formats, the fonts (colour, bold, italic, underline,
strikethrough, name and size), the fills, the borders (Excel's thirteen line styles) and the cell
formats with their alignment. A colour is kept as the file writes it — an RGB value, a theme slot,
an indexed entry or automatic, each with a tint — and `Theme.Resolve` gives the RGB value it
paints, through the workbook's own theme.

## Writing

```csharp
await using var output = File.Create("out.xlsx");
await using var book = XlsxWriter.Create(output, new XlsxWriteOptions { FullCalculationOnLoad = true });
var sheet = book.AddSheet("Trades", [new XlsxColumn(0, 0, Width: 18)]);
sheet.Write(new XlsxCell(0, 0, XlsxCellType.SharedString, null, Text: "Notional"));
sheet.Write(new XlsxCell(1, 0, XlsxCellType.Number, "1000000.25"));
sheet.Write(new XlsxCell(2, 0, XlsxCellType.Number, "1000000.25", Formula: new XlsxFormula("SUM(A2:A2)")));
await sheet.FlushAsync();   // between batches of a large sheet
await book.CompleteAsync();
```

Cells are written row by row, left to right. Text written as a shared string is gathered into the
shared strings part; every write to the output stream is asynchronous, so an ASP.NET Core response
body takes it directly.
