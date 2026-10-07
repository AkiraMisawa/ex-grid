# 04: Bulk writes and Action presses land, and the kept paints go

Status: done

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

- [x] LV-12, LV-13, LV-14 and LV-20's Action half pass (§32), each test named with its ADR
- [x] No `Paint` keeps a row instance; `PaintsKept` and the painted-text judgement are gone
- [x] Layer 1 and 2 green; the layer-3 specs that cover writes under live data (`write-refusal`,
  `grid-live`, `grid-live-local`) rewritten and run locally before the push (CLAUDE.md)

## Comments

- 2026-10-07: built on `claude/live-data-next-cc-grid-writes`. Paste, the fills, Delete and an Action
  press land as entered; no write compares painted text, and `_paints`, `_ownWrites` and D2 are gone.
  Of its last paints the grid keeps numbers only: the order each was painted under, and a serial
  per painted row component, so a told press finds the component that painted its button. A write
  aimed under an order that has moved since is refused as `EmptySelection`, as aimed with a
  Selection that went with that order; an Action press says `RowGone` or `OrderMoved`. Layer 2:
  `WritesLandAsEnteredTests` (renamed from `WriteRefusalTests`). Layer 3: `write-lands.spec.mjs`
  (renamed from `write-refusal.spec.mjs`) is rewritten; it runs locally with ticket 05, before the
  push. The editor's commit still refuses a change under it until ticket 05.
- 2026-10-07, later: the readings that only judged a write are gone (ADR-0021's note as amended):
  `BarPressTakenAt`, and the render the key field and the press listener passed along. With a Row Key a
  press acts on the row under its key; one that cannot be paired is refused as `OrderMoved`; the
  refusal's row is nullable. After review, a gesture aimed with a Selection that an order move has
  dropped since is refused by name. It is never taken as a first key, and never dropped without a word:
  - Delete, Ctrl+D and Ctrl+R are refused as a paste is.
  - A fill-handle release is refused too; before, it raised nothing.
  - Space on an action is refused as `OrderMoved`, naming no row, with or without a Row Key: the grid
    keeps no key of the dropped Selection (ADR-0160). It does not engage the Focus now in force.

  The tests no longer select again before the gesture. Layer 3 ran locally under the lock on the final
  code (see ticket 05's comment).
- 2026-10-07, on the orchestrator's decision (ADR-0142's Consequences, LV-13): a positional write
  aimed under an order that has moved since is refused as the new `PasteRefusalReason.OrderMoved`.
  That covers a paste, Delete, Ctrl+D or Ctrl+R told an earlier render, a Ctrl+Enter fill whose editor
  opened before the move, and a fill-handle release after it. `EmptySelection` is left for a gesture
  that had no Selection, an order move or not. The demo's and ExSheet's wordings name every reason and
  throw on any other; ExSheet no longer says "Select a cell to paste into." for an order move.
  - Layers 1 and 2: 9,163 passed, 9 skipped.
  - Layer 3, `write-lands.spec.mjs` with `--repeat-each=3`: WebAssembly 21 passed and 27 skipped as
    Server-only; Server 48 passed.
