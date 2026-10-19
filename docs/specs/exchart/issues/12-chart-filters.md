# 12: Chart Filters

Status: done

**Why:** [ADR-0273](../../../adr/0273-the-user-narrows-the-range-by-dragging-across-the-plot.md), [ADR-0272](../../../adr/0272-a-chart-holds-no-view-state-it-raises-what-the-user-changed.md), [ADR-0251](../../../adr/0251-a-chart-is-defined-in-the-datas-terms-and-a-stale-gesture-is-refused.md).

- [x] The range typed in the axis' own values (dates, numbers, categories); a value that does not parse is said, not guessed
- [x] The axis switched between proportional and categorical, raised as an event and taken back as a parameter

**Blocked by:** 11.
