# Windows, sixteenth run: a real Japanese IME on the Keyboard Field (ADR-0080)

**Verified commit: `bf13de1`** (`claude/exsheet-keyboard-field`; its tree is the base's at `b87cac6`, PR #44
squash-merged), on branch `claude/exsheet-windows-verify-16` from it. The procedure is
`docs/specs/exsheet/verify-on-windows-16.md` as it stands there, Parts A to D. Run on 2026-10-02 from 01:59
to 03:34 local time (BST) by the Claude Code session on the Windows desktop of the earlier runs, through WSL. The last
input was d5's. Narrator, which this run could not stop the second time it ran, was turned off by the user (Part D). **Nothing was decided.** No ADR, `CONTEXT.md` or `docs/definition-of-done.md` was changed.

**In short.** With the Microsoft Japanese IME driven by real keys, **a selected cell now composes from the first
key** (k1, k2: the IME's open status reads on, `ｋ`, `か`, `かｎ`, `かな` composed in the Keyboard Field drawn over
D10, the first Enter opens the Cell Editor holding `かな` with the keyboard, the second commits D10 and moves to D11),
in all 12 configurations, as Excel does. Every case of Part A read as ADR-0080 expects, and as Excel, except:

- **k6 on WebAssembly**: the second clause lost its first romaji key when the keyboard moved to the Cell Editor
  between the IME's two compositions (D10 `かな暗示` or `かなん時`; Excel and the Server host `かな感じ`);
- **k8 and k16**: the press never reached the page, because the IME's own windows (its prediction list under the
  composition, and the composition's text) took it, in Excel as well;
