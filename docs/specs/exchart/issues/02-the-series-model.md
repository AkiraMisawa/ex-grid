# 02: One series model under every chart

Status: ready-for-agent
Roadmap: internal

**Why:** [ADR-0251](../../../adr/0251-a-chart-is-defined-in-the-datas-terms-and-a-stale-gesture-is-refused.md).

- [ ] A series: points `(x, y?)`, its Series Kind, panel, axis side and slot; a missing `y` kept as missing
- [ ] An axis: dates, numbers, or categories in a declared order
- [ ] The Chart Definition built from series directly, with no grid type in the Engine
- [ ] Layer-1 tests named by ADR

**Blocked by:** 01.
