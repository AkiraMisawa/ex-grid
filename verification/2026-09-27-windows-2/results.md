# Verification — 2026-09-27, Windows, second run: ExSheet beside Excel, and layer 3

**Scope: Parts C and D of [`verify-on-windows-2.md`](../../docs/specs/exsheet/verify-on-windows-2.md).**
Part A, the oracle, is in
[`../2026-09-27-windows-excel-2/results.md`](../2026-09-27-windows-excel-2/results.md), Part B in
[`../2026-09-27-windows-excel-2/active-cell.md`](../2026-09-27-windows-excel-2/active-cell.md), and
Part E in [`../2026-09-27-windows-bisect/results.md`](../2026-09-27-windows-bisect/results.md).

**Verified commit: `e9c448aa9d5104d801ec96d66c6c4e59ca0c4823`.** Nothing is decided or changed here.
Each failure is listed with its criterion, ADR or ticket, for the user.

## Environment

- Windows 11 Pro 25H2 (build 26200), 3840×2160 at **150%**, regional format **en-GB** (restored
  after Part A), display language en-GB. The keyboard for the browsers is the Japanese IME, off
- Chrome 153.0.8010.54 and Edge 154.0.4258.37 as installed (the channels `chrome` and
  `msedge`), headed, driven by
  Playwright 1.62.1 under a portable Node v24.14.1, from a copy of `tests/ExGrid.Browser` on
  Windows (`%LOCALAPPDATA%\exgrid-layer3\run-2026-09-27-2`), identical to the verified commit's
- Both DemoHosts ran in WSL, built from the verified commit (`dotnet build ExGrid.slnx`, 0
  warnings): the WebAssembly host on `localhost:5299`, and the Server host on `localhost:6298`
  with `EXGRID_HOST_LOG` on the Windows temp directory, behind `latency-proxy.mjs` (5298, control
  7298), which Playwright started on Windows. Windows reaches WSL through localhost forwarding
- Excel 16.0.20326.20158 for the clipboard probe, started under en-GB

## Part C — ExSheet beside Excel again

### `sheet-vs-excel.spec.mjs`

Three runs, each on Chrome and Edge. The logs are in `../2026-09-27-windows-excel-2/`:

| Run | Log | Result |
|---|---|---|
| WebAssembly | [`sheet-vs-excel-wasm.log`](../2026-09-27-windows-excel-2/sheet-vs-excel-wasm.log) | **50 passed, 18 skipped** (4.6 min) |
| Server | [`sheet-vs-excel-server.log`](../2026-09-27-windows-excel-2/sheet-vs-excel-server.log) | **50 passed, 18 skipped** (2.7 min) |
| WebAssembly, `EXGRID_SHEET_UNBUILT=1` | [`sheet-vs-excel-unbuilt.log`](../2026-09-27-windows-excel-2/sheet-vs-excel-unbuilt.log) | 62 passed, **2 failed**, 4 skipped (5.5 min) |

Item by item. The columns are WebAssembly Chrome and Edge, Server Chrome and Edge, then
unbuilt Chrome and Edge. **"x (expected)"** is a probe marked `test.fail` for ADR-0052: it
asserts Excel's active cell, ExGrid's Focus is still the moving end, and it failed as expected.
These count as passed, as in the first run.

| Item | Result | Note |
|---|---|---|
| 1, 6–15, 20 (four of six), 21, 25–27 | passed everywhere | |
| 2, 3, 4, 5; 20 "the fill handle continues a series", 20 "a series filled up goes backwards" | **x (expected)** everywhere | ADR-0052: Excel keeps the active cell on the fixed end |
| 16, 17, 19 (three probes) | skipped in the ordinary runs; **passed** unbuilt | ticket 14 is not `done`, so they are `test.fixme` |
| 18 | skipped (`test.fixme`) | covered by `clipboard-probe.mjs`, below |
| 22, inserting rows above a selected range | skipped; unbuilt: **failed** on both browsers | ticket 13, ADR-0046 |
| 23 | skipped; unbuilt: passed | ticket 13 |
| 24 | skipped (`test.fixme`) | ticket 12 |

