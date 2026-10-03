# DA-17's CSV row, and PV-21's CSV target, measured again *(2026-10-01, ticket 07)*

Observational: recorded, never gated (Definition of Done §1). ExGrid.Data's ticket 07 made the CSV
reader faster, keeping every rule of ADR-0064, and asks for DA-17's CSV row to be measured again on
CoreCLR and in a published WebAssembly build, beside the first measurement
(`verification/2026-10-01-linux-measure`). This is that record. The profile, and each change with
its before and after, are in the ticket's Comments; the numbers behind them are here, in
`metrics.json`, under `profile`.

**Every number is this machine's, and the machine was shared.** It is not the first record's
machine: there the base build read the CSV in 955 ms on CoreCLR and 14.6 s in the browser, here in
692 ms and 12.9 s. So every comparison below measured its before again, here, alternating it with
the after under the same conditions. Two other agents built and tested on the machine until about
09:57 UTC, and the coordinator after; the final runs began after 11:09.

## The machine

| | |
|---|---|
| CPU | Intel(R) Xeon(R) Processor @ 2.10GHz (family 6, model 207, stepping 2), 4 vCPUs, a Firecracker guest |
| Memory | 15.7 GiB |
| OS | Ubuntu 24.04.4 LTS, Linux 6.18.44 |
| .NET | SDK 10.0.401; runtime 10.0.12 (CoreCLR, workstation GC); browser-wasm runtime pack 10.0.12 |
| Browser | Chromium 141.0.7390.37 (Playwright's build 1194), headed under Xvfb at 2560×1600×24; Playwright 1.62.1, Node 22.22.2 |

## How

**The builds.** Before is `9959cee`, ExGrid.Data as the first record measured it. After is
`02d0139`, ticket 07's twelve changes; the integration branch merged on the way changed nothing
under `src/ExGrid.Data`.

**On CoreCLR.** `tests/ExGrid.Data.Tests/MeasureTests.cs`, unchanged since the base, built in
Release and run as it says (`-explicit only -showLiveOutput`, its CSV test). The same test assembly
ran against the base's `ExGrid.Data.dll` and the current one, alternately, three rounds; the test
reads through the public API only. Each line is the median of the three rounds' medians, each of 7
reads after 2 warm-ups.

**In the browser.** `samples/ExGrid.DemoHost` was published with `dotnet publish -c Release` at each
build, trimmed and without AOT, and its `wwwroot` served as published, uncompressed, on port 5799 by
a small static server, as the first record served it. `measure-pivot.spec.mjs` ran with
`EXGRID_MEASURE=pivot` and `--grep` its CSV tests, through a config outside the repository that runs
the suite's own config against Playwright's Chromium and leaves the demo API server out (/pivot-csv
does not call it), under `xvfb-run`, holding the layer-3 lock. `EXGRID_MEASURE_CSV` named a
million-trade export written by the page's own `DemoCsv.TradeExportFileAsync` (89.7 MiB). Each
invocation reads the file three times, each on a fresh document, and the page's 100,000-trade sample
three times. The base ran three invocations and the head two, alternately.

**The profile** was taken with a scratch profiler and a scratch Blazor WebAssembly page, both outside
the repository: the read whole, the tokenizer alone, one declared column at a time, micro-benchmarks
of the shapes a reader could take, the yields between slices, and the file through `InputFile` as
/pivot-csv takes it. `dotnet-trace`'s sampled thread time gave CoreCLR's split by method.

## Against the targets

| Target | Before (this machine) | After | |
|---|---|---|---|
| **PV-21: a CSV of a million rows read ≤ 4 s**, in a published WebAssembly build | 12.9 s (12.8, 12.9, 13.9: the three invocations' medians) | **3.9 s and 4.1 s** (the two invocations' medians; reads from 3.9 to 4.9 s) | Met at the median on this machine; not with a margin, and not by every read |
| **On CoreCLR, about a tenth** (about 0.4 s) | 692 ms from memory | **491 ms** (best 414 ms) | Not met |

The page's own figure is the time from its `ReadAsync` to the status line, after `PivotSource.From`.
From the input event to the frame that shows the line, the medians were 4.06 and 4.27 s, against
12.96, 13.06 and 14.07 s.

What these say:

- **The browser read is 3.2 times faster**, and meets 4 s at its median here. The first record's
  machine read the base build 13% slower in the browser (14.6 s against 12.9 s), so there it would
  likely sit a little over 4 s.
- **CoreCLR's read is 1.4 times faster from memory and 1.7 times from a file**, and misses its tenth.
  Of what is left, the profile gives about 100 ms to the column of a million distinct Ids (making a
  million strings, and telling them apart), about 75 ms to finding the fields, and 15-45 ms to each
  other column.
- **The page's yields between slices are now a fair part of what is left in the browser**: 4.4 ms
  each, after each 30 ms slice. See *A proposal*, below.

## DA-17's CSV row

| A million records read from a CSV | Before | After |
|---|---|---|
| **CoreCLR, from memory** (89.7 MiB, 12 columns, keyed by Id) | 692 ms (755, 671, 692; best 634), allocating 195.9 MiB | **491 ms** (474, 509, 491; best 414), allocating 191.9 MiB |
| **CoreCLR, from a file** in the page cache | 779 ms (779, 780, 703; best 633) | **456 ms** (429, 456, 468; best 421) |
| **Browser, through `InputFile`** (/pivot-csv, the same 89.7 MiB) | 12.9 s (12.0 to 13.9 s) | **4.0 s** (3.9 to 4.9 s) |
| **Browser, 100,000 rows from memory** (the page's sample) | 1.3 s (1.2 to 1.4) | **0.5 s** (0.5 to 0.6) |
| The first report, after the read (browser) | 420 ms (374 to 489) | 368 ms (346 to 566) |
| The longest task while reading (browser) | 152 ms (119 to 179) | 127 ms (108 to 221) |

The browser took 18.6 times CoreCLR's time before (12.9 s against 692 ms), and 8.1 times after (4.0
s against 491 ms): most of what was cut there was the cost of a call in the interpreter.

On the way: 6.3 s at change 8, 4.0 s at change 10, 4.1 s (4.0 to 4.4) at change 11.

## The profile, in short

The ticket's Comments give it whole. At the base, on this machine:

- **CoreCLR (692-755 ms):** finding the fields 150 ms; the Id column, a million distinct texts, about
  200 ms (decoded, looked up and added to a `Dictionary<string, int>` that grows); every other
  column 10-40 ms; indexing the Record Key up to about 30 ms.
- **The browser (9.4-9.8 s of work, 11.1-12.9 s with the page's yields):** a call costs 16-24 ns
  in the interpreter against 1-2 ns on CoreCLR, and the reader made about ten for every field, so
  every column cost 0.3-0.8 s; finding the fields 1.2 s; the Id column 1.3 s; the Record Key 0.5 s;
  the yields 1.3-3.5 s.
- **Through `InputFile`:** each read is a call into JavaScript; the file took 1.1-1.2 s to copy in
  the reader's reads of 256 KiB.

## A proposal, not made: the browser's yield

`SnapshotLoadOptions.Yield` defaults, in a browser, to `Task.Delay(1)` "which lets the page paint",
as ExGrid.Data's ticket 01 specifies and as ExPivot's `PivotSlicing` does too. Measured here, 60
slices of 30 ms each followed by the yield:

| Yield | Per yield | Frames painted |
|---|---|---|
| `Task.Delay(1)` | 4.3-4.4 ms | 123-124 (about two a slice) |
| `Task.Yield()` | 0.4-0.6 ms | 59-61 (one a slice) |

`Task.Yield()` lets the page paint once a slice and costs a ninth as much. Over a read of about 100
slices that is about 0.4 s, a tenth of PV-21's target. It changes a documented default that the
family shares, so it was not changed here: it is the user's to decide, for ExGrid.Data and ExPivot
alike.

## What could not be measured as asked, and the nearest thing measured

- **Chrome and Edge are not installed here.** Playwright's Chromium 141 build stood in for both. This
  machine is Linux; Windows and macOS were not measured.
- **The first record's machine was not available.** Its before numbers are not this machine's, so
  the before was measured again here; the after cannot be set beside the first record's numbers
  directly.
- **The machine was shared** while the changes were measured one at a time; each was measured as an
  alternation of before and after, and the final runs began once the other agents were done.
- **The long tasks of the read (108-221 ms)** were not traced. The scratch page counted 7-8 young and
  2 full collections a read, most of them for the Id column's million strings, which is the likely
  source; ticket 07 did not change it.

## Console

The browser's console record was empty for every test of the targeted layer-3 runs (`pivot-csv.spec.mjs`,
12 tests on each host), and the measurement tests passed, which the fixture fails on an unexpected
message.

## The commands

```sh
# CoreCLR, three rounds, alternating the base's ExGrid.Data.dll and the current one
dotnet build tests/ExGrid.Data.Tests -c Release
dotnet tests/ExGrid.Data.Tests/bin/Release/net10.0/ExGrid.Data.Tests.dll -explicit only -showLiveOutput \
  -method 'ExGrid.Data.Tests.MeasureTests.DA17_a_million_rows_read_from_a_CSV'

# The browser, at each build
dotnet publish samples/ExGrid.DemoHost -c Release -o <scratch>/publish
node serve.mjs <scratch>/publish/wwwroot 5799
cd tests/ExGrid.Browser
EXGRID_BASE_URL=http://localhost:5799 EXGRID_MEASURE=pivot EXGRID_MEASURE_CSV=<scratch>/trades-1000000.csv \
  flock -w 7200 /tmp/exgrid-layer3.lock xvfb-run -a --server-args="-screen 0 2560x1600x24" \
  npx playwright test -c <scratch>/local.config.mjs measure-pivot.spec.mjs --grep "CSV of a million rows|100,000-trade sample"
```
