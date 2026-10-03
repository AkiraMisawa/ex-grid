# 72: A narrow view of `/pointing`

Status: done

**What to build:** ADR-0058, "What Part B of the ninth Windows run settled", the bullet on the
positions grids that could not be narrowed. DC-53 asks that the dashes and outlines stay inside the
grid's client area beside its own Scrollbar Gutter, and Part B could not set that up: neither page
wires column resizing, and the grids' widths are the page's.

**Blocked by:** None.

- [x] `/pointing?narrow` lays the positions grid out narrower than its columns, so it shows both of
      its own scrollbars, and its last column, PV, runs under the vertical gutter until the grid is
      scrolled right. Without the parameter the page is as today, so no spec that reads it changes.
      The DemoHost's page index names the view
- [x] Layer 3 on `/pointing?narrow`, both hosts: the grid scrolled to its last row and its last
      column, `=` and a press on R-40's PV; the dashes and both column outlines lie inside the
      scroller's client area, not under either gutter (DC-53). On macOS headless the horizontal
      scrollbar is an overlay; the assertion is what CI's Linux run checks
      (`tests/ExGrid.Browser/README.md`)

## Comments

2026-10-01, implemented on `agent/ps-57`.

- **The page.** `?narrow` (`[SupplyParameterFromQuery]`, present or not) gives the positions grid
  `ViewportWidth` 260 instead of 330, through the grid's parameter. The columns are 300 px together,
  so the grid shows both of its scrollbars, and PV (180 to 300 px) runs under the vertical gutter until
  the grid is scrolled right. 260 keeps part of Id in view once the grid is scrolled to its last
  column, so both of the lookup's column outlines can be seen beside the gutters. With `?narrow`, a
  paragraph, `#pointing-narrow`, says what the view is for. Without it, the page renders as before:
  330 px, and no paragraph. The index's entry for `/pointing` names the view in its description. A
  second link would have broken RI-1, which wants one link per routed page.
- **Layer 3.** `pointing-scope.spec.mjs` gains a `/pointing?narrow` describe, with one test. The grid
  overflows on both axes, and PV ends past the client area before scrolling. The scroller is scrolled
  to its last row and its last column, then `=` is typed and R-40's PV pressed. R-40's PV ends at the
  client area's right edge and at its bottom edge (within 1 px), so the case lies against both
  gutters. The dashes lie whole inside the client area, under the header. Both column outlines end at
  R-40's bottom, and neither runs past the client area's right or bottom edge. Their tops (under the
  header) and Id's left side (scrolled out) are cut by the Viewport's edges, not by a gutter. The
  measured gutters are recorded as an annotation. `lookup`, `table` and `pointFrom` moved from the
  `/pointing` describe to the module so that both describes share them.
- **The gutters on macOS are not checked here.** Headless Chrome on macOS measured both strips as 0 on
  `/pointing?narrow`, not only the horizontal one: client area 260 × 280, `scrollWidth` 300, PV's right
  edge 40 px past the client area's. A throwaway run that forced classic bars, as
  `scrollbar.spec.mjs` does, still measured 0 × 0. There the assertions about the right and bottom
  sides check against no gutter at all. So the test requires both strips to occupy layout everywhere
  but macOS, and CI's Linux run is the one that checks DC-53 against a gutter. It is not weakened to
  pass here: on macOS it skips only that guard.
- **Run** (macOS, headless, `--project=chrome`): `pointing-scope.spec.mjs` on the Server host, 23
  passed. On WebAssembly, 21 passed and 1 skipped (the Server-only SH-35 test). The new test first
  failed at the vertical-gutter guard, measured 0, and passed alone once the guard applied only off
  macOS. Layers 1 and 2 pass.
