# What to verify on Windows, sixth run

Status: ready-for-human once `claude/exsheet-f4` is merged into `claude/exsheet-start-8cx3v1` —
**A, then B**. Part A alone can run before that: it asks only Excel.

For the Claude Code session on the Windows desktop of the earlier runs. The fifth run's method,
tools and advance authorisation still apply: read [`verify-on-windows-5.md`](verify-on-windows-5.md)
and [`verify-on-windows-4.md`](verify-on-windows-4.md) first, and reuse the key-sending scripts in
`verification/2026-09-28-windows-excel-3/` (`active-cell.ps1`, `by-hand.ps1`). **Decide nothing.
Record everything.** Do not change any ADR, `CONTEXT.md` or `docs/definition-of-done.md`.

## Setup

- Fetch `claude/exsheet-start-8cx3v1`. Branch **`claude/exsheet-windows-verify-6`** from its tip, and
  record that tip as the verified commit of every part. If the tip moves during the run, do not
  merge it in.
- **The user has authorised this run in advance**, as for the fifth: real keys and mouse to Excel
  and the browsers. **Do not stop to ask.** Say "starting" before the first input and "finished"
  after the last.
- Every F4 below is a real key press sent to Excel's window, never `Range.Formula` written through
  COM: what is asked is what the key does to the text being edited. Read each result from the
  Formula Bar's text before the edit is committed (a screenshot, or the text copied out with
  Ctrl+A, Ctrl+C inside the edit), then press Escape.

## Part A — F4 in Excel (ADR-0051, "F4 cycles the Reference at the caret")

ADR-0051 took the readings below for the implementation. Record, for each case, the text and where
the caret or selection is after each press, and say whether it agrees with the reading. A blank
sheet, en-US.

| # | Type, then do this | Presses | Reading |
|---|---|---|---|
| 1 | `=B2`, F4 | 1 to 4 | `=$B$2`, `=B$2`, `=$B2`, `=B2` |
| 2 | `=A1+B2`, then ← ← ← (caret just after `A1`), F4 | 1 | `=$A$1+B2` |
| 3 | `=A1+B2`, then Home, → (caret just before `A1`), F4 | 1 | `=$A$1+B2` |
| 4 | `=A1+B2`, then Home, → → (caret inside `A1`), F4 | 1 | `=$A$1+B2` |
| 5 | `=SUM(A1:B2)`, then ← (caret just after `B2`), F4 | 1 to 4 | `$A$1:$B$2`, `A$1:B$2`, `$A1:$B2`, `A1:B2` |
| 6 | `=$A1:B2`, then ← (caret after `B2`), F4 | 1 | the next form of the first end (`$A1` → `A1`), given to both: `=A1:B2` |
| 7 | `=A1+B2`, then Shift+Home, Shift+→ (select `A1+B2`), F4 | 1 | `=$A$1+$B$2` |
| 8 | `=SUM(A:A)`, then ← , F4 | 1 to 3 | `$A:$A`, `A:A`, `$A:$A` |
| 9 | `=SUM(1:1)`, then ←, F4 | 1 to 3 | `$1:$1`, `1:1`, `$1:$1` |
| 10 | `=Sheet2!A1` (add a Sheet2 first), F4 | 1 | `=Sheet2!$A$1` |
| 11 | Make A1:B3 a Table named `Positions` with a column `PV`; in D1 type `=SUM(Positions[PV])`, then ←, F4 | 1 | unchanged |
| 12 | `=SUM(A1)`, then Home, → → (caret inside `SUM`), F4 | 1 | unchanged |
| 13 | `=1+2`, F4 | 1 | unchanged |
| 14 | `B2` (no `=`), F4 | 1 | unchanged |
| 15 | `=`, ↓ (pointing: `=A2`), F4, then ↓ | F4 once, then ↓ | after F4 `=$A$2`; after ↓, reading `=A3` (Excel may keep `=$A$3`: record which) |
| 16 | `=`, ↓, F4 (pointing), then type `+` | — | `=$A$2+` and pointing ends |
| 17 | Select a cell holding `=B2` without editing, press F4 | 1 | nothing to do with references: record what Excel repeats, if anything |

For each case also record **where the caret is after F4** (the reading is: at the end of the
rewritten Reference) and, for case 7, **what is selected** (the reading is: the rewritten span).

## Part B — the same cases in ExSheet

On the verified commit, `/sheet` in Chrome and Edge, both hosts. Type each case from Part A into a
cell, and again into the Formula Bar. Record the text after each press and whether it agrees with
Excel's from Part A. Also record:

- whether F4 with no edit open does anything at all (it should not);
- on the Server host behind the latency proxy (150 ms), case 1 typed as fast as `by-hand.ps1` sends
  it: `=B2` then four F4 presses in a burst, the text after each;
- any console message.

Results go to `verification/<date>-windows-excel-6/f4.md` (Part A) and
`verification/<date>-windows-6/f4.md` (Part B).

## Finishing

Commit everything to `claude/exsheet-windows-verify-6` and push. The last message lists every
disagreement between Excel and a reading, and between ExSheet and Excel, each with the ADR
paragraph or criterion it belongs to. It proposes nothing on the user's behalf.
