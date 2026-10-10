# One live update, step by step: ExGrid and ExPivot

Date: 2026-10-06. Measured at `bd4b2e45`, after PR #64, in a detached worktree. This is
live-data ticket 01: observations to inform tickets 02 and 03, not architectural decisions.
No file under `src/` was changed. Performance never gates.

The large pivot is the material finding: rebuilding its cube and report dominates the redraw,
although the source folds 1,000 changes in less than a millisecond on CoreCLR. On published
WebAssembly, the 401,001-row report painted and completed six live updates, then failed with an
out-of-memory exception on update seven, in each of three independent browser runs. Those runs
**failed**; their successful frames are conditional observations, not a successful verification.

## Environment and method

- Apple M4 Pro, 12 cores, 24 GiB; macOS 26.6.2 (25G83). The machine was not idle: a spot reading
  during the browser measurements was load average 5.47 / 4.95 / 3.99. No other agent in this task built or ran
  a browser during this measurement.
- Nix-managed .NET SDK 10.0.203, runtime 10.0.7, arm64. CoreCLR: Release,
  `DOTNET_TieredCompilation=0`, two warm-up iterations and nine retained iterations; tables use
  the **minimum** of those nine. GC was collected before a sequence, outside its timers; collections
  the timed work caused remain in its time. No timings are a difference between rebuilt variants.
- Google Chrome 154.0.8037.98, headless, 1,400 × 1,100 CSS px, Playwright, one worker. The WASM
  host was published in Release, IL without AOT; its published files were served by the suite's
  static host on port 5499. No build happened under a running host. Headless observations are not
  a claim about a physical display's latency or scrollbar correctness.
- The published runtime's `dotnet.native.mx9wzm9o5h.js` has `getHeapMax=()=>2147483648`: a 2 GiB
  WebAssembly heap ceiling. No heap option was changed. This is evidence about the build's ceiling,
  not a heap profile proving which objects caused the failure.
- Every output path was absolute and inside this verification directory, never nix's temporary
  directory. Raw samples and logs are under `raw/`; the small, disposable reproduction sources are
  under `harness/`, outside any product or test build. Temporary sample/test/spike edits were removed.
- ag-grid was read at `release-36.2.0`, commit
  `0fee5b7b1e839ae23fe860e404042448f3c1375d`, in the session scratch directory. No Enterprise code
  was copied. The existing [research note](../../docs/research/ag-grid-rendering-on-data-change.md)
  supplies the pinned references.

## ExGrid: the source, Window and render on CoreCLR

The actual `DemoPivotData` generator and `/grid-live-local`'s eleven columns and formats were
used, with no Filter or Sort, as that page opens. Changes affect P&L and retain the rows' keys.
To attribute a render, the first `min(k, 20)` changes are in view; remaining changes are spread
through the data deterministically. This differs from the demo's random workload, particularly
its single-change batch, which is normally off screen. The real page is measured separately below.

The source's event listener was disconnected while its steps were timed. After an initial
publication, a fake clock and a one-day gathering interval kept `Apply` from publishing; the
harness then called the real private `PublishPending`, through reflection, separately. Publication
includes `LiveRequery` and the Change Highlight's comparisons. The separate `LiveRequery` observation
calls that real method with the same old Window and changes; it is **already included** in
publication and must not be added again. Each isolated call reads an equivalent input; a small
standalone minimum can differ from a containing step's minimum.

The real grid then takes the source's new Window. `ApplyState` is measured after resetting its
observed Window so that it takes it again; the separate `TakeInWindow` column is included in it.
The source vouches for its keys, so neither call walks the whole Window. The next render is timed
through bUnit and includes its renderer/DOM processing, not the browser's work. Counts confirm
18 painted rows, one re-render at k = 1, and 18 at k = 100/1,000; no number here is inferred from a
build passing. A direct reflected call's fixed overhead is included in the small times.

