# What to verify on Windows, eleventh run

Status: ready-for-human. **Parts A and B were run on 2026-10-01 at 76d3866**
(`verification/2026-10-01-windows-excel-11/`, `verification/2026-10-01-windows-browser-11/`; what they
settled is in ADR-0071). **Part C is ready** (tickets 48, 49 and 51 are in).

This is for the Claude Code session on the Windows desktop of the earlier runs (Excel, Chrome, Edge,
WSL2 with nix).
- Read [`verify-on-windows-8.md`](verify-on-windows-8.md) and `verify-on-windows-10.md` first. The
  second is on `claude/exsheet-pointing-scope`. Their method and tools still apply.
- **Decide nothing. Record everything.** Do not change any ADR, `CONTEXT.md` or
  `docs/definition-of-done.md`.

Part A asks Excel only, and Part B asks the browsers only. Neither needs a build. Their answers
settle the readings in
[ADR-0071](../../adr/0071-a-sheets-cell-format-is-document-data-painted-on-white-paper.md) before
its tickets are built.

## Setup

- **The branch.** Fetch **`claude/exsheet-cell-format`**. Branch
  **`claude/exsheet-windows-verify-11`** from its tip, and record that tip as the verified commit.
- **Authorisation.** The user has authorised this run in advance: real keys and mouse to Excel and to
  the browsers. **Do not stop to ask.** Say "starting" before the first input and "finished" after
  the last.
- **Driving Excel.**
  - Drive Excel through `verification/2026-09-27-windows-excel/excel-driver.ps1`, with keys sent by
    `SendInput` and screenshots taken by `PrintWindow`, as the tenth run's `excel-only.ps1` did. That
    script is on `claude/exsheet-windows-verify-10`.
  - Write this run's script as `verification/<date>-windows-excel-11/cell-format.ps1`, taking one
    case or a list of cases.
  - **Every key a case asks about is a real key.** COM may only set up a case and read the result:
    a value, a Number Format, a Font, a Fill, a Border.
- **Keyboard and IME.**
  - The Japanese IME must be off (direct input) for Excel's window.
  - The keyboard layout is English (UK), as in the earlier runs, except where a case names the
    Japanese layout.
  - Record `vkKeyScan` for every character a case sends, as the tenth run did.
- **Record the environment**: the Excel version and build, the Office Theme, Windows' light or dark
  mode, the display scale, and the regional format (`Get-Culture`).
- **Screenshots and colours.**
  - Take every screenshot at **100% zoom**. Repeat at **150%** where a case says so.
  - Sample colours as hex from the screenshot, never from COM. The question is what Excel draws.

## Part A — Excel (ADR-0071, "Readings")

Use a fresh workbook for each case, with one sheet named `Sheet1` and A1 in view. Record each case as
one JSON line in `cell-format.jsonl` and a row in `cell-format.md`. Each row holds the screenshot
crops, the sampled colours, the COM reads that the case names, and the reading beside Excel's
answer.

### Group 1 — a Number Format's colour

| # | Set up (COM) | What is asked | Reading |
|---|---|---|---|
| 1 | A1 = -5, B1 = 5; format `[Black]0` … `[Yellow]0`, one name per row (rows 1–8). For `[White]`, fill the row black | The RGB of each of the eight names | Excel's legacy palette: `#000000`, `#0000FF`, `#00FFFF`, `#00FF00`, `#FF00FF`, `#FF0000`, `#FFFFFF`, `#FFFF00` |
| 2 | A1 = -5, B1 = 5; format `0;[Red]-0`; Font colour blue (`Font.Color`) on both | The colour of each | A1 red (the format wins), B1 blue |
| 3 | A1 = 5; format `[Color10]0` | The colour. For a later step; nothing is built on it now | — |
| 3b | Format `[Red]0` on A1 = `abc`, A2 = `TRUE`, A3 = `=1/0`; and `0;[Red]@` on A4 = 5 | Is any of them red? | none: no section shows them, so no colour |
| 3c | A1 = -123456789 in `0;[Red]-0`, column A narrowed until it shows `####` | The colour of the `#` | red: a `####` keeps its section's colour |

