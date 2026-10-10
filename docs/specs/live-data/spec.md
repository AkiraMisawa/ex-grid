# Live data in ExGrid and ExPivot

Status: done — but for [ticket 14](issues/14-a-remote-reports-window-is-read-ahead.md)
(`needs-triage`) and LV-15, observed only in part. Merged to `main` as #70 (554b3c2, 2026-10-10),
carrying tickets 01 to 13 as their Statuses say. It was built on `claude/live-data-best`
(2026-10-08), after the first version merged as #64. Tickets 01 to 03 were worked twice, side by
side: by a Claude Code session on `claude/live-data-next-cc` (tickets 04 to 13 below, #67) and by a
Codex session on `claude/live-data-next` (#66). On 2026-10-08 the user compared the two and took the
best of each: **the grid from the Claude Code track, ExPivot from the Codex track**, and the fixes
the comparison's review found in both. #70 superseded #66 and #67, which were not merged.

Decided with the user in the grilling of 2026-10-05, and in the decisions D1 to D10, P1 and P2 of
2026-10-06:

- [ADR-0140](../../adr/0140-a-row-key-names-a-row-across-versions-and-the-grid-repaints-a-changed-row-in-place.md):
  the Row Key, and a changed row repainted in place;
- [ADR-0141](../../adr/0141-exgrids-bundled-sources-take-live-data-by-row-key-on-expivots-rules.md):
  the bundled sources take live data by Row Key, on ExPivot's rules
  ([ADR-0067](../../adr/0067-live-data-a-change-batch-makes-the-next-snapshot-and-expivot-folds-it-in.md));
- [ADR-0142](../../adr/0142-a-write-lands-as-the-user-entered-it-and-a-change-under-the-editor-is-told.md):
  a write lands as the user entered it, and a change under the editor is told (rewritten on
  2026-10-07; it refused such a write until then).

And in the grilling of 2026-10-07, on ticket 01's numbers:

- [ADR-0160](../../adr/0160-the-grid-holds-no-consumer-row-beyond-the-window-it-was-given.md): the grid
  holds no Consumer row beyond the Window it was given;
- [ADR-0161](../../adr/0161-a-live-pivot-redraw-that-runs-out-of-memory-leaves-the-report-stale.md): ExPivot's live
  redraw made the next report from the last — narrowed on 2026-10-08 to its out-of-memory rule;
- ADR-0141's section of 2026-10-07 (who may vouch, and a pushed Window) and ADR-0130's (the Selection
  Summary's walk).

Decided with the user on the Codex track, 2026-10-06, and taken on 2026-10-08 for ExPivot:

- [ADR-0151](../../adr/0151-server-pivots-send-report-windows-and-share-the-local-engine.md): a server
  computes the Pivot Report and sends the requested Window and its changes; local data runs the same
  engine in the browser;
- [ADR-0152](../../adr/0152-report-apis-may-change-and-remote-reports-recover-their-baseline.md): Report
  Versions, Window Changes that name their Baseline Window, automatic recovery, versioned operations;
- [ADR-0153](../../adr/0153-reports-share-unchanged-computation-and-display-rows-own-no-report.md):
  computation follows dependencies, and display rows own no report.

And on 2026-10-08, merging the two (each recorded in the ADR it changes): a replaced Source refuses
what was aimed at the old one, and keys aimed with a dropped Selection open nothing (ADR-0142's and
ADR-0011's notes); the D1 rule settles at the first Window after a write's handler; `RowGone` is
`RowLeftTheWindow`; Window Changes are checked against their Window Digest before they are shown (ADR-0152);
Details are asked by Source Version; the Change Highlight is timed by the component's clock; a
cancelled computation keeps its state (ADR-0153); and a layer-3 wait's bound tells a hang, never a
speed (ADR-0056).

And on 2026-10-09, in the review and grilling of the merged pull request: one commit at a time, keys at a
replaced Source with nothing selected, and what the editor saw read only from the edited row (ADR-0142,
ADR-0011); a report that shrinks under the Window scrolled to is shown at its last rows (ADR-0151); the
report source keeps the reports the Change Highlight compares, decided with the user (ADR-0153, LV-22);
a first report computed from what the cube build and first layout made (ADR-0153); a Stale Report's cells
marked and Copy from it refused (ADR-0067); mark presses aimed at an older display (ADR-0043, MK-9); and
the names `Source`, `ReportSource` and Window Changes (ADR-0152).

The criteria are §32 of `docs/definition-of-done.md` (LV-1 to LV-33; the Codex track's LV-20 to LV-28
are LV-24 to LV-31 here), PV-2 and PV-42 to PV-48 in §29, SM-15 in §31, and two rows of §21.11. What is built, found and not done is in `docs/implementation-status.md`, "ExGrid's
live data (2026-10-06)". This spec synthesises those decisions; where it and they disagree, they win.

## Where it started

- **The research note**: [how ag-grid repaints when its data changes](../../research/ag-grid-rendering-on-data-change.md),
  pinned to ag-grid 36.2.0 (commit `0fee5b7b1e839ae23fe860e404042448f3c1375d`). Its §9 questions were
  decided by the ADRs above; where they differ, the ADRs hold.
- **The measurements**: [`verification/2026-10-05-macos-live-update-measure`](../../../verification/2026-10-05-macos-live-update-measure/README.md)
  (M1 to M5), and the Row Key's cost in ADR-0140's "Settled while building it" (D10).
- **Ticket 01's measurements**, and what they found:
  - [`2026-10-06-macos-live-update-costs-cc`](../../../verification/2026-10-06-macos-live-update-costs-cc/README.md),
    the cost of an update step by step;
  - [`2026-10-06-macos-pivot-oom`](../../../verification/2026-10-06-macos-pivot-oom/README.md), why a
    live ExPivot ran out of memory in the browser;
  - [`2026-10-06-macos-paint-text-cost`](../../../verification/2026-10-06-macos-paint-text-cost/README.md),
    what keeping painted text in place of rows would cost, and what reclaiming a generation costs the
    collector;
  - [`2026-10-06-macos-fluxor-spike`](../../../verification/2026-10-06-macos-fluxor-spike/README.md),
    ExGrid over a Fluxor store.

## The idea in one paragraph

ag-grid repaints quickly under live data because three things outlive an update: each row's id,
computed once; the row's node, and so its rendered row; and a map of ids, checked only when a node
is made. The family follows it where the ADRs allow. A Row Key names a row across versions and keeps
its component (ADR-0140). The bundled sources keep a map of keys, take changes by key, requery
incrementally and vouch that their Windows hold no row twice, so the grid does not walk them
(ADR-0141). What did not outlive an update was decided on 2026-10-07:
- a Window a Consumer pushes itself may now be vouched for (ADR-0141);
- ExPivot makes its next cube and report from the last and shares the rows it did not change — on
  2026-10-07 as the Claude Code track's ADR-0161, and since 2026-10-08 as the Codex track's incremental
  engine (ADR-0153), which replaced it.

What the grid kept beyond an update, it no longer keeps (ADR-0160).

## Reading ag-grid's source

The note and the tickets cite ag-grid by permalinks into one commit. To read around them, take that
commit locally — into the session's scratchpad, never into this repository:

```sh
git clone --depth 1 --branch release-36.2.0 https://github.com/ag-grid/ag-grid.git <scratchpad>/ag-grid
git -C <scratchpad>/ag-grid rev-parse HEAD   # 0fee5b7b1e839ae23fe860e404042448f3c1375d
```

- **The licences.** `ag-grid-community` is MIT. `ag-grid-enterprise` — grouping, aggregation, pivot,
  cell ranges — is under AG Grid's EULA: read it for research, describe its mechanisms in your own
  words with permalinks, and never copy its code here.
- **Every reference the note uses** is defined at its end, by label: `ag-` under
  `packages/ag-grid-community/src`, `age-` under `packages/ag-grid-enterprise/src`, `agr-` under
  `packages/ag-grid-react/src`, `doc-` under `documentation/ag-grid-docs/src/content/docs/`.
- **Where to start, by ticket.**
  - **02 (a pushed Window's vouch):** the map of ids, checked only when a node is made —
    `clientSideRowModel/clientSideNodeManager.ts` (`createRowNode`, L304–L320; the immutable
    `rowData` path, L86–L156).
  - **03 (ExPivot keeps its report):**
    - a row's id, computed once — `entities/rowNode.ts` (`setId`, L468–L493);
    - a group's id and its reuse across updates — enterprise
      `rowGrouping/groupStrategy/groupStrategy.ts` (L523; L492–L494; the delta, L107–L238);
    - the changed path — `utils/changedPath.ts` (L102–L136) and enterprise
      `rowHierarchy/changedPathImpl/changedPathFactory.ts`;
    - aggregation along it — enterprise `aggregation/aggregationStage.ts` (L128–L427);
    - pivot mode, which re-buckets every row and regenerates result columns by key — enterprise
      `pivot/pivotStage.ts` (L60–L196) and `pivot/pivotResultColsService.ts` (L143–L247);
    - row controllers kept by id across a refresh — `rendering/rowRenderer.ts` (L1040–L1061,
      L1311–L1397), `rendering/row/rowCtrl.ts` (`instanceId`, L170), and in React
      `reactUi/rows/rowContainerComp.tsx` (L74–L164).
- **Line numbers are the pinned commit's.** A newer ag-grid may have moved them; read the pinned
  one, or say which release you read.

## Tickets

- [01: Measure what an update costs, end to end](issues/01-measure-what-an-update-costs-end-to-end.md)
  — done.
- [02: A pushed Window vouches for its rows](issues/02-a-pushed-window-vouches-for-its-rows.md) — decided.
- [03: ExPivot keeps its report across live redraws](issues/03-expivot-keeps-its-report-across-live-redraws.md)
  — decided as ADR-0161 on the Claude Code track and as ADR-0151 to ADR-0153 on the Codex track; the
  latter was taken on 2026-10-08.
- **Building what was decided** (`claude/live-data-next-cc`):
  - [04: Bulk writes and Action presses land, and the kept paints go](issues/04-bulk-writes-and-action-presses-land-and-the-kept-paints-go.md)
  - [05: The editor's commit lands, with an Overwrite Notice](issues/05-the-editor-commit-lands-with-an-overwrite-notice.md)
  - [06: The grid holds no row beyond its Window](issues/06-the-grid-holds-no-row-beyond-its-window.md)
  - [07: A pushed Window vouches, and the painted rows are checked](issues/07-a-pushed-window-vouches-and-painted-rows-are-checked.md)
  - [08: The Selection Summary walks only what it needs](issues/08-the-selection-summary-walks-only-what-it-needs.md)
  - [09: A report row holds no value and no report](issues/09-a-report-row-holds-no-value-and-no-report.md)
    — replaced on 2026-10-08 by ADR-0153's detached display rows
  - [10: The report's Change Highlight keeps times, not reports](issues/10-the-reports-change-highlight-keeps-times.md)
    — replaced on 2026-10-08 by ADR-0153's stable lookup, timed by the component's clock
  - [11: The engine makes the next cube and report from the last](issues/11-the-engine-makes-the-next-report-from-the-last.md)
    — replaced on 2026-10-08 by ADR-0153's incremental computation
  - [12: A redraw that runs out of memory leaves the report stale](issues/12-a-redraw-out-of-memory-leaves-the-report-stale.md)
    — kept, ported onto the report-Window design (ADR-0161, PV-47)
  - [13: Measure after the changes](issues/13-measure-after-the-changes.md) — measured the Claude Code
    track's code; the Codex track's own after-record is
    [`2026-10-06-macos-live-report-after`](../../../verification/2026-10-06-macos-live-report-after/README.md).
    The merged code was measured on 2026-10-10, as PV-48 asks:
    [`2026-10-10-linux-merged-live-costs`](../../../verification/2026-10-10-linux-merged-live-costs/README.md).
- [14: A remote report's Window is read ahead](issues/14-a-remote-reports-window-is-read-ahead.md) — needs
  triage. Written up on 2026-10-10. It is to be measured first, then decided with the user.

## Numbers for the next decisions

`claude/live-data-best`, merged as #70, carries ADRs from two blocks (`docs/agents/numbering.md`):
ADR-0151 to ADR-0153 from `claude/live-data-next`'s, ADR-0160 and ADR-0161 from
`claude/live-data-next-cc`'s. ADR-0150 and ADR-0154 were not taken — ExPivot's vouch is ADR-0141's
section of 2026-10-07, and the write policy is ADR-0142 as rewritten — and stay unused. A next
decision for this work takes 0162, the next of the Claude Code track's block, which that branch
continued.