**Item 22, unbuilt (ticket 13, ADR-0046/0052).** After B3:C4 is selected from B3 and two rows are
inserted above it, the Name Box shows **`C4`**. Excel shows `B3`, its active cell, and the probe
asserts that. The Selection and the rows are as Excel has them. The Name Box shows ExGrid's Focus,
which is the moving end until ADR-0052 lands. [Part B, case 15](../2026-09-27-windows-excel-2/active-cell.md)
records Excel's side: the active cell stays where it was in the range.

### The clipboard, both directions (`clipboard-probe.mjs`)

[`clipboard-probe.mjs`](../2026-09-27-windows-excel-2/clipboard-probe.mjs), once on Chrome and
once on Edge, against the WebAssembly host. The outputs are
[`clipboard-probe-chrome.json`](../2026-09-27-windows-excel-2/clipboard-probe-chrome.json) and
[`clipboard-probe-msedge.json`](../2026-09-27-windows-excel-2/clipboard-probe-msedge.json). The two
browsers gave the same answers throughout, the page had focus at every paste and copy, and the
console was empty.

The probe was changed from the first run's in two ways. Excel's date now goes in as its serial
number (46291) with the format `m/d/yyyy`. Typed as `9/26/2026` under en-GB, the machine's format
now, it would be text. And ExSheet's notice is read straight after each paste, because a click on
a cell clears it. `m/d/yyyy` is Excel's built-in short date, which follows the regional format:
Excel shows the date, and copies it, as **`26/09/2026`**.

**Excel → ExSheet, the source column wide enough** (`=B1*2`, the date, `1234.5` as `#,##0.00`):
Excel's clipboard holds `6`, `26/09/2026`, `1,234.50` in both flavours. ExSheet (`/sheet`, en-US)
shows F2 `6`, **F3 `26/09/2026` as text** (en-US does not read it as a date), and F4 `1,234.50`
(1234.5). No notice.

**Excel → ExSheet, the source column too narrow (width 6), which ExSheet should now refuse by name
(e361956).** Excel's clipboard holds, in text, the values `6`, `26/09/2026`, `1,234.50`. In HTML
it holds `6`, **`######`** and **`######`**: the date and `1,234.50` are both too wide for the
column.

**ExSheet did not refuse it by name. Nothing was pasted, and no notice appeared, on both
browsers.** F7:F9 stayed empty, the Name Box stayed F7, and the notice element stayed empty.
[`clipboard-narrow-watch.mjs`](../2026-09-27-windows-excel-2/clipboard-narrow-watch.mjs) repeated
the paste on Chrome and watched for three seconds, twice, with the same result (the second run's output is
[`clipboard-narrow-watch-chrome.json`](../2026-09-27-windows-excel-2/clipboard-narrow-watch-chrome.json)):

- The browser's `paste` event reached the grid's root, with `text/plain` (the values),
  `text/html` (cells `6`, `######`, `######`), `text/rtf` and a file
- Nothing on the page changed: the notice, the Name Box and F7:F9, polled every 50 ms
- A `MutationObserver` on the notice saw one change, to an empty text
- The console was empty

The first run's ExSheet pasted `########` into the cell as text. This one writes nothing and says
nothing. **Failure against e361956's intent, ADR-0048 ("say it cannot be done"), ticket 14.** The
cause was not looked for.

**ExSheet → Excel** (E2 `=B2*2`, E3 `9/26/2026`, E4 `1234.5` formatted `#,##0.00` by the page's
button), copied by **Ctrl+C (the keyboard route)** and by **the Context Menu's Copy (the menu
route)**:

| | Chrome, keyboard | Chrome, menu | Edge, keyboard | Edge, menu |
|---|---|---|---|---|
| `data-ex-grid="invariant"` in the clipboard's HTML | **present** | **present** | **present** | **present** |
| Excel's paste: D1/H1 | 24, General | 24, General | 24, General | 24, General |
| D2/H2 | **46291, `m/d/yyyy`** | the same | the same | the same |
| D3/H3 | **1234.5, `#,##0.00`**, shows `1,234.50` | the same | the same | the same |

