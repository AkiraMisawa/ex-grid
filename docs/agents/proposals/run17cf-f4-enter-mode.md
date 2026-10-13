# Proposal: F4 at the edges, and Overwrite's and Point's Shift and Primary Modifier keys (run 17, Part C, and the readings of 2026-10-12, R1; area f4-enter-mode)

Findings C-diff-2 to C-diff-10 of the seventeenth Windows run
(`verification/2026-10-11-windows-17/report.md`, Part C, "The differences", on branch
`claude/exsheet-windows-verify-17`), and the readings R1 (i) to (viii) of 2026-10-12
(`verification/2026-10-12-windows-readings/report.md`, on branch
`claude/exsheet-windows-readings-2026-10-12`), which supersede this area's second round where they
differ. SH-28 and ADR-0202 say a disagreement with Excel is settled in ADR-0051 first; the
implementation on this branch follows Excel where a run observed it, and says "a reading" or "by
generalisation" where none did. Nothing below was decided with the user: each edit is an observation,
or a choice of this area's, named as such. The edits are numbered exact old → new blocks of whole
lines, in the order they apply; each old text occurs once in its file at the branch tip.

## What was built

- **C-diff-4, C-diff-5, C-diff-6** — the two ends of a cell range are References of their own
  (`FormulaEntry.CycleReference`): a caret cycles only the end it touches (`A1|:B2` touches `A1`,
  `A1:|B2` touches `B2`), from that end's own form; a selection cycles every end it covers, overlaps or
  touches, each to the next form of the first. Whole columns and whole rows stay one Reference of two
  forms. A Sheet qualifier stands before the first end.
- **C-diff-8** — the rewritten Reference's column letters are written in upper case; the rest of the
  text, a Sheet qualifier and the other end of a range among it, is left as typed.
- **C-diff-3, R1 (iv) to (vi)** — the engine answers the span of what it rewrote for a caret as for a
  selection. The core (`OnCycleReferenceKey`) places it as a selection where the F4 key carried a
  selection, or a caret the user moved in that text (the key message's existing "moved" flag: a caret
  key or a press), and as a caret at its end where the caret stood where typing, the edit's opening or
  an earlier F4 left it — in any editing state. The second round tied this to the editing state
  (Caret selects); R1 (iv) contradicted that in the Formula Bar, which is in Caret. While pointing, the
  function is asked over the whole Reference the outline wrote, so a pointed range's two ends cycle
  together (R1 (vi)); pointing goes on, the caret at the end of the rewritten Reference, which a key
  typed next follows (Part C, case 16). Excel reported the rewritten Reference as the bar's selection
  (R1 (vi)); ExSheet shows it with Point's own look and never selects the field's text, which a key
  typed would replace (ADR-0057, case 32). The placement-in-flight rule carries the selection as it
  carries a caret. `CycleReference`'s signature and `EditorRewrite` are unchanged; no public member
  was added (ADR-0201).
- **C-diff-7** — `ex-grid.js` forgets the user's caret move once the field's value is seen to differ
  from the text it was made in (`movedByUser`, and `setCaret` reads it before comparing), so an equal
  text written later by F4's cycle is not taken for the one the move was made in.
- **C-diff-2, R1 (vii)** — in Overwrite the gate claims Shift+Home, the four Shift+arrows and the
  Primary Modifier's arrows with Shift or without. Where a Reference can go, the Shift keys point: a
  Shift+arrow as Point does with it, and Shift+Home from the edited cell to the row's first column
  (observed with a Ctrl+C before it: in R1's reading after its first step, `=` in D10, a Ctrl+C that
  copied nothing, then Shift+Home pointed, and a Ctrl+C copied `A10:D10`; the text `=A10:D10` by
  generalisation). The Primary Modifier's arrows there are claimed and do nothing (a reading of this
  area's: Excel was not asked). Elsewhere they commit and then move or extend the Selection exactly
  as with no edit open, a Reject holding the commit; a commit whose own handler moved the order has
  dropped the Selection, and they move nothing then (ADR-0142). Caret, and a list of names, keep
  them the editor's; a list open over Point leaves Shift+Home and the Primary Modifier's arrows to
  the editor. Shift+End stays the editor's: see "Decided".
