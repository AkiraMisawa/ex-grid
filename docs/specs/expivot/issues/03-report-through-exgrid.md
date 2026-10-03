# 03: The report drawn by ExGrid

Status: done

**What to build:** `ExPivot`, the component: one ExGrid over the Pivot Report (ADR-0059), the `±`
button, the Context Menu's pivot commands, Show Details, and ADR-0063's `OnCellDoubleClick` in the
core.

**Blocked by:** 02

- [x] One grid, the label columns pinned, Header Groups over the value columns, Row Kinds (PV-2)
- [x] Expand and collapse from the button, the Context Menu and a double click; the Focus stays (PV-13)
- [x] A refresh that changes only values keeps the Row Sequence Version (PV-13)
- [x] Show Details from a double click and the Context Menu (PV-14); DC-63 in the core
- [x] A Field List interaction renders no grid row (PV-15)

## Comments

2026-09-30: `tests/ExPivot.Components` and `tests/ExGrid.Components/CellDoubleClickTests.cs`.
