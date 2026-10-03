# TEXT reads its format code in the invariant spelling, and shows a Value as a cell format would

*(Decided with the user on 2026-10-03, from the function catalogue's P1 Decide list,
`docs/specs/exsheet-functions/spec.md`.)*

`TEXT(value, format_text)` formats a Value with a number format code given as text. Excel reads
that code in the system's locale: German Excel writes a year `"JJJJ"`, and `"yyyy"` there is not a
year. The code is a string inside the Formula, so a workbook written in one locale reads its `TEXT`
codes differently in another.

[ADR-0047](./0047-the-formula-engine-is-exsheets-own-and-answers-as-excel-or-not-at-all.md) writes
every Formula in one spelling, whatever the Sheet's culture: English function names, `,` between
arguments, `.` as the decimal separator. A `TEXT` code read in the Sheet's culture would make the
same Formula mean two things.

## The decision

- **`format_text` is read in the invariant spelling, under every culture.** It is the code a cell
  format records ([ADR-0071](./0071-a-sheets-cell-format-is-document-data-painted-on-white-paper.md),
  `NumberFormat`): `y m d h s` for dates and times, `,` grouping thousands, `.` marking decimals.
  `TEXT(A1,"yyyy-mm-dd")` is a date under en-US, ja-JP and de-DE alike.
- **The result is what a cell in that format shows, under the Sheet's culture.** Separators, month
  and day names are the culture's, as a formatted cell's are. `TEXT(1234.5,"#,##0.00")` is
  `1,234.50` under en-US and `1.234,50` under de-DE. One renderer serves both, so `TEXT` and a cell
  format cannot drift apart.
- **A code is read as written, never as the built-in format it spells.** `"m/d/yyyy"` is Excel's
  built-in short date, which a cell shows in the culture's own pattern (`2026/09/26` under ja-JP).
  Typed into `TEXT`, it is a code of its own and shows `9/26/2026`, as written.
- **Width plays no part.** A number never becomes `####` for want of a column. A Value that the
  format cannot show at all, such as a negative number in a date format, is `#VALUE!`.
- **A colour in the code is ignored.** `TEXT` returns text, and text carries no colour.
- **The value is coerced as Excel's `TEXT` coerces it.** A blank is 0. Text that reads as a number
  under the Sheet's culture is that number. Other text goes to the code's text section, or stays as
  it is when there is none. A boolean stays `TRUE` or `FALSE`. An Error Value is the result.

## Refused until a Windows run answers

Under ADR-0047's rule, a code or a Value whose Excel answer is not known gives an Error Value, never
another answer:

- **A code outside `NumberFormat`'s subset** is `#VALUE!`: conditions, locale and elapsed-time
  brackets, fractions, `*` fill, and the rest `NumberFormat` refuses for a cell.
- **`General` with a number whose General text is longer than 11 characters** is `#VALUE!`. Excel
  is reported to fit `TEXT`'s General to a width, as it fits a cell's. Which width it uses is to be
  observed. Below 11 characters every reading agrees.
- **An empty `format_text`** is `#VALUE!`.

## What differs from Excel, by decision

- **Under a culture whose Excel spells codes in its own language** (de-DE, fr-FR, …), a Formula
  written there for Excel uses the local codes. ExSheet reads them as invariant codes. `"JJJJ"` is
  not a code ExSheet reads, so it is refused with `#VALUE!` rather than shown some other way. Under
  en-US, en-GB and ja-JP, Excel's codes are the invariant ones, and the answers are the same.

## Considered options

- **The Sheet's culture, as Excel does.** Rejected: the same Formula would mean two things in two
  cultures. It also breaks ADR-0047's one spelling for every Formula.
- **Both spellings, the invariant one first.** Rejected: a code can read differently in the two
  spellings. Under de-DE, `"0,00"` is two decimals to German Excel and a thousands separator to the
  invariant reading. Guessing between them is the quiet wrong answer this project refuses.

## Consequences

- `TEXT` joins the declared set (ADR-0047's dated additions). Its row in the catalogue is
  Supported, and its cases are in the Excel case corpus. The cases not settled by Microsoft's
  documentation are marked `uncertain` for the next Windows run.
- A Consumer who localises the Formula Bar in future (ADR-0047 keeps localised syntax out of the
  first version) translates `TEXT` codes along with function names. Both are display over the same
  stored form.