- **R1 (viii)** — Point claims Shift+Home again, now with Excel's meaning: it extends the outline from
  its fixed end to the row's first column, and pointing goes on. Round 2 had taken it out of Point's
  set.
- **R1 (i) to (iii)** — ExSheet's Formula Bar already did what Excel's did after `=` and ↓ (the bar is
  in Caret, so ↓ is the editor's): pinned in layer 2 and layer 3, nothing changed.

## Decided

- **Shift+End in Overwrite.** R1 (vii): typed `abc` into D10, Shift+End committed, left the Selection
  on D10 and turned End Mode on. ExGrid has no End Mode (ADR-0058, Q53), and its End with no edit
  open is the row's last cell (ADR-0012). The options: (a) commit, and extend to the row's last cell
  as Shift+End does with no edit open; (b) commit, and leave the Selection on the edited cell, as
  Excel's Selection was left, without End Mode; (c) leave it the editor's, as now. *(2026-10-13,
  decided with the user: (c), as built.)* ADR-0010's note below says so.
- **The pointed Reference's grey ground right after `=`.** ADR-0057 (cases 19 and 31 of the eighth
  run) shows no grey when the pointed Reference follows the Formula's `=` directly, and the Formula
  Bar's selection was empty there. R1's pictures show `D11` on the grey ground after `=`, ↓
  (`shots/r1-vi-2-cells.png`), `$D$11:$D$12` after F4 (`r1-vi-4-cells.png`) and `A11:D11` after
  Shift+Home (`r1-viii-3-cells.png`), with UI Automation giving each as the bar's selection. The two
  Excel runs disagree; ExGrid follows ADR-0057, and this area changed nothing there. Timing does not
  obviously explain it: the eighth run took its pictures 600 ms and about 1 s after the keys (case
  19: 652 and 1014 ms, `verification/2026-09-29-windows-excel-8/range-finder.md`), and the tenth run's
  grey that depended on timing was after `+`, not `=` (ADR-0057) — a reading. The options: (a) keep
  ADR-0057 (no grey straight after `=`); (b) show the grey there too, as R1 pictured it; (c) keep (a)
  until the next Windows run asks again (question 6). Built: (a), as it was. The orchestrator's ruling
  of 2026-10-13: (c), so ADR-0057 stands until question 6 is answered, and no ADR changes here.

## ADR-0051, "Formula entry: completion, Point mode, and a Formula Bar inside the grid's root"

### 1. docs/adr/0051-formula-entry-completion-point-mode-and-the-formula-bar.md

Old:

```
- **Shift+arrow points too.** While a pointing outline stands, the key listener claims the four
  Shift+arrows as well, so they extend the outline instead of selecting text in the input.
```

New:

```
- **Shift+arrow points too.** While a pointing outline stands, the key listener claims the four
  Shift+arrows as well, so they extend the outline instead of selecting text in the input.
  *(2026-10-12, after the readings of 2026-10-12, R1 (viii).)* **Shift+Home is claimed with them**,
  and extends the outline from its fixed end to the row's first column, pointing going on: in Excel,
  `=`, ↓, Shift+Home in D10 gave `=A11:D11`, still in Point (observed). From a range already pointed
  (`=D11:D12`), it extends from the range's fixed end (`=A11:D12`), as Shift+Home extends a
  Selection: a reading. The second round of the seventeenth run's corrections had left it to the
  editor, which selected text.
```

### 2. docs/adr/0051-formula-entry-completion-point-mode-and-the-formula-bar.md

Old:

```
- **While pointing, F4 cycles the Reference the pointing outline wrote**, and pointing goes on.
```

New:

```
- **While pointing, F4 cycles the Reference the pointing outline wrote**, and pointing goes on.
  *(2026-10-12.)* The whole of it: the function is asked with a selection over the outline's
  Reference, so a pointed range's two ends cycle together, as Excel's did (observed, the readings of
  2026-10-12, R1 (vi): `=`, ↓, Shift+↓, F4 in D10 gave `=$D$11:$D$12`). Asked at a caret at its end,
  only the second end would cycle, now that each end is a Reference of its own.
```

### 3. docs/adr/0051-formula-entry-completion-point-mode-and-the-formula-bar.md

Old:

```
**Readings, until the seventh Windows run observes them.** The rules above are Excel's documented
behaviour. These are the closest readings of what it does at the edges, taken for the
implementation and asked of Excel in `docs/specs/exsheet/verify-on-windows-7.md`:
```