### Group 2 — Fills and gridlines

| # | Set up (COM) | What is asked | Reading |
|---|---|---|---|
| 4 | B2 filled yellow; its neighbours empty | Is each of B2's four edges drawn in the gridline's colour, or in the Fill's? Sample 1 px on each side of each edge | the Fill covers them |
| 5 | B2 filled white (`Interior.Color = 0xFFFFFF`, not "No Fill") | The same | the gridlines around B2 disappear |
| 6 | B2 and C2 filled yellow | The line between B2 and C2 | none; yellow throughout |

### Group 3 — Borders

For each case, read all four edges of every cell it names (`Borders(xlEdgeLeft)` … as `LineStyle`,
`Weight` and `Color`). Record which cells hold which sides, then take the screenshot.

| # | Set up / keys | What is asked | Reading |
|---|---|---|---|
| 7 | COM: B2's right edge thick red. Read C2's left edge. Then set C2's left edge thin blue, and read B2's right edge | Does setting one side write the neighbour's? Which line is drawn? | unknown: record both reads and the pixels |
| 8 | COM: B2's bottom edge thick black. Also at 150% | How many pixels above and below the gridline the line takes, and whether it lies inside B2, inside B3, or across both | across both, centred |
| 9 | COM: B2:B14 each get a bottom edge in one of the thirteen styles. The styles as (`LineStyle`, `Weight`) are: hair (Continuous, Hairline), thin (Continuous, Thin), medium (Continuous, Medium), thick (Continuous, Thick), double (Double, Thick), dotted (Dot, Thin), dashed (Dash, Thin), dash-dot (DashDot, Thin), dash-dot-dot (DashDotDot, Thin), medium dashed (Dash, Medium), medium dash-dot (DashDot, Medium), medium dash-dot-dot (DashDotDot, Medium), slanted dash-dot (SlantDashDot, Medium). Also at 150% | A ×4 crop of each line, with its pattern read as pixels on and off along 40 px | — (reference crops for layer 3) |
| 10 | COM: B2's bottom edge thick black; B3 filled yellow | Does B3's Fill cover the part of the line inside B3? | no; the line is above the Fill |
| 11 | COM: B2:C3 with every edge thin black. Select B2:C3 with real keys | The screenshot: the Focus and the Selection over the borders | the Selection is drawn above |
| 12 | COM: B2 filled yellow, its top edge thin and bottom edge thick. Select row 3 with real keys (A3, Shift+Space) and insert (Ctrl+Shift+`=`) | Row 3's cells: Fill and four edges. Row 4's (the old row 3) top edge | Row 3 takes B2's Fill; the borders unknown: record |
| 13 | Select B2:D4 with real keys, press Ctrl+Shift+`&`, then Ctrl+Shift+`_` | After each key: every edge of B2:D4, and of A2:A4, E2:E4, B1:D1 and B5:D5 | `&`: the outer edges of the range only; `_`: every edge of the range cleared. The neighbours: record |
| 14 | Select B2:C3, Ctrl+select E5:F6, press Ctrl+Shift+`&` | Does each range get its own outline? | yes |
| 15 | Select column B (Ctrl+Space), press Ctrl+Shift+`&` | B1's top edge, B5's left and right edges, B1048576's bottom edge; and `Columns(2).Borders` | recorded at column level |

### Group 4 — keys

Before each case, A1 = `abc` and A2 = `def`, with no formatting. Read `Font.Bold`, `Font.Italic`,
`Font.Underline`, `Font.Strikethrough` and `NumberFormat` after each key.

