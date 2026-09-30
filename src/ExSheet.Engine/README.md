# ExSheet.Engine

The formula engine of ExSheet, with no UI and no dependency. It holds a **Sheet** — Excel's
extent, 1,048,576 rows by 16,384 columns, held sparsely — and computes the **Value** of every
cell from its **Entry**:

- Formulas in Excel's syntax, written the same way in every culture (`,` between arguments, `.`
  as the decimal separator, English function names)
- a declared set of functions, each giving Excel's result; a function outside the set is `#NAME?`,
  never an approximation
- recalculation of only what a change reaches, publishing Values only from a completed
  recalculation; a circular reference is `#CIRC!` in every cell of the cycle and every cell that
  depends on it

The **Sheet Document** is the Sheet's serialisable form (version 7, which also reads versions 1
to 6), with the formats set on its columns, rows and cells and the widths recorded on its columns. It holds Entries and never Values, so
anyone who wants a saved Sheet's numbers runs this engine — on a server as in the browser, with
the same result.

> **This is a prerelease (`0.x`).** ExSheet is built alongside ExGrid and is not part of its
> release. The API may change between prereleases.

## Requirements

- **.NET 10 or newer.** The package targets `net10.0` and has no dependencies.

## Install

```sh
dotnet add package ExSheet.Engine --prerelease
```

## Computing a Sheet

```csharp
using System.Globalization;
using ExSheet.Engine;

var sheet = new Sheet(CultureInfo.GetCultureInfo("en-US"));
sheet.Enter(CellAddress.Parse("A1"), "2");
sheet.Enter(CellAddress.Parse("B1"), "3");
var change = sheet.Enter(CellAddress.Parse("C1"), "=A1+B1");

sheet.GetValue(CellAddress.Parse("C1"));   // 5
change.ValueChanges;                        // C1
```

`Enter` reads text as a user typed it: text beginning with `=` is a Formula, and anything else is
a constant read under the Sheet's culture and recorded already parsed. Typing into a General cell
can give it a format, as in Excel: a date or a percentage typed as one, and a Formula of simple
arithmetic (`+`, `-`, single-cell References, parentheses) takes the format of the first
formatted cell it reads, so `=A1+7` over a date shows a date. It happens once, at entry. A Formula keeps the
whitespace it was typed with where Excel keeps it — before a token, but not at the end nor before
a `,` — and a tab between tokens is refused, as Excel refuses it. Its tokens are written in
Excel's spelling (`= sum( a1 , 2 ) ` is kept as `= SUM( A1, 2 )`), and rewriting its References
changes only the Reference tokens; one rewritten to `#REF!` takes the whitespace before it along
and keeps its qualifier (`Sheet1!#REF!`). References are written as Excel writes them: a range
spanning every row as whole columns (`A1:A1048576` is `A:A`), one spanning every column as whole
rows (`A:XFD` is `$1:$1048576`), and a structured reference in single brackets
(`Positions[[Market Value]]` is `Positions[Market Value]`).

A Sheet has a name, `Sheet1` unless it is given one (`new Sheet(culture, "Risk")`), and
`SheetEdit.Rename` changes it as Excel does, rewriting every Reference qualified with the old
name. A Reference qualified with the Sheet's own name (`Sheet1!A1`, `'My Sheet'!A1`) reads its
cells; any other qualifier is `#REF!`. `Sheet.IsValidName` applies Excel's rules: 1 to 31
characters, none of `: \ / ? * [ ]`, not beginning or ending with `'`, and not `History`.

## What a cell shows in its column

`Sheet.GetDisplay(address, width)` is what the cell shows in a column `width` characters wide —
Excel's unit, the number of digits of the font that fit between the cell's paddings, with
`Sheet.DefaultColumnWidth` (8.43) as Excel's default. Every character of a number's text is
charged one digit width, so a column holds `⌊width⌋` characters. A component converts from its
pixel width as `(columnPx − 2 × paddingPx) / digitWidthPx`.

```csharp
sheet.Enter(CellAddress.Parse("A1"), "=1/3");
sheet.GetDisplay(CellAddress.Parse("A1"), Sheet.DefaultColumnWidth).Text;   // 0.333333
sheet.GetDisplay(CellAddress.Parse("A1")).Text;                             // 0.333333333333333
```

- **General fits the column**, as Excel's does: decimals are rounded to the width, and the number
  is written in scientific notation where its integer part does not fit, where it has twelve or
  more digits, or where that shows it more closely. It never takes more than eleven characters
  besides a minus sign. Where not even `1E+08` fits, the cell `CannotShow` (`####`).
