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
