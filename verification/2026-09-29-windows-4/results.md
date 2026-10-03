# Verification — 2026-09-29, Windows, fourth run: ExSheet beside Excel, and layer 3

**Scope: Parts C and D of [`verify-on-windows-4.md`](../../docs/specs/exsheet/verify-on-windows-4.md).**
Part A, the oracle, is in
[`../2026-09-29-windows-excel-4/results.md`](../2026-09-29-windows-excel-4/results.md), and Part B in
[`../2026-09-29-windows-excel-4/active-cell.md`](../2026-09-29-windows-excel-4/active-cell.md).
Part E, other display scales, is optional, needs the user, and was not run.

**Verified commit: `393eb61146f10fade1f8c09d0432ff0dc6ee0f03`**, the tip of
`claude/exsheet-start-8cx3v1` when this run began, for every part; `claude/exsheet-windows-verify-4`
is branched from it. The tip was not merged again during the run. Nothing is decided or changed here.
Each failure and disagreement is listed with its criterion, ADR or ticket, for the user.

## Environment

- The machine of the first three runs: Windows 11 Pro 25H2 (build 26200), 3840×2160 at **150%**,
  regional format **en-GB** throughout C and D (Part A's `Set-Culture` changes were undone at 10:54),
  display language en-GB
- Chrome and Edge as installed, headed, driven by Playwright 1.62.1 under a portable Node v24.14.1
  from a copy of `tests/ExGrid.Browser` on Windows (`%LOCALAPPDATA%\exgrid-layer3\run-2026-09-29-4`),
  identical to the verified commit's, **with `npm ci` run afresh in it** (10:17) since
  `playwright.config.mjs` changed
- Both DemoHosts ran in WSL from the verified commit: the WebAssembly host on `localhost:5299`, the
  Server host on `localhost:6298` with `EXGRID_HOST_LOG` on the Windows temp directory. In the suite
  the browser reaches the Server host through `latency-proxy.mjs` (5298), which Playwright starts on
  Windows; the probes address it directly
- Excel 16.0.20326.20158 for Excel's side of Part C
- Build and layers 1–2 in WSL2 through nix at the verified commit: **0 warnings, 0 errors**; ExGrid.Tests
  784, ExSheet.Engine.Tests 1764, ExGrid.MudBlazor.Tests 79, ExGrid.Components 863 (1 skipped),
  ExSheet.Components.Tests 244 passed, 0 failed
- The run's inputs went from 10:12 to 11:57. No Excel was ended by a guard, and the machine was
  left as it was found (below)

## Part C — ExSheet beside Excel

### `sheet-vs-excel.spec.mjs`

Three runs, each with all three projects (`chrome`, `msedge`, and `chrome-150`, whose `grep` now
selects items 3 and 5 and active-cell cases 7 and 8). The logs are in `../2026-09-29-windows-excel-4/`:

| Run | Log | Result |
|---|---|---|
| WebAssembly | [`sheet-vs-excel-wasm.log`](../2026-09-29-windows-excel-4/sheet-vs-excel-wasm.log) | **69 passed, 0 failed**, 14 skipped (3.6 min, 10:58–11:01) |
| Server | [`sheet-vs-excel-server.log`](../2026-09-29-windows-excel-4/sheet-vs-excel-server.log) | **69 passed, 0 failed**, 14 skipped (2.1 min, 11:02–11:04) |
| WebAssembly, `EXGRID_SHEET_UNBUILT=1` | [`sheet-vs-excel-unbuilt.log`](../2026-09-29-windows-excel-4/sheet-vs-excel-unbuilt.log) | **79 passed, 0 failed**, 4 skipped (3.9 min, 11:04–11:08) |

- **Items 3 and 5 and active-cell cases 7 and 8 pass** on `chrome`, `msedge` and `chrome-150`, on
  both hosts and unbuilt, as the procedure expected. In the third run they failed everywhere at 150%
  (ADR-0050 §1, ADR-0052, ticket 06; ADR-0053)
- **Case 6**, now titled "Ctrl+click on the active cell takes it out and the first remaining cell of
  the range made last is active", passes on `chrome` and `msedge` in all three runs
- Skipped in the built runs: items 16, 17 and 19 (ticket 14 not `done`), 18 and 24 (`test.fixme`);
  unbuilt, 16, 17 and 19 run and pass, and 18 and 24 are skipped

### Ctrl+Enter beside Excel (ADR-0050, SH-27)

ExSheet: [`exsheet-probe.mjs`](../2026-09-29-windows-excel-4/exsheet-probe.mjs) (11:08–11:10), keys at
a person's pace (150 ms), each case on a fresh `/sheet`, the Entries read back through the Formula
Bar; outputs `exsheet-probe-{wasm,server}-{chrome,msedge}.json`. Excel:
[`main-keys.ps1`](../2026-09-29-windows-excel-4/main-keys.ps1) case 5 (the third run's script,
unchanged, 10:57) for the first row, and Part B item 2 for the other two
([`active-cell.md`](../2026-09-29-windows-excel-4/active-cell.md)).

| Typed, then Ctrl+Enter over B2:C3 | Active | Excel | ExSheet, both hosts, both browsers |
|---|---|---|---|
| `=A1`, from B2 | B2 | `=A1`, `=B1`, `=A2`, `=B2` | **`=A1`, `=B1`, `=A2`, `=B2`** |
| `=B2+$A$1`, from C3 (click C3, Shift+click B2) | C3 | `=A1+$A$1`, `=B1+$A$1`, `=A2+$A$1`, `=B2+$A$1` | **the same** |
| `=B2+$A$1`, from B3 (Enter inside B2:C3) | B3 | `=B1+$A$1`, `=C1+$A$1`, `=B2+$A$1`, `=C2+$A$1` | **the same** |

(The cells in the order B2, C2, B3, C3.) **ExSheet agrees with Excel in all three**, on all four
host and browser pairs. In the third run ExSheet wrote `=A1` into all four cells.

### A typed entry the engine refuses: `-B2 C2`

`-B2 C2` typed into F2 of `/sheet`, then Enter, then Escape. The same on both hosts and both browsers:

- After Enter **the editor stays open** on F2 with `-B2 C2`, marked `aria-invalid`, and a message
  shows under it, **naming the intersection operator**: "This Formula cannot be read at character
  6: the intersection operator (a space between two References) is not implemented." The editor's
  `aria-describedby` points at it, and the live region (`role=status`) says the same
  ([`C-R-then-Enter-exsheet-server-chrome.png`](../2026-09-29-windows-excel-4/shots/C-R-then-Enter-exsheet-server-chrome.png))
- After Escape the editor closes, F2 stays empty, and the focus is back on the grid. **The live
  region still holds the refusal's text**, not the Selection
- Excel makes it the Formula `=-B2 C2`, `#NULL!` (TYPED-054, typed, the third run and Part A now);
  ADR-0047 refuses it by name, as the procedure expected

### Column widths (SH-26)

In an empty column F: `1234567890` in F2, `12345678901` in F3, then F dragged narrower by the
right-hand edge of its header, then `123456789012` in F4. ExSheet with the Playwright mouse (27 CSS
px, about 40 screen pixels at 150%), Excel with the real mouse (40 screen pixels,
[`widths.ps1`](../2026-09-29-windows-excel-4/widths.ps1), 10:57):

| Step | ExSheet, F's width (CSS px), both hosts and browsers | shown | Excel, F's width | shown | Excel's file |
|---|---|---|---|---|---|
| empty | 99 | | 8.09 chars (48 pt) | | no `<col>` |
| `1234567890` in F2 | **114** | `1234567890` | **10.18** (59.5 pt) | `1234567890` | `bestFit="1" customWidth="1"` |
| `12345678901` in F3 | **124** | `12345678901` | **11.18** (65 pt) | `12345678901` | `bestFit="1" customWidth="1"` |
| F dragged narrower | **97** | F2 `1.23E+09`, F3 `1.23E+10` | **7.55** (45 pt) | `1.2E+09`, `1.2E+10` | `customWidth="1"`, no `bestFit` |
| `123456789012` in F4 | **97**, not widened | F4 `1.23E+11` | **7.55**, not widened | `1.2E+11` | `customWidth="1"` |

**Both widen the column twice and stop widening it once it has been dragged**, as the procedure
expected. The shown texts after the drag differ (`1.23E+09` in ExSheet, `1.2E+09` in Excel) because
the columns differ in width: ExSheet's starts at 99 CSS px and was left at 97, Excel's at 48 pt and
was left at 45 pt. Nothing here compares the two widths. In Excel's file an entry's width is written
with `bestFit="1"` and a user's without it; both set `customWidth`.

## Part D — layer 3, both browsers, both hosts, at 150%

As in the third run: the whole suite from the Windows copy, `node node_modules\@playwright\test\cli.js
test`, once per host. **771 tests per run**: `chrome` and `msedge` with 378 each, and `chrome-150`
with the 15 its `grep` now selects. Under ADR-0056 each spec file boots its app once and its tests
share the page. MEM-5/6's soak (`EXGRID_SOAK=1`) was not run, as in the earlier runs.

| Host | Log | Result |
|---|---|---|
| WebAssembly | [`layer3-wasm.log`](layer3-wasm.log) | 743 passed, **2 failed**, 26 skipped (16.8 min, 11:10–11:27) |
| Server | [`layer3-server.log`](layer3-server.log) | 744 passed, **5 failed**, 22 skipped (14.9 min, 11:27–11:42) |

Each host's records are kept apart: [`metrics-wasm.json`](metrics-wasm.json),
[`metrics-server.json`](metrics-server.json), [`console-wasm.json`](console-wasm.json),
[`console-server.json`](console-server.json). Besides the failures below, the console records hold
only `harness.spec.mjs`'s error on purpose and VZ-12b's expected warning.
`harness.spec.mjs`'s two `test.fail` tests ("a test that fails hands no page on", "a test whose
console reports an error hands no page on") fail on purpose and leave trace folders; they are not
failures.

### What the procedure expected

**All of it passed, on every project of both hosts**: MK-6 (ADR-0043); `sheet-vs-excel` items 3 and
5 and active-cell cases 7 and 8; BIG-5 on Server's `chrome-150`; and the new BIG-5 race test ("a
scroll to the end made as the grid becomes ready is not undone by the Layout Ceiling (BIG-5,
ADR-0053)") on `chrome`, `msedge` and `chrome-150`. BIG-1, VZ-14, VZ-15, SH-2, SH-18/DC-7,
SH-18/DC-2/DC-3 and "the Focus is never behind a scrollbar" pass everywhere too. **`chrome-150` ran
15 tests on each host and all 15 passed.**

### Failures, each rerun three times

The failing test alone, `--repeat-each=3`, on the project and host it failed on; logs `rerun-*.log`.
The last frame of each failure's trace is in [`frames/`](frames/), with its error in
`frames/<host>.txt`.

| Test | Criterion | Failed on | What it received | Reruns |
|---|---|---|---|---|
| **WR-7**: a Drawer toggle resizes the Stretch grid and its geometry follows | ADR-0028 | WebAssembly `chrome` and `msedge`; Server `chrome` and `msedge` | "the Stretch grid's width with the Drawer closed": **1214 in all four**, against 1229.8, 1223.3, 1243.7 and 1220.7 expected (the width with the Drawer open plus the Drawer's width, measured at the start). The last frame shows the page with its own vertical scrollbar, the Drawer closed | **3 of 3 passed** on each of the four |
| **RI-4**: modal, by click, handler returning once the dialog is shown | ADR-0020/0037; CON-1 | Server `chrome` | zero console errors: "WebSocket connection to 'ws://localhost:5298/_blazor?id=…' failed: Error in connection establishment: `net::ERR_NO_BUFFER_SPACE`", then "Failed to start the transport 'WebSockets'", and the warning "Failed to connect via WebSockets, using the Long Polling fallback" | 3 of 3 passed |
| **`sheet-vs-excel` item 23**: deleting a row a Formula references writes #REF! in its place | ADR-0046, ticket 13 | Server `chrome` | `locator.click: Element is not visible` for E1 (`r0c4`, which holds 14). The last frame shows the row's context menu open by B3. The same test passed in Part C's standalone Server run | 3 of 3 passed |
| **WR-6**: Striped paints the palette's table-stripe colour … a switch re-renders no row | ADR-0038/0030, RR-1 | Server **`msedge`** (the earlier runs saw it on `chrome`) | the rows' probe marks after the switch: 0..8, **9 missing**, the third run's shape | 3 of 3 passed |

### How often the three that came and went fail

`--repeat-each=10` on the host and project each failed on in the third run; logs `rep10-*.log`:

| Test | Criterion | Host, project | Failed | The failures |
|---|---|---|---|---|
| **FN-6a** (`sizing.spec.mjs`) | ADR-0045 | WebAssembly `chrome` | **2 of 10** (repeats 3 and 6) | Both "the Focus is whole on screen": the poll timed out after 5 s. It printed no value: in both traces the poll's first read of the grid's `aria-activedescendant` gave **no value**, right after the test's previous assertion had seen it end in `-r0c3`, and the poll then waited on `[id='null']` for its whole time. The last frame shows the 250 px window, column 評価額（円） in view, and the status line "Selection: 0,3,1,1 Pinned: 2" |
| FN-6a | ADR-0045 | WebAssembly `msedge` | 0 of 10 | |
| **DC-19/DC-34**, the mud Chrome case (`declarations.spec.mjs`) | ADR-0010 (DC-19, DC-34) | Server `chrome` | **5 of 10** (repeats 1, 4, 6, 8, 10) | All five: `.ex-point` count **received 1, expected 0**, as in the third run. The last frame shows the Formula Bar and F2's editor holding `=SUM(F3`, and the dashed point box on F3 |
| **WR-6** (`mud-app.spec.mjs`, Striped) | ADR-0038/0030, RR-1 | Server `chrome` | **1 of 10** (repeat 2) | Probe marks 0..8, 9 missing |

In the whole suites, FN-6a, DC-19/DC-34 and WR-6 passed on these projects, and WR-6 failed on Server
`msedge` (above).

### A scroll to the end before the grid knows its ceiling (ADR-0053)

[`end-probe.mjs`](end-probe.mjs), 11:49–11:57, with real input through
[`input-server.ps1`](input-server.ps1), a PowerShell started once and fed one command a line, so that
a gesture leaves within a few milliseconds. Each try opens `/wide` in a fresh tab. A script installed
before the page loads records, frame by frame, when the first row is painted, when the page becomes
interactive, when the spacer's declared height changes from the uncompressed 28,000,028 px to the
compressed 22,369,362 px (the ceiling reaching the grid), the scroll offset, and the mousedown and
keydown the page saw. Three ways, five tries each, on both hosts and both browsers (60 tries),
outputs `end-probe-<host>-<browser>-<gesture>-<moment>.json`:

- **drag, painted**: as soon as a first row is painted, the thumb dragged from 8 CSS px below the
  scroller's top to 150 px below its bottom, in 12 steps 8 ms apart
- **drag, ready**: the same, as soon as the grid is interactive and not busy
- **Ctrl+End, ready**: one real click on a cell, then a real Ctrl+End

**In all 60 tries the view ended at the end: the last row (999,999) painted and whole in the view,
rows 999,980–999,999 showing, and the thumb at the bottom** (`scrollTop` at its maximum, fraction
1.0000). Nothing reached the console. One capture per way is in [`shots/`](shots/)
(`D-end-…-1.png`, the window as the screen showed it, trimmed at its edges and halved).

**Whether the gesture came before the ceiling:**

- On **WebAssembly** the compressed spacer appeared 17–22 ms after the first row was painted, and
  every gesture came 20–42 ms after that: **the ceiling came first in all 30**
- On **Server** `#demo-interactive` appeared 90–103 ms after navigation and the first row 143–153
  ms, in a grid already interactive and not busy. The page was prerendered, but the recorder saw no
  row before `#demo-interactive`, so the "painted" and "ready" moments were the same (on WebAssembly
  too: interactive at 356–454 ms, the first row at 429–532 ms). **In 10 of the 20 drags the mousedown came 6–16 ms
  before the compressed spacer**; in the other 10, and for every Ctrl+End keydown, the ceiling came
  first (in 2 Ctrl+End tries the click came before it)
- **In every one of the 60 tries the scroll offset was still 0 when the ceiling arrived**: the view
  first moved 71–98 ms after it on WebAssembly and 25–109 ms after it on Server, including in the 10
  drags pressed before it. So no try with real
  input moved the view before the grid had its ceiling. The race the procedure describes was reached
  only by the suite's BIG-5 race test, which scrolls from script in the task in which the grid stops
  being busy, and which passed everywhere

**How the thumb was found, and the first attempts.** Neither browser paints anything in the grid's
scrollbar gutters here (below), so the thumb's place was found by trial with the real mouse
([`thumb-calibrate.mjs`](thumb-calibrate.mjs),
[`thumb-calibrate-wasm-chrome.json`](thumb-calibrate-wasm-chrome.json)): on a loaded `/wide` at the
top, a press 1–16 CSS px below the scroller's top, moved 60 px down, dragged the thumb (the view moved
2,362,899 px each time); a press at 20 px hit the track and paged (1,535 px). The first trial of the
probe (11:49) ran with its settings lost on the way to Windows (`WSLENV` did not carry them), so it
pressed at 24 px, on the track, and paged down to `scrollTop` 511 in all five tries; the settings were
then passed as `cmd` arguments. A second trial (one try) is the one that reached the end; the 60 tries
above are the record.

### The grid's scrollbars are not painted (ADR-0029, UX-10; ADR-0013)

While finding the thumb: **on this machine neither Chrome nor Edge paints anything in the grid's
scrollbar gutters**. The gutter takes its space (the scroller is 900 CSS px wide and 885 client px,
585 of 600 high), the thumb works (above), but the gutter is blank: no pixel darker than near-white
in the vertical gutter at the top or the bottom, with the pointer resting on it or not
([`D-gutter-wasm-chrome-top-right.png`](shots/D-gutter-wasm-chrome-top-right.png),
[`D-gutter-wasm-chrome-whole.png`](shots/D-gutter-wasm-chrome-whole.png)); nor does any bar show on
the grid in the `D-end-…` captures looked at, Chrome's and Edge's. The page's own scrollbar, at the window's right edge, is
painted. `ex-grid.css` says that with `--ex-scrollbar-width` and `--ex-scrollbar-color` unset, "the
native scrollbar stands untouched" (`.ex-scroller::-webkit-scrollbar { width: var(--ex-scrollbar-width, ) … }`);
here nothing of the native scrollbar is painted. The third run's wheel captures of `/wide`
(`../2026-09-28-windows-3/shots/D-wheel-*.png`) show the same. Nothing was changed and nothing is
concluded; the settings seen are Windows 11 25H2 at 150% with no `DynamicScrollbars` value under
`HKCU\Control Panel\Accessibility`.

## The machine afterwards

- Regional format **en-GB**; `HKCU\Control Panel\International` exported again at 11:58, **identical
  byte for byte** to the export taken before the run
- No Excel running; the AutoRecovered workbook unchanged (compared with its backup); no Notepad
  running (Part B's step closed the one it started)
- No Playwright browser or runner left (the one `node.exe` running is the user's Stream Deck
  plugin); both DemoHosts in WSL stopped
