# The pinned shade's cost — 2026-10-11, Linux container

**Scope: [ADR-0206](../../docs/adr/0206-a-ranges-tint-is-an-element-of-its-own-and-on-the-paper-the-pinned-tints-multiply-as-one-layer.md),
"What it costs, and what is checked".** A range is now a tint and a box in each layer it reaches, the
pinned tints and bands lie in a sticky shade layer of their own, and on ExSheet's Paper that layer
multiplies as one group, as each scrollable tint and band does. The ADR records the compositing cost as
a prediction and asks for three observations before it counts as done: the drag cost before and after,
the frame intervals while scrolling with column A selected at 100% and 150%, and the composited layer
count read through the Chrome DevTools Protocol. Its Verification lists PF-6 and PF-7 besides. They are
all here, with every raw figure in [`raw/`](raw/) (`scroll-layers.json` gzipped, read with `zcat`);
[`tables.py`](tables.py) prints every table below from them.

**Everything here is observational** (AGENTS.md: performance never gates). The sub-pixel gate, the one
check the ADR makes a condition, is a layer-3 test, not a measurement: `part-c.spec.mjs`, "DC-59: with a
pinned tint on screen and the page a third of a pixel across and down…". It passed here at 100% and at
150% on both hosts (below).

**Taken on the container's software rasteriser.** The machine has no GPU (`/dev/dri` is absent); the
browser is Playwright's bundled Chromium, not Google Chrome, and it rasterises and composites in
software (the WebGL renderer the pages were given is in `raw/scroll-layers.json.gz`). Frame intervals and
paint costs on a GPU, and on Windows, are not these, and the layer count is the compositor's answer for
this configuration. The machine was shared with other sessions' builds and test runs throughout; the load
average is recorded with each run.

## The machine and the builds

- An Intel Xeon at 2.10 GHz, 4 cores, 15 GB, Linux 6.18.44, in a container. Node 22.22.
- Chromium 141.0.7390.37 (`/opt/pw-browsers/chromium-1194`), driven by Playwright 1.56.
- **before**: `e3ad9a50`, main as `claude/pinned-shade` was cut, whose selection is ADR-0008's: one
  element per range per layer, its tint a `::before` beneath its outline. The branch was rebased
  afterwards onto `0186cc98`; none of the commits between touches the selection's code or its
  stylesheets' selection rules, so these comparisons were not taken again. PF-6 and PF-7, taken after
  the rebase, compare against `0186cc98` instead (below).
- **after**: the change, as committed with this record on `claude/pinned-shade`.
- Each a Release `dotnet publish` of `samples/ExGrid.DemoHost.Server` and `samples/ExGrid.DemoHost`,
  started as below: the Server host directly (no latency proxy, so no injected round trip), the
  WebAssembly host's files served by `../2026-09-29-selection-hole/static.mjs`.

## How to run it

From the repository root, with `npm ci` done in `tests/ExGrid.Browser` (Playwright comes from there):

```sh
S=/some/scratch
git archive e3ad9a50 | tar -x -C $S/before-src
nix develop -c dotnet publish $S/before-src/samples/ExGrid.DemoHost.Server -c Release -o $S/before/server
nix develop -c dotnet publish $S/before-src/samples/ExGrid.DemoHost -c Release -o $S/before/wasm
nix develop -c dotnet publish samples/ExGrid.DemoHost.Server -c Release -o $S/after/server
nix develop -c dotnet publish samples/ExGrid.DemoHost -c Release -o $S/after/wasm

(cd $S/before/server && ASPNETCORE_ENVIRONMENT=Development nix develop "$OLDPWD" -c dotnet ExGrid.DemoHost.Server.dll --urls http://localhost:7470) &
(cd $S/after/server && ASPNETCORE_ENVIRONMENT=Development nix develop "$OLDPWD" -c dotnet ExGrid.DemoHost.Server.dll --urls http://localhost:6470) &
node verification/2026-09-29-selection-hole/static.mjs $S/before/wasm/wwwroot 8471 &
node verification/2026-09-29-selection-hole/static.mjs $S/after/wasm/wwwroot 5471 &

export EXGRID_EXECUTABLE=/opt/pw-browsers/chromium-1194/chrome-linux/chrome   # where Chrome is absent
node verification/2026-10-11-linux-pinned-shade/run-drag.mjs
xvfb-run -a -s "-screen 0 2560x1600x24" node verification/2026-10-11-linux-pinned-shade/run-scroll.mjs
```

