# 19: Verify the engine's uncertain cases against a real Excel

Status: ready-for-human

**What to build:** ADR-0047 admits a function only when it matches Excel, and the tests are written
from Excel's observed behaviour. The engine was built without a real Excel, so every case whose
answer could not be settled from Microsoft's documentation was left out of the test tables. The
engine still has to do something in each case, and its current choice is shown in brackets. A
person types each Formula into a real Excel (desktop, Microsoft 365, en-US unless stated) and
records the result. Every row then either becomes a test that pins Excel's answer, or, if the
engine cannot match it, the function or the argument value is refused, as ADR-0047 requires.

This needs Excel on a person's machine, so it is `ready-for-human`. Record the Excel build number
in the Comments.

**Every row below is now a case in the Excel case corpus**
(`tests/ExSheet.Engine.Tests/ExcelCases/*.json`) with `source: "uncertain"`, its `ticket19Row`,
and the engine's current answer as `expect`. `ExcelOracle/oracle.ps1` asks Excel all of them with
the rest of the corpus ([verify-on-windows.md](../verify-on-windows.md), Part A). Row 11 is asked
with ranges (`=XLOOKUP(1,A1,B1,)`), because ExSheet does not read array constants such as `{1}`.

**Blocked by:** None (can start immediately)

| # | Enter this | Engine today | Excel's answer |
|---|---|---|---|
| 1 | `=0.1+0.2-0.3` | 5.55E-17 (plain IEEE) | **0** (ARITH-066, disagrees) |
| 2 | `=0.5-0.4-0.1` | -2.78E-17 | **0** (ARITH-067, disagrees) |
| 3 | `=1*(0.5-0.4-0.1)` | -2.78E-17 | -2.77556E-17, i.e. -2.7755575615628914E-17 (ARITH-068, agrees): a final subtraction inside a product is left alone |
| 4 | `=0.1+0.2=0.3` | FALSE | **TRUE** (ARITH-069, disagrees) |
| 5 | `="a"+1/0` | `#VALUE!` | `#VALUE!` (ARITH-070, agrees) |
| 6 | `="!"<"a"`, `="é">"z"` (text ordering) | ordinal, case-insensitive | TRUE (ARITH-071, agrees) / **FALSE** (ARITH-072, disagrees) |
| 7 | `=1E15&""`, `=1E-5&""`, `=123456789012345678&""` | scientific from 1E+15 and below 1E-05 | **`1000000000000000`** / **`0.00001`** / **`123456789012345000`**: the constant is stored with 15 digits on entry (ARITH-073..075, all disagree) |
| 8 | type `200%%` into a cell | | text `200%%` (TYPED-016, agrees) |
| 9 | `=SUM(1,,2)`, `=COUNT(,)`, `=COUNTA(,)` | a missing argument is 0 | 3 / 2 / 2 (SUM-013, COUNT-008, COUNTA-007, agree) |
| 10 | `=IF(TRUE,,2)`, `=IFERROR(1/0,)` | 0 | 0 / 0 (IF-016, IFERROR-011, agree) |
| 11 | `=XLOOKUP(1,{1},{2},)` | treated as omitted | 2 (XLOOKUP-066, agrees) |
| 12 | `=IF("TRUE",1,2)`, `=IF("x",1,2)` | 1 / `#VALUE!` | 1 / `#VALUE!` (IF-017, IF-018, agree) |
| 13 | `=ROUND(1.005,2)`, `=ROUND(2.675,2)` | 1.01 / 2.68 | 1.01 / 2.68 (ROUND-018, ROUND-019, agree) |
| 14 | `=ROUND(1.25,1.9)`, `=ROUND("1.25",1)` | truncated digits / | 1.3 / 1.3 (ROUND-020, ROUND-021, agree) |
| 15 | `=IFERROR(A1,"x")` with A1 empty | `""` (per documentation) | **0** (IFERROR-012, disagrees) |
| 16 | `=SUM(A1:A3)` with `#N/A` in A2 and `#DIV/0!` in A3 | the first in row-major order | `#N/A` (SUM-014, agrees) |
| 17 | `=COUNTA(#N/A,1)` | | 2 (COUNTA-008, agrees) |
| 18 | `=XLOOKUP(A1,…)` with A1 empty | no match | **2**: an empty lookup value matches a blank cell of `lookup_array` (XLOOKUP-067, disagrees) |
| 19 | XLOOKUP with an Error Value inside `lookup_array` | skipped | `c`, skipped (XLOOKUP-068, agrees) |
| 20 | XLOOKUP approximate match (`match_mode` -1 / 1) over mixed numbers and text | | over 10, x, 30, y: `a` for 20 with -1, `c` for 20 with 1, `b` for `"m"` with 1 (XLOOKUP-069, 070, 072, agree); **`c` for `"m"` with -1**: numbers count as smaller than text (XLOOKUP-071, disagrees) |
| 21 | XLOOKUP ties under `match_mode` -1 / 1 | the first in search order | `a`, `b`, `c`, `d` (XLOOKUP-073..076, agree) |
| 22 | `=XLOOKUP(1,A1:A3,B1:B3,,3)`, `…,,1.5)` | `#VALUE!` / truncated | **`a`**: match_mode 3 is accepted (XLOOKUP-077, disagrees) / `a`, truncated (XLOOKUP-078, agrees) |
| 23 | XLOOKUP `match_mode` 2 (wildcards) with a number as the lookup value | | `number`, `text`, `text` (XLOOKUP-079..081, agree) |
| 24 | type ` 12 ` (spaces around) | 12 | 12 (TYPED-017, agrees) |
| 25 | type `1,23` | text | text (TYPED-018, agrees) |
| 26 | type `+A1`, `-A1` | text | **typed with real keys: the Formulas `=+A1` and `=-A1`** (disagrees). Through COM's `FormulaLocal` both were text, so TYPED-019/020 read as agreeing; see `verification/2026-09-27-windows-excel/behaviours.md` |
| 27 | type `$5`, `26-Sep` | text | **5**, format `$#,##0_);[Red]($#,##0)` / **46291 (26 Sep 2026)**, format `d-mmm` (TYPED-021, TYPED-022, disagree) |
| 28 | under de-DE, type `WAHR` | text | text (TYPED-023, agrees). Excel's UI language here is English; `WAHR` is a word of German Excel, not of the regional format |
| 29 | type `12.5%`, `1,234`, `1E3`; then look at the cell's format | `0.00%` / General / General | 0.125, `0.00%` (agrees) / 1234, **`#,##0`** (disagrees) / 1000, **`0.00E+00`** (disagrees) (TYPED-024..026) |
| 30 | under ja-JP, type `2026/9/26`; which date format is applied | `yyyy/mm/dd` | 46291, shown `2026/09/26`, but the format is Excel's **built-in short date** (`m/d/yyyy` in the invariant codes), not `yyyy/mm/dd` (TYPED-027, disagrees) |
| 31 | format `0.00` on `-0.001` | `-0.00` | **`0.00`** (FMT-063, disagrees) |
| 32 | format `h:mm:ss` on 0.99999999 | rounds into the next day | `0:00:00` (FMT-064, agrees) |
| 33 | format `h:mm am/pm` (lower case) | literal `AM`/`PM` | **`6:00 PM`**; Excel rewrites the code to `h:mm AM/PM` (FMT-065, disagrees) |
| 34 | format `_)0` on 5 | one space | ` 5` (FMT-066, agrees) |
| 35 | `= A1 + B1` typed with spaces, then F2 | spaces kept (ADR-0047) | `= A1 + B1`, spaces kept (TEXT-074, agrees). Typed with real keys, F2 shows `= A1 + B1` (agrees); a trailing space is dropped on entry |

