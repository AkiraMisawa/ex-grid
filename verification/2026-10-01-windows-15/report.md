# Windows, fifteenth run: VZ-14 at the current base, a real Japanese IME, ticket 74's readings

**Verified commit: `1655299`** (`claude/exsheet-ime-and-scaling`), on branch `claude/exsheet-windows-verify-15`
from it. The procedure is `docs/specs/exsheet/verify-on-windows-15.md` as it stands there, all three parts.
Run on 2026-10-01 from 21:40 to 22:37 local time (BST), by the Claude Code session on the Windows desktop
of the earlier runs, through WSL. **Nothing was decided.** No ADR, `CONTEXT.md` or
`docs/definition-of-done.md` was changed.

## Not run: Part A at 125%

Part A asks for `scrollbar.spec.mjs` at 125% and at 150%. **Only 150% was run.** The display was at 150%,
the user's standing instruction to this session is not to change the display scale, and on this machine a
change of scale needs a Windows sign-out, which ends WSL and this session. 125% remains to be run.

## Files

| | |
|---|---|
| `report.md` | this report |
| `scrollbar-150-wasm.log`, `scrollbar-150-server.log` | Part A, each run's list reporter |
| `scrollbar-150-wasm.json`, `scrollbar-150-server.json` | Part A, Playwright's JSON report: the annotations hold the gutter and the DPR |
| `ime-probe.mjs` | Parts B and C in the browsers: the probe |
| `input-server.ps1` | the probe's real OS input (SendInput), with the IME's keys and its state |
| `ime-ui-trial.mjs`, `ime-ui-trial.ps1` | the trial that looked for the IME's candidate window |
| `records/` | one JSON per page, configuration and browser: every state read |
| `shots/` | the page's picture of the Sheet in every state (`<page>-<case>-<state>-<config>-<browser>.png`), for WebAssembly and Chrome, both pages and Part C (the other configurations' pictures stay on this machine: below) |
| `summarise.py`, `summary.txt` | the records grouped by state across the configurations |
| `excel/ask-excel.ps1` | Parts B and C in Excel |
| `excel/ask-excel.jsonl` | one line per Excel case: per state what was read |
| `excel/shots/` | Excel's pictures per state (the window, the Formula Bar, the Name Box, the mode, the cells, popups): Part B's pass b (`<case>-b-…`), the checks `e1`-`e3`, Part C, and the first pass's i1 |

## Environment

- **Windows 11** (10.0.26200, 25H2, UBR 9457), one display of 3840 × 2160 device px at **150%** (DPI 144; 2560 ×
  1440 to a process that is not DPI-aware). Windows' mode **light** (apps and system). High contrast off.
- **Regional format en-GB**, unchanged. The language list: **en-GB** (keyboard English (UK), 0x0809), en-US,
  and **ja** with the **Microsoft IME** (TIP `{03B5835F-F03C-411B-9CE2-AA23E1171E36}`, profile
  `{A76C93D9-5523-4E90-AAFA-4DB112F9AC76}`; `imjptip.dll` 10.0.26100.9278). The Japanese keyboard is the
  US layout here (kbd101.dll). One input method for every window (the per-app setting is off), so a
  window switched to Japanese switches the desktop: every run put English (UK) back at its end (below).
- **The IME's mode: Hiragana, romaji input** (`MSIME\romastyle` MS-IME). Read through `WM_IME_CONTROL`:
  conversion mode 0x19 (native, full-width, romaji) with the IME off, 0x9 once it was switched on (the romaji
  bit is not reported then; the input stayed romaji: `k`, `a` composed `か`). The IME's settings, read and
  not changed: prediction on with the input history (`PredictionUseInputHistory` 1), cloud suggestions off.
