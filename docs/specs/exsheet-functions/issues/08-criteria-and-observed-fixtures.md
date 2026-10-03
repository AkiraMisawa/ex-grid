# 08: Criteria aggregation and typed observation fixtures

Status: done

**What to build:** Admit SUMIF, SUMIFS, COUNTIF, COUNTIFS, AVERAGEIF, AVERAGEIFS, MAXIFS,
MINIFS and COUNTBLANK under ADR-0047, using the October 3 observations.

**Blocked by:** nothing within the admitted domain.

- [x] Preserve absent cells, numeric text, booleans and binary64 fixtures through `values`;
  expected numbers use `roundTrip` checked against `bits`.
- [x] Import 472 observed cases, retaining Excel's answer beside deliberate refusals.
- [x] Match criteria independently of aggregation, preserving range shape and sparse counts.
- [x] Extend literal SUMIF/AVERAGEIF result footprints in dependencies and cycle detection.
- [x] Refuse unobserved collation, long criteria and culture-sensitive numeric spelling.
- [x] Declare the functions for completion; repeated argument hints alternate range and criterion.

## Comments

2026-10-03: implemented. `CriteriaFunctionTests` covers the observations, expanded dependency
footprint, circular taint, whole-column blank counts, argument pairs and refusal boundaries.
`ObservedFixtureTests` checks typed fixture preservation. Browser acceptance lives in
`sheet-observed-functions.spec.mjs`. The original verification evidence is unchanged.