New:

```
**What Excel does at the edges** *(observed by the seventeenth Windows run, 2026-10-11, Excel
16.0.20430 on Windows 11, `verification/2026-10-11-windows-17/report.md`, Part C, and by the readings
of 2026-10-12, R1, `verification/2026-10-12-windows-readings/report.md`; settled here 2026-10-12, as
SH-28 and ADR-0202 ask)*. The rules above are Excel's documented behaviour. The bullets below were
first the closest readings of what it does at the edges, taken for the implementation and asked of
Excel in `docs/specs/exsheet/verify-on-windows-7.md`; the seventeenth run put them to Excel, typed
into a cell (Enter mode) and with F2 first (Edit mode), and R1 in the Formula Bar and while pointing.
Where a reading was wrong, the bullet says what Excel does and keeps what was read:
```

### 4. docs/adr/0051-formula-entry-completion-point-mode-and-the-formula-bar.md

Old:

```
- **The Reference at the caret** is the one the caret is inside or touching, on either side: `=A1|+B2`
  and `=|A1+B2` both cycle `A1`. A caret touching no Reference changes nothing, as does F4 in a
  function name, a number, a string, or text that is not a Formula.
```

New:

```
- **The Reference at the caret** is the one the caret is inside or touching, on either side: `=A1|+B2`
  and `=|A1+B2` both cycle `A1` (observed, cases 2 to 4). **Each end of a cell range is a Reference of
  its own** *(observed, cases 5, 6 and 22)*: `=SUM(A1:B2|)` and `=SUM(A1:|B2)` cycle `B2`, and
  `=SUM(A1|:B2)` cycles `A1`. A caret touching no Reference changes nothing, as does F4 in a function
  name (observed, case 12), a number (case 13) or text that is not a Formula (case 14), and in a
  string, a reading by generalisation: no case asked it.
```

### 5. docs/adr/0051-formula-entry-completion-point-mode-and-the-formula-bar.md

Old:

```
- **A range cycles as one**: `A1:B2` → `$A$1:$B$2` → `A$1:B$2` → `$A1:$B2` → `A1:B2`. From a range whose
  ends differ, the next form is taken from the first end and given to both.
```

New:

```
- **A cell range does not cycle as one: the end at the caret cycles, from its own form** *(corrected
  after the seventeenth Windows run, cases 5, 6 and 22. The reading was that a range cycles as one,
  `A1:B2` → `$A$1:$B$2` → `A$1:B$2` → `$A1:$B2` → `A1:B2`, its next form taken from the first end and
  given to both; Excel cycles only the end the caret touches)*. `=SUM(A1:B2)` with the caret after
  `B2` gives `=SUM(A1:$B$2)`, `=SUM(A1:B$2)`, `=SUM(A1:$B2)` and `=SUM(A1:B2)`; `=$A1:B2` gives
  `=$A1:$B$2`, the other end left; `=B2:A1` gives `=B2:$A$1`. A selection over both ends cycles both,
  as it cycles every Reference it covers (below), each to the first end's next form, and so does a
  range being pointed at (above; observed, R1 (vi)). Whole columns and whole rows stay one Reference
  (below).
```

### 6. docs/adr/0051-formula-entry-completion-point-mode-and-the-formula-bar.md

Old:

```
  `=$A$1+$B$1`. The first reading, "a selection that overlaps no Reference changes nothing", was
  wrong. The overlapping case and the form taken from the first Reference are still read.)*
```

New:

```
  `=$A$1+$B$1`. The first reading, "a selection that overlaps no Reference changes nothing", was
  wrong. The overlapping case and the form taken from the first Reference were observed by the
  seventeenth Windows run, cases 19 and 20: `1+B` selected in `=A1+B2` gave `=$A$1+$B$2`, and
  `A1+$B$2` selected gave `=$A$1+$B$2`.)*
```

### 7. docs/adr/0051-formula-entry-completion-point-mode-and-the-formula-bar.md

Old:

