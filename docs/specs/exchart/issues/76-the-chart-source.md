# 76: The Chart Source

Status: ready-for-agent
Roadmap: ExChart — A chart of a large table reads only what its pixels show, thinned where the data lives.

**Why:** [ADR-0322](../../../adr/0322-a-chart-reads-its-data-through-a-chart-source-that-thins-where-the-data-lives.md).

- [ ] The seam: asked for a view's range at the scene's pixel grid, it answers each pixel's first, last, lowest and highest, so the scene's PixelSummary counts the data's points, not the answer's
- [ ] Its reference implementation over a Snapshot
- [ ] Answers name the version and the view they were read for; one for a view or a version no longer shown is dropped, and the previous scene stays until the matching answer arrives, with Busy meanwhile
- [ ] The component reads through it on a change of view (ADR-0272), and its domain comes from the source

**Blocked by:** 74.