The hosts can be moved with `EXGRID_BEFORE_SERVER`, `EXGRID_AFTER_SERVER`, `EXGRID_BEFORE_WASM` and
`EXGRID_AFTER_WASM`. Stop each host by its own PID, or by its port (`fuser -k 7470/tcp`).

PF-6 and PF-7 are the browser suite's own measurements (`tests/ExGrid.Browser/observational.spec.mjs`),
run against each build's Release publish as CI runs the suite (`EXGRID_HOSTS` naming a directory that
holds `server`, `wasm` and `api`, the last `samples/ExGrid.DemoApi` published the same way), on each host,
the two builds interleaved, two rounds, with the hosts above stopped first. From `tests/ExGrid.Browser`,
for each of `before` and `after`:

```sh
EXGRID_HOSTS=$S/before EXGRID_BASE_URL=http://localhost:5471 \
  xvfb-run -a npx playwright test --project=chrome --grep "PF-6|PF-7" observational.spec.mjs
EXGRID_HOSTS=$S/before EXGRID_HOSTING=server EXGRID_BASE_URL=http://localhost:5470 \
  xvfb-run -a npx playwright test --project=chrome --grep "PF-6|PF-7" observational.spec.mjs
```

with a configuration that names the Chromium (`launchOptions.executablePath`) where Chrome is absent.
Each run writes `metrics.json` under `verification/<day>-linux/`; its `PF-6` and `PF-7` were copied into
[`raw/pf-6-pf-7.json`](raw/pf-6-pf-7.json) and the run's own directory removed, since a run on this
Chromium is not a Step 4 record.

## The drag cost

`../2026-09-29-selection-hole/drag-cost.mjs`, extended (2026-10-11) for `?pin=` (`EXGRID_PIN`), for
`?chrome=mud` (`EXGRID_CHROME`), and for a Chromium named by path (`EXGRID_EXECUTABLE`). It opens
`/sheet` headless at a device scale of 2 in a 1400×1000 viewport, as that record did, puts the Sheet at
the window's top, presses a start cell and moves the pointer one cell at a time, ten times: one row
down each time and one column right for the first four, so the range grows from 2 to 55 cells with the
Focus in its corner. Each move waits until a range's painted height shows it landed, so a drag is ten
renders on either host. Around the ten moves it reads `Performance.getMetrics` in thread ticks:
`TaskDuration` (all main-thread work), `ScriptDuration`, `RecalcStyleDuration` and `LayoutDuration`, in
ms, summed over the drag. `run-drag.mjs` interleaves the two builds round by round: four rounds of one
batch per side, five drags a batch, each batch a fresh browser.

The start is row 2 (A2, or B2), not the 2026-09-29 record's row 3: with the Sheet at the window's top
its Viewport is 445 px tall, and the tenth move from row 3 reached the edge band, which scrolls the
Sheet on past the range the wait looks for (every WebAssembly batch timed out so, in a first pass that
was discarded). On `/sheet` column A is pinned unless `?pin=` says otherwise, so:

- **A2, pin=1** crosses the pinned boundary on the first move right: a tint and a box in each layer
  after, a box with its `::before` tint in each layer before;
- **A2, pin=3** stays in the Pinned Columns for three moves and crosses on the fourth;
- **A2, pin=1, MudBlazor** is the first under `ExSheet.MudBlazor`'s Chrome, where the shade is coloured;
- **B2, pin=0** pins nothing, so no pinned layer is painted at all, before or after.

Measured on 2026-10-11 from 20:19 to 20:29 UTC; the load average was 5.2 at the start and 16.7 at the
end, other sessions building and testing on the same four cores. Every batch completed, and no console
error or warning was seen. Main-thread time per ten-render drag, in ms (medians, IQR of the task time):