```
- **The text keeps the case and the order it was typed in**, since only `$` signs are written:
  `=b2` gives `=$b$2`, and `=B2:A1` gives `=$B$2:$A$1`.
- **Whole columns and whole rows have two forms**: `A:A` ↔ `$A:$A`, `1:1` ↔ `$1:$1`.
- **A Sheet qualifier is kept**, and only the cell part cycles: `Sheet2!A1` → `Sheet2!$A$1`.
- **A structured reference does not cycle**: `Positions[PV]` is left as it is.
- **After F4, the caret is at the end of the rewritten Reference**, or, from a selection, the
  selection covers what was rewritten.
- **After F4 while pointing, a further move writes the new Reference as pointing writes it**, in the
  relative form. Whether Excel keeps the `$` form while pointing goes on is asked in the run.
```

New:

```
- **The rewritten Reference's column letters are written in upper case** *(corrected after the
  seventeenth Windows run, cases 21 and 22. The reading was that only `$` signs are written, so that
  `=b2` gave `=$b$2`; Excel gives `=$B$2`)*. **The rest of the text keeps the case and the order it was
  typed in**, a reading: no case observed lower-case text outside the rewritten Reference. `=B2:A1`
  gives `=B2:$A$1`, its order kept. A Sheet qualifier is not rewritten and keeps
  its case: it was not asked.
- **Whole columns and whole rows have two forms**: `A:A` ↔ `$A:$A`, `1:1` ↔ `$1:$1` (observed, cases
  8 and 9: the third press gives `$A:$A` and `$1:$1` again).
- **A Sheet qualifier is kept**, and only the cell part cycles: `Sheet2!A1` → `Sheet2!$A$1` (observed,
  case 10).
- **A structured reference does not cycle**: `Positions[PV]` is left as it is (observed, case 11).
- **After F4, the rewritten Reference is selected where the caret had been moved, and a caret stands
  at its end where typing left it** *(corrected after the seventeenth Windows run and the readings of
  2026-10-12. The reading was a caret at the end in every case; a first correction, taken from Part C
  alone, tied the selection to Edit mode, and R1 (iv) contradicted it in the Formula Bar)*. Observed:
  a caret moved with ←, or with Home and →, after F2 selected what was rewritten (`$A$1` in cases 2
  to 4, `$B$2` in case 5, the whole `$A:$A` and `$1:$1` in cases 8 and 9), and so did ← in the bar
  (R1 (v): `=SUM(A1:B2)`, ←, F4 gave `=SUM(A1:$B$2)` with `$B$2` selected); a selection stayed over
  what was rewritten (cases 7 and 18 to 20); and with the caret where typing left it, the caret stood
  at the end, typed into a cell (cases 1, 10, 21 and 22) and into the bar (R1 (iv): `=B2`, F4
  gave `=$B$2`, nothing selected). The Consumer answers the span of what was rewritten, a Sheet
  qualifier included before the first end, for a caret as for a selection. The F4 key message
  already says whether the user moved the caret in that text (above), and the grid selects the span
  where it says so or where the key carried a selection, and collapses it to a caret at its end
  otherwise, in any editing state. A press in the text counts as a move, and a caret where F2 opened
  the edit, or where an earlier F4 placed it, as one typing left: readings, by generalisation. A
  placement still in flight carries the selection as it carries a caret (the rule settled while
  building, above), so the next F4 cycles the same Reference. *(Whether a selection in Edit mode
  starts at a Sheet qualifier was not asked: case 10 was typed.)*
- **After F4 while pointing, the rewritten Reference is the one Point is writing**: the caret stands
  at its end, so a key typed next follows it (observed, case 16: `=`, ↓, F4, `+` gave `=$A$2+`).
  Excel reported it as the bar's selection (R1 (vi): UI Automation gave `$D$11:$D$12`); ExSheet
  shows it as Point shows the Reference it writes, with Point's look and never as a selection of the
  field's text, which a key typed would replace (Reference Outlines, below; ADR-0057, case 32).
  **A further move writes the new Reference as pointing writes it, in the relative form** (observed,
  case 15b: `=A3`, the reading's first option).
```

### 8. docs/adr/0051-formula-entry-completion-point-mode-and-the-formula-bar.md

Old:

```
- **While the edit is in the Formula Bar, typing keeps it in Caret.** Typing that ends Point there
  returns to Caret, not Overwrite. `Home`, `End`, ← and → move the caret, as they do in Caret
  anywhere, and Enter, Tab and Escape keep their meanings. Excel's bar stays in Edit while it is typed
  in (the tenth Windows run, cases 14–16).
```

