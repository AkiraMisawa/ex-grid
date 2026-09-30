# What to verify on Windows, ninth run

Status: ready-for-human — **Part A done 2026-09-30** (`verification/2026-09-30-windows-excel-9/`); **Part B once tickets 34–38 and 41 are done.**

For the Claude Code session on the Windows desktop of the earlier runs (Excel, Chrome, Edge, WSL2 with
nix). Read [`verify-on-windows-8.md`](verify-on-windows-8.md) and
[`verify-on-windows-5.md`](verify-on-windows-5.md) first: their method and tools still apply. **Decide
nothing. Record everything.** Do not change any ADR, `CONTEXT.md` or `docs/definition-of-done.md`.

Part A asks Excel only. It needs no build and does not wait for the implementation. Its answers settle
what [ADR-0058](../../adr/0058-a-formula-points-across-grids-through-a-pointing-scope.md) leaves to
this run, which is held in [ticket 41](issues/41-after-the-ninth-windows-run.md).

## Setup

- Fetch **`claude/exsheet-pointing-scope`**. This procedure lives there until that branch is merged.
  Branch **`claude/exsheet-windows-verify-9`** from its tip, and record the tip as the verified commit.
  If the tip moves during the run, do not merge it in.
- **The user has authorised this run in advance**: real keys and mouse to Excel, and, for Part B, to
  the browsers. **Do not stop to ask.** Say "starting" before the first input and "finished" after the
  last.
- Drive Excel through `verification/2026-09-27-windows-excel/excel-driver.ps1`, as the eighth run did.
  Write this run's script as `verification/<date>-windows-excel-9/pointing.ps1`, taking one case or a
  list (`-Case 1,2`).
- **Every Formula is typed with real keys and every cell is pointed at with the real mouse**, never
  written through COM: what is asked is what Excel does while an edit is open. COM may only set up a
  case. In `SendKeys`, brace `+ ^ % ~ ( ) [ ] { }`.
- **Excel's window must receive plain keys**: the Japanese IME off, and the English (UK) keyboard for
  Excel's window during the run, as the eighth run did.
- Record the Excel version and build, the Office Theme, Windows' light or dark mode and the display
  scale.

## Part A — Excel pointing into a Table and into another workbook, and completion

**Set up for every case** (COM): a fresh workbook `Book1` with one sheet, `Sheet1`, at 100% zoom.
A1:B4 is a Table named `Positions` with headers `Id` and `PV`: `R-1`, `R-2`, `R-3` under Id, and
`10`, `20`, `30` under PV. Where a case says so, a second workbook `Book2` is open beside it, with a
Table `Trades` in A1:B4 of its `Sheet1` in the same shape, and both windows are arranged side by side
(View › Arrange All › Vertical), so that a click can reach either.

Select **D10** of Book1 and type or point as the case says. **Do not press Enter** unless the case
says so. With the edit still open, wait 600 ms and record, then press Escape until Excel is Ready.

**What to record for each case**, as one JSON line in `pointing.jsonl` and a row in `pointing.md`:

1. The Formula Bar's text, through UI Automation (as the eighth run read it), and a screenshot of both
   windows.
2. The Name Box's text.
3. Any list shown under the edit: every item, in order, as its text reads, and which one is selected.
4. Where the pointed outline is, and in which workbook.

