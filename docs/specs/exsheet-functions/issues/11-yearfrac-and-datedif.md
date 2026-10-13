# 11: YEARFRAC and DATEDIF

Status: done

**What to build:** Admit calendar differences under ADR-0047, preserving Excel's 1900 calendar.

**Blocked by:** nothing within the admitted domain.

- [x] Import and exercise 57 YEARFRAC and 47 DATEDIF observations.
- [x] YEARFRAC bases 0–4, February ends, year lengths and reversed intervals.
- [x] DATEDIF Y, M, D and YM; YD only before the first completed anniversary.
- [x] Keep the existing MD refusal; retain Excel's answer beside deliberate refusals.
- [x] Pin date coercions, time truncation and calendar boundaries in `DateDifferencesTests`.
- [x] Offer YEARFRAC's five documented basis values through argument completion (ADR-0058).
- [ ] Confirm the basis list's captions against Windows Excel's popup; calculations alone do
  not establish its presentation.

## Comments

2026-10-03: implemented in `Functions.DateDifferences.cs`. Longer YD intervals need evidence
about which year's leap day survives removing whole years. They return `#VALUE!` until then.
The README and ADR-0047 record this limit. Browser acceptance includes a leap-day DATEDIF.
Basis choices follow Microsoft's [YEARFRAC documentation](https://support.microsoft.com/en-us/excel/functions/yearfrac-function).

2026-10-12, the area passes: `DATEDIF` and `YEARFRAC` give `#NUM!` for any serial below 0, and do not cut it
down to its day as the nine functions the second area pass asked at -0.5 do. Excel gave `#NUM!` at -1
(DATEDIF-045, YEARFRAC-056); between -1 and 0 the engine refuses it with `#VALUE!` until Excel is asked
(2026-10-13), and DATEDIF-048 and YEARFRAC-058 ask it. The readings part under `DATEDIF`: cut down to -1, or refused below 0,
`DATEDIF(-0.5,1,"D")` is `#NUM!`; truncated to serial 0, it is 1, and `DATEDIF` reads a boolean typed as a
date as 1 (DATEDIF-046), where `YEARFRAC` gives `#VALUE!` (YEARFRAC-054), so its coercion is not shown to be
the nine functions'. The ticket stays done: the cases are asked by the one
`oracle.ps1 -Area` pass before `0.1.0-rc.1` (ADR-0208's note of 2026-10-12).