| # | Keys | What is asked | Reading |
|---|---|---|---|
| 16 | On A1, each of Ctrl+B, Ctrl+2, Ctrl+I, Ctrl+3, Ctrl+U, Ctrl+4 and Ctrl+5, twice | What each sets, and whether the second press takes it off | each toggles; underline is single |
| 17 | A1 bold (COM). Select A1:A2 with the Focus on A1 (A1, Shift+Down), Ctrl+B. Then reset, and select A2:A1 with the Focus on A2 (A2, Shift+Up), Ctrl+B | Both cells' bold after each | the Focus decides: first both plain, then both bold |
| 18 | A1 = 1234.5. On A1, each of Ctrl+Shift with `~`, `!`, `@`, `#`, `$`, `%` and `^`, typed as the **character** on the UK layout | `NumberFormat` and `NumberFormatLocal` after each, and the text shown | General, `#,##0.00`, a time, a date, a currency, `0%`, `0.00E+00` |
| 19 | As 18, but sent as the **US position** of each character on the UK layout (VK `OEM_3`, `1`, `2`, `3`, `4`, `5`, `6`). Then Ctrl+Shift+`&` and `_` both ways. Then switch Excel's window to the **Japanese** layout (IME off) and repeat 18 and 19 | Which of the two ways Excel answers, on each layout | unknown: record |
| 20 | Repeat 18 under the regional formats `en-US` and `ja-JP` (`Set-Culture`, then restart Excel), if that is possible without signing out. Otherwise record that it was not | The formats each key applies under each | unknown: record |
| 21 | F2 on A1 (`abc`). Select `b` (Left, Shift+Left). Press Ctrl+B, then Ctrl+Shift+`$`, then Ctrl+1 (Esc to close), then Esc to cancel the edit | What each key does while the edit is open | Ctrl+B bolds `b` alone; `$`: record; Ctrl+1 opens a Font-only dialog |

### Group 5 — Format Cells and the palette

| # | Set up / keys | What is asked | Reading |
|---|---|---|---|
| 22 | Ctrl+1 on A1. Visit each tab | A screenshot of each tab. The Number tab's category names in order. The Border tab's line styles in order, and its presets. The order of the tabs. Which tab opens first on a fresh Excel, and on the second Ctrl+1 | Number, Alignment, Font, Border, Fill, Protection |
| 23 | The Font Color list on the Home ribbon, opened with the mouse | Every swatch's hex: the theme colours (10 × 6) and the standard colours (10), with their names from the tooltips, and the theme's name (Page Layout › Colors) | the current Office theme |
| 24 | A1 bold with a red Fill and a thick bottom edge; A2 plain. Select A1:A2 (Focus A1), Ctrl+1 | How the Font, Fill and Border tabs show the parts that differ | greyed or empty: record |
| 25 | A1 bold, A2 italic. Select A1:A2, Ctrl+1, set only the Font colour to red on the Font tab, OK | A1's and A2's bold, italic and colour | only the colour changed |
| 26 | Ctrl+1, then Ctrl+Tab and Ctrl+PageDown | Which tab each moves to | the next tab |

## Part B — the browsers' own keys

Write `verification/<date>-windows-browser-11/keys.html`, a static page. It holds one focusable
`div` with a **capture-phase** `keydown` listener. For every key with Ctrl, the listener calls
`preventDefault()` and logs `key`, `code`, `ctrlKey`, `shiftKey` and `altKey` to a list on the page.
Serve the page from WSL (`python3 -m http.server`).

Open the page in Chrome, then in Edge, each with **six other tabs open** and the page as the third
tab. Click the `div`, and send each key below with real input. After each key, wait 500 ms, then
record three things:
- whether the page logged it;
- whether the browser acted: another tab selected, a view-source tab, a bookmark dialog, or anything
  else;
- a screenshot.

Close anything the browser opened before sending the next key.

| # | Keys | Reading |
|---|---|---|
| 27 | Ctrl+1, Ctrl+2, Ctrl+3, Ctrl+4, Ctrl+5 | the page receives each; no tab switch |
| 28 | Ctrl+B, Ctrl+I, Ctrl+U | the page receives each; Ctrl+U opens no source |
| 29 | Ctrl+Shift with `~`, `!`, `@`, `#`, `$`, `%`, `^`, `&` and `_`, typed as characters, on the UK layout, then on the Japanese layout | the page receives each; record `key` and `code` |
| 30 | Ctrl+Tab and Ctrl+PageDown | the browser switches tabs; the page receives neither |