| # | Book2 | Keys and clicks | What is asked | Reading |
|---|---|---|---|---|
| 1 | — | `=`, then click B3 (a data cell of the Table) | The text written | `=B3` |
| 2 | open | `=`, then click B3 of Book2 | The text written | `=[Book2]Sheet1!$B$3` |
| 3 | — | `=`, then drag B2 to B4 (all of PV's data) | The text written | `=Positions[PV]` |
| 4 | — | `=`, then drag A2 to B4 (both columns' data) | The text written | `=Positions[[Id]:[PV]]` or `=Positions` |
| 5 | — | `=`, then click B1 (PV's header cell) | The text written | `=Positions[[#Headers],[PV]]` |
| 6 | open | As case 2, then `{DOWN}` | The text; where the outline is; which window is active | the outline moves to Book2's B4, and the text follows (`$B$4`) |
| 7 | open | As case 2, then `+{DOWN}` (Shift+↓) | The same | the outline grows to B3:B4 in Book2 |
| 8 | open | As case 2 | The Name Box's text while Book2's cell is pointed at (record it in case 2's row) | open |
| 9 | — | `=SUM(Positions[` | The list, if any | `Id`, `PV`, `#All`, `#Data`, `#Headers`, `#Totals`, `@` |
| 10 | — | `=`, then F3 | Is a dialog shown? Its title, and every name listed. Is `Positions` among them? | Paste Name; whether tables are listed is open |
| 11 | — | `=Posit`, then Escape once (the list closes, the edit stays), then `{BS}` | Is the list shown again after the Backspace? Its items | yes, as for `=Posi` |
| 12 | — | `=XLOOKUP(1,A2:A4,B2:B4,,0,` | The list at `search_mode`, every item's text | `1 - Search first-to-last`, `-1 - Search last-to-first`, `2 - Binary search (sorted ascending)`, `-2 - Binary search (sorted descending)` |
| 13 | — | `=XLOOKUP(1,A2:A4,B2:B4,,` | The list at `match_mode` again, to confirm the texts of 2026-09-27 item 14 in this build | the five texts of item 14 |

Results go to `verification/<date>-windows-excel-9/`: `pointing.ps1`, `pointing.jsonl`, `pointing.md`
(one row per case, with the reading and whether Excel agrees) and `shots/`.

## Part B — ExSheet's Pointing Scope on Windows

Only once tickets 34–38 and 41 are marked done on `claude/exsheet-pointing-scope`. On their commit, `/sheet`
and `/sheets` in Chrome and Edge, both hosts, at 150%.

- `=`, a click on a PV cell of the positions grid, `*2`, Enter: the text written before Enter, the
  value after it, and where DOM focus was throughout (the Sheet's editor, never the positions grid).
- `=SUM(`, a click on the PV header, `)`, Enter.
- `=`, a click on a PV cell, then ↓, →, ← and Shift+↓: the text after each, where the dashes are, and
  the Name Box (empty while pointing into the grid).
- Each refusal of ADR-0058's table (Shift+click, a drag, a column the table does not have): nothing
  written, and the reason shown.
- The completion cases of `verify-on-windows-10.md`, group 1, typed into ExSheet on `/sheet`, beside
  what Excel did there.
- **Ask Excel too** (ADR-0058, "Readings taken while building ticket 44"), with the Table `Positions`
  of `verify-on-windows-10.md`: `=XLOOKUP(1,A2:A4,B2:B4,,1)` with the caret moved between `,,` and
  `1`: is a list shown, and what does Tab write? `=XLOOKUP(1,A2:A4,B2:B4,,4` and `,,A` at
  `match_mode`: is a list shown? With the value list open, `Home`, `End` and Shift+→: what does each
  do? Then the same in ExSheet on `/sheet`.
- The pointer over the positions grid while pointing (`cell`), and not otherwise.
- The positions grid narrowed until it shows its own scrollbars: the outlines and the dashes on its
  last column and its last painted row are whole, not cut by the Scrollbar Gutter.
- On the Server host behind the latency proxy (150 ms): `=`, a click and `*` sent as fast as
  `by-hand.ps1` sends keys, ten times. The text must be `=XLOOKUP(…)*` every time (DC-54).
- With Windows' high contrast on: what is left of the outlines and the dashes.
- Any console message.

Results go to `verification/<date>-windows-9/pointing-scope.md`.

## Finishing

Commit everything to `claude/exsheet-windows-verify-9` and push. The last message lists every
disagreement between Excel and a reading, and between ExSheet and ADR-0058, each with the ADR paragraph
or criterion it belongs to. It proposes nothing on the user's behalf.
