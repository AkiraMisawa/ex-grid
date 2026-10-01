# Verification — 2026-09-30, Windows, ExSheet beside Excel (eighth run, Part B)

**Scope: Part B of [`verify-on-windows-8.md`](../../docs/specs/exsheet/verify-on-windows-8.md)**, what
ExSheet draws while a Formula is edited on `/sheet`, beside what Excel drew in Part A
([`../2026-09-29-windows-excel-8/range-finder.md`](../2026-09-29-windows-excel-8/range-finder.md)),
and the three further items the procedure lists.

**Verified commit: `9d208c0eac1fc606c40d80aa78edcffa9c737904`**, the tip of
`claude/exsheet-start-8cx3v1` when this run began. The results are committed on
`claude/exsheet-windows-verify-8b`, branched from that commit.

- The procedure names `claude/exsheet-reference-outlines`. That branch was merged into
  `claude/exsheet-start-8cx3v1` by PR #31 (`e153143`), so this run is on the merged tip.
- Tickets 28, 29 and 30 still say `Status: ready-for-agent` there (27 says `done`). Their
  implementation is merged, and the run went ahead on the user's word.
- Nothing here changes an ADR, `CONTEXT.md` or the Definition of Done.

Files beside this one:

- `part-b-probe.mjs`: the probe. Playwright opens `/sheet`, reads the DOM and takes the page's
  pictures. It sends no input: every key and click goes through `input-server.ps1`.
- `input-server.ps1`: real OS input. It is the fifth run's helper, with every key sent through
  `SendInput`, a virtual-key and a scan code per key, as Part A typed.
- `theme-state.ps1`: the display settings high contrast touches, as text, before and after.
- `summarise.py`: condenses a record to one line per case, surface and state, and compares the
  configurations.
- `summary.txt`: its output over the six recorded configurations.
- `part-b/`: the records, one file per configuration and mode:
  - `{wasm,server,server-150}-{chrome,msedge}.json`: every case;
  - `fast-server-150-*.json`: the fast-typing check;
  - `hc-*.json` and `hc-system-*.json`: high contrast;
  - `extra-*.json`: additions not in the procedure.
- `shots/`: the page's picture of the Sheet, the Formula Bar and the positions grid, at 600 ms, for
  every state:
  - all of WebAssembly under Chrome (`<case>-<surface>-<state>-wasm-chrome.png`);
  - the four high-contrast runs.

  The other configurations' pictures are identical in every reading below, and stay on the machine.

## Environment

- The machine of the earlier runs: Windows 11 Pro (build 26200), one display, 3840×2160 at **150%**
  (`devicePixelRatio` 1.5 on every page). Light mode, high contrast off (except where stated below),
  regional format en-GB.
- **Chrome 153.0.8010.54 and Edge 154.0.4258.37**, as installed, headed, the window at the display's
  own scale (`viewport: null`, 1600×1050 DIP).
  - Driven by Playwright 1.62.1 under the portable Node v24.14.1, from a copy of `tests/ExGrid.Browser`
    (`%LOCALAPPDATA%\exgrid-layer3\run-2026-09-30-8b`) with `npm ci` run afresh in it.
- **Both DemoHosts ran in WSL from the verified commit's build** (`dotnet build ExGrid.slnx`: 0
  warnings, 0 errors, 18:23):
  - WebAssembly on `localhost:5299`;
  - Server on `localhost:6298`.
- **The 150 ms configuration** reached the Server host through `tests/ExGrid.Browser/latency-proxy.mjs`,
  run on Windows (5298 → 6298, control on 7298). The probe set `rtt=150` before its first key, and the
  proxy answered `rtt=150`. The two plain Server configurations addressed 6298 directly.
- **The keyboard.**
  - Every browser window's layout was English (UK), 0x08090809, before and after each run. The
    probe's `english` request found it so, and changed nothing.
  - The helper's own thread typed through English (UK). `VK_IME_OFF` was sent once per run.
  - Each state's field value is the text the case types, so every key arrived as typed (the values
    are in the records).
- `/sheet` under the built-in Chrome (no `?chrome=`), at the page's own size. The Sheet is
  720×420 CSS px with column A pinned; the positions grid stands beside it.

## Method

