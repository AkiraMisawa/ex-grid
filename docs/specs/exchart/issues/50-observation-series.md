# 50: Observation, a fourth Series Kind

Status: open
Roadmap: internal

**Why:** [ADR-0290](../../../adr/0290-a-scatter-is-a-series-of-observations-each-a-row.md) (Q1).

- [ ] `SeriesKind.Observation`: points neither ordered nor unique in x, never joined, drawn as markers
- [ ] A point may carry a Row Key and a name
- [ ] A missing x or y is no point
- [ ] A chart holding an Observation reads x as a value axis (Numbers or Dates); categories refused by name
- [ ] Observation axes do not include zero by default; a series may declare from-zero; the break mark stays

**Blocked by:** —.
