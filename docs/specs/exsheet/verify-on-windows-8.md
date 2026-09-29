# What to verify on Windows, eighth run

Status: ready-for-human — **Part A now; Part B once tickets 27–30 are done.**

For the Claude Code session on the Windows desktop of the earlier runs (Excel, Chrome, Edge, WSL2 with
nix). Read [`verify-on-windows-5.md`](verify-on-windows-5.md) and
[`verify-on-windows-4.md`](verify-on-windows-4.md) first: their method and tools still apply. **Decide
nothing. Record everything.** Do not change any ADR, `CONTEXT.md` or `docs/definition-of-done.md`.

Part A asks Excel only. It needs no build and does not wait for the implementation. Its answers settle
the readings in [ADR-0057](../../adr/0057-references-are-outlined-in-colour-while-a-formula-is-edited.md)
before tickets 27–30 are finished.

## Setup

- Fetch **`claude/exsheet-reference-outlines`**. This procedure lives there until that branch is merged.
  Branch **`claude/exsheet-windows-verify-8`** from its tip, and record the tip as the verified commit.
  If the tip moves during the run, do not merge it in.
- **The user has authorised this run in advance**: real keys and mouse to Excel, and, for Part B, to
  the browsers. **Do not stop to ask.** Say "starting" before the first input and "finished" after the
  last.
- Drive Excel through `verification/2026-09-27-windows-excel/excel-driver.ps1` (`Connect-Excel`,
  `Reset-Book`, `Show-Excel`, `Send-Keys`, `Get-CellRect`, `Click-At`, `Save-Shot`), as
  `verification/2026-09-28-windows-excel-3/by-hand.ps1` does. Write this run's script as
  `verification/<date>-windows-excel-8/range-finder.ps1`, taking one case or a list (`-Case 1,2`).
- **Every Formula is typed with real keys**, never written through COM: what is asked is what Excel
  draws while an edit is open. COM may only set up a case: a sheet name, a Table, a cell's content
  before the edit. In `SendKeys`, brace `+ ^ % ~ ( ) [ ] { }` (`{+}`, `{(}`, `{[}` and so on).
- **Excel's window must receive plain keys.** The Japanese IME must be off (direct input) for Excel's
  window, or `=A1` is composed instead of typed. Switch Excel's window to the English (UK) keyboard
  for the run, as the fourth run did for Ctrl+Space, and back after.
- Record the Excel version and build (File › Account), the Office Theme (File › Account › Office
  Theme), Windows' light or dark mode, and the display scale. The colours may depend on each.

## Part A — Excel's range finder (ADR-0057, "Readings")

A fresh workbook for each case, with one sheet named `Sheet1`, at 100% zoom, with A1 in view. Select
**D10** and type the case's keys. **Do not press Enter.** With the edit still open, wait 600 ms, then
record the state as described below, then press Escape until Excel is Ready.

**What to record for each case**, as one JSON line in `range-finder.jsonl` and a row in `range-finder.md`:

1. **A screenshot of the window**, and crops of the Formula Bar, of the in-cell editor at D10, and of
   every outlined cell.
2. **Each outline's colour and extent.** For every cell the case names, take `Get-CellRect`, and sample
   the pixels 1 to 3 px inside each of its four edges. Record the most frequent colour that is neither
   the cell's ground nor the gridline, as hex. From the samples, record which cells an outline
   surrounds, one entry per outline.
3. **Each Reference's text colour**, in the Formula Bar and in the cell. Record the distinct saturated
   colours from left to right along the text, as hex. A pixel counts as saturated when max(R,G,B) −
   min(R,G,B) > 60. Say which characters each colour covers, reading the crop.