- in Part C, **Tab again and Shift+Tab again stay inside the grid** (A11Y-4's reading says they leave).

Part B read as the fifteenth run's record but for what the build changed (i7, i10a) and one IME choice (i6). Part D's
d1 to d4 read as expected. d5 ran with Narrator (NVDA is not installed), which the user had to turn off at the end.
The list of differences is at the end.

## Files

| | |
|---|---|
| `report.md` | this report |
| `ime-probe.mjs` | Parts A to D in the browsers: the probe (the fifteenth run's, extended as the procedure's Setup asks) |
| `input-server.ps1` | the probe's real OS input (SendInput), the IME's keys and state, Alt+Tab, the clipboard, Narrator |
| `d5-narrator.mjs` | Part D's d5: Narrator on `/sheet` |
| `records/` | one JSON per page, configuration and browser: every state read (`partc-…` for Part C, `d5-narrator-…` for d5 and its first try) |
| `shots/` | the page's picture of the grids in every state (`<page>-<case>-<state>-<config>-<browser>.png`), for WebAssembly and Chrome: `/sheet` (Parts A, B, D and C), `/features` (k11, Part C) and `/sheets` (k14). The other configurations' pictures stay on this machine (below) |
| `summarise.py`, `summary.txt` | the records grouped by state across the configurations |
| `excel/ask-excel.ps1` | Excel, the same keys |
| `excel/ask-excel.jsonl` | one line per Excel case: per state what was read |
| `excel/shots/` | Excel's pictures per state, cut from the whole window: the Formula Bar, the Name Box, the mode, the cells, D10 and the bar enlarged ×4, and the IME's composition window where it showed one (`d1-…-popup-1`) |

## Environment

- **Windows 11** (10.0.26200, 25H2, UBR 9457), one display of 3840 × 2160 device px at **150%** (DPI 144).
  Windows' mode **light** (apps and system). High contrast off. **Regional format en-GB**, unchanged.
- The language list: **en-GB** (keyboard English (UK), 0x0809), en-US, and **ja** with the **Microsoft IME**
  (`imjptip.dll` 10.0.26100.9278). The Japanese keyboard is the US layout here (kbd101.dll). One input method
  for every window, so each run put English (UK) back at its end.
- **The IME's mode: Hiragana, romaji input**, read through `WM_IME_CONTROL` as the fifteenth run read it
  (conversion mode 0x19 with the IME off, 0x9 once switched on). Its settings, read and not changed:
  prediction on with the input history (`PredictionUseInputHistory` 1, `PredictionMinimumCompositionStringLength`
  1), cloud suggestions off.
- **Chrome 153.0.8010.54** and **Edge 154.0.4258.48** (as the browsers report them), the Windows builds, headed, at the display's own scale
  (`viewport: null`, DPR 1.5).
- **Excel**: Microsoft 365, version 2609 (16.0.20430.20092, x64), English (UK) UI, the Office Theme "Use system
  setting".
- The runner: portable Node v24.14.1 on Windows, Playwright 1.62.1, from a copy of `tests/ExGrid.Browser` at
  `bf13de1`.
- The DemoHosts, built at `bf13de1`, ran in WSL2 (.NET SDK 10.0.203 via nix): WebAssembly on `localhost:5299`, the
  Server host on `localhost:6298`. Behind 150 ms, `latency-proxy.mjs` on Windows (5298 → 6298, control on 7298),
  its round trip set to 150 ms for each such run and back to 0 after it.
- Screen readers: **NVDA is not installed. Narrator** (Windows' own, `Narrator.exe` 10.0.26100.8972) is. Nothing
  was installed.

## Method

The fifteenth run's method (its report's "Method"), with these differences:

1. **Keys** go through SendInput as a virtual-key code and a scan code per key, never as a Unicode
   character. The IME is switched on and off by **VK_IME_ON (0x16)** and **VK_IME_OFF (0x1A)**. The window
   takes the Japanese keyboard by `WM_INPUTLANGCHANGEREQUEST` (0x04110411) for Parts A, B and D; Part C keeps
   English (UK).
2. **The probe** (`ime-probe.mjs`) reads, in every state, what the fifteenth run's read, and:
   - **the Keyboard Field**: whether it holds DOM focus, its value, whether it wears the build's composing
     class (`ex-key-field-composing`), its computed opacity, caret colour and outline, read-only, its tab
     stop, its `aria-activedescendant`, and its box beside the Focus cell's box;
   - **the root**: its tab stop, its `aria-activedescendant`, the focus ring's mark the build sets
     (`data-ex-focus-visible`), whether it matches `:focus-visible`, and its outline;
   - **every grid on the page** in brief (which holds the keyboard, its edit, its field, its Focus);
   - the page's presses (`mousedown`, `mouseup`, `click`), `focusout` with where focus went, `keyup` while the
     IME has the key, and `copy`/`paste` with their text, besides the fifteenth run's events;
   - in Part C, **the focused element as Chromium's accessibility tree has it** (over the DevTools protocol):
     its role, name, and what its `aria-activedescendant` resolves to;
   - after every state, the IME's open status and mode, and the window in front.
3. **Between cases**, as in the fifteenth run: Escape until no edit, list, Find, composition or Name Box
   keeps the keyboard; then, if the IME is still on, VK_IME_OFF (it now works with the keyboard on the
   field), and only if that leaves it on, F2, VK_IME_OFF in the edit, Escape. D10:F13 cleared with one Delete
   when any of those cells holds something, then D10 pressed.
4. **The configurations**: `/sheet` and `/sheet?chrome=mud` with Parts A, B and D, and `/features`,
   `/features?chrome=mud` with k11, `/sheets`, `/sheets?chrome=mud` with k14, each under Chrome and Edge, on
   WebAssembly, on the Server host and on the Server host behind 150 ms: 36 records, 02:21 to 03:29. Part C:
   `/sheet`, `/sheet?chrome=mud` and `/features`, Chrome, WebAssembly and the Server host: 6 records.
   k7, i10a, i10ax, i10b and d2 ran behind 150 ms only, as the procedure says.
5. **Excel** (`excel/ask-excel.ps1`): the fifteenth run's script as its pass b ran — no UI Automation at all
   between keys, the places of the Formula Bar, the Name Box and the status bar taken from the fifteenth run's
   i1 (case 0 of this run found them unchanged), what Excel shows read from its pictures. An Excel of the
   script's own per case (`New-Object`, never an Excel already running), Book1 maximised at 100%, D10 selected.
   02:14 to 02:21. k8x and k10 failed in that pass, because the script had not read the places of D8 and D1 (the
   `.jsonl` keeps the two failures); they ran again at 02:20 with those places added.
6. **Additions**, beside the procedure's cases (marked as additions in the records):
   - **k6x, k6y**: k6 with `kanji` typed in one state, 150 ms and 30 ms between its keys (k6 itself reads
     after every key, 600 ms or more apart). A trial before the recorded runs lost keys at 30 ms.
   - **k8x**: k8 with the press on D8, above D10 (below).
   - **k13x**: k13's copy and paste with a value to copy (`150` in D10).
   - **d4b, d4c**: d4 with the way back into the Sheet from the positions grid after it: Shift+Tab straight
     away (d4b), and after an Escape in the positions grid (d4c).
   - Excel was asked k6x, k6y and k8x too.
7. **Part C's "Tab into the grid from the element before it"** gives that element focus by script first, and
   is recorded as such: a press on it would follow a link (`/features`) or press a button. The element after
   the grid is on both pages another grid (the positions grid, the second grid), which takes the first
   Shift+Tab as its own key, so Part C presses Escape there first (both states are recorded).
8. **k15's Alt+Tab** goes to a window of the input helper's own (`exgrid-alttab-16`), opened before the
   browser or Excel came to the front, so that Alt+Tab never brought a window of the user's forward.

## Trials before the recorded runs

Six trials on `/sheet`, `/features` and `/sheets` (WebAssembly, Chrome) checked the probe before the recorded runs.
Their records are not committed; what they found changed the probe as follows.

- **k8.** A press on D12 during a composition never reached the page: no `mousedown`. The composition became the
  IME's prediction, `悲しんで`, and ended. k8x was added (below).
- **k16.** A press on the composition's own text reached nothing either. Mouse events were added to the probe's log to
  tell the two apart.
- **k6.** At 30 ms between keys, the second clause lost `ka`: D10 `かなン時`. k6 itself was rewritten to read after
  every key, and k6x and k6y were added.
- **d4b.** A press on the positions grid, then Shift+Tab: the positions grid took the key as its own. d4c was added.
- **Part C.** Ctrl+Down left D10 unpainted, so the press on it failed. Ctrl+Home was added before that press.

## The IME's own windows take presses

As the fifteenth run found, the IME's candidate and prediction windows belong to the Windows Input Experience, and no
picture this machine gives holds them. **This run found that they also take presses:**

- In k8, the press at D12's middle (two rows below D10) landed on the prediction list the IME shows under the
  composition. The page saw no `mousedown`. The composition became the prediction under the pointer, `悲しんで`, and
  ended: `compositionupdate` and `compositionend` with that text.
  - This happened in every configuration.
  - In Excel the click at D12 did not reach the sheet either. The composition was committed into the edit, still
    `かな`; Excel stayed in Enter mode on D10.
- In k16, the press on the composition's own text reached neither the page nor the grid: no event at all, and the
  composition went on. Excel did the same: still composing, the underline kept, Enter mode.
- k8x pressed D8 instead, above D10, clear of the IME's windows. That press reached the page and read as ADR-0080
  expects.

`悲しんで` is the IME's prediction from `かな` (`かなしんで`). The IME's prediction uses its input history
(`PredictionUseInputHistory` 1). Every Japanese string the records hold was scanned: each is the keys typed
(`kana`, `kanji`, `desu`, `a`), their conversions (`かな`, `感じ`, `暗示`, `ン時`, `穴`, `です`), or that prediction.

## Part A: the IME on a selected cell

D10 selected by a press, no edit open. **ExSheet** read the same in all 12 configurations of `/sheet` and
`/sheet?chrome=mud` (WebAssembly, Server, Server behind 150 ms; Chrome and Edge), unless a row says otherwise. Excel
ran in its own pass (`excel/ask-excel.jsonl`), and what it showed is read from its pictures.

How the composition was drawn, in every state that composed:

- The Keyboard Field wore `ex-key-field-composing`, at opacity 1, with a 2 px outline: Highlight, `#0078d7`, under the
  built-in Chrome, and `#594ae2` under the MudBlazor Chrome. The caret was visible, and the IME's own dotted underline
  ran under the text.
- The field's box was the Focus cell's box: 99 × 28 CSS px over D10.
- While it composed, the Formula Bar stayed empty and the Name Box read `D10`.
- When no composition was under way, the field was empty at opacity 0.

| # | ExSheet | Excel | Expected of the build | Matches |
|---|---|---|---|---|
| k1 | The IME's open status **on** after VK_IME_ON, mode Hiragana (0x9). The keyboard in the Keyboard Field. | On (0x9) | On | **yes** |
| k2 | `ｋ`, `か`, `かｎ`, `かな` in the field, drawn over D10. The first keydown `Process`; then `Process` with `isComposing`. No edit open; the Focus D10. Space: `かな`. The first Enter: `compositionend` `かな`; the Cell Editor open holding `かな`, with the keyboard; the field empty, opacity 0. The second Enter: D10 `かな`, the Focus D11, the keyboard back in the field | `ｋ` … `かな` composed in the cell (Enter mode). The first Enter ends the composition, the edit still open. The second commits: D10 `かな`, D11 | as described | **yes** |
| k3 | The first Escape: `compositionend` with `''`; the Cell Editor open and empty, with the keyboard. The second cancels it: D10 empty, the keyboard in the field | The first Escape: the edit open and empty (Enter). The second: Ready, D10 empty | as described | **yes** |
| k4 | Each ↓: `Process` then `ArrowDown`, both with `isComposing`. The field still `かな` composing; no Focus move, no outline, nothing written. Then as k2: D10 `かな`, D11 | ↓, ↓ keep `かな` (Enter mode, D10); then D10 `かな`, D11 | as described | **yes** |
| k5 | F2, the IME on, Escape: the keyboard in the field, the IME still on. The press on D10, then `kana` composes from the first key; D10 `かな`, D11 | — | D10 `かな`, not `k穴` | **yes** |
| k6 | Every key read alone. **Space after `kana`: `かな`. Then `k` ended that composition** (`compositionend` `かな`), and the IME started a second one: two compositions, not one, everywhere. **Server host, both delays (8 records):** the Cell Editor opened holding `かな`, and the keyboard stayed in the field. The second composition went on there (the field showing `かなｋ` … `かなかんじ`, then Space `かな感じ`). The first Enter ended it and typed it into the edit: `かな感じ`. The second Enter: D10 `かな感じ`, D11. **WebAssembly (4 records):** 22–24 ms after the first composition ended (44–45 ms under the MudBlazor Chrome), DOM focus moved from the field to the Cell Editor. The IME restarted the composition there with `ｋ`, but the next `a` composed `あ`, not `か`. D10 `かなあんじ`, then Space `かな暗示`, D11 | `k` committed `かな` and started `ｋ` … `かんじ` in the same cell; Space `感じ`. D10 `かな感じ`, D11 | One composition of two clauses, converted together; D10 holds both | **no**. Two compositions in both, and on WebAssembly a key of the second was lost (below) |
| k6x (addition) | `kanji` at 150 ms between keys. Server: as k6, D10 `かな感じ`. WebAssembly: D10 `かな暗示` (from `かなあんじ`) | D10 `かな感じ` | — | Server and Excel the same; WebAssembly loses a key |
| k6y (addition) | `kanji` at 30 ms. Server: D10 `かな感じ`. WebAssembly `/sheet`: `かな暗示`. WebAssembly `/sheet?chrome=mud`: **`かなん時`** (`ka` both lost) | D10 `かな感じ` | — | the same, as k6x |
| k7 | Server behind 150 ms only (4 records). After the first Enter, the editor was being opened. `desu` composed in the field and was appended: D10 **`かなです`**, D11. Read 2 s later: the same | Typed with no pause: D10 `かなです`, D11 | D10 `かなです`, D11; nothing lost, doubled or reordered | **yes** |
| k8 | **The press never reached the page** (above): the composition became `悲しんで` and ended. The Cell Editor open holding `悲しんで`, the keyboard in it, the Focus D10. Escape cancelled it: D10 empty | The click did not reach the sheet: `かな` committed into the edit, Enter mode on D10 | D10 `かな`, the Focus D12, no edit open | **no** (the IME took the press, in Excel too) |
| k8x (addition) | The press on D8: `compositionend` `かな` before the `mousedown`. D10 `かな`, the Focus D8, no edit open, the keyboard in the field | D10 `かな`, D8, Ready | as k8's | **yes** |
| k9 | The press on the Formula Bar's text: `compositionend` `かな`, then the `mousedown` on the bar. The edit open holding `かな` in the cell and the bar; the keyboard in the bar, the Focus D10 | The composition committed; Edit mode, the keyboard in the bar holding `かな`, D10 | record | the same in both |
| k10 | The press on column D's header: `compositionend` `かな` first. **Column D selected** (the Name Box `D1`, D1 to D13 shaded); D10 `かな`; nothing sorted; no edit; the keyboard in the field | Column D selected (`D:D`), D10 `かな`, Ready | `かな` goes to D10 | **yes** |
| k11 | `/features` and `/features?chrome=mud`, 12 records. **Book (A1, not Editable): the field read-only;** VK_IME_ON left the IME **off**. `kana` arrived as plain keydowns: nothing typed, nothing opened. **Trader (B1): the field not read-only**; the IME on; composed as k2. B1 `かな` after the second Enter, the Focus B2 | — | Book: off, nothing typed or opened. Trader: as k2 | **yes** |
| k12 | Space: keydown **`Process`**. The IME inserted a full-width space through a composition (`compositionstart`, `compositionupdate`/`compositionend` with `　`). The Cell Editor opened holding `　`, with the keyboard | Space opened an edit (Enter mode) holding a character that does not show | record | the same in both (an edit holding the IME's space) |
| k13 | With the IME on and nothing composed, every key reached the grid as itself (`ArrowDown`, `ArrowRight`, `Tab`, `Enter`, `Delete`, Ctrl+`c`, Ctrl+`v`). Focus D10 → D11 → E11 → F11 → F12; Delete on the empty F12. Ctrl+C put `\r\n` on the clipboard (F12 is empty); the press on D12, then Ctrl+V pasted it. Nothing was typed into the field | — | each the grid's; nothing typed into the field | **yes** |
| k13x (addition) | `150`, Enter with the IME off (D10 `150`); ↑. With the IME on: Ctrl+C put `150\r\n` on the clipboard; the press on D12; Ctrl+V: D12 `150`; Delete cleared D12 | — | — | — |
| k14 | `/sheets` and `/sheets?chrome=mud`, 12 records. Left B2: `kana` composed in the left Sheet's field; Enter opened the left Cell Editor holding `かな`. The press on the right B2: the keyboard in the right Sheet's field; **the left edit still open, holding `かな`**. `kana` then composed in the right field, and nothing reached the left | — | as described | **yes** |
| k15 | Alt+Tab away (to the helper's window): **the composition ended there** (`compositionend` `かな`), and the Cell Editor opened holding `かな`. Back: the edit still open holding `かな`, the keyboard in it. Enter: D10 `かな`, D11. Enter: D12 | Alt+Tab away: the composition committed into the edit (`かな`, Enter mode). Back: the same. Enter: D10 `かな`, D11; Enter: D12 | record whether the composition survives | the same in both: it does not survive, its text stays in the open edit |
| k16 | **The press never reached the page** (above). The field still `かな`, composing; no edit open | The same: still composing, Enter mode | record (the procedure: the press ends the composition) | the same in both; the press did not end it |

## Part B: the fifteenth run's cases again

Every state read the same in all 12 configurations but i4's empty bar (below). Against the fifteenth run's record:

| # | This run | The fifteenth run | Reads differently? |
|---|---|---|---|
| i1x | F2, the IME on; `ｋ` … `かな` in the Cell Editor; Space `かな`; Enter ends the composition; Enter: D10 `かな`, D11 | the same | no |
| i2x | The first Escape ends only the composition (`''`), the edit open and empty; the second cancels, D10 unchanged | the same | no |
| i3x | Each ↓ `Process` then `ArrowDown`, composing; nothing moves; D10 `かな`, D11 | the same | no |
| i4 | The IME on (it now turns on, in the field); the press on D10; the press into the bar: the keyboard in the bar, empty; `ｋ` … `かな` in the bar; D10 `かな`, D11. The bar's coloured layer marked shown on WebAssembly and hidden on the Server host, in the two states with the bar pressed and empty | the same, the layer too | no |
| i5 | With `あ` composed after `=A1+`: the layer hidden, A1 drawn in the field's own colour (no saturated pixel in A1's box); `あ` once. Escape: A1 `#326ac7` again. D10 `=A1+B1` (`#VALUE!`), D11 | the same | no |
| i6 | `=`, `あ`; **↓: `穴`** (the IME's conversion); Escape: `あ`, still composing; VK_IME_OFF: the IME stays on; ↓: `穴` again; nothing points | ↓ gave the prediction `あるみたいだから`; otherwise the same | **the IME's choice**: `穴`, as Excel gave in the fifteenth run |
| i7 | **The press selected `D10`** (selection 0 to 3, ticket 78). With the IME on, `kana` replaced it: the Name Box `ｋ` … `かな` (`compositionstart` with `D10`). Enter ends the composition and goes nowhere. Escape: the keyboard back in the field, the Name Box `D10` | The press left the caret after `D10`; `D10かな` | **yes**: `かな`, not `D10かな`. Excel, the fifteenth run's and this run's d1: `かな` |
| i8 | Ctrl+F; `kana` composed in Find's field; Enter ends it: `かな`, the outcome empty; Escape closes Find | the same | no |
| i9 | ↓ writes `=SUM(A2:A4,B2:B4,C2:C4,D11`, the caret at 26 of 26, `scrollLeft` 100 of `scrollWidth` 199 (`clientWidth` 99), the Name Box `D11`, one point | the same | no |
| i10a | Behind 150 ms, at 150 ms between keys: **`kana` composed in the field**; D10 `かな`, D11 | `k` opened the editor as a plain key; D10 `kana `, D12 | **yes**: the build composes on a selected cell |
| i10ax | D10 `かな`, D11 | the same | no |
| i10b | D10 `かな`, D11 | the same | no |

**Console:** in every record, info messages only. WebAssembly logged `Debugging hotkey…`. The Server host logged the
circuit's `Normalizing '_blazor'…` and `WebSocket connected…`.

## Part C: the keyboard and the clipboard with the IME off

**Configurations.** English (UK), the IME off, Chrome. Three pages: `/sheet`, `/sheet?chrome=mud` and `/features`.
Each ran on WebAssembly and on the Server host, 6 records.

**How the readings agree.** `/sheet` and `/sheet?chrome=mud` read the same on both hosts, and so did `/features`. The
accessibility tree was read after every state over the DevTools protocol. Below, "ring" is the root's mark
`data-ex-focus-visible`.

| Step | `/sheet` (both Chromes) | `/features` | Expected |
|---|---|---|---|
| The press on D10 (C1 on `/features`) | the keyboard in the Keyboard Field, no ring | the same | the field; no ring after a click (KB-12) |
| `150`, Enter, `200`, Enter, every key event in one SendInput call | D10 `150`, D11 `200`, the Focus D12; the keyboard in the field; ring | C1 `150`, C2 `200`, the Focus C3 | as at the base |
| ↑ ↓ → ← | D11, D12, E12, D12 | C2, C3, D3 (Narrow, the field read-only over it), C3 | as at the base |
| Ctrl+↓, Ctrl+↑, Ctrl+→, Ctrl+← | D1048576, D11, XFD11, D11 | C400, C1, F1, A1 | as at the base |
| Home, End, PageDown | A11, XFD11, XFD23 | A1, F1, F12 | as at the base |
| Ctrl+Home (added, to bring D10 back into view) | A1 | A1 | — |
| F2, Escape | the Cell Editor open holding `150`, the keyboard in it; Escape: back in the field | the same | as at the base |
| Ctrl+C on D10, a press on D13, Ctrl+V | the clipboard `150\r\n` (`copy` on the field); D13 `150` (`paste` on the field) | C1 → C4 `150` | copy and paste land |
| Focus given by script to the element before the grid, then Tab | the Revalue button, then **Tab: the keyboard in the field, ring** | the navigation's link, then the field, ring | the field; ring (KB-12) |
| Tab again | **the Focus moved to E13**, the keyboard still in the field | the Focus C4 → D4, in the field | A11Y-4's layer 3: leaves the grid |
| Focus given by script to the element after the grid, Shift+Tab | the positions grid's root (display-only, tab stop `0`, matching `:focus-visible`). **That grid took Shift+Tab as its own**: the keyboard stayed on it | the second grid's root: the same | Shift+Tab lands in the grid |
| Escape there, then Shift+Tab | the keyboard in the Sheet's field, ring | the same | — |
| Shift+Tab again | **the Focus moved to D13**, still in the field | the Focus D4 → C4 | Shift+Tab again leaves it (A11Y-4) |
| A press on the display-only grid | the keyboard on that grid's root; no `:focus-visible` | the same | the root on a display-only grid |

**Accessibility (A11Y-21).**

- With the keyboard in the field, the focused node was the field: `textbox`, focused. Its `aria-activedescendant`
  resolved to the Focus cell's `gridcell`, with the cell's text as its name (`150`, or `''` for an empty cell).
- The root carried no `aria-activedescendant` meanwhile.
- In the Cell Editor (F2), the focused node was the editor's `textbox`, with no active descendant.
- On the display-only grid, the focused node was the root (`grid`), resolving to its Focus cell.

## Part D: the cases the fifteenth run left

`/sheet` and `/sheet?chrome=mud`, Chrome and Edge, WebAssembly, the Server host, and behind 150 ms. d1 to d4 read the
same in all 12 configurations. The one exception is the IME's mode read while it was off: `alphanumeric` behind 150 ms,
left by d2's reset, and `hiragana` elsewhere. Excel was asked d1 and d3.

| # | ExSheet | Excel | Expected | Matches |
|---|---|---|---|---|
| d1 | The press on the Name Box selected `D10` (0 to 3). With the IME on, `kana` replaced the selection: `ｋ` … `かな` (`compositionstart` with the selected `D10`). Enter ended the composition: the Name Box `かな`, the keyboard still there, nothing done. Escape: the keyboard in the field, the Name Box `D10` | The click selected `D10`; the composition stood in the IME's own window over the box; Enter: the Name Box `かな`; Escape: `D10` | the Name Box `かな` while composing; Enter goes nowhere | **yes** |
| d2 | Behind 150 ms, 4 records. The press on F8, at once the press on the Name Box, VK_IME_ON and `k`, in one step: `compositionstart` with `F8`, the Name Box `ｋ` … `かな`, the Focus F8. 2 s later: `かな` | — | `かな`, not `F8かな` | **yes** |
| d3 | The press between `1` and `0`, dragged to between `D` and `1`, released: **the whole `D10` selected** (0 to 3), the keyboard in the Name Box. Escape: back in the field | **The whole `D10` selected** after the same gesture | the whole text selected at the release | **yes** |
| d4 | Escape, Escape: the keyboard still in the field. The press on the Revalue button: the keyboard on the button. Tab: back in the field, D10. **Tab: the Focus E10**, the keyboard in the field | — | the last Tab stays in the grid (KB-8) | **yes** |
| d4b (addition) | Escape, Escape, a press on the positions grid. Shift+Tab and Tab were that grid's own: the keyboard stayed on its root | — | — | — |
| d4c (addition) | As d4b with Escape in the positions grid first. Shift+Tab: back in the Sheet's field, D10. **Tab: the Focus E10**, in the field | — | (KB-8, as d4) | **yes** |
| d5 | **NVDA is not installed; Narrator is** (Windows' own); nothing was installed. Narrator, on `/sheet`, Chrome, WebAssembly, with the Revalue button focused by script before it started. What it said is read from its own "copy the last phrase" command (the Narrator key with Ctrl+X) after each step. **Tab into the grid: "Enter Table, 1048576 by 16384, edit, Scan Off"**: no cell named. The Sheet had no Focus yet on the fresh page, so the field carried no `aria-activedescendant`. **↓: "Scan"**, and DOM focus went to `body`. **→: "m"**, focus still on `body`. Narrator's speech recap window opened (the Narrator key with Alt+X), but UI Automation read nothing in it beyond its title | — | record what it says | recorded (below) |

**d5, how it went:**

1. **The first try stopped at its start** (`records/d5-narrator-wasm-chrome-try1.json`). Narrator's first start on this
   machine put a window of its own, "Narrator updates", in front, and the probe could not bring Chrome back. Narrator
   then refused `Stop-Process` ("Access is denied"; it runs with UI Access) and was stopped with its own keys, Insert
   and Escape.
2. **The second try ran its four steps**, then neither Insert+Escape nor Win+Ctrl+Enter, sent through SendInput,
   stopped Narrator. **The user turned Narrator off.**
3. **Narrator's first start wrote its defaults** to `HKCU\Software\Microsoft\Narrator` (36 values and the key
   `NarratorHome`) and created `HKCU\Software\Microsoft\Windows NT\CurrentVersion\Accessibility\ATConfig`. Both
   were put back to the export taken before d5: the exports compare equal.
4. **The copy command itself may have played a part.** The Narrator key with Ctrl+X was sent after every step, and
   Narrator turned scan mode on between "Scan Off" and the next step. This run cannot tell whether that command or
   the ↓ moved focus to `body`.

## Every difference, and what it belongs to

Nothing here is decided. Each item is what was read beside what the procedure, an ADR, a DoD row or a ticket says.

1. **k6, k6x, k6y on WebAssembly: the second composition lost its first romaji key.**
   - What happened: after Space, the IME ended the first composition at the next key (`k`). On WebAssembly, DOM focus
     moved from the Keyboard Field to the Cell Editor 22–24 ms later (`/sheet`) or 44–45 ms later (the MudBlazor
     Chrome), before the next key. The IME carried `ｋ` there as
     a new composition, without its romaji state, so `a` composed `あ`.
   - Result: D10 `かな暗示` (k6, k6x; k6y on `/sheet`) or `かなん時` (k6y under the MudBlazor Chrome, `ka` both
     lost). Excel and the Server host (both delays): `かな感じ`.
   - It belongs to ADR-0080, "A composition": "DOM focus never moves while a composition lasts"; "The core's request
     that the editor take the keyboard waits while the field composes, and is granted when the composition ends";
     "A second composition the IME finishes in the field before the first one's editor has the keyboard … is typed
     into the edit at its caret, in order".
   - It belongs to DoD row ED-30: "keys typed after a composition's end follow it in order".
2. **k6, everywhere and in Excel: two compositions, not one.** The procedure reads "one composition of two clauses,
   converted together". This IME ended the first clause's composition when the second clause's first key came,
   after Space had converted it. In Excel and on the Server host, D10 still held both clauses. (The procedure's k6
   reading.)
3. **k8: the press on D12 never reached the page.** The IME's prediction list stood there. The composition became the
   prediction `悲しんで`, and the Cell Editor opened holding it on D10.
   - In Excel the click did not select D12 either: `かな` was committed into an open edit on D10.
   - It belongs to DoD row ED-30: "after a press on D12 mid-composition, D10 holds the text and D12 is selected". It
     also belongs to ADR-0080's paragraph "A primary press anywhere in the root during a composition ends it first".
   - With the press clear of the IME's windows (k8x, D8), both read as expected.
4. **k16: the press on the composition's own text never reached the page.** The composition went on, as in Excel. It
   belongs to the procedure's note on k16 ("the press is on D10's row, and ends the composition") and the same
   paragraph of ADR-0080.
5. **Part C: Tab again, and Shift+Tab again, stay in the grid.**
   - Tab into the grid from the element before it landed in the Keyboard Field, with the ring. Tab again moved the
     Focus right inside the grid.
   - Shift+Tab into the grid from the element after it landed in the field. Shift+Tab again moved the Focus left
     inside the grid.
   - It belongs to the procedure's Part C reading ("Shift+Tab again leaves it (A11Y-4)"). It also belongs to DoD row
     A11Y-4's layer-3 criterion: "Tab from before the grid then Tab again, and Shift+Tab from after it then Shift+Tab
     again … focus enters the root, or the field on a grid that edits, then leaves the grid entirely, either way".
   - **The element after the grid** on `/sheet` and `/features` is another grid, display-only. That grid took the
     first Shift+Tab as its own key; Escape there released it.
6. **d5, Narrator.**
   - Tab into the grid on a fresh `/sheet`: "Enter Table, 1048576 by 16384, edit, Scan Off", with no cell, since there
     was no Focus.
   - ↓: "Scan", and DOM focus went to `body`.
   - →: "m".
   - It belongs to ADR-0080, "What assistive technology is given", and ADR-0033 ("What a screen reader then says is
     still owed a real one").
   - The method may have played a part (above). Part C's DevTools reading resolved the field's active descendant to
     the cell (A11Y-21).
7. **Part B against the fifteenth run.**
   - **i7**: the Name Box press now selects `D10`, so the composition replaces it (`かな`, as Excel; ticket 78).
   - **i10a**: `kana` now composes on the selected cell (ADR-0080).
   - **i6**: the IME's ↓ gave `穴` instead of the prediction `あるみたいだから`. That is the IME's own choice, and
     Excel gave `穴` in the fifteenth run.
   - These read differently from that record. None contradicts a reading of this run's procedure.

Every other state read as the procedure expects, or as Excel where the procedure says "record": k1 to k5, k7, k8x, k9,
k10, k11, k12, k13, k14, k15; Part B but the three above; Part C's keys, clipboard, ring and accessibility; d1 to d4.

## Seen, and not asked

- **Under the MudBlazor Chrome, the composition is drawn in the core's field and then moves.**
  - While composing, the field's box is the Focus cell's: 99 px wide, 405 px from the left.
  - Mud's Cell Editor, which takes the text at the composition's end, stands 8 px further in and 83 px wide. The
    text moves by that much at the hand-over.
  - The field's outline wears Mud's colour (`#594ae2`) and its text `#424242`. ADR-0080 records the composing cell
    under a substituted Chrome as looking like the built-in Cell Editor until the commit.
- **The ring (KB-12)**:
  - shown after Tab or Shift+Tab into the grid;
  - not shown after a press;
  - shown again whenever the keyboard came back to the field by the grid itself after a key: Enter committing an edit,
    Escape cancelling one, a Name Box or Find closed by Escape.
- **On the Server host, the second composition of k6 stayed in the field** while the edit, already open, held the
  first. The field showed both, `かなかんじ`, as one text over D10, until the composition ended.
- **The IME's prediction list and composition take presses** (above). A press on D12, two rows under a composition on
  D10, chose a prediction on this machine.
- **d3 in Excel**: the click into the Name Box selects its whole text whatever the drag. Excel and ExSheet agree.

## The machine afterwards

- **Processes.**
  - No Excel is running. Every Excel the script started was its own (`New-Object`): 20 in all, for case 0, the 17
    cases and the 2 run again. 8 of them did not quit within 10 s and were ended by `Stop-Process`, each the script's
    own.
  - No Playwright browser and no input helper is running.
  - The latency proxy was stopped, and both DemoHosts in WSL were stopped.
  - The user's Chrome and the Stream Deck's `node` were left alone.
- **Excel's AutoRecover workbooks were not touched.** `Book1 (version 1).xlsb`, the unsaved workbook and `Excel15.xlb`
  hash as the backup taken before the first Excel started (`%LOCALAPPDATA%\exgrid-layer3\autorecover-backup-run16`).
- **The keyboard.**
  - English (UK), 0x08090809, as found. Each probe run and the Excel script put it back at its end.
  - The IME is off. Its settings were not changed. Its input history was: this run's conversions and the
    prediction `悲しんで`, chosen in k8.
- **Narrator** is off (turned off by the user), and its settings are as before d5 (above).
- **The regional format**: en-GB, unchanged. **The display**: 150%, unchanged.
- **Kept on this machine, not committed** (`%LOCALAPPDATA%\exgrid-layer3\run-2026-10-02-16`; the records hold every
  state):
  - the page's pictures of every configuration but WebAssembly and Chrome's `/sheet`, `/features` and `/sheets`;
  - Excel's whole-window pictures (`ask-excel-16`; the committed crops are cut from them);
  - the trials' records.
