# 15: The fill handle and the Fill Intent

Status: ready-for-agent

**What to build:** The fifth ADR-0050 declaration. The core paints the handle at the corner of the Selection's last
range, owns the one-axis drag and its outline, checks `Editable`, and raises a Fill Intent on
release. A disjoint Selection shows no handle. ExSheet fills as Excel does for copy (with
References shifted), linear series from two or more numbers, and dates by day. It refuses every
other pattern. One fill is one undo step.

**Blocked by:** 03, 05, 12

- [ ] Off by default: no handle is painted (existing suites green)
- [ ] A drag raises one Fill Intent with source, target and direction; the grid writes nothing (ADR-0050)
- [ ] 1, 2 dragged gives 1, 2, 3, 4; a date gives the following days; a Formula shifts its References
- [ ] `Item 1` dragged is refused, not filled with copies
- [ ] A target covering a non-editable column is refused whole (ADR-0035)

## Comments
