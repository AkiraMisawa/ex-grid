# The harness of ticket 01 (Claude Code's run)

Recreated, never committed: these are copies of the files the measurement added or changed, at the
repository-relative paths they had. Nothing under `src/` is among them.

| Path | What it is |
|---|---|
| `spikes/live-update/Costs/` | The CoreCLR harness, a Release console: `grid`, `pivot`, `vouch` and `pivot-memory` modes (see `Program.cs`) |
| `tests/ExGrid.Browser/measure-live-costs.spec.mjs` | The layer-3 harness: `EXGRID_MEASURE=live-costs`, `pivot-costs` or `live-bytes` |
| `samples/ExGrid.DemoPages/Pages/GridLiveLocalPage.razor` | `/grid-live-local` with `?probe=1` (a second source and the two checks, timed before each batch), `?step=1` (a batch per press, the page not rendering after it) and the first changed position in the status line. A changed file: `modified-files.patch` holds the change alone |
| `samples/ExGrid.DemoPages/Pages/PivotLiveCostsPage.razor` | `/pivot-live-costs`: ExPivot over D10's report, `?a=&b=&batch=&step=1&probe=1` |
| `samples/ExGrid.DemoHost.Server/WireCounters.cs`, `Program.cs` | The Server host's connection counter, `GET /api/wire` (M2's, from `spikes/render-bench/Bench.Server`). `Program.cs` is a changed file, in `modified-files.patch` |
| `scripts/` | The drivers that ran it here, from the session's scratchpad: the other track's idle check, the waits, the per-run wrappers that log `raw/contention.log`, the batches, and a lister for `raw/browser-results.json`. Their paths are this machine's |

## Putting it back

From the repository root, at `41c8d8c8` or a commit where the patched files have not moved:

```sh
H=verification/2026-10-06-macos-live-update-costs-cc/harness
git apply $H/modified-files.patch
mkdir -p spikes/live-update/Costs
cp $H/spikes/live-update/Costs/* spikes/live-update/Costs/
cp $H/samples/ExGrid.DemoHost.Server/WireCounters.cs samples/ExGrid.DemoHost.Server/
cp $H/samples/ExGrid.DemoPages/Pages/PivotLiveCostsPage.razor samples/ExGrid.DemoPages/Pages/
cp $H/tests/ExGrid.Browser/measure-live-costs.spec.mjs tests/ExGrid.Browser/
git add -N spikes/live-update/Costs samples/ExGrid.DemoHost.Server/WireCounters.cs \
  samples/ExGrid.DemoPages/Pages/PivotLiveCostsPage.razor tests/ExGrid.Browser/measure-live-costs.spec.mjs
```

(`git add -N` because the flake sees only files git knows of.)

## Running it

```sh
# CoreCLR, from the repository root; write the output where it lasts, never under the temp directory
nix develop -c dotnet build -c Release spikes/live-update/Costs/Costs.csproj
DOTNET_TieredCompilation=0 nix develop -c dotnet spikes/live-update/Costs/bin/Release/net10.0/Costs.dll grid  out.json 100000 1,100,1000 3 15
DOTNET_TieredCompilation=0 nix develop -c dotnet spikes/live-update/Costs/bin/Release/net10.0/Costs.dll pivot out.json 1000 100 1,1000 3 15
DOTNET_TieredCompilation=0 nix develop -c dotnet spikes/live-update/Costs/bin/Release/net10.0/Costs.dll vouch out.json 100000
DOTNET_TieredCompilation=0 nix develop -c dotnet spikes/live-update/Costs/bin/Release/net10.0/Costs.dll pivot-memory out.json 1000 100 1 20

# The hosts, published once in Release
nix develop -c dotnet publish samples/ExGrid.DemoHost -c Release -o <hosts>/wasm
nix develop -c dotnet publish samples/ExGrid.DemoHost.Server -c Release -o <hosts>/server
nix develop -c dotnet publish samples/ExGrid.DemoApi -c Release -o <hosts>/api

# The browser, from tests/ExGrid.Browser (npm ci first); one configuration per run
EXGRID_MEASURE=live-costs EXGRID_COSTS_ROWS=1000000 EXGRID_COSTS_BATCH=1000 EXGRID_COSTS_RUNS=15 EXGRID_COSTS_WARMUP=3 \
  EXGRID_COSTS_OUT=<out>.json EXGRID_HOSTS=<hosts> EXGRID_BASE_URL=http://localhost:5599 EXGRID_HEADLESS=1 \
  nix develop ../..#browser -c npx playwright test measure-live-costs.spec.mjs --project=chrome
#   add EXGRID_COSTS_PROBE=1 for the source alone and the two checks
EXGRID_MEASURE=pivot-costs EXGRID_COSTS_A=1000 EXGRID_COSTS_B=100 EXGRID_COSTS_BATCH=1 EXGRID_COSTS_PROGRESS=<progress>.jsonl …   # as above
EXGRID_MEASURE=live-bytes EXGRID_HOSTING=server EXGRID_BASE_URL=http://localhost:5598 EXGRID_COSTS_ROWS=1000000 EXGRID_COSTS_BATCH=1000 …
```

Pick ports nobody else uses (`lsof -ti tcp:<port>`): `hosting.mjs` derives the host, proxy and API
ports from `EXGRID_BASE_URL`, and an already-running host on them is reused.

Two corrections were made to the spec during the run, and the copy here has both: the Server bytes
are read off the circuit's connection, which is the one that wrote the most over all the presses
apart from the harness's own `GET /api/wire` connection (the runs here took first the connection
that wrote most on the first press, then the one that wrote most in all, which was the harness's own;
the circuit's figures were derived from the per-connection counts the second runs kept, into
`raw/server-bytes.json`); and the pivot runs append each redraw to
`EXGRID_COSTS_PROGRESS` and stop when the page logs that it ran out of memory.
