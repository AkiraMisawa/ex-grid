# The harness of ticket 13

Recreated, never committed: copies of the files the measurement added or changed, at the
repository-relative paths they had. Nothing under `src/` is among them. It is ticket 01's harness
([`2026-10-06-macos-live-update-costs-cc/harness`](../../2026-10-06-macos-live-update-costs-cc/harness/README.md)),
ticket 07's addition to it (the vouched pushed Window), the paint-text record's GC method and the
out-of-memory record's loop, brought to the code at `526f3b75`.

| Path | What it is |
|---|---|
| `spikes/live-update/Costs/` | The CoreCLR harness, a Release console. Modes: `grid`, `pivot`, `vouch`, `pivot-memory` (ticket 01's, `pivot` and `pivot-memory` rewritten for the redraw of ADR-0161), `pivot-gc` and `pivot-steady` (the paint-text record's, ported: `PivotGc.cs`, `GcRecorder.cs`), `push-count` (new: the key calls of a whole pushed update). See `Program.cs` |
| `tests/ExGrid.Browser/measure-live-costs.spec.mjs` | Ticket 01's layer-3 harness, with `EXGRID_COSTS_PUSH=1` / `EXGRID_COSTS_VOUCH=1` and the pivot probe's `NextCube` / `NextReport` readings |
| `samples/ExGrid.DemoPages/Pages/GridLiveLocalPage.razor` | Ticket 01's `?probe=1` and `?step=1`, and `?push=1&vouch=1`: the source's Window pushed by the page under the same Row Key. A changed file; `modified-files.patch` holds its change and `Program.cs`'s |
| `samples/ExGrid.DemoPages/Pages/PivotLiveCostsPage.razor` | `/pivot-live-costs`, with the out-of-memory record's `[DEBUG-oom]` probes, `report.ValueAt(row, c)` for the moved API, and a probe of `NextCube` / `NextReport` on the report the previous probe saw |
| `samples/ExGrid.DemoHost.Server/WireCounters.cs`, `Program.cs` | The Server host's connection counter, `GET /api/wire` (M2's) |
| `scripts/` | What ran it here, with this machine's paths: the idle check and its bounded wait, the per-run wrappers that log `raw/contention.log` (`run-core.sh`, `run-browser.sh`, `run-oom.sh`), the batches, the smoke runs, the loop, and the scripts that made the tables and `metrics.json` from `raw/` |

## Putting it back

From the repository root, at `526f3b75` or a commit where the patched files have not moved:

```sh
H=verification/2026-10-07-macos-live-update-costs-after/harness
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

**The before code** was a `git archive` of `41c8d8c8` with the two harnesses of 2026-10-06 put back:
`git apply ../2026-10-06-macos-paint-text-cost/harness/harness.patch` (its `PaintCosts`, for `pivot-gc`) and
ticket 01's `Costs` copied into `spikes/live-update/Costs` (for `pivot` and `grid`), built and published with
this worktree's flake (`nix develop <this worktree> -c dotnet …`), its hosts into a directory of their own.

## Running it

```sh
nix develop -c dotnet build -c Release spikes/live-update/Costs/Costs.csproj
C=spikes/live-update/Costs/bin/Release/net10.0/Costs.dll
DOTNET_TieredCompilation=0 nix develop -c dotnet $C pivot        out.json 1000 100 1,1000 3 15
DOTNET_TieredCompilation=0 nix develop -c dotnet $C grid         out.json 1000000 1,100,1000 3 15
DOTNET_TieredCompilation=0 nix develop -c dotnet $C vouch        out.json 100000
DOTNET_TieredCompilation=0 nix develop -c dotnet $C pivot-gc     out.json 1000 100 3 15 full verbose
DOTNET_TieredCompilation=0 nix develop -c dotnet $C pivot-steady out.json 1000 100 150
DOTNET_TieredCompilation=0 nix develop -c dotnet $C pivot-memory out.json 1000 100 1 20
DOTNET_TieredCompilation=0 nix develop -c dotnet $C push-count   out.json 100000 3

# Hosts, published once in Release
nix develop -c dotnet publish samples/ExGrid.DemoHost -c Release -o <hosts>/wasm
nix develop -c dotnet publish samples/ExGrid.DemoHost.Server -c Release -o <hosts>/server
nix develop -c dotnet publish samples/ExGrid.DemoApi -c Release -o <hosts>/api

# The browser, from tests/ExGrid.Browser (npm ci first), one configuration per run, under the layer-3 lock
EXGRID_MEASURE=pivot-costs EXGRID_COSTS_A=1000 EXGRID_COSTS_B=400 EXGRID_COSTS_BATCH=1 EXGRID_COSTS_OUT=<out>.json \
  EXGRID_HOSTS=<hosts> EXGRID_BASE_URL=http://localhost:5799 EXGRID_HEADLESS=1 \
  nix develop ../..#browser -c npx playwright test measure-live-costs.spec.mjs --project=chrome
#   EXGRID_COSTS_PROBE=1 for the probes; EXGRID_COSTS_RUNS=80 for the long runs
EXGRID_MEASURE=live-costs EXGRID_COSTS_ROWS=1000000 EXGRID_COSTS_BATCH=1000 [EXGRID_COSTS_PUSH=1 [EXGRID_COSTS_VOUCH=1]] …
EXGRID_MEASURE=live-bytes EXGRID_HOSTING=server EXGRID_BASE_URL=http://localhost:5798 EXGRID_COSTS_ROWS=1000000 EXGRID_COSTS_BATCH=1000 …
EXGRID_MEASURE=pivot … npx playwright test measure-pivot.spec.mjs --grep "1,000 changes folded"   # PV-21

# The memory loop: serve <hosts>/wasm/wwwroot with static-host.mjs on 5799, then (change its Playwright path first)
node scripts/loop.mjs http://localhost:5799 1000 100 20 "" 1

# The tables and metrics.json, from the record's raw files
python3 scripts/core-tables.py <record>/raw
python3 scripts/browser-tables.py <record>/raw
python3 scripts/server-bytes.py <record>/raw
python3 scripts/metrics.py <record>
```

Pick ports nobody else uses (`lsof -ti tcp:<port>`): `hosting.mjs` derives the host, proxy and API ports
from `EXGRID_BASE_URL`, and an already-running host on them is reused.

Two traps met here. **The Server-bytes spec still picks the polling connection** as the circuit's (`wire` in
`browser-results.json` reads about 549 bytes for every press); `scripts/server-bytes.py` derives the
circuit's bytes from the per-connection counts, as ticket 01 did. **`measure-pivot.spec.mjs` writes its
`metrics.json` under `verification/<day>-<platform>/`** and a second run overwrites the first's entry; the
PV-21 figures here are read from each run's own log.