The marker survives the real clipboard on both routes in both browsers. The HTML carries each
value as `x:num` with its format as `mso-number-format`. **Excel takes the formats now**, where the
first run's paste gave the date as General 46291. Excel shows the pasted date as `########`,
because `26/09/2026` does not fit its default column. The value and format are right.

### The typing race (`typing-probe-2.mjs`)

The first run's [`typing-probe-2.mjs`](../2026-09-27-windows-excel/typing-probe-2.mjs), on Chrome:
click F1, type `1`, Enter; click F2, `2`, Enter; click F3, `3`, Enter; click F7, `7`, Enter, with
the pause between steps varied, three trials each. Output:
[`typing-probe-2.json`](../2026-09-27-windows-excel-2/typing-probe-2.json), and nine more trials
on the Server host at 30 ms in
[`typing-probe-2-server-30ms.json`](../2026-09-27-windows-excel-2/typing-probe-2-server-30ms.json).

| Host | 0 ms | 30 ms | 60 ms |
|---|---|---|---|
| WebAssembly | right (3 of 3) | right (3 of 3) | right (3 of 3) |
| Server (`localhost:6298`, direct) | right (3 of 3) | **wrong in 6 of 12** | right (3 of 3) |

**The first run's defect is gone**, a value in the row below the one clicked. Every trial that
typed anything put each value in its clicked cell, including at 0 ms. **The Server host at 30 ms
fails another way: in 6 trials of 12, nothing landed at all.** F1..F8 stayed empty and the Name Box
stayed F1. The Server host's log has no warning or error. The other 6 trials at 30 ms were right.
Listed against ED-22 and ADR-0010, "keys are neither lost nor reordered". A click between keys was
outside ED-22's test in the first run. The cause was not looked for, and whether the page was
interactive yet at the first click was not established.

## Part D — layer 3, both browsers, both hosts

