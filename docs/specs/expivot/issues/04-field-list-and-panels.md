# 04: The Field List, its menus and panels

Status: done

**What to build:** Excel's "PivotTable Fields" pane (ADR-0060): the fields with their checkboxes and
search, the four Areas, drag and drop on Blazor's own events, each entry's menu, Filter…, Field
Settings…, Value Field Settings…, the report filter band, and the `IPivotChrome` seam.

**Blocked by:** 03

- [x] Ticking, dragging, reordering, removing, with the same layouts as the rules (PV-8)
- [x] A substituted Chrome is handed the same rules (PV-9)
- [x] Menus and panels under their entry, as wide as the pane, one at a time, Escape and the keyboard's way back (PV-11)
- [x] The report filter band (PV-12)
- [x] Drag and drop in a real browser, both hosts (PV-10)

## Comments

2026-09-30: seen in a browser, a panel opened in the flow was only half the pane wide and pushed
the Areas out of view; ADR-0060 was revised (before it was decided) to open it at its static
position, as wide as the pane.
