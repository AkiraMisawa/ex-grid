# Nodes and edges are two Snapshots, and ExChart lays out nothing

*Decided with the user, 2026-10-20, in the grilling of the Chart Spec (AI-Q6; [ADR-0320](0320-an-ai-draws-a-chart-by-writing-a-chart-spec-that-names-data-it-never-holds.md)). The binomial
lattice Showcase (PR #199) drew its tree as page code; a lattice, a trinomial tree or a network of
counterparties is data an AI should be able to chart.*

A lattice is points joined by lines that are not a series: a node at step i with j up-moves joins two
nodes at step i + 1. Neither a line series (one path through its points) nor a scatter (no lines)
says it.

## Decisions

- **Two Snapshots.** The nodes carry a key, an x and a y column, and may carry the columns a colour
  or a size is read from (ADR-0320). The edges carry only a from-key and a to-key column. A spec
  names them: `"edges": {"data": "lattice_edges", "from": "from", "to": "to"}`.
- **An edge is drawn as a line between its nodes' positions.** It has no position of its own, and
  nothing about its look is inferred from the nodes'.
- **No lattice-specific kind.** A binomial tree, a trinomial tree and a network are the same two
  tables; a kind per lattice would be a kind per Consumer.
- **ExChart computes no layout.** Where a node lies is a column. A force or tree layout is
  computation, which the family does not execute (spine rule 3); a capability computes it, and the
  AI calls that capability first.
- **Refused by name:** an edge whose endpoint is not a node, two nodes with one key, and a node
  without a position. A node no edge reaches is drawn, alone, as given.
- **The tooltip reads a node**, its key and its columns; an edge says its two keys.
  *Settled while building it, 2026-10-22 (ticket 73):* "its columns" are the ones the spec names in
  `tooltip`, besides its colour's and its size's, never every column of the table; the pointer finds
  an edge where no node is within reach.
- **"View as table" lists the nodes, then the edges** ([ADR-0275](0275-a-chart-is-read-aloud-as-a-summary-and-as-a-table.md)), and the summary says "N nodes
  and M edges".

## Consequences

- Node and edge series join the Chart Spec's schema in its first version (ADR-0320).
- Thinning ([ADR-0254](0254-past-the-plots-pixels-a-series-is-thinned-to-its-minimum-and-maximum.md)) does not apply to edges; a chart with more edges than the plot can show is a
  question for when one is measured, not answered here.
