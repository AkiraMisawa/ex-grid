# 73: Nodes and edges

Status: ready-for-agent
Roadmap: ExChart — A lattice or a network is drawn from a table of nodes and a table of edges.

**Why:** [ADR-0321](../../../adr/0321-nodes-and-edges-are-two-snapshots-and-exchart-lays-out-nothing.md).

- [x] Edges drawn between their nodes' positions, cut to the plot; refusals for a missing endpoint, a repeated key, a node without a position
- [x] A node's tooltip (the Observation's, named by its key when it has no label; its colour's and its size's values), the table (edges after nodes) and the summary ("N nodes and M edges"); the Chart Spec's `key` and `edges`
- [ ] A node's tooltip carries no column the spec does not name: a spec field naming more columns for it is still to do
- [ ] An edge's tooltip says its two keys: the pointer finds only marks today, so finding the nearest edge is still to do
