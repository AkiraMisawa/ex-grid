# 04: The first function set, admitted against Excel

Status: done

**What to build:** `SUM`, `AVERAGE`, `MIN`, `MAX`, `COUNT`, `COUNTA`, `IF`, `ROUND`, `IFERROR`, `ISERROR` and
`XLOOKUP`, each giving Excel's result, including blanks, text and Error Values inside ranges, and
Excel's rounding. An unknown name is `#NAME?`. Each function's tests are a table of Excel's observed
results, and a function with a known deviation stays out (ADR-0047).

**Blocked by:** 03

- [x] Each function passes its table of Excel's observed results, edge cases included
- [x] An unknown function is `#NAME?`
- [x] The function list is exposed for completion (ticket 10) and documented

## Comments

2026-09-27, engine half: the eleven functions are declared in `ExSheet.Engine` and each has a
table in `tests/ExSheet.Engine.Tests/FunctionTests.cs`, transcribed from Microsoft's
documentation (blanks, text, booleans and Error Values typed as arguments and inside ranges;
`ROUND`'s own examples and half away from zero; `XLOOKUP`'s match and search modes). An unknown
function is `#NAME?`, and a call with the wrong number of arguments is refused on entry, as
Excel refuses it. `DeclaredFunction.All` exposes the list with each signature for completion and
the argument hint (ticket 10), and the package README documents it.

Cases whose Excel result is not certain were left out of the tables rather than guessed; the
behaviour implemented for them is listed in the engine's hand-off report. Two parts of the set are
refused rather than admitted, and are put to the user as decisions: `XLOOKUP`'s binary search
(`search_mode` 2 and −2) is `#VALUE!`, and any result that would spill an array is `#VALUE!`,
which `IFERROR` and `ISERROR` do not catch. `IFERROR`/`ISERROR` treat `#GETTING_DATA` as not an
error, but no public path can produce `#GETTING_DATA` until Linked Tables exist (ticket 16), so
that rule is pinned there. **What remains for the component:** nothing in this ticket; the list
is consumed by ticket 10.
