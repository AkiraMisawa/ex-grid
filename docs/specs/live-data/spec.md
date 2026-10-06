# Live data in ExGrid and ExPivot

Status: in progress — the first version is built on `claude/exgrid-live-data`; tickets 01 to 03 are
what comes next

Decided with the user in the grilling of 2026-10-05, and in the decisions D1 to D10, P1 and P2 of
2026-10-06:

- [ADR-0140](../../adr/0140-a-row-key-names-a-row-across-versions-and-the-grid-repaints-a-changed-row-in-place.md):
  the Row Key, and a changed row repainted in place;
- [ADR-0141](../../adr/0141-exgrids-bundled-sources-take-live-data-by-row-key-on-expivots-rules.md):
  the bundled sources take live data by Row Key, on ExPivot's rules
  ([ADR-0067](../../adr/0067-live-data-a-change-batch-makes-the-next-snapshot-and-expivot-folds-it-in.md));
- [ADR-0142](../../adr/0142-a-write-is-refused-when-what-the-user-saw-of-its-target-changed.md):
  a write is refused when what the user saw of its target changed.

The criteria are §32 of `docs/definition-of-done.md` (LV-1 to LV-18), PV-42 and PV-43 in §29, and
two rows of §21.11. What is built, found and not done is in `docs/implementation-status.md`, "ExGrid's
live data (2026-10-06)". This spec synthesises those decisions; where it and they disagree, they win.

## Where it started

- **The research note**: [how ag-grid repaints when its data changes](../../research/ag-grid-rendering-on-data-change.md),
  pinned to ag-grid 36.2.0 (commit `0fee5b7b1e839ae23fe860e404042448f3c1375d`). Its §9 questions were
  decided by the ADRs above; where they differ, the ADRs hold.
- **The measurements**: [`verification/2026-10-05-macos-live-update-measure`](../../../verification/2026-10-05-macos-live-update-measure/README.md)
  (M1 to M5), and the Row Key's cost in ADR-0140's "Settled while building it" (D10).

## The idea in one paragraph

ag-grid repaints quickly under live data because three things outlive an update: each row's id,
computed once; the row's node, and so its rendered row; and a map of ids, checked only when a node
is made. The family follows it where the ADRs allow. A Row Key names a row across versions and keeps
its component (ADR-0140). The bundled sources keep a map of keys, take changes by key, requery
incrementally and vouch that their Windows hold no row twice, so the grid does not walk them
(ADR-0141). What still does not outlive an update is listed in the tickets: a Window a Consumer
pushes itself is walked whole each time, and ExPivot builds its cube, its report and every report
row again on every redraw.

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
  — first; the other two are decided on its numbers.
- [02: A pushed Window vouches for its rows](issues/02-a-pushed-window-vouches-for-its-rows.md)
  — a decision for the user, for ExGrid's push form and ExPivot alike.
- [03: ExPivot keeps its report across live redraws](issues/03-expivot-keeps-its-report-across-live-redraws.md)
  — ag-grid's approach for the pivot; decisions for the user, then an ADR.

## Numbers for the next decisions

ADRs for this work take numbers from `claude/exgrid-live-data`'s block, ADR-0140 to ADR-0149, while
the work stays on that branch: 0143 is the next. A new branch reserves a block of its own on `main`
first (`docs/agents/numbering.md`).
