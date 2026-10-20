# 74: The Chart Source

Status: open
Roadmap: ExChart — A chart of a large table reads only what its pixels show, thinned where the data lives, and follows the table as it changes.

**Why:** [ADR-0322](../../../adr/0322-a-chart-reads-its-data-through-a-chart-source-that-thins-where-the-data-lives.md).

- [ ] The seam, its reference implementation, and answers that name the table version they were read from
- [ ] The Snapshot's bridge in ExChart; a chart drawn from a spec follows its handle's latest Snapshot

**Blocked by:** 70.
