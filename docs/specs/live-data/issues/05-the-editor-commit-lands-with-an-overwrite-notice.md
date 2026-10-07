# 05: The editor's commit lands, with an Overwrite Notice

Status: done

**What to do:** build the Cell Editor's half of [ADR-0142](../../../adr/0142-a-write-lands-as-the-user-entered-it-and-a-change-under-the-editor-is-told.md). A commit lands with the text typed.
A change made under the open editor is told by an Overwrite Notice. The Consumer may refuse the
intent. A commit whose row is gone, or whose order moved, is refused with the editor left open.

**Blocked by:** 04

## What to build

- **The editor keeps the cell's painted text when it opens** (one string). This replaces D2's "text of
  the render the opening gesture was taken against".
- **At commit, the grid compares that text with the cell's painted text now.**
  - When they differ, the commit lands, and the grid raises an Overwrite Notice: the cell, the text seen
    and the text replaced.
  - The user's own writes not yet painted when the editor opened are not reported (D1, for the notice
    only).
- **`GridEditIntent<TRow>`** (`src/ExGrid/Cells/GridEditIntent.cs`, today a positional record of row,
  column and value) gains:
  - the text seen and the text replaced;
  - `Refuse(message)`, honoured when called before the `OnEdit` handler completes. The editor stays open
    with the typing, and the message is shown at the editor and in the live region as a Reject's is
    (ADR-0034).
- **The commit goes to the row the editor was opened on**: by Row Key, or by position under the Row
  Sequence Version it opened under. Otherwise it is refused as `RowGone` or `OrderMoved`, the editor stays
  open, and Escape writes nothing.
- **`CommitRefusalReason`** loses `CellChanged` and `RenderNoLongerKept`, and gains `RowGone` and
  `OrderMoved`. ExSheet's `_commitRefused` and the DemoPages' handler follow.
- **The notice reaches Chrome** through a grid event, and Chrome words it into the root's live region
  (A11Y-16). The grid holds no string for it.

## Done when

- [ ] LV-11, LV-16, LV-17, LV-19, LV-20 and LV-21 pass (§32)
- [ ] The Fluxor spike's check 2 (`5` Enter ↑ `7` Enter through a store) lands on both hosts with no
  notice. It is in `verification/2026-10-06-macos-fluxor-spike/harness/`; run it locally once, it is not
  committed as a project
- [ ] Layer 1 and 2 green; new and rewritten layer-3 specs run locally before the push

## Comments

- 2026-10-07: built on `claude/live-data-next-cc-grid-writes`. A commit lands with the text typed;
  over a cell that changed under the editor it raises `OnOverwriteNotice` (`GridOverwriteNotice`),
  and the Edit Intent carries `SeenText` and `ReplacedText`. `GridEditIntent.Refuse(message)` holds
  the editor as a Reject does: the intent is now raised while the editor still stands, and the edit
  ends once the handler has accepted. D1 for the notice keeps the user's own writes as positions
  and the paint they were raised after. `CommitRefusalReason` is `RowGone` or `OrderMoved`.
  - Read conservatively, pending the user (reported to the lead): ADR-0011's discard stays for an
    order move outside a commit; `OrderMoved` and a row followed by key come from the commit's own
    D5 ask, after which the editor is held. Without a Row Key, a row that left the Window under the
    same order is still discarded as `RowLeftTheWindow` (ED-21): `RowGone` names a key.
  - The DemoHost and ExSheet word the notice into a live region. Layer 3 and the Fluxor spike's
    check 2 run before the push; their results are added here.
