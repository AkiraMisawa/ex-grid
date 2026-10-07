# Explicit writes during live updates

Decision: [ADR-0154](../../docs/adr/0154-user-writes-prevail-and-consumers-own-value-conflicts.md).
This record follows the [report implementation measurements](../2026-10-06-macos-live-report-after/README.md).
The earlier measurements used the former displayed-value conflict policy; they are retained as
historical evidence, not relabelled as measurements of this revision.

## Scope and method

Machine: Apple M4 Pro, 24 GiB RAM, arm64, macOS 26.6.2 (25G83). The repository's Nix
development shells supply the .NET SDK and Node tools. Browser runs use installed Chrome
headlessly on this Mac; no claim about native scrollbar geometry follows from these runs.

Cell edits, paste, Ctrl+Enter, Delete and fills no longer reject an operation merely because a
value changed. Actions resolve the original target to its current row and leave business
conflicts to the Consumer. Target/order checks, Consumer validation, gathered publication and
once-only Action delivery remain. ExSheet and the Docs Site move with the changed API.

The browser memory reproduction retains the same 400,000 source records, 401,001 report rows,
1,000 changes per batch, one-second Change Highlight, 64-row Window and explicit one-million
leaf/row caps. It uses normal GC and the unchanged WASM ceiling. The probe now positively checks
the grid's Action-address collection and requires it to be empty for this display-only pivot.
It records Snapshot compaction and slice counts as well as report/operation/highlight roots,
managed allocation counters and WASM capacity.

The separate million-record fixture has 1,000 leaves and 1,101 report rows. Source cardinality
and report cardinality must not be compared as the same workload.

Managed memory between collections is not retained live-object size. WASM capacity is reserved
linear memory and does not shrink after GC. A live weak-reference target may simply be
uncollected. Aggregate allocation-counter differences are useful; individual samples from
`GetTotalAllocatedBytes(false)` need not advance on each update.

The CoreCLR component timing reuses the earlier harness: Release, tiering disabled, two warmups
and nine retained observations; prebuilt batches and forced collections are outside the timer.
It covers Apply through report computation, Window projection and bUnit render/diff, not browser
layout or physical display. Performance is observational and does not gate.

## Results

The 401,001-row reproduction completed **1,000 updates and 15 Snapshot compactions**, with no
runtime or console error. Its display-only grid retained **zero Action addresses** throughout.
Two operation Reports and at most eight distinct operation/highlight Reports were directly
rooted by the report source; the final Window remained 64 rows.

| Observation | Initial | Final | Maximum sampled |
|---|---:|---:|---:|
| Managed memory, normal GC, MiB | 600.3 | 1,116.3 | 1,256.9 |
| WASM linear-memory capacity, MiB | 1,076.3 | 2,048.0 | 2,048.0 |

WASM capacity last grew at update 257 and stayed at the unchanged 2 GiB ceiling through update
1,000. Completing this workload does **not** establish ample headroom or safety for arbitrary
high-cardinality reports. Each compaction's observation interval allocated about 930 MiB
(median 930.4 MiB); rebuilding those large structures remains expensive. The mean allocation
over the full 1,000-update run was 16.2 MiB/update, including the resets. That mean is not an
ordinary-update allocation estimate or a timing measurement.

The large run used the published write-policy implementation at `3378d77a`, before the later
target-binding/Space edge-case fixes. Those later fixes do not change the pivot computation or
its compaction policy. Raw samples, compaction flags, root counts and a reproducible summary are
in [`raw/large-1000`](raw/large-1000). The ten-update preflight is only an instrumentation check.

The published Docs Site at `76f28f10` passed three repetitions of both the Built-in and MudBlazor
examples (six observations): a typed
125 survives F9 and commits over the feed; invalid text remains in a rejected editor; the
Consumer rejects an approval at 175; generated source tabs show both examples and `Position.cs`.
No browser errors or warnings occurred. The first smoke script, against `25cee5d4`, used the
Built-in input selector for MudBlazor too; correcting that selector made the same published
application pass. Both the initial observation and final repeated result are retained.