| Rows | Changes | Apply ms | Publication ms | Requery alone ms | TakeInWindow ms | ApplyState ms | bUnit render ms |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 100,000 | 1 | 0.013 | 0.052 | 0.039 | <0.001 | 0.006 | 0.146 |
| 100,000 | 100 | 0.091 | 0.144 | 0.090 | <0.001 | 0.005 | 0.229 |
| 100,000 | 1000 | 0.685 | 1.017 | 0.377 | 0.001 | 0.007 | 0.247 |
| 1,000,000 | 1 | 0.020 | 0.380 | 0.421 | 0.001 | 0.008 | 0.187 |
| 1,000,000 | 100 | 0.142 | 0.532 | 0.481 | 0.002 | 0.009 | 0.279 |
| 1,000,000 | 1000 | 1.080 | 1.755 | 1.173 | 0.001 | 0.007 | 0.280 |

At 100,000 rows and one visible change, the render is the largest isolated step. Publication
and Apply take over as the batch grows; at 1,000,000 rows, publication is the largest step in all
three configurations. The grid's vouched-for Window pass is not the cost that grows with the data.
These minima are not a sum measured end to end; use the browser observations for that.

## ExPivot: every step of a stable-row redraw on CoreCLR

There are 1,000 outer text Items (`A00000`…) and 10, 100 or 400 inner text Items (`B0000`…), one
record per pair, one decimal Sum, and no column field. The resulting reports have 11,001,
101,001 and 401,001 rows (outer group rows and the Grand Total included). Each batch changes one
record in every outer group, 1,000 changes in total, without changing any Item or row order.
This is the large two-row-field shape of D10; it is not `/pivot-live`'s small fourteen-row report.

The source's Apply/fold, its next `AggregateAsync` answer, `CubeAsync`, `ReportAsync`,
`HasSameRowsAsAsync`, the actual private `LabelWidthsAsync`, and the actual private `Show` were
timed as separate calls. Slicing's budget was a day so work stayed on this thread, including the
async code paths. `Show` includes extending the Change Highlight's history and building the grid's
columns. The values themselves remain lazy until read. The source's answer is a distinct step:
folding a batch does not eliminate the assembly of the full answer's Item/Leaf arrays.

| Report rows | Apply/fold ms | Next answer ms | Cube ms | Report ms | SameRows ms | Label widths ms | Show ms |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 11,001 | 0.183 | 0.183 | 1.148 | 1.544 | 0.109 | 0.354 | 0.014 |
| 101,001 | 0.346 | 1.605 | 17.582 | 37.855 | 1.162 | 3.366 | 0.040 |
| 401,001 | 0.518 | 6.532 | 74.221 | 156.859 | 4.235 | 13.254 | 0.038 |

`TreeAsync` and cached-key construction were also called independently, with no edited product
variant. Fresh axis roots/lists were prepared outside each timer; the key constructor was called
by a compiled expression against each existing report row, writing the results into an allocated
array. This includes a delegate call per key; array allocation is excluded. These are **substeps of
Cube and Report**, not additional costs.

| Report rows | Row tree ms | Column tree ms | Cached keys made ms |
| --- | --- | --- | --- |
| 11,001 | 0.619 | 0.114 | 0.074 |
| 101,001 | 11.197 | 0.238 | 5.399 |
| 401,001 | 55.212 | 0.587 | 12.962 |

The report's keys were then checked by the real grid's `RequireDistinctKeys`; the former
instance check was timed beside it. `TakeInWindow` below performs that same keyed check, and is
not an additional pass in the product. The following `ApplyState` was measured with the check
already complete. The diagnostic render used a real ExGrid with two simple columns over the
report rows; it is **not** the full ExPivot render: it does not use ExPivot's label templates,
columns or Change Highlight delegate. It establishes component reuse (18 painted/re-rendered,
zero remounted), and is not used to estimate the complete pivot's browser render. The full
component and its Change Highlight are present in the browser observations.

| Report rows | Key check ms | Instance check ms | TakeInWindow ms | ApplyState after check ms | Diagnostic bUnit render ms |
| --- | --- | --- | --- | --- | --- |
| 11,001 | 0.191 | 0.218 | 0.191 | 0.009 | 0.096 |
| 101,001 | 1.909 | 2.759 | 1.869 | 0.027 | 0.128 |
| 401,001 | 9.045 | 11.881 | 8.796 | 0.034 | 0.132 |

