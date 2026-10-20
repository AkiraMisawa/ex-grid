# 74: The Snapshot bridge

Status: done
Roadmap: ExChart — A Chart Spec draws a Snapshot by its columns' names, and the next version of the table is drawn by reading the spec against it.

**Why:** [ADR-0322](../../../adr/0322-a-chart-reads-its-data-through-a-chart-source-that-thins-where-the-data-lives.md).

- [x] `SnapshotChartData` in ExChart: a Snapshot read by column name through the Engine's `IChartData`, with the version it was read from
- [x] A chart drawn from a spec follows its handle's latest Snapshot: the host reads the spec again against the Snapshot it is given; a Snapshot never changes in place

The Chart Source that thins where the data lives is ticket 76.

**Blocked by:** 70.