1. **Six configurations**, run one after another (18:35:54–18:48:05):
   - WebAssembly, Chrome and Edge;
   - Server, Chrome and Edge;
   - Server behind the 150 ms proxy, Chrome and Edge.

   Each is one page load of `/sheet`, waited until B12 shows `318.25` (the Linked Table's first push).
   Screen pixels were calibrated against the page's own `mousemove` (one try each).
2. **Every case of Part A, typed once into D10 (the "cell" surface) and once into the Formula Bar**:
   - For the cell surface, D10 is pressed with the real mouse and the keys are typed.
   - For the Formula Bar, D10 is pressed, then the Formula Bar is pressed 4 CSS px inside its right
     end, then the keys are typed.
   - Keys go one every 30 ms, as Part A typed them.
   - **600 ms after the last key** the DOM is read and a picture taken, and 300 ms later a second
     picture (for moving dashes). A reading waits longer only if the layer of the surface holding
     focus has not caught up with its field. None had to wait: every state of every configuration,
     at 150 ms too, was read at 600 ms.
   - Then Escape until no edit is open.
3. **What is read, per state**:
   - **The DOM**: which surface holds focus. For both surfaces: the field's value, whether it wears
     `ex-reference-text-shown`, its layer's text and visibility, and each span of the layer with its
     computed `color`, `-webkit-text-fill-color` and ground.
   - **Every `.ex-reference-outline` and `.ex-point` over the Sheet**, with its computed colours and
     the cells in view it covers. The cells in view are those whose middle lies in the scroller's
     client area; a Reference whose cells are all outside the view is "none in view".
   - **The positions grid's outlines**, with the column each covers.
   - **The pixels of the page's own picture** (`page.screenshot`, device scale; the screen copy
     returns nothing on this machine since 2026-09-29):
     - each visible span's darkest pixel, which is its text colour, and its ground;
     - each outline's line (its outer 3 device px) and fill (inside 9 px), as the most frequent
       colour;
     - along Point's dashes, the colour and the number of runs;
     - the pixels that changed between the two pictures.
4. **Part A's COM set-ups, made on `/sheet` with keys** (the user's instruction for Part B):
   - **Case 21**: `=A1+B1` typed into D10 and entered with Enter. Then D10 pressed, and the four
     states. Afterwards D10 is pressed and cleared with Delete.
   - **Cases 11, 12, 27 and 28**: read against `/sheet`'s Linked Table `Positions` (columns `Id`,
     `Book`, `PV`), shown in the positions grid. No set-up is needed.
   - **Case 9: not applicable.** It adds a sheet `Sheet2`. `/sheet` holds one Sheet, and ExSheet has
     no second sheet to add; a Reference qualified with another name names no cells (ADR-0046).
   - **Case 23: not applicable.** It sets the sheet's zoom to 400%. `/sheet` has no zoom of its own,
     and the browser's page zoom scales the whole page, which Excel's sheet zoom does not.
   - Part A's `1fb` (case 1 typed into the Formula Bar) is case 1's Formula Bar surface here.
5. **Before every case**, D10 is checked empty and cleared with Delete if not. In every configuration
   this happened once, before case 8: case 7's Formula Bar keys had entered `=A1+B1` into D10 (below).
6. **The probe gained three things after the six configurations had run**, for the additions and
   the high-contrast run: `MODE=enter`, case `7k` (run only when named), and `EMULATION`. The code
   the cases run through did not change. `part-b-probe.mjs` is the final version.
7. **Trials (18:29–18:34)** tried the probe out and are not recorded. They showed three things, each
   fixed before the recorded run:
   - A press on D10 that came soon after the cleanup's own press on D10 was a double click, and
     opened an edit. The probe now presses D10 only when the Focus is elsewhere.
   - The Cell Editor's text scrolls, so a span's pixels are read only where the layer shows it.
   - Case 21's set-up sent Enter 30 ms after the last key once, and D10 then did not take the Focus
     after a press. This was repeated nine times as an addition, and did not recur (below).

## Results

**Every reading was identical in all six configurations**, the DOM and the pixels alike (`summary.txt`,
78 states: "same in 6", and no pixel reading differs). So the table has one ExSheet column per surface,
not one per configuration. The Excel column is Part A's, in brief.

- **"coloured"**: the field's own text is transparent over its layer, which shows each Reference in
  its colour.
