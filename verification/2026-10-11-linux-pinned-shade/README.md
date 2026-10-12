# The pinned shade's cost — 2026-10-11, Linux container

**Scope: [ADR-0206](../../docs/adr/0206-a-ranges-tint-is-an-element-of-its-own-and-on-the-paper-the-pinned-tints-multiply-as-one-layer.md),
"What it costs, and what is checked".** A range is now a tint and a box in each layer it reaches, the
pinned tints and bands lie in a sticky shade layer of their own, and on ExSheet's Paper that layer
multiplies as one group, as each scrollable tint and band does. The ADR records the compositing cost as
a prediction and asks for three observations before it counts as done: the drag cost before and after,
the frame intervals while scrolling with column A selected at 100% and 150%, and the composited layer
count read through the Chrome DevTools Protocol. They are here, with every raw figure in [`raw/`](raw/)
(`scroll-layers.json` gzipped, read with `zcat`); [`tables.py`](tables.py) prints every table below from them.

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

## The sub-pixel gate, and the rest of layer 3, as run here

The layer-3 runs that verified ADR-0206 in this container (Playwright's bundled Chromium, headed under
xvfb, the `chrome` project and the `chrome-150` project, both on that Chromium) are in the branch's
report rather than here, since a bundled-Chromium run is not a Step 4 record. The gate's own result:
passed at 100% (the `chrome` project) and at 150% (`chrome-150`, `--force-device-scale-factor=1.5`),
on the WebAssembly host and on the Server host, three times each (`--repeat-each=3`), with A2:A14 of
`/sheet?case=lines&pin=2` selected beside the lines (a pinned tint on screen, no outline on the lines),
the page shifted 0.33 px across and down, and Thin, Thick, Double, Dotted and MediumDashDotDot read on
B's bottoms (a Pinned Column) and D's rights (a scrollable one) at their exact Device Pixels.

**Where the gate has not run.** Only under software compositing: SwiftShader here, and CI's Linux
Chrome and Edge, which have no GPU either. The risk the ADR names is an offscreen backdrop at a
fractional offset under a GPU's compositor, which is where Windows users are. So the gate is owed a run
by hand on the next Windows run, at 100% and at 150% (`part-c.spec.mjs`, the test named "the sub-pixel
gate", on both hosts), before DC-59 is signed off.

Before that run counted, the tests were checked against the base build: on `e3ad9a50`, under
`ExSheet.MudBlazor`, the black line between C2 and C3 of B2:C3 read (16, 13, 41) both with B2:C3 wholly
pinned and with the boundary through it — the colour ADR-0206 predicted, and its scrollable side too —
where the change reads (0, 0, 0) in both, as unpinned.
