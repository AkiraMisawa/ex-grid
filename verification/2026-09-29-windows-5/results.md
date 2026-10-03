# Verification — 2026-09-29, Windows, fifth run: the browser by hand, and layer 3

**Scope: Parts B and C of [`verify-on-windows-5.md`](../../docs/specs/exsheet/verify-on-windows-5.md).**
Part A, the ordering operators asked of Excel, is in
[`../2026-09-29-windows-excel-5/results.md`](../2026-09-29-windows-excel-5/results.md).

**Verified commit: `3fa16a90fb42f0c4c8223189d93649a9e8996e52`**, the tip of
`claude/exsheet-start-8cx3v1` when this run began, for every part; `claude/exsheet-windows-verify-5`
is branched from it. The tip was not merged again during the run. Nothing is decided or changed here.
Each failure and disagreement is listed with its criterion, ADR or ticket, for the user.

## Environment

- The machine of the earlier runs: Windows 11 Pro (build 26200), 3840×2160 at **150%**
  (`devicePixelRatio` 1.5 in every page), light theme, regional format **en-GB** throughout B and C
  (Part A's `Set-Culture` was undone at 18:15:05)
- **Chrome 153.0.8010.54 and Edge 154.0.4258.37**, as installed, headed, driven by Playwright 1.62.1
  under a portable Node v24.14.1 from a copy of `tests/ExGrid.Browser` on Windows
  (`%LOCALAPPDATA%\exgrid-layer3\run-2026-09-29-5`), identical to the verified commit's, **with
  `npm ci` run afresh in it** (18:15)
- Both DemoHosts ran in WSL from the verified commit's build: the WebAssembly host on
  `localhost:5299`, the Server host on `localhost:6298` with `EXGRID_HOST_LOG` on the Windows temp
  directory. Part B addresses them directly; in the suite the browser reaches the Server host
  through `latency-proxy.mjs` (5298), which Playwright starts on Windows
- Build and layers 1–2 in WSL2 through nix at the verified commit: **0 warnings, 0 errors**;
  ExGrid.Tests 793, ExSheet.Engine.Tests 1771, ExGrid.MudBlazor.Tests 79, ExGrid.Components 868
  (1 skipped), ExSheet.Components.Tests 246 passed, 0 failed

## Part B — what changed in the browser, by eye and by hand

[`part-b-probe.mjs`](part-b-probe.mjs), once per host, browser and item, 18:20–18:25. **Every input
was real OS input** — the mouse and keys through [`input-server.ps1`](input-server.ps1), the fourth
run's helper with a few commands added (Shift+click, `VK_IME_OFF`, reading a line of screen pixels,
the clipboard, and the Notepad copy). Playwright opened the pages, waited as the suite's fixture
does (`#demo-interactive` attached, no grid `aria-busy`) and read the DOM; it sent no input. The
window was 1600×1000 at the display's own scale (`viewport: null`), and screen pixels were
calibrated against the page's own `mousemove` before any input. Every record is in
[`part-b/`](part-b/), one file per item, host and browser; the screenshots, cropped to the grid, in
[`shots/`](shots/). No page logged a console error or warning, and none raised an exception.

### 1. The grid's scrollbar (ADR-0029, third correction)

`/wide` and `/features`, the built-in Chrome. The pointer was put on the page's heading and left
there for 1.5 s; then the screen's pixels were read along the centre line of each gutter, and the
grid captured. Then the vertical thumb, where those pixels showed it, was dragged 150 CSS px down
with the real mouse (15 moves, 16 ms apart), and on `/wide` the horizontal thumb 150 CSS px right.

**The same on both hosts and both browsers:**

| | Gutter (CSS px) | The thumb at rest, in the gutter's pixels | The drag |
|---|---|---|---|
| `/wide`, vertical | 12 | **visible**: `#adadad` on the track's `#ffffff`, from 2 to 14.7 CSS px of a 588 px track (12.7 px long), one lighter pixel (`#afafaf`) at each rounded end | **moved it**: `scrollTop` 0 → 5,872,781.5 (Edge 5,872,782.5); the thumb then read at 151.3–164 (Edge 152–164.7) |
| `/wide`, horizontal | 12 | **visible**: `#adadad`, from 2 to 84 CSS px of an 888 px track | **moved it**: `scrollLeft` 0 → 1548.67; the thumb then read at 152–234 |
| `/features`, vertical | 12 | **visible**: `#adadad`, from 2 to 14.7 CSS px of a 320 px track | **moved it**: `scrollTop` 0 → 4616.67 (Edge on WebAssembly 4617.33) |
| `/features`, horizontal | 0 | — (no horizontal gutter: the columns fit) | — |

