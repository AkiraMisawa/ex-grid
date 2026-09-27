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
