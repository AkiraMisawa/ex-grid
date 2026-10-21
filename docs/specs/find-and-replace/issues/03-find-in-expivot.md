# 03: Find in ExPivot

Status: done
Roadmap: ExPivot — Ctrl+F finds a label or a value in the report as shown.

**Why:** [ADR-0332](../../../adr/0332-find-reaches-every-product-replace-is-a-write-asked-of-the-consumer-and-the-matches-in-view-are-marked.md) §1.

- [x] ExPivot answers Find over the laid-out cells, by displayed text; collapsed groups not searched, nothing expanded
- [x] Passes the reference match function, so matches are marked
- [x] Ctrl+H refused and said
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
- 2026-10-21: With 02 merged, Ctrl+H is refused and said: the report grid's `OnFindRefused` hears
  `ReplaceUnavailable`, and the Pivot Toolbar says "Cannot change this part of a PivotTable report."
  (`replace-unavailable`, in English and Japanese). A step that cannot read the report — the source
  refuses, answers another version or range, no longer holds the report on screen, or the report keeps
  changing across three searches — is said on the Pivot Toolbar and given up, moving nothing; a live
  update while a step reads is searched again over the report now shown. For that, ExGrid now takes an
  `OperationCanceledException` thrown by `OnFind` as a step its Consumer gave up, not as a crash.
- 2026-10-21: **Column labels: decided by ADR-0333 and built in ticket 05.** ADR-0332 §1 names the
  column labels among what Find searches; in ExPivot the column Items and the value captions stand in
  the report grid's headers, which ExGrid's Find now searches itself before the rows, so they are found,
  marked and said ("Found in a header") with the Focus left where it was. The Layout page, the README
  and the Change Note say so.
