# 15: The fill handle and the Fill Intent

Status: done

**What to build:** The fifth ADR-0050 declaration. The core paints the handle at the corner of the Selection's last
range, owns the one-axis drag and its outline, checks `Editable`, and raises a Fill Intent on
release. A disjoint Selection shows no handle. ExSheet fills as Excel does for copy (with
References shifted), linear series from two or more numbers, and dates by day. It refuses every
other pattern. One fill is one undo step.

**Blocked by:** 03, 05, 12

- [x] Off by default: no handle is painted (existing suites green)
- [x] A drag raises one Fill Intent with source, target and direction; the grid writes nothing (ADR-0050)
- [x] 1, 2 dragged gives 1, 2, 3, 4; a date gives the following days; a Formula shifts its References
- [x] `Item 1` dragged is refused, not filled with copies
- [x] A target covering a non-editable column is refused whole (ADR-0035)

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

2026-09-27, engine half: `SheetEdit.Fill(source, target, direction)` resolves a Fill Intent as
Excel fills, one line at a time (a column for a vertical fill, a row for a horizontal one), and
is one `SheetStep`. Formulas are copied with their relative References shifted, repeating the
source; two or more numbers continue as Excel's linear trend, the least-squares line through
them with the source cells left as typed (Microsoft's examples: 1, 2 → 3, 4, 5; 1, 3 → 5, 7, 9;
100, 95 → 90, 85; 1, 3, 4 → 5.67, 7.17, 8.67); a single date shown with a date-only format goes
on by one day per cell, backwards when filling up or left; a single number is copied, as the
plain drag copies it; text with no pattern, booleans, Error Values and blanks are copied,
repeating the source. Formats and alignment repeat from the source. Everything else is refused
with `SheetRefusalReason.FillPatternNotSupported`, naming the cell, before anything is written:
text holding a digit (`Item 1`, `Q1`), the day and month names of Excel's built-in lists (in
English and in the Sheet's culture), two or more dates, a time of day, and numbers mixed with
anything else. A target that does not extend the source along one axis is refused as
`FillShapeNotSupported`; the target may be given as the new cells alone or as the whole
extended range (`FillTests`). Covers the engine side of the third and fourth criteria.
**What remains is the component's:** the fill declaration, the handle and the drag, one Fill
Intent per drag, `Editable` on the target, and handing the intent to `SheetEdit.Fill`. Reported
as uncertain against Excel: the last binary digit of a trend value, the backwards repetition
of a multi-cell pattern filled up or left, and Formulas mixed with text in one source (copied
here).

2026-09-27, ExSheet wiring: ExSheet declares `ShowFillHandle`. `OnFill` maps the intent's
`Source`, `Target` and `Direction` onto `SheetEdit.Fill` and asks `Sheet.Check` first,
synchronously, before the handler's first await. A refusal (`FillPatternNotSupported`,
`FillShapeNotSupported`) calls `GridFillIntent.Refuse()`, so the Selection stays on the source,
and puts the engine's sentence, which names the cell, in the notice. Anything else is done
through the undo stack as one step, and the grid then selects the source and the target together
(ADR-0050, item 5 refined). An intent under a Row Sequence Version other than 0 is refused. Layer 2
drives a real drag through the grid's handle in `FillWiringTests`: the handle is painted, 1, 2 → 3,
4 selecting A1:A4, a date by day, a Formula with its References shifted, `Item 1` refused with
nothing written and the Selection on the source, and one undo. The first, second and fifth
criteria are the core's, covered by its `FillHandleTests`, and ExSheet declares every column
Editable. Layer 3 for DC-13 (a real mouse drag under both Chromes, and the edge auto-scroll)
belongs to ticket 18.
