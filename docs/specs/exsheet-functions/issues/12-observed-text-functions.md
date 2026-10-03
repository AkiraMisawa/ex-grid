# 12: SEARCH, UPPER, LOWER and PROPER

Status: done

**What to build:** Admit observed text matching and casing under ADR-0047 independently of
the host platform's Unicode tables.

**Blocked by:** nothing within the admitted character domain.

- [x] Import and exercise all 81 observations, retaining Excel's answer for refused cases.
- [x] SEARCH's own case folding, wildcard matching, escaping and positions.
- [x] Explicit casing mappings, PROPER word boundaries and the observed LOWER sigma contexts.
- [x] Refuse other Unicode characters and contexts; document the exact alphabets in the README.
- [x] Declare the functions for completion; browser acceptance includes UPPER and SEARCH.

## Comments

2026-10-03: implemented in `Functions.ObservedText.cs`, verified by `ObservedTextTests`.
Supplementary-plane observations do not establish a general casing or matching algorithm.
The implementation therefore reports `#VALUE!` outside the documented domain.
