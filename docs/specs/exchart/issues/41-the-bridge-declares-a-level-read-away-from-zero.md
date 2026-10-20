# 41: The selection bridge declares a level read away from zero

Status: needs-info
Roadmap: ExChart — a chart made from a grid's selection can read rates away from zero, as a chart built by hand can.

**Why:** [ADR-0253](../../../adr/0253-every-series-declares-its-series-kind-and-its-drawing-follows.md) gives a
Level axis zero unless the series declares otherwise (`FromZero = false`). `GridChartDeclarations` has no
way to say it, so every chart made from a grid's selection draws rates between 3.0% and 3.6% from zero, as
nearly flat lines (seen on the Docs Site's "From the grid" Example, 2026-10-20).

- [ ] Decide the declaration's shape (per column, as `KindOf` is) — a change to ADR-0258's declarations, made with the user
- [ ] The bridge carries it onto each series it makes, in both orientations
- [ ] Layer 2 test naming ADR-0253
