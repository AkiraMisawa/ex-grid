# Live report computation and retention after ADR-0150 to ADR-0153

Date: 2026-10-06. Observations on `claude/live-data-next`, following the [original update measurements](../2026-10-06-macos-live-update-costs/README.md), [memory diagnosis](../2026-10-06-macos-pivot-memory/README.md) and [boundary comparison](../2026-10-06-macos-pivot-boundary-bench/README.md). Performance does not gate. This directory records the product implementation, not the restricted A/B prototype.

## Method

- Apple M4 Pro, 12 cores, 24 GiB; macOS 26.6.2. Nix .NET 10.0.203 / runtime 10.0.7; Chrome 154.0.8037.98.
- CoreCLR: Release, `DOTNET_TieredCompilation=0`, two warm-ups and nine retained observations; minima below. Full GC occurs outside each CoreCLR timer. Each phase is timed directly, not inferred from differences between builds.
- Browser: published Release WebAssembly IL, headless Chrome, 1,400 × 1,100 CSS pixels. One runner, no simultaneous build or measurement. The default 2 GiB WASM heap ceiling is unchanged. No forced GC, history removal, or highlight disabling in the browser.
- Absolute output paths remain in this directory. Disposable harness sources are archived as `.txt`; they are not product pages or tests. The pinned ag-grid source remains release-36.2.0 (`0fee5b7b1e839ae23fe860e404042448f3c1375d`), read only in the session scratchpad; no Enterprise source is copied.

## Stable-row updates on CoreCLR

The fixture matches ticket 01: 1,000 outer Items, 10/100/400 inner Items, one record per leaf, decimal Sum, 1,000 changes per batch. `ReportSteps` times the Snapshot publication, session-owned aggregate fold, affected Cube cells, persistent report structure and maintained label widths separately. It asserts the row sequence is shared. The private state is obtained through reflection only in this disposable measurement; correctness tests use public interfaces.

| Report rows | Snapshot Apply ms | Fold ms | Affected Cube ms | Report structure ms | Label widths ms |
| --- | --- | --- | --- | --- | --- |
| 11001 | 0.0760 | 0.0819 | 0.9182 | 0.0927 | 0.0011 |
| 101001 | 0.1035 | 0.2696 | 3.9211 | 0.1668 | 0.0041 |
| 401001 | 0.1146 | 0.4188 | 9.9267 | 0.2205 | 0.0042 |

The old 401,001-row fixture rebuilt the Cube in 74.221 ms and Report in 156.859 ms. The new phases have different responsibilities and do not establish an end-to-end speedup by summing their independent minima. The full `PivotAnswer` assembly and full row-order/label-width scans no longer occur on this value-only path.

An intermediate implementation re-merged every affected total in reference order, costing 217.591 ms for the largest Cube phase. It is retained in `raw/before-additive/`. The final implementation uses subtraction/addition only for exact Sum/count parts with a sufficient decimal precision/overflow bound; all other parts preserve reference-order merging. The bound is an optimization guard, not a cap or a change of numerical semantics. Extreme decimal values, doubles, Product, Variance and percentage dependencies are compared to fresh computation in the engine tests.

## The actual component, including ExGrid

`ReportRendering` measures from public Snapshot Apply through report computation, Window projection, component diff and bUnit DOM updates. It does not measure browser layout or paint. Its separate Window admission measurement clears only the grid's observed-Window marker, then calls its real `TakeInWindow`; reflection overhead is included. The component receives 64 rows at every full report size and vouches for them. These admission times replace a former whole-report key check (9.045 ms at 401,001 rows); they are not a measurement of checking the same number of rows.

| Report rows | Whole update min ms | Median ms | Window admission min ms | Painted | Rendered | Mounted |
| --- | --- | --- | --- | --- | --- | --- |
| 11001 | 1.9635 | 2.2220 | 0.0006 | 18 | 4 | 0 |
| 101001 | 7.9847 | 8.8918 | 0.0000 | 18 | 2 | 0 |
| 401001 | 12.9266 | 14.9793 | 0.0016 | 18 | 2 | 0 |

The original ticket 01 render-only measurement excluded the computation now inside this timer. Do not divide those figures as if they measured the same scope. In every observation the rows that rendered equal the rows whose displayed text changed; no row remounted.

