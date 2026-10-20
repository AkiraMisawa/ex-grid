# 18: "Add to chart" adds a series to a chart already drawn

Status: done

**Why:** [ADR-0258](../../../adr/0258-the-selection-bridge-follows-the-rows-it-was-made-from.md) (M3-Q4, the user's U7 and U8).

- [x] "Add to chart: *name*" for each open chart the Consumer registered as a target
- [x] The added series keeps rows of its own and takes its panel by the Header Group defaults
- [x] A series already in the chart, or one whose axis does not match the chart's (a curve into a history), is refused by name

**Blocked by:** 15, 16.

Roadmap: internal