A follow-up measurement uses the **actual ExPivot component** in bUnit, with its real label
cells/templates, columns, normal component subtree and Change Highlight delegate. The simplified render
above remains a separate diagnostic. There was no structural obstacle to measuring the full
component; it had been omitted from the first attribution pass.

The component is initially mounted normally. Its source listener is then disconnected so source
Apply cannot trigger an unmeasured redraw. Each next answer and Cube are prepared, and the mounted
component's actual `BuildAsync` (Report, SameRows and label widths) and `Show` are completed **outside**
the render timer. On the renderer's dispatcher, the timer brackets that ExPivot's `StateHasChanged`
through its synchronous descendant rendering. The harness asserts immediately afterward that the
child grid holds the new report Window and rendered, then checks component instances, render counts
and changed DOM text. It also checks that the grid received ExPivot's real Change Highlight delegate.
The viewport is 1,200 × 480, compact labels, two row fields and one Value Field, as in the WASM page.
The fake clock advances 1 ms per update, keeping Change Highlight expiry out of this measurement.
Release, tiering disabled, two warm-ups and nine retained observations follow the same CoreCLR method.

| Report rows | Actual ExPivot bUnit render, including Window check ms | Painted rows | Re-rendered rows | Remounted rows |
| --- | --- | --- | --- | --- |
| 11,001 | 0.413 | 18 | 18 | 0 |
| 101,001 | 1.984 | 18 | 18 | 0 |
| 401,001 | 8.467 | 18 | 18 | 0 |

This is a .NET component-render call observed through bUnit, including render-tree diff and bUnit
DOM processing; it excludes browser layout/paint. Crucially, the child ExGrid receives the new Window
**inside** this call, so its Window adoption and key validation are included. **Do not add the earlier
key-check, TakeInWindow or ApplyState timings to this render column**, or subtract their independent
minima to invent a smaller render-only time. The 401,001-row render minimum is even slightly below the
earlier standalone key-check minimum, a reminder that separately observed minima are not additive.
The displayed rows all render once with new report-row instances, while all 18 component instances
are retained. Actual cell text changed in four painted rows at 11,001 report rows and two at each
larger size; the other rendered rows were not remounted either.

Cube and Report dominate at every report size. At 401,001 rows their minima total about 231 ms,
against 9.0 ms for the keyed check, 13.3 ms for label widths and 6.5 ms to assemble the next answer.
Removing only the check would leave the main work intact. `Show` itself is small; the Change
Highlight's lazy work at painted cells belongs to the subsequent render.

## Published WebAssembly: from Apply to the report's frame

For ExGrid, the probe follows `measure-live.spec.mjs`: the real `/grid-live-local` page runs with
`?rows=N&batch=k&interval=0`. Its status line reports Apply in rounded milliseconds; Apply includes
any source notification and synchronous .NET/DOM rendering. It is therefore **not added** to a
separate render time. The probe timestamps the status mutation and its next animation frame,
adding back the reported Apply duration (approximately ±0.5 ms from rounding, plus the small
status-publication tail). It watches grid text and counts grid mutation records as well.

Every sample records the status before the batch, at the measured mutation and after Pause.
Those batch numbers are checked below. A batch that changed no painted text still has a meaningful
publication-to-next-frame observation; it is labelled as such instead of waiting for a later
batch or a highlight's expiry. The rAF callback is the frame opportunity after the DOM write,
not an instrumented GPU presentation timestamp. Long tasks were flushed with `takeRecords()`;
none means no task above the browser's 50 ms reporting threshold, not zero CPU cost.

| Rows | Changes | Samples | Apply ms, median (rounded) | Apply → frame ms, median | Range ms | No painted text change | Grid mutations, median |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 100,000 | 1 | 36 | 10.0 | 15.0 | 2.0–26.1 | 36 | 0 |
| 100,000 | 100 | 36 | 10.0 | 11.0 | 7.7–18.2 | 6 | 5.5 |
| 100,000 | 1000 | 36 | 26.0 | 26.1 | 15.1–58.1 | 0 | 16 |
| 1,000,000 | 1 | 36 | 79.0 | 79.1 | 6.0–138.1 | 36 | 0 |
| 1,000,000 | 100 | 36 | 13.0 | 15.1 | 5.1–75.0 | 3 | 5.5 |
| 1,000,000 | 1000 | 36 | 30.0 | 30.1 | 18.0–90.1 | 0 | 17 |

