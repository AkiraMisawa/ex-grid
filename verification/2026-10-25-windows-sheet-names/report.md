# Windows, 2026-10-25: the name Excel gives a new Sheet

**Asked at:** `5e3eecb4` (origin/main), on 2026-10-25 from 02:50 to 03:05 BST, on the Windows desktop of the earlier
runs, Excel for Microsoft 365 16.0 build 20430, regional format en-GB. It carries on the 2026-10-24 run's answer 13
(`../2026-10-24-windows-workbook-12/report.md`), which the user chose to build for 0.2.0. These are questions, not
checks: the run tags nothing.

| | |
|---|---|
| `ask-names.ps1`, `records/reopened.jsonl` | each case's workbook saved as `.xlsx`, opened again, and two Sheets added after the last (COM) |
| `ask-session.ps1`, `records/session.jsonl` | within one session: a copy, a deletion, a rename and a move, then two Sheets added (COM) |
| `records/keys-xs.jsonl` | with real keys, through `../2026-10-24-windows-workbook-12/excel/xl-server.ps1`: Shift+F11, Ctrl+Z, Shift+F11, Shift+F11, Ctrl+Z twice, Ctrl+Y |

## The answers

- **Within one session, a new Sheet takes `Sheet` and the next number of a count of the Sheets made, passing over a name
  a Sheet has.** A new workbook has made the Sheet it starts with. A copy uses a number: after one copy of Sheet1, two
  new Sheets are Sheet3 and Sheet4; after two, Sheet4 and Sheet5. A Sheet added and then deleted, renamed or moved has
  used its number: the next are Sheet3 and Sheet4.
- **Undoing an insertion gives its number back:** Shift+F11 made Sheet2, Ctrl+Z took it out, and Shift+F11 made Sheet2
  again. Ctrl+Y after undoing two insertions did nothing.
- **An opened workbook has made none:** saved and opened again, Sheet1, Sheet2, Sheet3 add Sheet4 and Sheet5; Sheet1
  and Sheet3 add Sheet2, then Sheet4; Sheet5 alone adds Sheet1, then Sheet2; Jan and Feb add Sheet1, then Sheet2; a
  workbook whose five later Sheets were all deleted before the save adds Sheet2 and Sheet3.

What was built from it is in ADR-0331's note of 2026-10-25, and pinned by `SheetNamingTests`.