The ten packages at `25cee5d4` also passed the package smoke check, including the trimmed
Consumer's Arrow and versioned report-protocol round trips. At `76f28f10`, the full solution
built with zero warnings and errors; layers 1 and 2 passed **9,116 tests, eight existing skips,
zero failures**. The initial stale-answer test failure and its deterministic completion fix
are explained in [the review record](review.md).

The same CoreCLR component harness at `76f28f10` measured these whole-update times (ms):

| Report rows | Previous median | Current minimum | Current median | Current maximum | Rows rendered / painted |
|---:|---:|---:|---:|---:|---:|
| 11,001 | 2.222 | 2.267 | 2.326 | 2.566 | 4 / 18 |
| 101,001 | 8.892 | 8.195 | 8.317 | 9.653 | 2 / 18 |
| 401,001 | 14.979 | 12.746 | 13.743 | 14.884 | 2 / 18 |

Every case delivered a 64-row Window and mounted zero new row components. The observations are
comparable in method, but are separate runs, not proof of a causal speedup from the write-policy
change. They show no material regression in this measured path. No compaction occurs within the
11 batches of each component case; the large reset allocation above is a separate observation.
The rewritten `write-intent.spec.mjs` passed with `--project=chrome --repeat-each=3` against
published hosts at `76f28f10`: **42 passed / nine Server-only skips on WASM**, **51 passed on
Server**, including the 150 ms latency cases. Both console records are empty. This is a targeted
local run; the full browser matrix remains CI's after the user approves push and PR creation.
The separate **1,000,000-record / 1,000-leaf / 1,101-report-row** fixture at `76f28f10` completed
200 updates and three compactions with no runtime error or browser warning. Managed memory was
153.2 MiB initially, 200.1 MiB finally, and 207.7 MiB at the largest sample; WASM capacity was
343.5 MiB initially and 412.3 MiB finally. Action addresses stayed at zero, operation Reports at
two, and distinct operation/highlight Reports at no more than 11. It retained a 64-row Window.
See [`raw/million-200`](raw/million-200). Allocation counters are coarse here too: a zero interval
at a compaction is not zero allocation.

These results separate two costs: retaining source records and constructing a high-cardinality
report. The latter dominates the 401,001-row fixture and its compaction reset. The original OOM
was a distinct ownership defect: painted rows/keys retained entire historical Reports; detached
display identities and shared computation repaired that defect, and ADR-0154 now removes the
obsolete painted-value policy entirely. The current 2 GiB high-water mark is still a practical
limitation for large local reports. Server data is computed server-side and sends report Windows
and deltas, as ADR-0151 requires.

The earlier record's three million-record CSV imports (median 2,353 ms to report, longest task
99 ms) remain prior measurements. This revision does not change CSV parsing and does not claim
to have removed that long task. The new million-record experiment above measures sustained
updates of a generated source, not CSV import time.

## Why the earlier capacity grew near updates 65 and 129

The source's [Snapshot tuning](../../src/ExGrid.Data/Storage/SnapshotTuning.cs) permits 64 batch
segments. [Batch compaction](../../src/ExGrid.Data/Storage/BatchApplier.cs) merges their surviving
records and shares the base segments. This fixture changes the same 1,000 records repeatedly,
so the first compaction leaves one batch segment: later compactions are expected every 64
updates (65, 129, 193, and so on).

Compaction changes physical record addresses. [AggregationPass.Fold](../../src/ExPivot.Engine/Columnar/AggregationPass.cs)
therefore refuses incremental folding at this boundary, and
[PivotComputationSession](../../src/ExPivot.Engine/PivotComputationSession.cs) rebuilds the
aggregate pass, Cube, computation indexes, Report and report structure. This reset boundary is
explicit in ADR-0153. The old working structures remain until their replacements are ready.
[LocalPivotReportSource](../../src/ExPivot.Engine/Reports/LocalPivotReportSource.cs) also retains
two operation Reports and a separate time-bounded Change Highlight history; pruning follows
the new report's completion. Those Reports share normal-generation data, but a reset creates
new full structures. This is distinct from the removed grid painted-value history.

