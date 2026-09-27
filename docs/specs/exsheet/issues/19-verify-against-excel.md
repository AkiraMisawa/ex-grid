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

**Blocked by:** None (can start immediately)

| # | Enter this | Engine today | Excel's answer |
|---|---|---|---|
| 1 | `=0.1+0.2-0.3` | 5.55E-17 (plain IEEE) | |
| 2 | `=0.5-0.4-0.1` | -2.78E-17 | |
| 3 | `=1*(0.5-0.4-0.1)` | -2.78E-17 | |
| 4 | `=0.1+0.2=0.3` | FALSE | |
| 5 | `="a"+1/0` | `#VALUE!` | |
| 6 | `="!"<"a"`, `="é">"z"` (text ordering) | ordinal, case-insensitive | |
| 7 | `=1E15&""`, `=1E-5&""`, `=123456789012345678&""` | scientific from 1E+15 and below 1E-05 | |
| 8 | type `200%%` into a cell | | |
| 9 | `=SUM(1,,2)`, `=COUNT(,)`, `=COUNTA(,)` | a missing argument is 0 | |
| 10 | `=IF(TRUE,,2)`, `=IFERROR(1/0,)` | 0 | |
| 11 | `=XLOOKUP(1,{1},{2},)` | treated as omitted | |
| 12 | `=IF("TRUE",1,2)`, `=IF("x",1,2)` | 1 / `#VALUE!` | |
| 13 | `=ROUND(1.005,2)`, `=ROUND(2.675,2)` | 1.01 / 2.68 | |
| 14 | `=ROUND(1.25,1.9)`, `=ROUND("1.25",1)` | truncated digits / | |
| 15 | `=IFERROR(A1,"x")` with A1 empty | `""` (per documentation) | |
| 16 | `=SUM(A1:A3)` with `#N/A` in A2 and `#DIV/0!` in A3 | the first in row-major order | |
| 17 | `=COUNTA(#N/A,1)` | | |
| 18 | `=XLOOKUP(A1,…)` with A1 empty | no match | |
| 19 | XLOOKUP with an Error Value inside `lookup_array` | skipped | |
| 20 | XLOOKUP approximate match (`match_mode` -1 / 1) over mixed numbers and text | | |
| 21 | XLOOKUP ties under `match_mode` -1 / 1 | the first in search order | |
| 22 | `=XLOOKUP(1,A1:A3,B1:B3,,3)`, `…,,1.5)` | `#VALUE!` / truncated | |
| 23 | XLOOKUP `match_mode` 2 (wildcards) with a number as the lookup value | | |
| 24 | type ` 12 ` (spaces around) | 12 | |
| 25 | type `1,23` | text | |
| 26 | type `+A1`, `-A1` | text | |
| 27 | type `$5`, `26-Sep` | text | |
| 28 | under de-DE, type `WAHR` | text | |
| 29 | type `12.5%`, `1,234`, `1E3`; then look at the cell's format | `0.00%` / General / General | |
| 30 | under ja-JP, type `2026/9/26`; which date format is applied | `yyyy/mm/dd` | |
| 31 | format `0.00` on `-0.001` | `-0.00` | |
| 32 | format `h:mm:ss` on 0.99999999 | rounds into the next day | |
| 33 | format `h:mm am/pm` (lower case) | literal `AM`/`PM` | |
| 34 | format `_)0` on 5 | one space | |
| 35 | `= A1 + B1` typed with spaces, then F2 | spaces dropped | |

- [ ] Every row has Excel's answer and the build number is recorded
- [ ] Each row where the engine differs is fixed to match, or the function / argument value is refused with an Error Value, and ADR-0047's list is updated accordingly
- [ ] Rows 1–3 settle ADR-0047's `0.1+0.2-0.3` example, and the ADR's text is corrected if Excel returns 0

## Comments