- **Any other format is never shortened.** A formatted number or a date longer than the width
  cannot show, which is Excel's `####` (ADR-0016).
- `GetDisplay(address)` is the text at no width: General in full, at fifteen significant digits,
  for an accessible name or a copy.

`Sheet.GetWidthOnEntry(address)` is the width, in characters, that the number typed into a cell
needs, for widening a column at its default width or one widened by entry, as Excel does: every integer digit
without scientific notation (decimals are rounded instead), the scientific form of twelve or more
digits, or the whole text of a date or another formatted number. It is `null` for text, a
Formula and anything else that never widens a column. Which of these rules Microsoft documents,
and which are still to be observed in Excel, the case corpus says per case.

## Functions

`DeclaredFunction.All` lists the declared set, with each function's arguments as Excel documents
them:

| Function | Arguments |
|---|---|
| `SUM`, `AVERAGE`, `MIN`, `MAX` | `number1, [number2], ...` |
| `COUNT`, `COUNTA` | `value1, [value2], ...` |
| `IF` | `logical_test, value_if_true, [value_if_false]` |
| `ROUND` | `number, num_digits` |
| `IFERROR` | `value, value_if_error` |
| `ISERROR` | `value` |
| `XLOOKUP` | `lookup_value, lookup_array, return_array, [if_not_found], [match_mode], [search_mode]` |

Where the engine cannot give Excel's answer, it gives an Error Value and never a different
answer:

- **No spilled arrays.** A Formula whose result would be a multi-cell range, or an operator applied
  to one, is `#VALUE!`; so is an `XLOOKUP` whose return array is more than one cell across.
  `IFERROR` and `ISERROR` do not turn that refusal into a fallback.
- **`XLOOKUP`'s binary search** (`search_mode` 2 and −2) answers only over a lookup array sorted
  as the mode says — one kind of value, no blanks, text of ASCII letters, digits and spaces; it is
  `#VALUE!` otherwise. Over duplicate keys it returns the one Excel was observed to: an equal key
  is the first of its run ascending and the last descending.
- **`#CIRC!`** is shown by every cell of a circular reference and every Formula that reads one,
  where Excel shows 0. `IFERROR` does not catch it.
- **`#GETTING_DATA`**, a Linked Table's data on its way, is not an error to `IFERROR` and
  `ISERROR`, where Excel's are: a fallback never stands in for data that has not arrived.

## Operations, and undoing them

Every user operation is a `SheetEdit`, and `Sheet.Do` returns the one `SheetStep` that undoes and
redoes it, each with one recalculation. The stack of steps is the caller's.

```csharp
var step = sheet.Do(SheetEdit.InsertRows(row: 2));   // References are rewritten to keep naming the same cells
step.Undo();                                         // Entries, formats and References exactly as they were
```

- **Insertion and deletion** of rows and columns rewrite every Reference, relative and absolute
  alike; a range grows or shrinks as Excel's does, and a Reference whose cells are all deleted is
  written `#REF!` in the stored Formula. An insertion that would push an Entry off the Sheet's
  edge is refused (`SheetRefusedException`), as Excel refuses it; one that pushes only the cells a
  Reference names off is done, as in Excel, with a range cut at the edge and a Reference left
  with no cells written `#REF!`. Inserted rows take
  the number format and alignment of the row above — its cells' and the row's own — and inserted
  columns those of the column to the left, as Excel's default does; Entries are never copied.
  Formats set on rows and columns move with them, and so do column widths.
- **Column widths** are part of the Sheet Document, in characters as Excel counts them.
  A width is one of three kinds (ADR-0046, 2026-09-28): **default** (never set, not recorded),
  **widened by entry** (`SheetColumnWidthKind.WidenedByEntry`: an entry widened the column to it;
  a longer entry widens it again) or **set by the user** (`SheetColumnWidthKind.SetByUser`: a
  drag, a size to fit, a command; an entry never widens it). Both recorded kinds are marked
  custom (`IsCustom`), as Excel's file marks them (`customWidth`).
  `SheetEdit.SetColumnWidth(range, width)` sets the user's width on every column the range spans,
  from more than 0 to 255 characters; `null` puts them back at the default width, which is not
  recorded. `SheetEdit.SetAutomaticColumnWidth(range, width)` records a width widened by entry.
  `Sheet.GetColumnWidth(column)` is the recorded `SheetColumnWidth` — `Width`, `Kind`,
  `IsSetByUser` and `IsCustom` — or `null`. An inserted column takes the width of the column to
  its left, of its kind, as it takes its formats; a deleted column's width goes with it; undo puts
  back both. `SheetChange.Columns` names the columns whose width, or its kind, a change (or its
  undo) changed. A version 4 document recorded only widths the user set, and they open as the
  user's; a version 5 document's custom widths open as the user's and its others as widened by
  entry.
