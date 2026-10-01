# 73: A column reached by the arrows is scrolled into view

Status: done

**What to build:** ADR-0058, "What Part B of the ninth Windows run settled", the sub-bullet under
"← and → point at the next column the table has", decided with the user on 2026-10-01 when ticket 71
was built without it. Ticket 71 dashes the column that ← or → reaches from a column header, and does
not scroll. In a grid narrower than its columns the dashed column can lie outside the view.

**Blocked by:** None (ticket 71 is merged).

- [x] When ← or → after a column header press reaches a column, the grid scrolls it into view across:
      the column whole inside the client area, beside the vertical Scrollbar Gutter. The vertical
      offset stays as it is. A column already whole in view, or pinned, does not move the grid (SH-35)
- [x] The grid answers a request to scroll a column into view across only (DC-55), beside
      `GridPointedAt.RevealAsync` for a cell. The offset comes from `ColumnGeometry.ScrollLeftToReveal`,
      so nothing is measured; only the scroll offset is set, through the JavaScript already allowed for
      it (ADR-0021). Every public member keeps its XML doc comment
- [x] ↓ from a header, and the arrows from a cell, reveal as they do today
- [x] Layer 2 in ExGrid for the reveal (a column out of view to the right and to the left, one whole in
      view, a pinned one: the offset set, or nothing set) and in ExSheet for the Scope asking for it.
      Layer 3 in `pointing-scope.spec.mjs` on `/pointing?narrow`: `=`, a press on Id's header, →; the
      text is `=Positions[PV]`, PV's dashes lie whole inside the scroller's client area, and
      `scrollTop` is unchanged. Then ←: `=Positions[Id]`, Id whole in view (DC-55, SH-35)

## Comments

2026-10-01, implemented on `agent/ps-58`.

- **ExGrid (DC-55).** `GridPointedAt.RevealColumnAsync(column)` sits beside `RevealAsync` for a cell.
  It answers whether the grid shows the column, and false for a name it does not show or a
  declaration given no grid. The grid arms a column reveal, and `StageReveal` stages it at the top
  of the render, as every reveal is staged. The new offset is
  `ColumnGeometry.ScrollLeftToReveal(column, scrollLeft)`, so a pinned column, or one already whole
  in view, answers the offset the grid has, and nothing is written. The width it reveals against
  is the grid's visible width, which has the reported vertical gutter taken out (ADR-0013), so the
  column ends beside the gutter. The vertical offset passed is the grid's own (`_scrollTopPx`): the rows stay where they
  are painted, as they do when a cell's row is already in view. The write is the existing
  `setScrollOffset`, with the existing reveal token; no script, listener or read is added. A cell
  or a column asked for before the render replaces the other, so the later request is revealed.
  The tail of `StageReveal` (the no-op check, the model moving first, the numbered write) is now
  `StageScroll`, which both paths share. The Selection and the Focus do not move, and a reveal of
  the grid's own Focus is never armed, even with the Focus out of view.
- **ExSheet (SH-35).** `PointingScope.PointArrowAsync` now asks the grid to reveal whatever the
  arrow dashed. A cell goes through `RevealAsync`, as before, and a column through
  `RevealColumnAsync`. The registered grid's `RevealAsync(PointDashes)` chooses between the two by
  whether the dashes name a row. ↓ from a header and the arrows from a cell reveal as before; their
  tests are unchanged and pass.
- **Docs and the DemoHost.** The Scope's and the Sheet's doc comments, `src/ExSheet/README.md` and
  `/pointing`'s prose say that a column reached is scrolled into view across. The `?narrow`
  paragraph says that → from Id's header brings PV into view.
- **"Whole inside the client area" is read across.** A column's dashes run down the painted rows
  (DC-53), and the Viewport cuts them top and bottom, so only their left and right sides can lie
  inside the client area. The layer 3 test checks those two sides, and `scrollTop` for the rows.
- **Tests.** Layer 2, `PointedStepTests` +5, in a 200 px grid scrolled 100 px down:
  - a column out of view to the right, with a reported 12 px vertical gutter, is written at
    `(100, 112)`: Amount ends at the client area's edge, and its header is painted;
  - a column out of view to the left is written at `(100, 0)`;
  - two columns whole in view write nothing, while the grid's own Focus stands out of view on its
    last cell; a third, half in view, is written at `(100, 0)`, not at the Focus's row;
  - a pinned column writes nothing, and the column under it comes back clear of it;
  - a name the grid does not show, and a declaration with no grid, answer false; an empty or null
    name throws.

  `PointingScopeKeyboardTests` +1, on a 250 px registered grid scrolled 40 px down (`ScopedSheets`
  gains `NarrowAsync`, and the Sheet tests' `ScrollToAsync` takes any grid). After a press on Id's
  header, → to Book writes no offset, → to Value writes `(40, 50)`, ← to Book writes none, and ← to
  Id writes `(40, 0)`. The new tests failed first against a stub, then for the Scope before it
  asked, and pass now. Layers 1–2: 826 + 2082 + 88 + 1190 (1 skipped, as before) + 379, all passing.

  Layer 3, `pointing-scope.spec.mjs`, gains one test in the `/pointing?narrow` describe. The grid
  is scrolled 280 px down, and the test waits until it no longer paints R-1. Off macOS it requires
  the vertical gutter to occupy layout, as ticket 72's test does. PV's header ends past the client
  area. Then `=`, a press on Id's header, and → give `=Positions[PV]`. `scrollLeft` moves, and PV's
  dashes lie inside the client area across, with their right side within 1 px of the client
  area's right edge, against the gutter. They are down PV's header, and `scrollTop` is the same
  number as before. ← gives `=Positions[Id]`, with `scrollLeft` 0, Id's dashes inside the client
  area, and `scrollTop` unchanged. The keyboard stays in the Sheet, the grid has no Selection, and
  nothing is refused. It ran headless on macOS, chrome, on private ports, as one run of the spec file
  on each host: WebAssembly, 24 passed and 1 skipped (the Server-only SH-35 test); Server, 25
  passed. On macOS the gutter is 0 there (ticket 72's Comments), so the check against a real gutter
  is CI's Linux run.
- **Left open:** nothing in this ticket. Seen while reading, and not changed: `ExSheet`'s
  `OnPointingRefused` doc comment still lists "an arrow after a press on a column's header" among
  the refusals. Since ticket 71, such an arrow is refused only when the first row cannot be written
  or the column is no longer shown.

