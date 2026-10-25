# The Engine Host Worker (spike)

ADR-0370 section 2 put ExSheet's and ExPivot's engines, under React, in a Web Worker running .NET
WebAssembly; section 6 asked for that Worker to be measured before a product's first step is called done.
This is that Worker as a spike, the measurement, and a third arm beside it: a TypeScript engine of only
what render-bench's two scenarios exercise, in the same Worker shape and on the page's own thread. It
ships nothing, and the .NET Worker is not to become a product (the family's engines move to TypeScript).
Disposable, like the rest of `spikes/`.

The figures and what they say are in [`results/20261025-engine-host/summary.txt`](results/20261025-engine-host/summary.txt).
In short: an edit that recalculates 20,000 cells shows in 1.0 s on published Blazor WebAssembly, 1.0 s
from the .NET Worker interpreted, 0.56 s compiled ahead of time, and 11 ms from the TypeScript Worker
(15 ms with the TypeScript engine on the page), whose answers equal the C# engine's cell for cell. The
TypeScript page downloads 58 KB and paints in 0.17 s, against 2.7 to 5.7 MB and 2 to 5 s. The TypeScript
engine calculates Values only, so its times are a floor, not a port's. And **the 3.3 s the morning's
baseline recorded for Blazor WebAssembly was mostly the baseline's own configuration**: Bench.Host under
`dotnet run` serves a build whose runtime is set up for debugging.

## What is here

| | |
|---|---|
| `EngineHost/` | One .NET WebAssembly build (`Microsoft.NET.Sdk.WebAssembly`) of ExSheet.Engine, ExSheet.Xlsx, ExPivot.Engine, ExGrid.Data and ExGrid.Data.Xlsx. `Program.cs` holds the `[JSExport]` methods a Worker calls: open a Sheet Document, enter a cell and answer the Window, read a Window; take trades, answer a report Window as `PivotReportJson` writes it, apply a Change Batch; read and write an `.xlsx`, so both readers are in the download |
| `EngineProbe/` | `Probe.cs`, the engine calls of each gesture timed alone, run on the JIT by this console and in the Worker by `/probe` |
| `client/` | The TypeScript: `engine-worker.ts` starts .NET in a module Worker and answers requests in the order posted; `engine-client.ts` is the page's side, a promise per call; `engine-window-source.ts` is a Grid Source over the Window the engine answers; `sheet-page.ts` and `pivot-page.ts` draw it with ExGrid's Script Core; `core-data.ts` is render-bench's Sheet and trades |
| `client/src/ts-engine/` | The TypeScript engine: `sheet.ts` (Formulas of the scenario, the cells each cell is read by, recalculation in dependency order), `pivot.ts` (CorePivot's one layout, a Change Batch applied to the sums it reaches), `host.ts` (the same protocol the .NET Worker answers). `ts-engine-worker.ts` serves it in a Worker, `ts-engine-page.ts` on the page; `check-page.ts` compares its answers with the C# engine's |
| `serve.mjs` | Serves the pages at render-bench's paths (`/core/sheet`, `/core/pivot`), `/probe` and `/check`, the bundles, and one published `_framework`, Brotli-compressed; `ts` serves the TypeScript Worker in the .NET Worker's place, `ts-page` the TypeScript engine on the page |
| `tools/` | `cdp-probe.mjs` runs the probe in the Worker; `summarize-probe.mjs` prints it beside the JIT's; `cdp-check.mjs` runs `/check` |
| `results/` | The record |

render-bench gained three tools for this, usable on any host: `tools/cdp-boot.mjs` (the time to the first
painted cell and every byte downloaded, a Worker's included, its own script read from Resource Timing), `tools/summarize-boot.mjs`, and
`tools/serve-published.mjs` (a published Bench.Client served as a static host serves it).
`tools/cdp-core.mjs` now also keeps what a page records of its own side of each operation.

## How the Worker answers

- **One Worker for the page**, holding every engine in one runtime. `[JSExport]` methods take and give
  strings: a request crosses as `postMessage`, the Worker calls the export, and the answer is JSON. The
  TypeScript engine answers the same requests with the same JSON, from a Worker or from the other end of
  a MessageChannel on the page: the pages cannot tell which, and nothing in them changes.
- **An answer carries what it was asked against.** A Sheet's answers carry its version; the pivot's carry
  its Report Version. On the page, every write moves the source's epoch as it is posted, and a read
  answered for an older epoch is asked again, never drawn (spine rule 6). Nothing waits on a clock.
- **Writes are never refused for being late, and reads coalesce.** The Worker takes requests in the order
  posted and finishes each, an awaited one included, before the next. An edit is answered with the Window
  the grid holds, so one message each way paints it. One read is in flight at a time; when it lands, the
  next asks for what the grid needs then. Until the last write is answered the source says `loading`,
  which the Script Core paints as `ex-loading`.

The pages stand in for ExSheet's and ExPivot's Script Cores, which do not exist yet. The Sheet page takes
digits and Enter on the Focus as ExSheet's editor would and leaves every other key to the grid; it draws
columns A to Z, 70 px wide, rows 28 px. The pivot page draws the report's label column and value columns,
with CorePivot's controls. The data is the Blazor pages': CoreSheet's Sheet, written as a Sheet Document's
JSON, and CorePivot's 20,000 trades from the same seed (`DotNetRandom`), sent once as columns of JSON.

## Running it

The interpreter needs nothing beyond the repository's SDK. AOT needs the `wasm-tools` workload, which the
nix SDK cannot take (its store is read-only); a writable copy of it can:

```sh
D=$(nix develop -c bash -c 'echo $DOTNET_ROOT' | tail -1)
cp -rL "$D" ~/dotnet-wasm && chmod -R u+w ~/dotnet-wasm
DOTNET_ROOT=~/dotnet-wasm PATH=~/dotnet-wasm:$PATH dotnet workload install wasm-tools
```

Then, from the repository root:

```sh
nix develop -c dotnet publish -c Release spikes/engine-host/EngineHost -o /tmp/eh-interp
DOTNET_ROOT=~/dotnet-wasm PATH=~/dotnet-wasm:$PATH \
  dotnet publish -c Release spikes/engine-host/EngineHost -p:RunAOTCompilation=true -o /tmp/eh-aot
nix develop .#browser -c bash -c 'cd spikes/engine-host/client && npm ci && npm run typecheck && npm run build'

node spikes/engine-host/serve.mjs /tmp/eh-interp/wwwroot 5493      # or /tmp/eh-aot/wwwroot, or ts, or ts-page
/opt/pw-browsers/chromium --headless=new --remote-debugging-port=9333 --window-size=1400,1000 about:blank &
cd spikes/render-bench
CDP_PORT=9333 node tools/cdp-core.mjs http://127.0.0.1:5493 worker-interp sheet,pivot
CDP_PORT=9333 node tools/cdp-boot.mjs http://127.0.0.1:5493 worker-interp sheet,pivot
CDP_PORT=9333 node ../engine-host/tools/cdp-probe.mjs http://127.0.0.1:5493 interp
CDP_PORT=9333 node ../engine-host/tools/cdp-check.mjs http://127.0.0.1:5493       # needs a .NET build served
nix develop -c dotnet run -c Release --project ../engine-host/EngineProbe      # the JIT's probe
```

Run one host at a time, on a quiet machine: a build that has just finished moved Blazor's A1 edit from
1.07 s to 1.8 s in the run this record replaced (`results/20261025-engine-host/superseded/`).

## What it does not settle

- **What a TypeScript port costs at run time.** The TypeScript engine keeps none of what ExSheet.Engine
  keeps beside the Values (Steps, Spills, formats carried by Formulas, volatile functions); a port's
  recalculation will cost more than its 6 ms. What the port costs to write is estimated in the record.
- **ExSheet's and ExPivot's own Script Cores** are not written; the pages draw what render-bench's driver
  reads and nothing more. Their figures include ExGrid's Script Core and the crossing, not a product UI.
- **The page's frame during a recalculation** was not measured as frames: the driver scrolls only when
  nothing recalculates. With the TypeScript engine on the page, how long each call holds the page's
  thread is recorded instead (its `workerMs`).
- **A real user's machine.** This is headless software rendering in a 4-core container.
- **Blazor Server** was not run again; its column is the morning's, from another container.
