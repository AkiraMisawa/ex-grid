# 02: The engine

Status: done

**What to build:** `ExPivot.Engine`, with no dependency: Pivot Fields, the Pivot Layout, the cube
and the report, as ADR-0059 describes; the Field List's rules as `PivotLayoutEdits`; the layout's
JSON as `PivotLayoutJson`.

**Blocked by:** 01

- [x] Items, their order and Hidden Items (PV-3, PV-5)
- [x] The eleven Aggregations, exact in `decimal`, totals from records (PV-4)
- [x] Compact, Outline, Tabular; subtotals; grand totals; collapse; Σ Values; Show Values As (PV-6)
- [x] Number formats with their caps; `General` at 15 digits (PV-7)
- [x] The Field List's rules as functions (PV-8), and the layout's JSON (PV-16)
- [x] A layout that only re-lays out reuses the cube (`PivotCube.Holds`)

## Comments

2026-09-30: `tests/ExPivot.Engine.Tests`, 158 tests.
