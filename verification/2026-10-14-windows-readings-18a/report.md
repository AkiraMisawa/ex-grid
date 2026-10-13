# Windows, run 18a: Excel's answers to the eighteenth run's questions, and ED-2 on Windows

**Verified commit:** `73d2b2bc` (origin/main), on branch `claude/windows-readings-18a`. This run was asked for by the
session preparing `0.1.0-rc.1`, and ADR-0208 says these questions are asked now. It ran on 2026-10-13 from 22:15 to
23:01 BST. A Claude Code session in WSL2 on the Windows desktop of the earlier runs ran it, in its own worktree.

**Nothing was decided.** Excel's answers are records, not decisions. Where one differs from what ExSheet or ExPivot does
or reads, it is listed under "Where Excel differs" for the user. No product code changed, and no ADR, `CONTEXT.md`, the
Definition of Done or a test changed. The corpus changed only where `-Update` changed it, less four promotions put back
(see "The area oracle").

## Files

| | |
|---|---|
| `report.md` | this report |
| `area/` | the oracle pass: `scripts/area-pass.ps1` and `scripts/restore-culture.ps1` (Windows), the logs (`logs/`), `count.py` with its output `counts-before.md` and `counts-after.md`, and `summarise.py` with its output `summary.md`, which lists every case the pass asked |
| `../../tests/ExSheet.Engine.Tests/ExcelOracle/results-2026-10-14-run18a-*.json` | the pass's results, as the oracle wrote them |
| `excel/xl-server.ps1`, `xs.py`, `xs-start.sh`, `rx.py` | the Excel driver: an Excel of the script's own, real keys and presses through SendInput, and pictures Excel's windows draw of themselves. This is the 2026-10-12 readings' server with three commands added: `ps` (one PowerShell expression over COM), `savexml` (a copy saved as .xlsx, its `sheet1.xml` and `styles.xml` kept), and `bar2` (the Formula Bar's selection with its start and end offsets through UI Automation, so a caret's place is read) |
| `excel/f4.py`, `focus.py`, `fh.sh`, `widen.py`, `fc.py`, `t103.py` | the scripts that asked each question file. `hd.sh`, `pxs.sh`, `handle.ps1`, `px.ps1`, `ink.ps1` and `inkruns.py` are the measuring tools (the 2026-10-12 readings') |
| `records/*.jsonl` | every reading as the scripts wrote it, and every exchange with the server (`*-xs.jsonl`, `steps.jsonl` for the commands given one at a time) |
| `shots/` | the crops (423): the cells, the bar, dialogs and enlarged parts, plus the `sheet1.xml`/`styles.xml` read. Whole-window pictures stay on the machine, because they show the title bar's account corner |
| `ed2/` | the ED-2 runs: `ed2.cmd`, each host's list output and JSON report |

## Environment

- **Windows 11 Pro 25H2**, build 26200. One display, 3840 × 2160 at **150%**. Display language **en-GB**.
- **Excel**: Microsoft 365, version **16.0.20430.20146**, x64, **Current Channel** (`CDNBaseUrl` 492350f6-…), English
  (UK) display language. It is the same build as runs 17 and the 2026-10-12 readings.
- **Regional format**:
  - **en-GB** at the start and at the end.
  - Set to **en-US** at 22:21:53 for the oracle pass and for every Excel reading but Format Cells 9 and 10.
  - Format Cells 9 and 10 set it in turn to en-GB, en-US, zh-CN and ko-KR, each before a new Excel started.
  - Put back to **en-GB** at 22:57:19. `HKCU\Control Panel\International` exported before and after reads the same
    (SHA-256 `071c2b47…456b1af`, the same hash as the 2026-10-12 passes).
  - The user had said a change of regional format need not be asked each time.
- **Keyboard**: Excel's window was given English (UK) (0x08090809) by the server, as before.
- **Browsers (ED-2)**: Windows' own Edge 155.0.4283.45 and the installed Chrome, driven by Playwright 1.62.1 under the
  portable Node v24.14.1, from a copy of `tests/ExGrid.Browser` at `73d2b2bc` (`npm ci` on Windows).
  - The DemoHosts were built at `73d2b2bc` in this worktree and ran in WSL (`dotnet run`, Debug). This worktree had
    ports of its own: WebAssembly on 5399 (API 8399), the Server host on 6398 behind the latency proxy on 5398 (API
    8398), with `EXGRID_HOST_LOG` set to the path the Windows runner reads.
- **Excel's AutoRecover folder** was copied first (`%LOCALAPPDATA%\exgrid-layer3\autorecover-backup-run18a`).
  - At the end it held the same files, except that Excel had removed the empty `XLSTART` folder (at 22:33). It was put
    back, empty, as it was.
  - No Excel is running after the run.

## 1. The area oracle (`docs/specs/exsheet/run-18/area-oracle.md`)

`oracle.ps1 -Area <the 31 files> -Update` through COM under en-US, 22:21:53 to 22:23:43. Then
`oracle.ps1 -Id LET-113 -Keys -Update` under en-US, 22:23:56 to 22:24:02. Each was run as run 17's Part A ran it. The
file names no locale for any uncertain case. Every case the pass asked that needs another locale (104 needing en-GB)
is already `observed`, so no other locale was asked. `import-function-observations.py` was not run: the file does not
ask for it.

| Results file | Regional format | How | agree | disagree | blocked | recorded |
|---|---|---|---|---|---|---|
| `results-2026-10-14-run18a-com-en-US.json` | en-US | COM, the 31 files | 536 | 66 | 115 | 28 |
| `results-2026-10-14-run18a-keys-en-US.json` | en-US | keys, LET-113 | 0 | 0 | 0 | 1 |

**The uncertain cases asked (139):**

- **44 agreed.** 40 were promoted to `observed`.
- **66 disagreed.**
- **29 were recorded:** 28 by COM, and LET-113 by keys. In each the engine refuses by decision.
- **None was blocked.**

The other rows of the table are cases already `observed`, asked again, or `documented`. TABLE-005 to 010 are blocked:
Excel has no Linked Table waiting for its data.

The file counted 137 askable through COM. The pass asked 138 through COM because TABLE-024 is `uncertain` in
`linked-tables.json`, and LET-113 by keys.

**Four agreements were not promoted.** EDATE-023, EOMONTH-018, NETWORKDAYS-010 and WORKDAY-011 agree with Excel's
`#VALUE!`. `-Update` made them `observed`. `RefusalTests.ADR0047_every_refusal_in_the_corpus_is_the_formulas_and_every_excel_value_error_is_not`
then failed:

- the engine still refuses each of the four (a Refusal);
- that test requires an observed `#VALUE!` to be an Error Value a Formula can catch.

Making the engine answer a catchable `#VALUE!`, or marking the cases `engineDiffersByDecision`, is a product decision.
The four were therefore put back to `uncertain` by hand, and are left for the orchestrator.

**The corpus after the pass:** observed 3251, documented 10, uncertain 99. The 32 files: 717 observed, 6 documented, 99
uncertain (`counts-after.md`; before: 677, 6, 139). `nix develop -c dotnet test tests/ExSheet.Engine.Tests` over it:
6036 passed, 0 failed, 3 skipped.

`summary.md` lists every disagreement with Excel's double, every recorded case, and the 40 promoted. In outline:

- **Excel answers where the engine refuses with `#VALUE!`:**
  - EDATE-018, 019; FILTER-014; MOD-033 to 036;
  - PMT-038 to 041, 044, 045, 047 to 053;
  - SORT-026, 029, 030, 034, 035, 037 to 039, 042; SORTBY-010; TRANSPOSE-007; UNIQUE-021 to 023; XNPV-018.
- **Another error where the engine refuses:**
  - `#NUM!`: DATEDIF-048, EDATE-020, HOUR-005, MINUTE-003, SECOND-003, YEARFRAC-058, PMT-034 to 037, 042, 046,
    XNPV-019, 021 to 024;
  - `#DIV/0!`: PV-013, SORT-032, XNPV-026 to 028;
  - `#N/A`: SORT-028, 031, XNPV-025, 029, 030.
- **INDIRECT-075 and 076** (`R2C2:C3`, `R3:R2C2`): Excel sums, 1.333 and 46.333, where the engine expects `#REF!`.
- **PMT-043:** Excel would not take `=PMT(1E-320,0.5,1000)` through COM (0x800A03EC).
- **MOD-039:** Excel's double is 2349.9750989999998. The expected 2349.975099 is the same double: 0 units in the last
  place. It is listed because the oracle compared the text of `value2`.

## 2. The Excel UI questions

Each was read in an Excel of the driver's own, at 100% zoom, under en-US unless it says otherwise. Every key and press
was a real one (SendInput). UI Automation and COM were used only where no edit was open, or through UI Automation alone
mid-edit.

### `f4-enter-mode.md` (`excel/f4.py`; `records/f4.jsonl`; `shots/f4-*`)

After every key, with no key sent to read it, the script read the bar's text and its selection, with start and end
offsets through UI Automation, and the status bar's Cell Mode. It also took a picture of the bar with the Name Box and
of C8:F14. In the pictures checked, the caret and highlight match UI Automation's offsets.

1. **F2 in the bar, then F4.** `=A1+B2` typed in the bar is Edit mode. F2 turns it to **Enter** mode. F4 gives
   `=A1+$B$2` with **a caret at the end** (8, 8): nothing is selected.
2. **Which caret selects.**
   - (a) `=A1+B2`, ←, → gives a caret at 6. F4 gives `=A1+$B$2` with **a caret** at 8.
   - (b) `=A1+B2`, ←×3, `0` gives `=A10+B2` with the caret at 4. F4 gives `=$A$10+B2` with **`$A$10` selected** (1–6).
   - (c) A press into the bar's text right after `A1` of a committed `=A1+B2` (x 383, between the `1` and the `+`)
     opens Edit with the caret at 3. F4 gives `=$A$1+B2` with **`$A$1` selected** (1–5).
3. **Shift+Home while pointing at a range.** In D10: `=`, ↓, Shift+↓ gives `=D11:D12`. Shift+Home gives **`=A11:D12`**,
   still in **Point** mode, with `A11:D12` selected in the bar and outlined on the sheet.
4. **F4 on a Reference with a Sheet qualifier**, F2 first.
   - `=Sheet2!A1` with the caret after `A1`, which is the text's end: F4 gives `=Sheet2!$A$1` with **a caret at the end**
     and nothing selected.
   - Asked again with the caret inside the text: `=Sheet2!A1+1`, ←←, the caret after `A1`. F4 gives `=Sheet2!$A$1+1`
     with **`$A$1` selected** (8–12). The selection starts at `$A$1`, not at `Sheet2!`.
5. **The Primary Modifier's arrows after `=`.**
   - `=` then Ctrl+↓: Excel **points**, in Point mode, at **`=D1048576`**.
   - `=` then Ctrl+Shift+↓ gives **`=D10:D1048576`**, anchored at the cell being edited.
6. **The grey ground after `=`.** `=`, ↓ gives `=D11`. `=`, ↓, ↓ gives `=D12`.
   - In the cell, the pointed Reference **stands on a grey ground** (`f4-q6a-2-cells.png`, `f4-q6b-3-cells.png`).
   - In the bar it has no grey.
   - UI Automation gives the bar's selection as **`D11`** and **`D12`** (offsets 1–4), not an empty one.
7. **The directions R1 (vii) did not ask.** Each one is from `abc` in D10, Enter mode, and **every one committed** `abc`
   to D10:

   | Key | Selection after | Active cell |
   |---|---|---|
   | Shift+↑ | D9:D10 | D10 |
   | Ctrl+← | A10 | A10 |
   | Ctrl+↑ | D1 | D1 |
   | Ctrl+↓ | D1048576 | D1048576 |
   | Ctrl+Shift+← | A10:D10 | D10 |
   | Ctrl+Shift+↑ | D1:D10 | D10 |
   | Ctrl+Shift+↓ | D10:D1048576 | D10 |

8. **`=` then Shift+Home.** It gives **`=A10:D10`**, in **Point** mode, with `A10:D10` selected in the bar. On the
   sheet, A10:D10 is outlined and dashed, and the Reference in the cell stands on grey (`f4-q8-2-cells.png`).

### `focus-among-ranges.md` (`excel/focus.py`; `records/focus.jsonl`; `shots/focus-*`)

R4's selections, at Excel's 100% and 150% zoom on the 150% display. The colours were read along four lines across the
active cell's edges.

1. **A display at 100%.** **Not asked.** The desk has one display, at 150%. Changing its scale needs a Windows sign-out,
   which stops WSL and this session.
2. **D4 filled yellow.** At both zooms the border is 1 device pixel of #217346, as before. Between the border and the
   area's outline:
   - **on the left and top, a white device pixel**, as without a Fill: a line of its own, outside the cell;
   - **on the right and bottom, the Fill** (#FFFF00), inside the cell.

   The outline's own pixel, #AFAFAF without a Fill, is **#C7C700**: the yellow under the selection shade. So that pixel
   is the cell's ground under the shade, and the Fill covers the gridline there.
3. **The active cell while it is edited.** After F2 on D4, **no border is drawn**: no #217346 on any edge, at either
   zoom. The #AFAFAF line around the area stays, and the cell is white with the caret at its left
   (`focus-edit-z100-1-after-f2-active.png`).
4. **Gridlines off.** In all three selections, at both zooms, the **#AFAFAF line is gone**. That pixel is the shade,
   #C7C7C7, or white outside a shaded area. The white device pixel between the border and the outline stays. So the
   #AFAFAF line is **the gridline under the shade**.

### `fill-handle-axis.md` (`excel/fh.sh`; `records/steps.jsonl`; `shots/fh-*`)

1. **Every cell, at the far corner.** Ctrl+A, `XFD1048576` in the Name Box, Enter, Ctrl+A (`1:1048576`, active
   XFD1048576), Ctrl+Backspace (the view at row 1048520, column 16347). **No handle is drawn** at XFD1048576. The only
   square found is the select-all corner (`fh-1-1-far-corner-c.png`).
2. **The Extent at the first column.**
   - D's Heading, then Shift+B's, gives B:D, active D1. The handle is **at D's top right** (x 420–424, y 351–356).
   - Row 4's Heading, then Shift+row 2's, gives 2:4, active A4. The handle is **at row 4's bottom left** (y 465–469).
   - So it is the last column or row in both cases, wherever the press began.
3. **A whole row with column A scrolled away.** Row 3, then the horizontal bar's right arrow ×3 (ScrollColumn 4). The
   handle is **drawn at the left of the part in view**, clipped to **4 × 6 px** (x 40–43, y 436–441). Unscrolled it is
   6 × 6 at x 38–43.
4. **A lone number in a range that is not a whole axis.** The handle was dragged two columns right.
   - C1 `1`, C2 `5`: D1:E1 is **`1 1`** and D2:E2 is **`5 5`**. Copied.
   - C1 `1`, C2 empty: D1:E1 is **`2 3`**, a series. D2:E2 stays empty.
5. **Columns B:C, B1 `1`, C1 empty, dragged right to E.** D1 is **`2`** and **E1 is empty**. The pattern (number, blank)
   is extended: the number goes on as a series, and the blank stays blank.
6. **A whole column's format.** C1 `1`, C:C made bold with Ctrl+B on its Heading, dragged right to E.
   - D100, E100 and E1048576 are **bold**; F100 is not. D1 `2`, E1 `3`.
   - `UsedRange` is **`C1:E1`**.
   - The saved `sheet1.xml` holds **`<col min="3" max="5" … style="1"/>`**, a column's format, with cells only in row 1
     (`fh-6-after-sheet1.xml`; before: `<col min="3" max="3" … style="1"/>`).
7. **Left, and a date.**
   - Column C (C1 `5`) dragged left to A gives **B1 `4`, A1 `3`**. A drop over A's middle (x 89) reached only B. Dropped
     further left (x 50), it reached A.
   - Column C with C1 `9/26/2026` (typed) dragged right gives **D1 `9/27/2026`**, E1 `9/28/2026`.

### `widening.md` (`excel/widen.py`; `records/widen.jsonl`; `shots/w-*`)

Each case ran in a new Excel. A1 held 123456789.12 in `0.00` at the standard width (8.09), showing `########`.
"Widens" means `UseStandardWidth` false and the number shown.

| # | Gesture | Column A after | A1 shows | Widens? |
|---|---|---|---|---|
| 1 | Font tab, Strikethrough alone, OK | 11.73 | `123456789.12` | **yes** |
| 2 | `Font.Color = 255` through COM | 8.09 | `########` | no |
| 2 | then the Font tab, Normal font checked, OK | 11.73 | `123456789.12` | **yes** |
| 3 | Font tab shown, OK with nothing changed | 8.09 | `########` | no |
| 4 | Alignment tab, Horizontal: Center, OK (`HorizontalAlignment` −4108) | 8.09 | `########` | no |
| 5 | **Home tab's Font Colour, its face** (red) | **11.73** | `123456789.12` | **yes** |
| 5 | **Home tab's Font Colour, its arrow, Blue** (a workbook of its own; colour 0x0070C0) | **11.73** | `123456789.12` | **yes** |
| 6 | `Font.Italic = True` through COM | 8.09 | `#######` | no |
| 6 | `Font.Strikethrough = True` through COM | 11.73 | `123456789.12` | **yes** |
| 6 | `Font.Color = 255` through COM | 8.09 | `########` | no |
| 7 | `Font.Underline = 2` through COM (widened; set back to standard), then **Ctrl+U** (underline off) | 11.73 | `123456789.12` | **yes** |
| 7 | `Font.Strikethrough = True` through COM (widened; set back), then **Ctrl+5** (off) | 11.73 | `123456789.12` | **yes** |
| 8 | Ctrl+4 (underline) | 11.73 | `123456789.12` | **yes** |
| 9 | Ctrl+I, then Ctrl+U | 8.09 after Ctrl+I; **12.45** after Ctrl+U | `#######`, then the number | no, then **yes** |
| 10 | A1 and C1, press then Ctrl+press; Font tab Bold, OK | A 11.73, C 11.73 | both shown | **both**; **one Ctrl+Z undoes both** (8.09, `########`) |
| 10 | Column A from its Heading; Font tab Bold, OK | 11.73 | `123456789.12` | **yes** |
| 11 | Ctrl+Shift+& (outline drawn) | 8.09 | `########` | no |
| 11 | Four thin edges through COM, then Ctrl+Shift+_ (removed) | 8.09 | `########` | no |
| 11 | Border tab, Outline, OK | 8.09 | `########` | no |

Format Cells' UI Automation tree gave no controls in this run (one empty pane). Its controls were therefore pressed where
its pictures show them (`w-dlg-*-c.png`), and each case's dialog was pictured before OK (`w-*-1-before-ok-dialog.png`).

### `format-cells-readings.md` (`excel/fc.py`, `excel/t103.py`; `records/fc.jsonl`, `records/t103.jsonl`; `shots/fc-*`, `shots/t103-*`)

The Border tab on opening, each case in a new Excel. Escape left every line as set (`unchanged: true` for each).

| # | Lines (through COM) | Selected | Edge buttons pressed | Style on opening | Colour |
|---|---|---|---|---|---|
| 1 (a) | B2's left thick | B2 | Left | **Thick** | Automatic |
| 1 (b) | C2's left thick | B2 | **Right** (preview: B2's right thick) | **Thick** | Automatic |
| 2 | B2 thick top, medium right | B2 | Top, Right | **Thick** | Automatic |
| 3 | B2 thick top, thick bottom | B2 | Top, Bottom | **Thick** | Automatic |
| 4 | A1:B1's bottom thick | A2:B2 | Top (Inside enabled) | **Thick** | Automatic |
| 5 | A1's and C1's bottom thick | A2, Ctrl+press C2 | Top | **Thick** | Automatic |
| 6 (a) | A1's bottom thick red | A2 | Top | **Thick** | **red** (and the Style list drawn in red) |
| 6 (b) | A1's bottom medium dashed, Automatic | A2 | Top | **Medium Dashed** | Automatic |
| 7 | ticket 102, below | C2, then B2 | Left on C2; Right on B2 | **Thick** both | **red** both |
| 8 | A1's bottom thick, then A2's top thin | A1 | Bottom (preview thin) | **Thin** | Automatic |

- **7 (ticket 102).** The steps:
  1. C2's left set thin blue and E5's right thick red through COM, E5 = 1.
  2. E5, Ctrl+C; B2, Ctrl+V; Escape.
  3. The file saved: B2 records `<right style="thick"><color rgb="FFFF0000"/>` and C2 records
     `<left style="thin"><color rgb="FF0000FF"/>`, as the case expects (`fc-7-ticket-102-file-*.xml`).

  Over C2 the Border tab opens with **Left pressed, its preview line red, Style Thick, Colour red**. That is B2's
  drawn line, not C2's own thin blue. Over B2 it opens with **Right pressed, Thick, red**. COM's `Borders` reads thick
  red on both cells' shared edge.

  Over B2, Ctrl+1 opened the dialog on the Number tab (B2 holds 1), so the Border tab was pressed.
- **8.** The dialog over A1 shows the thin line. **This is because COM's write of A2's top replaced A1's bottom.** After
  the two COM steps, COM reads A1's bottom as thin. A copy saved at that point records nothing on A1 and a thin top on
  A2 (`fc-8-file-*.xml`). So through COM there were never two records on that edge. The earlier report's reading
  (a thin line over A1) holds, with that cause.
- **9 (ticket 103).**
  - Under en-GB, `d-mmm-yy` typed into Custom shows `5-Jan-26`. The file records **`numFmtId="164"`,
    `formatCode="d\-mmm\-yy"`** (the hyphens escaped).
  - Under en-US, in a new Excel, the file opened shows `5-Jan-26`. **A copy saved straight after opening, before any
    dialog, already records `numFmtId="15"`**, and no custom `numFmt`.
  - Ctrl+1 opens on **Custom**, Type **`d-mmm-yy`**, with the list's built-in `d-mmm-yy` entry selected and **Delete
    greyed** (`t103-q9-3-en-US-dialog-c.png`).
  - Custom, then OK with no change: still 15.
  - So the code becomes built-in 15 **on opening under en-US**, not only once the Number tab is touched.
- **10 (ticket 103).** 46027, `d-mmm-yy`, then `mmm`, then `mmmm`, each typed into Custom:

  | Regional format | `d-mmm-yy` | `mmm` | `mmmm` |
  |---|---|---|---|
  | zh-CN | `5-1月-26` (recorded as built-in 15) | **`1月`** | **`一月`** |
  | ko-KR | **`5-1-26`**: the typed code was stored as **`d/mmm/yy`** | **`1`** | **`1월`** |

  Under ko-KR the hyphens typed were taken as the date separator. The code is stored with `/`, and ko-KR's separator
  `-` shows.

## 3. `f4-enter-mode.md` question 9: ED-2 on Windows

`features.spec.mjs --grep "ED-2"`, headed, `--project=chrome --project=msedge --repeat-each=3`, on each host
(`ed2/ed2.cmd`):

| Host | Passed | Failed | Skipped | Flaky | Time |
|---|---|---|---|---|---|
| WebAssembly (5399) | **36** | 0 | 6 | 0 | 27.1 s |
| Server (5398 → 6398) | **42** | 0 | 0 | 0 | 53.4 s |

- The 6 skipped are "with a 150 ms round trip, …" on WebAssembly, three times in each browser. It skips itself there
  (`test.skip(!SERVER, …)`).
- **Both named tests pass in Chrome and in Edge, every time.** "In Caret Ctrl+← and Ctrl+→ move the caret by a word …"
  passes on both hosts, and "with a 150 ms round trip …" passes on the Server host.
- Each asserts **that Ctrl+→ from the start of `alpha beta` lands at 6**, the next word's start, because `wordKeys`
  gives 6 where `navigator.platform` reads Windows. So **a forward word move lands at the next word's start on Windows**,
  whether held behind F2 or not.
- The fixture fails a test on an unexpected console message or a circuit's error, so none occurred.

## Where Excel differs

These are what Excel showed, set against what ExSheet does or reads, from each question file. They are for the user.

1. **Widening 5: the Home tab's Font Colour widens**, by its face and from its list. ExSheet's Font Colour widens
   nothing. COM's `Font.Color` does not widen, so Excel's button and Excel's method part here. The file says the user
   decides whether the face follows the button or the method.
2. **Fill handle 5: a whole axis with a lone number beside a blank** (B:C, B1 `1`, C1 empty, to E). Excel gives D1 `2`
   and E1 empty. ExSheet refuses ("numbers mixed with other cells").
3. **Fill handle 4: a lone number in a range that is not a whole axis.** C1 `1` with C2 empty, dragged right, is a
   series in Excel (`2 3`). C1 `1` with C2 `5` copies (`1 1`, `5 5`). ExSheet's series by one reaches only whole columns
   or rows today.
4. **Format Cells 10: ko-KR's `mmm` is `1` in Excel, with no `월`.** ICU's abbreviated name is `1월`. `mmmm` (`1월`) and
   zh-CN's `1月` / `一月` agree with ICU's names. Also under ko-KR, a typed `-` became the date separator (`d/mmm/yy`).
5. **Format Cells 9: built-in 15 on opening.** Under en-US the custom code is recorded as built-in 15 as soon as the file
   is opened and saved again, without the Number tab being touched. The reading had "once the Number tab is touched and
   OK pressed".
6. **The oracle:** the 66 disagreements above, and the four agreements put back to `uncertain` (EDATE-023,
   EOMONTH-018, NETWORKDAYS-010, WORKDAY-011): Excel's `#VALUE!` there is a catchable error, which the engine refuses.

Everything else answered as the question files have ExSheet or ExGrid:

- **Fill handle:** 1 (none at the far corner), 2 (the last column or row), 3 (drawn clipped at the left), 6 (a column's
  format, D100/E100 bold) and 7.
- **Widening:** 1–4 and 6–11.
- **Format Cells:** 1–8 (Thick wherever exactly one line shows, and a coloured or dashed line followed). These extend
  the rule to left and right edges, to ranges, to several ranges, and to one line on two edges. Case 2 opens on Thick
  with a thick top and a medium right.
- **Focus among ranges:** 2–4 are readings. The Paper does not draw the #AFAFAF line, and it is the gridline.
- **F4:** 1–8 are readings for the orchestrator. ExSheet's side is not read here.

## Not done here

- **Focus among ranges 1** (a display at 100%): the desk has no such display, and changing scale signs Windows out.
- **The rows checked by hand** (VZ-14, VZ-16, FN-12e and `run-18/glyphs.md`, ED-30's real IME, SH-42, SH-51, SH-52,
  KB-3) wait for `0.1.0-rc.1`, as the request said.
- **No screen reader** was run (ADR-0033, PR #109).

## Nothing for the Docs Site

This branch adds a verification record, the oracle's results files, and the corpus's `source` fields that `-Update`
changed. None of it is anything a Consumer sees.
