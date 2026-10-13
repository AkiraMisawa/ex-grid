# 04: The P2 Ready functions

Status: done

**What to build:** the catalogue's P2 functions marked Ready, under ADR-0047's rule:

- Aggregation: `PRODUCT`, `MEDIAN`, `LARGE`, `SMALL`
- Logic: `ISTEXT`, `ISNA`, `IFS`, `SWITCH`, `NA`
- Lookup and reference: `XMATCH`, `CHOOSE`, `ROW`, `COLUMN`
- Math: `TRUNC`, `POWER`, `SQRT`
- Dates: `WEEKDAY`, `DAYS`, `NETWORKDAYS`, `WORKDAY`
- Text: `TEXTJOIN`, `CONCATENATE`, `SUBSTITUTE`, `REPLACE`, `FIND`
- Financial: `PMT`, `PV`, `FV`, `NPV`

**Blocked by:** nothing

- [x] Each function's cases are in `ExcelCases/<function>.json`, Microsoft's examples as `documented`
- [x] `IFS` refuses arguments that are not in pairs on entry, as Excel does
- [x] `XMATCH` shares `XLOOKUP`'s search, its modes and its refusals
- [x] `ROW()` and `COLUMN()` recalculate when an insertion or deletion moves their cell
- [x] Catalogue rows Supported; ADR-0047's additions name them; the engine's README lists them
- [ ] The next Windows run asks Excel every `uncertain` case
- [ ] `XMATCH`'s `match_mode` and `search_mode` value lists, as Excel's completion shows them,
      are observed and declared (ADR-0058); until then completion lists no values there

## Comments

2026-10-03: admitted. 145 cases, every one passing; every documented example, such as
`NETWORKDAYS`'s 110, 109 and 107 days and `PMT`'s monthly payment, is reproduced. Refused until
Excel answers: `LARGE` and `SMALL` with a `k` that is not whole (LARGE-007); `PMT`, `PV` and
`FV` with a `type` other than 0 or 1 (PMT-008); `NETWORKDAYS` and `WORKDAY` before 1 March
1900, with a boolean typed, or with text among the holidays. Spill refusals, as ADR-0047 has them:
`ROW` over several rows, `COLUMN` over several columns, `CONCATENATE` over a range of several
cells, a `TEXTJOIN` delimiter of several cells.

The new functions moved completion's lists: `=SU` now offers `SUBSTITUTE` before `SUM`, and `=S`
offers `SMALL` first. The layer-2 and layer-3 tests that named the first candidate were updated,
and the layer-3 ones run on both hosts.

2026-10-12: the seventeenth Windows run answered NETWORKDAYS-005 (51) and SUBSTITUTE-005 (`aba`). Dates
before 1 March 1900 count on `WEEKDAY`'s days, for `WORKDAY` too, and `SUBSTITUTE` counts overlapping
instances (ADR-0047's note). It also answered the refusals above: a `type` of 2 is payment at the start
(PMT-008), and `LARGE`'s k of 1.5 is the 2nd largest (LARGE-007). `PV` and `FV` now give every double
Excel returned, raising 1+rate by squaring, and `PMT` computes the payment exactly and rounds it once,
one of several evaluations that fit the four payments read (`spikes/exsheet-financial-evaluation/`).
`NPV` still differs from Excel in the last digits (NPV-001, 002); whether it is refused before the tag is
the user's question (`docs/agents/proposals/run17-financial.md`).

2026-10-12, the area passes: `NPV` is refused for 0.1.0 (ADR-0202's note of that date) and moves to
ticket 13. `PV` and `FV` give every double and Error Value Excel was observed to give: they read any
`type` as Excel does and raise 1+rate to any `nper` by the financial power, with the exponential and
the logarithm correctly rounded (`spikes/exsheet-financial-evaluation/`, `Search.cs --refusals`); PV at
a rate of -1 with an `nper` above 0 is refused until asked (PV-013). `PMT` is kept under ADR-0047's
exception for `PMT` alone (decided with the user): its result may differ from Excel's in its last
digits. Where it answers, the branch gives the exact payment rounded once at the end of each period,
and the exact payment rounded once at the start too (decided with the user, 2026-10-13); it refuses where the evaluations its observed payments leave part in kind or beyond the
last digits, until asked (PMT-034 to 053).

2026-10-12, with the area passes (`verification/2026-10-12-windows-area/report.md` and
`verification/2026-10-12-windows-area-2/report.md`): Excel truncates `SMALL`'s k that is not whole
(SMALL-004, 005, 009: 1.5 and 1.2 are each the smallest, 3.9 the 3rd), rounds `LARGE`'s up (LARGE-008,
009), and a k below 1 or above the count of the numbers is `#NUM!` before it is rounded (LARGE-011, 013:
0.5 and 0.99; SMALL-007: 0.5; SMALL-008: 9.5 over nine numbers, which truncated would be the 9th
smallest; LARGE-010: 10.5 over ten). `LARGE` and `SMALL` coerce a Value given directly as the array
(LARGE-014: `LARGE("5",1)` is 5; SMALL-010: `SMALL(TRUE,1)` is 1). The engine reads k so for `LARGE`,
`SMALL` and `AGGREGATE`'s 14 and 15 (`KthOf`), and no k is `#VALUE!` any more (ADR-0047's note of that
date; `KthAndModTests`). SMALL-011 (`SMALL(A1:A9,9.2)`, `#NUM!`) is a new `uncertain` case: a k a little
above the count, which rounded to nearest would be 9. It is asked with ticket 16's open box, beside
AGGREGATE-068, and this ticket stays done (ADR-0208's note of 2026-10-12). NETWORKDAYS-010 and
WORKDAY-011 ask the refusal of a boolean typed as a date, which no case asked before; WEEKDAY-008,
DAYS-005, NETWORKDAYS-009 and WORKDAY-010 are `#NUM!` in Excel, as the engine answers.
