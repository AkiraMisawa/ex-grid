# A live ExPivot runs out of memory in the browser: the cause

Date: 2026-10-06. Branch `claude/live-data-next-cc`, at `41c8d8c8` (origin/main), in a worktree of its
own with the harness below. Ticket 01's run
([`2026-10-06-macos-live-update-costs-cc`](../2026-10-06-macos-live-update-costs-cc/README.md)) saw a
live ExPivot throw `OutOfMemoryException` in the browser on the 7th redraw of a 401,001-row report and the
14th of a 201,001-row one. This record finds why. What was decided on it is
[ADR-0160](../../docs/adr/0160-the-grid-holds-no-consumer-row-beyond-the-window-it-was-given.md) and the
rewrite of [ADR-0142](../../docs/adr/0142-a-write-lands-as-the-user-entered-it-and-a-change-under-the-editor-is-told.md).

Memory, not time, is measured here, so the other track working on the machine at the same time does
not bear on the numbers. Every number is observational.

## The answer in brief

- **Each live redraw left its whole report and cube reachable.** After a full, compacting collection,
  the managed heap grew by 41 MB a redraw at 101,001 rows, without end, and every report shown stayed
  alive. The WebAssembly heap cannot grow past 2 GB, and it never shrinks.
- **What held them was ExGrid's kept paints** (ADR-0142 as built, `PaintsKept = 64`). Each paint kept its
  painted row instances. A `PivotReportRow` points at its report (`PivotReport.cs:276`), and the report
  at its cube, so a paint of 11 rows kept a generation of 52 MB at 101,001 rows and 194 MB at 401,001.
  With the paints capped at 2, the heap levels off and 401,001 rows run without failing.
- **The class design and the architecture set how big each kept generation is**: about 500 bytes a
  report row in WebAssembly, built afresh on every redraw. **The platform sets the ceiling**: 2 GB.
  Neither of those is the leak; the leak is the paints.

## Environment

- Apple M4 Pro, 12 cores, 24 GB, macOS 26.6.2.
- The WebAssembly DemoHost published with `dotnet publish -c Release` (SDK 10.0.203, no AOT), served by
  `tests/ExGrid.Browser/static-host.mjs` on port 5699.
- Google Chrome 154 through Playwright, headless.
- The published `dotnet.native.js` caps the heap: `getHeapMax=()=>2147483648`, so 2 GB.
- The harness page is ticket 01's `/pivot-live-costs` (`?a=1000&b=…&batch=1&step=1`): records of two text
  fields `A00000`… × `B0000`…, one record per pair, so the report has as many rows as records, plus A's
  subtotals and the grand total. Each press of its step button applies one Change Batch of one changed
  record; ExPivot redraws.
- Two probe buttons were added for this record, tagged `[DEBUG-oom]` (`harness/`):
  - one makes a full, compacting collection twice and shows the managed heap, with weak references to
    every report, cube and Snapshot seen so far;
  - one builds a fresh answer, cube and report beside the ones on screen, and measures what each adds
    to the managed heap after a full collection.

## The loop that reproduces it

`harness/loop.mjs <baseUrl> <a> <b> <redraws> "" 1` loads the page and presses the step button. After
each redraw has landed, which is when the report shows the changed record's new value, it records the
WebAssembly heap, the managed heap after a full collection, and how many reports, cubes and Snapshots are
still alive. It stops at the first "Out of memory" on the console.

### As built: every generation stays

At 1000 × 400 (401,001 report rows), no collection forced (`raw/run1-1000x400.log`):

| After | Load | 1 | 2 | 3 | 4 | 5 | 6 | 7 |
|---|---:|---:|---:|---:|---:|---:|---:|---:|
| WebAssembly heap, MB | 591 | 783 | 1,081 | 1,273 | 1,465 | 1,758 | 1,958 | 2,048, out of memory |

The 7th redraw threw `System.OutOfMemoryException: Out of memory at
ExPivot.Engine.PartColumns..ctor` inside `PivotCube.BuildAsync`, as an unhandled exception that stopped
the page.

At 1000 × 100 (101,001 rows), a full collection after each redraw (`raw/run2-1000x100-gc.log`,
`raw/run3-weak.log`):

| After | Load | 1 | 2 | 3 | 4 | 6 | 8 | 10 | 12 |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| Managed heap after a full collection, MB | 87.9 | 129.1 | 172.3 | 215.5 | 258.7 | 341.0 | 423.3 | 505.6 | 587.9 |
| Reports alive / seen | 1/1 | 2/2 | 3/3 | 4/4 | 5/5 | 7/7 | 9/9 | — | — |
| Snapshots alive / seen | 1/1 | 2/2 | 3/3 | 4/4 | 4/5 | 4/7 | 4/9 | — | — |

- **The growth survives a full, compacting collection**: 41.1 MB a redraw, in a straight line. It is
  retention, not a collector that is slow to run, and not fragmentation.
