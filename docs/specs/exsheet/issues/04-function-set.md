# 04: The first function set, admitted against Excel

Status: ready-for-agent

**What to build:** `SUM`, `AVERAGE`, `MIN`, `MAX`, `COUNT`, `COUNTA`, `IF`, `ROUND`, `IFERROR`, `ISERROR` and
`XLOOKUP`, each giving Excel's result, including blanks, text and Error Values inside ranges, and
Excel's rounding. An unknown name is `#NAME?`. Each function's tests are a table of Excel's observed
results, and a function with a known deviation stays out (ADR-0047).

**Blocked by:** 03

- [ ] Each function passes its table of Excel's observed results, edge cases included
- [ ] An unknown function is `#NAME?`
- [ ] The function list is exposed for completion (ticket 10) and documented

## Comments
