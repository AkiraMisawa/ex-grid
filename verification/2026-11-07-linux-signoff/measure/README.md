# The browser and circuit measurements for the 0.2.0 sign-off

Date: 2026-11-07. The code measured is `bd2baee6`, `v0.2.0-rc.3`'s commit. Observational: recorded, never
gated (Definition of Done §1, §22 Step 6). What the numbers say against 0.1.0 is in the
sign-off's [`results.md`](../../2026-11-09-linux-signoff/results.md), Step 6, which reads them through this run.

## Method

- **Machine**: a cloud container, Intel Xeon @ 2.10 GHz, 4 vCPUs, 15 GiB; Linux 6.18; .NET SDK 10.0.203
  through nix, runtime 10.0.7; Node 26.12.1; Playwright 1.62.1 driving Chrome for Testing 153.0.8010.12 as
  `chrome`, headed under xvfb at 2560×1600. Nothing else ran, but for the overlap named under PF-8.
- **Not an A/B.** 0.1.0's record compared its code with the code before it on one container. This one
  compares with 0.1.0's record, made in a container of the same kind but not the same one, which that
  record found reads 10–60% apart for the same code. A move of that size says little either way.
- **PV-21, DA-17 and LV-15**: the DemoHost published in Release as WebAssembly, trimmed, without AOT, and
  served by `tests/browser/static-host.mjs` on port 5440; the demo API in Release on 8440 with
  `EXGRID_DEMO_TRADES=1000000`. The CSV is the page's own export of a million trades, 94,055,079 bytes
  (89.7 MiB), written by a copy of 0.1.0's `csvgen` that was never committed.
  `EXGRID_MEASURE=pivot npx playwright test --project=chrome measure-pivot.spec.mjs`, then
  `EXGRID_MEASURE=live … measure-live.spec.mjs`.
- **PV-21's questions**: in the first run, both tests of questions failed on the harness rather than the
  product. It read the refusal notice as absent, but the Script Core keeps it in the page, hidden; and it
  matched the cap's refusal by the C# core's words. [`pv21/measure-pivot.local.diff`](pv21/measure-pivot.local.diff)
  is the change they ran with, each test run alone: [`pv21/run-questions.log`](pv21/run-questions.log) and
  [`pv21/run-cap.log`](pv21/run-cap.log). [`metrics-first-run.json`](metrics-first-run.json) is the first
  run's record. [`metrics.json`](metrics.json) is the first run's other numbers, with these two runs'
  numbers in place of the failed tests'.
- **SRV-6 and SH-19 on a circuit**: on the Server host the suite starts, `EXGRID_HOSTING=server`, port 5498:
  [`measure-server.log`](measure-server.log) and [`measure-sheet.log`](measure-sheet.log).
- **PF-8**: `spikes/render-bench/tools/cdp-core.mjs` against Bench.React built by Vite and served by
  `vite preview` on 5496 (`script-react`, scenarios grid and template), and against Bench.Client published in
  Release and served by `tools/serve-published.mjs` on 5495 (`script-blazor-wasm-published`, every scenario).
  Both ran in `/usr/bin/google-chrome --headless=new` on CDP port 9333, five rounds each, one arm at a time.
  The first pair of runs overlapped PV-21's reruns; they were set aside and both arms run again, alone. The
  last minute of the WebAssembly arm's grid scenario ran beside the sign-off's TypeScript type check.
  [`pf8/summary.txt`](pf8/summary.txt) is `tools/summarize-core.mjs` over
  `results/20261030-script-core`'s two files and these two.
- **MEM-7, PV-48 and LV-23** were not run. Their harnesses measured the C# core's render and the report
  Window path, both since removed. The sign-off's `results.md` explains why.