New:

```
- **While the edit is in the Formula Bar, typing keeps it in Caret.** Typing that ends Point there
  returns to Caret, not Overwrite. `Home`, `End`, ← and → move the caret, as they do in Caret
  anywhere, and Enter, Tab and Escape keep their meanings. Excel's bar stays in Edit while it is typed
  in (the tenth Windows run, cases 14–16). *(Observed again by the readings of 2026-10-12, R1 (i) to
  (iii): a press into Excel's bar, `=`, ↓ stayed in Edit with the text `=` and nothing outlined; F4
  and ↓ changed nothing, and `+` typed on. ExSheet's bar does the same: the seventeenth run's Part C
  read `=` and `=+` there, cases 15, 15b and 16, and its "difference" 10 is none.)*
```

## ADR-0010, a note under the key table of "The core arbitrates the keys"

### 9. docs/adr/0010-chrome-seams-column-menu-editor-loading.md

Old:

```
| Everything else | the editor |
```

New:

```
| Everything else | the editor |

*(2026-10-12, after the seventeenth Windows run, Part C, case 7, and the readings of 2026-10-12, R1
(vii).)* **In Overwrite the core also takes Shift+Home, the four Shift+arrows, and the Primary
Modifier's arrows with Shift or without.** Typed into a cell, Excel's Enter mode committed the entry
on Shift+Home and extended the Selection, which the Shift+→ after it narrowed to `B10:D10` (case 7);
on Shift+→, Shift+↓ and Shift+← it committed and extended from D10 (`D10:E10`, `D10:D11`,
`C10:D10`); on Ctrl+→ it committed and moved to XFD10, and on Ctrl+Shift+→ it committed and selected
`D10:XFD10` (R1 (vii)). ExGrid left them to the editor, which selected its text or moved its caret.
Shift+↑ and the Primary Modifier's other arrows follow by generalisation. Where the Consumer says a
Reference can go, the Shift keys point
([ADR-0051](./0051-formula-entry-completion-point-mode-and-the-formula-bar.md)): a Shift+arrow extends
a pointing outline from the edited cell, as Point does with it, and Shift+Home points from the edited
cell to the row's first column (observed with a Ctrl+C before it: in R1's reading after its first
step, `=` in D10, a Ctrl+C that copied nothing, then Shift+Home pointed, and a Ctrl+C copied
`A10:D10`); the Primary Modifier's arrows there are taken and do nothing, as `End` does
([ADR-0058](./0058-a-formula-points-across-grids-through-a-pointing-scope.md), Q53), rather than
commit a Formula that ends too early — a reading: Excel was not asked. Elsewhere they commit, and then
move or extend the Selection exactly as the same key does with no edit open
([ADR-0012](./0012-anchor-focus-and-keyboard-navigation.md)), an arrow with the Primary Modifier to
the edge the Consumer answers; a Reject holds the commit, as it does for the arrows
([ADR-0034](./0034-validation-is-a-consumer-verdict-enforced-only-at-the-editor.md)), and a commit
whose own handler moved the order has dropped the Selection the key was aimed with, so the key moves
and extends nothing, as Enter and the arrows move nothing then
([ADR-0142](./0142-a-write-lands-as-the-user-entered-it-and-a-change-under-the-editor-is-told.md)).
Caret keeps them the editor's, and so does a list of names
([ADR-0058](./0058-a-formula-points-across-grids-through-a-pointing-scope.md), Q51); a list open over
Point leaves Shift+Home and the Primary Modifier's arrows to the editor, and its Shift+arrows point.
**Shift+End stays the editor's**: Excel's committed, left the Selection on D10 and turned End Mode
on (R1 (vii)), and the grid has no End Mode (ADR-0058, Q53). *(2026-10-13, decided with the user:
Shift+End is left to the editor.)*
```

## Definition of Done

### 10. docs/definition-of-done.md

Old:

```
| **ED-2** | MUST | Both editing states exist and the arrow keys mean different things in each: **Overwrite** commits and moves, **Caret** moves the caret (ADR-0010) | Layer 3, real keys | the two are distinguishable by observable behaviour, and F2 moves between them |
```

New:

