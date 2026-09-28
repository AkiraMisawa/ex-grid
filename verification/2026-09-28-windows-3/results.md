# Verification — 2026-09-28, Windows, third run: ExSheet beside Excel, and layer 3

**Scope: Parts C and D of [`verify-on-windows-3.md`](../../docs/specs/exsheet/verify-on-windows-3.md).**
Part A, the oracle, is in
[`../2026-09-28-windows-excel-3/results.md`](../2026-09-28-windows-excel-3/results.md), and Part B in
[`../2026-09-28-windows-excel-3/active-cell.md`](../2026-09-28-windows-excel-3/active-cell.md).
Part E, other display scales, is optional, needs the user, and was not run.

**Verified commit: `b1f569a5aa166d12aa77c9c620959e2ab63d60d2`**, the tip of
`claude/exsheet-start-8cx3v1` when Part C began, merged into `claude/exsheet-windows-verify-3` as `b988a42`
before these parts ran. Parts A and B were verified at `20666707a4c8b7b71a899f25ad3e885a02da402c`;
the procedure lets C and D run at a later tip. Between the two, nothing changed in the case corpus,
`oracle.ps1`, the second run's scripts, `sheet-vs-excel.spec.mjs` or `playwright.config.mjs`.
Nothing is decided or changed here. Each failure and disagreement is listed with its criterion, ADR
or ticket, for the user.

## The gate, checked again at `b1f569a`

| Item | Present | Where |
|---|---|---|
| 1. ADR-0052 implemented, no `test.fail(true, 'ADR-0052` left in `sheet-vs-excel.spec.mjs` | yes | `67b5f81` merges it; 0 markers |
| 2. ADR-0053, with a `chrome-150` project in `playwright.config.mjs` | yes | `7f5fefa` |
| 3. `GridPasteIntent.Refuse` | yes | `48cf90d` |
| 4. The Server Formula Bar fix (DC-19/DC-22) | yes | `0efc1fe`, `a07788a` |
| 5. `main` merged in | yes | `git merge-base --is-ancestor b894b69 origin/claude/exsheet-start-8cx3v1` succeeds; `f3ae0e6` merges `main` |

**The narrowed focus hand-back** (ADR-0021, "Widened 2026-09-28") landed before Part C and is in
the verified commit: `e22c840`, merged by `4e66148`, and into the merge of `main` by `53012f3`. It
came in with the branch's tip; nothing was merged by hand beyond that tip.

## Environment

