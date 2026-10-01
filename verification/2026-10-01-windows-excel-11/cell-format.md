# Windows, eleventh run, Part A: a Cell Format, asked of Excel

[`docs/specs/exsheet/verify-on-windows-11.md`](../../docs/specs/exsheet/verify-on-windows-11.md),
Part A, cases 1 to 26 with 3b and 3c. These cases ask about the readings of
[ADR-0063](../../docs/adr/0063-a-sheets-cell-format-is-document-data-painted-on-white-paper.md).
Part B, the browsers' own keys (cases 27 to 30), is in
[`../2026-10-01-windows-browser-11/keys.md`](../2026-10-01-windows-browser-11/keys.md), and its
summary is repeated here. Part C waits for tickets 48, 49 and 51.

- **Verified commit:** `76d3866c3233fc76b79956f157c2e3f7ee18a14a`. It is the tip of
  `claude/exsheet-cell-format` ("ADR-0063: CellFormatAt answers while an edit is open; ticket 52 skips
  an empty OK"). It was recorded on the branch `claude/exsheet-windows-verify-11`, which was cut from
  that tip.
- **When:** 2026-10-01. Excel ran from 02:07 to 02:47, local time; the browsers ran from 02:51 to 02:55.
- **Nothing was decided.** No ADR, `CONTEXT.md` or `docs/definition-of-done.md` was changed.

**Files.**

| File | Contents |
|---|---|
| `cell-format.ps1` | The script. It takes one case or a list of cases, a pass name and a zoom |
| `cell-format.jsonl` | One line for each case and pass: the set-up, then for each state the keys sent, what COM read and what the pixels say. The jsonl line numbers given below are 1-based |
| `shots/` | For each state, a picture of A1 to about H16 with the headings (`<case>-<state>.png`). There is a cut of each dialog or list of Excel's (`…-window-<n>.png`), and the crops a case names, enlarged two or four times (`…-x2.png`, `…-x4.png`) |

The whole-screen pictures stay on the machine. They are in `%LOCALAPPDATA%\exgrid-layer3\cell-format-11\`.

## Summary

### Where Excel's answer differs from the reading

There is one, **case 15**, an outline over a whole column.

- Ctrl+Shift+& on column B recorded only its left and right edges.
- `B1` has no top edge, and `B1048576` has no bottom edge.
- `Columns(2).Borders` reads left and right as `Continuous Thin`, top and bottom as `None`, and
  inside-horizontal as `None`.
- At the last row, the picture cannot settle it: the Selection's outline lies over that edge.
- The reading was the one ticket 45 took: left and right recorded at column level, with the top of
  row 1 and the bottom of row 1048576 recorded on those cells.

Every other reading matched what Excel did. The detail is in the tables below.

### Cases marked "record", and what was recorded

- **Case 7, one edge set from each side.**
  - Setting B2's right edge (thick red) also made C2's left edge read the same through COM.
  - Setting C2's left edge (thin blue) then made B2's right edge read thin blue.
  - Only the later line is drawn: blue on the gridline's pixel, with no red left.
  - The thick red line had covered the gridline's pixel and one pixel on each side.
- **Case 12, a row inserted under a bordered row.**
  - The new row 3 took B2's Fill (yellow).
  - B3's top edge reads `Continuous Thick`. This is B2's bottom edge, the edge the two cells share.
  - B3's bottom edge reads `None`. B4's top edge (the old row 3) reads `None`.
  - B2's thin top edge was not repeated on the new row.
  - The row heights: row 2 and the new row 3 are 15 pt. Row 4 (the old row 3) is 14.5 pt; it was
    15 pt before the insert.
- **Case 13, the cells beside the range.**
  - After Ctrl+Shift+&, A2:A4 read a right edge, E2:E4 a left edge, B1:D1 a bottom edge and B5:D5 a
    top edge. Each is `Continuous Thin #000000`, the same edge as the outline, read from the other
    side.
  - After Ctrl+Shift+_, every one of them reads `None`, as does every edge of B2:D4.
- **Case 19, by character or by key position.**
  - On the English (UK) layout, **Excel answers by the character the keys type, not by the US
    position**:
    - VK `OEM_3` with Ctrl+Shift types `@` on UK. It applied the time format, as Ctrl+Shift+@ does.
    - VK `3` with Ctrl+Shift types `£`. It changed nothing.
    - VK `2` with Ctrl+Shift types `"`. It opened an edit of A1, with its text selected, and Escape
      cancelled it.
  - Ctrl+Shift+& and _ are the same keys both ways on UK. They set and cleared A1's outline.
  - **This machine's Japanese layout cannot tell the two ways apart.**
    - The Japanese IME runs over the English 101-key arrangement
      (`HKLM\…\i8042prt\Parameters`, `LayerDriver JPN` = `kbd101.dll`).
    - So on it, the character and the US position are the same key.
    - Excel's window was on HKL `0x04110411` with the IME closed.
    - Every key applied Excel's usual format there: General, `#,##0.00`, `h:mm`, `d-mmm-yy`,
      currency, `0%`, `0.00E+00`.
    - Japanese 106/109-key positions would need that setting changed and a restart. That was not
      done.
- **Case 20, the formats each key applies under other regional formats.** This was possible without
  signing out: `Set-Culture`, then a new Excel. The column holds `NumberFormat` / `NumberFormatLocal`,
  then the text A1 = 1234.5 showed.

  | Key | en-GB (case 18) | en-US | ja-JP |
  |---|---|---|---|
  | Ctrl+Shift+~ | `General` / `General`: `1234.5` | `General`: `1234.5` | `General` / `G/標準`: `1234.5` |
  | Ctrl+Shift+! | `#,##0.00`: `1,234.50` | `#,##0.00`: `1,234.50` | `#,##0.00`: `1,234.50` |
  | Ctrl+Shift+@ | `h:mm` / `hh:mm`: `12:00` | `h:mm AM/PM`: `12:00 PM` | `h:mm`: `12:00` |
  | Ctrl+# (UK; no Shift there) | `d-mmm-yy` / `dd-mmm-yy`: `18-May-03` | `d-mmm-yy`: `18-May-03` | `d-mmm-yy` / `dd-mmm-yy`: `18-5-03` |
  | Ctrl+Shift+$ | `$#,##0.00_);[Red]($#,##0.00)` / `£#,##0.00;[Red]-£#,##0.00`: `£1,234.50` | `$#,##0.00_);[Red]($#,##0.00)`: `########` at the standard width | `$#,##0_);[Red]($#,##0)` / `¥#,##0;[Red]-¥#,##0`: `¥1,235` |
  | Ctrl+Shift+% | `0%`: `123450%` | `0%`: `123450%` | `0%`: `123450%` |
  | Ctrl+Shift+^ | `0.00E+00`: `1.23E+03` | `0.00E+00`: `1.23E+03` | `0.00E+00`: `1.23E+03` |

  Excel's `International` values were as follows:
  - en-US: country 1, `$`, date order 0.
  - ja-JP: country 81, `¥`, date order 2.
  - en-GB: country 44, `£`, date order 1.

  The culture was set back to en-GB afterwards. `HKCU\Control Panel\International` then matched the
  export taken before the run, line for line.
- **Case 21, the `$` key during an edit.** F2 on A1 (`abc`), then `b` was selected. Ctrl+Shift+$
  changed nothing: the text stayed `abc` with `b` selected, and the cell showed no change.
- **Case 24, how Format Cells shows parts that differ.** A1 is bold, with a red Fill and a thick
  bottom edge. A2 is plain. The selection was A1:A2 with the Focus on A1.
  - **Font tab.**
    - The *Font style* box is empty and its list has nothing selected.
    - The *Normal font* box is drawn in its mixed state, a filled square. UI Automation reads it as
      `On`.
    - Font, Size, Underline, Colour and Effects show the values the two cells share.
    - The preview is not bold.
  - **Fill tab.** *No Colour* is highlighted (UI Automation: selected) and the Sample is empty.
  - **Border tab.**
    - The preview shows no outer line.
    - It draws the inside horizontal line (A1's thick bottom, between A1 and A2) as a grey dotted
      line.
    - The Horizontal button reads `On`. *Inside* is enabled.
- **Case 3 (no reading; "for a later step").** This en-GB Excel refuses `[Color10]0`. It takes
  `[Colour10]0`, and draws that `#008000`.
  - "Unable to set the NumberFormat property": the refusal came the same way through
    `NumberFormat`, passed in en-US, and through `NumberFormatLocal`.
  - `[COLOR10]0`, `[Color 10]0`, `0;[Color10]-0` and `[Color3]0` were refused too. An earlier run's
    Format Cells had already refused `[Color3]0` and `0;[Color10]-0`, typed as Custom codes.
  - `[Colour10]0` reads back as `[Colour10]0` through both properties.
- **Case 9 ("—", reference crops).** Every line, at 100% and at 150%, is in the table under group 3.
- **Part B, case 29.** Chrome and Edge each received every Ctrl+Shift character. The `key` and `code`
  they logged are listed in
  [`keys.md`](../2026-10-01-windows-browser-11/keys.md#case-29-ctrlshift-with-each-character).
  - On UK, `~` is `key=~ code=Backslash`, `@` is `code=Quote`, and `#` comes with `shiftKey=false`.
  - On the Japanese layout (101 arrangement), `~` is `code=Backquote`, `@` is `code=Digit2` and `#`
    is `code=Digit3`.
  - Cases 27, 28 and 30 matched their readings in both browsers.

### Seen beside the readings

These answers were not asked for.

- **Setting heavier edges made Excel raise row heights.** None of these rows had been set by hand,
  and every one was 14.5 pt before.
  - In case 8 (and case 10), B2's thick bottom edge raised rows 2 and 3 to 15 pt.
  - In case 9, the edges lie under rows 2 to 14, in the procedure's order: hair, thin, medium, thick,
    double, dotted, dashed, dash-dot, dash-dot-dot, medium dashed, medium dash-dot, medium
    dash-dot-dot, slanted dash-dot.
    - After they were set, rows 4, 5, 7, 11, 12, 13 and 14 were 15 pt, and row 6 was 15.5 pt.
    - Rows 2, 3, 8, 9, 10 and 15 stayed 14.5 pt.
    - Every edge of Weight Medium or Thick had a raised row above it, below it, or both. Those are
      medium, thick, double, the three medium dashes and slanted dash-dot. Below slanted dash-dot,
      row 15 was not raised.
    - No row was raised that had only 1-px edges beside it.
  - The same heights came at 150% zoom.
- **Excel's drawn tints are not quite the Fill tab's own RGB names.** The ribbon list and the Format
  Cells picker draw the same 70 colours.
  - Format Cells' Fill tab names its swatches with RGB values.
  - 38 of the 50 tints are drawn 1 to 3 away from the RGB value they are named with, in some channel.
    For example, `#DBE9F7` is drawn for "RGB(218, 233, 248)".
  - Two more tints are named "Grey" rather than with an RGB value. Both are drawn `#7F7F7F`.
  - The theme row's eight RGB names match the drawn colours. Its other two are "White" and "Black".
    The standard colours are named, not given as RGB.
- **Opening the ribbon's Font Colour list showed a teaching callout the first time it was opened that
  night**, in a look at the list's UI Automation before case 23 was written. The callout said "Making
  content more readable" and stood beside a *High-contrast only* switch. It was not clicked. It did
  not show in case 23's runs. Office counted it:
  `HKCU\…\Office\16.0\Common` `AccessibleColorsCategoryTeachingCalloutV2` went from 1 to 2.
- **Selecting A1:A2 put Excel's Quick Analysis button on screen** (in case 17). It is a 33 × 33 px
  window of class `NUIDialog`, at the selection's bottom right, and it shows in that case's pictures.

## Environment

| | |
|---|---|
| Excel | Microsoft 365, Version 2609 (Build 20430.20092 Click-to-Run), Current Channel, x64; `Application.Build` 20430. English (UK) interface |
| Office Theme | "Use system setting" (File › Account, read through UI Automation; `UI Theme` = 6 in the registry) |
| Windows | Light mode (apps and system); high contrast off; display scale **150%** (144 DPI); work area 3840 × 2088 |
| Regional format | en-GB (`Get-Culture`; `LocaleName` en-GB, `dd/MM/yyyy`, `HH:mm:ss`, `£`); interface en-GB. Case 20 set en-US and ja-JP, then en-GB again |
| Excel's standard font | Aptos Narrow, 11 |
| Keyboard | Excel's window was on English (UK), HKL `0x08090809`, at the start of every case, and was put back to it at the end of every run. The IME was closed (`open=0`). Cases 18j and 19j used the Japanese layout, HKL `0x04110411`, with the IME closed (`open=0`, conversion `0x19`). That layout runs over `kbd101.dll` |
| Languages | en-GB (0809), en-US (0409), ja (Microsoft IME) |
| Zoom | 100%. Cases 8 and 9 were also run at 150% (`-Zoom 150`) |
| Gridlines | Excel draws them `#E0E0E0` (sampled). COM's `GridlineColor` is Automatic |

**vkKeyScan** for every character the cases send. Each case's line in the jsonl also holds the ones
it sent.

| Layout | `~` | `!` | `@` | `#` | `$` | `%` | `^` | `&` | `_` | `=` | `b` `i` `u` | `1`–`5` |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| UK `0x0809` | `0x1de` | `0x131` | `0x1c0` | `0x0de` | `0x134` | `0x135` | `0x136` | `0x137` | `0x1bd` | `0x0bb` | `0x042` `0x049` `0x055` | `0x031`–`0x035` |
| Japanese `0x0411` (101 arrangement) | `0x1c0` | `0x131` | `0x132` | `0x133` | `0x134` | `0x135` | `0x136` | `0x137` | `0x1bd` | `0x0bb` | same | same |

On UK, `#` is a key without Shift. So "Ctrl+Shift with `#`, typed as the character" was sent as Ctrl
with `OEM_7` and no Shift, as the character needs. Case 0 typed every character the cases send into
A1 (`'~!@#$%^&_=1234.5abc`), and A1 read it back exactly.

## Method

- **An Excel of the script's own for every case**, started with `New-Object` and checked to be a new
  process. It was ended at the end of the case: by `Quit`, or by `Stop-Process` when Quit had not
  finished in 10 s (most cases).
  - No running Excel was attached to. The user's AutoRecovered and unsaved workbooks were not touched.
  - Each case got a fresh workbook with one sheet, `Sheet1`, maximised, with A1 in view and selected.
- **Keys** went through `SendInput`: a virtual-key and a scan code for each key, the scan code taken
  from the layout of Excel's window.
  - A key named as a character was sent as the key that types it, with the Shift it needs (`VkKeyScanEx`).
  - A key named by position was sent by its virtual-key code.
  - Clicks and drags were the real mouse.
- **COM only set cases up and read results**: values (as Formulas, in en-US), Number Formats (in en-US,
  through `InvokeMember`), Fonts, Fills and Borders.
  - It was not called while an edit or a dialog was open.
  - Places on the screen were read before the edit or dialog opened.
- **Pictures** were taken with `PrintWindow` (`PW_RENDERFULLCONTENT`) for each window of Excel's,
  laid at its place. A copy of the screen comes back empty on this machine.
  - The account's initials in the title bar were blanked.
  - File › Account was read through UI Automation and was not pictured.
- **Colours were sampled from the pictures.**
  - **Text:** each cell's ground is its most frequent pixel. The text's colour is the most frequent
    pixel more than 24 away from the ground; that is the strokes' core. The colours after it are
    ClearType fringes.
  - **Edges:** the pixels across each edge, from 6 (or 8) px before the coordinate
    `PointsToScreenPixels` gives for it to 6 (or 8) px after, at a quarter, a half and three quarters
    along it. They are written as runs of offset and colour (`-3..-1 000000`).
  - **Lines in case 9:** for each row of pixels around the edge, 40 px from 8 px inside the cell,
    written as `#` (luminance below 64), `=`, `-` or `.`.
- **UI Automation** read the Formula Bar, the Format Cells dialog (tabs, lists, check boxes, radio
  buttons, values) and Excel's lists and tooltips.
  - Format Cells' unnamed lists (Font, Font style, Size) were not walked; the Font list holds every
    installed font. Their selected values were read.

**Additions to the procedure.** Each is marked in the record.

- **Cases 18, 19, 18j, 19j and 20 used a sentinel.** Before each key, A1's Number Format was set to
  `0.000` and column A to its standard width (COM). A key that changes nothing then shows `0.000`. The
  key Ctrl+Shift+~ sets General, so without the sentinel it could not be told from no change.
- **Case 16** cleared A1's formats (COM `ClearFormats`) before each key's pair of presses.
- **Case 3, pass b** tried the spellings listed above, one per cell.
- **Cases 8, 9 and 10, passes b and c,** were run again for two reasons:
  - Pass a had read the "before" profile at the geometry after the rows had grown. In passes b and
    c, every state reads its own geometry.
  - Pass c adds the gridline beside the edge, in columns A and C, as the reference for "above and
    below".
- **Case 23** reads each swatch's name from the tooltip that hovering it shows. Those swatches are not
  in the list's UI Automation tree, although the Format Cells picker's swatches are.
  - Pass a read the tooltip 800 ms after each move, and half the reads were empty or showed the
    previous swatch.
  - Passes b and c wait for the tooltip placed at the swatch. Pass c also reads the Colours gallery's
    highlight down the right column.

**Runs that stopped.** Each has its own line in the jsonl.

- **Case 3, pass b, first try:** a fault in the script's error read (line 7).
- **Case 19, pass a:** Ctrl+Shift+VK `2` opened an edit (that key types `"` on UK). COM then refused
  the next read (`RPC_E_CALL_REJECTED`; line 32). Pass b reads such a state from the picture and the
  Formula Bar, then presses Escape.
- **Case 0, pass b:** a strict-mode fault reading a registry value that is not there (line 45).
  Pass c is the record.

## Group 1: a Number Format's colour

| # | Set up | Reading | Excel | Pictures |
|---|---|---|---|---|
| 1 | Rows 1–8: A = -5, B = 5, format `[Black]0` … `[Yellow]0`; row 7 (`[White]`) filled black | `#000000`, `#0000FF`, `#00FFFF`, `#00FF00`, `#FF00FF`, `#FF0000`, `#FFFFFF`, `#FFFF00` | **The same, in both columns**: Black `#000000`, Blue `#0000FF`, Cyan `#00FFFF`, Green `#00FF00`, Magenta `#FF00FF`, Red `#FF0000`, White `#FFFFFF` (on `#000000`), Yellow `#FFFF00`. Each is the strokes' core, the most frequent pixel (14–35 px a cell) | `1-set.png`, `1-set-x4.png` |
| 2 | A1 = -5, B1 = 5, `0;[Red]-0`, Font colour blue | A1 red, B1 blue | **A1 `#FF0000`, B1 `#0000FF`**. COM reads both Fonts as `#0000FF` | `2-set-x4.png` |
| 3 | A1 = 5, `[Color10]0` | — | **Refused** ("Unable to set the NumberFormat property"); A1 stays General, black. Pass b: only `[Colour10]0` is taken, and it draws `#008000` (above) | `3-set-x4.png`, `3-b-set-x4.png` |
| 3b | `[Red]0` on `abc`, `TRUE`, `=1/0`; `0;[Red]@` on 5 | none red | **None red**: all four are `#000000`. `#DIV/0!` has Excel's green error triangle | `3b-set-x4.png` |
| 3c | -123456789 in `0;[Red]-0`, column narrowed until `####` | red | **Red**: `#########` at width 9.5, `#FF0000` (at width 10 the digits still showed, also red) | `3c-fits-x4.png`, `3c-hashes-x4.png` |

## Group 2: Fills and gridlines

Pixels across each edge of B2, before and after the Fill. Offsets are from the coordinate
`PointsToScreenPixels` gives, in screen order: top to bottom across a horizontal edge, left to right
across a vertical one. The gridline sits at offset 0 on B2's top edge, -1 on its bottom edge and -2
on its left and right edges. It is 1 px of `#E0E0E0`.

| # | Set up | Reading | Excel | Pictures |
|---|---|---|---|---|
| 4 | B2 yellow | the Fill covers them | **Covers all four.** The gridline's pixel is yellow on each side: top `0..6 ffff00`, bottom `-6..-1 ffff00`, left `-2..6 ffff00`, right `-6..-2 ffff00`. The pixel beyond the gridline is still the neighbour's white | `4-plain-x4.png`, `4-filled-x4.png` |
| 5 | B2 `Interior.Color = 0xFFFFFF` (`Pattern` 1, `ColorIndex` 2) | the gridlines disappear | **Gone.** All four edges are `ffffff` throughout | `5-filled-x4.png` |
| 6 | B2:C2 yellow | yellow throughout | **Yellow throughout** between B2 and C2 (`-6..6 ffff00`) | `6-filled-x4.png` |

## Group 3: Borders

Edges are read through COM as `LineStyle Weight Color`. A side with no line reads `None Thin #000000`.

| # | Set up / keys | Reading | Excel | Pictures |
|---|---|---|---|---|
| 7 | B2 right thick red; read C2 left; C2 left thin blue; read B2 right | record | One edge, read from both cells. The later line is the one drawn (see the summary). Red covered `x-3..x-1`, the gridline (`x-2`) and one pixel on each side; blue covers `x-2` alone | `7-b2-right-red-x4.png`, `7-c2-left-blue-x4.png` |
| 8 | B2 bottom thick black, at 100% and 150% | across both, centred | **Across both, centred: one pixel above the gridline, the gridline, one pixel below**, measured against the gridline at the same boundary in columns A and C. At 100%, the line is at `-3..-1` and the gridline at `-2`. At 150%, the line is at `-4..-2` and the gridline at `-3`. Rows 2 and 3 grew to 15 pt (above) | `8-c-set-x4.png`, `8-z150-c-set-x4.png` |
| 9 | Thirteen bottom edges in B2:B14 | — | The table below | `9-c-set-x2.png`, `9-z150-c-set-x2.png`, `9-c-<style>-x4.png`, `9-z150-c-<style>-x4.png` |
| 10 | B2 bottom thick black; B3 yellow | the line is above the Fill | **Above.** The pixel below the gridline, inside B3, is black (`-3..-1 000000`), and B3's yellow starts after it (`0..8 ffff00`) | `10-c-set-x4.png` |
| 11 | B2:C3 every edge thin black; B2:C3 selected with Down, Right, Shift+Right, Shift+Down | the Selection is drawn above | **Above.** The Selection's outline is 2 px of `#217346`, with a white line inside it. It covers the outer borders: the black top line of B2 at `0` became `-1..0 217346`. Inside, the borders stay black over the shade (`#C7C7C7` in C2, B3 and C3; the Focus cell B2 stays white) | `11-b-bordered-x4.png`, `11-b-selected-x4.png` |
| 12 | B2 yellow, thin top, thick bottom; A3, Shift+Space (selection `3:3`), Ctrl+Shift+= | Row 3 takes B2's Fill; borders: record | **B3 is yellow.** The borders are in the summary | `12-b-set-x4.png`, `12-b-inserted-x4.png` |
| 13 | B2:D4 by keys; Ctrl+Shift+&, then Ctrl+Shift+_ | &: outer edges only; _: all cleared; neighbours: record | **&:** the outer edges of B2:D4 only. No inside edge was set. **_:** every edge of B2:D4 is `None`. The neighbours are in the summary | `13-b-amp-x2.png`, `13-b-underscore-x2.png` |
| 14 | B2:C3 by keys, E5:F6 by a drag with Ctrl held (selection `B2:C3,E5:F6`, Focus E5); Ctrl+Shift+& | yes | **Yes.** Each range has its own outline, with no edge between B2:C3 and E5:F6 and none inside either | `14-b-amp-x2.png` |
| 15 | Column B by Right, Ctrl+Space (selection `B:B`); Ctrl+Shift+& | recorded at column level | **Differs** (the summary). Left and right only: `B1`, `B2`, `B5`, `B1048575` and `B1048576` read left and right `Continuous Thin`, with top and bottom `None` | `15-b-amp-x2.png`, `15-b-bottom-x2.png` |

**Case 9, the thirteen styles.** The pass c rows are listed for each zoom. Each line is at the bottom
of its cell. The **gridline** column gives the reference: the gridline at the same boundary in
column A.

Patterns are 40 px long, starting 8 px inside the cell; `#` is a dark pixel and `.` a light one. A
row the line does not touch is left out. All read `#000000` (Automatic) through COM, and COM read
back the style and weight that were set.

| Style (`LineStyle`, `Weight`) | 100%: rows (gridline) and pattern | 150%: rows (gridline) and pattern |
|---|---|---|
| hair (Continuous, Hairline) | `-1` (gridline `-1`): `.#.#.#.#…` | `-1` (`-1`): `#.#.#.#.…` |
| thin (Continuous, Thin) | `-2` (`-2`): solid | `-3` (`-3`): solid |
| medium (Continuous, Medium) | `-2`, `-1` (`-1`): solid, the gridline and 1 px above | `-1`, `0` (`0`): solid, the gridline and 1 px above |
| thick (Continuous, Thick) | `-3..-1` (`-2`): solid, centred | `-4..-2` (`-3`): solid, centred |
| double (Double, Thick) | `-2` and `0` (`-1`): two 1-px lines with the gridline's row white between them | `-2` and `0` (`-1`): the same |
| dotted (Dot, Thin) | `-1` (`-1`): `..##..##` (2 on, 2 off) | `-1` (`-1`): `.##..##.` |
| dashed (Dash, Thin) | `-1` (`-1`): `#.###.###` (3 on, 1 off) | `1` (`1`): `#.###.###` |
| dash-dot (DashDot, Thin) | `-2` (`-2`): `########...###...` (8, 3, 3, 3) | `-2` (`-2`): `..#########...###...` (9, 3, 3, 3) |
| dash-dot-dot (DashDotDot, Thin) | `-3` (`-3`): `########...###...###...` | `-3` (`-3`): `..#########...###...###.` |
| medium dashed (Dash, Medium) | `-1`, `0` (`0`): `########...#########...` (8 on, 3 off) | `-1`, `0` (`0`): `..#########...#########.` (9 on, 3 off) |
| medium dash-dot (DashDot, Medium) | `-4`, `-3` (`-3`): `########...###...` | `-4`, `-3` (`-3`): `..#########...###...` |
| medium dash-dot-dot (DashDotDot, Medium) | `-1`, `0` (`0`): `########...###...###...` | `-1`, `0` (`0`): `..#########...###...###.` |
| slanted dash-dot (SlantDashDot, Medium) | `-4`, `-3` (`-3`): two rows offset, `###########.#####.` over `#########..####..` | `-4`, `-3` (`-3`): `##.###########.#####.` over `..##########..####..` |

At both zooms, every 1-px style lies on the gridline. Every 2-px style takes the gridline and the
pixel above it. Thick takes the gridline and a pixel on each side. Double puts its two lines on
either side of the gridline. The dashes are longer at 150% (9 px against 8), and the dots and gaps
stay 3 px. In the 150% table, offsets count from a different rounding of the cell's coordinate,
which is why its gridline offsets are not the 100% ones.

## Group 4: keys

A1 = `abc` and A2 = `def`, with no formatting, before each case. Cases 18 to 20 put 1234.5 in A1.

| # | Keys | Reading | Excel | Pictures |
|---|---|---|---|---|
| 16 | Ctrl+B, Ctrl+2, Ctrl+I, Ctrl+3, Ctrl+U, Ctrl+4, Ctrl+5, each twice | each toggles; underline single | **Each toggles.** B and 2 bold, I and 3 italic, U and 4 underline (`Single`, 2), 5 strikethrough. The second press of each took it off. `NumberFormat` stayed General | `16-ctrl-<key>-<n>-x4.png` |
| 17 | A1 bold; A1, Shift+Down, Ctrl+B; then A1 bold again, A2, Shift+Up, Ctrl+B | the Focus decides | **The Focus decides.** Focus A1 (bold): both became plain. Focus A2 (plain): both became bold | `17-focus-a1-ctrl-b-x4.png`, `17-focus-a2-ctrl-b-x4.png` |
| 18 | Ctrl with ~ ! @ # $ % ^ as characters, UK | General, `#,##0.00`, a time, a date, a currency, `0%`, `0.00E+00` | **As read**: General, `#,##0.00`, `h:mm` (local `hh:mm`), `d-mmm-yy` (local `dd-mmm-yy`), `$#,##0.00_);[Red]($#,##0.00)` (local `£#,##0.00;[Red]-£#,##0.00`, shown `£1,234.50`), `0%`, `0.00E+00`. Ctrl+# is Ctrl+`OEM_7` with no Shift on UK; Column A widened to 8.73 for the date | `18-uk-char-<name>-x2.png` |
| 19 | As 18 by US position; & and _ both ways; then 18 and 19 on the Japanese layout | record | By character (the summary) | `19-b-uk-vk-<name>-x2.png`, `18j-…`, `19j-…` |
| 20 | 18 under en-US and ja-JP | record | The summary's table | `20-<culture>-char-<name>-x2.png` |
| 21 | F2 on A1, Left, Shift+Left (`b` selected), Ctrl+B, Ctrl+Shift+$, Ctrl+1, Esc, Esc | Ctrl+B bolds `b` alone; `$`: record; Ctrl+1 opens a Font-only dialog | **Ctrl+B bolded `b` alone.** The edit stayed open with `b` selected; the picture shows `b` heavier; the dialog then read *Font style* `Bold`. **`$` changed nothing.** **Ctrl+1 opened Format Cells with one tab, Font**, showing *Normal font* off. Esc closed it, and the edit was still open with `b` selected. Esc cancelled the edit: A1 = `abc`, not bold, and its `b` not bold | `21-ctrl-b-x4.png`, `21-ctrl-shift-dollar-x4.png`, `21-ctrl-1-window-1.png` |

## Group 5: Format Cells and the palette

| # | Set up / keys | Reading | Excel | Pictures |
|---|---|---|---|---|
| 22 | A fresh Excel; Ctrl+1 on A1; each tab clicked in order; Esc; Ctrl+1 | Number, Alignment, Font, Border, Fill, Protection | **Tabs, in order:** Number, Alignment, Font, Border, Fill, Protection. **First on a fresh Excel:** Number. **Second Ctrl+1**, after Protection was the last tab shown and Esc closed the dialog: **Protection**. Case 26's fresh Excel opened on Number again | `22-<state>-window-1.png` |
| 23 | Home › Font Colour, opened by a click on its arrow; each swatch hovered; Page Layout › Colours | the current Office theme | **The palette below. The theme is "Office"**: it is the highlighted entry (ground `#EBEBEB`) at the top of Page Layout › Colours | `23-c-palette-x2.png`, `23-c-theme-colours.png` |
| 24 | A1 bold, red Fill, thick bottom; A2 plain; A1:A2 (Focus A1), Ctrl+1 | record | The summary | `24-tab-font-window-1.png`, `24-tab-fill-window-1.png`, `24-tab-border-window-1.png` |
| 25 | A1 bold, A2 italic; A1:A2, Ctrl+1, Font › Colour › Red, OK | only the colour changed | **Only the colour.** A1 is bold and red (`#FF0000`); A2 is italic and red. The dialog's *Colour* read `Colour Red` before OK | `25-red-chosen-window-1.png`, `25-after-ok-x4.png` |
| 26 | Ctrl+1, Ctrl+Tab, Ctrl+PageDown | the next tab | **The next tab each time:** Number → Alignment → Font | `26-<state>-window-1.png` |

**Case 22's details.**

- **The Number tab's categories:** General, Number, Currency, Accounting, Date, Time, Percentage,
  Fraction, Scientific, Text, Special, Custom.
- **The Border tab's line styles,** in UI Automation's order, two columns of seven:
  - None, Hair, Dotted, Dash-dot-dot, Dash-dot, Dashed, Thin.
  - Medium Dash-dot-dot, Slanted Dash-dot, Medium Dash-dot, Medium Dashed, Medium, Thick, Double.
  - Thin is selected when the dialog opens.
- **Its presets:** None, Outline and Inside. Inside is disabled for one cell.
- **Its edge buttons:** Top, Horizontal, Bottom, Diagonal Up, Left, Vertical, Right, Diagonal Down.
  Horizontal and Vertical are disabled for one cell.
- **The Fill tab's palette:** *No Colour*, then the same 60 theme colours and 10 standard colours,
  named with RGB values where they are tints. Then *Fill Effects…* and *More Colours…*.
- **The Font tab's Colour box** opens a picker: Automatic, the 60 theme colours and the 10 standard
  colours by name, then *More Colours…*. Its swatches sample to the same hex as the ribbon's.

**Case 23, the Font Colour list.** Each swatch shows its colour sampled from the picture (11 × 11 px
at its middle, every one uniform) and the name its tooltip gave.

- Tooltips also said "Good contrast" or "Low contrast".
- Above the grid are *High-contrast only* and *Automatic*. Automatic is drawn as a black square.
- Below it are *More Colours…* and, under Theme Colours, the tints in five rows.

| | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9 | 10 |
|---|---|---|---|---|---|---|---|---|---|---|
| theme | `#FFFFFF` White, Background 1 | `#000000` Black, Text 1 | `#E8E8E8` Light Grey, Background 2 | `#0E2841` Dark Blue, Text 2 | `#156082` Dark Teal, Accent 1 | `#E97132` Orange, Accent 2 | `#196B24` Dark Green, Accent 3 | `#0F9ED5` Turquoise, Accent 4 | `#A02B93` Plum, Accent 5 | `#4EA72E` Green, Accent 6 |
| tint 1 | `#F2F2F2` …Darker 5% | `#7F7F7F` …Lighter 50% | `#D0D0D0` …Darker 10% | `#DBE9F7` …Lighter 90% | `#C1E4F5` …Lighter 80% | `#FAE2D6` …Lighter 80% | `#C1F0C8` …Lighter 80% | `#CAEDFB` …Lighter 80% | `#F1CEEE` …Lighter 80% | `#D9F2D0` …Lighter 80% |
| tint 2 | `#D8D8D8` …Darker 15% | `#595959` …Lighter 35% | `#AEAEAE` …Darker 25% | `#A6C9EB` …Lighter 75% | `#83CAEB` …Lighter 60% | `#F6C6AC` …Lighter 60% | `#84E291` …Lighter 60% | `#95DCF7` …Lighter 60% | `#E49EDD` …Lighter 60% | `#B3E5A1` …Lighter 60% |
| tint 3 | `#BFBFBF` …Darker 25% | `#3F3F3F` …Lighter 25% | `#747474` …Darker 50% | `#4D94D8` …Lighter 50% | `#45B0E1` …Lighter 40% | `#F1A984` …Lighter 40% | `#47D45A` …Lighter 40% | `#60CBF3` …Lighter 40% | `#D76DCC` …Lighter 40% | `#8ED873` …Lighter 40% |
| tint 4 | `#A5A5A5` …Darker 35% | `#262626` …Lighter 15% | `#3A3A3A` …Darker 75% | `#215E99` …Lighter 25% | `#0F4861` …Darker 25% | `#BF4F14` …Darker 25% | `#12501B` …Darker 25% | `#0B769F` …Darker 25% | `#78206E` …Darker 25% | `#3A7D22` …Darker 25% |
| tint 5 | `#7F7F7F` …Darker 50% | `#0C0C0C` …Lighter 5% | `#171717` …Darker 90% | `#153D64` …Lighter 10% | `#0A3041` …Darker 50% | `#7F340D` …Darker 50% | `#0C3512` …Darker 50% | `#074F6A` …Darker 50% | `#501549` …Darker 50% | `#265316` …Darker 50% |
| standard | `#C00000` Dark Red | `#FF0000` Red | `#FFC000` Orange | `#FFFF00` Yellow | `#92D050` Light Green | `#00B050` Green | `#00B0F0` Light Blue | `#0070C0` Blue | `#002060` Dark Blue | `#7030A0` Purple |

In the tint rows, "…" stands for the base colour's name in the same column. For example, column 4,
tint 1 is "Dark Blue, Text 2, Lighter 90%". The full names are in the jsonl (line 43, state
`hovered`).

## The machine afterwards

- **Excel:** no Excel process is running. Every case's Excel was the script's own.
- **AutoRecover:** `%APPDATA%\Microsoft\Excel` is unchanged. Its three files (`Book1 (version 1).xlsb`,
  the unsaved `Book1((Unsaved-…)).xlsb` and `Excel15.xlb`) hash the same as the copy taken at the
  start.
- **Regional format:** en-GB. `HKCU\Control Panel\International` and `HKCU\Keyboard Layout` match the
  exports taken at the start, line for line.
- **Office's registry changes.** These are Office's own records of the run, and none was set back:
  - `HKCU\Software\Microsoft\Office\16.0\Excel`, 12 lines: a session ID, an add-in load time and a
    dirty-workbook sentinel.
  - `…\Common`, 86 lines: session IDs, sync times and template-catalogue entries, a web-service cache
    for en-US and ja-JP (written when case 20's Excels started), and the teaching callout's count
    above.
- **Keyboard:** the window in front was on English (UK) at the end (`0x08090809`).
