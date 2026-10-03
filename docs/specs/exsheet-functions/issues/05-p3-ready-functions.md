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
