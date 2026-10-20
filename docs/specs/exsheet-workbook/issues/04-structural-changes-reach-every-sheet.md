# 04: Inserting and deleting rows and columns rewrites References on every Sheet

Status: done
Roadmap: ExSheet — Inserting a row on one Sheet keeps the Formulas on other Sheets pointing at the same cells.

**Why:** [ADR-0331](../../../adr/0331-exsheet-holds-a-workbook-of-sheets-behind-sheet-tabs.md) Consequences; ADR-0046 ("the engine belongs to the workbook").

- [x] Row and column insertion and deletion on one Sheet rewrite References to it on every Sheet, as on its own
- [x] Cut and paste (a move) and fill across Sheets rewrite as Excel does
- [x] Copying between Sheets shifts relative References as Excel does
- [x] An insertion or deletion on a Sheet of a Workbook of several is no longer refused with `NotSupportedException` (01)

**Blocked by:** 01.

**Settled while building (2026-10-20):** an insertion or deletion on one Sheet rewrites, on every other
Sheet, each Reference qualified with its name as one of its own is rewritten (`StructuralEdit.Map`),
and recalculates those Formulas and any whose Reference reaches the edit (`Sheet1!1:1` under a column
insertion); the undo puts them back exactly. Copying between Sheets needed nothing new: a `SheetBlock`
pasted on another Sheet shifts its relative References as on its own, and Excel does the same. The
engine has no cut (no move of cells) and no fill across Sheets, which in Excel needs grouped Sheets,
outside ADR-0331's basic tab operations; neither has anything to rewrite until one is added.