All 216 samples retained the observed batch number after Pause. In all 135 samples with changed grid text, the grid and status mutations were seen in the same MutationObserver delivery, and the grid frame was no later than the status frame (within 1 ms). The other 81 samples measure a frame after a publication without a painted text change. No sample was substituted by a later batch.


For ExPivot, a disposable sample page uses the **real ExPivot component**, the same shape as the
CoreCLR measurement, its default slicing and Change Highlight, and a zero redraw interval. It
raises the Consumer's `MaxLeaves` and `MaxRows` to 1,000,000 explicitly, because the default
200,000-leaf cap refuses the largest shape. The WASM heap limit stays unchanged. A button applies
exactly one prebuilt 1,000-change batch. Immediately before `Apply`, a synchronous measurement-only
`performance.mark` timestamps the start; a MutationObserver and rAF timestamp the changed report.
The first group and its first detail change on every batch. The next batch is not applied until
the previous changed report has reached that frame.

| Report rows | Completed frames | Apply → frame ms, median | Range ms | Outcome |
| --- | --- | --- | --- | --- |
| 11,001 | 27 | 63.1 | 57.2–110.3 | 3/3 runs passed (9 updates each) |
| 101,001 | 27 | 485.8 | 448.0–530.7 | 3/3 runs passed (9 updates each) |
| 401,001 | 18 | 2071.1 | 1962.6–2139.7 | Failed on update 7 in 3/3 runs |

**The 401,001-row failure is reproducible.** Each repeat ran in a fresh Playwright worker with its own browser (worker PIDs 66712, 66815 and
66916 in the retained log). Each of those three measurement runs painted
its initial report and completed six updates. The seventh failed in
`PivotCube.BuildAsync`, while allocating `Dictionary<long, int>`, with
`System.OutOfMemoryException`. All eighteen successful samples are retained, and each failed run's
console stack is in `raw/pivot-browser-400-repeatN-errors.json`. There is no seventh frame to time.
This is not a passed measurement or a steady-state latency claim. An earlier combined run failed
already at the largest shape's initial cube; a standalone fresh-browser load then succeeded.
Both observations are retained. No timeout, retry or heap-limit increase was used to turn an
exception into a pass. Diagnosing the retained memory is outside this measurement ticket.
The subsequent [controlled memory diagnosis](../2026-10-06-macos-pivot-memory/README.md)
identifies the paint-history retention path; the failed runs above remain failed observations.

A CoreCLR minimum cannot predict a WebAssembly frame. The browser numbers include the interpreter,
slicing/yields, allocation/collection, rendering and the next frame opportunity. The actual ExPivot's CoreCLR row render counts are measured above; an isolated WASM .NET-render time was not measured. This run did not
instrument the shipped code to separate those browser costs, so it does not attribute a browser
outlier to GC or to .NET rendering without evidence.

## Blazor Server: bytes per update

A temporary page in `spikes/render-bench/Bench.Server` renders the **real ExGrid**, using the demo's
rows and eleven columns, its Row Key and source Change Highlight. It has one button per update and
uses `IHandleEvent` to avoid an unrelated parent render. The first `min(k, 20)` amended trades are
in view; the other amendments are off screen. Only P&L changes, so this is a controlled visible
workload, not the demo's random mix of P&L and Notional. The highlight lasts ten minutes to keep
expiry renders outside these observations.

The M2 byte counters and frame/ack method are reused: the spike's connection middleware counts bytes Kestrel writes to the
circuit, after the default per-message compression; Chrome's `JS.RenderBatch` payload is read after
decompression. One update ends on the new render batch and its `OnRenderCompleted` acknowledgement,
not a fixed sleep. HTTP bytes used to read the counters are on other connections. The spike sends `X-Bench-Connection` on the WebSocket handshake; the driver uses that Kestrel
connection ID, not an inference from byte deltas. It also awaits the event completion, so its
bytes are included. The original M2 heuristic (the connection whose counter increased most) was
wrong for these small batches: an HTTP counter response could be larger than the grid update.
Those invalid observations, including zero wire-byte samples, are retained under `raw/exploratory/`
and excluded from every final table. The driver also parses each length-prefixed SignalR message,
including multiple messages carried by one WebSocket event. The server's protocol handshake was
observed as binary opcode 2 with bytes `7b7d1e` (`{}` plus Record Separator); it is decoded as the
JSON handshake before binary message framing begins. The raw file records its request ID, opcode
and bytes. Earlier driver failures (an unhandled handshake and a partial run before framing was
corrected) are exploratory artifacts. No timeout was extended. Four warm-up updates precede
fifteen retained updates per configuration.

