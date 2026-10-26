# Differential runs

The Script Core's formula engine held to `ExSheet.Engine` on random Sheets (ADR-0370, section 2), where
the shared cases (`tests/cases/sheet/`, `tests/cases/README.md`) hold it on chosen ones. Each run draws its
Sheets from a seed, asks `tools/sheet-oracle` what the C# engine answers, builds the same Sheet here
(`sheet.ts`), and lists every cell, refused entry and refused format code where the two part. Every
difference is a defect in one engine or the other: it is fixed in the engine that is wrong and written
into `tests/cases/sheet/differential-findings.json`, cut down to the Formula that shows it.

| File | Holds |
|---|---|
| `math.differential.ts` | Excel's arithmetic: random nests of the numeric functions and operators over numbers crowded near the places the last bit or a rounding goes wrong, each Value compared by its 64 bits |
| `number-text.differential.ts` | Number to text: random numbers under random Number Formats, at no width and at random column widths, and through `TEXT` and `&` |
| `workbook.differential.ts` | Whole Sheets: random texts typed under random cultures (numbers in the culture's spelling, dates, times, percentages, currencies, booleans, Error Values, words) and random Formulas over them, spills included |

A Sheet is a request: its culture, the texts typed into it in order, the format codes set on ranges, the
cells to read and, now and then, a column width. A number is compared by its bits, so a last bit, a `-0`
or an overflow shows; its `shown` text is each side's own round-trip spelling, there for the reader only.
The cultures are those the Script Core holds data for.

## Running one

```sh
nix develop -c dotnet build tools/sheet-oracle
cd src/npm/sheet-engine
EXGRID_SHEET_ORACLE="dotnet ../../../tools/sheet-oracle/bin/Debug/net10.0/ExSheet.Oracle.dll serve" \
  nix develop .#browser -c npm run differential
```

`EXGRID_SHEET_ORACLE` is the command that starts the oracle's `serve`, which answers one JSON request a
line. A run prints its seed in the test's name; `EXGRID_SHEET_SEED` repeats it. A test's message shows the
first differences; `EXGRID_SHEET_DIFF_OUT` names a file every difference is written to, a line each.
