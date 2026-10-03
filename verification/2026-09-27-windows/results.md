# Verification — 2026-09-27, Windows

**Scope: Part B of [`verify-on-windows.md`](../../docs/specs/exsheet/verify-on-windows.md).** The
existing layer 3 suite, on Chrome and Edge and on both hosts, on a real Windows desktop. This run
covers **DC-1** (§26): every declaration ExSheet added to ExGrid is off on the existing pages, so
the whole existing suite must pass unchanged. Part C is at the end. Parts A and D are in
[`../2026-09-27-windows-excel/`](../2026-09-27-windows-excel/results.md).

**Verified commit: `9a78a14aca7192640f783d7e17cd5eb3e1228d29`**, the tip of
`claude/exsheet-start-8cx3v1` when the run began. Recorded on `claude/exsheet-windows-verify`.

## Environment

- Windows 11 Pro 25H2 (build 26200), one display, 3840×2160 at **150%** (2560×1440 logical).
  Scrollbars are the platform's own and occupy layout
- **Google Chrome 153.0.8010.54** and **Microsoft Edge 154.0.4258.37**, the installed Windows
  builds, run headed
- The runner: portable Node v24.14.1 on Windows, with Playwright 1.62.1, driving a copy of
  `tests/ExGrid.Browser`. The copy sits at `…\run-2026-09-27\tests\ExGrid.Browser`, so the run's
  records land in `…\run-2026-09-27\verification\2026-09-27-windows\`, and are copied here
- The hosts ran in WSL2 on the same machine (.NET SDK 10 via nix), from this commit's checkout:
  the WebAssembly DemoHost on `localhost:5299`, and the Server DemoHost on `localhost:6298`. Windows
  reaches them through WSL's localhost forwarding, and the config's `reuseExistingServer` reuses
  them. The Server run's latency proxy (`5298 → 6298`) was started by the runner, on Windows
- **The Server host's log** (CON-6 reads it per test) was written through `EXGRID_HOST_LOG` to
  `C:\Users\amisa\AppData\Local\Temp\exgrid-server-host-6298.log`, the path the runner's
  `os.tmpdir()` names. A host in WSL writing to its own `/tmp` would have left CON-6 reading an
  empty file and passing without testing anything

The two commands were §22 Step 4's: `npx playwright test`, then `EXGRID_HOSTING=server npx
playwright test`, each teed here. MEM-5's soak (`EXGRID_SOAK=1`) and `measure-server` were not
asked for by Part B and were **not run**; their tests are skipped by name.

## Results

| Run | Log | Chrome | Edge | Total |
|---|---|---|---|---|
| WebAssembly | `layer3.log` | 194 passed, **9 failed**, 6 skipped | 194 passed, **9 failed**, 6 skipped | 388 passed, 18 failed, 12 skipped (10.3 min) |
| WebAssembly, `navigation.spec.mjs` again | `layer3-navigation-rerun.log` | 3 passed | 3 passed | 6 passed |
| Server | `layer3-server.log` | 197 passed, **8 failed**, 4 skipped | 198 passed, **7 failed**, 4 skipped | 395 passed, 15 failed, 8 skipped (7.1 min) |
| Server, WR-6 three times more | `wr6-server-rerun.log` | 3 passed | — | 3 passed |

The container's run was 202 passed on WebAssembly and 204 on Server, on Chromium alone. It was
made before commit `c2c41f3` (MEM-4 below). The skips are MEM-5's soak and the tests that
`skip` themselves by host.

### Failures

| Criterion | Test | Host | Browsers | What failed |
|---|---|---|---|---|
| **MEM-4** | `memory.spec.mjs` "disposal takes the module's listeners off the root" | both | both | The instance root carries **six** listeners where the test expects five: `input (capture)` is new, beside copy, keydown (capture), mouseleave, mousemove and paste |
| **VZ-14**, and ADR-0012/0013 "the Focus is never behind a scrollbar" ×3 | `scrollbar.spec.mjs` (4 tests) | both | both | **"the Focus is behind the horizontal scrollbar"**: at Ctrl+End the Focus's bottom is at 898–900 px, where the readable area ends at 872 px (+ slack). That is about one row below it |
| **BIG-1** | `virtualisation.spec.mjs` "the far corner is reachable and painted at 10⁶ rows" | both | both | The last row's cell `r999999c99` is never painted (15 s) |
| **BIG-5** | `virtualisation.spec.mjs` "the first and the last row paint their own data" | both | both | The last row's cell `r999999c0` is never painted |
| **RI-1, RI-3** | `navigation.spec.mjs` | WebAssembly | both | **Not the product's.** The spec reads `samples/ExGrid.DemoPages/Pages`, which the Windows copy of the suite did not include. Run again with it: 6 of 6 pass. The Server run had it and passed |
| **WR-6** | `mud-app.spec.mjs` "Striped paints the palette's table-stripe colour…" | Server | Chrome only | After the scheme switch, row 9's render marker was missing (rows 0–8 kept theirs): one row re-rendered. See below |
| **CON-1** (recorded) | BIG-1 on Server, Chrome | Server | Chrome | `console.json`: "Failed to load resource: net::ERR_NO_BUFFER_SPACE" for `/_framework/blazor.web.js` through the latency proxy. That is a Windows socket error, not the grid's; it is listed because CON-1 lists every console error |

### What is a regression from the ExSheet work (DC-1), and what is not

To separate the two, the commit this branch left `main` at, **`789b208`** (#24, the merge base),
was built in a separate worktree. Its WebAssembly DemoHost ran on `localhost:5399`, and its own
`memory`, `scrollbar` and `virtualisation` specs ran against it from this machine
(`layer3-mb-789b208.log`). The specs are byte-identical between `789b208` and the verified commit:
`git diff 789b208 9a78a14 -- tests/ExGrid.Browser` is empty.

| Criterion | At `789b208` (before ExSheet) | At `9a78a14` | Reading |
|---|---|---|---|
| MEM-4 | **passes**, both browsers | fails | **Introduced on this branch.** The listener came with `c2c41f3` ("The caret is reported and set, never inferred", ADR-0051 second round). ADR-0021 records it as part of the same allowlisted editor use. MEM-4's expected list still names five, and its comment says "on the instance root and nowhere else". The same commit also adds a `selectionchange` listener on `document` per instance, which MEM-4 does not look at. Whether the test or the code changes is the user's decision |
| VZ-14 and the three scrollbar tests | **fail the same way**, both browsers | fail | **Not from the ExSheet work.** It was already failing on `main` at #24. The same spec passed on this machine on 2026-09-23 at 150% and at 125% (`../2026-09-23-windows/results.md`), so something between that run and `789b208` broke it on Windows |
| BIG-1, BIG-5 | **fail the same way**, both browsers | fail | The same: already on `main` at #24. The last row not being painted, and the Focus sitting a row below the readable area at Ctrl+End, look like one fault, but that is not established |

So **DC-1 fails on one criterion, MEM-4**. The other failures are Windows failures of `main` that
the ExSheet work inherited, and they matter just as much for sign-off: VZ-14 is the criterion only
a Windows desktop can discharge. Which commit between 2026-09-23 and `789b208` broke them was not
bisected in this run.

### WR-6

It failed once, on the Server host, on Chrome only. It passed on Edge, and on both browsers on
WebAssembly. Run three times more on the same host and browser (`wr6-server-rerun.log`), **it
passed all three**. So it fails intermittently: once in four runs here. The one failure was one
row (row 9 of 0–9) re-rendered across the colour-scheme switch, which is what RR-1 forbids. A
failure that comes and goes is still a failure of RR-1; it is listed, not excused.

## Part C — the new declarations in a real browser

**Not run: tests not yet written.** Ticket 18 (the browser suite) is `ready-for-agent`, not
`done`, and so are several tickets it depends on (10, 11, 13, 14, 16). DC-3, DC-8, DC-13, DC-22
and the IME check therefore have no specs to run, and none was improvised.

Two things seen on the way belong beside DC-22, though they come from Part D's probes, not from
Part C:

- **A value typed straight after a click can land in the wrong cell.** Found when
  `sheet-vs-excel.spec.mjs` first ran on the Server host, then reduced to
  `../2026-09-27-windows-excel/typing-probe-2.mjs`. The probe clicks F1, types `1`, presses
  Enter, clicks F2, types `2`, and so on. On the Server host (loopback, no latency injected), with
  0, 30 or 60 ms between the steps, a value landed a row too low (`2` in F3, `7` in F8) in every
  trial; from 100 ms on, never. On WebAssembly it happened at 0 ms only. The click that follows an
  Enter is applied before the Enter's move. ED-22 says keys are "neither lost nor reordered". Its
  test covers keys alone, and it passes. A click between keys is not in it
- Point mode and completion, whose tickets (10, 11) are not `done`, **pass** Part D's probes on
  both hosts and both browsers when run anyway (`EXGRID_SHEET_UNBUILT=1`)

## Files

- `layer3.log`, `layer3-server.log`: the two §22 Step 4 runs
- `layer3-navigation-rerun.log`: `navigation.spec.mjs` again, with the pages the spec reads
- `layer3-mb-789b208.log`: the merge-base comparison
- `wr6-server-rerun.log`: WR-6 three times on the Server host, Chrome
- `console.json`, `metrics.json`: the run's records, written by the fixture. `metrics.json` holds
  the observational numbers, recorded and never gated