The earlier `large-200/result.json` records roughly 930 MiB allocated in each observation
interval ending at updates 65, 129 and 193. At update 129, WASM capacity grows while managed
memory falls and the live weak Report targets fall from 71 to 9. Those observations are
consistent with reset allocation and collection, not proof that each old generation stays
strongly rooted. The new probe records actual compaction flags and both report retention paths
to test that explanation over more cycles. It does not change compaction thresholds, caps,
highlight duration or collection behavior.

## Reproduction

Run from the worktree root. The ordinary verification commands were:

```sh
nix develop -c dotnet build ExGrid.slnx
nix develop -c dotnet test ExGrid.slnx
nix develop -c bash tests/ExGrid.PackageSmoke/check.sh
```

The published applications are produced with `dotnet publish -c Release`, one output directory
each: `.memory-hosts/write-intent/{wasm,server,api,docs}`. The corresponding projects are
`samples/ExGrid.DemoHost`, `samples/ExGrid.DemoHost.Server`, `samples/ExGrid.DemoApi` and
`samples/ExGrid.Docs`. Every publish has its own raw log. Browser commands were:

```sh
EXGRID_HOSTS="$PWD/.memory-hosts/write-intent" EXGRID_BASE_URL=http://localhost:5799 \
  EXGRID_HEADLESS=1 nix develop .#browser -c bash -c \
  'cd tests/ExGrid.Browser && npx playwright test write-intent.spec.mjs --project=chrome --repeat-each=3'
EXGRID_HOSTING=server EXGRID_HOSTS="$PWD/.memory-hosts/write-intent" \
  EXGRID_BASE_URL=http://localhost:5798 EXGRID_HEADLESS=1 nix develop .#browser -c bash -c \
  'cd tests/ExGrid.Browser && npx playwright test write-intent.spec.mjs --project=chrome --repeat-each=3'
```

The disposable probes are archived under `harness/`, not installed in the product. For memory,
copy `LiveCostPage.razor.txt` to `samples/ExGrid.DemoPages/Pages/LiveCostPage.razor`, and the two
`oom.*.mjs.txt` files to `tests/ExGrid.Browser/` without `.txt`. Stage those paths for the Nix
flake, then republish the WASM host. Create the output directory before running:

```sh
OOM_PROBE=1 OOM_UPDATES=1000 OOM_TIMEOUT_MS=600000 OOM_PATH='/live-cost-pivot?b=400' \
  OOM_OUTPUT="$PWD/verification/2026-10-07-macos-write-intent/raw/large-1000" \
  EXGRID_HOSTS="$PWD/.memory-hosts/write-intent" EXGRID_BASE_URL=http://localhost:5499 \
  nix develop .#browser -c bash -c \
  'cd tests/ExGrid.Browser && npx playwright test --config oom.config.mjs --project=chrome'
```

For the separate million-record run, use `OOM_UPDATES=200`, `OOM_TIMEOUT_MS=300000`,
`OOM_PATH='/live-cost-pivot?b=10&outer=100&records=1000000'` and output `raw/million-200`.
The longer timeout is for the explicit workload length, not a retry of a failed assertion.
`harness/summarize-memory.py <result.json>` derives the saved summaries from raw samples.
The saved result JSON is formatted losslessly with one observation per line to keep the diff
readable; no samples or fields were removed.

For component timing, copy and stage `ReportRendering.cs.txt` as
`tests/ExPivot.Components/ReportRendering.cs`, create `raw/core`, and run:

```sh
LIVE_COST_OUTPUT="$PWD/verification/2026-10-07-macos-write-intent/raw/core" \
  DOTNET_TieredCompilation=0 nix develop -c dotnet run --project tests/ExPivot.Components \
  -c Release -- -class '*ReportRendering'
```

Unstage and remove every copied probe afterward. No copied probe is part of the final source
tree. The Docs smoke script uses the published Docs Site served by `static-host.mjs` on port
5697; run it with the browser Nix shell's Node. Browser runners and hosts ran sequentially,
and component timings ran without another agent building or testing. No full local layer-3
matrix was run.