- **Chrome 153.0.8010.54** (as the browser reports it; 154.0.8037.92 is installed beside it and waits for
  every Chrome to close, which the user's Chrome being open prevents) and **Edge 154.0.4258.48**, the Windows
  builds, headed, at the display's own scale (`viewport: null`, DPR 1.5).
- **Excel**: Microsoft 365, version 2609 (16.0.20430.20092, x64), English (UK) UI, the Office Theme "Use system setting".
- The runner: portable Node v24.14.1 on Windows, Playwright 1.62.1, from a copy of `tests/ExGrid.Browser`.
- The DemoHosts, built at `1655299`, ran in WSL2 (.NET SDK 10 via nix): WebAssembly on `localhost:5299`, the
  Server host on `localhost:6298`. Behind 150 ms, `latency-proxy.mjs` on Windows (5298 → 6298, control on
  7298), its round trip set to 150 ms for the run and back to 0 after it.

## Method

1. **Keys** go through SendInput as a virtual-key code and a scan code per key, never as a Unicode
   character, so that the IME composes them. The IME is switched on and off by its own keys, **VK_IME_ON
   (0x16)** and **VK_IME_OFF (0x1A)**. The window is switched to the Japanese keyboard by
   `WM_INPUTLANGCHANGEREQUEST` (0x04110411), as Win+Space would, for Part B; Part C keeps English (UK). The
   probe's sending thread types through English (UK); every character typed with the IME off (`= + ( ) :
   , [ ] % .`, letters, digits, space) is the same key on the US-layout Japanese keyboard.
2. **The browsers** (`ime-probe.mjs`): Playwright opens the page and reads; it sends no input. The page gets
   the probe's listeners, in the capture phase on the window: `focusin`, `keydown` (key, code, keyCode,
   `isComposing`), `compositionstart`, `compositionupdate`, `compositionend` (with `data`), `beforeinput` and
   `input` (inputType, data, `isComposing`), each with the field's value at that moment. After every step:
   600 ms (900 ms behind 150 ms), then the DOM read (which element has the keyboard, whether an edit is open,
   the Focus, the Name Box, the Cell Editor's and the Formula Bar's value, caret and coloured layer, the
   completion list, the argument hint, Find's field and outcome, Reference Outlines and Point's dashes, D10
   and D11), the events since the last reading, the IME's open status and mode, and the page's picture of
   the Sheet (and of Find's popover when it is open), with the colours read from it in each coloured span's
   box and in the whole field.
3. **Between cases** the probe presses Escape until no edit, list, Find or Name Box keeps the keyboard; then,
   if the IME is still on, it opens an edit with F2, switches the IME off there and presses Escape (see "The
   IME and the grid" below: VK_IME_OFF with the keyboard on the grid leaves the IME on). D10 and D11 are
   cleared with Delete before a case that finds them holding something. After each case, D10 is selected
   and its Entry read from the Formula Bar.
4. **The configurations**: `/sheet` and `/sheet?chrome=mud`, Chrome and Edge, WebAssembly, Server, and Server
   behind 150 ms: twelve records, 21:59 to 22:27. Part C: `/sheet`, Chrome, WebAssembly.
5. **Excel** (`excel/ask-excel.ps1`): the thirteenth run's script with this run's cases and the IME's steps.
   An Excel of the script's own per case (`New-Object`, never an Excel already running), Book1 maximised at
   100%, the Table `Positions` in A1:B4, D10 selected; real keys and the real mouse; 600 ms after each state a
   picture (PrintWindow) and another 300 ms later; UI Automation for the Formula Bar, the Name Box, the status
   bar's mode and any popup; and the IME's state. Part B's Excel cases ran twice: the second time, pass b,
   with no UI Automation at all ("Excel's keys went to the Name Box", below).
6. **Additions, beside the procedure's cases** (marked as additions in the records):
   - **i1x, i2x, i3x**: i1, i2 and i3 with the edit opened by F2 first and the IME switched on in the Cell
     Editor. In ExSheet the procedure's own keys never compose (below), so these are the cases where the
     reading of ED-11 is asked.
   - **i1y**: i1 with the IME left on by an earlier edit (F2, the IME on, Escape), as a user who was typing
     Japanese a moment ago.
   - **i4 and i8 switch the IME on again** once the keyboard is in the bar or in Find. A VK_IME_ON with the
     IME already on changes nothing.
   - **i10** is i1 (i10a), i1x (i10ax) and i4 (i10b) at a typist's pace, 150 ms between keys, behind 150 ms,
     read once after the last key and again 2 s later.
   - **Excel i1x**, as ExSheet's.

## The IME's candidate window

The IME's candidate window could not be pictured or read on this machine. **So what the IME chose is read
from the composition itself**: the page's `compositionupdate` data and the field's value, and in Excel the
cell and the Formula Bar.

- **No window of the browser's or Excel's holds it.** With the candidates up after `kana`, Space, Space
  (`ime-ui-trial.mjs`, on a page of the trial's own), no top-level window appeared or went (the visible
  windows were listed before and after: only the desktop's `Progman` changed its handle's order).
- **The IME's UI is the Windows Input Experience's** (`TextInputHost`, one `Windows.UI.Core.CoreWindow` over
  the whole screen). UI Automation shows that window by name only ("Windows Input Experience") and nothing
  under it. TextInputHost has no top-level element in UI Automation's tree at all.
- **PrintWindow of that window**, with and without `PW_RENDERFULLCONTENT`, gave every pixel empty.
- **The screen copy** (`CopyFromScreen`) gave every pixel black, as on the eighth run.

## The IME and the grid

**With D10 selected, the keyboard is on the grid itself (`div.ex-grid`), and there VK_IME_ON leaves the IME
off.** Seen in a trial before the recorded runs, and then in every configuration of i1, i2, i3 and i8's
first step.

- The page sees a `keydown` whose key is `Unidentified`. ExSheet does nothing with it.
- The IME's open status reads `False` afterwards. The grid is not a text field, and Chrome and Edge give
  it no input context.
- **So the procedure's keys for i1, i2 and i3 never compose in ExSheet.** `k` opens the Cell Editor as
  an ordinary key, and `ana` follow as Latin letters (`kana`).
- **Excel composes from the first key** with D10 selected (i1-b: `ｋ`, `か`, … in the cell).
- VK_IME_OFF with the keyboard on the grid also leaves the IME as it was. The probe therefore switches
  the IME off in an F2 edit between cases (Method 3).
- **i1y** shows the case a user meets after typing Japanese a moment earlier: the IME is left on, and
  D10 is selected.
  - `k` opens the Cell Editor as an ordinary key.
  - Once the editor has the keyboard, `ana` compose. Space converts them, and D10 holds `k穴`.

## Excel's keys went to the Name Box: the first pass of Part B, and pass b

**In Part B's first pass, Excel's i1, i1x, i2, i3 and i4 did not record what Excel does.** The cause was
the tool. (i7's keys go to the Name Box anyway; it is reported from pass b like the rest.)

- **i1, i2, i3 (D10 selected, Ready).** UI Automation's focused element became the Name Box (`Edit`) at
  the first composed key. The IME's composition window (`MSCTFIME Composition`) stood over the Name Box,
  and after Enter the Name Box held the composition. D10 stayed empty.
- **i1x and i4.** The edit was open, and the same happened after a reading. i4's bar held `ｋ`, and the
  Name Box got `穴`.
- **i5, i6 and i9 were not affected.** Their edits held text before the IME was switched on.

Three checks found where it came from (`e1`, `e2`, `e3` in `excel/ask-excel.jsonl`):

- **e1.** The IME on, then `kana`, Space and Enter in one state, with nothing read between the keys.
  The composition went into the cell. The next Enter, after a reading, did not commit.
- **e2.** e1 with no UI Automation in the case at all. D10 = `かな`, and the active cell D11.
- **e3.** i4 with no UI Automation. D10 = `かな`, and D11.
  - Its first run failed, because without UI Automation the bar had no place.
  - It ran again in pass b with the place found in i1.

**So the UI Automation reads between keys sent the IME's composition, or the next key, elsewhere.** The
tenth run saw something like it after a click into an empty Formula Bar.

**Pass b** (`-Pass b -NoUia`, files `<case>-b-…`) ran all of Part B's Excel cases again with no UI
Automation:

- the same keys and the real mouse;
- the places of the Formula Bar, the Name Box and the status bar taken from i1;
- pictures and the IME's state after every state;
- D10 read through COM after Escape.

What Excel shows below is pass b's, read from its pictures (the cell, the bar and the status bar's mode).
The first pass is kept in the `.jsonl`. Its pictures are kept for i1 only; the rest stay on this machine.

## Part A — VZ-14 at the current base (150% only)

`tests/ExGrid.Browser/scrollbar.spec.mjs` at `1655299`, headed, `--project=chrome --project=msedge`, from
a Windows copy of `tests/ExGrid.Browser`, as the run of 2026-09-23 did. WebAssembly at 21:40:59, the Server
host at 21:41:16 (`EXGRID_HOSTING=server`: the browsers reach the host through the latency proxy, which
Playwright started on Windows, at 0 ms).

| Host | Result | Log |
|---|---|---|
| WebAssembly | **8 passed**, 0 failed, 0 skipped, 0 flaky (4 per browser), 11.2 s | `scrollbar-150-wasm.log` |
| Server | **8 passed**, 0 failed, 0 skipped, 0 flaky (4 per browser), 6.9 s | `scrollbar-150-server.log` |

**VZ-14's test ("the Focus stays readable with the OS doing the scaling") passed on `chrome` and `msedge`,
on both hosts.** What it recorded, the same in all four:

- **DPR 1.5**; the platform `Windows`.
- **The gutter as the grid is told it: 12 × 12 CSS px** (`{"width":12,"height":12,"deviceContent":[1332,882]}`):
  border box minus content box, from a ResizeObserver, unrounded. The content box is 1332 × 882 device px.
- The Focus inside the readable area at the far corner and back.

**Beside the run of 2026-09-23:**

- That run recorded 15.34375 CSS px at 150% (23 device px ÷ 1.5), the native bar.
- Since 2026-09-29 the grid draws its own 12 px bar on every platform (ADR-0029). The gutter the grid is
  told is now **12 CSS px, a whole number** (18 device px at 1.5).
- The test asserts that the DPR is not an integer (it is 1.5) and that the bars occupy layout. It records
  the gutter and does not assert it.
- DoD row VZ-14 reads: "On a real Windows desktop at fractional display scaling (125%), where the native
  scrollbar is a non-integer number of CSS pixels, the Focus still lands inside the readable area".
  **At this base and at 150%, the bar is neither native nor a non-integer number of CSS pixels**, and
  125% was not run.
- The test passed. What VZ-14 needs at this base is the Definition of Done's to say, and nothing here
  decides it.

The other tests of the file, also passing on both browsers and hosts, recorded (at the emulated DPR 1, as
on 2026-09-23): the grid's gutter 12 × 12 with the platform's own scrollbars, 15 × 15 with scrollbars that
occupy layout (the forcing stylesheet's), and 15 × 15 at every browser zoom (DPR 1, 1.25, 2).

## Part B — a real IME, ExSheet beside Excel

ExSheet read the same in all twelve configurations (`/sheet` and `/sheet?chrome=mud`, Chrome and Edge,
WebAssembly, Server, Server behind 150 ms) in **131 of 133 states** (`summary.txt`). The two others are
i4's `into-bar` and `ime-on-in-bar`:

- with the Formula Bar's field just pressed and empty, its coloured layer was marked shown on WebAssembly
  and hidden on the Server host;
- nothing else differed.

The events the page saw, the editor's own scroll, and the colours read from the pictures are listed per
configuration under each state in `summary.txt`.

| # | ExSheet (all twelve) | Excel (pass b) | The reading | Matches |
|---|---|---|---|---|
| i1 | The IME stays off (above). `kana`, then Space, typed as text: `kana `. The first Enter commits `kana ` and moves to D11; the second to D12 | `ｋ`, `か`, `かｎ`, `かな` composed in the cell (mode Enter); Space converts (`かな`). The first Enter ends the composition, the edit open (`かな`, Enter). The second commits: D10 `かな`, D11, Ready | nothing commits or moves while composing; the first Enter ends the composition, the second commits and moves to D11 (ED-11) | **no**: nothing composes with these keys |
| i1x (addition) | F2, the IME on: `ｋ`, `か`, `かｎ`, `かな` composed in the Cell Editor (every keydown `Process`, `isComposing` from the second). Space: `かな`. The first Enter: `compositionend`, the edit open, D10. The second: D10 `かな`, the Focus D11 | the same, from F2 (mode Edit throughout) | (i1's) | **yes** |
| i1y (addition) | `k` opens the editor; `ana` compose; Space: `穴`; D10 `k穴`, D11 | — | — | — |
| i2 | Nothing composes: the first Escape cancels the edit (`kana`), D10 unchanged; the second Escape sends the keyboard to `body` | the first Escape ends only the composition: the edit open and empty (Enter). The second cancels the edit: Ready, D10 unchanged | asked: does the first Escape end only the composition? | **no** with these keys |
| i2x (addition) | F2, the IME on, `kana`: the first Escape ends only the composition (`compositionend` with `''`), the edit open and empty; the second cancels the edit, D10 unchanged | (as i2) | (i2's) | **yes**: the first Escape ends only the composition |
| i3 | Nothing composes: `kana `; the first ↓ commits it and moves to D11, the second to D12; the Enters to D13, D14 | Space converts (`かな`); ↓, ↓ keep `かな` under the conversion's underline, mode Enter, D10; the first Enter ends the composition, the second commits D10 `かな` and moves to D11 | the arrows choose among the candidates; the Focus does not move and nothing points | **no** with these keys |
| i3x (addition) | F2, the IME on, `kana`, Space (`かな`). Each ↓: two keydowns, `Process` then `ArrowDown`, both with `isComposing`; the composition stays `かな`; the Focus D10, nothing pointed, no outline. The first Enter ends the composition; the second commits D10 `かな`, D11 | (as i3) | (i3's) | **yes**. Which candidate the arrows reached is not readable: the candidate window is not (above), and the composition did not change |
| i4 | The bar pressed: the keyboard in the bar, empty; the IME on there. `ｋ`, `か`, `かｎ`, `かな` composed in the bar; Space `かな`; the first Enter ends the composition, the edit open in the bar; the second commits D10 `かな`, D11 | the same in the bar (mode Edit): D10 `かな`, D11 | as i1, in the bar; D10 holds what was chosen | **yes** |
| i5 | `=A1+`, A1 coloured (`#326ac7`). With `あ` composed: the field `=A1+あ`, `あ` once (the picture: underlined once), **the layer hidden and A1 drawn in the field's own colour** (no saturated pixel in A1's box: black, `#424242` under the Mud Chrome). Escape ends the composition (`=A1+`), A1 coloured again. `B1`, Enter: D10 `=A1+B1` (shows `#VALUE!`), D11 | `=A1+` with A1 blue; with `あ` composed, **A1 black** and `あ` once, underlined. Escape: `=A1+`. D10 `=A1+B1`, D11 | while `あ` is composed, A1 keeps its colour and the composition is shown once (ADR-0057). After Enter, D10 holds `=A1+B1` | **no** for A1's colour (Excel too); **yes** for the rest |
| i6 | `=`, the IME on, `あ`. ↓: the composition becomes **`あるみたいだから`**, the IME's prediction; no outline, nothing written. Escape: back to `あ`, **still composing**. VK_IME_OFF: the IME stays on and the composition stays (`Process` keydown). ↓: the prediction again; nothing points | ↓: **`穴`** (converted); Escape: `あ`, composing; VK_IME_OFF: still composing (the open status still on, the mode now read as alphanumeric); ↓: `穴` again. Nothing points | while composing, ↓ is the IME's: no outline, nothing written. After the composition ends, ↓ points at D11 | **yes** while composing; the second half **not reached**: the keys did not end the composition, in either |
| i7 | The press leaves `D10` in the Name Box with the caret at its end. The IME on; `kana` composed after it: `D10かな`. Enter ends the composition and goes nowhere. Escape: the keyboard back to the grid, the Name Box `D10` | the click selects `D10`; `かな` composed over it (in the IME's own window over the box); Enter ends it: the Name Box `かな`, nothing else. Escape: `D10` | the first Enter ends the composition and goes nowhere | **yes** |
| i8 | Ctrl+F opens Find, the keyboard in its field; the IME on there; `kana` composed; Enter ends the composition, Find's field `かな`, the outcome empty; Escape closes Find | (not asked) | the first Enter ends the composition and finds nothing yet | **yes** |
| i9 | `=SUM(A2:A4,B2:B4,C2:C4,`; `あ` composed; Escape ends it; the IME off; ↓ writes **`=SUM(A2:A4,B2:B4,C2:C4,D11`**, the caret at 26 of 26, the Name Box `D11`, one point. **The editor scrolled to its end**: `scrollLeft` 100 of `scrollWidth` 199, `clientWidth` 99 (Mud: 103.3 of 187, 83) | ↓ writes `=SUM(A2:A4,B2:B4,C2:C4,D11`, `D11` on grey, mode Point | after ↓, `…,D11` is written and the caret is inside the Cell Editor's visible width (ticket 75) | **yes** |
| i10 | Behind 150 ms, 150 ms between keys. **i10a** (i1's keys): 7 keydowns for 7 keys; `k`, `a` (and on two records `n`) reached the grid before the editor had the keyboard, and none was lost: D10 `kana `, the Focus D12, as at 0 ms. **i10ax** (i1x's): 7 keydowns, D10 `かな`, D11. **i10b** (i4's): 7 keydowns, D10 `かな`, D11 | — | the same reading as at 0 ms; no key lost or doubled | **yes** |

**Console**: in every record, info messages only (WebAssembly: `Debugging hotkey…`; Server: the circuit's
`Normalizing '_blazor'…` and `WebSocket connected…`). The Server host's own log: 951 entries, all `info`.

## Part C — ticket 74's readings, asked of Excel

Excel (first pass; the IME off and English (UK), so its UI Automation reads were not in the way: the list
rows are UI Automation's, and the pictures agree) beside ExSheet on `/sheet`, Chrome, WebAssembly. Every
state: Excel in mode Enter with the argument tip `XLOOKUP(lookup_value, lookup_array, return_array,
[if_not_found], [match_mode], [search_mode])`; ExSheet with its argument hint.

| # | Typed after `=XLOOKUP(1,A2:A4,B2:B4,,` | Excel's list | ExSheet's list | ExSheet as decided (ADR-0058) | Excel and ExSheet |
|---|---|---|---|---|---|
| c1 | `1.0` | **none** | `1 - Exact match or next larger item` alone | the same | **differ** |
| c2 | `+1` | `1 - Exact match or next larger item` alone | the same | the same | same |
| c3 | `1 ` | **none** | `1 - …` alone | the same | **differ** |
| c4 | `-0` | `0 - Exact match` alone | the same | the same | same |
| c5 | `--1` | **`1 - Exact match or next larger item` alone** | every value, `0` selected | every value, the first selected | **differ** |
| c6 | `50%` | every value, `0` selected | every value, `0` selected | the same | same |
| c7 | `(` | **every value, `0` selected** | nothing (the list's element holds the argument hint alone, no option) | nothing | **differ** |
| c8 | `(A` | **every value, `0` selected** | `AVERAGE` | the functions beginning with A | **differ** |
| c9 | `Positions[` | **`@ - This Row`** (selected), `Id`, `PV`, `#All`, `#Data`, `#Headers`, `#Totals` | `Id` (selected), `Book`, `PV` | the table's columns | **differ** (both list the columns; Excel adds the specifiers and selects `@ - This Row`) |
| c10 | `Positions[Id]` | every value, `0` selected | every value, `0` selected | the same | same |
| c11 | `(1)` | **none** | every value, `0` selected | every value, the first selected | **differ** |

Where Excel listed one value (c2, c4, c5) it showed that value's tip beside it (c5: "Searches for an Exact
match, if not found searches for the next larger item"). ExSheet's Table `Positions` on `/sheet` has three
columns (`Id`, `Book`, `PV`); Excel's has two (`Id`, `PV`), as the procedure sets it up.


## Every difference, and what it belongs to

Nothing here is decided. Each item is what was read beside what the procedure, an ADR, a DoD row or a
ticket says.

1. **VZ-14 was not run at 125%.**
   - DoD row VZ-14 names "a real Windows desktop at fractional display scaling (125%)". Part A ran at
     150% only (above).
   - At 150% it passed on both browsers and both hosts. **At this base, the gutter the grid is told is
     12 CSS px, the grid's own bar.** That is a whole number.
   - The same row's premise, "where the native scrollbar is a non-integer number of CSS pixels", did not
     hold at 150%. The test asserts the fractional DPR and the bars that occupy layout. It records the
     gutter and does not assert it.
2. **i1, i2, i3 with the procedure's keys: in ExSheet the IME cannot be switched on with D10 selected,
   so nothing composes.** The keys type Latin text; Excel composes from the first key.
   - The readings of i1 and i3 are not reached with these keys. i2's question gets the answer "the
     first Escape cancels the edit".
   - With the edit opened by F2 first (i1x, i2x, i3x), every reading matched.
   - It belongs to DoD row ED-11 and ADR-0010 ("A composing IME is left alone too"), the reading in the
     procedure's table.
   - ED-11's own criterion ("`isComposing` and keyCode 229 both pass through") was not contradicted:
     every composing keydown reached the field, and the core took none.
3. **i5: while `あ` is composed after `=A1+`, A1 is not drawn in its colour.**
   - The coloured layer is hidden, and the field's own uncoloured text shows. A1 reads black (`#424242`
     under the Mud Chrome).
   - The procedure's reading is "A1 keeps its colour (ADR-0057)". ADR-0057's bullet "The field's own
     text becomes transparent only while the layer's text equals the field's value" says the layer is
     hidden while the field holds an IME composition.
   - Excel too drew A1 black while composing. The composition was shown once in both, and D10 held
     `=A1+B1` after Enter.
4. **i6: the procedure's keys did not end the composition, in ExSheet or in Excel.**
   - Escape after ↓ went back from the IME's candidate to `あ` and kept composing. VK_IME_OFF during
     the composition left the IME on and the composition in place.
   - So "after the composition ends, ↓ points at D11" was never reached; the second ↓ was the IME's
     again.
   - The first half matched: while composing, ↓ drew no outline and wrote nothing.
   - It belongs to the procedure's reading for i6 (ED-11, ADR-0010).
5. **Part C, ticket 74's readings, against Excel.** They belong to ADR-0058, "What the thirteenth
   Windows run settled", the sub-bullet "What counts as a number, and where the rule stops", and to
   ticket 74.
   - `1.0` (c1) and `1 ` (c3): Excel lists nothing. ExSheet lists `1 - …` alone, as decided.
   - `--1` (c5): Excel lists `1 - Exact match or next larger item` alone. ExSheet lists every value, as
     decided.
   - `(` (c7): Excel lists every value, `0` selected. ExSheet lists nothing, as decided.
   - `(A` (c8): Excel lists every value, `0` selected. ExSheet lists `AVERAGE`, as decided.
   - `Positions[` (c9): Excel lists `@ - This Row` (selected), the columns, then `#All`, `#Data`,
     `#Headers` and `#Totals`. ExSheet lists the columns, `Id` selected.
   - `(1)` (c11): Excel lists nothing. ExSheet lists every value, as decided.
   - c2 (`+1`), c4 (`-0`), c6 (`50%`) and c10 (`Positions[Id]`) read the same in both.

Every other state of Part B read as its reading: i1x, i2x, i3x, i4, i7, i8, i9 (ticket 75: the caret at
the editor's end, inside its width), i10. ExSheet read the same in all twelve configurations, but for the
coloured layer of i4's empty bar (shown on WebAssembly, hidden on the Server host).

## Seen, and not asked

- **A second Escape with no edit open sent the keyboard from the grid to `body`** (i2, all twelve). D10
  stayed selected, and nothing else changed.
- **The Name Box press (i7) behaves differently in the two.**
  - ExSheet leaves the caret after `D10`, so the composition was appended (`D10かな`).
  - Excel's click selected `D10`, so the composition replaced it (`かな`).
- **Excel draws the composition in the cell** (underlined, a thick bar under the converted clause). In the
  Name Box it draws it in the IME's own window (`MSCTFIME Composition`), which PrintWindow renders empty.
- **The same keys met the IME differently in the two.** After `=`, `あ`, ↓:
  - the browsers' composition became the IME's prediction `あるみたいだから`;
  - Excel's became the conversion `穴`.
- **The IME learnt during the run.** In the trial before the recorded runs, Space after `kana` gave `かな`
  and a second Space `仮名`. In the recorded runs, Space gave `かな` every time. The IME keeps its input
  history (`PredictionUseInputHistory` 1); this run's conversions (`かな`, `穴`) are in it now.
- **↓ during a conversion** (i3x): the page saw two keydowns per ↓, `Process` and then `ArrowDown`, both
  composing. The composition string did not change, and neither did Excel's.
- **Behind 150 ms at a typist's pace** (i10a), the second and sometimes third key reached the grid before
  the Cell Editor had the keyboard. They were not lost: D10 held `kana `.

## The machine afterwards

- **Processes.**
  - No Excel is running. Three of the run's Excels did not quit within 10 s and were ended by
    Stop-Process, each the script's own: the last of pass b and two of Part C's.
  - No Playwright browser and no input helper is running.
  - The latency proxy was stopped at 22:38, and both DemoHosts in WSL were stopped.
  - The user's Chrome and the Stream Deck's `node` were left alone.
- **Excel's AutoRecover workbooks were not touched.** `Book1 (version 1).xlsb`, the unsaved workbook and
  `Excel15.xlb` hash as the backup taken before the first Excel started
  (`%LOCALAPPDATA%\exgrid-layer3\autorecover-backup-run15`). The folder holds the same three files.
- **The keyboard.**
  - English (UK), 0x08090809, on every window, as found. Each probe and each run of the Excel script put
    it back at its end.
  - With one input method for every window, the trial before the recorded runs had left the terminal
    on the Japanese keyboard (IME off) for a few minutes. It was put back then.
  - The IME is off. Its settings were not changed; its input history was (above).
- **The regional format**: en-GB, unchanged. **The display**: 150%, unchanged.
- **Pictures kept on this machine, not committed** (the records hold every state):
  - the page's pictures of the Server and Edge configurations, `%LOCALAPPDATA%\exgrid-layer3\run-2026-10-01-15\shots`;
  - Excel's first-pass pictures but i1's, `…\ask-excel-15\first-pass-shots`.
