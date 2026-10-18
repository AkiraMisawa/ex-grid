# The browser and live measurements for the 0.1.0 sign-off, on 0.1.0-rc.3's code

Date: 2026-10-18. The code measured is `release/0.1` at `28ab5e1bf8c1e4c71dbcb19e9bd430cbd0612f48`. 0.1.0-rc.3 is tagged on
`cff4a0dd94d0bbbb0fd171e5a276dabebbdb1c07`, which adds only `verification/` to it, so this is X's code. Observational: recorded, never gated (Definition of Done §1, §22 Step 6).

## Method

- **Machine**: a cloud container, Intel Xeon @ 2.10 GHz, 4 vCPUs, 15 GiB; Linux 6.18; .NET SDK 10.0.203
  through nix, runtime 10.0.7; node 24.14.1; Chromium 141.0.7390.37 (Playwright's build) as `chrome`,
  under xvfb. Nothing else ran; the load average stayed between 0.9 and 2.0 (`ab/run.log`), above 3 once,
  for the 30 s of an LV-15 run.
- **The same container, the code of the last record beside it.** [`2026-10-10-linux-merged-live-costs`](../../2026-10-10-linux-merged-live-costs/README.md)
  found that one kind of container reads 10–25% apart from one start to the next for the same code. A
  first PV-21 run on this one read 20–60% above that record's, so the comparison here is an A/B on one
  container, not a comparison of records: the reference side, **ref**, is `554b3c20`, PR #70's merge, the
  code whose PV-21 and PV-48 that record measured. Its DemoHost was published with `-p:NuGetAudit=false`:
  its SignalR package has since been reported vulnerable, which fails a restore.
- **Hosts**: each side's DemoHost published in Release as WebAssembly, trimmed, without AOT and without
  `wasm-tools`, served by `tests/ExGrid.Browser/static-host.mjs`; each side's demo API in Release with
  `EXGRID_DEMO_TRADES=1000000`. The CSV is the page's own `DemoCsv.TradeExportFileAsync` export of a million
  trades (89.7 MiB, `harness/csvgen` of the 2026-10-10 record).
- **Harness**: as the 2026-10-10 record's `harness/`, copied to where it builds on each side and never committed:
  `PerfWholeUpdate.cs` (PV-48 on CoreCLR, `DOTNET_TieredCompilation=0`, its marks filled as the branch side's were),
  `PerfLiveMemoryPage.razor` and `perf-memory.spec.mjs` (LV-23). `measure-pivot.spec.mjs` (PV-21, DA-17) and
  `measure-live.spec.mjs` (LV-15) are each side's own, unchanged.
- **Order**: PV-21 ref, rc3, rc3, ref; LV-15 rc3, ref, ref, rc3; PV-48 three rounds of nine runs a setting
  (round 1 ref then rc3, round 2 rc3 then ref, both advancing the clock 1 ms; round 3 ref then rc3, 5 s);
  LV-23 rc3 then ref at 10, 100 and 400 inner Items. SRV-6 (`measure-server.spec.mjs`) and SH-19
  (`measure-sheet.spec.mjs`) ran once on rc3, on the Server host the suite starts.
- **Records**: PV-21 and LV-15 are read from each run's printed records (`ab/*.log`), because the record
  directory a run writes is shared by the runs of a day; the comparison is `tools/ablog.py`.

## In brief

- **PV-21 and DA-17 are level with ref on this container**, within ±20% for every load, read, question,
  live batch and refusal. Where rc3 is more than 20% slower in both of its runs it is a gesture's first
  answer under 0.1 s: opening a field's menu 28 ms against 17, sorting Z to A 57 against 42, opening the
  report filter 29 against 20, the first answer of a filter to USD or (All) 70–76 against 49–55. Each
  stays inside PV-21's 0.1 s.
- **LV-15 did not change**: 1,000 changes to a million rows, 65–66 ms from Apply to the frame against
  64–69, the grid inside the batch's frame in all 12 runs on both sides.
- **PV-48 is 10–70% slower than ref and well inside 0.2 s**: 29–38 ms against 26–30 for 1,000 changes at
  401,001 report rows, 3.7–7.5 ms against 3.1–5.4 for one change; about 0.9 MiB more allocated an update,
  and no collection on either side. Each setting is nine runs, and the rounds disagree by up to half, so
  the size of the difference is not settled here.
- **LV-23: the heap levels off on both sides**, at 22, 132 and 406 MB after 20 redraws of 11,001, 101,001
  and 401,001 report rows (ref 23, 130, 399), with no error. A redraw's median is 148–184 ms against
  94–154 (one run a side).
- **SRV-6** (no earlier figure): the hover band's lag behind the pointer, median (max), 14 (24) ms at no
  round trip, 67 (68) at 50 ms, 168 (183) at 150 ms; frames sent and received a second 39/28, 49/35, 50/35.
- **SH-19** on the Server host, key to frame, median: =S 22.4 ms and =SU 23.4 at no round trip; 161.6 and
  202.5 at 150 ms. Against the pools of 2026-10-11 (22.4, 19.9, 160.0, 199.0 and 25.3, 24.9, 163.9, 168.0)
  only =SU at 150 ms is more than 20% above one of them (+20.5% on the second pool), and level with the first.

## Tables

PV-48, ms (median of nine) and MiB allocated an update, by round:

| Report rows | Changes | Round | ref ms | rc3 ms | rc3/ref | MiB ref -> rc3 | GC ms ref -> rc3 | first report ms ref -> rc3 |
|---|---|---|---|---|---|---|---|---|
| 11,001 | 1 | r1 | 4.04 | 3.70 | 0.91 | 1.0 -> 1.9 | 0 -> 0 | 2388 -> 2064 |
| 11,001 | 1 | r2 | 3.37 | 5.87 | 1.74 | 1.0 -> 1.9 | 0 -> 0 | 1855 -> 2170 |
| 11,001 | 1 | r3 | 3.11 | 3.73 | 1.20 | 0.9 -> 1.1 | 0 -> 0 | 2127 -> 2087 |
| 11,001 | 1,000 | r1 | 8.35 | 9.25 | 1.11 | 2.4 -> 3.4 | 0 -> 0 | 2388 -> 2064 |
| 11,001 | 1,000 | r2 | 6.70 | 8.01 | 1.20 | 2.4 -> 3.4 | 0 -> 0 | 1855 -> 2170 |
| 11,001 | 1,000 | r3 | 5.98 | 7.59 | 1.27 | 2.4 -> 2.6 | 0 -> 0 | 2127 -> 2087 |
| 101,001 | 1 | r1 | 5.01 | 5.50 | 1.10 | 1.0 -> 1.9 | 0 -> 0 | 237 -> 235 |
| 101,001 | 1 | r2 | 4.36 | 7.28 | 1.67 | 1.0 -> 1.9 | 0 -> 0 | 260 -> 237 |
| 101,001 | 1 | r3 | 3.37 | 4.81 | 1.42 | 0.9 -> 1.1 | 0 -> 0 | 253 -> 274 |
| 101,001 | 1,000 | r1 | 23.39 | 19.11 | 0.82 | 8.1 -> 10.0 | 0 -> 0 | 237 -> 235 |
| 101,001 | 1,000 | r2 | 18.17 | 29.28 | 1.61 | 8.1 -> 10.0 | 0 -> 0 | 260 -> 237 |
| 101,001 | 1,000 | r3 | 11.16 | 17.33 | 1.55 | 7.8 -> 8.0 | 0 -> 0 | 253 -> 274 |
| 401,001 | 1 | r1 | 5.42 | 7.48 | 1.38 | 1.0 -> 1.9 | 0 -> 0 | 1405 -> 1228 |
| 401,001 | 1 | r2 | 5.22 | 7.51 | 1.44 | 1.0 -> 1.9 | 0 -> 0 | 1398 -> 1270 |
| 401,001 | 1 | r3 | 4.81 | 5.90 | 1.23 | 0.9 -> 1.1 | 0 -> 0 | 1131 -> 1416 |
| 401,001 | 1,000 | r1 | 29.52 | 38.13 | 1.29 | 17.8 -> 19.8 | 0 -> 0 | 1405 -> 1228 |
| 401,001 | 1,000 | r2 | 30.06 | 38.13 | 1.27 | 17.8 -> 19.8 | 0 -> 0 | 1398 -> 1270 |
| 401,001 | 1,000 | r3 | 26.24 | 28.89 | 1.10 | 17.6 -> 17.8 | 0 -> 0 | 1131 -> 1416 |

LV-23, one run a side:

| b | side | redraw median ms (2-20) | first redraw ms | managed MB at 0 / 6 / 20 | linear MB at 20 | errors |
|---|---|---|---|---|---|---|
| 10 | ref | 94 | 358 | 15 / 20.7 / 22.6 | 96 | 0 |
| 10 | rc3 | 148 | 274 | 15.2 / 20.9 / 22.1 | 96 | 0 |
| 100 | ref | 132 | 375 | 91.8 / 122 / 130 | 239 | 0 |
| 100 | rc3 | 148 | 355 | 93.4 / 123.6 / 131.6 | 239 | 0 |
| 400 | ref | 154 | 576 | 337.6 / 398.2 / 399.3 | 687 | 0 |
| 400 | rc3 | 184 | 537 | 343.8 / 404.4 / 405.5 | 687 | 0 |

PV-21 and DA-17, every figure of both sides' runs: `ab/pv21-table.txt`. LV-15: `ab/lv15-table.txt`.

## PF-8

`spikes/render-bench`'s Bench.Host in Release, "Measure all modes" headless over CDP (`spikes/render-bench/tools/cdp-run.mjs`,
40 iterations, a step of 50 rows, 20 columns), on rc3. The entry is `pf8/20261018-122858-082.json`; it is
committed under `spikes/render-bench/results/` on `main`, since the 0.1.0 tag may add only `verification/`
and `docs/releases/` to rc.3. Median / p95 / max ms a step at 800 cells: Direct 15.3 / 26.3 / 50.1,
Accessor 15.7 / 19.2 / 23.6, AccessorMeta 20.5 / 29.9 / 32.5, RowComponent 25.3 / 31.9 / 35.5,
RowComponentTone 27.3 / 32.6 / 38.1, Component 74.6 / 88.5 / 91.9.
