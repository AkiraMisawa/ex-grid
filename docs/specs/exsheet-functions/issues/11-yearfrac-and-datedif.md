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
