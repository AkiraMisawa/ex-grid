# A live pivot redraw that runs out of memory leaves the report stale

*(Decided with the user on 2026-10-07, in the grilling of live data continued on the Claude Code track,
as "ExPivot's live redraw makes the next report from the last". **Narrowed on 2026-10-08:** comparing
the two tracks of live data continued, the user took the Codex track's ExPivot —
[ADR-0151](./0151-server-pivots-send-report-windows-and-share-the-local-engine.md) to
[ADR-0153](./0153-reports-share-unchanged-computation-and-display-rows-own-no-report.md) — for everything
this ADR had decided about how a redraw is computed. What remains here is the rule for a redraw that runs
out of memory. What was decided first, and what replaced it, is kept below.)*

**The rule: an `OutOfMemoryException` while the report of the newest data is computed is caught where the
redraw is asked for.**

- **A live or data redraw** — new data under the layout on screen:
  - what was being computed is dropped;
  - the report on screen stays, marked a Stale Report with the reason and Retry
    ([ADR-0067](./0067-live-data-a-change-batch-makes-the-next-snapshot-and-expivot-folds-it-in.md));
  - the next change asks again.
- **A user's layout change** is refused: the layout goes back to the one on screen, and the Pivot Toolbar
  says that memory ran out while that layout was laid out. Before a first report, it is a refusal.
  *(Settled while merging, 2026-10-08: the report on screen is the old layout's, so marking it stale for a
  layout it does not show would be untrue.)*
- **The reason says that memory ran out, not that the data is wrong.** A WebAssembly heap does not give
  memory back, so a redraw that failed may fail again.
- **Nothing reaches the renderer as an unhandled exception.** Before this, the exception did, and the page
  stopped ([`2026-10-06-macos-pivot-oom`](../../verification/2026-10-06-macos-pivot-oom/README.md)). That
  contradicts principle 1: say it cannot be done.

As built on `claude/live-data-best`: `PivotReportClient` and `ItemLabels` let the exception through, and
`ExPivot.Asking.cs` turns it into the Stale Report or the refusal, with `StaleReportWords.OutOfMemory`
worded in English and Japanese. Layer 2 injects the throw through an Order Key and through a slicer's
yield, under both Chromes (PV-47).

## What was decided on 2026-10-07, and what replaced it on 2026-10-08

On 2026-10-07 this ADR had a live redraw make the next cube and report from the last: the axis nodes
kept, the cells on a changed path computed again, a report row holding no value and no report, a Change
Highlight keeping times rather than reports, and an answer that could name the leaves that changed. It
started afresh on any change of structure, a sort by value, or a Show Values As reading other rows. Ticket
13 measured it ([`2026-10-07-macos-live-update-costs-after`](../../verification/2026-10-07-macos-live-update-costs-after/README.md)):
268.5 to 19.0 ms on CoreCLR at 401,001 report rows, and a flat heap in the browser.

The Codex track answered the same ticket with ADR-0151 to ADR-0153. Comparing the two on 2026-10-08, the
user took that track's ExPivot and this track's grid
([ADR-0142](./0142-a-write-lands-as-the-user-entered-it-and-a-change-under-the-editor-is-told.md),
[ADR-0160](./0160-the-grid-holds-no-consumer-row-beyond-the-window-it-was-given.md)):

| Here, 2026-10-07 | Now |
|---|---|
| The engine makes the next cube and report from the last; afresh on structure, value sorts and Show Values As that reads other rows | ADR-0153: computation follows dependencies, structural changes and value sorts included |
| A report row holds no value and no report | ADR-0153: detached display rows and keys own no Report |
| The Change Highlight keeps times, not reports | ADR-0153's stable lookup. The source keeps the reports the highlight compares, bounded by its duration over the time between redraws ("What the history holds"). The time is the component's own, taken when it adopts a report that lists the change (ADR-0068's note of 2026-10-08) |
| An answer may name the leaves that changed | Not taken. The engine took a source's list as given, and a source naming too few leaves painted totals that did not add up, with nothing to say so (found in review, 2026-10-07). Under ADR-0151 only the bundled Snapshot source computes incrementally, from its own fold, and a server sends report Windows whose deltas are checked against a digest (ADR-0152) |
| A redraw that runs out of memory leaves the report stale | Kept: this ADR, widened to a layout change |

Ticket 01's and ticket 13's measurements stay as the record of the code they measured.

## Consequences

- **PV-47** judges this ADR (§29). PV-44 to PV-46, the criteria of the first decision, were replaced by
  LV-29, LV-30 and a restated PV-45.
- **ADR-0067's section of 2026-10-07** points here.