4. **Anything else drawn**: a fill inside an outline (sample the cell's centre), small squares at an
   outline's corners, a dashed line, a line that moves (two screenshots 300 ms apart).

| # | Set up (COM) | Keys typed into D10 | What is asked | Reading |
|---|---|---|---|---|
| 1 | — | `=A1+B1+C1+D1+E1+F1+G1+H1+I1+J1` | The colour of each Reference, in order. After how many the colours repeat | eight colours, then round again |
| 2 | — | `=A1+A1` | One colour or two; one outline or two | one colour, one outline |
| 3 | — | `=A1+$A$1` | The same | one colour, one outline |
| 4 | — | `=A1+B1+A1` | The colour of each of the three | A1 first colour, B1 second, the second A1 first |
| 5 | — | `=B2:A1` | The outline's cells | A1:B2 |
| 6 | — | `=A1:B2+B2` | Colours and outlines | two colours, two outlines |
| 7 | — | `=A1+B1`, then F2, `{HOME}{RIGHT}{DEL 3}` (leaving `=B1`) | B1's colour before the deletion and after | first colour after: colours follow first appearance |
| 8 | — | `=Sheet1!A1` | Coloured? Outlined? | both |
| 9 | Add a sheet `Sheet2`, then activate Sheet1 | `=Sheet2!A1+B1` | Is `Sheet2!A1` coloured? B1's colour | `Sheet2!A1` uncoloured, B1 first colour |
| 10 | — | `=SUM(A:A)`; then, as a second case, `=SUM(1:1)` | The outline's extent (sample row 1, row 30 and the last visible row; column A and the last visible column) | the whole column; the whole row |
| 11 | A1:B4 as a Table named `Positions`, headers `Id`, `PV`, numbers below | `=SUM(Positions[PV])` | Coloured? Which cells are outlined: B1 (header), B2:B4 (data), or A1:B4 | coloured; B2:B4 |
| 12 | As 11 | `=SUM(Positions[PV])+SUM(Positions[Id])` | Two colours? | two |
| 13 | — | `=SUM(A1,` | Is A1 coloured and outlined? | yes |
| 14 | — | `=A1+` | The same | yes |
| 15 | — | `="A1"&B1` | Is the `A1` inside the string coloured? | only B1 |
| 16 | — | `=LOG10(A1)` | Is `LOG10` coloured, or cell LOG10 outlined? | only A1 |
| 17 | — | `=a1` | Coloured? | yes |
| 18 | — | `A1` (no `=`) | Anything coloured or outlined? | nothing |
| 19 | — | `=`, then `{DOWN}` (pointing at D11) | The pointed outline: colour, dashed or solid, moving or still | dashed, first colour |
| 20 | — | `=`, `{DOWN}`, `{+}`, `{DOWN}` | The first outline once pointing has moved on, and the new one | the first solid; the new one dashed, second colour |
| 21 | `=A1+B1` written into D10 through COM | Five states, one after another: D10 selected with no edit; F2; Escape, then a double-click on D10; Escape, then a click into the Formula Bar's text | Outlines shown in each state | none when only selected; shown for F2, the double-click and the Formula Bar |
| 22 | — | `=D10` | Is D10, the cell being edited, outlined? | yes |
| 23 | Zoom 400% | Case 1's keys | One outline close up: its width in screen pixels, the fill, the corner squares | solid, a pale fill, corner squares |

For case 21, find the Formula Bar's text box through UI Automation (Excel's edit control above the
column headings), or from a screenshot, and record the point clicked.

Results go to `verification/<date>-windows-excel-8/`: `range-finder.ps1`, `range-finder.jsonl`,
`range-finder.md` (one row per case, with the reading and whether Excel agrees) and `shots/`.

## Part B — ExSheet beside Excel

Only once tickets 27–30 are marked done on `claude/exsheet-reference-outlines`. On their commit,
`/sheet` in Chrome and Edge, both hosts, at 150%. Type each case from Part A into a cell, and again
into the Formula Bar. Record whether ExSheet's colours and outlines agree with Excel's from Part A.
Also:

- on the Server host behind the latency proxy (150 ms), case 1 typed as fast as `by-hand.ps1` sends
  it: whether any frame shows coloured text over the wrong characters (ADR-0057: none should);
- case 11 with the positions grid beside the Sheet: the PV column's outline and its colour;
- with Windows' high contrast on, case 1: what is left (ADR-0057, "Not done");
- any console message.

Results go to `verification/<date>-windows-8/reference-outlines.md`.

## Finishing

Commit everything to `claude/exsheet-windows-verify-8` and push. The last message lists every
disagreement between Excel and a reading, and between ExSheet and Excel, each with the ADR paragraph
or criterion it belongs to. It proposes nothing on the user's behalf.
