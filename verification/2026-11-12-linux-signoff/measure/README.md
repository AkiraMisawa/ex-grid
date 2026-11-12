# The browser and circuit measurements for the 0.2.1 sign-off

Date: 2026-11-11 (UTC). The code measured is `85a7ea06`, `v0.2.1-rc.1`'s commit, X. Observational: recorded, never
gated (Definition of Done §1, §22 Step 6). What the numbers say is in [`../results.md`](../results.md), Step 6.

## Method

- **Machine**: a cloud container, Intel Xeon @ 2.10 GHz, 4 vCPUs, 15 GiB; Ubuntu 24.04.5 LTS, Linux 6.18; .NET
  SDK 10.0.203 through nix, runtime 10.0.7; Node 26.12.1; Playwright 1.62.1 driving Chrome for Testing
  153.0.8010.12 as `chrome`, headed under xvfb at 2560×1600. Each measurement ran with nothing beside it.
- **One run, and no A/B.** Every number was measured once at X. The comparison is with 0.2.0's record,
  [`../../2026-11-09-linux-signoff/measure`](../../2026-11-09-linux-signoff/measure/README.md), taken two days
  before in another container of the same kind: its quiet rerun for the browser numbers, its first run for the
  circuit's, its `pf8/` for PF-8. 0.2.0's code was not measured again in this container, so a move between the
  two records is the code's or the container's, and this record tells the two apart only where the code a
  number measures did not change. That record found one container of this kind 20–60% slower than another on
  unchanged code.
- **PV-21, DA-17 and LV-15**: the DemoHost published in Release as WebAssembly, trimmed, without AOT
  ([`pv21/publish.log`](pv21/publish.log)), and served by `tests/browser/static-host.mjs` on port 5440; the demo
  API in Release on 8440 with `EXGRID_DEMO_TRADES=1000000` ([`pv21/api-build.log`](pv21/api-build.log),
  [`pv21/api.log`](pv21/api.log)). The CSV is the page's own export of a million trades, 94,055,079 bytes
  (89.7 MiB), written by `DemoCsv.TradeExportFileAsync` ([`pv21/csvgen.log`](pv21/csvgen.log)).
  `EXGRID_MEASURE=pivot EXGRID_MEASURE_CSV=… npx playwright test --project=chrome measure-pivot.spec.mjs`, all
  seven tests passed as committed ([`pv21/run.log`](pv21/run.log)); then `EXGRID_MEASURE=live …
  measure-live.spec.mjs` ([`lv15/run.log`](lv15/run.log)).
- **A line in `pv21/run.log` that is not the product's.** The suite tried to start a demo API of its own on
  8440 while the million-trade one was still coming up, and that second process failed to bind the port
  (`Hosting failed to start … address already in use`). The page asked the million-trade API, as the Arrow
  read's `trades: 1000000` and every label of the run say; no test saw the line, and none of it is the page's
  console.
- **SRV-6 and SH-19 on a circuit**: on the Server host the suite starts, `EXGRID_HOSTING=server`, port 5498:
  [`measure-server.log`](measure-server.log) (`EXGRID_MEASURE=pointer`, `measure.spec.mjs`) and
  [`measure-sheet.log`](measure-sheet.log) (`EXGRID_MEASURE=sheet`, `measure-sheet.spec.mjs`).
- **[`metrics.json`](metrics.json)** is the four runs' records in one file: the suite writes one per run, and each
  was moved here and merged by key, none overwriting another.
- **PF-8**: `spikes/render-bench/tools/cdp-core.mjs` against Bench.React built by Vite and served by
  `vite preview` on 5496 (`script-react`, scenarios grid and template), and against Bench.Client published in
  Release and served by `tools/serve-published.mjs` on 5495 (`script-blazor-wasm-published`, every scenario).
  Both ran in `/usr/bin/google-chrome --headless=new` on CDP port 9333, five rounds each, one arm at a time.
  [`pf8/summary.txt`](pf8/summary.txt) is `tools/summarize-core.mjs` over 0.2.0's two files
  (`../../2026-11-09-linux-signoff/measure/pf8/`) and X's two, 0.2.0 first.
- **MEM-7, PV-48 and LV-23** were not run. Their harnesses measured the C# core's render and the report
  Window path, both since removed. `../results.md` says so in Step 6.