- The computed style agreed: `::-webkit-scrollbar` 12px wide on a transparent background,
  `::-webkit-scrollbar-thumb` `color(srgb 0 0 0 / 0.32)` with a 6px radius; neither
  `--ex-scrollbar-width` nor `--ex-scrollbar-color` was set on either page, so these are the
  defaults. `#adadad` is black at 32% over white
- The page saw the drags as a `mousedown` and a `mouseup` on `div.ex-scroller`
- **By eye**, in the screenshots, the thumb shows as a short grey rounded bar at the top of the
  vertical gutter and at the left of the horizontal one, and after the drag lower down and further
  right: [`B-scroll-wide-at-rest-wasm-chrome.png`](shots/B-scroll-wide-at-rest-wasm-chrome.png),
  [`B-scroll-wide-after-vertical-drag-server-msedge.png`](shots/B-scroll-wide-after-vertical-drag-server-msedge.png),
  [`B-scroll-features-after-vertical-drag-wasm-chrome.png`](shots/B-scroll-features-after-vertical-drag-wasm-chrome.png),
  and the same names for every host and browser. The fourth run found these gutters blank
- The procedure asks for one screenshot per browser; this run kept five per host and browser
  (at rest and after each drag, both pages)

### 2. Copy and paste after an edit (`b65b211`)

`/sheet`, a fresh page for each ending: a click on E1, `5`, then **Enter**, **Tab** or **Escape**;
a click on B2, Ctrl+C; a click on F6, Ctrl+V. Before the Ctrl+C the clipboard was set to a
sentinel, so a copy that wrote nothing would show. `VK_IME_OFF` was sent before typing.

**F6 showed B2's value, `12`, after all three endings, on both hosts and both browsers (12 of 12).**

| After | E1 then | Active cell before B2 | Ctrl+C on B2 | Ctrl+V on F6 |
|---|---|---|---|---|
| Enter | `5` | E2 | the clipboard held `12\r\n` (HTML Format, UnicodeText, Text and Chromium's own formats); the sentinel was gone | F6 shows `12`, the Formula Bar `12`, F6 active |
| Tab | `5` | F1 | the same | the same |
| Escape | empty (the edit discarded) | E1 | the same | the same |

- The page saw each key as a `keydown` on `div.ex-grid` followed by a `copy` or `paste` event on
  `div.ex-grid`, and document focus was on `div.ex-grid` throughout
- **Recorded only:** after each ending, and still after the click on B2, the document held a
  collapsed caret (`getSelection().type` "Caret") inside `div.ex-spacer`; after the Ctrl+C it held
  none. This is the state `b65b211`'s message describes, and the one its capture-phase handler
  clears before the browser runs the command
- Screenshots after the Ctrl+V: [`B-copy-after-Enter-wasm-chrome.png`](shots/B-copy-after-Enter-wasm-chrome.png)
  and the same names for Tab, Escape, each host and each browser

### 3. Plain text pasted over a range (ADR-0014, amended)

For each host and browser: `=A1` (three characters, no line break) written to a file, the file
opened in Notepad, Ctrl+A, Ctrl+C, and **that tab closed with Ctrl+W**. Then `/sheet`, a click on B2,
a Shift+click on C3, Ctrl+V.

**The same on both hosts and both browsers (4 of 4):**

| | Selection (painted ranges) | Name Box / active | B2 | C2 | B3 | C3 |
|---|---|---|---|---|---|---|
| before | **B2:C3**, one range | B2 | `12` | `0.5` | `7` | `0.75` |
| **after Ctrl+V** | **B2**, one range | **B2** | **`=A1`**, showing `Item` | `0.5` | `7` | `0.75` |

- **The value landed in B2 alone and the Selection became B2**, as the procedure expects. C2, B3 and
  C3 kept the page's opening values: `/sheet` is not empty there, unlike Excel's sheet in the
  fourth run. The Entries read back through the Formula Bar: B2 `=A1`, C2 `0.5`, B3 `7`, C3 `0.75`.
  D2 (`=B2*C2`) and D5 then showed `#VALUE!`, since B2 is text now
- The clipboard after Notepad's Ctrl+C: Text, UnicodeText and `EnterpriseDataProtectionId`; read
  again just before the Ctrl+V: Text and UnicodeText, `=A1`. Nothing of a spreadsheet's
- **Recorded only:** the live region (`.ex-announce`) still read "2 rows by 2 columns selected, B 2
  to C 3" after the Ctrl+V, when the Selection was B2 alone (ADR-0033: a 1×1 Selection is not
  announced)
- **Notepad.** One Notepad window was open before this item (the one the equality run left). Each
  of the four copies opened the file as a tab in it and closed that tab with Ctrl+W; afterwards one
  Notepad window was open and the file's tab was not. No window title was read into the records