```
| **ED-2** | MUST | Both editing states exist and the arrow keys mean different things in each: **Overwrite** commits and moves, and its Shift+arrows, Shift+Home and Primary Modifier's arrows commit and then move or extend the Selection as they do with no edit open, or, where a Reference can go, the Shift keys point; **Caret** moves the caret, and the Shift forms select text (ADR-0010, note of 2026-10-12) | Layer 2 + Layer 3, real keys | the two are distinguishable by observable behaviour, and F2 moves between them |
```

### 11. docs/definition-of-done.md

Old:

```
| **DC-45** | MUST | With a Reference cycling function declared, F4 while an edit is open, in the Cell Editor or the Formula Bar, sends the text and selection its key message carries, writes back the text the function answers and places the selection it answers; while pointing it rewrites the Reference the outline wrote and pointing goes on. Without the function, or with no edit open, F4 is not claimed. No JavaScript use is added (ADR-0051, 2026-09-29; ADR-0021) | Layer 2 + Layer 3 on the Server host with 150 ms injected | as stated; four F4 presses in a burst give the four forms in order |
```

New:

```
| **DC-45** | MUST | With a Reference cycling function declared, F4 while an edit is open, in the Cell Editor or the Formula Bar, sends the text and selection its key message carries, writes back the text the function answers and places the selection it answers — as a selection where the key carried a selection or a caret the user moved in that text, and as a caret at its end where typing left the caret (ADR-0051, 2026-10-12); while pointing it is asked over the whole Reference the outline wrote, rewrites it, and pointing goes on with the caret at its end. Without the function, or with no edit open, F4 is not claimed. No JavaScript use is added (ADR-0051, 2026-09-29; ADR-0021) | Layer 2 + Layer 3 on the Server host with 150 ms injected | as stated; four F4 presses in a burst give the four forms in order |
```

### 12. docs/definition-of-done.md

Old:

```
- **SH-28's edge cases, and SH-51 and SH-52's by-hand halves, are run on Windows** (the seventh run's
  Parts A and B, ticket 160), and a disagreement is settled in ADR-0051 or ADR-0100 first.
```

New:

```
- **SH-28's edge cases, and SH-51 and SH-52's by-hand halves, are run on Windows** (the seventh run's
  Parts A and B, ticket 160), and a disagreement is settled in ADR-0051 or ADR-0100 first. *(SH-28's
  were run in the seventeenth, Part C, and the readings of 2026-10-12, R1, and settled in ADR-0051 on
  2026-10-12.)*
```

### 13. docs/definition-of-done.md

Old:

```
| **SH-28** | MUST | F4 cycles the Reference at the caret as ADR-0051 says: `A1` → `$A$1` → `A$1` → `$A1` → `A1`; a range as one, from its first end; every Reference a selection covers, overlaps or touches at either end (`+` selected in `=A1+B1` gives `=$A$1+$B$1`); whole columns and rows in two forms; the Sheet qualifier kept; a structured reference, a function name, a number and text that is not a Formula unchanged; the caret at the end of the rewritten Reference. Until the seventh Windows run, the edge cases are readings (ADR-0051, 2026-09-29) | Layer 1 + Layer 2; Layer 3 beside Excel after the run | as stated |
```

New:

```
| **SH-28** | MUST | F4 cycles the Reference at the caret as ADR-0051 says: `A1` → `$A$1` → `A$1` → `$A1` → `A1`; of a cell range, the end at the caret, from its own form (`=SUM(A1:B2)`, the caret after `B2`, gives `=SUM(A1:$B$2)`); every Reference, and every end of a range, a selection covers, overlaps or touches at either end, to the first one's next form (`+` selected in `=A1+B1` gives `=$A$1+$B$1`), and a range being pointed at whole (`=D11:D12` gives `=$D$11:$D$12`); whole columns and rows in two forms; the Sheet qualifier kept; the rewritten Reference's column letters in upper case (`=b2` gives `=$B$2`); a structured reference, a function name, a number and text that is not a Formula unchanged; the rewritten Reference selected where the caret had been moved or a selection made, and a caret at its end where typing left the caret, in the cell and in the Formula Bar. The edge cases are Excel's as the seventeenth Windows run (Part C) and the readings of 2026-10-12 (R1) observed them (ADR-0051, 2026-10-12) | Layer 1 + Layer 2 + Layer 3 | as stated |
```

## Docs Site

