# 77: The 3D surface and intervals in a Chart Spec

Status: ready-for-agent
Roadmap: ExChart — An AI draws a 3D surface, and a series' bands and whiskers, from a Chart Spec.

**Why:** [ADR-0320](../../../adr/0320-an-ai-draws-a-chart-by-writing-a-chart-spec-that-names-data-it-never-holds.md) says the first version covers everything ExChart draws; tickets 70 to 73 cover the 2D series, candles, scatter, colour, size, nodes and edges, and not these.

- [ ] A spec for an `ExSurface`: its grid or its triangles read from a table, its colour and centre, its log axes
- [ ] A series' interval (`lower`, `upper` columns) and an Observation's `xLower`, `xUpper`, as ADR-0304 draws them
- [ ] The schema, the tool's description and the answer say both

**Blocked by:** 70.
