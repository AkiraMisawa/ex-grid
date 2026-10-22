# 77: The 3D surface and intervals in a Chart Spec

Status: done
Roadmap: ExChart — An AI draws a 3D surface, and a series' bands and whiskers, from a Chart Spec.

**Why:** [ADR-0320](../../../adr/0320-an-ai-draws-a-chart-by-writing-a-chart-spec-that-names-data-it-never-holds.md) says the first version covers everything ExChart draws; tickets 70 to 73 cover the 2D series, candles, scatter, colour, size, nodes and edges, and not these.

- [x] A spec for an `ExSurface`: its grid or its triangles read from a table, its colour and centre, its log axes
- [x] A series' interval (`lower`, `upper` columns) and an Observation's `xLower`, `xUpper`, as ADR-0304 draws them
- [x] The schema, the tool's description and the answer say both

**Blocked by:** 70.

## Comments

- 2026-10-22: built. A surface is a spec holding `surface` in place of `axis` and `series` (ADR-0320's note of that date); a Board does not yet take one as a view and refuses it by its path, which is ExBoard's to take up.
