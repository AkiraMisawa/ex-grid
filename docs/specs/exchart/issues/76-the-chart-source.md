# 76: The Chart Source

Status: done
Roadmap: ExChart — A chart of a large table reads only what its pixels show, thinned where the data lives.

**Why:** [ADR-0322](../../../adr/0322-a-chart-reads-its-data-through-a-chart-source-that-thins-where-the-data-lives.md).

- [x] The seam: asked for a view's range at the scene's pixel grid, it answers each pixel's first, last, lowest and highest, so the scene's PixelSummary counts the data's points, not the answer's
- [x] Its reference implementation over a Snapshot
- [x] Answers name the version and the view they were read for; one for a view or a version no longer shown is dropped, and the previous scene stays until the matching answer arrives, with Busy meanwhile
- [x] The component reads through it on a change of view (ADR-0272), and its domain comes from the source

**Blocked by:** 74.

## Comments

- 2026-10-22: the seam, the reference implementation and a Board's asking were built with ADR-0344 (#229); `SnapshotChartData` adapts a Snapshot to it. The component's part is `ExChart`'s `Spec` and `Sources`: it asks on its first drawing and on each change of range, keeps the scene and is Busy until the newest ask is answered, drops an overtaken answer and says an older version's in its place. "Its domain comes from the source": unnarrowed, the source answers the data's whole range, and the axis spans it.
