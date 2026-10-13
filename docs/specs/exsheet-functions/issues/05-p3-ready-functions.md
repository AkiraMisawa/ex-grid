# 05: The P3 Ready functions

Status: done

**What to build:** the catalogue's P3 functions marked Ready, under ADR-0047's rule: `RANK.EQ`,
`STDEV.S`, `STDEV.P`, `VAR.S`, `VAR.P`, `XOR`, `ROWS`, `COLUMNS`, `SIGN`, `EXP`, `LN`,
`LOG10`, `PI`, `TIME`, `HOUR`, `MINUTE`, `SECOND`, `REPT`, `EXACT`, `NUMBERVALUE` and `XNPV`.

**Blocked by:** nothing

- [x] Each function's cases are in `ExcelCases/<function>.json`, Microsoft's examples as `documented`
- [x] Catalogue rows Supported; ADR-0047's additions name them; the engine's README lists them
- [ ] The next Windows run asks Excel every `uncertain` case: `HOUR`, `MINUTE` and `SECOND` round
      a time to the nearest second, and `NUMBERVALUE`'s edges, first

## Comments

2026-10-03: admitted. 67 cases, every one passing. `XNPV` refuses with `#VALUE!` a value or a
date that is not a number, a blank among them included, until Excel is asked. The layer-3 test
of completion over a repeated letter (DC-31, =SS) no longer names the first candidate: which
function is first is the declared set's order, and the test is about the caret.

2026-10-12, the area passes: `XNPV` gives every double Excel was observed to give: the days over 365,
whole years by squaring and e^(years ln(1+rate)) correctly rounded otherwise, each value divided,
summed from the first (XNPV-001, 004 to 006, 011, 013 to 016), one of the two evaluations that give
them. It checks the rate before it evaluates: `#NUM!` below 0, Excel's answer at -0.5 to -2 (XNPV-007
to 010, 012, 017), which no evaluation gives, and the engine's at other negative rates (XNPV-020
asks). It refuses a rate of 0, a last addition that nearly cancels, and an Error Value in its rate or
ranges beside another check that would give another answer, until Excel is asked (XNPV-018, XNPV-019,
XNPV-021 to 030; `spikes/exsheet-financial-evaluation/`).

2026-10-12, the area passes: `HOUR`, `MINUTE` and `SECOND` give `#NUM!` for any serial below 0, and do not
cut it down to its day as `YEAR` and its kin do. Excel gave `#NUM!` at -1 under `MINUTE` (MINUTE-002). At
-0.5, between -1 and 0, the readings part: cut down to -1 it is `#NUM!`, and read as a time of day -0.5 is
12:00, where `HOUR` would be 12. The engine refuses a serial between -1 and 0 with `#VALUE!` until Excel is
asked (2026-10-13), and HOUR-005, MINUTE-003 and SECOND-003 ask it.
The ticket stays done: they are asked by the one `oracle.ps1 -Area` pass before `0.1.0-rc.1` (ADR-0208's note
of 2026-10-12).
