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
