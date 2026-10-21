# 01: The file layer reads and writes an `.xlsx` package

Status: done

**Why:** [ADR-0330](../../../adr/0330-exsheet-opens-and-saves-xlsx-with-a-reader-and-writer-of-its-own.md) §1, §2 and §9.

- [x] A new shipped package `ExGrid.Data.Xlsx` (refs `ExGrid.Data` only), packed and smoke-tested
- [x] Reads the workbook part, the worksheets in order with their names and the one shown, shared strings, and each cell's place, type, value and Formula text, streamed
- [x] Reads the styles part as far as ADR-0330 §4 carries (Number Formats, fonts, fills, borders, alignment) and the theme's colours
- [x] Writes the same back
- [x] Refuses by name: not a zip, `.xlsm`/`.xlsb`/`.xls`, an encrypted file, a DTD, beyond the size or cell-count limit
- [x] The default limits set from a measurement recorded in the ticket
- [x] Layer 1 tests, named for ADR-0330; Change Note

**Blocked by:** None (can start immediately).

**Settled while building (2026-10-21):** the package is `src/ExGrid.Data.Xlsx`, net10.0, referencing
`ExGrid.Data` alone at exactly its own version, and nothing from NuGet: `System.IO.Compression` and
`XmlReader`/`XmlWriter` with DTD processing prohibited. Its model stays at the file's level — cell
types, raw values, Formula text with its shared or array kind and the dynamic-array mark, style
indices, column widths, the styles' lists and the theme's twelve colours — and mapping it to a
Sheet is ticket 02's. `XlsxReader.OpenAsync` reads the workbook, its relationships, the shared
strings (held whole), the styles and the theme; `OpenSheet` streams a worksheet's cells in batches of
4,096, yielding after each 30 ms slice and reporting `XlsxProgress`, as ExGrid.Data's loads do.
`XlsxWriter` writes the same model back, streaming each sheet into the zip and draining it to the
output asynchronously, so an ASP.NET body that refuses synchronous writes takes it. A refusal is an
`XlsxException` with an `XlsxRefusal` reason and the part, sheet and cell it concerns: `NotAZip`,
`NotAWorkbook`, `MacroEnabled`, `Binary`, `Legacy` (an OLE file that is not encrypted), `Encrypted`
(an OLE file holding `EncryptedPackage`), `StrictOpenXml`, `Dtd`, `Malformed`, `TooLarge` and
`TooManyCells`. A zip whose deflated entry inflates past the size it declares is cut there by the
platform, and is refused as `Malformed`, naming the part; a stored entry, which the platform reads to
its compressed size, is refused as `TooLarge` past what it declares. The size limit counts what the
zip declares, and no part is read past that.

The defaults were measured with `tests/ExGrid.Data.Xlsx.Tests/Measure` (run by hand, never gated) on
a 4-vCPU Intel Xeon, .NET 10.0.7, Ubuntu 24.04, CoreCLR Release. Each workbook has ten columns a row
— four numbers, three texts, two Formulas and a date, most styled — and is read in a process of its
own:

| Cells | File | Sheet XML | Write | Read | Allocated by the read | Held mid-read | Peak working set |
|---|---|---|---|---|---|---|---|
| 100,000 | 0.7 MiB | 3.8 MiB | 0.18 s | 0.18 s | 14 MiB | 0.5 MiB | 52 MiB |
| 1,000,000 | 6.9 MiB | 39.3 MiB | 1.21 s | 0.65 s | 146 MiB | — | 123 MiB |
| 5,000,000 | 34.4 MiB | 203 MiB | 4.69 s | 1.91 s | 755 MiB | — | 290 MiB |
| 10,000,000 | 68.9 MiB | 409 MiB | 9.46 s | 3.30 s | 1,519 MiB | 0.5 MiB | 290 MiB |

Texts drawn from a pool of 1,000. With every text its own, the worst case for the shared strings:
1,000,000 cells are 40.1 MiB of sheet XML and 10.6 MiB of shared strings, read in 0.93 s holding
21 MiB; 10,000,000 cells are 419.5 MiB and 105.9 MiB, written in 13.5 s and read in 4.95 s,
holding 206.5 MiB (the shared strings, at about twice their XML) with a peak working set of
564 MiB. A cell costs about 43 bytes of XML. So the defaults are **`MaxCells` = 10,000,000**, a
full-height sheet of ten columns, read by the file layer in 3–5 s on CoreCLR, and
**`MaxUncompressedBytes` = 1 GiB**, which holds that sheet's XML and its shared strings with room
for heavier markup, and refuses a zip bomb before it is inflated. Both are `XlsxReadOptions`
properties. WebAssembly was not measured; at the ratio the CSV reader measured (about 15 times
CoreCLR), ten million cells would take about 50 s there, and ExSheet's opening (ticket 02 on) may
well want a lower `MaxCells` of its own, set from what it builds per cell.

Theme colours resolve with ECMA-376's tint (§18.8.19) in HSL. It gives Excel's greys exactly
(`F2F2F2`, `D9D9D9`, `BFBFBF`, `A6A6A6`, `808080`, `595959`, `404040`, `262626`, `0D0D0D`), but on
a coloured accent it differs from what Excel's palette shows by one or two in a channel (accent 1,
`4472C4`, at +0.8 gives `DAE3F3` where Excel shows `D9E1F2`); no variant of the algorithm tried
matched both. That is left for ticket 07's run on Windows to read against Excel.

The package is packed, its nuspec read back and a workbook written and read through it by
`tests/ExGrid.PackageSmoke/check.sh`, but into `.feed-unreleased`, not into the feed the release
publishes: the release publishes exactly the packages ADR-0042's table names, and the Docs Site's
Packages page is generated from the same list, so adding `ExGrid.Data.Xlsx` to the release is an
ADR-0042 change, left to be decided.
