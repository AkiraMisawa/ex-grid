# 03: RI-9's floating inspector is not moved by its title bar

Status: done
Roadmap: ExGrid — A floating Row Inspector dragged by its title bar always follows the drag.

**Seen:** `inspectors.spec.mjs:160`, "RI-9: floating: an inspector is dragged by its title bar, and
closes on its own", Server host, Chrome and Edge, shard 2/4, in both runs: the offset expected
`[120, 90]`, received `[0, 0]` (line 180).

- [x] Find whether the drag is lost or read too early.
- [x] Fix it in the product, or where the test reads.
- [x] The test passes 20 runs under the widened conditions.

**Found (2026-10-20):** the drag was lost. The DemoHost's floating panel rendered the layer that
takes a drag's moves and its release only in the render that answered the press: on the Server host a
round trip later. Moves and a release made sooner, at machine speed, reached nothing, and the panel
stayed put.

**Fixed:** the layer is always in the markup and shown by the stylesheet while the title bar is
pressed (`:active`, but not on its close button), so it is there from the press.
