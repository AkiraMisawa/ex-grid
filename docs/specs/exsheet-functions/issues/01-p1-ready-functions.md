# 01: The P1 Ready functions, admitted against Microsoft's documentation

Status: done

**What to build:** the catalogue's P1 functions marked Ready, each giving Excel's result under
ADR-0047's rule, or an Error Value where the engine cannot yet say what Excel gives:

- Logic: `AND`, `OR`, `NOT`, `IFNA`, `ISBLANK`, `ISNUMBER`
- Rounding: `ROUNDUP`, `ROUNDDOWN`, `ABS`, `INT`, `MOD`
- Dates: `DATE`, `YEAR`, `MONTH`, `DAY`, `EOMONTH`, `EDATE`
- Text: `LEFT`, `RIGHT`, `MID`, `LEN`, `TRIM`, `CONCAT`
- Lookup: `INDEX` (the array form)

**Blocked by:** nothing

- [x] Each function's cases are in `tests/ExSheet.Engine.Tests/ExcelCases/<function>.json`:
      Microsoft's examples and statements as `documented`, the cases it leaves open as `uncertain`
- [x] The declarations carry Excel's argument names, for completion and the argument hint
- [x] `DATE` gives a General cell the short date format on entry, as Microsoft documents (FF-036)
- [x] Their catalogue rows are Supported; ADR-0047 names them in a dated addition; the engine's
      README lists them
- [ ] The next Windows run asks Excel every `uncertain` case, and each answer is applied

## Comments

2026-10-03: admitted. Every case in the corpus passes: 185 new ones, and two formula-format cases.
Refused until Excel answers, each with an Error Value and never another answer: `MOD` whose
quotient is 2^27 or more (`#NUM!`, MOD-008), `EOMONTH` and `EDATE` from or to a day before 1 March
1900 (EOMONTH-008, EDATE-008), and a boolean typed into either (EOMONTH-009, EDATE-009). The
functions that answer about any Value (`IFNA`, `ISBLANK`, `ISNUMBER`) give `#GETTING_DATA` and
`#CIRC!` back, as `IFERROR` and `ISERROR` do; `FunctionAdditionTests` pins that, `INDEX` over a
Linked Table's column, and `CONCAT` past a cell's 32767 characters.

What the Windows run asks first, because the engine's answer is a reading rather than documented:
whether numbers inside a range count for `AND` and `OR`, and how typed text and an empty argument
count; whether `INDEX` over one row takes `row_num` alone along the row; `ROUNDUP` and `ROUNDDOWN`
over doubles that sit beside a decimal boundary (`0.1+0.2`, `0.3-0.1`); and `MOD(0.3,0.1)`.

2026-10-12: the seventeenth Windows run answered EOMONTH-008 and EDATE-008 (59 and 46). Before 1 March 1900
the months are counted on the real calendar; a start of serial 0 or 60 stays refused (ADR-0047's note).

2026-10-12, the area pass (`verification/2026-10-12-windows-area/`): Excel answered EOMONTH-010 (`EOMONTH(0,1)`,
59) and EDATE-010 (`EDATE(60,1)`, 89), and both follow it. The start is read as `YEAR`, `MONTH` and `DAY` read it,
serial 0 in January 1900 and serial 60 the 29th of February, and the months are counted on the real calendar.
`EDATE` from serial 0, whose day is 0, stays refused (EDATE-016), and so does a start between -1 and 0, which is
serial 0 truncated and -1 cut down to its day, until Excel is asked which (EOMONTH-016 and 017, EDATE-017).
EOMONTH-012 to 017 and EDATE-013 to 017 are `uncertain` for the next pass, and YEAR-008, MONTH-005 and DAY-005
ask how those read a serial between -1 and 0 (ADR-0047's note).

2026-10-12, the second area pass (`verification/2026-10-12-windows-area-2/`): Excel answered EOMONTH-012 to 017 and
EDATE-013 to 017, and `YEAR`, `MONTH` and `DAY` of -0.5 (YEAR-008, MONTH-005, DAY-005). A start between -1 and 0
is cut down to its day, -1, and `#NUM!`, as `YEAR` reads a serial, and `EOMONTH` and `EDATE` follow it. `EDATE(0,1)`
is 31: serial 0 keeps its day 0, which is the day before the 1st of the month reached. Six of 112 readings give every
observed answer (`spikes/exsheet-date-readings/`); they part at `EDATE` from serial 0 to January 1900, March 1900 and
January 10000, which stay refused, and EDATE-018 to 022 are `uncertain` for the next pass (ADR-0047's note).
A boolean typed as `EOMONTH`'s start or as `EDATE`'s months stays refused with `#VALUE!` too, and EOMONTH-018 and
EDATE-023 ask it: the seventeenth run typed one as `EOMONTH`'s months and as `EDATE`'s start alone (EOMONTH-009,
EDATE-009), and Excel reads a boolean typed as a start date as 1 under `DATEDIF` (DATEDIF-046) and gives `#VALUE!`
under `YEARFRAC` (YEARFRAC-054), so neither answers the other argument.
