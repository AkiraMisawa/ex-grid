# 05: The MudBlazor Wrapper

Status: done

**What to build:** `ExPivot.MudBlazor` (ADR-0062): `MudPivotChrome`, drawing the pane, the menus, the
panels and the band with MudBlazor's controls and dressing the report grid with `MudGridChrome` in
ExPivot's words, and `mud-ex-pivot.css`, on `MudExGridPaper`.

**Blocked by:** 04

- [x] The same gestures make the same layout as the built-in markup (PV-9)
- [x] A MudSelect's list takes Escape before its panel (PV-11)
- [x] Every Visual Token mapped onto the palette, no geometry, no rule against a core class, both schemes (PV-18)
- [x] The report's Context Menu in ExPivot's words, the Consumer's `Label` included (PV-19)
- [x] No JavaScript of its own (PV-10)
- [x] `/pivot?chrome=mud` on both hosts (PV-20)

## Comments

2026-09-30: `tests/ExPivot.MudBlazor.Tests`, 27 tests; `pivot.spec.mjs` under both Chromes.