| Host | Start, pinned, Chrome | n per side | Before: task (IQR) | After: task (IQR) | Before: style | After: style | Before: script | After: script |
|---|---|---|---|---|---|---|---|---|
| server | A2, pin=1, built-in | 20 / 20 | 69.2 (67.3-72.9) | 69.1 (61.1-73.6) | 19.6 | 17.6 | 10.1 | 9.7 |
| server | A2, pin=3, built-in | 20 / 20 | 67.8 (63.5-72.7) | 68.9 (63.8-75.2) | 18.2 | 16.3 | 9.4 | 10.1 |
| server | A2, pin=1, MudBlazor | 20 / 20 | 318.0 (299.1-341.3) | 320.2 (288.8-352.6) | 231.9 | 231.0 | 12.7 | 13.3 |
| server | B2, pin=0, built-in | 20 / 20 | 59.8 (53.5-62.4) | 48.6 (45.8-51.9) | 12.3 | 3.2 | 9.2 | 9.2 |
| wasm | A2, pin=1, built-in | 20 / 20 | 577.4 (538.6-766.0) | 578.2 (559.6-642.8) | 19.5 | 17.5 | 516.1 | 521.5 |
| wasm | A2, pin=3, built-in | 20 / 20 | 597.3 (566.1-645.8) | 574.7 (535.6-688.6) | 19.0 | 15.6 | 534.1 | 520.1 |
| wasm | A2, pin=1, MudBlazor | 20 / 20 | 948.4 (908.4-1064.0) | 1000.4 (918.1-1076.7) | 145.5 | 151.0 | 743.8 | 787.4 |
| wasm | B2, pin=0, built-in | 20 / 20 | 602.4 (541.9-700.3) | 584.3 (565.9-645.9) | 13.1 | 3.6 | 548.0 | 540.0 |

What this supports, on this machine: no comparison moved the task time by more than its own spread but
one. B2 with nothing pinned fell by about 11 ms on the Server host (59.8 to 48.6, the IQRs apart), nearly
all of it style recalculation (12.3 to 3.2 ms); on WebAssembly its style recalculation fell the same way
(13.1 to 3.6 ms), inside a task spread too wide to show it. Which part of the change that comes from —
the `::before` and its `clip-path` gone from the range, or ExSheet's overrides of them — was not
isolated. Where the pinned boundary is crossed, style recalculation fell by 1.9 to 3.4 ms under the
built-in Chrome, and the task medians on the Server host stayed within 1.1 ms. On WebAssembly, where the
C# render is in the script time and takes about 520 ms a drag under this machine's load, the medians
moved by −23 to +52 ms inside IQRs 80 to 230 ms wide. Under `ExSheet.MudBlazor`, whose style
recalculation dominates (about 230 ms a drag on the Server host, 150 ms on WebAssembly), nothing
attributable to the change was measured.

## Frame intervals while scrolling with column A selected, and the composited layers

`scroll-layers.mjs` opens `/sheet` headed, under xvfb, with the display scale on the command line as the
browser suite's `chrome-150` project sets it (`--force-device-scale-factor`, a 1280×800 window, the
viewport left to the window), at 100% and at 150%. It presses A1, then reads:

1. **the layers**, once the tree has stopped changing for ten frames: `LayerTree.layerTreeDidChange`'s
   list, each with `LayerTree.compositingReasons`;
2. **the frame intervals** from `requestAnimationFrame`, over 150 frames scrolling down 28 px a frame
   (a row) and over 150 frames scrolling across 64 px a frame;

