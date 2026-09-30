# The hole's cost — 2026-09-29, macOS

**Scope: ADR-0008, "Excel's look for the Focus and a single range", and ticket 24's "the hole adds
nothing measurable to a drag: measured before it is claimed".** A range holding the Focus is
painted with a hole where the Focus is (an inline `--ex-range-hole` polygon clipping the range's
`::before` tint), and a range isolates its painting so that its tint lies beneath its outline.
What follows is what those cost in the browser's main thread during a selecting drag.

**The raw per-drag rows of the figures below were not kept.** The first round aggregated them in
memory and kept only the medians, and for one comparison the quartiles. The scripts here replace
the scratch scripts it used, with the same procedure, and `run.mjs` writes every drag to
`results.json`. **A re-run with them is pending**, on a machine no other session is loading.

## What is driven

`drag-cost.mjs` opens `/sheet` in headless Chrome at a device scale of 2 in a 1400×1000 viewport,
presses a start cell and moves the pointer one cell at a time, ten times: one row down each time
and one column right for the first four. The range grows from 2 to 55 cells, with the Focus in its
corner throughout. Each move waits until the range's painted height shows it landed, so a drag is
ten renders on either host. Around the ten moves it reads the Chrome DevTools Protocol's
`Performance.getMetrics` with thread-tick timing: `TaskDuration` (all main-thread work),
`ScriptDuration`, `RecalcStyleDuration` and `LayoutDuration`, in ms, summed over the drag. The wait
polls on animation frames, so its own script is in every figure alike. Start cells: B3 (`r2c1`), and
A3 (`r2c0`), which is pinned, so the range crosses the pinned boundary and is painted whole in both
layers and clipped.

`EXGRID_INJECT` adds a stylesheet first, which is how one feature is switched off on the build
that has it: the hole (`.ex-range::before { clip-path: none !important; }`), or the isolation
(`.ex-range { isolation: auto !important; } .ex-range::before { z-index: auto !important; }`).

`run.mjs` runs each comparison in rounds, interleaving its two sides so drift in the machine falls
on both: four rounds of one batch per side, five drags per batch, each batch a fresh browser. It
prints the medians and interquartile ranges, and writes `results.json`. A batch that times out
(a move's render not landing within 30 s) is counted and left out.

## How to run it

From the repository root, with `npm ci` done in `tests/ExGrid.Browser` (Playwright comes from
there, or from the path in `EXGRID_PLAYWRIGHT`) and Google Chrome installed:

```sh
# Release builds of the two commits, each published into a directory of its own
git worktree add /tmp/hole-before 150d643
nix develop -c dotnet publish /tmp/hole-before/samples/ExGrid.DemoHost.Server -c Release -o /tmp/before-server
nix develop -c dotnet publish /tmp/hole-before/samples/ExGrid.DemoHost -c Release -o /tmp/before-wasm
nix develop -c dotnet publish samples/ExGrid.DemoHost.Server -c Release -o /tmp/after-server
nix develop -c dotnet publish samples/ExGrid.DemoHost -c Release -o /tmp/after-wasm

# The four hosts on the ports run.mjs expects
(cd /tmp/before-server && nix develop "$OLDPWD" -c dotnet ExGrid.DemoHost.Server.dll --urls http://localhost:5313) &
(cd /tmp/after-server && nix develop "$OLDPWD" -c dotnet ExGrid.DemoHost.Server.dll --urls http://localhost:5316) &
node verification/2026-09-29-selection-hole/static.mjs /tmp/before-wasm/wwwroot 5314 &
node verification/2026-09-29-selection-hole/static.mjs /tmp/after-wasm/wwwroot 5315 &

node verification/2026-09-29-selection-hole/run.mjs
```

`150d643` is the decision commit, whose code is the pre-change code. The hosts can be moved with
`EXGRID_BEFORE_SERVER`, `EXGRID_AFTER_SERVER`, `EXGRID_BEFORE_WASM` and `EXGRID_AFTER_WASM`. Stop
each host by its own PID when done.

## The figures reported in the first round

Measured on 2026-09-29 between about 22:10 and 22:35 (UTC+1), on macOS 26.6.2 (25G83), an Apple M4
Pro with 12 cores, with the installed Google Chrome, headless, with the scratch predecessors of these
scripts. The load average was not recorded. It was lower than in the re-run attempted at 23:25,
which other sessions' layer-3 runs had at 60 to 85 on 12 cores: there the WebAssembly medians rose
from 27 to 33 ms to between 45 and 69 ms, and batches timed out. That attempt was stopped and its
figures are not used.

The builds were Release publishes of the pre-change code (`150d643`) as **before**, and of the
changed code as it stood at `3d0f5f5` as **after**. At `3d0f5f5` the range's tint was not yet
isolated beneath its outline. The isolation comparison ran later, on a Debug `dotnet run` Server host
of `f885663`. Main-thread time per ten-render drag, in ms:

| Comparison | Host | Start | n per side | Before | After |
|---|---|---|---|---|---|
| Before against after | Server | B3 | 20 | 13.2 | 15.0 |
| Before against after | Server | A3, across the pinned boundary | 20 | 12.7 | 14.9 |
| Before against after, repeated (six rounds) | Server | B3 | 30 | 14.1, IQR 12.0-19.8 | 14.7, IQR 12.4-17.7 |
| Before against after | WebAssembly | B3 | 20 | 28.2 | 29.0 |
| Before against after | WebAssembly | A3, across the pinned boundary | 20 | 28.0 | 26.8 |

| Comparison, on the changed build | Host | n per side | Shipped | Switched off |
|---|---|---|---|---|
| The hole | Server | 20 | 16.2 | 15.6 |
| The hole | WebAssembly | 20 | 33.3 | 32.6 |
| The isolation (tint beneath the outline) | Server, Debug | 20 | 13.9 | 15.1 |

Style recalculation rose by about 0.1 to 0.2 ms per drag from B3 (0.4 to 0.6, the `::before`), and
by 0.5 ms from A3 on the Server host (0.4 to 0.9). Script moved by 0.2 to 0.3 ms on the Server
host, and by −2.0 to +0.4 ms on WebAssembly, where it includes the C# render; layout by 0.1 ms or
less.

What this supports: switching the hole off changed the drag by 0.6 to 0.7 ms, inside the spread of
the repeated comparison, whose interquartile ranges are 5 to 8 ms wide. The change as a whole moved
the Server medians by 0.6 to 2.2 ms in one direction and the WebAssembly medians by −1.2 to +0.8 ms
in both. Nothing was measured that is attributable to the hole. The figures are medians of small
samples without their rows, so they are a record of what was reported rather than evidence anyone can
re-derive, until the pending re-run replaces them.
