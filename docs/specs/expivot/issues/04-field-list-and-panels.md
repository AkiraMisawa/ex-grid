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

2026-10-05: extended removal to a drop on the same ExPivot's report, including its empty state.
The indication says Remove Field; only the dragged placement is removed, following Defer Layout
Update. Details, other pivots, list fields, external or cancelled drags and Σ Values do nothing.
Drag and drop callbacks carry the source and rendered layout so a late entry index cannot remove
or move a different field. The Field List's heading now has a close icon under both Chromes; its
body scrolls independently, keeping the icon reachable when cramped, and closing keeps pending
edits. ADR-0061, PV-10 and PV-30 record the extension.
The review found that a view's delayed DOM lambda could still read a replacement Context. Both
views now capture the render's context. Four component cases retain the actual rendered dragstart
and drop bindings under both Chromes: all four failed before this fix and pass with it. The
cramped-pane browser case also scrolls with Field Settings open and checks that the panel tracks
its opener while the close button remains reachable.

2026-10-05: CI run 37323504879 exposed a scroll defect: after the heading/body split, an
absolutely positioned popup still used the outer pane as its containing block. Its opener
scrolled while the popup stayed behind. The full-width body now contains both, with horizontal
padding inside that width, preserving the fixed heading. A nonzero body scroll reproduced a
122 px separation under both Chromes before the fix. The browser test now checks two scroll
offsets after the opening focus has arrived, reads both boxes together, and reaches the panel's
last action before using the heading's close button. A separate negative test for panel
dismissal also waits for opening focus before pressing the report; with a 150 ms round trip its
old setup failed on every run by racing that focus request. Neither test uses a fixed delay.
