# What to verify on Windows, tenth run

Status: done — run on 2026-09-30, groups 1 to 3 at 261666c and group 4 at 632b323
(`verification/2026-09-30-windows-excel-10/`). What it settled is in ADR-0051. *(Set from the
records on 2026-10-10.)*

For the Claude Code session on the Windows desktop of the earlier runs. Read
[`verify-on-windows-9.md`](verify-on-windows-9.md) and [`verify-on-windows-8.md`](verify-on-windows-8.md)
first: their method and tools apply unchanged. **Decide nothing. Record everything.** Do not change any
ADR, `CONTEXT.md` or `docs/definition-of-done.md`.

This run asks Excel what three decisions of 2026-09-30 left as readings or left unobserved:

- **Group 1, completion**: the readings ticket 39 took, recorded in
  [ADR-0058](../../adr/0058-a-formula-points-across-grids-through-a-pointing-scope.md), "Readings, until
  Excel is observed".
- **Group 2, the pointed Reference's shade**: ADR-0057, "What Part B of the eighth Windows run
  settled", and ticket 43. Both are on `claude/exsheet-eighth-run-b` until it is merged. Excel's shade
  is known for the first two colours only.
- **Group 3, the Formula Bar's keys**: ADR-0051's note "An edit in the Formula Bar never enters
  Overwrite" and ticket 42, on the same branch. Excel's bar was seen in Edit (2026-09-27, item 12);
  its keys were not.

## Setup

- Fetch **`claude/exsheet-pointing-scope`**. Branch **`claude/exsheet-windows-verify-10`** from its tip,
  and record the tip as the verified commit.
- **The user has authorised this run in advance**: real keys and mouse to Excel. **Do not stop to
  ask.** Say "starting" before the first input and "finished" after the last.
- Drive Excel as the ninth run did (`verification/2026-09-30-windows-excel-9/pointing.ps1`): an Excel
  of the script's own per case, `SendInput` keys, the real mouse, `PrintWindow` pictures, UI Automation
  for the Formula Bar's text and selection, the status bar's mode and any popup list. Write this run's
  script as `verification/<date>-windows-excel-10/excel-only.ps1`, taking one case or a list.
- The Japanese IME off, English (UK) for Excel's window, as before. Record the Excel version, the
  Office Theme, Windows' mode and the display scale.

**Set up for every case** (COM): a fresh workbook with one sheet, `Sheet1`, at 100% zoom. A1:B4 is a
Table named `Positions` with headers `Id` and `PV`: `R-1`, `R-2`, `R-3` under Id, and `10`, `20`, `30`
under PV. **D10** is selected. Do not press Enter unless the case says so; after the case, Escape
until Excel is Ready.

**For every state, record**: the Formula Bar's text and its selection (the caret is a selection of
length 0), the status bar's mode (Ready, Enter, Edit, Point), the active cell, any list shown under the
edit (every item, in order, and which is selected), and a picture.

## Group 1 — completion

| # | Keys typed into D10 | What is asked | Reading |
|---|---|---|---|
| 1 | `=XLOOKUP(1,A2:A4,B2:B4,,0` | With the caret after the `0`: is a list shown? Its items | no list |
| 2 | `=XLOOKUP(1,A2:A4,B2:B4,,-` | The list and its items | `-1 - …` only |
| 3 | `=XLOOKUP(1,A2:A4,B2:B4,,`, then `{DOWN}`, then `{TAB}` | The item selected after ↓; the text after Tab; is a list still open | `-1` written; no list |
| 4 | `=SUM(Positions[`, then `{DOWN}` until `PV` is selected, then `{TAB}` | The text after Tab (with or without `]`); is a list still open, and its items | `=SUM(Positions[PV`; the list still open on `PV` |
| 5 | `=Posit`, then `{TAB}` | The text after Tab; is a list still open, and its items | `=Positions`; the list still open on `Positions` |
| 6 | `=XLOOKUP(1,A2:A4,B2:B4,,`, then `{RIGHT}` | With the value list open: does → move the caret, point, or choose? | the caret moves (nothing to its right, so it stays) |

## Group 2 — the pointed Reference's shade