| Rows | Changes | Painted rows | Render payload bytes/update, median | Compressed wire bytes/update, median |
| --- | --- | --- | --- | --- |
| 100,000 | 1 | 18 | 357 | 30 |
| 100,000 | 100 | 18 | 2480 | 122 |
| 100,000 | 1000 | 18 | 2480 | 122 |
| 1,000,000 | 1 | 18 | 357 | 30 |
| 1,000,000 | 100 | 18 | 2480 | 122 |
| 1,000,000 | 1000 | 18 | 2480 | 122 |

All 90 retained updates produced exactly one render batch, one acknowledgement and one event completion. All six configurations negotiated `permessage-deflate; client_max_window_bits=15`; the browser console was clean. Bytes do not grow with total rows when the same visible rows change; k=100 and k=1,000 amend the same painted slice in this controlled workload.


## What the numbers propose for tickets 02 and 03

- **02: consider a Consumer vouch for a pushed Window**, while preserving named refusal when it
  is not vouched for. ExGrid's bundled path already makes its check negligible. The large pivot's
  check costs about 9 ms, so a vouch can remove a real whole-report pass, but cannot by itself
  repair a 231 ms Cube-plus-Report rebuild or a roughly two-second browser redraw. The Consumer's
  correctness obligation is a decision for the user, not a conclusion established by timing.
- **03: retain the computation and report across stable-row updates**, if the user's contract
  permits it. Prioritise the axis trees/cube and report construction, then the full-width/sequence
  passes. Retaining only rendered row components has already been achieved: zero remounted rows in
  both the diagnostic and the actual ExPivot render, yet almost all large-report work remains. A cached key's construction is about
  13 ms here, and is one part of Report rather than the whole problem.
- **A differential source answer also has a measurable purpose**: the next answer still costs
  about 6.5 ms at 400,000 leaves even though Apply/fold takes 0.52 ms. Whether such a contract reaches
  server Consumers, how full answers fall back, and what certifies unchanged rows are design choices.
- **Memory must be part of the follow-up verification**. Keeping structures may reduce allocations,
  but this ticket has not proved the cause of the OOM or that any proposed design fixes it. Re-run
  this largest shape through more than six updates, with its existing heap limit, after implementation.

There is no Docs Site change: this ticket observes existing behaviour and changes no product API,
UI or stated contract. It does not sign off Windows, physical scrolling, a real IME, or the full
layer-3 suite.

## Reproduce

The archived files in `harness/` are measurement artifacts, not permanent tests or sample pages.
Use a disposable worktree at the measured commit, copy them back to the paths below, and `git add`
new files before invoking nix (its flake sees tracked files only). Run
`git apply --unidiff-zero verification/2026-10-06-macos-live-update-costs/harness/setup.patch`
from the worktree root for the two project-file links, blank favicon, and explicit connection-ID
header described below. The patch touches no product source; the new-file copies are still required.

1. Copy `LiveCostMeasurement.cs.txt` to `tests/ExPivot.Components/LiveCostMeasurement.cs`. The
   setup patch adds its temporary Compile link to `../../samples/ExGrid.DemoPages/DemoPivotData.cs`.
   Build it in Release, then run the built test assembly with the xUnit runner directly:
   `DOTNET_TieredCompilation=0 LIVE_COST_OUTPUT=/absolute/path/raw nix develop -c dotnet
   tests/ExPivot.Components/bin/Release/net10.0/ExPivot.Components.Tests.dll -class
   ExPivot.Components.Tests.LiveCostMeasurement`. The logs distinguish the two-step run from the
   one-method Tree/key run. The `dotnet test --filter` attempt ran the whole test assembly on this
   toolchain; its numbers were replaced by the isolated run, not mixed into the final tables.
