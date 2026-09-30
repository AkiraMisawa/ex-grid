# 42: An edit in the Formula Bar never enters Overwrite

Status: ready-for-agent

**What to build:** ADR-0051's note of 2026-09-30, "An edit in the Formula Bar never enters
Overwrite". Found by Part B of the eighth Windows run
(`verification/2026-09-30-windows-8/reference-outlines.md`, case `7k`): `=A1+B1` typed into the
Formula Bar, then `Home`, committed the Formula and moved the Focus to column A. Typing the `A` had
ended Point in Overwrite. Excel's Formula Bar is always in Edit.

**Blocked by:** None (can start immediately)

- [ ] While the edit's surface is the Formula Bar, typing that ends Point returns to Caret, not
      Overwrite (`ExGrid.FormulaEntry.cs`, where typing ends Point) (ED-29)
- [ ] An edit that moves from the cell into the bar (`OnFormulaBarFocusAsync` with an edit open)
      goes into Caret; one that moves back into the cell keeps the mode it has (ED-29)
- [ ] `Home`, `End`, ← and → in the bar move the caret and commit nothing; Enter, Tab and Escape keep
      their meanings; the key listener's gate follows the mode the core tells it (ED-29)
- [ ] Nothing changes for an edit in the cell: typing onto a cell still opens Overwrite, and the arrows
      still commit and move there (ADR-0012)
- [ ] Layer 2: the mode after typing in the bar, after a press into the bar mid-edit, and in the cell
      (ED-29)
- [ ] Layer 3 on `/sheet` under both Chromes: `=A1+B1` typed into the bar, then `Home`, →, three
      Deletes: the bar holds `=B1`, the edit is open, and the Focus has not moved (ED-29). Write it;
      the orchestrator runs it