- **Formats** are set at three levels, cell over row over column, as in Excel.
  `SheetEdit.SetFormat(range, format)` and `SheetEdit.SetAlignment(range, alignment)` record
  whole columns (`CellRange.Parse("B:D")`) and whole rows (`"2:4"`) as one entry each, and the
  whole Sheet (`"A:XFD"`) as one run; a cell inside that set its own format takes the new one,
  as Excel's do. Any other range sets its cells. `Sheet.GetFormat` and `Sheet.GetAlignment`
  answer what takes effect; `GetRowFormat`, `GetColumnFormat` and their alignment counterparts
  what a level sets. The per-cell forms taking addresses keep working.
  `SheetEdit.SetStyle(ranges, format, alignment)` sets a number format, an alignment or both on
  several ranges — a selection of several rectangles, whole columns and rows among them — as one
  step; there `null` leaves a property as it is, and `NumberFormat.General` sets General.
- **Copy and paste.** `Sheet.Copy(range)` gives the Entries (`SheetBlock`) for a paste inside
  the Sheet, where relative References shift by the distance pasted, and the Values for anywhere
  else: `Text` as the cells show them, `Html` unformatted. `SheetEdit.PasteText` reads each
  pasted field as if typed under the Sheet's culture, except that a field that cannot be read as
  a Formula (`=1+`) is taken as text, as Excel takes it. A copy that reaches a `#GETTING_DATA`
  Value is refused.
- **Fill.** `SheetEdit.Fill(source, target, direction)` fills as Excel does for copies (with
  References shifted), a linear trend from two or more numbers (held at 15 significant digits,
  as Excel holds it), and a single date by day, and
  refuses every other pattern — `Item 1`, day and month names, several dates — rather than fill
  it with copies.
- `Sheet.Check(edit)` says whether an edit would be refused, without doing it.

## Linked Tables

Data the application holds reaches Formulas as a Linked Table, pushed whole and read by
structured reference and by key:

```csharp
sheet.DeclareLinkedTable("Positions", ["Id", "PV"], key: "Id");
sheet.Enter(CellAddress.Parse("A1"), "=XLOOKUP(\"R-4471\", Positions[Id], Positions[PV])");
// A1 is #GETTING_DATA until the first snapshot arrives.
sheet.PushLinkedTable("Positions", [[Value.FromText("R-4471"), Value.FromNumber(250.5)]]);
```

A snapshot replaces the last in one step and recalculates only the Formulas that read the table.
The Sheet Document records each table's declaration — its name, column names and key — and never
its rows: a Sheet opened from one already has the tables declared, and their readers show
`#GETTING_DATA` until the first snapshot is pushed. Declare your tables at start-up regardless:
the same declaration again changes nothing, and one with other columns or another key replaces
the held one, dropping its rows so readers wait again. A table is never undeclared.

The **key** is optional, and names one of the columns. Every snapshot of a keyed table is checked:
no key may appear twice, compared as `XLOOKUP`'s exact match compares, so `r-4471` and `R-4471` are
one key while the number 1 and the text `1` are two. A blank key is not a key. A snapshot in which a
key repeats throws `RepeatedKeyException`, naming the table, the key column and the key. The
previous snapshot is not kept: the table waits again, and every reader shows `#GETTING_DATA`, which
`IFERROR` does not catch. `RepeatedKeyException.Change` says which cells changed with it. A table
whose rows are told apart by several columns needs a column that joins them (`ACME|5Y`) as its key.
A column the table does not have is `#REF!`; a column used where one Value is wanted gives its
Value when it has exactly one row, and `#VALUE!` otherwise.

## Formula entry

`FormulaEntry` answers, over a Formula's unfinished text and its caret, what an editor needs:
completion candidates (`Sheet.Complete` adds the Sheet's Linked Tables to the functions), the
argument hint, whether a Reference can be written at the caret (Point mode), the Reference
text for a range, what F4 makes of the Reference at the caret, and every Reference in the text
with the cells it names or the Linked Table column it reads, for Reference Outlines
(`Sheet.References` reads a Sheet qualifier against the Sheet's own name).

## More

The decisions behind the engine are ADR-0046 to ADR-0051 in
[`docs/adr/`](https://github.com/AkiraMisawa/ex-grid/tree/main/docs/adr), and the terms — Sheet,
Entry, Value, Formula, Reference, Error Value, Sheet Document — are defined in
[`CONTEXT.md`](https://github.com/AkiraMisawa/ex-grid/blob/main/CONTEXT.md).
