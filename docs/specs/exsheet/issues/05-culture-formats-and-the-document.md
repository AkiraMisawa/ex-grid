# 05: Culture, number formats, dates, and the versioned Sheet Document

Status: done

**What to build:** A Sheet has a declared culture that decides how typed constants are read and how Values show.
Constants are recorded parsed. Dates are serials in Excel's 1900 system, including its
29 February 1900. Excel's number format codes and horizontal alignment are settable per cell.
Values show at most 15 significant digits, and `####` when they do not fit. The Sheet Document
carries a version and the culture, and a reader refuses a version it does not know.

**Blocked by:** 02

- [x] `1,234.5` under `en-US` and `2026/9/26` under `ja-JP` become a number and a date (ADR-0048)
- [x] A document saved under `en-US` and opened under `de-DE` shows the same numbers (ADR-0048)
- [x] Date serials match Excel's around 1900-02-29 (ADR-0047)
- [x] Format codes for numbers, percent, thousands and dates render as Excel's under the culture
- [ ] A number too wide for its column is `####` (ADR-0016)
- [x] An unknown document version is refused, not guessed at (ADR-0048)

## Comments

2026-09-27, engine half: a `Sheet` has a declared culture; `Sheet.Enter` reads typed numbers
(the culture's group and decimal separators, parentheses, exponent, `%`, fifteen significant
digits kept), dates in the culture's day order (and year-first ISO), times, booleans and Error
Values, and records them parsed; a date or percentage typed into a General cell gives it the
implied format, as Excel does. Serials follow Excel's 1900 system, 29 February 1900 included
(serial 60), and serial 0 shows as 0 January 1900. `NumberFormat` reads a declared subset of
Excel's codes (sections, `0 # ?`, thousands and scaling commas, `%`, `E+00`, `@`, literals, and
`y m d h s AM/PM`) and refuses the rest by name; `Sheet.GetDisplay` formats under the culture
with at most fifteen significant digits and resolves General alignment. Alignment is settable
per cell. The Sheet Document records version 1, the culture, Entries, formats and alignment,
and refuses an unknown version, a missing one, and anything version 1 does not define
(`CultureTests`, `NumberFormatTests`, `SheetDocumentTests`). **What remains is the component's:**
`####` for a number too wide for its column (ADR-0016) — the engine reports
`CellDisplay.IsNumber`, and `CellDisplay.CannotShow` for a date no width can show — and the
commands that set formats and alignment.