2. Copy `LiveCostPage.razor.txt` into `samples/ExGrid.DemoPages/Pages/LiveCostPage.razor`. Publish
   `samples/ExGrid.DemoHost -c Release -o /absolute/path/hosts/wasm`. Copy the two `measure-cost`
   spec/config files into `tests/ExGrid.Browser`, install its locked dependencies with `npm ci`, and
   run `npx playwright test --config measure-cost.config.mjs --project=chrome --repeat-each=3`
   inside the browser nix shell, with `LIVE_COST_OUTPUT`, `EXGRID_HOSTS`,
   `EXGRID_BASE_URL=http://localhost:5499` and `EXGRID_HEADLESS=1` set. This runs only the eight
   successful configurations. For the largest pivot, use `COST_PIVOT_INNER=400`,
   `--grep 'measure pivot' --repeat-each=3`; its three failures are the observation above. Playwright replaces the worker and its browser
   after each failed test; the three recorded worker PIDs establish independent browsers for
   this run. If measuring a changed implementation that completes all updates, use independent
   browser invocations for the largest shape as well.
3. Copy `GridCost.razor.txt` and `GridCostIndex.razor.txt` into `spikes/render-bench/Bench.Server`;
   the setup patch adds the temporary reference to `../../../samples/ExGrid.DemoPages/ExGrid.DemoPages.csproj`.
   The setup patch adds `<link rel="icon" href="data:," />` to the spike's `App.razor` head: otherwise its missing
   favicon is an unexpected 404, which failed and invalidated the first wire attempt.
   Build Release, start that spike with `BENCH_PORT=5497`, and run `measure-cost-wire.mjs.txt` as a
   `.mjs` beside the browser tests with `COST_SERVER=http://localhost:5497` and `LIVE_COST_OUTPUT`.
   Stop only the PID started for this spike. Do not run it beside another browser measurement.
4. For the full ExPivot render supplement, copy `LiveFullRenderMeasurement.cs.txt` to
   `tests/ExPivot.Components/LiveFullRenderMeasurement.cs` and `git add` it. This file needs no
   project-file modification. Build that test project in Release, then run only
   `ExPivot.Components.Tests.LiveFullRenderMeasurement` with the direct xUnit class command from
   step 1, `DOTNET_TieredCompilation=0` and the absolute `LIVE_COST_OUTPUT`. This supplement does
   not rerun the other measurements or a browser.

`raw/exploratory/` retains the first ExGrid probe without a long-task flush and its interrupted
harness run; it is excluded from final statistics. The large trace ZIPs and published binaries are
not committed; their relevant console/stack data and the reproduction sources are kept instead.

## Retained evidence

- CoreCLR: `raw/grid-core.json`, `raw/pivot-core.json` and
  `raw/pivot-trees-and-keys.json`; execution logs `core-isolated.log` and `tree.log`.
  The full-component supplement is `raw/pivot-full-render.json`, with its isolated passing
  execution in `raw/full-render.log` and reproduction in `harness/LiveFullRenderMeasurement.cs.txt`.
- Published WASM: `raw/grid-browser-*-repeat[012].json` and
  `raw/pivot-browser-*-repeat[012].json`; `browser-final.log` records the 24 successful
  repeated configurations. `browser-pivot400.log` records the three failed large-pivot runs,
  with their exception stacks in `pivot-browser-400-repeat[012]-errors.json`. The failed
  seventh updates have no frame sample and are not substituted or assigned a duration.
- Initial large-pivot observations: `raw/pivot-browser-400-initial-error.txt`,
  `raw/pivot-browser-400-fresh.json` and `raw/browser-400-fresh.log`.
- Server: `raw/grid-wire.json` contains all 90 retained updates, explicit circuit IDs,
  protocol handshakes and decompressed message accounting; `raw/wire.log` records completion.
- `harness/summarize.py` recalculates the tables from these retained samples and checks the
  batch/frame correspondence and sample counts. `raw/tables.json` is its generated table output.
  Files under `raw/exploratory/` are excluded: they include the early unflushed long-task probe,
  the non-isolated CoreCLR invocation, and invalid wire-driver runs.
