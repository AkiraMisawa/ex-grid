# Live data in ExGrid and ExPivot

Status: in progress — the first version from `claude/exgrid-live-data` merged into `main` in
PR #64; tickets 01 to 03 continue on `claude/live-data-next`

Decided with the user in the grilling of 2026-10-05, and in the decisions D1 to D10, P1 and P2 of
2026-10-06:

- [ADR-0140](../../adr/0140-a-row-key-names-a-row-across-versions-and-the-grid-repaints-a-changed-row-in-place.md):
  the Row Key, and a changed row repainted in place;
- [ADR-0141](../../adr/0141-exgrids-bundled-sources-take-live-data-by-row-key-on-expivots-rules.md):
  the bundled sources take live data by Row Key, on ExPivot's rules
  ([ADR-0067](../../adr/0067-live-data-a-change-batch-makes-the-next-snapshot-and-expivot-folds-it-in.md));
- [ADR-0142](../../adr/0142-a-write-is-refused-when-what-the-user-saw-of-its-target-changed.md):
  a write is refused when what the user saw of its target changed.

Continuation Q1, decided with the user on 2026-10-06:
[ADR-0150](../../adr/0150-a-consumer-can-vouch-for-a-pushed-windows-rows.md) lets a Consumer
explicitly vouch for a pushed Window with a Row Key, with validation remaining the default.
It clarifies the source form too. This decision is recorded; implementation is pending.

Continuation Q3, decided with the user on 2026-10-06 after the A/B and memory experiments:
[ADR-0151](../../adr/0151-server-pivots-send-report-windows-and-share-the-local-engine.md)
chooses server-computed report Windows and changes, with the same incremental engine in the
browser for local CSV. The server boundary is settled; operations outside the Window and
immutable row ownership still need their concrete design. The diagnosed
historical-Report retention must be repaired while preserving ADR-0142.

Continuation Q5 to Q7, decided with the user on 2026-10-06:
[ADR-0152](../../adr/0152-report-apis-may-change-and-remote-reports-recover-their-baseline.md)
permits changing/retiring the existing API and choosing appropriate names without a required
compatibility path. Remote Order Keys are registered on the server; Culture and display settings
are explicit. A missing delta baseline automatically requests a complete current Window, with
Stale Report/Retry on failure; an operation against an older version is never silently rebound.

Continuation Q8 and Q9, decided with the user on 2026-10-06:
[ADR-0153](../../adr/0153-reports-share-unchanged-computation-and-display-rows-own-no-report.md)
separates immutable shared report versions from detached display rows and keys. Ordinary
changes update their dependencies, and unchanged rows are reused. A provider unable to identify
changes may declare full refresh explicitly; the bundled Snapshot source remains incremental.
The design is accepted and `/implement` is authorized, including commits but not a feature push
or PR without the user's confirmation.

The criteria are §32 of `docs/definition-of-done.md` (LV-1 to LV-28), PV-2, PV-42 and PV-43 in §29, and
two rows of §21.11. What is built, found and not done is in `docs/implementation-status.md`, "ExGrid's
live data (2026-10-06)". This spec synthesises those decisions; where it and they disagree, they win.

## Where it started

- **The research note**: [how ag-grid repaints when its data changes](../../research/ag-grid-rendering-on-data-change.md),
  pinned to ag-grid 36.2.0 (commit `0fee5b7b1e839ae23fe860e404042448f3c1375d`). Its §9 questions were
  decided by the ADRs above; where they differ, the ADRs hold.
- **The measurements**: [`verification/2026-10-05-macos-live-update-measure`](../../../verification/2026-10-05-macos-live-update-measure/README.md)
  (M1 to M5), and the Row Key's cost in ADR-0140's "Settled while building it" (D10).
  Ticket 01's [step-by-step measurements of ExGrid and ExPivot](../../../verification/2026-10-06-macos-live-update-costs/README.md)
  record the costs after PR #64, including the repeated large-pivot WebAssembly failure.
  The follow-up [memory diagnosis](../../../verification/2026-10-06-macos-pivot-memory/README.md)
  isolates historical paints retaining whole Report generations; the
  [incremental computation-boundary experiment](../../../verification/2026-10-06-macos-pivot-boundary-bench/README.md)
  compares Leaf Aggregate deltas with server-computed report Windows and informed ADR-0151.

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
  — decided by ADR-0150, for ExGrid's push form and ExPivot alike; implementation pending.
- [03: ExPivot keeps its report across live redraws](issues/03-expivot-keeps-its-report-across-live-redraws.md)
  — ADR-0151 decides the shared engine and server Window boundary; ADR-0152 settles API freedom,
  remote policies and recovery; ADR-0153 completes row ownership and incremental scope. Ready for implementation.

## Numbers for the next decisions

ADRs for this continuation take numbers from `claude/live-data-next`'s block, ADR-0150 to ADR-0159,
reserved on `main` on 2026-10-06 (`docs/agents/numbering.md`). ADR-0150 records the pushed Window's
promise; ADR-0151 records the shared local/server report engine and server Window boundary.
ADR-0152 records API freedom, remote policies and version recovery; ADR-0153 completes retained
computation and detached display rows. ADR-0154 is the next number. The old
branch's unused ADR-0143 to ADR-0149 stay unused; its reservation does not transfer with the work.
