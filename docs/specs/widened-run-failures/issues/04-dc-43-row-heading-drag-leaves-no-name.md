# 04: DC-43's Row Heading drag leaves the Name Box empty

Status: done
Roadmap: ExGrid, ExSheet — A Row Heading drag at the bottom edge always ends with its selection named.

**Seen:** `headings.spec.mjs:240`, "DC-43: a Row Heading drag at the bottom edge scrolls down, and never
sideways", Server host, Chrome shard 2/4 on `main` and Chrome and Edge shard 2/4 on the branch: the
Name Box expected "A3", received "" (line 257).

- [x] Find whether the drag's selection is lost or read before it is answered.
- [x] Fix it in the product, or where the test reads.
- [x] The test passes 20 runs under the widened conditions.

**Found (2026-10-20):** the release was lost. While the edge band scrolls, the render that paints
the rows under the pointer is behind the scroll, so the pointer at the bottom edge stands over the
scroller's bare spacer, not the Viewport. A release there reached no handler, the Heading drag never
ended, and the Name Box kept showing nothing while the Size Tip stood (seen in 6 of 20 runs; the Input
Journal had no release).

**Fixed:** the grid's capture-phase release listener hands a release on its own scroller or spacer to
the Viewport, as the release it was, so a drag ends wherever the button comes up.