- The machine of Parts A and B: Windows 11 Pro 25H2 (build 26200), 3840×2160 at **150%**, regional
  format **en-GB** throughout C and D (A's `Set-Culture` changes had been undone at 10:35),
  display language en-GB
- Chrome 153.0.8010.54 and Edge 154.0.4258.37 as installed, the second run's builds, headed, driven
  by Playwright 1.62.1 under a portable Node v24.14.1 from a copy of `tests/ExGrid.Browser` on
  Windows (`%LOCALAPPDATA%\exgrid-layer3\run-2026-09-28-3`), identical to the verified commit's
- Both DemoHosts ran in WSL from the verified commit: the WebAssembly host on `localhost:5299`, the
  Server host on `localhost:6298` with `EXGRID_HOST_LOG` on the Windows temp directory. In the suite
  the browser reaches the Server host through `latency-proxy.mjs` (5298), which Playwright starts on
  Windows; the probes below address it directly, except the wheel probe's 100 ms case, which went
  through the same proxy started by hand
- Excel 16.0.20326.20158 for the clipboard probe and the Excel side of "What `main` brought"
- Build and layers 1–2 in WSL2 through nix, at `b988a42`: `dotnet build ExGrid.slnx` **0 warnings,
  0 errors**; `dotnet test ExGrid.slnx` **green**: ExGrid.Tests 766, ExSheet.Engine.Tests 1718,
  ExGrid.MudBlazor.Tests 79, ExGrid.Components 856 (1 skipped), ExSheet.Components.Tests 233
  passed, 0 failed
- The run's inputs went from 12:49 to 14:18. No Excel was ended by a guard, and the machine was
  left as it was found (below)

## Part C — ExSheet beside Excel

### `sheet-vs-excel.spec.mjs`

Three runs, each on Chrome and Edge (`--project=chrome --project=msedge`; `chrome-150` selects none
of these tests). The logs are in `../2026-09-28-windows-excel-3/`:

| Run | Log | Result |
|---|---|---|
| WebAssembly | [`sheet-vs-excel-wasm.log`](../2026-09-28-windows-excel-3/sheet-vs-excel-wasm.log) | 60 passed, **6 failed**, 14 skipped (5.0 min) |
| Server | [`sheet-vs-excel-server.log`](../2026-09-28-windows-excel-3/sheet-vs-excel-server.log) | 60 passed, **6 failed**, 14 skipped (2.7 min) |
| WebAssembly, `EXGRID_SHEET_UNBUILT=1` | [`sheet-vs-excel-unbuilt.log`](../2026-09-28-windows-excel-3/sheet-vs-excel-unbuilt.log) | 70 passed, **6 failed**, 4 skipped (5.5 min) |

Item by item, the same on both browsers in every run:

| Item | Result | Note |
|---|---|---|
| 1, 6–15, 21, 23, 25–27; active cell, cases 1, 2, 3, 6, 12 and 13 | passed everywhere | |
| **2 and 4** | **passed** everywhere | no `test.fail` any more; the procedure expected them to pass |
| **3 and 5** | **failed** everywhere | see below; the procedure expected them to pass |
| **20, all six probes** (including "the fill handle continues a series" and "a series filled up goes backwards") | **passed** everywhere | as the procedure expected |
| **22**, rows inserted over a selected range | **passed** everywhere | as the procedure expected |
| active cell, **cases 7 and 8** | **failed** everywhere | see below |
| 16, 17, 19 (four probes) | skipped (ticket 14 not `done`); **passed** unbuilt | |
| 18, 24 | skipped (`test.fixme`) | 18 is `clipboard-probe.mjs` below; 24 waits on ticket 12 |

**Items 3 and 5, and active-cell cases 7 and 8 (ADR-0050 §1, ADR-0052, ticket 06; ADR-0053).**
Each sets the Sheet's `scrollTop` to *n* × 28 and then expects row *n* + 1 to be painted: A100 after
`scrollTop = 99 * 28` (items 3 and 5, spec lines 187 and 216), E300 after `299 * 28` (cases 7 and 8,
line 315). The cell is not found within 5 s, on both hosts and browsers. The test body after that
line did not run, so what these items assert about Excel was not reached.
[`sheet-scroll-probe.mjs`](sheet-scroll-probe.mjs) read what happens at that step
([`sheet-scroll-probe.json`](sheet-scroll-probe.json)), on Chrome against the WebAssembly host:

| Chrome's scale | Layout Ceiling told | Spacer declared | `scrollTop` asked | Top row shown | Row *n* + 1 painted |
|---|---|---|---|---|---|
| the display's (150%) | 22,369,618 | 22,369,362 | 2,772 (99 × 28) | **130** | no |
| | | | 8,372 (299 × 28) | **393** | no |
| forced to 1 | 33,554,428 | 29,360,156 | 2,772 | 100 | yes |
| | | | 8,372 | 300 | yes |

At the display's 150% the Sheet's spacer is compressed (29,360,156 → 22,369,362, a factor of
1.3125), so `scrollTop` = *n* × 28 shows a later row than *n* + 1. At a forced scale of 1 nothing
is compressed and the rows are where the test expects them. Under Playwright's default viewport
`devicePixelRatio` reads 1.0000000298 while the ceiling is still the 150% one.

### The clipboard, both directions (`clipboard-probe.mjs`)

The second run's probe, changed in one way: straight after each paste, before any click, it reads
the Name Box and the live region's Selection as well as the notice
([`clipboard-probe.mjs`](../2026-09-28-windows-excel-3/clipboard-probe.mjs)). Once on Chrome and once
on Edge against the WebAssembly host:
[`clipboard-probe-chrome.json`](../2026-09-28-windows-excel-3/clipboard-probe-chrome.json),
[`clipboard-probe-msedge.json`](../2026-09-28-windows-excel-3/clipboard-probe-msedge.json). **The two
browsers gave the same answers throughout**, the page had focus at every paste and copy, and the
console was empty. Excel copied and showed the date as `26/09/2026` (en-GB), as in the second run.

**Excel → ExSheet, the source column wide enough**: F2 `6`, F3 `26/09/2026` as text, F4 `1,234.50`
(1234.5), no notice; the Name Box `F2`, the Selection F2:F4. The same as the second run.

**Excel → ExSheet, the source column too narrow (width 6).** Excel's clipboard holds `6`,
`26/09/2026`, `1,234.50` as text and `6`, `######`, `######` as HTML. **ExSheet refused the paste by
name**, on both browsers:

> Nothing was pasted: the field for F8 is '######', which is how the source shows a value too wide
> for its column. The source column was too narrow to show the value, so the value itself was not
> copied. Widen the column there and copy again.

F7:F9 stayed empty, and **the Name Box stayed `F7`**. The live region, read at the same moment, still
held `3 rows by 1 columns selected, F 2 to F 4`, its announcement of the wide paste before; the
click on F7 that preceded this paste selected one cell. This is what the procedure expected, where
the second run wrote nothing and said nothing.

**ExSheet → Excel** (E2 `=B2*2`, E3 `9/26/2026`, E4 `1234.5` as `#,##0.00`), by Ctrl+C and by the
Context Menu's Copy:

| | Chrome, keyboard | Chrome, menu | Edge, keyboard | Edge, menu |
|---|---|---|---|---|
| `data-ex-grid="invariant"` in the clipboard's HTML | **present** | **present** | **present** | **present** |
| Excel's paste: D1/H1 | 24, General | 24, General | 24, General | 24, General |
| D2/H2 | 46291, `m/d/yyyy` | the same | the same | the same |
| D3/H3 | 1234.5, `#,##0.00`, shows `1,234.50` | the same | the same | the same |

The same as the second run: the marker survives the real clipboard on both routes in both browsers,
and Excel takes the values with their formats.

### The typing race (`typing-probe-2.mjs`)

The first run's probe, changed only to run twelve trials per pause and to wait as the suite's
fixture does ([`typing-probe-2.mjs`](../2026-09-28-windows-excel-3/typing-probe-2.mjs)): on Chrome,
against the Server host directly (`localhost:6298`), click F1, type `1`, Enter; F2, `2`; F3, `3`;
F7, `7`, with the pause between steps. Output:
[`typing-probe-2.json`](../2026-09-28-windows-excel-3/typing-probe-2.json).

**The wait used: `#demo-interactive` attached, then no `.ex-grid` with `aria-busy`**, each with a
60 s limit, before the first click. `#demo-interactive` is rendered by the Routes component once
`RendererInfo.IsInteractive`, which on the Server host is once the circuit has connected. The
second run's copy waited for the same two, but let the first time out silently after 30 s; here a
wait that ran out would have been recorded against its trial. None ran out: the page was
interactive 100 ms or less after the navigation ended, and no grid was busy 121–178 ms after it.

| Host | 0 ms | 30 ms | 60 ms |
|---|---|---|---|
| Server (`localhost:6298`, direct) | **right, 12 of 12** | **right, 12 of 12** | **right, 12 of 12** |

Every trial put `1`, `2`, `3` and `7` in F1, F2, F3 and F7, and left the Name Box on F8. The second
run's "nothing landed" (6 of 12 at 30 ms) did not happen.

### What `main` brought, beside Excel (ADR-0054, ADR-0055, ADR-0050 item 5)

The same steps in both, from the cells `/sheet` opens with. Excel:
[`main-keys.ps1`](../2026-09-28-windows-excel-3/main-keys.ps1), real keys and the real mouse, each
step read through COM once Excel is Ready
([`main-keys.jsonl`](../2026-09-28-windows-excel-3/main-keys.jsonl)); its sheet has /sheet's cells
less the Linked Table's three Formulas, B7's date as its serial with `m/d/yyyy`. ExSheet:
[`main-keys-probe.mjs`](../2026-09-28-windows-excel-3/main-keys-probe.mjs), Playwright on `/sheet`
at a 150 ms pace, on both hosts and both browsers (`main-keys-exsheet-<host>-<browser>.json`).
**The four ExSheet runs gave identical answers.** Screenshots are `C-M<case>-…` in
[`../2026-09-28-windows-excel-3/shots/`](../2026-09-28-windows-excel-3/shots/): Excel's without a
suffix, ExSheet's with `-exsheet-<host>-<browser>`. The Selection is `Selection.Address` in Excel
and the live region in ExSheet; the active cell is `ActiveCell` and the Name Box.

| Case | Excel | ExSheet | |
|---|---|---|---|
| **Delete** over B2:D4 | B2:D4 empty; B5 `0`, D5 `0`; Selection B2:D4, active B2 | the same | agree |
| then **Ctrl+Z** | every cell back (B5 `39`, D5 `15.25`); Selection B2:D4, active B2 | the same | agree |
| **Backspace** on B3 (`7`) | Enter mode (status bar "Enter"), the cell and the Formula Bar empty; Name Box B3 | the Cell Editor open and empty, the Formula Bar empty; Name Box B3 | agree |
| then Enter | B3 empty, B5 `32`; active B4 | the same | agree |
| **Ctrl+D** over B2:B5, B2 `=A2*2`, A2:A5 `1..4` | B3:B5 `=A3*2`, `=A4*2`, `=A5*2` (4, 6, 8); Selection B2:B5, active B2 | the same Entries and values; Selection B2:B5, active B2 | agree |
| **Ctrl+R** over B2:E2, B2 `=B1+1`, B1:E1 `10..40` | C2:E2 `=C1+1`, `=D1+1`, `=E1+1` (21, 31, 41); Selection B2:E2, active B2 | the same | agree |
| **Ctrl+Enter** with `=A1` over B2:C3 | **B2 `=A1`, C2 `=B1`, B3 `=A2`, C3 `=B2`** (Item, Qty, Apples, Item); Selection B2:C3, active B2 | **`=A1` in all four** (Item in all four); Selection B2:C3, active B2 | **disagree** |
| **Ctrl+F** for `needle` in A5000, from A1, then Enter | the Find and Replace dialog; A5000 found and active, the dialog stays; **the view scrolled to rows 4972–5029, A5000 in its middle** | the grid's Find panel; A5000 found and the Focus (Name Box A5000), the panel stays; **A5000 on the view's bottom row** (rows 4988–5000 shown) | found alike; revealed differently |
| then Escape | the dialog closes; active A5000, scroll row 4972 | the panel closes; the Focus A5000, still in view | agree |

**Ctrl+Enter (ADR-0052 case 13, ADR-0050 item 5, DC-40).** Excel writes `=A1` into the active cell
and shifts its relative References into the others, as a fill does. ExSheet writes the same text,
`=A1`, into every cell. ADR-0050 says Ctrl+Enter's intent names no fill source, so ExSheet takes it
as that text in each cell. Listed as a disagreement with Excel, not decided.

**Where a found cell is revealed (ADR-0055, FD-5).** Both reach A5000 and leave the dialog or panel
up. Excel scrolls the row to the middle of the view; ExSheet scrolls as far as it must, and the row
lands at the bottom. FD-5 asks that the cell be revealed, and it is. Recorded, not decided.

**One Excel case was asked twice.** The first attempt at case 4 (13:07) typed `=B1+1` through
`SendKeys`, where `+` means Shift: Excel received `=B1!`, raised its formula error dialog, and the
script's next COM call failed. The script was changed to send `{+}`, and the second attempt (13:09)
is the record. Both are in `main-keys.jsonl`; B2 was left unchanged by the first.

## Part D — layer 3, both browsers, both hosts, at 150%

As the second run's Part D: the whole suite from the Windows copy, `node
node_modules\@playwright\test\cli.js test`, once per host, each teed here. **720 tests per run**:
`chrome` and `msedge` with 355 each, and the new `chrome-150` project with the 10 its `grep`
selects. MEM-5/6's soak (`EXGRID_SOAK=1`) was not run, as in the earlier runs.

| Host | Log | Result |
|---|---|---|
| WebAssembly | [`layer3-wasm.log`](layer3-wasm.log) | 685 passed, **9 failed**, 26 skipped (25.0 min, 13:12–13:37) |
| Server | [`layer3-server.log`](layer3-server.log) | 687 passed, **11 failed**, 22 skipped (17.6 min, 13:38–13:55) |

Each host's records are kept apart, since both runs write the same files:
[`metrics-wasm.json`](metrics-wasm.json), [`metrics-server.json`](metrics-server.json),
[`console-wasm.json`](console-wasm.json), [`console-server.json`](console-server.json). **The console
records hold no error.** `console-wasm.json` holds one entry per browser, the ExGrid warning the
VZ-12b test (`stretch.spec.mjs`) provokes on purpose and names; `console-server.json` is empty. Every
skip is by design: MEM-5/6's soak, the Server-only tests on WebAssembly and the one
WebAssembly-only test on Server, `measure.spec.mjs`'s two measurements, and the `sheet-vs-excel`
items waiting on tickets 12 and 14.

### Failures, by criterion, and three reruns of each

Each failing test was run again three times (`--repeat-each=3`) on its own host and project,
right after both runs: `rerun-wasm-chrome.log`, `rerun-wasm-msedge.log`, `rerun-server-chrome.log`,
`rerun-server-msedge.log` and `rerun-server-chrome-150.log`, beside this file.

| Criterion | Test | WebAssembly | Server | Reruns (3 each) | Second run |
|---|---|---|---|---|---|
| **MK-6**, ADR-0043 | `marks.spec.mjs`: after "mark all", rows scrolled to far away paint ticked | **fail**, both | **fail**, both | **fail 3 of 3**, everywhere | passed |
| **ADR-0050 §1, ADR-0052, ticket 06** | `sheet-vs-excel.spec.mjs` items 3 and 5 | **fail**, both | **fail**, both | **fail 3 of 3**, everywhere | `test.fail` (expected) |
| **ADR-0052** | `sheet-vs-excel.spec.mjs` active cell, cases 7 and 8 | **fail**, both | **fail**, both | **fail 3 of 3**, everywhere | new |
| **FN-6a**, ADR-0045 | `sizing.spec.mjs`: a window narrower than the pinned block suspends pinning | **fail**, Chrome | passed | **pass 3 of 3** (WebAssembly, Chrome): comes and goes | failed, WebAssembly Edge |
| **DC-19/DC-34** | `declarations.spec.mjs`: pointing from the Formula Bar (mud Chrome) | passed | **fail**, Chrome | **pass 3 of 3**, builtin and mud: comes and goes | failed on Server, both |
| **WR-6**, ADR-0038/0030, RR-1 | `mud-app.spec.mjs`: Striped paints the table-stripe colour | passed | **fail**, Chrome | **pass 3 of 3**: comes and goes | failed, Server Chrome |
| **BIG-5** | `virtualisation.spec.mjs`: the first and the last row paint their own data | passed, all three projects | passed on `chrome` and `msedge`; **fail on `chrome-150`** | **fail 1 of 3** (Server, `chrome-150`): comes and goes | failed, both hosts |

**What each says:**

- **MK-6**: the test sets `/marks`' `scrollTop` to 700,000 × 28 and waits for row 700,000's cell. It
  is not there within 15 s; the page's snapshot at the failure shows rows **876,204** onwards. The
  same pattern as the three `sheet-vs-excel` failures (Part C): a `scrollTop` computed as rows × 28
  on a grid whose scroll height is compressed at 150% (ADR-0053). The marking itself was not reached
- **`sheet-vs-excel` items 3 and 5, cases 7 and 8**: as in Part C, row 100 (or 300) is not painted
  after `scrollTop` = 99 × 28 (or 299 × 28); at 150% that shows row 130 (or 393)
- **FN-6a**, WebAssembly Chrome: after the window narrows below the pinned block, "the Focus is whole
  on screen" was not true within 5 s. The same failure the second run had on WebAssembly Edge
- **DC-19/DC-34**, Server Chrome, mud Chrome: after a press into the Formula Bar, the pointing
  outline (`.ex-point`) was still there after 5 s, where the test expects it gone
- **WR-6**, Server Chrome: one row fewer than expected carried its probe (0..8, where 0..9 was
  expected), as in the first two runs
- **BIG-5**, Server, `chrome-150`: after `scrollTop = scrollHeight`, row 999,999 was not painted
  within 15 s; the snapshot at the failure still shows rows 0–20. Both failures (the run's and the
  rerun's) are at the first scroll to the end (line 132)

**Now passing that failed in the second run**, on both hosts and browsers: **VZ-14** (the
`scrollbar.spec.mjs` group "the Focus is never behind a scrollbar" and "the Focus stays readable
with the OS doing the scaling"), **BIG-1**, **BIG-5** (on `chrome` and `msedge`), **SH-2**,
**SH-18/DC-7** and **SH-18/DC-2/DC-3**, the ones the procedure named; and also SH-18/DC-22 and MEM-4
on the Server host. VZ-15, new, passed everywhere. `sheet-vs-excel` items 2, 4, 20 and 22 pass, as
Part C found.

### The `chrome-150` project on Windows

It launches the installed Chrome with `--force-device-scale-factor=1.5 --window-size=1280,800` and
the viewport left to the window (`viewport: null`), and runs the 10 tests its `grep` selects: the
four "never behind a scrollbar" and VZ-14 tests, SH-18/DC-7, SH-18/DC-2/DC-3, SH-2, BIG-1, VZ-15 and
BIG-5. **On this machine the display is already at 150%, so the forced factor is the display's
own.** On WebAssembly all 10 passed. On Server 9 passed and BIG-5 failed, then failed once in its
three reruns (above). The ordinary `chrome` and `msedge` projects ran the same 10 tests at the OS's
150%, and passed them all on both hosts.

### The mouse wheel on `/wide` at 10⁶ rows

[`wide-probe.mjs`](wide-probe.mjs), headed, the window 1600×1000 at the display's 150%, the viewport
left to the window. The wheel is **one real notch** (`mouse_event`, 120) through
[`os-input.ps1`](os-input.ps1) at a point in the grid, after the cursor was placed there and the
page reported the move where it was aimed. "Near the end" is after a click on a cell in view and a
real Ctrl+End. The view's top is read every animation frame for 2 s after the notch, from the
painted cells. Outputs: `wide-probe-<host>-<browser>.json`, screenshots `shots/D-wheel-*`. The Server
host was asked directly (`localhost:6298`) and through `latency-proxy.mjs` at a 100 ms round trip
(`server-rtt100`).

| | At the top, one notch down | Near the end, one notch up | then one notch down |
|---|---|---|---|
| `scrollTop` moved | 100 px | −100 px | 100 px |
| **Rows moved** (every host and browser) | **4.47** | **−4.33** | **4.33** |

For comparison: 100 px is 3.57 rows of 28 px uncompressed, so 4.47 rows is 1.25 times that, and
Part B measured Excel at 3 rows per notch at 100% zoom. Both browsers and all three hosts moved the
same amount.

**Whether the rows jitter.** In each frame the probe compared the rows' movement with the native
scroll's. On the Server host most frames of the scroll moved the rows by the uncompressed amount
(Δ`scrollTop` ÷ 28), and the render then caught up in one to three frames in which `scrollTop` did
not move and the rows did:

| Host | Frames that moved uncompressed (of about 20 scrolling) | Catch-up frames | Largest catch-up |
|---|---|---|---|
| WebAssembly, Chrome and Edge | 0 at the top; 1–4 near the end | 0 at the top; 1 near the end | 0.05 rows |
| Server, direct, Chrome and Edge | 18–19 at the top; 3–6 near the end | 1–4 | 0.29 rows |
| Server, 100 ms round trip, Chrome and Edge | 22 at the top; 3–5 near the end | 2–3 | **0.62 rows** |

No frame moved the rows against the scroll by more than 0.05 rows. The largest was 0.048 rows, on
WebAssembly near the end. Recorded, not judged: ADR-0053 expects the rows to drift by (k − 1) × Δs
between the native scroll and the answering render on a Server circuit.

### Ctrl+Plus to 200%, then Ctrl+End, on `/wide`

The same probe, with real keys: Ctrl+Plus five times (Chrome's and Edge's steps 110, 125, 150, 175,
200%; `devicePixelRatio` 1.5 → 3), then Ctrl+End. Two orders: **as run first**, a click on a cell,
the zoom, then Ctrl+End with the page where the zoom left it; and **with the grid in view**, the
zoom, the page scrolled so the grid's top is at the window's top, a click on a cell, then Ctrl+End.
Outputs `zoom-probe-<host>-<browser>.json`; screenshots `shots/D-zoom-200-ctrl-end-<order>-*`,
captured from the screen. The same on both hosts and both browsers:

| | As run first | With the grid in view |
|---|---|---|
| The Focus | r999999c99 (K-999999, M99) | the same |
| **The last row painted** | **yes**, `K-999999`, whole inside the grid's readable box | the same |
| **The Focus whole inside the grid's readable box** | **yes** | **yes** |
| **The Focus whole in the browser window** | **no**: the window shows the page's heading and the grid's first rows; the Focus is at y 872–900, the window 453 CSS px tall | **no**: the grid's top at the window's top, the Focus at y 564–592 and x 833–924, the window 793 × 453 |
| The Layout Ceiling told / spacer | 11,184,810 / 11,184,553 px | the same |

At 200% the grid, 924 × 592 CSS px, is larger than the window's 793 × 453, so its bottom rows and
right columns are outside the window whatever the page scroll. Inside the grid the last row and the
Focus are whole. The page, not the grid, would have to scroll for the Focus to be seen.

**One capture was redone.** The first pass (`wide-probe-*.json`'s `zoom` entry, and
`shots/D-zoom-200-ctrl-end-<host>-<browser>.png`) took Playwright's screenshot, which at 200% showed
the page shifted from the geometry read at the same moment, and measured the Focus against the grid
only. The record is the second pass, `zoom-probe-*.json`, captured from the screen by
[`window-shot.ps1`](window-shot.ps1). The first pass's grid-relative answers were the same.

**One wheel pass was redone.** The first pass on WebAssembly Chrome
([`wide-probe-wasm-chrome-attempt1.json`](wide-probe-wasm-chrome-attempt1.json)) reached the end by a
Playwright click on row 0's cell, scrolled out of view by then, and Ctrl+End did not take the view
there. Its two "near the end" notches were taken at the top instead. The probe was changed to click
a cell in view and send a real Ctrl+End, and every host and browser was run with that.

## The machine afterwards

The regional format was en-GB throughout C and D and was not changed. `HKCU\Control Panel\International`
exported at 14:19 is identical, byte for byte, to Part A's `international-after.reg` (SHA-256
`071c2b47…56b1af`). No Excel is running; the AutoRecovered workbook in `%APPDATA%\Microsoft\Excel` is
untouched (8 KB, 2026-09-02). No Playwright browser or node process was left; the DemoHosts in WSL
were stopped.
