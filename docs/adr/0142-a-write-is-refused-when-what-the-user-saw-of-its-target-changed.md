# A write is refused when what the user saw of its target changed before it lands

*(Decided with the user, 2026-10-05, in the grilling of ExGrid's live data — Q5 with its parts a and
b, R8 and R9. Two things the user said set the rule: "an action can change values too", and telling
the user that a write was refused because the data upstream changed "is required".)*

Under live data, a value can change between the moment a user sees it and the moment a write over it
lands. Today such a write lands anyway, and the change is lost without anyone seeing it.

- **The Cell Editor commits onto the row that is in the Window when the commit lands**
  (`ExGrid.razor:7235`), not onto the row it was opened on.
  - The editor covers the cell, so the user cannot see a change made upstream while they type.
  - For example: a notional shows 1,000,000. Upstream it becomes 2,000,000 while the editor is
    open. The user, who saw 1,000,000, types 1,500,000 and commits. The 2,000,000 is overwritten,
    and no one ever saw it.
- **A paste or a fill is refused only when the order moved**, that is, when the Row Sequence Version
  changed ([ADR-0011](./0011-selection-is-rectangles-in-index-space-and-is-dropped-on-reorder.md),
  [ADR-0014](./0014-paste-shape-rules-and-selection-count.md)). A change of values under its target
  is written over.
- **An Action is resolved when its press is handled.** On a circuit, a newer render may have painted
  the row by then, and the action then acts on values the user did not see.

**The rule: a write is refused when what the user saw of its target changed before the write lands,
and the user is told why.**

- What the user saw is the painted text of the target's painted cells. This is the same comparison
  the Change Highlight makes
  ([ADR-0067](./0067-live-data-a-change-batch-makes-the-next-snapshot-and-expivot-folds-it-in.md);
  Q6).
- A cell that was not on screen was not seen, and is not compared.

## The Cell Editor

- **The grid keeps the edited cell's painted text when the editor opens.** At commit, if the cell
  now paints other text, the commit is refused.
- **The editor stays open, with what the user typed.**
  - The grid raises the reason with the cell's new painted text. Chrome words it into its refusal
    live region (A11Y-16), and the grid holds no string for it.
  - The editor covers the cell, so the new value is shown in the notice.
- **A second commit is judged against the value the notice showed.** Escape leaves without writing.
- **A change to another cell of the row does not refuse the commit**, so a row whose P&L moves every
  second can still have its notional edited.
  - A Consumer whose write depends on another cell judges the row at commit through its Edit
    Verdict ([ADR-0034](./0034-validation-is-a-consumer-verdict-enforced-only-at-the-editor.md)),
    which sees the row as it is then.

## An Action, a paste and a fill

- **An Action press** is judged on the row's painted cells. Their painted text in the render the
  press was taken against is compared with the row's when the press is handled.
  - This applies to every action, and needs no declaration.
  - An action that writes nothing, refused in a race, costs the user a second press, and the reason
    is given.
- **A paste, a Ctrl+Enter fill and a fill-handle drag** are judged on the target's painted cells, the
  same way. This check is made beside the existing Row Sequence Version check.
- **A Template cell's own controls are the Consumer's**
  ([ADR-0037](./0037-entering-a-cell-never-reaches-into-content-the-core-did-not-render.md)).
  - Their handlers receive the row the template was painted with.
  - Checking that row is the Consumer's job, because the grid cannot see what a Consumer's control
    does.

## The render a gesture was taken against

- **A press on the rows already carries it.** The browser reads the row order and the layout that
  the painting render wrote on the Viewport, and tells the core before Blazor dispatches the press
  (`ex-grid.js:1762`; [ADR-0021](./0021-javascript-is-allowlisted-not-minimised.md)'s note of
  2026-10-02; ED-31).
- **A keyboard gesture carries the same.** The capture-phase key listener and the clipboard read,
  which are ADR-0021's first and third entries, read it.
  - Reading what a render wrote is not a measurement.
  - Nothing per cell reaches JavaScript.
- **The grid keeps what it painted for its last few renders**, as it already keeps their layouts.
  - A gesture taken against a render older than those kept is refused, because the grid can no
    longer tell what the user saw.
- **How long the window is depends on the host.** On WebAssembly a press is handled within the
  render it was taken against, so the window is near zero. On a circuit it is a round trip.

## Settled while building it

*(2026-10-06, decided with the user — D1 to D5, P1 and P2 — when the first build met the bundled sources.)*

