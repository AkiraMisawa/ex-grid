# 03: Find in ExPivot

Status: ready-for-agent
Roadmap: ExPivot — Ctrl+F finds a label or a value in the report as shown.

**Why:** [ADR-0332](../../../adr/0332-find-reaches-every-product-replace-is-a-write-asked-of-the-consumer-and-the-matches-in-view-are-marked.md) §1.

- [x] ExPivot answers Find over the laid-out cells, by displayed text; collapsed groups not searched, nothing expanded
- [x] Passes the reference match function, so matches are marked
- [ ] Ctrl+H refused and said
- [x] Both Chromes; layer 2 and layer 3; Docs Site and Change Note

**Blocked by:** 01 (done). The Ctrl+H item waits on 02, which gives ExGrid Ctrl+H and its refusal.

## Comments

- 2026-10-21: Find in ExPivot is built: the report grid's `OnFind` walks the report by the Report
  Version on screen, reading the cells through the report source's Copy in blocks (a server's `copy`
  transport), and `FindMatch` passes the reference rule. Layer 2 (`FindTests`, `MudPivotFindTests`),
  layer 3 (`pivot-find.spec.mjs`), the Docs Site's Layout page (Find, with an Example in both Chromes)
  and the Change Note `find-in-expivot.md` are in. Left open: **Ctrl+H refused and said**. On `main`
  ExGrid does not take Ctrl+H at all; ticket 02 makes the grid take it and raise `OnFindRefused` with
  `FindRefusalReason.ReplaceUnavailable` where no `OnReplace` is wired, which is always so in ExPivot.
  Once 02 is merged, ExPivot binds `OnFindRefused` on the report grid and says that refusal in a live
  region in a word of its own (English and Japanese), with layer 2 and layer 3 tests and a sentence on
  the Layout page.
