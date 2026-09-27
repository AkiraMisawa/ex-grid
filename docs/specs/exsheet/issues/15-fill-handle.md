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

The core half, 2026-09-27: the fifth ADR-0050 declaration. It covers DC-12, DC-14 and the
Fill Intent half of DC-13, and DC-15 for the drag.

- **`ExGrid.ShowFillHandle`** (`bool`, off by default) paints `ex-fill-handle` in the
  selection overlay. It sits at the bottom-right corner of the Selection's last range, in the
  pinned layer or the scrollable one, whichever holds the corner. A disjoint Selection shows
  no handle, and neither does an open edit. Its size is `GridMetrics.FillHandleSizePx`,
  emitted inline. The press that grabs it is read by arithmetic against the same corner,
  with a grab box twice its side, as every other press in the body is (ADR-0008).
- **The drag** uses the selection drag's own event path: the move handler is attached only
  while it runs, the edge band auto-scrolls it, and the button-up check ends it. It extends
  along one axis, the one the pointer went further along past the source, counted in cells.
  A tie goes vertical. The target is outlined as `ex-fill-target`. The Selection does not
  move. Only the overlay renders, and every row still skips its render.
- **On release**, one **`OnFill`** (`EventCallback<GridFillIntent>`) is raised:
  `Source`, `Target` (the cells to fill, never the source itself), `Direction`
  (`GridDirection`) and `RowSequenceVersion`. The grid writes nothing. A target covering a
  column that is not `Editable` is refused whole first, through `OnPasteRefused` with
  `TargetNotEditable`. That is ADR-0035's one gate for paste and fill. Nothing is raised in
  three cases: a release back inside the source, a drag whose button came up outside the grid,
  and a drag across a change of the Row Sequence Version.
- `FillRules` (`HandleRange`, `ExtensionFor`, `PlanFill`), `FillExtension` and
  `FillDecision` are the pure half. No JavaScript was added.

Layer 1: `FillRuleTests`. Layer 2: `FillHandleTests`.

What remains:

- ExSheet's meaning for the intent: copy with References shifted, a linear series, dates by
  day, refusing every other pattern, and one undo step. This covers the third and fourth
  checkboxes.
- Layer 3 for DC-13: a real drag by mouse under both Chromes, with the handle grabbed where it
  is painted, and the edge auto-scroll carrying a fill.
- Excel's Selection after a fill covers the source and the target. The grid leaves the
  Selection on the source, because ADR-0050 does not say otherwise and a Consumer may refuse
  the pattern. If the Excel behaviour is wanted, it needs a decision.
