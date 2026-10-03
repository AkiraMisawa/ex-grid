# 09: VLOOKUP, HLOOKUP and MATCH

Status: done

**What to build:** Admit the three legacy lookups under ADR-0047, without borrowing XLOOKUP's
distinct duplicate-key behavior.

**Blocked by:** nothing within the admitted domain.

- [x] Import and exercise all 240 observed cases.
- [x] Exact matching, wildcards, coercions, blank results and row/column selection.
- [x] Ascending and descending approximate matches, including their observed duplicate choices.
- [x] Require homogeneous, nonblank, error-free sorted keys for approximate lookup.
- [x] Explicit refusals retain `excelExpect`; the README records the admitted text/error domains.
- [x] Offer range_lookup and match_type choices through ADR-0058's existing completion seam.
- [ ] Confirm the choice captions and boolean whole-value list against Windows Excel's popup;
  the October 3 run observed calculations, not those popups.

## Comments

2026-10-03: implemented in `Functions.LegacyLookup.cs`, verified by `LegacyLookupTests`.
Unsorted input, broader collation, an exact match after an Error Value and a descending
nearest non-equal duplicate remain refused. Observation of a numeric answer does not override
ADR-0047's existing sorted-data refusal. Browser acceptance includes VLOOKUP.

The choice values and meanings follow Microsoft's
[VLOOKUP](https://support.microsoft.com/en-us/excel/functions/vlookup-function),
[HLOOKUP](https://support.microsoft.com/en-us/excel/functions/hlookup-function) and
[MATCH](https://support.microsoft.com/en-us/excel/functions/match-function) documentation.
Whole boolean spellings follow ADR-0058's existing whole-value rule; other text keeps the full
list. Popup presentation remains an explicit Windows follow-up rather than claimed observation.
