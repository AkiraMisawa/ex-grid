# What to verify on Windows, eighth run

Status: ready-for-human — **Part A now, by hand; Part B once tickets 27–30 are done.**

Part A is done by the user, by hand, in Excel (Microsoft 365) on Windows. It asks Excel only, so it
does not wait for the implementation. The session that wrote ADR-0057 records what the user reports
in `verification/<date>-windows-excel-8/reference-outlines.md`. Part B is for the Claude Code session
on the Windows desktop of the earlier runs, under the fifth run's method and advance authorisation
([`verify-on-windows-5.md`](verify-on-windows-5.md)). **Decide nothing. Record everything.**

## Part A — Excel's range finder (ADR-0057, "Readings")

A blank workbook with one sheet named `Sheet1`. Type each Formula into **D10**, and **do not press
Enter**: read the screen while the edit is open, then press Escape. For each case, record:

- the colour of each Reference's text, in the cell and in the Formula Bar;
- which cells are outlined, and in which colour;
- anything else drawn: a fill inside the outline, handles at its corners, a dashed or moving line.

For the colours, read the hex value with PowerToys Color Picker (Win+Shift+C), or save a screenshot.

| # | Type into D10 | What is asked | Reading |
|---|---|---|---|
| 1 | `=A1+B1+C1+D1+E1+F1+G1+H1+I1+J1` | The colour of each Reference, in order; after how many the colours repeat | eight colours, then round again |
| 2 | `=A1+A1` | One colour or two; one outline or two | one colour, one outline |
| 3 | `=A1+$A$1` | The same | one colour, one outline |
| 4 | `=A1+B1+A1` | The colours of the three | A1 first colour, B1 second, the second A1 first |
| 5 | `=B2:A1` | The outline's cells | A1:B2 |
| 6 | `=A1:B2+B2` | Two colours or one; the outlines | two colours, two outlines |
| 7 | `=A1+B1`, then delete `A1+` with the keyboard | B1's colour before and after | first colour after: colours follow first appearance |
| 8 | `=Sheet1!A1` | Coloured? Outlined? | coloured and outlined |
| 9 | Add a sheet `Sheet2`. Back on Sheet1: `=Sheet2!A1+B1` | Is `Sheet2!A1` coloured? B1's colour | `Sheet2!A1` uncoloured, B1 first colour |
| 10 | `=SUM(A:A)`, then `=SUM(1:1)` | The outline's extent | the whole column; the whole row |
| 11 | Make A1:B4 a Table named `Positions` with headers `Id` and `PV`. In D10: `=SUM(Positions[PV])` | Coloured? Which cells are outlined: header, data, all? | coloured; outlined over the data cells |
| 12 | `=SUM(Positions[PV])+SUM(Positions[Id])` | Two colours? | two |
| 13 | `=SUM(A1,` | A1 coloured? | coloured |
| 14 | `=A1+` | The same | coloured |
| 15 | `="A1"&B1` | Is the `A1` inside the string coloured? | only B1 |
| 16 | `=LOG10(A1)` | Is `LOG10` coloured or outlined (it is also a cell)? | only A1 |
| 17 | `=a1` (lower case) | Coloured? | coloured |
| 18 | `A1` (no `=`) | Anything coloured? | nothing |
| 19 | `=`, then ↓ (pointing) | The pointed outline: colour, dashed or solid, moving or still | dashed, first colour |
| 20 | `=`, ↓, then type `+`, then ↓ again | The first Reference's outline once pointing moved on | solid; the new one dashed, second colour |
| 21 | Put `=A1+B1` in D10 and press Enter. Then: select D10 (no edit); F2; Escape; double-click D10; Escape; click into the Formula Bar | Outlines shown for each | none on select; shown for F2, double-click and the Formula Bar |
| 22 | `=D10` (the cell itself) | Is D10 outlined? | outlined |
| 23 | Case 1 again, zoomed in on one outline | The line's width, a fill inside, corner handles | solid, a pale fill, handles |

## Part B — ExSheet beside Excel

On the commit tickets 27–30 land in, `/sheet` in Chrome and Edge, both hosts, at 150%. Type each case
from Part A into a cell, and again into the Formula Bar. Record whether ExSheet's colours and outlines
agree with Excel's from Part A. Also:

- on the Server host behind the latency proxy (150 ms), case 1 typed as fast as `by-hand.ps1` sends
  it: whether any frame shows coloured text over the wrong characters (ADR-0057: it should not);
- case 11 with the positions grid beside the Sheet: the PV column's outline and its colour;
- with Windows' high contrast on, case 1: what is left (ADR-0057, "Not done");
- any console message.

Results go to `verification/<date>-windows-8/reference-outlines.md`.

## Finishing

Part B is committed to `claude/exsheet-windows-verify-8` and pushed. The last message lists every
disagreement between Excel and a reading, and between ExSheet and Excel, each with the ADR paragraph
or criterion it belongs to. It proposes nothing on the user's behalf.