- **The Snapshots stop at 4**, as `SnapshotPivotSource.AnswersHeld = 4` intends. The reports and cubes
  do not stop.

### The paints capped at 2: it levels off

The same loop on a build with `PaintsKept = 2` (`harness/paints-kept-2.patch`, a probe, never a fix):

| | Managed heap after a full collection | Reports alive | Out of memory |
|---|---|---|---|
| 1000 × 100, 10 redraws (`raw/run4-paints2.log`) | 225.2 MB from redraw 4 on | 4 | no |
| 1000 × 400, 15 redraws (`raw/run5-paints2-400k.log`) | 626.5–662.7 MB | 3 | no; the WebAssembly heap stayed at 1,287–1,383 MB |

The kept paints are the whole of the unbounded growth. What stays with the cap is a few generations held
on purpose: the report on screen, the Change Highlight's history of reports (ADR-0068; with the default
1 s highlight and 250 ms redraw, about five of them, read from the code rather than measured), and the source's four
Snapshots.

## What one generation is made of

The second probe, on the page as loaded, before any redraw (`raw/gen.log`). Bytes are managed heap after
a full collection, per report row:

| Report rows (leaves) | Answer | Cube | Report | Value cells, every row read | One generation in all |
|---|---:|---:|---:|---:|---:|
| 11,001 (10,000) | 1 B | 294 B | 120 B | 107 B | 5.5 MB |
| 101,001 (100,000) | 4 B | 310 B | 117 B | 107 B | 51.8 MB |
| 401,001 (400,000) | 4 B | 279 B | 117 B | 107 B | 193.8 MB |

- **The answer adds almost nothing**: it shares the source's arrays.
- **The cube is the largest part**, about 300 bytes a leaf. Reading the code: its cell arrays and its
  map of cells are made for twice the leaves (`PivotCube.BuildAsync`, `Math.Max(16, leafCount * 2)`),
  though subtotals add a quarter of a per cent; every axis node is an object, and each makes a `List` for
  its children, leaves included.
- **A report row** is an object, an array of labels and the labels themselves; **a value cell**, once
  read, is a boxed value in an array on the row. Only painted cells are read in use; the column is the
  cost of reading them all.

## The cause, in layers

1. **The leak: kept paints hold row instances, and a report row holds its generation.** ExGrid kept its
   last 64 paints so that a write could be judged against what the user saw (ADR-0142 as decided on
   2026-10-05). Each paint kept its rows to recompute their painted text later, though the ADR said the
   grid keeps "what it painted" and ADR-0140 that it "holds nothing between Windows". ExGrid's code
   assumed a row is a small value; ExPivot's row is a handle into its whole report and cube. Neither ADR
   wrote down its assumption, and the defect sits between them.
2. **The amplifier: every redraw builds a whole new cube and report.** Nothing is shared between two
   generations, so each kept one costs its full size. Ticket 01 measured where the redraw's time goes:
   57–89% in building the cube and the report.
3. **The weight of a row**: about 500 bytes a report row in a 32-bit WebAssembly heap, more on CoreCLR's
   64-bit one.
4. **The ceiling**: the 2 GB heap of .NET's default WebAssembly build, which never gives memory back.
5. **The failure mode**: an out-of-memory inside a redraw is an unhandled exception, and the page stops.
   ADR-0067's Stale Report is the way to say that a report could not be brought up to date.

ExGrid's own rows from `GridSource.From` are the Consumer's small objects, so the same paints keep a few
old versions of a few rows there, not a generation. The Fluxor spike measured exactly that: 630 old trade
versions at most, and no old store state
([`2026-10-06-macos-fluxor-spike`](../2026-10-06-macos-fluxor-spike/README.md), check 6). A row type that
points at its container, such as a row view over a Snapshot, would meet the same leak.

On CoreCLR, ticket 01's harness saw the same: every report and cube shown stayed alive, the heap grew from
154 MB to 1,225 MB in 20 redraws at 101,001 rows, and clearing the kept paints by reflection freed 912 MB
([`2026-10-06-macos-live-update-costs-cc`](../2026-10-06-macos-live-update-costs-cc/README.md), raw
`pivot-memory-*.json`). The Server host runs that runtime, one grid per circuit.

## Harness

- `harness/loop.mjs` and `harness/gen.mjs`: the two Playwright scripts. They load Playwright through
  `createRequire` from an absolute path to a checkout's `tests/ExGrid.Browser`; change that path to run
  them elsewhere.
- `harness/samples/ExGrid.DemoPages/Pages/PivotLiveCostsPage.razor`: ticket 01's harness page with the two
  `[DEBUG-oom]` probes. Copy it into `samples/ExGrid.DemoPages/Pages/`, `git add -N` it, publish the
  WebAssembly DemoHost and serve it with `static-host.mjs`.
- `harness/paints-kept-2.patch`: the probe that capped the kept paints at 2.
