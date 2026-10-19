# 02: One series model under every chart

Status: ready-for-agent
Roadmap: internal

**Why:** [ADR-0256](../../../adr/0256-a-series-look-is-chosen-by-three-appearance-keys.md), [ADR-0257](../../../adr/0257-notes-and-interpolated-series-are-the-consumers-said-as-such.md), [ADR-0251](../../../adr/0251-a-chart-is-defined-in-the-datas-terms-and-a-stale-gesture-is-refused.md).

- [ ] A series: points `(x, y?)` with values held as handed in (decimal or double), its Series Kind, panel, axis side and Appearance Keys (ADR-0256); a missing `y` kept as missing
- [ ] Notes; Interpolated series as points or as a sampled function `valueAt(t)`; Segments with method and extrapolated flag (ADR-0257)
- [ ] An axis: proportional (dates, numbers, declared positions) or categories in a declared order, declared in the Chart Definition with a default by type (ADR-0251)
- [ ] The Chart Definition built from series directly, with no grid type in the Engine
- [ ] Layer-1 tests named by ADR

**Blocked by:** 01.