## Part C — ExSheet beside Excel (after tickets 48, 49 and 51)

*(Written 2026-10-01, once tickets 48, 49 and 51 were in.)* ExSheet is put beside Part A's Excel
screenshots, case by case. **Decide nothing. Record everything.**

### Setup

- **Branch and host.** Fetch `claude/exsheet-cell-format`, branch `claude/exsheet-windows-verify-11c`
  from its tip, and record the tip.
  - Run the DemoHost as the ninth run's Part B ran it (`verification/2026-10-01-windows-9/`), and the
    thirteenth run's Part B after it: from WSL with nix, the WebAssembly host
    (`samples/ExGrid.DemoHost`) and the Server host (`samples/ExGrid.DemoHost.Server`), opened from
    Windows' Chrome and Edge.
- **The pages.** `/sheet?case=<name>` opens one of Part A's set-ups instead of the page's own
  Sheet. The names are 1, 2, 3b, 3c, 4–14, 16–18, `12-1` (the twelfth run's case 1) and `12-14`
  (the twelfth run's case 14).
  - The case pages pin no column, as Part A's workbook had no frozen panes.
  - Add `&chrome=mud` for `ExSheet.MudBlazor`'s Chrome, and `&scheme=dark` for the dark scheme.
- **Zoom.** Use the browser at 100%, and at 150% where a case says so. Prefer Windows' display scale
  at 150%, as Part A's 150% screenshots were taken, and record which was used.
- **Pixels.** Read them across each gridline as Part A's group 3 did, from a screenshot of the page, not
  from the DOM.

### Cases

| Part A case | On ExSheet | What to put beside Excel's |
|---|---|---|
| 1, 2, 3b, 3c | `?case=` with the same name | the colour of each value and of the `#` |
| 4, 5, 6 | `?case=4`, `5`, `6` | the gridlines around and between Fills |
| 7 | `?case=7`, then set B2's right edge and C2's left edge by Format Cells (Ctrl+1) as Part A did by COM | the line drawn, after each setting |
| 8, 9 | `?case=8`, `?case=9`, at 100% and at 150% | case 9's table, pixel by pixel, for all thirteen styles; the row heights unchanged (ExSheet keeps one height where Excel raises a row) |
| 10 | `?case=10` | whether B3's Fill covers the line's part inside B3 |
| 11 | `?case=11`, B2:C3 selected with real keys | the Focus and the Selection over the lines, on all four sides |
| 12, 13, 14 | `?case=12`, `13`, `14`, with Part A's keys | the edges after each key |
| `12-1`, `12-14` | `?case=12-1`, `?case=12-14` | the line drawn where both cells record one; a line on column A's left beside the Row Headings |
| 16, 17, 18 | `?case=16`, `17`, `18`, with Part A's keys, as characters on the UK layout | what each key sets, and its undo |
| 22, 24, 25 | `/sheet`, Ctrl+1, under both Chromes | Format Cells' tabs; how cells that differ show; what OK sets |

- **Run every case under Chrome** on the WebAssembly host. Run cases 8, 9, 11 and 16–18 under Edge and on
  the Server host too.
- **Run cases 1, 4 and 9 again with `&scheme=dark`**: the Paper stays white and the Ink black (SH-39).

### Results

- Write `verification/<date>-windows-excel-11c/beside-excel.md`. Give one row per case: Excel's
  screenshot, ExSheet's, and **same** or **different**, saying what differs. Push it on
  `claude/exsheet-windows-verify-11c`.
- **List every difference.** Mark each one that ADR-0071 already records as deliberate: one row height,
  and Ctrl+1 during an edit.

## Results

- Push `claude/exsheet-windows-verify-11` with `verification/<date>-windows-excel-11/` and
  `verification/<date>-windows-browser-11/`.
- Summarise in `cell-format.md`: every case whose answer differs from its reading, and every case
  marked "record".