- Screenshots: [`B-paste-B2-C3-selected-wasm-chrome.png`](shots/B-paste-B2-C3-selected-wasm-chrome.png),
  [`B-paste-Ctrl-V-wasm-chrome.png`](shots/B-paste-Ctrl-V-wasm-chrome.png) and the same names for
  each host and browser

### What went wrong in Part B

- **The first run of item 2 on WebAssembly Chrome stopped at its first clipboard read** (18:21:18),
  after the click on E1, `5`, Enter, the click on B2 and the Ctrl+C had gone out: "Requested
  Clipboard operation did not succeed", the clipboard held by another process for a moment. The helper now tries a clipboard access again, up to 20 times 100 ms apart, and the item
  was run again whole at 18:21:54. The stopped run's output is
  [`part-b/copy-wasm-chrome-attempt1.json`](part-b/copy-wasm-chrome-attempt1.json); its steps were not
  kept (the probe then wrote a case's steps only at its end, and now writes them as it goes)

## Part C — layer 3, both browsers, both hosts, at 150%

As in the fourth run: the whole suite from the Windows copy, `node node_modules\@playwright\test\cli.js
test`, once per host. **792 tests per run**: `chrome` and `msedge` with 388 each, and `chrome-150`
with the 16 its `grep` selects. MEM-5/6's soak (`EXGRID_SOAK=1`) was not run, as in the earlier runs.

| Host | Where the real cursor was | Log | Result |
|---|---|---|---|
| WebAssembly | physical (415, 569), where Part B's last click left it | [`layer3-wasm.log`](layer3-wasm.log) | **766 passed, 0 failed**, 26 skipped (17.3 min, 18:26–18:43) |
| Server | the same | [`layer3-server.log`](layer3-server.log) | 766 passed, **4 failed**, 22 skipped (16.0 min, 18:43–18:59) |
| Server, again | parked at physical (3700, 300), off every browser window | [`layer3-server-parked.log`](layer3-server-parked.log) | 768 passed, **2 failed**, 22 skipped (15.9 min, 19:08–19:24) |

**The second Server run is not in the procedure.** It was run after the reruns below showed that
UX-16's failure followed the real cursor: Part B's real input had left it where Playwright's browser
windows open, and it stayed there through the first two runs. Every run after 19:02 had the cursor
parked (`%LOCALAPPDATA%\exgrid-layer3\park-cursor.ps1`, which prints where it is before each run).

Each run's records are kept apart: `metrics-{wasm,server,server-parked}.json` and
`console-{wasm,server,server-parked}.json`. The console records hold only `harness.spec.mjs`'s error
on purpose and, on WebAssembly, VZ-12b's expected warning. `harness.spec.mjs`'s two `test.fail` tests
fail on purpose (they print as `x`) and are not failures. **`chrome-150` ran 16 tests in each run and
all 16 passed.**

### Failures, each rerun three times

The failing test alone, `--repeat-each=3`, on the project and host it failed on; logs `rerun-*.log`
(cursor where Part B left it) and `parked-*.log` (cursor parked). The last frame of each failure's
trace is in [`frames/`](frames/), with its error and page snapshot in `frames/<run>.txt`.

| Test | Criterion | Failed in | What it received | Reruns |
|---|---|---|---|---|
| **"a Cell State ground and the overlays paint over the stripe"** (`stripes.spec.mjs`) | UX-16 (ADR-0038/0029) | the first Server run, `chrome` and `msedge`; passed on WebAssembly and in the parked Server run | "the hover band over a stripe": the ground at row 11 **received `248,248,248,255`**, the stripe's colour before the hover, expected to differ. In the trace's DOM snapshots the page's `.ex-hover-row` already stood at `top: 224px` (row 8) before the test moved the pointer to row 11, and reached `top: 308px` (row 11) only after the test had read the colour. The last screencast frames are blank | cursor where Part B left it: **3 of 3 failed** on `chrome`, **3 of 3 failed** on `msedge`. Cursor parked: **3 of 3 passed** on each |
| **"DC-13: the edge auto-scroll carries a fill past the bottom of the Viewport"** (`declarations.spec.mjs`) | DC-13 (ADR-0050; the edge band, ADR-0008) | the first Server run, `msedge`; the parked Server run, **`chrome` and `msedge`**; passed on WebAssembly, and on Server `chrome` in the first run | after the fill, `goTo E2` and Ctrl+↓: the Name Box **received `E2` (2)**, expected a row past 20. The last frames take two shapes ([`frames/dc13-last-frames.png`](frames/dc13-last-frames.png), all ten): the Name Box at E2 with the series 1, 2, 3 … down column E in view; or the Name Box at **E77** (`76`) or **E81** (`80`), the Viewport painted with **no rows at all**, and the status line "1 cells selected (outside the visible range)" with "Go to selection" | `msedge`, cursor where Part B left it: **1 of 3 failed**. Cursor parked: `msedge` **3 of 3 failed**, `chrome` **3 of 3 failed** |
| **WR-6**: Striped paints the palette's table-stripe colour … a switch re-renders no row (`mud-app.spec.mjs`) | ADR-0038/0030, RR-1 | the first Server run, `msedge` | the rows' probe marks after the switch: 0..8, **9 missing**, the earlier runs' shape | 3 of 3 passed |

DC-13 passed on both hosts in the fourth run. Since the fourth run's commit, the commits that touch
`declarations.spec.mjs` or `ex-grid.js` are `ab309df` and `b65b211` (recorded only).

### The fourth run's failures, `--repeat-each=10`

On the host and project each failed on in the fourth run, all with the cursor parked; logs
`rep10-*.log`:

| Test | Criterion | Host, project | Failed | The failures |
|---|---|---|---|---|
| **DC-19/DC-34**, the mud Chrome case ("pointing from the Formula Bar; a press into the bar keeps its caret …") | ADR-0010 (DC-19, DC-34) | Server `chrome` | **0 of 10** (the fourth run: 5 of 10) | |
| **WR-7**: a Drawer toggle resizes the Stretch grid and its geometry follows | ADR-0028 | WebAssembly `chrome` | **0 of 10** | |
| WR-7 | ADR-0028 | WebAssembly `msedge` | **0 of 10** | |
| WR-7 | ADR-0028 | Server `chrome` | **0 of 10** | |
| WR-7 | ADR-0028 | Server `msedge` | **0 of 10** | |
| **`sheet-vs-excel` item 23**: deleting a row a Formula references writes #REF! in its place | ADR-0046, ticket 13 | Server `chrome` | **0 of 10** | |
| **WR-6**: Striped … a switch re-renders no row | ADR-0038/0030, RR-1 | Server `chrome` | **1 of 10** (repeat 8) | the probe marks after the switch: 0..8, **9 missing**. The last frame shows the Blotter in the dark scheme with the Drawer open, Positions showing rows T-1100001 to T-1100008 whole and the next one cut by the horizontal scrollbar ([`frames/rep10-wr6-server-chrome/`](frames/rep10-wr6-server-chrome/)) |
| WR-6 | ADR-0038/0030, RR-1 | Server `msedge` | **1 of 10** (repeat 10) | the same: 0..8, **9 missing** ([`frames/rep10-wr6-server-msedge/`](frames/rep10-wr6-server-msedge/)) |
| **FN-6a**: a window narrower than the pinned block suspends pinning, and widening restores it | ADR-0045 | WebAssembly `chrome` | **0 of 10** (the fourth run: 2 of 10) | |

In the whole suites, DC-19/DC-34, WR-7, item 23 and FN-6a passed on every project of both hosts;
WR-6 failed once, in the first Server run on `msedge` (above).

## Failures and disagreements, together

| What | Criterion, ADR, ticket | Where | How often |
|---|---|---|---|
| DC-13, the edge auto-scroll carrying a fill past the Viewport's bottom | DC-13, ADR-0050 (the edge band, ADR-0008) | Server `chrome` and `msedge` | first Server run: `msedge` failed, `chrome` passed; parked run: both failed; reruns 1 of 3 (`msedge`, cursor from Part B), 3 of 3 (`msedge`, parked), 3 of 3 (`chrome`, parked). WebAssembly passed |
| UX-16, the hover band over a stripe | UX-16, ADR-0038/0029 | Server `chrome` and `msedge`, with the real cursor over where the browser windows open | 2 of 2 in the first Server run and 6 of 6 reruns with the cursor there; 0 of 2 in the parked run and 0 of 6 reruns parked |
| WR-6, a probe mark missing after the scheme switch | ADR-0038/0030, RR-1 | Server `msedge` (first run); Server `chrome` and `msedge` (`--repeat-each=10`) | 1 in the first Server run (reruns 3 of 3 passed); 1 of 10 on each project |
| Part B: the live region still announced "2 rows by 2 columns selected, B 2 to C 3" after the paste collapsed the Selection to B2 | ADR-0033, ADR-0014 (amended) | both hosts, both browsers | 4 of 4 |
| Part A | ADR-0047, ticket 04 | — | none: 7 of 7 agree |

## The machine afterwards

Checked at 19:26, after the last run:

- Regional format en-GB; `HKCU\Control Panel\International` exported again, SHA-256
  `071c2b47…456b1af`, the same as the export taken before Part A
- No Excel running; the AutoRecovered workbook byte for byte unchanged
- Both DemoHosts stopped; no browser or Node process of this run left (the only Node running is
  another application's, running since 2026-09-27); no `input-server.ps1` left
- **One Notepad window open**, as before Part B: the four tabs this run opened were each closed with
  Ctrl+W, and their file is not open. No window title was read into the records
- The real cursor left parked at physical (3700, 300)