The grey ground and the dark shade show on the Reference Point is writing, unless it follows the `=`
directly (ADR-0057). Each case points at the n-th Reference, so that it takes the n-th colour.

| # | Keys typed into D10 | The pointed Reference is the |
|---|---|---|
| 7 | `=1+`, `{DOWN}` | first (reading `#0401a2`, observed in the eighth run) |
| 8 | `=A1+`, `{DOWN}` | second (`#630101`, observed) |
| 9 | `=A1+B1+`, `{DOWN}` | third |
| 10 | `=A1+B1+C1+`, `{DOWN}` | fourth |
| 11 | `=A1+B1+C1+E1+`, `{DOWN}` | fifth |
| 12 | `=A1+B1+C1+E1+F1+`, `{DOWN}` | sixth |
| 13 | `=A1+B1+C1+E1+F1+G1+`, `{DOWN}` | seventh |

For each, record in the cell, **as the eighth run read case 20x**: the ground's colour under the
pointed Reference, and the distinct saturated colours of its text, as hex, with the other References'
text colours beside them. Then repeat cases 7–13 with **Office Theme "Black"** (File › Account), and
record the same. Put the theme back to what it was.

## Group 3 — the Formula Bar's keys

| # | Keys and clicks | What is asked | Reading |
|---|---|---|---|
| 14 | Click into the Formula Bar's empty text, type `=A1+B1`, then `{HOME}` | The mode after typing and after Home; the caret; the active cell; is the edit still open | Edit throughout; the caret at 0; D10 still edited |
| 15 | As 14, then `{RIGHT}{DEL 3}` | The text | `=B1` |
| 16 | As 14 without Home, then `{END}`, `{LEFT}`, `{LEFT}` | The caret after each | at the end, then one and two to the left |
| 17 | As 14 without Home, then `{F2}` | The mode before and after F2 | open |
| 18 | Type `=A1+B1` into D10 (the cell, not the bar), then click into the Formula Bar's text after `B1` | The mode before and after the click; then `{HOME}`: the caret, the active cell | Enter, then Edit; Home moves the caret |
| 19 | As 18, then click back into D10's own text | The mode after the click; then `{LEFT}`: does it move the caret or commit and move the cell | Edit stays; the caret moves |

## Group 4 — F2 in the Formula Bar, and the keys after it *(added 2026-09-30, after groups 1–3)*

Case 17 found that F2 in the Formula Bar takes Edit to **Enter**. What the keys do in the bar after
that decides ADR-0051's rule for F2 there (ticket 42), and was not asked. Run these on the branch's
tip as it is now; commit the results to `claude/exsheet-windows-verify-10` as a second commit.

**Click into the empty Formula Bar and type as one state**, with no reading between the click and
the first key: in groups 1–3, a reading there moved the focus to the Name Box (pass a of 14–17).

| # | Keys and clicks | What is asked |
|---|---|---|
| 20 | Click into the empty Formula Bar, type `=A1+B1`, `{F2}`, then `{HOME}` | The mode after F2 and after Home; the Formula Bar's text and caret; the active cell; is the edit still open, or was the Formula entered (and into which cell) |
| 21 | As 20 without Home, then `{RIGHT}` | The same |
| 22 | As 20 without Home, then `{DOWN}` | The same |
| 23 | As 20 without Home, then `{F2}` again | The mode after the second F2 |
| 24 | Click into the empty Formula Bar, type `=A1+`, `{F2}` | The mode after F2 (Point, Enter or Edit); then `{DOWN}`: the text (is `D11` written?) and the mode |
| 25 | Type `=A1+B1` into D10 (the cell), `{F2}`, `{F2}` | The mode after each F2, in the cell: the cell's own cycle, for comparison |

Record as for groups 1–3, in the same files (`excel-only.md` gains a "Group 4" section, the `.jsonl`
gains the lines).

## Finishing

Commit everything to `claude/exsheet-windows-verify-10` and push. The results go to
`verification/<date>-windows-excel-10/`: the script, a `.jsonl`, `excel-only.md` (one row per case,
with the reading and whether Excel agrees) and `shots/`. The last message lists every disagreement
with a reading, and every shade observed in group 2, each with the ADR paragraph or ticket it belongs
to. It proposes nothing on the user's behalf.
