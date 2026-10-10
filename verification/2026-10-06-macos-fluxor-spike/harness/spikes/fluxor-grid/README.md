# fluxor-grid — ExGrid over a Fluxor store

A disposable spike: a trade blotter whose state lives in a Fluxor feature, drawn by ExGrid two ways.
The findings are in
[`verification/2026-10-06-macos-fluxor-spike`](../../verification/2026-10-06-macos-fluxor-spike/README.md).
Fluxor is referenced here and nowhere under `src/` or `samples/` (AGENTS.md).

| Path | What it is |
|---|---|
| `FluxorGrid.App/Store` | The feature (`TradesState`), its actions, its reducers, and the live feed the effect drives |
| `FluxorGrid.App/Pages/PushPage.razor` | `/w1`: the store pushes the Window |
| `FluxorGrid.App/Pages/SourcePage.razor` | `/w2`: the store's list handed to `GridSource.From` under a Row Key; `?edits=source` writes to the source first; `?gather=0` turns gathering off |
| `FluxorGrid.App/Grid` | The columns, the intents turned into actions, and the instruments: a render counter, the census, the panel |
| `FluxorGrid.Wasm` | The WebAssembly host, port 5899 |
| `FluxorGrid.Server` | The Blazor Server host, port 5898, not prerendered |
| `check/check.mjs` | The browser check (headless Chrome) |

The pages take the feed from their query: `interval` (ms), `perTick`, `ticks` (0 for no end) and
`addEvery`.

## Running it

```sh
# from the repository root
git add spikes/fluxor-grid                      # flakes see tracked files only
nix develop -c dotnet build spikes/fluxor-grid/FluxorGrid.Wasm/FluxorGrid.Wasm.csproj
nix develop -c dotnet build spikes/fluxor-grid/FluxorGrid.Server/FluxorGrid.Server.csproj
spikes/fluxor-grid/host.sh wasm                 # prints the PID on 5899
spikes/fluxor-grid/host.sh server               # prints the PID on 5898

cd spikes/fluxor-grid/check
nix develop ../../..#browser -c npm install
nix develop ../../..#browser -c node check.mjs host=wasm variants=w1,w2,w2src,w2g0 checks=1,2,3,4,5,6
nix develop ../../..#browser -c node check.mjs host=server variants=w1,w2,w2src,w2g0 checks=1,2,3,4,5,7
```

Stop each host with `kill <pid>`, by the PID `host.sh` printed, never by a pattern. Results go to
`check/out/`, which git ignores. `race.mjs` and `retention.mjs` are the two probes the
verification record quotes.
