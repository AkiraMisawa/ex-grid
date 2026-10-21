# 02: Replace in ExGrid, asked of the Consumer

Status: ready-for-agent
Roadmap: ExGrid — Ctrl+H replaces the current match or every match, the Consumer doing the write.

**Why:** [ADR-0332](../../../adr/0332-find-reaches-every-product-replace-is-a-write-asked-of-the-consumer-and-the-matches-in-view-are-marked.md) §2 and §3.

- [ ] Ctrl+H opens the panel with Replace, offered only when the Replace Request is handled and a visible column is Editable; refused and said otherwise
- [ ] Replace: one Edit Intent on the current match, then the next; Replace All: one request, answered with a count or a refusal naming cells
- [ ] `GridSource.From` answers it (layer 1, exhaustive like Find), all or nothing
- [ ] Both Chromes; an input in the Input Order; layer 2 and layer 3; Docs Site and Change Note

**Blocked by:** 01.