## M3: the real live-demo generator

`ReportGeneratorRendering` uses the actual `DemoPivotData` declarations/generator, the `/pivot-live` amendment algorithm and seed 20261001, its Region > Desk × Product layout, 2,000 trades and five changes per batch. There are 100 redraws per setting, 14 report rows and 11 painted rows. A fake clock advances by the page's 250 ms cadence; timer expiry before the batch is outside the redraw count.

| Subtotals | Mounted per update | Rendered min / median / max | Text-changed min / median / max |
| --- | --- | --- | --- |
| True | 0 | 3 / 6.0 / 8 | 3 / 6.0 / 8 |
| False | 0 | 1 / 3.5 / 5 | 1 / 3.5 / 5 |

Original M3 rendered and mounted all 11 rows. The new run renders exactly the text-changing rows and mounts none. The original unchanged-row median was five with subtotals and eight without; differences in the exact batch history mean those are workload context, not a per-batch paired comparison.

## Browser memory and CSV

The former 401,001-row reproduction failed on update seven in three runs. The final 70-update run completes with the real 64-paint history intact, no runtime/console errors and no forced collection. The current Window holds 64 rows, and versioned operations hold two Reports; weak-reference probes do not prolong their lifetimes. A separate run completes 200 updates (raw/large-200), ending with all 64 paints held, two operation Reports and 16 of 201 weak Report targets not yet collected. No runtime or console error occurs.

Source cardinality and report cardinality remain distinct. One million source records with 1,000 leaves / 1,101 report rows completes 70 updates. The 12-column, 85,000,089-byte CSV of one million records loads with the page's unchanged default caps and produces 111 report rows.

Raw `large-first`, `million-low-cardinality` and `million-csv` predate the final highlight retention, sliced initialization and exact-sum optimization. They are intermediate observations only. `discarded-wrong-query` was interrupted after a mistyped query parameter; it is not a million-record result. Use the `*-final` and `large-200` records for final claims. The 200-update run has managed memory of 590.1 MiB initially and 1,094.4 MiB at its final probe (maximum 1,256.9 MiB). WASM capacity ends at 1,973.6 MiB, close to its unchanged ceiling: there is no claim of unlimited headroom. The aggregate allocation-counter difference is about 20.3 MiB per update, versus the old approximately 214.6 MiB; individual `GetTotalAllocatedBytes(false)` observations can remain unchanged between collections and are not reliable per-update allocation figures. The million-record live run ends at 157.7 MiB managed / 343.5 MiB WASM capacity. The CSV load-to-rows observation is 2,544 ms.

Normal-GC managed memory and WASM capacity are not live-object sizes: uncollected weak references do not prove strong retention, and WASM capacity does not shrink after a collection.

## PV-21 in the real browser

The unchanged `measure-pivot.spec.mjs` runs on published WebAssembly with one million local
trades, 1,000 amendments per batch, and the demo API holding one million trades. Its own protocol
turns the API's background feed off while measuring the local feed. All 12 observed batches are
shown in the batch's frame; Apply-to-report-frame median is 33 ms (21–42 ms). No long task is
observed in those update intervals. The page's Apply timer can include synchronous report work;
these are not separate phase measurements. A frame opportunity is not physical display latency.

The existing CSV measurement repeats the actual million-row file three times: median read
2,067 ms, first report question 286 ms, file-input-to-report 2,353 ms (2,344–2,407 ms), and first
visual response 40 ms. The longest task through the report is median 99 ms (98–113 ms). This is
not a claim that every task stays below the nominal slicing budget; CSV reading has a remaining
long task. `raw/pv21-metrics.json` and `raw/pv21.log` retain the observations. Performance remains
non-gating. The original measurements and their browser limitations remain in the linked baseline.

## Verification and reproduction

The permanent tests cover random structural batches and all existing aggregations/percentage modes against fresh computation, independent report sessions, versioned offscreen operations, unavailable versions, metadata-only updates, delayed Details commands and unchanged-row rendering. The final full layer 1/2 run passes 9,100 tests (eight existing skips), and the solution builds with zero warnings/errors. The changed remote browser spec and adjacent live spec pass three repetitions on each host: 42 WebAssembly and 42 Server cases, Chrome. The full Chrome/Edge cross-host suite remains CI's.