- **"plain"**: the field's own text shows.
- **Colours**: the computed colour of the span or the outline. Where the pixels read otherwise, the
  pixel colour follows.
- **Fills**: always read from the pixels.

| # | Keys into D10 | Excel (Part A) | ExSheet, typed into D10 | ExSheet, typed into the Formula Bar | Agrees with Excel |
|---|---|---|---|---|---|
| 1 | `=A1+B1+C1+D1+E1+F1+G1+H1+I1+J1` | seven colours, then round again: A1 `#326ac7`, B1 `#c0353e`, C1 `#8157b7`, D1 `#007c20`, E1 `#b03e84`, F1 `#b64900`, G1 `#267392`, H1 `#326ac7`, I1 `#c0353e`, J1 `#8157b7`. The Formula Bar black. Outlines on all ten, with fill and corner squares | **The same ten colours in the same order**, in the cell (the editor shows the text's end; G1–J1's pixels read `#267392`, `#326ac7`, `#c0353e`, `#8157b7`). The Formula Bar plain. Outlines on A1–F1 in their colours (fills `#eaf0f9`, `#f8eaeb`, `#f2eef7`, `#e5f1e8`, `#f7ebf2`, `#f7ece5`). G1 and beyond lie outside the Sheet's 720 px view. No corner squares | The same colours, in the Formula Bar (the pixels agree for all ten). The cell plain. The outlines as typed into D10 | Colours and order **yes**. The Formula Bar's shades differ (Excel `#006cbe`… in its bar, `1fb`). No corner squares |
| 2 | `=A1+A1` | one colour `#326ac7`; one outline, drawn twice (fill `#d9e2f4`) | both `#326ac7`; two outlines over A1, fill `#d7e3f4` (two layers) | the same | yes; the double fill `#d7e3f4` against `#d9e2f4` |
| 3 | `=A1+$A$1` | as 2 | as 2: `A1` and `$A$1` `#326ac7`; fill `#d7e3f4` | the same | as 2 |
| 4 | `=A1+B1+A1` | `#326ac7`, `#c0353e`, `#326ac7`; A1 fill `#d9e2f4` | `#326ac7`, `#c0353e`, `#326ac7`; A1 fill `#d7e3f4`, B1 `#f8eaeb` | the same | yes (fill as 2) |
| 5 | `=B2:A1` | one outline over A1:B2; `B2:A1` one run `#326ac7` | `B2:A1` one span `#326ac7`; the outline over A1:B2 (fill `#eaf0f9`) | the same | yes |
| 6 | `=A1:B2+B2` | A1:B2 blue; B2 red over it, fill `#e7dde7` (both laid together) | A1:B2 `#326ac7`, B2 `#c0353e` over it; B2's fill `#e5dde6` | the same | yes; the fill `#e5dde6` against `#e7dde7` |
| 7 | `=A1+B1`, then F2, `{HOME}{RIGHT}{DEL 3}` | before: A1 `#326ac7`, B1 `#c0353e`; after (`=B1`): B1 `#326ac7`, A1 no longer outlined | before: `#326ac7`, `#c0353e`. After F2 unchanged. After the deletion `=B1`: B1 **`#326ac7`**, its outline blue, A1's gone | **Before: as in the cell. After F2 the edit stays in the Formula Bar, unchanged. `{HOME}` then ends the edit**: `=A1+B1` is entered into D10 and the Focus moves to A10, `{RIGHT}` to B10, and the Deletes act on the empty B10 (addition `7k`, a reading after each key). No outline remains | cell **yes**. Formula Bar: Excel was asked in the cell only |
| 8 | `=Sheet1!A1` | `Sheet1!A1` one run `#326ac7`; A1 outlined | `Sheet1!A1` one span `#326ac7`; A1 outlined | the same | yes |
| 9 | `=Sheet2!A1+B1` | `Sheet2!A1` black; B1 `#326ac7` | **not applicable** (Method 4) | — | — |
| 10a | `=SUM(A:A)` | the whole column | `A:A` `#326ac7`; one outline over A1:A13, every row in view | the same | yes |
| 10b | `=SUM(1:1)` | the whole row | `1:1` `#326ac7`; the outline over A1:F1, every column in view (two elements, see below) | the same | yes |
| 11 | `=SUM(Positions[PV])` (the Linked Table) | `Positions[PV]` one run `#326ac7`; the Table's data B2:B4 outlined, not its header | `Positions[PV]` one span `#326ac7`. **The positions grid's PV column outlined in `#326ac7`**, over its five data rows and not its header (fill `#eaf0f9`). Nothing on the Sheet | the same | yes |
| 12 | `=SUM(Positions[PV])+SUM(Positions[Id])` | `#326ac7` over PV's data, `#c0353e` over Id's | `Positions[PV]` `#326ac7`, `Positions[Id]` `#c0353e`; the positions grid's PV outlined blue, Id red (fill `#f8eaeb`) | the same | yes |
| 13 | `=SUM(A1,` | A1 coloured and outlined | `A1` `#326ac7`, A1 outlined | the same | yes |
| 14 | `=A1+` | the same | the same | the same | yes |
| 15 | `="A1"&B1` | `"A1"` black; B1 `#326ac7`, outlined | `"A1"` plain; `B1` `#326ac7`, B1 outlined; A1 not | the same | yes |
| 16 | `=LOG10(A1)` | `LOG10` black; A1 `#326ac7` | `LOG10` plain; `A1` `#326ac7`, A1 outlined | the same | yes |
| 17 | `=a1` | `a1` lower case, `#326ac7` | `a1` `#326ac7`; A1 outlined | the same | yes |
| 18 | `A1` (no `=`) | nothing | no span, no outline | the same | yes |
| 19 | `=`, `{DOWN}` | `=D11`; D11 first colour's outline, **green dashes `#217346`** over it, still | `=D11`, `D11` `#326ac7` (no grey); D11 outlined `#326ac7`; **dashes `#000000`**, 2 px, 16 runs along the top, still (0 pixels changed in 300 ms) | **`{DOWN}` does not point: the text stays `=`**, nothing outlined | cell: yes, **except the dashes' colour**. Formula Bar: Excel was asked in the cell only |
| 20 | `=`, `{DOWN}`, `{+}`, `{DOWN}` | `=D11+D11`; both `#326ac7`, fill `#d9e2f4`, green dashes; the second `D11` grey, `#0401a2` | `=D11+D11`; both `#326ac7`; fill `#d7e3f4`; dashes `#000000`; **the second `D11` on `#c6c6c6`, its text `#1b3a6d`** | `=` then `=+`: no pointing | cell: yes, except the dashes' colour and the grey text's shade |
| 20x | as 20, then `{DOWN}{DOWN}` | D11 solid blue; D12 `#c0353e`, fill `#f9ebec`, green dashes; `D12` grey, `#630101` | D11 `#326ac7`; D12 `#c0353e`, fill `#f8eaeb`, dashes `#000000`; **`D12` on `#c6c6c6`, its text `#6a1d22`** | as 20 | cell: yes, except as 20 |
| 21 | `=A1+B1` entered first; (1) selected; (2) F2; (3) Escape, double click; (4) Escape, a press into the Formula Bar | (1) none; (2) and (3) coloured in the cell, the Formula Bar black; (4) the Formula Bar coloured (`#006cbe`, `#bc2f34`), the cell black | (1) **none**; (2) and (3) the cell coloured `#326ac7`, `#c0353e`, the Formula Bar plain, A1 and B1 outlined; (4) the Formula Bar coloured `#326ac7`, `#c0353e`, the cell plain, the outlines as before | (the case's own state 4) | yes; the Formula Bar's shades as 1 |
| 22 | `=D10` | `D10` `#326ac7`; D10 shows only **blue corner squares**, under the active cell's green border | `D10` `#326ac7`; D10 has an outline element in `#326ac7`, but **nothing of it shows**: it lies under the Cell Editor (pixels white). No corner squares | the same: the Cell Editor box stands over D10 showing `=D10`, plain | **in part**: Excel's squares are the only part that showed there |
| 23 | Zoom 400% | 2 px line, 1 px gap, 5×5 squares | **not applicable** (Method 4) | — | — |
| 24 | `+A1` | `A1` `#326ac7`, outlined | `A1` `#326ac7`, A1 outlined; the `+` plain | the same | yes |
| 25 | `-B2` | `B2` `#326ac7`, outlined | `B2` `#326ac7`, B2 outlined | the same | yes |
| 26 | `=SUM(A1:` | `A1` `#326ac7`, outlined; the colon black | `A1` `#326ac7`, A1 outlined; the colon plain | the same | yes |
| 27 | `=SUM(Nope[PV]` | nothing coloured or outlined | nothing | the same | yes |
| 28 | `=SUM(Positions[Nope]` | nothing | nothing; the positions grid unoutlined | the same | yes |
| 29 | `=SUM(`, `{DOWN}` | `D11` grey `#c6c6c6`, text `#0401a2`; green dashes | `=SUM(D11`; **`D11` on `#c6c6c6`, its text `#1b3a6d`** (pixels `#1c3a6d`); D11 outlined `#326ac7`, dashes `#000000` | `=SUM(`: no pointing | shown selected **yes**; the text's shade and the dashes' colour differ |
| 30 | `=1+`, `{DOWN}` | as 29 | `=1+D11`, as 29 | `=1+` | as 29 |
| 31 | `=`, `{DOWN}{DOWN}` | `=D12`; **not** grey; dashes on D12, none on D11 | `=D12`, `D12` `#326ac7` **on no grey**; D12 outlined, dashes on it; D11 nothing | `=` | yes, except the dashes' colour |
| 32 | `=D11+`, `{DOWN}{DOWN}`, then `5` | before: `D12` grey `#630101`. After `5`: `=D11+D125`, `D125` `#c0353e`, nothing grey, D12's outline and dashes gone | before: `D12` on `#c6c6c6`, text `#6a1d22`; D12 red, dashes `#000000`. **After `5`: `=D11+D125`**, `D125` `#c0353e`, nothing grey, **D12's outline and dashes gone**, D11 stays; D125 lies below the painted rows and draws nothing | `=D11+` (no pointing), then `=D11+5`; D11 outlined | cell: yes, except the shade and the dashes' colour |

### What ExSheet draws, throughout

- **The colour of a Reference's text and of its outline's line is the palette's** (`--ex-reference-1`
  to `-7` unset on `/sheet`, so the defaults, which are Excel's seven).
- **The line is 2 px, inside the cells.** It is `outline: 2px solid currentColor`, offset −2 px. The
  fill is `color-mix(… 10%, transparent)`, which reads `#eaf0f9` over white for the first colour.
- **Point's dashes** (`.ex-point.ex-point-on-reference`) are `dashed 2px`, offset −4 px, in `#000000`.
  That is the Focus outline's colour on `/sheet`: `.ex-focus` computes `solid 2px #000000`, and
  `--ex-focus-outline` is not set, so it falls back to `CanvasText`.
- **The Cell Editor's own outline is `solid 2px #0078d7`** (`Highlight`).
- **A Reference whose range crosses the pinned column A is two elements** (cases 5, 6 and 10b: two
  `.ex-reference-outline` of the same box). One Reference inside a single part, pinned or not, is
  one element (cases 1, 2, 4). The fill does not double where the two lie together (case 5's fill
  is `#eaf0f9`, one layer).

## The further items

### The Server host behind 150 ms, case 1 as fast as `by-hand.ps1` sends it (ADR-0057; DC-47)

`MODE=fast`, 18:48:05–18:48:34. `by-hand.ps1` sends a string with one `SendKeys.SendWait`. Here the 30
keys' 98 key events went in **one `SendInput` call**, which took 50–61 ms. Every animation frame was
sampled from before the first key to 500 ms after the colours came back. Three times per surface, per
browser:

| Browser | Surface | Frames sampled | Frames with coloured text | **Frames whose layer differed from the field** | The field held the whole text after | The colours were back after |
|---|---|---|---|---|---|---|
| Chrome | cell | 141–143 | 62–77 | **0, 0, 0** | 261–278 ms | 331–344 ms |
| Chrome | Formula Bar | 126–127 | 61–63 | **0, 0, 0** | 192–197 ms | 295–304 ms |
| Edge | cell | 140–142 | 62–74 | **0, 0, 0** | 255–283 ms | 335–348 ms |
| Edge | Formula Bar | 127–128 | 61–62 | **0, 0, 0** | 192–206 ms | 302–317 ms |

**No frame showed transparent field text over a layer holding other text** (DC-47's criterion). The
field's text showed plain while the round trip was in flight, and the colours came back about 100 ms
after the field was whole.

### Case 11 with the positions grid beside the Sheet (SH-31)

**The PV column of the positions grid is outlined in `#326ac7`, the colour `Positions[PV]` wears in
the editor**, in both surfaces and every configuration.

- The outline covers the column's five data rows, not its header: the box starts where the rows
  start and is 5 × 28 px tall.
- Its fill reads `#eaf0f9`. After Escape it is gone: the next case's readings (13) find no outline
  in the positions grid.
- In case 12, Id is outlined in `#c0353e` as `Positions[Id]` is coloured.

### Windows' high contrast on, case 1 (ADR-0057, "Not done")

High contrast was switched on through `SystemParametersInfo(SPI_SETHIGHCONTRAST)`. Windows applied
"High Contrast Black" (`hcblack.theme`, dark system colours). Case 1 was then typed in both surfaces,
on WebAssembly, under Chrome and Edge. It ran twice:

- **18:51, under Playwright's default emulation.** Playwright emulates `forced-colors: none` and
  `prefers-color-scheme: light` unless told otherwise, and the page reported both. The system colours
  still came from Windows:
  - the page's ground was `#202020` and its text white;
  - **the Reference colours were unchanged**, in the text and on the outlines, over dark fills
    (`#222831` for the first);
  - `shots/1-*-typed-wasm-hc-*.png`.

  This is not what a user's browser shows, and is recorded as run.
- **18:53, with no emulation** (`forcedColors: null`, `colorScheme: null`: the browser's own state).
  The page reported `forced-colors: active` and `prefers-color-scheme: dark`.
  - **Every Reference's text and every outline turned white** (`color` computes `#ffffff`; the spans'
    `-webkit-text-fill-color` still computes the Reference's colour, but nothing of it is painted).
  - The outlines are white 2 px lines over A1–F1, so **which cells are read still shows, but not which
    Reference reads which**.
  - The Cell Editor's outline is `#8ee3f0`. The grid's scrollbar thumbs show as white rings.
  - `shots/1-*-typed-wasm-hc-system-*.png`.

  This is what ADR-0057's "Not done" expects of forced colours. Excel was not checked under high
  contrast.

**The machine afterwards.** High contrast is off again, with its flags `0x7e` as they were.
`theme-state.ps1`'s reading before (18:51:03) and after (18:54:10) is identical in the theme
(`Custom.theme` again), light mode, the accent and colorization values, and every `Control Panel\Colors`
entry.

- Turning it off left four things behind, which were then put back to the values read before:
  - the `HighContrast` key's record of the last contrast theme (`High Contrast Scheme`,
    `Previous High Contrast Scheme MUI Value`, and the new `LastUpdatedThemeId` and
    `… MUI Ptr`);
  - `MenuHilight`, `0 120 215` as before (Windows had left `51 153 255`), in the registry and live
    (`SetSysColors`).
- **One difference remains**: `SystemParametersInfo(SPI_GETHIGHCONTRAST)` still names "High Contrast
  Black" as its scheme in memory, where it named none before. Another `SPI_SETHIGHCONTRAST` to clear
  it re-applied the theme's leftovers (they were put back again), so it was left.

### Console messages

- **Every page**: none of type error or warning, and no page error.
- **WebAssembly**: one info message per load, "Debugging hotkey: Shift+Alt+D (when application has
  focus)".
- **Server**: two info messages per load: SignalR "Normalizing '_blazor'…" and "WebSocket
  connected…".
- **The Server host's log**: only `info:` lines. The circuit log `EXGRID_HOST_LOG` pointed to was
  never created, so no circuit raised an unhandled exception.

## Additions, not in the procedure

- **`7k`: case 7's keys one at a time, a reading after each** (WebAssembly, Chrome and Edge, 18:49).
  - In the cell, F2 changes nothing visible, `{HOME}` puts the caret at 0, `{RIGHT}` at 1, and each
    Delete removes a character, down to `=B1`.
  - **In the Formula Bar, F2 changes nothing, and `{HOME}` ends the edit.** `=A1+B1` is entered
    into D10 (it shows `#VALUE!`), and the Focus moves to A10. `{RIGHT}` then moves it to B10, and
    the Deletes act on B10, which is empty.
  - This is why D10 held `=A1+B1` before case 8.
- **Case 21's set-up with Enter 30 ms after the last key, then a press on D10**, three times each on
  WebAssembly Chrome, WebAssembly Edge and Server Chrome (18:49–18:50). Each time the Enter entered
  the Formula, the Focus went to D11, and **the press took the Focus to D10**. The trial's failure did
  not recur.

## Where Excel and ExSheet differ

Each is listed with the ADR-0057 paragraph or the criterion it belongs to. Nothing here is decided.

1. **Point's dashes are black on `/sheet`, green in Excel** (cases 19, 20, `20x`, 29–32):
   `#000000` against Excel's `#217346`.
   - ADR-0057, "What the eighth Windows run settled", and DC-46 both say "the dashes take
     `--ex-focus-outline`": in the Focus outline's colour. They do.
   - `/sheet`'s Focus outline is `CanvasText` black. Excel's active-cell border is green.
2. **The pointed Reference's text is a different shade on the same grey** (cases 20, `20x`, 29, 30,
   32): ExSheet `#1b3a6d` and `#6a1d22`, against Excel's `#0401a2` and `#630101`. The ground is
   `#c6c6c6` in both.
   - ADR-0057, "What cases 24–32 settled": "a darker shade of its colour (`#0401a2` for the first,
     `#630101` for the second)".
   - `ex-grid.css` says the shade is approximated: the colour mixed 55% toward black.
