# PV-21 and DA-17, measured *(2026-10-01, the pages at `b78b348`)*

Observational: recorded, never gated (Definition of Done §1). PV-21 holds ExPivot to the targets the
user set as "snappy" (Q52), in the browser over a million records, and is where the default cap on
leaves (200,000, provisional) is to be settled from a measurement. This record measures it; the
setting is the user's. DA-17 records a million records built from objects, read from a CSV and read
from Arrow, on CoreCLR and in the browser. This is the record that ExPivot's ticket 08 and
ExGrid.Data's ticket 06 ask for.

`metrics.json` holds every number. The browser's are under `chromium-local`, as the fixtures'
`record()` wrote them. CoreCLR's are under `coreclr`, and the machine is under `machine`.

**Every number is this machine's**: a 4-vCPU KVM guest at 2.8 GHz. A desktop is faster, and no
number here gates anything.

## The machine

| | |
|---|---|
| CPU | Intel(R) Xeon(R) Processor @ 2.80GHz (family 6, model 85, stepping 7), 4 vCPUs, a KVM guest |
| Memory | 15.7 GiB |
| OS | Ubuntu 24.04.4 LTS, Linux 6.18.44 |
| .NET | SDK 10.0.401; runtime 10.0.12 (CoreCLR, workstation GC); browser-wasm runtime pack 10.0.12 |
| Browser | Chromium 141.0.7390.37 (Playwright's build 1194), headed under Xvfb at 2560×1600×24; Playwright 1.62.1, Node 22.22.2 |

## How

**In the browser.** `samples/ExGrid.DemoHost` was published with `dotnet publish -c Release`. The
build is trimmed and has no `wasm-tools` workload, so there is no AOT: the Mono interpreter with its
jiterpreter, as PV-21 asks. Its `wwwroot` was served as published, uncompressed, on port 5699 by a
small static server that answers a page route with `index.html` and serves `.wasm` as
`application/wasm`. The demo API server, a Release build, ran at 8699 with
`EXGRID_DEMO_TRADES=1000000`; it generated the trades in 5.0 s into a data directory of this run's
own.

`tests/ExGrid.Browser/measure-pivot.spec.mjs` drove the pages with `EXGRID_MEASURE=pivot`. It ran
through a config outside the repository, which runs the suite's own config against
`/opt/pw-browsers/chromium`, because no Chrome or Edge is installed here. It ran under `xvfb-run`,
holding the layer-3 lock, one test at a time, each test on a document of its own. Nothing else ran
on the machine meanwhile.

| Page | What it measured |
|---|---|
| `/pivot?trades=1000000` | The trades as objects, read into a Snapshot by `PivotSource.From`; the gestures; the questions by their leaves |
| `/pivot-live?trades=1000000&batch=1000` | The page's own Change Batches of 1,000 amended trades |
| `/pivot-db` | The API server's trades as an Arrow stream, read into a Snapshot in the page |
| `/pivot-csv` | The trade export of a million trades (89.7 MiB, written by the page's own `DemoCsv.TradeExportFileAsync`), chosen through `setInputFiles`; and the page's 100,000-trade sample |

**The clock is the page's own** (`performance.now()`), so the runner's latency is in none of the
numbers. An init script, installed before the app's first script, holds a capture-phase listener
for the input, a MutationObserver over the document, and a `PerformanceObserver` for long tasks.

- **The input** is the first `pointerdown`, `mousedown`, `keydown`, `click`, `input` or `change` after
  a gesture is armed, taken at its own `timeStamp`. The target is hovered first and left to rest for
  300 ms, so that what the pointer's arrival paints is not taken for the answer.
- **The first visual answer** is the first animation frame after the DOM's first change after the
  input.
- **The answer** is the first animation frame after the DOM showed the answer: the report's text
  changed, its row count changed, a menu or a dialog opened, or a status line said that a read was
  done.
- **Blocked** is the longest long task (over 50 ms) between the input and the answer's frame. The
  page settles before the next gesture: no long task for half a second.

Each gesture ran 5 times. Each question by its leaves ran 4 times on one load, and the first time is
kept apart. There were 12 Change Batches. The Arrow stream was read once at load and 5 more times. The
CSV was read 3 times, on a fresh load each. A number below is the median, and a range is the best and
the worst.

**On CoreCLR.** The explicit tests were built in Release and run as `Measurements.cs` says
(`-explicit only -showLiveOutput`), three times each:

- `tests/ExPivot.Engine.Tests/Measurements.cs`, the engine. It gained the layout of a held answer
  and the cube an answer makes.
- `tests/ExGrid.Data.Tests/MeasureTests.cs`, new: objects and CSV.
- `tests/ExGrid.Data.Arrow.Tests/MeasureTests.cs`: Arrow.

Each test warms up and repeats within a run. A number below is the median of the three runs' medians.

## PV-21 against its targets

| Target | In the browser | On CoreCLR (target: about a tenth) |
|---|---|---|
| **The first visual answer to any gesture ≤ 0.1 s** | 10–44 ms, the medians of 13 gestures; 38–42 ms, the medians of the Updates that asked the questions by their leaves. The worst single one was 163 ms: Book ticked, in the first round | No screen |
| **Collapse, sort or a change of form ≤ 0.1 s** | 28–32 ms; the worst 77 ms (table below) | `PivotEngine.Report` from the answer held: 0.9–2.4 ms, at 2,976 leaves and at 199,511 |
| **A change that needs a new question ≤ 0.3 s** | 159–229 ms for the 50-leaf report /pivot opens with; the worst 333 ms. The first question as the page loads: 556 ms. A larger report: 0.25 s at 1,350 leaves and 2.7 s at 197,151 (the cap, below) | 25.5 ms at 2,976 leaves (keyed, as /pivot is), 45.1 ms at 26,784, and 140.5 ms at 199,511, plus 119 ms to make its cube |
| **1,000 changes ≤ 0.2 s** | 43 ms (35–57), from `Apply` to the frame that shows them. 53–165 ms from the start of the batch's task, when it ran long enough to be a long task | 6.3 ms: the batch 0.4, apply and fold 5.4, the next answer 0.5 (exact Sums). 14.8 ms with a `double`'s Sum or a Max. 31 ms at 199,511 leaves, where the next answer takes 26.0 |
| **A CSV of a million rows read ≤ 4 s** | 14.6 s (14.4–14.7) to read; the first report 15.2 s after the file was chosen | 955 ms from memory, 952 ms from a file |
| **The page blocked ≤ 50 ms at a time** | The gestures: the median 0–51 ms, the worst 162 ms. The CSV read: 131 ms. The Arrow read: 196 ms, and 454 ms at the first read. A Change Batch: up to 164 ms. The first question at load: 204 ms. A question of 1,350 leaves: 74 ms, rising to 1.9 s near the cap. Loading `/pivot?trades=1000000`: 7.6 s in one task | No screen |

The gestures, over a million trades on `/pivot`:

| Gesture | First visual answer | Answer | Blocked |
|---|---|---|---|
| A field's menu opened | 11 ms (10–35) | the same | 0 |
| Region sorted Z to A, from its menu | 31 ms (19–77) | the same | 0 (worst 76) |
| … and A to Z | 28 ms (22–52) | the same | 0 (worst 54) |
| The first outer Item collapsed | 28 ms (23–53) | the same | 0 (worst 52) |
| … and expanded | 31 ms (25–70) | the same | 0 (worst 69) |
| The Layout menu opened | 12 ms (9–18) | the same | 0 |
| Show in Tabular Form | 32 ms (23–52) | the same | 0 (worst 51) |
| Show in Compact Form | 31 ms (25–32) | the same | 0 |
| Book ticked into Rows: a new question | 44 ms (40–163) | 229 ms (188–333) | 51 (worst 162) |
| Book unticked: a new question | 40 ms (38–45) | 184 ms (171–303) | 0 (worst 122) |
| The report filter band opened | 10 ms (10–56) | the same | 0 (worst 55) |
| Filtered to USD: a new question | 42 ms (40–46) | 159 ms (144–181) | 0 |
| Back to (All): a new question | 43 ms (40–48) | 192 ms (163–260) | 0 |

What the rows say:

- **Every gesture laid out from the answer held meets 0.1 s, its worst included.** Its first visual
  answer is the answer itself, because the report is laid out in the input's own task.
- **A new question meets 0.3 s in its median for the 50-leaf report, and its worst does not**
  (333 ms). While the question is out, the page answers first (PV-25): 40–44 ms after a gesture in
  the pane or the band, and 38–42 ms after an Update (medians).
- **1,000 changes meet 0.2 s four times over.** On WebAssembly, the redraw runs inside `Apply` when
  it is due: in all 12 batches, the report changed in the batch's own frame. The time is taken from
  the page's clock (below).
- **The CSV misses 4 s by 3.7 times, and CoreCLR misses its tenth by 2.4 times.** The time goes on
  reading, not on getting the file. The page's 100,000-trade sample, read from a `MemoryStream`,
  takes 1.7 s (1.5–1.7): a tenth of the rows in a little more than a tenth of the time.
- **50 ms at a time holds for the gestures' medians, and not beyond them.** The slices of a question
  and of a load are 30 ms, and they stay within it. These do not:
  - the synchronous read of objects that `/pivot` does;
  - some part of each load, which was not traced further: 131 ms in the CSV read, and 196 ms in the
    Arrow read;
  - the first run of a code path, as the first questions show against the ones after them;
  - after a question's last slice, the answer assembled and the cube made, which grow with the leaves.

## DA-17

| A million records | On CoreCLR | In the browser |
|---|---|---|
| **Built from objects** | 426 ms: 12 columns keyed by a unique text Id (`Build`; `BuildAsync` in 30 ms slices takes 445 ms); 372 ms unkeyed. The engine's 13 declared columns: 242 ms keyed by a `long`, 180 ms unkeyed | 3,803 ms: /pivot's declarations, 11 columns keyed by the text Id, in one synchronous `PivotSource.From` |
| **Read from a CSV** | 955 ms from memory (89.7 MiB, 12 columns, keyed by Id); 952 ms from a file in the page cache | 14.6 s through `InputFile` (the same 89.7 MiB). 100,000 rows from memory: 1.7 s |
| **Read from Arrow** | 475 ms from memory and 511 ms from a stream (11 columns keyed by TradeId, 75.7 MiB). ADR-0064's shape without the unique id: 77 ms | 3,693 ms (3,495–3,769), from the response's last byte to the Snapshot (12 columns keyed by TradeId, 81.3 MB). The page's own figure, with the request (435 ms), is 4,070 ms. The first read at load took 4,128 ms |

The browser takes 8–15 times CoreCLR's time: 8.9× for objects, 15× for the CSV, and 7.8× for Arrow.
On CoreCLR, the unique text column is most of what Arrow costs: without it, the stream is read in
77 ms rather than 475. The browser was not measured without it.

The server's side of the Arrow stream, from its log: reading the SQLite trades into a Snapshot took
5,420 ms, and writing the stream took 405 ms. It did this once for the version. Every later request
was served the bytes it kept, compressed with Brotli at its fastest level.

## The cap on leaves

`/pivot` over a million trades. Each layout was built in the pane while Defer Layout Update held it,
then asked once, by Update. The leaves are the combinations that hold trades, counted on CoreCLR over
the same trades (`DemoPivotData.Trades(count: 1,000,000)`).

| Layout | Leaves | Report rows | Update to the report: first / then (range) | Blocked: first / then |
|---|---|---|---|---|
| Rows TradeDate; Columns Product | 1,350 | 271 | 338 ms / 247 ms (233–262) | 166 / 74 ms |
| Rows TradeDate, Book; Columns Product | 13,500 | 2,971 | 365 ms / 362 ms (348–363) | 163 / 158 ms |
| Rows TradeDate, Book, Confirmed; Columns Product | 26,982 | 8,371 | 839 ms / 646 ms (640–692) | 492 / 311 ms |
| Rows TradeDate, Quantity; Columns Product | 66,150 | 13,501 | 1,516 ms / 1,163 ms (1,030–1,186) | 879 / 621 ms |
| Rows TradeDate, Notional | 134,646 | 134,917 | 2,505 ms / 2,546 ms (2,438–2,627) | 1,799 / 1,832 ms |
| Rows TradeDate, Quantity; Columns Product, Region | 197,151 | 13,501 | 2,752 ms / 2,704 ms (2,566–2,708) | 1,996 / 1,889 ms |
| Rows TradeDate, Quantity; Columns Product, Currency | 314,758 | refused: "This layout needs more than 200,000 cells." | 536 ms / 533 ms (498–552), to the refusal | 149 / 137 ms |

"First" is the first question of its kind since the load. "Then" is the median of the next three.
The first visual answer was 37–47 ms in every case but one, which took 137 ms. An earlier run asked
each of the last four on a fresh load. It found 1,352, 2,828, 3,002 and 625 ms, blocked for 721,
2,106, 2,160 and 151 ms; it is in `metrics.json`. On CoreCLR, a question of 199,511 leaves takes
140–164 ms, and making its cube takes 119 ms more.

What these numbers suggest. The decision is the user's, and it belongs in ADR-0065:

- **A question costs more with every leaf.** On this machine it costs about 0.25 s at 1,350 leaves,
  0.36 s at 13,500, 0.65 s at 26,982, 1.2 s at 66,150, and 2.5–2.7 s from 134,646 to 197,151. The
  0.3 s target is passed somewhere between 1,350 and 13,500 leaves. At the default of 200,000, a
  question takes nine times the target.
- **Most of that time is one task, and it holds the page.** The source's pass is sliced and yields
  every 30 ms. What follows its last slice is not sliced: the answer is assembled, its cube is made,
  and the report is laid out. That one task took 74 ms at 1,350 leaves, 0.3 s at 26,982, 0.6 s at
  66,150, and 1.8–1.9 s near the cap. On CoreCLR, the cube alone is 119 ms of the roughly 260 ms a
  question near the cap costs. So the cap keeps a question finite, and it refuses a larger one in
  half a second, because the source stops at the leaf that passes it. It does not keep the page
  responsive below it.
- **On CoreCLR, the same question near the cap costs about 0.26 s**, near the browser's 0.3 s
  target. That is roughly what a Blazor Server host pays, since ExPivot runs on the server there. A
  WebAssembly page that asks a server through `PivotSource.Fetch` is spared the pass, but it still
  makes the cube from the answer in the browser.
- **The ways forward**, none of them taken here:
  - a lower default in the browser;
  - slicing ExPivot's work after the answer as the pass is sliced, so that a large question is slow
    but never freezes the page;
  - a default per host. ADR-0065 already says that "the right cap depends on the device, and on where
    the aggregation runs".

## What could not be measured as asked, and the nearest thing measured

- **Chrome and Edge are not installed here.** Playwright's Chromium 141 build stood in for both. This
  machine is Linux; Windows and macOS were not measured.
- **The first visual answer and the page blocked have no CoreCLR counterpart**, because CoreCLR has no
  screen. The CoreCLR column compares the engine's own operations with a tenth of each target.
- **Objects in the browser were read by the synchronous `PivotSource.From` that `/pivot` runs.**
  `FromAsync` reads in slices so that the page keeps painting, but no page uses it, so it was not
  measured in the browser. On CoreCLR, its counterpart `BuildAsync` costs 4% more than `Build`: 445 ms
  against 426. The number is the page's own clock around `From`. Making the million trade objects is
  the demo's own work, and it is not counted. The page's first task is 7.6 s: the app's start, the
  trades made, the read, and the first render.
- **The 1,000 changes start at no input.** A batch is the page's own timer's. The batch's start was
  therefore taken from the page's clock: the line "applied in N ms", which the page writes as `Apply`
  returns, gives N, and N is subtracted from the moment that line reached the DOM. The answer is the
  frame after that. Where the batch's task ran long enough to be a long task, the time from that
  task's start is given as well: 53–165 ms, against 35–57 ms. That task also includes amending the
  trades and writing the line.
- **The Arrow read in the browser includes more than reading.** It runs from the response's last byte
  (Resource Timing's `responseEnd`) to the status line. So it includes copying the body from
  JavaScript into .NET, and `PivotSource.From`, which only binds the fields. The request is reported
  apart from it.
- **The CSV in the browser came through `InputFile`'s stream**, not from memory. The page's
  100,000-trade sample, read from a `MemoryStream`, is what shows that the time goes on reading.
- **The first question at load (556 ms, blocked 204 ms) includes code running for the first time.**
  The first run of each question by its leaves is kept apart for the same reason.

## Console

The browser's console record was empty for every test of the run: no error, no warning and no page
error. The run's own `console.json` held nothing, and it is not kept.

## The commands

```sh
# CoreCLR, three times each
dotnet build tests/ExPivot.Engine.Tests -c Release   # and tests/ExGrid.Data.Tests, tests/ExGrid.Data.Arrow.Tests
dotnet tests/ExPivot.Engine.Tests/bin/Release/net10.0/ExPivot.Engine.Tests.dll -explicit only -showLiveOutput

# The browser
dotnet publish samples/ExGrid.DemoHost -c Release -o <scratch>/publish
node serve.mjs <scratch>/publish/wwwroot 5699
EXGRID_DEMO_TRADES=1000000 EXGRID_DEMO_DATA=<scratch>/api-data dotnet samples/ExGrid.DemoApi/bin/Release/net10.0/ExGrid.DemoApi.dll --urls http://localhost:8699
cd tests/ExGrid.Browser
EXGRID_BASE_URL=http://localhost:5699 EXGRID_MEASURE=pivot EXGRID_MEASURE_CSV=<scratch>/trades-1000000.csv \
  flock -w 7200 /tmp/exgrid-layer3.lock xvfb-run --auto-servernum --server-args="-screen 0 2560x1600x24" \
  npx playwright test -c <scratch>/playwright.local.config.mjs measure-pivot.spec.mjs
```
