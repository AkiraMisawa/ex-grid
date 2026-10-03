# 31: Drag a Reference Outline to rewrite its Reference, with its corner squares

Status: needs-triage

**Not built, and kept in view on purpose** (ADR-0057, "Not done", decided with the user
2026-09-30). The corner squares of a Reference Outline wait for this gesture, so that they never
offer a drag that does nothing. Whoever builds the gesture builds the squares with it.

**Needs a decision before an agent can take it.** It is a new pointer gesture over the grid. It
would compete for the same presses with the fill handle (ADR-0050, item 5) and with a selecting
drag (ADR-0008, ADR-0052). Record that in an ADR first.

What Excel does, from the eighth Windows run
(`verification/2026-09-29-windows-excel-8/range-finder.md`, `shots/Z01-*`, `Z23-*`):

- Every Reference Outline has a **5×5 px square at each corner**, in the Reference's colour, with a
  1 px margin of white around it. The squares keep that size in screen pixels at 400% zoom.
- On the cell being edited, only the squares show. Its line lies under the active cell's border,
  and its fill under the editor's ground.
- Excel's documented behaviour, not yet observed here: dragging an outline's edge moves the
  Reference (`A1:A5` becomes `C1:C5`), and dragging a corner resizes it (`A1:A5` becomes `A1:B8`).
  The Formula's text is rewritten as the drag goes.

To ask of Excel when this is picked up: whether the cursor changes over an edge and over a square,
what Escape does mid-drag, whether the drag auto-scrolls at the Viewport's edge, and what a drag
does to a structured reference's outline in another grid.

## Comments