3. **The fills differ by a unit or two** (cases 2, 3, 4, 6, 20, `20x`):
   - one layer: `#eaf0f9` against Excel's `#ebf0f9` (Excel's case 10 read `#eaf0f9`);
   - two layers: `#d7e3f4` against `#d9e2f4`;
   - B2 in case 6: `#e5dde6` against `#e7dde7`;
   - the second colour's fill: `#f8eaeb` against `#f9ebec`.

   ADR-0057, "What the eighth Windows run settled" ("a pale wash of it (`#ebf0f9` for the first)",
   "`#d9e2f4` against `#ebf0f9`").
4. **No corner squares** (every outline; case 22, where Excel's squares were all that showed of D10's
   outline, shows nothing there). ADR-0057, "Not done" (the squares wait for the drag,
   decided 2026-09-30); `docs/definition-of-done.md` §21.11.
5. **The line lies inside the cells**, where Excel's lies over the gridline and 1 px outside.
   ADR-0057, "Where ExSheet is deliberately unlike Excel".
6. **The Formula Bar wears the cell's palette** (cases 1, 21): `#326ac7`, `#c0353e`… where Excel's bar
   has `#006cbe`, `#bc2f34`…. ADR-0057, "Where ExSheet is deliberately unlike Excel" ("One palette
   serves both surfaces").

Everything else Part A recorded, ExSheet drew the same:
- the seven colours in order of first appearance, and round again;
- the colour shared by the same cells;
- `Sheet1!A1`;
- nothing inside a string or on a function name;
- whole columns and rows;
- the Linked Table's columns outlined in the positions grid in the text's colour;
- nothing for an undeclared table or column;
- `+A1`, `-B2` and `=SUM(A1:` coloured;
- the colours only in the surface the edit is in, under every way of opening it (21);
- the grey on the pointed Reference, except straight after `=` (31);
- `5` following the grey Reference, with D12's outline and dashes gone (32).

### Seen in ExSheet, with no Excel reading to compare

- **In the Formula Bar, `{DOWN}` does not point** (cases 19, 20, `20x`, 29–32). The text stays `=`,
  `=SUM(`, `=1+` or `=D11+`, and nothing is outlined. Part A typed every pointing case into the cell.
- **In the Formula Bar, F2 changes nothing and `{HOME}` enters the Formula and moves the Focus** (case
  7, `7k`). Part A typed case 7 into the cell.
- **A Reference whose range crosses the pinned column is drawn as two elements** (5, 6, 10b). DC-46
  reads "one element per Reference (never per cell)".

## The machine afterwards

- **Processes**: no browser, probe, helper or proxy process of this run is left. The latency proxy
  was ended at 18:54, and both DemoHosts in WSL.
- **High contrast**: as above; off, with one in-memory scheme name left.
- **The keyboard**: English (UK), 0x08090809, as found.
