# 04: The Field List, its menus and panels

Status: done

**What to build:** Excel's "PivotTable Fields" pane (ADR-0061): the fields with their checkboxes and
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
the Areas out of view; ADR-0061 was revised (before it was decided) to open it at its static
position, as wide as the pane.

2026-10-05: fixed the Field List menu remaining open after a report press, and Escape being lost
after a press on a disabled command. ADR-0061 and PV-11 now distinguish outside dismissal, which
keeps the destination's keyboard, from Escape's return to the opener. The shared menu frame can
take focus without another Tab stop. Browser cases cover both Chromes, including back-to-back
operations behind a circuit's round trip; component tests deliver an old rendered dismissal after
a replacement menu or panel exists, and preserve the render-count and instance-independence checks.
