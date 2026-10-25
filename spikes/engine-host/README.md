# The Engine Host Worker (spike)

ADR-0370 section 2 puts ExSheet's and ExPivot's engines, under React, in a Web Worker running .NET
WebAssembly; section 6 asks for that Worker to be measured before a product's first step is called done.
This is that Worker as a spike, and the measurement. It ships nothing. Disposable, like the rest of
`spikes/`.

The figures and what they say are in [`results/20261025-engine-host/summary.txt`](results/20261025-engine-host/summary.txt).
In short: the Worker edits render-bench's Sheet in 0.97 s interpreted and 0.51 s compiled ahead of time,
against 1.07 s for a published Blazor WebAssembly client; it downloads 2.7 MB (interpreter) or 5.7 MB
(AOT) against Blazor's 4.0 MB; and **the 3.3 s the morning's baseline recorded for Blazor WebAssembly was
mostly the baseline's own configuration**: Bench.Host under `dotnet run` serves a build whose runtime is
set up for debugging.

## What is here

| | |
|---|---|
| `EngineHost/` | One .NET WebAssembly build (`Microsoft.NET.Sdk.WebAssembly`) of ExSheet.Engine, ExSheet.Xlsx, ExPivot.Engine, ExGrid.Data and ExGrid.Data.Xlsx. `Program.cs` holds the `[JSExport]` methods a Worker calls: open a Sheet Document, enter a cell and answer the Window, read a Window; take trades, answer a report Window as `PivotReportJson` writes it, apply a Change Batch; read and write an `.xlsx`, so both readers are in the download |
| `EngineProbe/` | `Probe.cs`, the engine calls of each gesture timed alone, run on the JIT by this console and in the Worker by `/probe` |
| `client/` | The TypeScript: `engine-worker.ts` starts .NET in a module Worker and answers requests in the order posted; `engine-client.ts` is the page's side, a promise per call; `engine-window-source.ts` is a Grid Source over the Window the Worker answers; `sheet-page.ts` and `pivot-page.ts` draw it with ExGrid's Script Core |
| `serve.mjs` | Serves the pages at render-bench's paths (`/core/sheet`, `/core/pivot`), the bundles, and one published `_framework`, Brotli-compressed |
| `tools/` | `cdp-probe.mjs` runs the probe in the Worker; `summarize-probe.mjs` prints it beside the JIT's |
| `results/` | The record |

render-bench gained three tools for this, usable on any host: `tools/cdp-boot.mjs` (the time to the first
painted cell and every byte downloaded, a Worker's included), `tools/summarize-boot.mjs`, and
`tools/serve-published.mjs` (a published Bench.Client served as a static host serves it).
`tools/cdp-core.mjs` now also keeps what a page records of its own side of each operation.

## How the Worker answers

- **One Worker for the page**, holding every engine in one runtime. `[JSExport]` methods take and give
  strings: a request crosses as `postMessage`, the Worker calls the export, and the answer is JSON.
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

node spikes/engine-host/serve.mjs /tmp/eh-interp/wwwroot 5493      # or /tmp/eh-aot/wwwroot
/opt/pw-browsers/chromium --headless=new --remote-debugging-port=9333 --window-size=1400,1000 about:blank &
cd spikes/render-bench
CDP_PORT=9333 node tools/cdp-core.mjs http://127.0.0.1:5493 worker-interp sheet,pivot
CDP_PORT=9333 node tools/cdp-boot.mjs http://127.0.0.1:5493 worker-interp sheet,pivot
CDP_PORT=9333 node ../engine-host/tools/cdp-probe.mjs http://127.0.0.1:5493 interp
nix develop -c dotnet run -c Release --project ../engine-host/EngineProbe      # the JIT's probe
```

Run one host at a time, on a quiet machine: a build that has just finished moved Blazor's A1 edit from
1.07 s to 1.8 s in the run this record replaced (`results/20261025-engine-host/superseded/`).

## What it does not settle

- **ExSheet's and ExPivot's own Script Cores** are not written; the pages draw what render-bench's driver
  reads and nothing more. Their figures include ExGrid's Script Core and the crossing, not a product UI.
- **The page's frame during a recalculation** is the Worker's purpose and was not measured: the driver
  scrolls only when nothing recalculates.
- **A real user's machine.** This is headless software rendering in a 4-core container.
- **Blazor Server** was not run again; its column is the morning's, from another container.
