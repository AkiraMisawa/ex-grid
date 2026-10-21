# 12: A Board over sources held on a server

Status: ready-for-agent
Roadmap: internal

ADR-0341 (after ticket 02): an input binds to a source, not only to a Snapshot. A Board bound to an
`IShapedSource` checks each input by `BoardInput.CheckAsync` before anything is drawn; a grid view over a
fetching source is a Shaped Grid whose rows are fetched (ADR-0025), a pivot over a fetching report
source, and a link hands the source its condition to execute instead of narrowing a Snapshot in memory
(ADR-0340, Q5). A chart waits for ADR-0322's Chart Source.