- [x] Every row has Excel's answer and the build number is recorded
- [ ] Each row where the engine differs is fixed to match, or the function / argument value is refused with an Error Value, and ADR-0047's list is updated accordingly
- [ ] Rows 1–3 settle ADR-0047's `0.1+0.2-0.3` example, and the ADR's text is corrected if Excel returns 0

## Comments

2026-09-27, Excel's answers, from a real Excel on Windows: **Microsoft 365, 16.0.20326.20158
(build 20326), 64-bit, Current Channel**, UI language English (UK). Formulas went in through
`Range.Formula2`, and typed constants through `Range.FormulaLocal` under the row's regional
format: en-US unless stated, de-DE for row 28 and ja-JP for row 30. All of it was done by
`ExcelOracle/oracle.ps1` (`results-2026-09-27*.json`). Each row of the table above has Excel's
answer, its case id, and whether it agrees with the engine. 18 of the 57 cases disagree through
COM, in 14 rows: 1, 2, 4, 6, 7, 15, 18, 20, 22, 27, 29, 30, 31 and 33. Row 26 disagrees too once
typed with real keys, though COM agreed. That makes 15 rows. They are listed in
`verification/2026-09-27-windows-excel/results.md` under "Disagreements", for a decision. Nothing
was changed in the engine, so the second and third checkboxes stay open. Rows 1–3: Excel returns
0 for rows 1 and 2, and the IEEE result for row 3. That answers ADR-0047's `0.1+0.2-0.3` example;
correcting the ADR is the user's call.