### 14. samples/ExGrid.Docs/Pages/Sheet/SheetFormulas.razor

Old:

```
                <b>Pointing.</b> Wherever a Reference can go — after <code>=</code>, an operator, <code>(</code> or
                <code>,</code> — the arrow keys and the mouse point at cells and write their Reference. Shift and an
                arrow, or a drag, writes a range. F4 cycles the Reference at the caret through <code>$A$1</code>,
                <code>A$1</code>, <code>$A1</code> and <code>A1</code>.
```

New:

```
                <b>Pointing.</b> Wherever a Reference can go — after <code>=</code>, an operator, <code>(</code> or
                <code>,</code> — the arrow keys and the mouse point at cells and write their Reference. Shift and an
                arrow, or a drag, writes a range, and Shift+Home a range to the row's first column. Typed into the
                Formula Bar, the arrow keys move the caret instead, as they do in Excel's bar, until <kbd>F2</kbd>
                points. F4 cycles the Reference at the caret through <code>$A$1</code>, <code>A$1</code>,
                <code>$A1</code> and <code>A1</code>; in a range typed out only the end at the caret cycles, and its
                column letters are written in upper case, while a range being pointed at cycles whole. Where the caret
                was moved with a key or a press, the rewritten Reference is left selected, so the next F4 cycles it
                again; where typing left the caret, the caret stands after it.
```

### 15. samples/ExGrid.Docs/Pages/KeyboardByHand.cs

Old:

```
        new("Editing", "[Enter] / [Tab] while editing", "Commit, and move on as [Enter] and [Tab] do, cycling inside the selection.", Editing,
            ["ADR-0010", "ADR-0012"]),
```

New:

```
        new("Editing", "[Enter] / [Tab] while editing", "Commit, and move on as [Enter] and [Tab] do, cycling inside the selection.", Editing,
            ["ADR-0010", "ADR-0012"]),
        new("Editing", "Arrow keys, [Shift] + arrow, [Shift] + [Home] or [Ctrl] + arrow while typing over a value",
            "Commit, then move or extend the selection as the same key does with no edit open. After [F2] in the cell they move the caret, or select text.", Editing,
            ["ADR-0010", "ADR-0012"]),
```

### 16. samples/ExGrid.Docs/Pages/KeyboardByHand.cs

Old:

```
            "Point at a cell and write its Reference where one can go. With [Shift], write a range.",
```

New:

```
            "Point at a cell and write its Reference where one can go. With [Shift], write a range; [Shift] + [Home] writes one to the row's first column.",
```

### 17. samples/ExGrid.Docs/Pages/KeyboardByHand.cs

Old:

```
        new("Entries and Formulas", "[F4] while a Formula is typed", "Cycle the Reference at the caret through $A$1, A$1, $A1 and A1.",
```

New:

```
        new("Entries and Formulas", "[F4] while a Formula is typed", "Cycle the Reference at the caret through $A$1, A$1, $A1 and A1 — in a range, the end at the caret; a range being pointed at, whole. After the caret was moved with a key or a press, it is left selected.",
```

### 18. samples/ExGrid.Docs/Pages/Grid/GridEditing.razor

Old:

```
            edit commits. <kbd>Enter</kbd> and <kbd>Tab</kbd> commit and move on, cycling inside the selection, so a
            block can be filled by typing; <kbd>Escape</kbd> cancels.
```

New:

```
            edit commits. <kbd>Enter</kbd> and <kbd>Tab</kbd> commit and move on, cycling inside the selection, so a
            block can be filled by typing; <kbd>Escape</kbd> cancels. While typing over a value, the arrow keys,
            <kbd>Home</kbd> and <kbd>End</kbd> commit and move, and <kbd>Shift</kbd> with an arrow or with
            <kbd>Home</kbd>, and <kbd>Ctrl</kbd> with an arrow, commit and then extend the selection or go to the
            edge from the edited cell, as they do with no edit open — as Excel's Enter mode was seen to do with
            <kbd>Shift</kbd> and an arrow, <kbd>Shift</kbd>+<kbd>Home</kbd> and <kbd>Ctrl</kbd>+<kbd>→</kbd>.
            After <kbd>F2</kbd> in the cell they move the caret, or select text, instead.
```

## Readings taken while building, and choices of this area

