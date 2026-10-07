# 04: Bulk writes and Action presses land, and the kept paints go

Status: ready-for-agent

**What to do:** build the half of [ADR-0142](../../../adr/0142-a-write-lands-as-the-user-entered-it-and-a-change-under-the-editor-is-told.md) (rewritten 2026-10-07) that is not the Cell Editor.
A paste, a Ctrl+Enter fill, a fill-handle drag, Delete, Ctrl+D, Ctrl+R and an Action press land as
entered over cells whose painted text changed. The grid stops keeping what earlier renders painted.

**Blocked by:** None. Ticket 05 builds the editor's half on top of this one.

## Where it stands

- `src/ExGrid/Components/ExGrid.SeenText.cs` keeps `_paints` (`PaintsKept = 64`, `class Paint` with
  `TRow?[] Rows`), judges targets against them (`JudgeTargets`, `JudgeActionRow`), keeps the user's own
  writes (`_ownWrites`), and answers told Action presses (`ActionPressTakenAt`, `_actionPressPending`).
- The refusal reasons `PasteRefusalReason.TargetChanged`, `PasteRefusalReason.RenderNoLongerKept`,
  `ActionRefusalReason.RowChanged` and `ActionRefusalReason.RenderNoLongerKept` exist for this rule.
- `tests/ExGrid.Components/WriteRefusalTests.cs` pins the old rule. Its tests are rewritten to the new one,
  not deleted: each case that refused now lands, or is refused for the order or the row's presence.

## What to build

- **No write compares painted text.** Paste, fill, Delete, Ctrl+D, Ctrl+R and the fill handle check only
  the existing refusals and the Row Sequence Version (ADR-0011/0014/0035).
- **An Action press acts on the row it was pressed on.**
  - With a Row Key, it acts on the row under that key.
  - Without one, a told press whose component was disposed acts on the row at the told position while
    the Row Sequence Version is the one it was taken under.
  - Otherwise it is refused as `RowGone` or `OrderMoved`. No press is lost to Blazor.
- **The kept paints go.** Keep the layouts of recent renders, as positions and geometry, so a press lands
  on the cell it was made on (ED-31), and the row order a gesture was taken under.
- **The reasons change** as ADR-0142's Consequences list. Update their XML docs and every Consumer that
  words them: `samples/ExGrid.DemoPages/Pages/Features.razor`, `src/ExSheet/Components/ExSheet.razor`, and
  the reference Chrome's wording.
- **D5 stays:** the bound source puts out what it gathered before a write is handled.

## Done when

- [ ] LV-12, LV-13, LV-14 and LV-20's Action half pass (§32), each test named with its ADR
- [ ] No `Paint` keeps a row instance; `PaintsKept` and the painted-text judgement are gone
- [ ] Layer 1 and 2 green; the layer-3 specs that cover writes under live data (`write-refusal`,
  `grid-live`, `grid-live-local`) rewritten and run locally before the push (CLAUDE.md)

## Comments