- **The user's own writes count as seen** (D1). A cell that one of the user's own earlier gestures
  wrote, after the render a later gesture was taken against, is not compared for that gesture.
  - The user knows what they wrote. Without this, `5` Enter ↑ Ctrl+V typed at machine speed on a
    circuit would refuse the paste, because the render the paste was taken against did not yet show
    the 5; on WebAssembly the same keys would paste.
  - Rejected: judging a held key against the render when it is handed on (D1, option c). That
    would miss a change made upstream while the key was held.
- **The editor keeps the painted text of the render the gesture that opened it was taken against**
  (D2), with the rule above. Keeping the text when the editor opens left a round trip between the
  key and the open in which an upstream change went unseen.
- **Delete, Ctrl+D and Ctrl+R are writes**, and follow the rule as a paste does (D3).
- **A fill also judges its source** (D4). A fill-handle drag, Ctrl+D and Ctrl+R write the values of
  their source cells. If a painted source cell's text changed, the fill would write values the user
  did not see, so it is refused.
- **A bound source puts out what it has gathered before a write is judged** (D5).
  - The grid asks the source first, so the write is judged against the newest version, and the
    Edit Intent carries that version.
  - Without it, a commit made while `GridSource.From` was gathering passed the grid's check, and
    the source's `ReplaceRow` then refused it with an exception the user never heard of. A
    gathered change to the edited cell itself would have been written over unseen.
  - A user's gesture brings the change forward, as in ExPivot
    ([ADR-0067](./0067-live-data-a-change-batch-makes-the-next-snapshot-and-expivot-folds-it-in.md)).
- **A cell derived from the user's own write is still compared** (P1, accepted). D1 lets off only
  the cells the user's own gestures wrote. A cell computed from them — an ExSheet formula, a spilled
  array, a Consumer's computed column — still changes, and is still compared. So on a circuit,
  writing A1 and at once pasting over B1 = A1 × 2 is refused, where on WebAssembly it lands: a host
  difference that remains, at machine speed only. The refusal is the safe side and says why, and a
  second gesture lands.
  - Rejected for now: judging such a gesture against the first render after the user's write
    (which would miss an upstream change in the same round trip, close to what D1 rejected), and a
    Consumer declaring its derived cells. The second is the one to take if ExSheet finds it bites.
- **A refused commit says whether the cell changed or the grid can no longer tell** (P2). When the
  render the opening gesture was taken against is no longer kept, the refusal's reason is
  `RenderNoLongerKept`, never a change: a wrong reason is worse than none.
- **No press is lost to Blazor** (found by the first layer-3 run, 2026-10-06). Blazor does not
  deliver an event whose attribute a component since disposed had rendered. Without a Row Key, a
  row whose instance a render replaced while a press was on its way has its component disposed, so
  the click on its action never arrived: the press was neither fired nor refused, and nothing said
  so. The listener now tells the core which row, column and action the press was on, with the
  render it was taken against. A press whose row component is no longer rendered is answered by
  the core — refused or fired by the rule above — at once, or after the render that disposes it.
  Which, is decided by the order the core hears things in.
- **With a Row Key, an Action press is paired with its row by key**
  ([ADR-0140](./0140-a-row-key-names-a-row-across-versions-and-the-grid-repaints-a-changed-row-in-place.md)).
  Without one, a press whose row moved cannot be paired, and is refused as taken against a render no
  longer kept.

## Considered options

- **Refuse when the row's instance changed** (Q5a, option A). Rejected: a row with one
  fast-moving cell could never be edited.
- **Hand the Consumer both rows and let it decide** (Q5a, option C; R8, option ii). Rejected: a
  Consumer that forgets to check writes over the change without seeing it.
- **Refuse only the actions a Consumer declares as writing** (R8, option iii). Rejected for the same
  reason: a missed declaration is a lost update.
- **Write onto the newest version** (Q5, option c). Rejected: it is quietly wrong.
- **Discard the editor's text** (Q5b, option B). Rejected: the user would lose what they typed, for
  something that was not their doing.

## Consequences

- **This is not a Change Highlight comparison.**
  [ADR-0068](./0068-change-highlight-is-asked-of-the-consumer-and-painted-without-animation.md) says
  the grid never compares values to mark a cell, and that still holds. Here the grid compares what it
  painted, so that a write does not land on something the user did not see.
- **New refusal reasons are added**: one for a commit, and one for each gesture. They are raised
  through the grid's existing channels and worded by Chrome. The grid holds no strings.
- **On the Server host, a press on a row that changes faster than a round trip** may be refused more
  than once. Each refusal says why.
- **The Definition of Done gains LV-11 to LV-14, LV-16 and LV-17** (§32).
