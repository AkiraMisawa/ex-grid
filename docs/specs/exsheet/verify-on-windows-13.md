# What to verify on Windows, thirteenth run

Status: ready-for-human — **Part A is Excel only and needs no build**; **Part B once tickets 55–58 are
done.**

For the Claude Code session on the Windows desktop of the earlier runs. Read
[`verify-on-windows-9.md`](verify-on-windows-9.md) and [`verify-on-windows-10.md`](verify-on-windows-10.md)
first: their method and tools apply unchanged, and Part B of the ninth run
(`verification/2026-10-01-windows-9/`) is the model for Part B here. **Decide nothing. Record
everything.** Do not change any ADR, `CONTEXT.md` or `docs/definition-of-done.md`.

*(Numbered thirteenth: `verify-on-windows-11.md` and `verify-on-windows-12.md` are the Cell Format's, on
`claude/exsheet-cell-format`. This file was written as the twelfth and renamed on 2026-10-01, when
the run had begun at 6ebc186 with its cases unchanged.)*

Part B of the ninth run left three questions to Excel
([ADR-0058](../../adr/0058-a-formula-points-across-grids-through-a-pointing-scope.md), "What Part B of
the ninth Windows run settled"), and decided three changes that Part B here types into ExSheet.

## Setup

- Fetch **`claude/exsheet-ninth-run-b`**. This procedure lives there until that branch is merged.
  Branch **`claude/exsheet-windows-verify-13`** from its tip, and record the tip as the verified
  commit. If the tip moves during the run, do not merge it in.
- **The user has authorised this run in advance**: real keys and mouse to Excel, and, for Part B, to
  the browsers. **Do not stop to ask.** Say "starting" before the first input and "finished" after the
  last.
- Drive Excel as Part B of the ninth run did (`verification/2026-10-01-windows-9/excel/ask-excel.ps1`):
  an Excel of the script's own per case, real keys, the real mouse, pictures, UI Automation for the
  Formula Bar's text and selection, the status bar's mode and any popup list. Where UI Automation
  gives a list no rows, read them from the picture, as that run did. Write this run's script as
  `verification/<date>-windows-13/excel/ask-excel.ps1`.
- The Japanese IME off, English (UK) for Excel's window. Excel's keyboard check compares case and
  all. Record the Excel version, the Office Theme, Windows' mode and the display scale.

## Part A — Excel

**Set up for every case**, as Part B of the ninth run did: Book1 with the Table `Positions` in A1:B4
(`Id`: `R-1`, `R-2`, `R-3`; `PV`: `10`, `20`, `30`), D10 selected; and, where a case says so, Book2
beside it with the Table `Trades` in the same shape, arranged side by side.

For every state, record the Formula Bar's text and selection, the status bar's mode, the active cell
and workbook, any list under the edit (every item, in order, and which is selected), any tip beside
it, and a picture.

### Group 1 — what text at a value-list argument lists (Q50)

`A` at `match_mode` listed every value, `0 - Exact match` selected; `4` listed nothing. Which rule
gives both is not known.

| # | Keys typed into D10 | What is asked |
|---|---|---|
| 1 | `=XLOOKUP(1,A2:A4,B2:B4,,A1` | A list? Its items and which is selected |
| 2 | `=XLOOKUP(1,A2:A4,B2:B4,,1+` | The same |
| 3 | `=XLOOKUP(1,A2:A4,B2:B4,,X` | The same |
| 4 | `=XLOOKUP(1,A2:A4,B2:B4,,AV` | The same (`AV` begins `AVERAGE`) |
| 5 | `=XLOOKUP(1,A2:A4,B2:B4,,Positions` | The same (a Table's name) |
| 6 | `=XLOOKUP(1,A2:A4,B2:B4,,"` | The same |
| 7 | `=XLOOKUP(1,A2:A4,B2:B4,,0,A` | At `search_mode`: the same |
| 8 | `=XLOOKUP(1,A2:A4,B2:B4,,0,5` | At `search_mode`, a number that is no value: the same |
| 9 | `=SUM(A` | Outside a value-list argument, for comparison: the list |

### Group 2 — the caret inside a value (Q49)

| # | Keys | What is asked |
|---|---|---|
| 10 | `=XLOOKUP(1,A2:A4,B2:B4,,-1)`, `{F2}`, `{LEFT}{LEFT}` (the caret between `-` and `1`) | A list? Then `{TAB}`: what is written, and is the Formula entered |
| 11 | `=XLOOKUP(1,A2:A4,B2:B4,,10)`, `{F2}`, `{LEFT}{LEFT}` (the caret between `1` and `0`) | The same |
| 11a | `=XLOOKUP(1,A2:A4,B2:B4,,  )`, `{F2}`, `{LEFT}{LEFT}{LEFT}` (the caret before the two spaces) | The same. ExSheet counts spaces before `,` or `)` as nothing of the argument and lists (ticket 55's Comments); Excel was not asked |

### Group 3 — the arrow keys straight after a column heading in another workbook (Q52)

Book2 beside Book1, as in Part B of the ninth run.

| # | Keys and clicks | What is asked |
|---|---|---|
| 12 | `=`, a click on Book2 (D10), a click on Book2's column B heading, then `{RIGHT}` | The text; where the dashes lie |
| 13 | As 12, then `{LEFT}` | The same |
| 14 | As 12, then `{UP}` | The same |
| 15 | As 12, then `{DOWN}{DOWN}` | The same, after each ↓ |
| 16 | `=`, a click on Book2 (D10), a click on the header cell `PV` of `Trades` (B1), then `{RIGHT}` and `{DOWN}` | The same, after each |

## Part B — ExSheet beside the decisions

Once tickets 55–58 say `Status: done` on `claude/exsheet-ninth-run-b`. As Part B of the ninth run:
`/sheet`, `/pointing` and `/pointing?narrow`, Chrome and Edge, WebAssembly, Server, and Server behind
150 ms, at 150%, real keys and the real mouse; the probe reads the DOM and takes the page's pictures.

| # | Page | Keys | What should be read (ADR-0058, Part B of the ninth run) |
|---|---|---|---|
| b1 | `/sheet` | `=XLOOKUP(1,A2:A4,B2:B4,,1)`, F2, ←←, then Tab | no list after ←←; Tab commits and moves to E10 (Q49) |
| b2 | `/sheet` | `=XLOOKUP(1,A2:A4,B2:B4,,-1)`, F2, ←, then Tab | no list; Tab commits (Q49, the caret inside a value) |
| b3 | `/sheet` | `=XLOOKUP(1,A2:A4,B2:B4,,`, then `Home` | the list closes; `A10` is pointed at and written (Q51) |
| b4 | `/sheet` | as b3, then `End` instead | the list closes; nothing is written, and the edit stays open (Q53) |
| b4a | `/sheet` | `=SUM(`, then `Home`; Escape; `=SUM(`, then `End` | `=SUM(A10`, A10 pointed at; then nothing written, the edit open (Q53) |
| b5 | `/sheet` | as b3, then Shift+→ instead | the list closes; `D10:E10` is pointed at and written (Q51) |
| b6 | `/sheet` | `=Posit`, then `Home` | the list of names: the caret moves to 0; the edit stays open (Q51, unchanged) |
| b7 | `/pointing` | `=`, a press on PV's header, ↓, ↓ | `=XLOOKUP("R-1", …)`, then `"R-2"`; the dashes on the cell (Q52) |
| b8 | `/pointing` | `=`, a press on PV's header, ← | `=Positions[Id]`, passing over Book; the dashes over Id's body (Q52) |
| b9 | `/pointing` | `=`, a press on PV's header, ↑ | unchanged; nothing told (Q52, an edge) |
| b10 | `/pointing` | `=`, a drag down PV's data, then ↓ | `=`, the drag's reason told; ↓ points in the Sheet, at C4 |
| b10a | `/pointing?narrow` | the grid scrolled to its first column; `=`, a press on Id's header, →, then ← | `=Positions[PV]`, the grid scrolled across so that PV's dashes lie whole in view, not scrolled down; then `=Positions[Id]`, Id whole in view (ticket 58) |
| b11 | `/pointing?narrow` | the grid scrolled to its last row and column; `=`, a press on R-40's PV | the dashes and both column outlines inside the client area, beside both gutters (DC-53) |

Behind 150 ms, wait for the positions grid to wear `ex-pointed-at` before any press that follows `=`
(ADR-0058, "On a circuit": a press within that round trip is an ordinary press).

## Finishing

Commit everything to `claude/exsheet-windows-verify-13` and push. The results go to
`verification/<date>-windows-13/`: the scripts, the `.jsonl`, the records, `excel/shots/` and the
page's pictures, and one report with a row per case: what was read, and, for Part B, whether it
matches the reading in the table. The last message lists every difference, each with the ADR
paragraph or ticket it belongs to. It proposes nothing on the user's behalf.
