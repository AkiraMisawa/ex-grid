# Live data in ExGrid and ExPivot

Status: in progress — the first version is built and merged (#64). Tickets 01 to 03 were measured and
decided on `claude/live-data-next-cc` (2026-10-07), and tickets 04 to 13 build what was decided

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
- [ADR-0161](../../adr/0161-expivots-live-redraw-makes-the-next-report-from-the-last.md): ExPivot's live
  redraw makes the next report from the last;
- ADR-0141's section of 2026-10-07 (who may vouch, and a pushed Window) and ADR-0130's (the Selection
  Summary's walk).

The criteria are §32 of `docs/definition-of-done.md` (LV-1 to LV-23), PV-42 to PV-48 in §29, SM-14
in §31, and two rows of §21.11. What is built, found and not done is in `docs/implementation-status.md`, "ExGrid's
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
- ExPivot makes its next cube and report from the last and shares the rows it did not change
  (ADR-0161).

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
  — decided, as ADR-0161.
- **Building what was decided** (`claude/live-data-next-cc`):
  - [04: Bulk writes and Action presses land, and the kept paints go](issues/04-bulk-writes-and-action-presses-land-and-the-kept-paints-go.md)
  - [05: The editor's commit lands, with an Overwrite Notice](issues/05-the-editor-commit-lands-with-an-overwrite-notice.md)
  - [06: The grid holds no row beyond its Window](issues/06-the-grid-holds-no-row-beyond-its-window.md)
  - [07: A pushed Window vouches, and the painted rows are checked](issues/07-a-pushed-window-vouches-and-painted-rows-are-checked.md)
  - [08: The Selection Summary walks only what it needs](issues/08-the-selection-summary-walks-only-what-it-needs.md)
  - [09: A report row holds no value and no report](issues/09-a-report-row-holds-no-value-and-no-report.md)
  - [10: The report's Change Highlight keeps times, not reports](issues/10-the-reports-change-highlight-keeps-times.md)
  - [11: The engine makes the next cube and report from the last](issues/11-the-engine-makes-the-next-report-from-the-last.md)
  - [12: A redraw that runs out of memory leaves the report stale](issues/12-a-redraw-out-of-memory-leaves-the-report-stale.md)
  - [13: Measure after the changes](issues/13-measure-after-the-changes.md)

## Numbers for the next decisions

ADRs for this work take numbers from `claude/live-data-next-cc`'s block, ADR-0160 to ADR-0169
(`docs/agents/numbering.md`): 0162 is the next. A Codex session works the same tickets on
`claude/live-data-next` with ADR-0150 to ADR-0159, for comparison; its tickets may share these numbers.
