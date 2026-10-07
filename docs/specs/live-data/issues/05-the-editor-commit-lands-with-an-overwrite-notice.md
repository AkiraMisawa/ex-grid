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

- [x] LV-11, LV-16, LV-17, LV-19, LV-20 and LV-21 pass (§32)
- [x] The Fluxor spike's check 2 (`5` Enter ↑ `7` Enter through a store) lands on both hosts with no
  notice. It is in `verification/2026-10-06-macos-fluxor-spike/harness/`; run it locally once, it is not
  committed as a project
- [x] Layer 1 and 2 green; new and rewritten layer-3 specs run locally before the push

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
- 2026-10-07, later: rebuilt on the user's decision (ADR-0011's note of 2026-10-07, option A). An open
  editor outlives an order move: with a Row Key it and the Focus follow their row, holding its key
  (ADR-0160's third holding), without scrolling — an editor whose row moved out of view stays mounted,
  worn away (`ex-editor-away`), so keys still reach it; without one it stays, and its commit is refused
  as `OrderMoved`. `OrderChanged` is gone from `EditDiscardReason`; `RowLeftTheWindow` stays for a
  keyless row that slid out under the same order. D1 now follows each written cell's painted text, so
  neither a scroll nor a change to another cell, of the same row or another, ends it.
  `GridCommitRefusal.PaintedText` is gone.
- 2026-10-07, after review:
  - D1 is tested with the written row scrolled out of view, and with a fill that reached past the
    painted rows. Its bound is documented where it is declared: positions and strings only.
  - D1 now judges a new Window under the order just taken in.
  - An editor whose row moved out of view is tested to stand at that row again once the row is painted.
  - `/features?upstream=1&rowkey=1` binds the source by trade id. There F8 amends the top row's
    Notional past every other row's, so under a sort by Notional the row moves to the end.
  - A layer-3 test in `write-lands.spec.mjs` drives that page. The editor goes with its row out of
    view without a scroll and keeps the keyboard, the keys typed reach it, and Enter lands on that row.
  - ExSheet's and the demo's wordings of a commit refusal name every reason and throw on any other.

  Results:
  - Layers 1 and 2: 9,161 passed, 9 skipped.
  - Layer 3, locally under the lock in headless Chrome:
    - `write-lands.spec.mjs` with `--repeat-each=3`: WebAssembly 21 passed and 27 skipped as
      Server-only; Server 48 passed.
    - `grid-live` and `grid-live-local`: 9 and 9 passed on each host.
  - The Fluxor spike's check 2 was copied to `spikes/fluxor-grid` with the notice wired in, and deleted
    after. W1 and W2, paused and live, pass on both hosts, and the second commit lands with no refusal
    and no notice.