The [independent Standards and Spec review](review.md) found two issues on each axis, all fixed:
trim-safe report JSON, complete display-settings validation, atomic publication after concurrent
validation and the obsolete PV-2 wording. The protocol regressions pass 21 tests. The package
smoke test packs all ten packages, compiles the README Consumers and publishes/runs a trimmed,
reflection-disabled Consumer without blanket assembly roots; its report and Arrow round trips
pass. The report smoke includes populated Details and explicit words, policies and glyph widths.

The existing Server `write-refusal.spec.mjs` passes all 15 cases after the historical-text change.
The Docs Site smoke exercises remote Details and both live examples with Built-in and MudBlazor
Chrome: six observations, no console/runtime errors (`raw/docs-smoke.json`).

The review fixes were followed by a fresh solution build, full layer 1/2 run, package smoke,
Release publication and both targeted browser runs. Reproduce those checks with:

```sh
nix develop -c dotnet test ExGrid.slnx
nix develop -c dotnet build ExGrid.slnx
nix develop -c bash tests/ExGrid.PackageSmoke/check.sh
# After publishing wasm, api and server under .memory-hosts/after:
EXGRID_HOSTS="$PWD/.memory-hosts/after" EXGRID_BASE_URL=http://localhost:5499 nix develop .#browser -c bash -c 'cd tests/ExGrid.Browser && npx playwright test pivot-db.spec.mjs pivot-live.spec.mjs --project=chrome --repeat-each=3'
EXGRID_HOSTS="$PWD/.memory-hosts/after" EXGRID_HOSTING=server EXGRID_BASE_URL=http://localhost:5498 nix develop .#browser -c bash -c 'cd tests/ExGrid.Browser && npx playwright test pivot-db.spec.mjs pivot-live.spec.mjs --project=chrome --repeat-each=3'
```

To reproduce the disposable measurements, copy the `.txt` harness files to their original locations (`ReportSteps.cs` in engine tests; `ReportRendering.cs` and `ReportGeneratorRendering.cs` in component tests; `LiveCostPage.razor` in demo pages; `oom.*` in browser tests). For M3 only, copy the current `samples/ExGrid.DemoPages/DemoPivotData.cs` into the component tests as `MeasurementDemoPivotData.cs`. Stage these new paths so nix sees them, then remove them from the working tree and index after measuring. Never commit them in these build locations.

```sh
LIVE_COST_OUTPUT="$PWD/verification/2026-10-06-macos-live-report-after/raw/core" DOTNET_TieredCompilation=0 nix develop -c dotnet run --project tests/ExPivot.Engine.Tests -c Release -- -class "*ReportSteps"
LIVE_COST_OUTPUT="$PWD/verification/2026-10-06-macos-live-report-after/raw/core" DOTNET_TieredCompilation=0 nix develop -c dotnet run --project tests/ExPivot.Components -c Release -- -class "*ReportRendering"
LIVE_COST_OUTPUT="$PWD/verification/2026-10-06-macos-live-report-after/raw/core" DOTNET_TieredCompilation=0 nix develop -c dotnet run --project tests/ExPivot.Components -c Release -- -class "*ReportGeneratorRendering"
nix develop -c dotnet publish samples/ExGrid.DemoHost -c Release -o .memory-hosts/after/wasm
OOM_PROBE=1 OOM_UPDATES=200 OOM_PATH="/live-cost-pivot?b=400" OOM_OUTPUT="$PWD/verification/2026-10-06-macos-live-report-after/raw/large-200" EXGRID_HOSTS="$PWD/.memory-hosts/after" EXGRID_BASE_URL=http://localhost:5499 nix develop .#browser -c bash -c "cd tests/ExGrid.Browser && npx playwright test --config oom.config.mjs --project=chrome"
```

Create the output directory first. The million-record live fixture uses `?b=10&outer=100&records=1000000` and 70 updates. `harness/make-csv.py` generates the CSV; pass its absolute path as `OOM_CSV` to select the CSV test. Do not run these concurrently with another measurement or rebuild a running host.
