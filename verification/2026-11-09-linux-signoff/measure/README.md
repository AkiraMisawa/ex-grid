# The browser and circuit measurements for the 0.2.0 sign-off

Date: 2026-11-09. The code measured is `0326ce27`, `v0.2.0-rc.4`'s commit, X. Observational: recorded, never
gated (Definition of Done §1, §22 Step 6). What the numbers say is in [`../results.md`](../results.md), Step 6.

## Method

- **Machine**: a cloud container, Intel Xeon @ 2.10 GHz, 4 vCPUs, 15 GiB; Linux 6.18; .NET SDK 10.0.203
  through nix, runtime 10.0.7; Node 26.12.1; Playwright 1.62.1 driving Chrome for Testing 153.0.8010.12 as
  `chrome`, headed under xvfb at 2560×1600.
- **This container read slow.** The first run of every number, X's, came out 20–60% slower than the
  sign-off run of 2026-11-07 at rc.3 ([`../../2026-11-07-linux-signoff/measure`](../../2026-11-07-linux-signoff/measure/README.md)),
  in a container of the same kind, and so did code that did not change between the two, the CoreCLR CSV
  reader among it. So the browser measurements were run again with nothing beside them (`rerun/`), and then
  run once more on rc.3's code in this same container (`ab-rc3/`), to tell the code from the container.
- **PV-21, DA-17 and LV-15**: the DemoHost published in Release as WebAssembly, trimmed, without AOT, and
  served by `tests/browser/static-host.mjs` on port 5440; the demo API in Release on 8440 with
  `EXGRID_DEMO_TRADES=1000000`. The CSV is the page's own export of a million trades, 94,055,079 bytes
  (89.7 MiB). `EXGRID_MEASURE=pivot npx playwright test --project=chrome measure-pivot.spec.mjs`, then
  `EXGRID_MEASURE=live … measure-live.spec.mjs`. At X the harness is the one #544 fixed: it reads the refusal
  notice as hidden right after `#pivot-reset`, and the cap's refusal by the Script Core's sentence, and all
  seven tests pass unchanged ([`pv21/run.log`](pv21/run.log), [`lv15/run.log`](lv15/run.log)).
- **The quiet rerun** ([`rerun/`](rerun/)): the same two specs on the same published build, with nothing else
  running. [`rerun/metrics.json`](rerun/metrics.json) holds its numbers, and the first run's circuit numbers
  (SRV-6, SH-19), which were not run again. These are X's numbers in `../results.md`.
- **The A/B** ([`ab-rc3/`](ab-rc3/)): rc.3's commit, `bd2baee6`, in a worktree of its own, published and
  served the same way on the same ports, right after the rerun. rc.3's harness needed the same change as on
  2026-11-07, [`ab-rc3/measure-pivot.local.diff`](ab-rc3/measure-pivot.local.diff), which reads the notice as
  hidden after the next layout is built, not after the reset, since rc.3 leaves it shown there (the defect rc.4
  fixes). [`ab-rc3/metrics.json`](ab-rc3/metrics.json) is rc.3's numbers.
- **SRV-6 and SH-19 on a circuit**: on the Server host the suite starts, `EXGRID_HOSTING=server`, port 5498:
  [`measure-server.log`](measure-server.log) and [`measure-sheet.log`](measure-sheet.log).
  [`metrics.json`](metrics.json) is the first run's whole record, kept as
  [`metrics-first-run.json`](metrics-first-run.json) too.
- **PF-8**: `spikes/render-bench/tools/cdp-core.mjs` against Bench.React built by Vite and served by
  `vite preview` on 5496 (`script-react`, scenarios grid and template), and against Bench.Client published in
  Release and served by `tools/serve-published.mjs` on 5495 (`script-blazor-wasm-published`, every scenario).
  Both ran in `/usr/bin/google-chrome --headless=new` on CDP port 9333, five rounds each, one arm at a time,
  nothing beside them; then both arms again on rc.3's code ([`ab-rc3/pf8/`](ab-rc3/pf8/)).
  [`pf8/summary.txt`](pf8/summary.txt) is `tools/summarize-core.mjs` over `results/20261030-script-core`'s two
  files, the two of 2026-11-07 (`../../2026-11-07-linux-signoff/measure/pf8/`) and X's two;
  [`pf8/ab-summary.txt`](pf8/ab-summary.txt) over rc.3's two of today and X's two, rc.3 first.
- **MEM-7, PV-48 and LV-23** were not run. Their harnesses measured the C# core's render and the report
  Window path, both since removed. `../results.md` explains why.