- **Which caret selects after F4.** Built: a caret the user moved in that text (the key message's
  existing flag, set by a caret key or a press, cleared by typing) or a selection carried selects; any
  other caret stays a caret. It agrees with every observed case (Part C, cases 1 to 14 and 18 to 22,
  typed and with F2; R1 (iv) and (v)). Another rule agrees with them too — a caret at the end of the text stays a
  caret, and any other selects — and the two differ only where nothing was observed: the caret moved
  back to the end by a key, and a caret moved and then typed at. Asked below.
- **A press in the text counts as a move; a caret where F2 opened the edit does not** — by
  generalisation of the cases above.
- **The Primary Modifier's arrows where a Reference can go are claimed and do nothing** — this area's
  choice, so that a Formula ending too early is not committed; Excel was not asked.
- **The Primary Modifier's ←, ↑ and ↓, with Shift or without, and Shift+↑, in Overwrite** — by
  generalisation of R1 (vii)'s →, and of Shift+→, ↓ and ←.
- **Shift+Home after Shift+↓ has written a range while pointing** extends from the outline's fixed end
  (`=D11:D12`, Shift+Home gives `=A11:D12`), as Shift+Home extends a Selection — a reading.
- **A list open over Point still leaves Shift+Home to the editor** (ADR-0058's set, Q51, is unchanged),
  and every open list leaves the Primary Modifier's arrows to it.
- **The span with a Sheet qualifier** starts at the qualifier (`Sheet2!$A$1` selected): case 10 was
  typed, so a selection there was not seen.

## Questions for the next Windows run

Each is asked of Excel 16 on Windows, with the Selection, the Name Box, the text and the caret or
selection in it read through UI Automation after each key, and no key sent to read them:

1. **F2 in the Formula Bar, then F4.** `=A1+B2` typed into the bar, F2 (Enter mode), F4: a caret at
   the end, or `$B$2` selected?
2. **Which caret selects.** In the bar: `=A1+B2`, ←, → (the caret back at the end), F4 — selected, or
   a caret? `=A1+B2`, ←, ←, ←, `0` typed (`=A10+B2`, the caret after `A10`), F4 — selected, or a
   caret? A press into the bar's text right after `A1` of an existing `=A1+B2`, F4?
3. **Shift+Home while pointing at a range.** In D10, `=`, ↓, Shift+↓ (`=D11:D12`), Shift+Home: does
   the outline become `A11:D12`, `A11:D11`, or something else?
4. **F4 in Edit mode on a Reference with a Sheet qualifier.** `=Sheet2!A1` with F2 first, the caret
   after `A1`, F4: does the selection start at `Sheet2!` or at `$A$1`?
5. **The Primary Modifier's arrows where a Reference can go.** In D10, `=` then Ctrl+↓: does Excel
   point (and where), commit, or do nothing? And Ctrl+Shift+↓?
6. **The grey ground right after `=`.** In D10, `=`, ↓, and `=`, ↓, ↓: is the pointed Reference on the
   grey ground, and what does UI Automation give as the bar's selection? (The eighth run's case 31
   saw no grey and an empty selection; R1 (vi) pictured the grey.)
7. **The directions R1 (vii) did not ask.** `abc` typed into D10, each from a fresh entry: Shift+↑,
   Ctrl+←, Ctrl+↑, Ctrl+↓, Ctrl+Shift+←, Ctrl+Shift+↑ and Ctrl+Shift+↓ — committed, and the
   Selection moved or extended as with no edit open? (Built by generalisation of →, ↓ and ←.)
8. **`=` then Shift+Home, with no key between.** R1 read it with a Ctrl+C before Shift+Home and a
   Ctrl+C after it (`A10:D10`); the text, its selection and the mode were not read: `=A10:D10`, in
   Point?

## Files touched outside the area's list

- `tests/ExGrid.Components/ShippedStylesheetTests.cs`: two regexes that pin `movedByUser` and
  `setCaret` as written, updated to the fix (C-diff-7), and the one that pins `pointKeys`, which now
  takes Shift+Home (R1 (viii)).
- `tests/ExGrid.Components/OverwriteShiftKeyTests.cs` is new (named in the list).
- `tests/ExSheet.Components/Support/SheetTestContext.cs`: its key helper carries the "moved" flag
  and Ctrl.
- `tests/ExGrid.Components/PointModeTests.cs`: one comment.