As the first run's Part B (§22 Step 4): the whole suite from the Windows copy, `node
node_modules\@playwright\test\cli.js test`, once per host, each teed here. **596 tests per run**,
178 more than the first run's 418, with ticket 18's specs (`sheet.spec.mjs`, `declarations.spec.mjs`,
`sheet-vs-excel.spec.mjs`, `inspector-edits.spec.mjs` and the rest). MEM-5's soak
(`EXGRID_SOAK=1`) was not run, as in the first run.

| Host | Log | Result |
|---|---|---|
| WebAssembly | [`layer3.log`](layer3.log) | 547 passed, **19 failed**, 30 skipped (22.8 min) |
| Server | [`layer3-server.log`](layer3-server.log) | 545 passed, **25 failed**, 26 skipped (16.4 min) |

The numbers the suite records are in [`metrics.json`](metrics.json), and each test's console
errors and warnings are in [`console.json`](console.json), which is **empty for both browsers**.
The ExSheet probes marked `test.fail` for ADR-0052 (`sheet-vs-excel.spec.mjs` items 2–5 and two of
item 20) failed as expected and count as passed. Every skip is by design: MEM-5/6's soak, the
Server-only tests on WebAssembly, the `sheet-vs-excel` items waiting on tickets 12–14, and
`measure.spec.mjs` on WebAssembly.

### Failures, by criterion

| Criterion | Test | WebAssembly | Server | First run |
|---|---|---|---|---|
| **VZ-14**, ADR-0012/0013 "the Focus is never behind a scrollbar" | `scrollbar.spec.mjs`: with the platform's own scrollbars; with scrollbars that occupy layout; at every zoom level, after moving again; VZ-14, the Focus stays readable with the OS doing the scaling | **fail**, both browsers | **fail**, both | the same (Part E) |
| **BIG-1**, **BIG-5** | `virtualisation.spec.mjs`: the far corner at 10⁶ rows; the first and the last row paint their own data | **fail**, both | **fail**, both | the same (Part E) |
| **SH-18/DC-7** | `sheet.spec.mjs`: Ctrl+arrow stops at the end of each block | **fail**, both | **fail**, both | new spec |
| **SH-18/DC-2/DC-3** | `sheet.spec.mjs`: a column heading, a Row Heading and the corner select | **fail**, both | **fail**, both | new spec |
| **SH-2** | `sheet.spec.mjs`: the Focus reaches XFD1048576 and the DOM does not grow | **fail**, both | **fail**, both | new spec |
| **DC-19/DC-34** | `declarations.spec.mjs`: pointing from the Formula Bar (builtin and Mud) | passed | **fail**, both, builtin and Mud | new spec |
| **SH-18/DC-22** | `sheet.spec.mjs`: the Formula Bar and the Cell Editor are one text | passed | **fail**, Edge only | new spec |
| **MEM-4** | `memory.spec.mjs`: disposal takes the module's listeners off the root | passed | **fail**, Chrome only | failed on both hosts |
| **WR-6**, ADR-0038/0030, RR-1 | `mud-app.spec.mjs`: Striped paints the table-stripe colour | passed | **fail**, Chrome only | failed on Server, Chrome |
| **FN-6a**, ADR-0045 | `sizing.spec.mjs`: a window narrower than the pinned block suspends pinning | **fail**, Edge only | passed | passed |

**What each new failure says:**

- **SH-18/DC-7** and **SH-2**: at the last row, 1048576 (Ctrl+Down to it; `XFD1048576` through
  the Name Box), the grid's `aria-activedescendant` is **empty**. The Focus's cell is not in the DOM.
  The expected pattern was `-r1048575c0` and `-r1048575c16383`
- **SH-18/DC-2/DC-3**: a column heading's selection is painted **22,369,617.79 px tall**, where the
  test expects more than 28,000,000 (10⁶ rows of 28 px). For the record, 2²⁵ ÷ 1.5 = 22,369,621.3,
  and 1.5 is this display's scale. The cause was not looked for
- **DC-19/DC-34**, Server: typing `=B2*C2=` into the Formula Bar key by key, the field held
  `=B2*C2`, then was empty, then held **`=`**. The text before the last key was lost
- **SH-18/DC-22**, Server, Edge: typing `*` into a Formula Bar that should hold only the new text,
  the field held **`=B3+1*`**. The earlier entry's text was still in it
- **MEM-4**, Server, Chrome: the list of the module's listeners on the root came back **empty**,
  where eight were expected: `copy`, `input` (capture), `keydown` (capture), `mousedown` (capture),
  `mouseleave`, `mousemove`, `mouseup` (capture) and `paste`. It
  passes on WebAssembly and on Server Edge. **The expected list includes the editor's `input`
  listener**, as the procedure says it now should
- **WR-6**, Server, Chrome: one row fewer than expected carried its probe (0..8, where 0..9 was
  expected), as in the first run
- **FN-6a**, WebAssembly, Edge: after the window narrows below the pinned block, "the Focus is
  whole on screen" was not true within 5 s. It passed on Chrome and on the Server host

**Now passing that failed in the first run:** MEM-4 on WebAssembly (both browsers) and on Server
Edge; RI-1 and RI-3 (`navigation.spec.mjs`), which the first run had passing on a rerun.

### The typing race at 150 ms

`circuit.spec.mjs`, "a click between keys is ordered with them (ED-22, ADR-0010)", **passed on both
browsers on the Server host**, through the latency proxy. That includes its 150 ms round-trip case,
"with a 150 ms round trip and no pause, each value lands in the cell clicked for it (SRV-5)", and
its 0, 30 and 60 ms cases. The other 150 ms tests of `circuit.spec.mjs` and `declarations.spec.mjs`
passed too: typing into an open field on a 150 ms circuit (the Cell Editor, builtin and Mud, and
the Name Box), DC-20, the two DC-28 tests and SRV-5/ED-22. **This differs from Part C's
`typing-probe-2.mjs`**, which lost everything in 6 of 12 trials at 30 ms. The probe addresses the
Server host directly (`localhost:6298`), not through the proxy, and waits only for `aria-busy` to
clear.

### The DC-33 marker, as the suite records it

`metrics.json` records `data-ex-grid="invariant"` as **kept** for the keyboard copy and the menu
copy, on both hosts, in both browsers. That agrees with Part C's probe.
