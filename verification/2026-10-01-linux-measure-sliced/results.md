# PV-40, observed: the work after an answer, sliced *(2026-10-01, the pages at `6ce3255`)*

Observational: recorded, never gated (Definition of Done §1). ADR-0066 settled the cap on leaves
at 200,000, on the condition that the work after a question's pass is sliced as the pass is. PV-40
asks for that, and for the longest task near the cap to be observed again. The first record,
`verification/2026-10-01-linux-measure`, found 1.9 s of one task after the source's last slice.
This record is ticket 22's.

It holds the cap test of `measure-pivot.spec.mjs` and the test of the questions below the cap. Each
was run on the build after the change and, for comparison, on the integration branch before it,
back to back, on the same machine. `metrics.json` holds every number: the browser's under
`chromium-local`, by build, as the fixtures' `record()` wrote them, with the leaves each layout
makes; CoreCLR's under `coreclr`; and the machine under `machine`.

**Every number is this machine's**, and it is not the first record's machine. This one is a 4-vCPU
guest at 2.1 GHz, where the first was at 2.8 GHz. Two other agents were building in their own
worktrees while it ran, so the numbers are noisier than the first record's. That is why the build
before the change was measured again here, rather than compared with the first record's numbers.

## The machine

| | |
|---|---|
| CPU | Intel(R) Xeon(R) Processor @ 2.10GHz (family 6, model 207, stepping 2), 4 vCPUs, a KVM guest |
| Memory | 15 GiB |
| OS | Ubuntu 24.04.4 LTS, Linux 6.18.44 |
| .NET | SDK 10.0.401; runtime 10.0.12 (CoreCLR); browser-wasm runtime pack 10.0.12 |
| Browser | Chromium 141.0.7390.37 (Playwright's build 1194), headed under Xvfb at 2560×1600×24; Playwright 1.62.1, Node 22.22.2 |
| Load | Two other agents building in their own worktrees meanwhile |

## How

**The builds.** Both were published with `dotnet publish samples/ExGrid.DemoHost -c Release`,
trimmed, with no `wasm-tools` workload and so no AOT, as PV-21 asks.

- **Before**: the integration branch at `d596209`, extracted with `git archive`.
- **After**: this branch at `6ce3255`. The commits after it change documentation only.

Each build's `wwwroot` was served as published, uncompressed, on port 5599, by a small static
server. It answers a page route with `index.html`, and serves `.wasm` as `application/wasm`.

**The runs.** `tests/ExGrid.Browser/measure-pivot.spec.mjs` ran with `EXGRID_MEASURE=pivot`,
through a config outside the repository that runs the suite's own config against
`/opt/pw-browsers/chromium`. No Chrome or Edge is installed here. Each run was under `xvfb-run`,
holding the layer-3 lock.

- **Three tests:** `--grep "near the 200,000-leaf cap"`, then `--grep "questions of a thousand to
  thirty thousand leaves"`, then `--grep "and the gestures over them"`.
- **Order:** each test on the build before, then on the build after.
- **Data:** `/pivot?trades=1000000`. The demo API server, which the suite's config starts, held
  20,000 trades. These tests do not call it.

**The probe is the first record's.** The time runs from the input — Update, Defer Layout Update
holding the layout built in the pane — to the frame after the report's row count changed, all in
the page's clock. "Longest task" is the longest long task (over 50 ms) between the two, and 0 when
there was none.

Each layout was asked four times on one load. The first time is kept apart, and the figures are the
median of the next three, with the best and the worst.

## PV-40: the longest task, by leaves

| Layout | Leaves | Report rows | Answer, before | Answer, after | Longest task, before | Longest task, after |
|---|---|---|---|---|---|---|
| Rows TradeDate; Columns Product | 1,350 | 271 | 256 ms (232–273) | 226 ms (196–239) | 77 ms (66–95) | **0** (0–0) |
| Rows TradeDate, Book; Columns Product | 13,500 | 2,971 | 375 ms (299–411) | 384 ms (317–473) | 133 ms (127–133) | **0** (0–94) |
| Rows TradeDate, Book, Confirmed; Columns Product | 26,982 | 8,371 | 619 ms (515–849) | 632 ms (539–779) | 241 ms (232–433) | **0** (0–172) |
| Rows TradeDate, Quantity; Columns Product | 66,150 | 13,501 | 984 ms (925–1,035) | 1,048 ms (972–1,309) | 522 ms (512–532) | **107 ms** (0–146) |
| Rows TradeDate, Notional | 134,646 | 134,917 | 1,949 ms (1,926–2,001) | 2,333 ms (2,170–2,718) | 1,304 ms (1,302–1,398) | **136 ms** (114–194) |
| Rows TradeDate, Quantity; Columns Product, Region | 197,151 | 13,501 | 2,590 ms (1,983–2,638) | 2,791 ms (2,693–2,907) | 1,652 ms (1,488–1,798) | **137 ms** (117–172) |
| Rows TradeDate, Quantity; Columns Product, Currency | 314,758 | refused | 503 ms (464–531) | 519 ms (516–521) | 134 ms (129–144) | 132 ms (117–145) |

The first time of each, after the load:

| Leaves | Answer, before | Answer, after | Longest task, before | Longest task, after |
|---|---|---|---|---|
| 1,350 | 360 ms | 309 ms | 146 ms | 0 |
| 13,500 | 416 ms | 362 ms | 172 ms | 0 |
| 26,982 | 641 ms | 720 ms | 368 ms | 154 ms |
| 66,150 | 1,187 ms | 1,347 ms | 645 ms | 103 ms |
| 134,646 | 1,999 ms | 2,498 ms | 1,521 ms | 119 ms |
| 197,151 | 2,061 ms | 2,658 ms | 1,462 ms | 172 ms |
| 314,758, refused | 491 ms | 556 ms | 138 ms | 150 ms |

The first visual answer was 38–44 ms in the median of every layout, before and after. The worst
single one was 177 ms before and 173 ms after.

What the rows say:

- **The page is no longer held for the work after an answer.** Near the cap, the longest task fell
  from 1.65 s to 137 ms (medians). At 134,646 leaves it fell from 1.30 s to 136 ms, and at 66,150
  from 522 ms to 107 ms. Below 30,000 leaves, the median run had no long task at all.
- **The tasks that remain are not the work after the answer.** They are traced below. Most are the
  runtime's garbage collections, which no slicing can cut.
- **Near the cap, an answer takes a little longer.** At 197,151 leaves it takes 8% longer (2.59 s →
  2.79 s), at 134,646 leaves 20% longer, and at 66,150 leaves 6% longer. That is the cost of
  yielding:
  - each yield is a `setTimeout` round, which browsers clamp to 4 ms once timeouts nest;
  - the page paints between slices.
- **Below 30,000 leaves, the answer times are unchanged within the noise.** A quick layout yields
  nothing, and a medium one yields little.
- **The refused layout is unchanged.** The source stops at the leaf that passes the cap, and
  nothing is built after it. Its 132 ms task was there before. It was not traced.

## The gestures laid out from the answer held

The ticket asked that these stay as quick as they were, and that no yield be added where none is
needed. The first test of `measure-pivot.spec.mjs` was run on both builds, in the same way, after the
others (`--grep "and the gestures over them"`): /pivot's 50-leaf report, five rounds of each gesture.

| Gesture | Answer, before | Answer, after | Longest task, before / after |
|---|---|---|---|
| A field's menu opened | 9.6 ms (8.8–31.4) | 11.1 ms (9.5–36.5) | 0 / 0 |
| Region sorted Z to A | 23.3 ms (19.9–40.8) | 34.8 ms (20.9–46.7) | 0 / 0 |
| … and A to Z | 30.8 ms (19.0–34.0) | 27.2 ms (20.5–33.2) | 0 / 0 |
| The first outer Item collapsed | 23.5 ms (18.4–44.5) | 24.7 ms (18.8–41.5) | 0 / 0 |
| … and expanded | 33.0 ms (20.5–34.7) | 31.2 ms (24.1–39.8) | 0 / 0 |
| The Layout menu opened | 10.0 ms (8.5–12.2) | 10.4 ms (9.2–19.8) | 0 / 0 |
| Show in Tabular Form | 25.2 ms (19.1–33.7) | 24.8 ms (21.0–35.5) | 0 / 0 |
| Show in Compact Form | 22.4 ms (18.8–29.7) | 22.5 ms (19.8–44.9) | 0 / 0 |
| Book ticked into Rows: a new question | 171 ms (160–290) | 199 ms (156–386) | 0 / 51 (worst 139 / 152) |
| Book unticked: a new question | 181 ms (143–225) | 180 ms (148–249) | 0 / 0 (worst 121 / 125) |
| Filtered to USD: a new question | 136 ms (124–149) | 154 ms (151–165) | 0 / 0 |
| Back to (All): a new question | 155 ms (146–212) | 175 ms (150–197) | 0 / 0 |

- **A gesture laid out from the answer held is as quick as before**, within the noise. Its first
  visual answer is still the answer itself: it is laid out and painted in the input's own turn, and
  no yield was added.
- **A new question took 0–28 ms more in its median.** Each of these answers has 50 leaves or
  fewer, because a book belongs to one region and one desk. Work after the pass of that size never
  reads the clock and adds no yield, so the difference is not the slicing's.
- **The build after was measured second, on a busier machine.** In the same run, the page's read of
  a million objects into a Snapshot, which this change does not touch, took 16% longer: 3,721 ms
  against 3,207 ms.

## The long tasks left, traced

Two ways of tracing were combined.

- **A diagnostic build.** A throwaway copy of the build after, never committed, logged every slice
  that ran over 40 ms when it ended, with the runtime's collections during it. The same was done for
  ExPivot's own steps and for putting the report on screen.
- **A script.** It asked the near-cap layout twice on `/pivot?trades=1000000`, and set the browser's
  long tasks beside the log.

| Long task | What ran in it |
|---|---|
| 103 ms | A 30 ms slice of the source's pass. A full collection (gen 2) ran in it, and the slice took 102 ms |
| 108 ms | A slice of the cube, made from the answer. A full collection ran in it, and the slice took 107 ms |
| 72 ms | The report put on screen. Presenting it took 54 ms, which includes the first render of the grid with the new report |
| 105 ms (second question) | A slice of the source's question, with a full collection in it: 104 ms |

- **One other slice ran over 40 ms.** It took 48 ms, with no collection in it, and made no long
  task. It was a slice of the source's question, and whether it fell in the pass or in the answer's
  assembly was not told apart.
- **The report's layout logged no slice over 40 ms**, and the browser saw no long task in
  ExPivot's other steps but the one that put the report on screen.
- **The collections are the runtime's.** Mono's collector stops the world. It ran one or two full
  collections in each question near the cap. The build before had the same collections, inside its
  one long task.
- **In the second question, putting the report on screen took 30 ms**, and made no long task.

## On CoreCLR

The synchronous forms — `PivotEngine.Cube` and `Report`, and the bundled source's answer when its
budget is never spent — now run the steps of the sliced forms, unsliced. They were measured with the
explicit tests of `tests/ExPivot.Engine.Tests/Measurements.cs`, in Release: the build before and the
build after in turn, twice over.

| A million trades | Before (the two runs' medians) | After |
|---|---|---|
| A question, 2,976 leaves | 20.3, 27.7 ms | 24.4, 21.9 ms |
| A question, 26,784 leaves | 46.2, 45.3 ms | 39.2, 42.4 ms |
| A question, 199,511 leaves | 133.7, 133.6 ms | 124.7, 113.3 ms |
| The same, keyed (rows kept) | 123.4, 116.6 ms | 114.0, 123.8 ms |
| The cube of 2,976 leaves | 2.3, 2.3 ms | 2.4, 2.4 ms |
| The cube of 199,511 leaves | 112.9, 100.1 ms | 109.8, 116.7 ms |
| A layout of the answer held: as asked, a collapse, a sort, a sort by value, the Tabular form | 1.0–2.1 ms | 1.0–2.4 ms |

**The synchronous forms cost what they cost before**, within run-to-run noise.

## What could not be measured as asked, and the nearest thing measured

- **Chrome and Edge are not installed here.** Playwright's Chromium 141 build stood in for both. This
  machine is Linux.
- **The machine is busier and different from the first record's.** So the before and after were
  each measured here, back to back, rather than compared with the first record's numbers. For what
  it is worth, the first record's near-cap figures — 2.7 s, and a 1.9 s task — are close to this
  run's before.
- **The long tasks were traced on a diagnostic build, not on the build measured.** It is the build
  after, plus a line of logging for each slice that ran over 40 ms.

## Console

- **Every test of both runs passed.** The fixtures fail a test on any console error, uncaught page
  error, warning from ExGrid's own code, or unhandled exception (CON-1, CON-2, CON-3, CON-6).
- **The diagnostic run printed every console warning and error, and there were none.**
- **The runs' own `console.json` was not kept.** It is where a third party's warning would have been
  listed.

## The commands

```sh
# The builds: the branch before, extracted, and this one
git archive --format=tar -o base.tar d596209 && tar -xf base.tar -C base-src
(cd base-src && dotnet publish samples/ExGrid.DemoHost -c Release -o <scratch>/publish-before)
dotnet publish samples/ExGrid.DemoHost -c Release -o <scratch>/publish-after

# Each build served in turn, as published, and measured, holding the layer-3 lock
node serve.mjs <scratch>/publish-<build>/wwwroot 5599
cd tests/ExGrid.Browser
EXGRID_BASE_URL=http://localhost:5599 EXGRID_MEASURE=pivot \
  xvfb-run -a --server-args="-screen 0 2560x1600x24" \
  npx playwright test -c <scratch>/playwright.local.config.mjs measure-pivot.spec.mjs --grep "near the 200,000-leaf cap"
#   and --grep "questions of a thousand to thirty thousand leaves"

# CoreCLR, both builds in Release, in turn, twice
dotnet tests/ExPivot.Engine.Tests/bin/Release/net10.0/ExPivot.Engine.Tests.dll -explicit only -showLiveOutput \
  -method ExPivot.Engine.Tests.Measurements.Laying_out_the_answer_held \
  -method ExPivot.Engine.Tests.Measurements.Asking_a_million_records
```
