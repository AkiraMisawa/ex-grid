# 03: References, the dependency graph, and Error Values

Status: ready-for-agent

**What to build:** The whole Reference grammar and a recalculation that is incremental and never shows a half
result. That covers `$A$1`, `A$1`, `A1:B2`, `A:A`, `1:1`, the Sheet qualifier (parsed and
recorded, one Sheet), operators with Excel's precedence, and `&`. Only dependents recompute. A
cycle is `#CIRC!` in every member and every dependent. Error Values propagate.

**Blocked by:** 02

- [ ] Every Reference form parses, and the invariant syntax round-trips (ADR-0047)
- [ ] Only dependents of a change recompute (layer 1, counted)
- [ ] A cycle shows `#CIRC!` in each member and each dependent; breaking it recovers (ADR-0047)
- [ ] `#DIV/0!`, `#VALUE!` and the rest propagate as Excel's do
- [ ] No Window is pushed with Values from an unfinished recalculation (layer 2)

## Comments
