# A write lands as the user entered it, and a change under the editor is told

*(Decided with the user on 2026-10-05, in the grilling of ExGrid's live data — Q5 with its parts a and
b, R8 and R9 — as "a write is refused when what the user saw of its target changed". **Rewritten with the
user on 2026-10-07** (the grilling of live data continued, Q-A to Q-C, Q-A', R1 to R3). The rule turned
over. What was decided first, what changed and why are kept below, under "What changed on 2026-10-07".)*

Under live data, a value can change between the moment a user sees it and the moment a write over it
lands. On 2026-10-05 the answer was to refuse such a write. On 2026-10-07 the user turned it round:

> "The user overwrites on purpose. Even if the value was updated to something else meanwhile, the
> overwrite with what they entered should succeed."

**The rule: a write lands as the user entered it, on the row the user aimed it at. The one change the
user could not have seen — a change to the cell under an open editor — is told when the commit lands.**

## What lands

- **Every write lands with what the user entered**: a Cell Editor commit, a paste, a Ctrl+Enter fill, a
  fill-handle drag, Delete, Ctrl+D, Ctrl+R and an Action press.
- **No write is refused because a value changed.** The grid compares no painted text to judge a write.
- **The refusals that are not about a changed value all stand:** a paste's shape (ADR-0014), the
  editable declaration ([ADR-0035](./0035-paste-and-fill-respect-the-editable-declaration.md)), an Edit
  Verdict's Reject ([ADR-0034](./0034-validation-is-a-consumer-verdict-enforced-only-at-the-editor.md)),
  and the Consumer's own refusal of an intent (below).

## On the row the user aimed it at

An overwrite is meant for a row, not for whatever row a position names when the write lands. That is
[ADR-0011](./0011-selection-is-rectangles-in-index-space-and-is-dropped-on-reorder.md)'s line, and it
holds.

- **A commit goes to the row the editor was opened on.**
  - With a Row Key ([ADR-0140](./0140-a-row-key-names-a-row-across-versions-and-the-grid-repaints-a-changed-row-in-place.md)),
    it goes to the row under that key, wherever the row is now.
  - Without one, it goes by position, under the Row Sequence Version the editor was opened under.
- **An Action press acts on the row it was pressed on.**
  - With a Row Key, the row's component lives as long as its key is in the Window, and hears the click
    itself.
  - Without one, a press whose row component a render disposed is answered by the core from what the
    browser told it (below). It acts on the row at the told position while the Row Sequence Version is
    the one the press was taken under.
- **The editor outlives an order move** ([ADR-0011](./0011-selection-is-rectangles-in-index-space-and-is-dropped-on-reorder.md)'s
  note of 2026-10-07, decided with the user). With a Row Key, the editor and the Focus follow the row
  they were opened on; without one, the editor stays where it is and its commit is refused as
  `OrderMoved`. The grid does not scroll to follow a row that moved out of view.
- **A paste and a fill stay positional**, as ADR-0014 has them, and are refused when the order moved
  (ADR-0011): the Selection they were aimed with no longer names those rows.
- **When the row is gone, or the order moved, the write is refused, and says why.**
  - A commit is refused with the editor left open and the text typed kept; Escape leaves without
    writing. The reason is `RowGone` (the key is no longer in the Window) or `OrderMoved` (no Row Key,
    and the Row Sequence Version moved).
  - An Action press is refused for the same two reasons.
  - Throwing the typed text away was rejected on 2026-10-05 (Q5b, option B) and stays rejected: the user
    would lose what they typed for something that was not their doing.
- **ag-grid does the same for the editor and for actions.** Its editor belongs to the row node: the row
  stays rendered while it is edited, and the commit goes to that node with the typed value
  ([`rowRenderer.ts#L1553-L1580`](https://github.com/ag-grid/ag-grid/blob/0fee5b7b1e839ae23fe860e404042448f3c1375d/packages/ag-grid-community/src/rendering/rowRenderer.ts#L1553-L1580)).
  Its cell ranges are positions that a sort or new data does not move, so its paste then writes other
  rows. ExGrid keeps ADR-0011's refusal there on purpose.

## The Cell Editor tells a change it covered

The editor covers the cell, so a change made upstream while the user types is the one change they cannot
see. Everything else a write lands on was on screen, and a live change there wears the Change Highlight
([ADR-0068](./0068-change-highlight-is-asked-of-the-consumer-and-painted-without-animation.md)).

- **The editor keeps the cell's painted text when it opens.** It is one string, held while the editor is
  open.
- **At commit, if the cell now paints other text, the commit lands, and the grid raises an Overwrite
  Notice.** It names the cell, the text the user saw when the editor opened, and the text the commit
  replaced.
  - The Edit Intent carries the same two texts. A Consumer that must not write over a change can refuse
    the intent (below), or offer to put the replaced value back.
  - Chrome words the notice into the root's live region, as it words a refusal (A11Y-16). The grid holds
    no string for it. What else is shown, such as a toast, is Chrome's and the Consumer's; nothing is
    added to the row (ADR-0013).
- **The user's own writes count as seen.** A cell that one of the user's own earlier writes covered, not
  yet painted when the editor opened, is not reported as changed. Without this, `5` Enter ↑ `7` Enter typed
  at machine speed on a circuit would report the user's own 5 as a change; on WebAssembly the same keys
  would not. This is D1 of 2026-10-06, kept for the notice alone.
- **A bound source puts out what it has gathered before a commit lands** (D5 of 2026-10-06, kept). The
  commit is then made on, and the notice compared with, the newest version.
- **On a circuit, a change made between the key that opens the editor and the editor opening is not
  told.** That window is a round trip; the user accepted it with the rest of this rule.

## The Consumer may refuse an edit

- **`GridEditIntent.Refuse(message)`**, called before the `OnEdit` handler completes, refuses the commit.
  - The editor stays open with the text typed, as under an Edit Verdict's Reject, and the message is
    shown the same way: the popover at the editor and the root's live region (ADR-0034).
  - It is the edit's counterpart of `GridPasteIntent.Refuse()` and `GridFillIntent.Refuse()`
    ([ADR-0050](./0050-what-exsheet-asks-of-exgrids-core.md), item 3), with a message, because the
    editor is where the user is looking.
- **An Edit Verdict judges before the intent is raised; `Refuse` answers after.** A Consumer whose store
  is the point where writes are ordered — a reducer, a server — refuses there, against its own newest
  version, and the typing is not lost. The Fluxor spike found it could only lose it
  ([`2026-10-06-macos-fluxor-spike`](../../verification/2026-10-06-macos-fluxor-spike/README.md),
  proposal B).
- **A refusal after the handler has completed is not offered.** By then the editor is closed.

## What a gesture carries, and what the grid keeps

- **A gesture carries the render it was taken against**, as before. A press on the rows carries the row
  order and the layout the painting render wrote on the Viewport (`ex-grid.js`;
  [ADR-0021](./0021-javascript-is-allowlisted-not-minimised.md)'s note of 2026-10-02; ED-31), and a
  keyboard gesture carries the same through the capture-phase key listener and the clipboard read.
  Reading what a render wrote is not a measurement, and nothing per cell reaches JavaScript.
- **The grid uses it for where, never for what.** The layout lets a press land on the cell it was made on
  however late the core hears it (ED-31), and the row order is the Row Sequence Version a positional
  write is checked against.
- **The grid keeps nothing of what earlier renders painted.** No painted text, and no row instance
  ([ADR-0160](./0160-the-grid-holds-no-consumer-row-beyond-the-window-it-was-given.md)). The layouts of a
  few recent renders are kept, as positions and geometry.
- **No press is lost to Blazor** (found by the first layer-3 run, 2026-10-06). Blazor does not deliver an
  event whose attribute a component since disposed had rendered. The listener tells the core which row,
  column and action a press was on, with the render it was taken against, and the core answers a press
  whose row component is gone: it acts, or refuses for one of the two reasons above.

## What changed on 2026-10-07

**The rule decided on 2026-10-05:** a write is refused when what the user saw of its target changed
before it lands, and the user is told why. "What the user saw" was the painted text of the target's
painted cells. The grid kept its last 64 paints with their row instances, so that a gesture taken against
an earlier render could be judged against it. Two things the user said then set it: "an action can
change values too", and telling the user that a write was refused because the data upstream changed "is
required". Write onto the newest version was considered and rejected then: "it is quietly wrong".

**What turned it over:**

- **The user's view of a write.** An overwrite is the user's intent. A refusal for a change they did not
  see makes them write again what they already decided.
- **What the kept paints cost.** A kept paint held its row instances, and an ExPivot report row holds its
  whole report and cube. So every live redraw stayed reachable for 64 paints, and a live ExPivot ran out of
  the 2 GB WebAssembly heap on the 7th redraw of a 401,001-row report
  ([`2026-10-06-macos-pivot-oom`](../../verification/2026-10-06-macos-pivot-oom/README.md)).
  - Keeping painted text in place of rows (proposal A) was measured. It costs about 0.5 ms a full repaint
    in WebAssembly, and it stops the growth
    ([`2026-10-06-macos-paint-text-cost`](../../verification/2026-10-06-macos-paint-text-cost/README.md)).
  - It could not keep three Action behaviours without keeping row keys or instances in older paints too.
- **A refusal that depended on timing.** A user's own edit coming back through a store was refused at
  machine speed and not by hand, the user's own value given as the upstream change
  ([`2026-10-06-macos-fluxor-spike`](../../verification/2026-10-06-macos-fluxor-spike/README.md), check
  2). That is the kind of outcome principle 6 forbids.

**What became of the first decision's parts:**

| Then | Now |
|---|---|
| A commit over a changed cell is refused; the editor stays open | It lands; an Overwrite Notice tells it |
| An Action, a paste, a fill, Delete, Ctrl+D and Ctrl+R are judged on the target's painted cells, and a fill on its source's (D3, D4) | They land. Only the order (ADR-0011/0014) and the row's presence are checked |
| The grid keeps what it painted for its last renders | It keeps nothing of what they painted (ADR-0160) |
| D1, the user's own writes count as seen | Kept, for the Overwrite Notice |
| D2, the editor keeps the text of the render its opening gesture was taken against | The editor keeps the text when it opens; the round trip before is accepted |
| D5, a bound source puts out what it gathered before a write is judged | Kept |
| P1, a cell derived from the user's own write is still compared | Gone with the comparison |
| P2, `RenderNoLongerKept` is never worded as a change | Gone: no render is kept to be lost. A refusal now says `RowGone` or `OrderMoved` |
| No press is lost to Blazor | Kept |

**The quietness that remains, accepted by the user:** on a circuit, a value that changes in the round trip
before a paste, a fill or an Action press lands is written over without a notice. The cell was on screen,
and a live change there is marked. On WebAssembly a gesture is handled within the render it was taken
against, so the window is near zero.

## Considered options

*(2026-10-07)*

- **Keep refusing, and keep painted text in the paints in place of rows** (proposal A). Rejected by the
  user in favour of the overwrite. It was measured first: cheap, and it fixed the memory.
- **Let a write land, and keep one render's painted text to tell a bulk write which cells it overwrote.**
  Rejected. On WebAssembly the window is near zero, on a circuit one round trip, and the cells were on
  screen; it is state kept for very little.
- **Let a write land silently everywhere, the editor included**, as ag-grid does. Rejected: under the
  editor the change is one the user cannot see, and a money figure would be replaced with no one ever
  seeing it (principle 1).
- **Hand the Consumer what the user saw and let it decide** (2026-10-05's Q5a, option C). Taken in part.
  The default now is to land, so a Consumer that does nothing gets what the user wanted, and the edit's
  intent carries the texts for one that must refuse.

## Consequences

- **The refusal reasons change.**
  - `CommitRefusalReason` loses `CellChanged` and `RenderNoLongerKept`, and gains `RowGone` and
    `OrderMoved`.
  - `ActionRefusalReason` loses `RowChanged` and `RenderNoLongerKept`, and gains the same two.
  - `PasteRefusalReason` loses `TargetChanged` and `RenderNoLongerKept`.
- **The Edit Intent gains the text the user saw and the text the commit replaced, and `Refuse(message)`.**
  ADR-0034 and [ADR-0007](./0007-edits-are-an-overlay-owned-by-the-consumer.md) are noted.
- **`CONTEXT.md` gains Overwrite Notice.**
- **The Definition of Done's LV-11 to LV-14, LV-16 and LV-17 are rewritten** (§32), and LV-19 to LV-21
  are added.