then presses A1 again and Ctrl+Space, so column A is selected, and reads both again. `run-scroll.mjs`
does that on both builds, both hosts, both scales and both Chromes, two rounds each, the builds
interleaved; the table pools the two rounds (300 intervals each). Measured from 20:30 to 20:46 UTC, the
load average between 8.9 and 15.6. All 32 runs completed, no console error or warning was seen, and the
WebGL renderer every page was given was SwiftShader's (`ANGLE (Google, Vulkan 1.3.0 (SwiftShader Device
(Subzero) (0x0000C0DE)), SwiftShader driver)`). Intervals in ms:

| Host | Scale | Chrome | Build | Layers, A1 / column A | Column A, down: median, p95, max | Column A, across: median, p95, max | A1, down: median, p95 |
|---|---|---|---|---|---|---|---|
| server | 100% | built-in | before | 40 / 40 | 16.7, 16.8, 33.3 | 16.7, 16.7, 50 | 16.7, 16.8 |
| server | 100% | built-in | after | 40 / 43 | 16.7, 16.8, 50 | 16.7, 16.8, 116.7 | 16.7, 16.8 |
| server | 100% | MudBlazor | before | 44 / 44 | 16.7, 50.0, 83.4 | 16.7, 33.4, 83.4 | 16.7, 50.0 |
| server | 100% | MudBlazor | after | 44 / 47 | 16.7, 50.0, 116.7 | 16.7, 33.4, 133.4 | 16.7, 50.0 |
| server | 150% | built-in | before | 46 / 46 | 16.7, 50.0, 99.9 | 16.7, 33.3, 166.6 | 16.7, 33.4 |
| server | 150% | built-in | after | 46 / 49 | 16.7, 33.4, 83.3 | 16.7, 50.0, 216.6 | 16.7, 33.4 |
| server | 150% | MudBlazor | before | 47 / 47 | 16.7, 83.3, 783.3 | 16.7, 50.0, 250 | 16.7, 100.0 |
| server | 150% | MudBlazor | after | 47 / 50 | 16.7, 99.9, 233.3 | 16.7, 66.7, 233.3 | 16.7, 83.3 |
| wasm | 100% | built-in | before | 40 / 40 | 50.0, 100.0, 250 | 33.4, 83.3, 200 | 33.4, 116.6 |
| wasm | 100% | built-in | after | 40 / 43 | 50.0, 116.7, 166.7 | 33.4, 100.9, 300 | 50.0, 116.6 |
| wasm | 100% | MudBlazor | before | 44 / 44 | 83.3, 200.0, 333.3 | 50.1, 116.6, 250 | 50.0, 100.0 |
| wasm | 100% | MudBlazor | after | 44 / 47 | 66.7, 150.0, 316.6 | 50.0, 83.4, 116.6 | 50.1, 100.0 |
| wasm | 150% | built-in | before | 46 / 46 | 50.0, 83.3, 183.3 | 33.4, 50.1, 100 | 33.4, 66.7 |
| wasm | 150% | built-in | after | 46 / 49 | 49.9, 66.7, 133.4 | 33.3, 50.1, 66.7 | 33.4, 66.7 |
| wasm | 150% | MudBlazor | before | 47 / 47 | 66.7, 150.0, 300 | 50.0, 83.3, 116.7 | 50.1, 100.0 |
| wasm | 150% | MudBlazor | after | 47 / 50 | 66.7, 116.6, 216.7 | 50.0, 100.0, 133.3 | 50.0, 83.4 |

**The layers.** Chromium composites every sticky element here, and on `/sheet` that is every pinned
cell and Row Heading on screen (a `StickyPosition` layer each), the pinned selection layer's box and the
Focus (`StickyPosition` too), besides the scrollers, the scrollbars and the header. Selecting column A:

- **before** adds no layer: the column's box, its tint a `::before`, replaces the Focus's layer.
- **after** adds three, on every host, scale and Chrome:
  - the pinned shade layer, reported by the first element it paints, `div.ex-range-tint`, as
    `StickyPosition`;
  - a layer with no node and no compositing reason, a few pixels larger than the column's box (103×425
    beside a 99×420 tint at 100%; 153×635 beside 149×630 at 150%), which is the compositor's own;
  - a second layer of the Viewport's contents (`div.ex-viewport`, `OverflowScrolling`), the cells
    painted after the blend, which can no longer merge into the layer painted before it. At 150% the
    Sheet's scroller's own content layer stops drawing and that second one draws instead, so the count
    still rises by three.
  
  Which DOM element each layer is was read through `DOM.describeNode` on each layer's
  `backendNodeId`, in a separate pass of the same steps (not kept in `raw/`).

**What the ADR predicted, against this.** The sticky pinned cells are composited, so the blend above
them does read a backdrop the compositor assembles, and it costs the three layers above while a pinned
tint is on screen; with one cell selected, where no shade layer is emitted, it costs none (the same
count on both builds). In the frame
intervals no difference between the builds stands out from the spread: on the Server host the medians
are the 60 Hz frame (16.7 ms) before and after at both scales and under both Chromes, and on WebAssembly,
where each scroll renders the slice in C# on the main thread, the medians are equal or within a frame of
each other in every row, and the tails moved both ways. The sub-pixel gate, the check that a fractional
offset does not blur the lines under that compositing, passed (above). A GPU's compositor, and Windows',
were not measured, and would be the next reading if a run on one shows a pinned range scrolling worse
than an unpinned one.

## PF-6 and PF-7, before and after

The Definition of Done's observational measurements on `/wide` (10⁶ rows × 100 columns, 28 px rows, a
900×600 Viewport, A and B pinned), taken on 2026-10-12 from 02:54 to 03:31 UTC, after the branch was
rebased: **before** is `0186cc98` (main), **after** is `f7022450`, this branch with the review applied.
Bundled Chromium 141, headed under xvfb, the `chrome` project; each build's Release publish; the load
average (one minute) at each run's start is in the tables. All sixteen tests passed, and no console
message was recorded.

**PF-6**, in ms: the settle repaint (script, style and layout after a fling, median of 8, with its range)
and the frame intervals (median and p95 of 150 frames of ordinary scrolling, 30 of fling), with
horizontal virtualisation on and off. Both builds painted the same DOM in every run: 248 elements and
210 cells with it on, 2,228 and 2,100 with it off.

| Host | Build | Round | Load | Settle, on | Settle, off | Rows, on | Columns, on | Fling, on | Rows, off | Columns, off | Fling, off |
|---|---|---|---|---|---|---|---|---|---|---|---|
| wasm | before | 1 | 2.08 | 22.81 (17.32-34.28) | 76.76 (66.19-91.38) | 16.7, 50 | 16.7, 66.5 | 16.7, 16.8 | 33.3, 50.1 | 16.7, 33.4 | 16.7, 16.7 |
| wasm | after | 1 | 3.42 | 24.01 (17.32-39.02) | 77.97 (63.7-90.93) | 16.7, 16.8 | 16.7, 16.8 | 16.7, 16.7 | 16.7, 33.4 | 16.7, 16.8 | 16.7, 16.8 |
| wasm | before | 2 | 1.97 | 30.6 (20.63-44.24) | 74.07 (68.68-94.24) | 16.7, 16.7 | 16.7, 33.4 | 16.7, 16.8 | 33.3, 50.1 | 16.7, 33.4 | 16.7, 16.7 |
| wasm | after | 2 | 6.32 | 24.76 (17.96-40.53) | 72.33 (65.54-99.15) | 16.7, 16.7 | 16.7, 16.8 | 16.7, 16.8 | 16.7, 33.4 | 16.7, 16.7 | 16.7, 16.8 |
| server | before | 1 | 12.49 | 7.18 (6.4-9.64) | 52.79 (0.02-65.87) | 16.7, 33.3 | 16.7, 16.8 | 16.7, 16.8 | 33.3, 83.3 | 16.7, 50 | 16.7, 16.8 |
| server | after | 1 | 19.96 | 6.97 (0.01-8.92) | 58.72 (43.58-68.82) | 16.7, 33.4 | 16.7, 33.3 | 16.7, 16.8 | 16.7, 50 | 16.7, 33.4 | 16.7, 16.7 |
| server | before | 2 | 0.78 | 8.11 (5.88-12.39) | 59.32 (42.78-68.68) | 16.7, 16.8 | 16.7, 16.8 | 16.7, 16.8 | 16.7, 50 | 16.7, 16.8 | 16.7, 16.8 |
| server | after | 2 | 5.13 | 7.4 (5.8-11.47) | 45.76 (41.48-78.46) | 16.7, 16.8 | 16.7, 16.8 | 16.7, 16.7 | 16.7, 16.8 | 16.7, 16.7 | 16.7, 16.7 |

Against [ADR-0004](../../docs/adr/0004-cap-the-cells-touched-per-frame.md)'s table (WebAssembly,
headless Chromium on an M-series Mac, a Debug build): its 220 / 2,200 cells and 279 / 2,326 elements were
read at 100,000 rows on the markup of that day, against 210 / 2,100 and 248 / 2,228 here; the shape holds —
the settle repaint is where the setting shows, about three times dearer with it off on WebAssembly (22.8 to
30.6 ms against 72.3 to 78.0; ADR-0004's 16.3 against 49.8), and ordinary scrolling keeps the display's
frame either way. That frame is 16.7 ms on this 60 Hz display, where ADR-0004's 8.3 ms was a 120 Hz
display's. On the Server host the settle repaint is the browser's work alone, the render being the
host's: 7.0 to 8.1 ms on, 45.8 to 59.3 off. One settle each side read next to nothing (0.02 and 0.01 ms),
a fling whose last frame landed on a slice already painted.

Before against after: PF-6 selects nothing, so nothing this change paints differently is on screen, and
its counts are equal. The settle repaints and the fling and column frames moved both ways within their
ranges. One pattern is recorded rather than explained: with virtualisation off, the row-scrolling median
was 33.3 ms in three of the four `before` runs and 16.7 ms in all four `after` runs, with p95s that
overlap (50 to 83.3 before, 16.8 to 50 after). Nothing in the change applies with no selection, and the
load varied from 0.8 to 20 across the runs; a run on a quiet machine, or on real hardware, would say
whether it is more than that.

**PF-7**, in ms per step: a 10 × 7 rectangle in the scrollable columns, its bottom edge dragged one row a
step, forty steps, the main thread's script, style and layout time per step. Before, the range is one
element (its tint a `::before`); after, a tint and a box.

| Host | Build | Round | Load | Step: median | p95 | max |
|---|---|---|---|---|---|---|
| wasm | before | 1 | 2.08 | 7.67 | 21.04 | 22.06 |
| wasm | after | 1 | 3.42 | 7.78 | 20.14 | 25.7 |
| wasm | before | 2 | 1.97 | 7.87 | 17.22 | 21.57 |
| wasm | after | 2 | 6.32 | 6.97 | 22.67 | 22.76 |
| server | before | 1 | 12.49 | 1.19 | 2.43 | 3.88 |
| server | after | 1 | 19.96 | 1.27 | 3.57 | 3.94 |
| server | before | 2 | 0.78 | 1.15 | 1.99 | 2.68 |
| server | after | 2 | 5.13 | 1.27 | 1.96 | 2.07 |

Against [ADR-0008](../../docs/adr/0008-selection-is-painted-by-an-overlay.md)'s pair (real hardware,
Windows, a render's maximum: 2.10 ms for the overlay, 19.70 ms for a class on every cell): on the Server
host, where a step's main-thread work is the browser's alone, the maxima are 2.07 to 3.94 ms after, 2.68
to 3.88 before, the overlay's order and far from the per-cell classes'. On WebAssembly a step's script
time holds the .NET render too, on the same thread, so its 7 to 8 ms medians and 22 to 26 ms maxima are
not the overlay's paint alone, and they sit within each other's spread before and after. The medians
moved by +0.08 to +0.12 ms on the Server host, the second element per range, and by −0.9 to +0.1 ms on
WebAssembly.

## The sub-pixel gate, and the rest of layer 3, as run here

The layer-3 runs that verified ADR-0206 in this container (Playwright's bundled Chromium, headed under
xvfb, the `chrome` project and the `chrome-150` project, both on that Chromium) are in the branch's
report rather than here, since a bundled-Chromium run is not a Step 4 record. The gate's own result:
passed at 100% (the `chrome` project) and at 150% (`chrome-150`, `--force-device-scale-factor=1.5`),
on the WebAssembly host and on the Server host, three times each (`--repeat-each=3`), with A2:A14 of
`/sheet?case=lines&pin=2` selected beside the lines (a pinned tint on screen, no outline on the lines),
the page shifted 0.33 px across and down, and Thin, Thick, Double, Dotted and MediumDashDotDot read on
B's bottoms (a Pinned Column) and D's rights (a scrollable one) at their exact Device Pixels.

Run again on 2026-10-12 with the review applied (`f7022450`), with the whole of `part-c.spec.mjs`, both
projects, three times each, on each host's Release publish: passed. On the way, the Server host failed
the gate 3 runs of 6, and not on a line: its Ctrl+press on F3 took F4. The press read F3's place while
the last Shift+↓ answers were still scrolling the Sheet to keep A14 in view. The test now waits for
A2:A14 to be painted, reading the range and its cells in one evaluation, and for the circuit to fall
quiet, before it reads F3's place (principle 6).

**Where the gate has not run.** Only under software compositing: SwiftShader here, and CI's Linux
Chrome and Edge, which have no GPU either. The risk the ADR names is an offscreen backdrop at a
fractional offset under a GPU's compositor, which is where Windows users are. So the gate is owed a run
by hand on the next Windows run, at 100% and at 150% (`part-c.spec.mjs`, the test named "the sub-pixel
gate", on both hosts), before DC-59 is signed off.

Before that run counted, the tests were checked against the base build: on `e3ad9a50`, under
`ExSheet.MudBlazor`, the black line between C2 and C3 of B2:C3 read (16, 13, 41) both with B2:C3 wholly
pinned and with the boundary through it — the colour ADR-0206 predicted, and its scrollable side too —
where the change reads (0, 0, 0) in both, as unpinned.
