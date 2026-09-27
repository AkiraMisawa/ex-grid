# What to verify on Windows, second run

Status: ready-for-human

For a Claude Code session on the Windows desktop used on 2026-09-27 (Excel, Chrome, Edge, WSL2
with nix). The first run's method, rules and tools still apply: read
[`verify-on-windows.md`](verify-on-windows.md) and [`excel-behaviours.md`](excel-behaviours.md)
first, and reuse `verification/2026-09-27-windows-excel/excel-driver.ps1` and the probes. **Decide
nothing. Record everything.** Do not change any ADR, `CONTEXT.md` or
`docs/definition-of-done.md`.

## Setup

- Fetch `claude/exsheet-start-8cx3v1` and branch **`claude/exsheet-windows-verify-2`** from its tip.
  Record that tip's commit as the verified commit.
- Build and run layers 1–2 as the first run did (in WSL). Record the counts.
- Before sending any key or mouse input, tell the user and wait for their go-ahead: the screen must
  be unlocked, the IME off, and their hands off the keyboard and mouse. Changing the regional format
  (`Set-Culture`) needs their agreement each time, and it is restored afterwards.

## Part A — the oracle again, whole corpus

`tests/ExSheet.Engine.Tests/ExcelOracle/oracle.ps1`, as before: en-US for the whole corpus, then
de-DE, ja-JP and en-GB for the cases that name them. Then `-Update`.

- Many cases changed since the first run to take Excel's observed answers. There are new
  `uncertain` cases for XLOOKUP's `match_mode` 3 (regular expressions: case sensitivity, partial
  versus full match, each construct), column widths and their `custom` flag, the whitespace and
  Reference-writing rules, and fill rounding.
- **Record the typed-entry cases twice:** once through COM, and once with real keys where COM and
  the keyboard can differ. TYPED-019/020 showed they can: `+A1` typed is a Formula, while COM's
  `FormulaLocal` gives text. List every case where the two differ.
- Results go to `verification/<date>-windows-excel/`, in the shape of the first run's `results.md`.
  Disagreements are listed, never fixed.

## Part B — the active cell (ADR-0052)

ADR-0052 makes ExGrid's Focus Excel's active cell. The rules it still needs come from here. For
each case below, record: the Selection (`Selection.Address`), the active cell (`ActiveCell`), which
cell is kept in view, and what the Name Box shows **during** the gesture (Excel shows a size such
as `3R x 2C` while dragging; take a screenshot). Use COM where it has the same meaning, and real
keys or the mouse otherwise, as the first run did.

1. Click B2, then Shift+↓ ×2 and Shift+→: the Selection, the active cell, and what scrolls when
   the extension leaves the screen downwards and to the right.
2. Drag from D5 to B2, upwards and to the left. Then drag from B2 to D5.
3. Select A1:C3, then press Enter twice. Where is the active cell? Then Shift+→ and Shift+↓: which
   edges of the range move?
4. The same with Tab, Shift+Tab and Shift+Enter, including wrapping at the range's end. Record the
   cycling order.
5. Ctrl+click to add D5 to A1:B2, then Shift+↓. Which range extends, and from where?
6. Ctrl+click on a cell that is already selected (Excel 365 deselects it). Then Shift+↓.
7. Shift+Backspace with a range selected (it collapses to the active cell), and Ctrl+Backspace
   after scrolling away (it scrolls back to the active cell).
8. Ctrl+. (period), repeatedly, in A1:C3: where does the active cell go?
9. Ctrl+Space and Shift+Space: where is the active cell?
10. Ctrl+A from inside a block of data, then Ctrl+A again. Where is the active cell?
11. Ctrl+Shift+End and Ctrl+Shift+Home from B2, and Shift+PageDown: the Selection and the active
    cell.
12. Select B2:D4 (active B2), type `x`, then Enter. Which cell holds `x`, and where does the active
    cell go?
13. Select B2:D4, type `x`, then Ctrl+Enter. Which cells are filled?
14. Select B2:D4, then Delete: which cells are cleared?
15. After a paste that spills from a single cell, and after inserting rows over a selected range:
    where is the active cell?

Record in `verification/<date>-windows-excel/active-cell.md`, one table per case, with the method
and screenshots. Where Excel's answer depends on a setting ("After pressing Enter, move selection"
in File › Options › Advanced), record the setting's value.

## Part C — ExSheet beside Excel again

Re-run `tests/ExGrid.Browser/sheet-vs-excel.spec.mjs` on both hosts and both browsers. The
clipboard items (16–19) are now wired: ExSheet carries Entries in its own copies, and its copies to
Excel carry formats. `clipboard-probe.mjs` covers both directions again, including a date copied
from a too-narrow Excel column, which ExSheet should now refuse by name. Record whether the HTML's
`data-ex-grid="invariant"` marker survives the real clipboard in Chrome and Edge, on both copy
routes.

The typing race: re-run `typing-probe-2.mjs` at 0, 30 and 60 ms on both hosts. It should now put
every value in the clicked cell.

## Part D — layer 3, both browsers, both hosts

As the first run's Part B (§22 Step 4), with the whole suite, including ticket 18's new specs
(`sheet.spec.mjs`, `declarations.spec.mjs` or whatever it added — read `tests/ExGrid.Browser`).
Record failures with their criterion IDs. MEM-4 now expects the editor's `input` listener. Also
run `typing-race` or its equivalent under the latency proxy at 150 ms.

## Part E — the bisect

Follow [`bisect-on-windows.md`](bisect-on-windows.md) for VZ-14 and BIG-1/5. It is independent of
A–D. Do it last, or first if the user prefers.

## Finishing

Commit everything to `claude/exsheet-windows-verify-2` and push. The last message to the user
lists every disagreement and failure, with the ticket, ADR or criterion it belongs to. It proposes
nothing on the user's behalf.
