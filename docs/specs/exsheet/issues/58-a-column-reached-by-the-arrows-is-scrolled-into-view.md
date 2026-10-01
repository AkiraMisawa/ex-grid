# 58: A column reached by the arrows is scrolled into view

Status: ready-for-agent

**What to build:** ADR-0058, "What Part B of the ninth Windows run settled", the sub-bullet under
"← and → point at the next column the table has", decided with the user on 2026-10-01 when ticket 56
was built without it. Ticket 56 dashes the column that ← or → reaches from a column header, and does
not scroll. In a grid narrower than its columns the dashed column can lie outside the view.

**Blocked by:** None (ticket 56 is merged).

- [ ] When ← or → after a column header press reaches a column, the grid scrolls it into view across:
      the column whole inside the client area, beside the vertical Scrollbar Gutter. The vertical
      offset stays as it is. A column already whole in view, or pinned, does not move the grid (SH-35)
- [ ] The grid answers a request to scroll a column into view across only (DC-55), beside
      `GridPointedAt.RevealAsync` for a cell. The offset comes from `ColumnGeometry.ScrollLeftToReveal`,
      so nothing is measured; only the scroll offset is set, through the JavaScript already allowed for
      it (ADR-0021). Every public member keeps its XML doc comment
- [ ] ↓ from a header, and the arrows from a cell, reveal as they do today
- [ ] Layer 2 in ExGrid for the reveal (a column out of view to the right and to the left, one whole in
      view, a pinned one: the offset set, or nothing set) and in ExSheet for the Scope asking for it.
      Layer 3 in `pointing-scope.spec.mjs` on `/pointing?narrow`: `=`, a press on Id's header, →; the
      text is `=Positions[PV]`, PV's dashes lie whole inside the scroller's client area, and
      `scrollTop` is unchanged. Then ←: `=Positions[Id]`, Id whole in view (DC-55, SH-35)

## Comments
