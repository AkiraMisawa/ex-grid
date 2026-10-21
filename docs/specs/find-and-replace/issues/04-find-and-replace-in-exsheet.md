# 04: Find and Replace in ExSheet, within the Sheet or the Workbook

Status: ready-for-agent
Roadmap: ExSheet — Find and Replace search the Sheet shown or the whole Workbook.

**Why:** [ADR-0332](../../../adr/0332-find-reaches-every-product-replace-is-a-write-asked-of-the-consumer-and-the-matches-in-view-are-marked.md) §2 and §5; ADR-0331 §8. Replaces `exsheet-workbook` ticket 10.

- [ ] ExSheet draws the panel itself, in both Chromes, with Within: Sheet (default) or Workbook
- [ ] A match on another Sheet shows it and selects the cell
- [ ] Replace matches the Entry; Replace All all or nothing, no cap, one undo step, count said; over the Workbook too
- [ ] Marks by ExSheet's own match function
- [ ] Layer 2 and layer 3; Docs Site and Change Note

**Blocked by:** 01, 02; `exsheet-workbook` 06.
