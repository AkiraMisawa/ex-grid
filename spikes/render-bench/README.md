# render-bench — a disposable render-cost harness

Built to measure, before the specification was frozen, **how much the shape of the API costs at
render time**. Throw it away once the conclusions are in.

## What it measures

The hypothesis that cost is decided by **the number of cells touched per render**, not by the row
count. The data is already in memory, and **only rendering is measured** (fetching, sorting and
filtering are excluded — [ADR-0001](../../docs/adr/0001-consumer-pushes-the-window-grid-does-not-fetch.md)
puts them on the Consumer side, so the grid never walks every row).

Six modes stack up, isolating **the cost of one design decision each**:

| Mode | What it adds |
|---|---|
| `Direct` | the floor with no abstraction: plain markup, direct field access |
| `Accessor` | a per-column `Func<Row, object>` accessor (boxes every decimal) |
| `AccessorMeta` | a per-cell metadata lookup (a Cell State probe, keyed on (group, metric)) |
| `RowComponent` | the boundary at the row: row is a component, cells stay plain markup |
| `RowComponentTone` | `RowComponent` + a per-cell tone rule: a Consumer delegate on the value answering a closed enum, painted as an interned class ([ADR-0006](../../docs/adr/0006-grid-owns-a-generic-cell-state-vocabulary.md)) |
| `Component` | the boundary at the cell: every cell is a Blazor component |

There is a second benchmark for **painting the selection**: a CSS class per cell versus a single
absolutely positioned overlay, with scrolling held fixed so only the selection moves.

The bar is **16.6 ms** (one frame at 60fps). A median past that is what "sluggish" means.

## Running it

```sh
nix develop -c dotnet run -c Release --project Bench.Host --urls http://0.0.0.0:5199
```

Open <http://localhost:5199>. Under WSL2, if `localhost` does not reach it from the Windows
browser, use the address from `hostname -I`.

1. Pick a preset (`Fling (40×20, 50 rows)` is the harshest realistic case)
2. **Measure all modes**
3. **Save results to the server** → lands in `spikes/render-bench/results/{timestamp}.json`

For the felt experience of manual scrolling: `Start frame measurement` → drag the grid → `Stop and
summarise`.

A headless run is possible with `nix develop .#browser`, driven over the Chrome DevTools Protocol.

## Caveats on the measurement conditions

- **Measure in Release.** Debug is a different kind of slow.
- The programmatic measurement covers **Blazor's render plus the DOM update**. `StateHasChanged()`
  can complete a render synchronously, so the browser's layout and paint are left outside the
  timed region, with 1 ms yielded after each iteration. **End-to-end frame time is what the manual
  scrolling measurement is for.**
- Headless Chromium works but renders in software, so **its absolute numbers diverge from real
  hardware**. Use headless to reproduce exceptions; take numbers on a real browser. (The same
  measurement gave a 11.7 ms maximum headless and 19.7 ms on real hardware.)
- **No AOT** (it needs the `wasm-tools` workload). These are Release IL numbers; AOT may leave more
  headroom.
- 5,000 rows are generated and cycled with a modulus. To confirm that render cost does not depend
  on the row count, change `TotalRows` and re-run.

## This code is deliberately a bad example

Two things here **must not be carried into product code**
([ADR-0018](../../docs/adr/0018-multiple-instances-must-be-independent.md)):

- CSS class names `.r` `.c` `.sel` `.window` `.scroller` — no prefix; they would collide with a
  host application
- `window.bench` — a single global; a second instance would break the first

They are acceptable only because this is disposable.
