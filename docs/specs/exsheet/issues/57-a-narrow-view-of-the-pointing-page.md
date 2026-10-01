# 57: A narrow view of `/pointing`

Status: ready-for-agent

**What to build:** ADR-0058, "What Part B of the ninth Windows run settled", the bullet on the
positions grids that could not be narrowed. DC-53 asks that the dashes and outlines stay inside the
grid's client area beside its own Scrollbar Gutter, and Part B could not set that up: neither page
wires column resizing, and the grids' widths are the page's.

**Blocked by:** None.

- [ ] `/pointing?narrow` lays the positions grid out narrower than its columns, so it shows both of
      its own scrollbars, and its last column, PV, runs under the vertical gutter until the grid is
      scrolled right. Without the parameter the page is as today, so no spec that reads it changes.
      The DemoHost's page index names the view
- [ ] Layer 3 on `/pointing?narrow`, both hosts: the grid scrolled to its last row and its last
      column, `=` and a press on R-40's PV; the dashes and both column outlines lie inside the
      scroller's client area, not under either gutter (DC-53). On macOS headless the horizontal
      scrollbar is an overlay; the assertion is what CI's Linux run checks
      (`tests/ExGrid.Browser/README.md`)

## Comments
