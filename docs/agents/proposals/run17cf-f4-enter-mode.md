# Proposal: F4 at the edges, and Overwrite's Shift keys (run 17, Part C; area f4-enter-mode)

Findings C-diff-2 to C-diff-8 of the seventeenth Windows run
(`verification/2026-10-11-windows-17/report.md`, Part C, "The differences", on branch
`claude/exsheet-windows-verify-17`). SH-28 and ADR-0202 say a disagreement with Excel is settled in
ADR-0051 first; the implementation on this branch follows Excel, and the text below is what the
orchestrator applies to the ADRs, the Definition of Done and the Docs Site. Each edit is an exact
old → new.

## What was built

- **C-diff-4, C-diff-5, C-diff-6** — the two ends of a cell range are References of their own
  (`FormulaEntry.CycleReference`): a caret cycles only the end it touches (`A1|:B2` touches `A1`,
  `A1:|B2` touches `B2`), from that end's own form; a selection cycles every end it covers, overlaps or
  touches, each to the next form of the first. Whole columns and whole rows stay one Reference of two
  forms. A Sheet qualifier stands before the first end.
- **C-diff-8** — the rewritten Reference's column letters are written in upper case; the rest of the
  text, a Sheet qualifier and the other end of a range among it, is left as typed.
- **C-diff-3** — the engine answers the span of what it rewrote for a caret as for a selection; the core
  (`OnCycleReferenceKey`) places it as a selection while the edit is in Caret — after F2 in a cell,
  and in the Formula Bar until F2 takes it to Enter mode — and collapses it to a caret at its end in
  Overwrite and while pointing. `CycleReference`'s signature and `EditorRewrite` are unchanged; no
  public member was added (ADR-0201), but both seams answer differently, and each has a Change Note
  (below). The placement-in-flight rule carries the selection as it carries a caret.
- **C-diff-7** — `ex-grid.js` forgets the user's caret move once the field's value is seen to differ
  from the text it was made in (`movedByUser`, and `setCaret` reads it before comparing), so an equal
  text written later by F4's cycle is not taken for the one the move was made in.
- **C-diff-2** — in Overwrite the gate claims Shift+Home and the four Shift+arrows. Where a Reference
  can go they point: a Shift+arrow as Point does with it, and Shift+Home from the edited cell to the
  row's first column (a reading, below). Elsewhere they commit and then extend the Selection exactly
  as with no edit open, a Reject holding the commit; a commit whose own handler moved the order has
  dropped the Selection, and they extend nothing then, as Enter and the arrows move nothing
  (ADR-0142). Caret, and a list of names open in Overwrite, keep them the editor's, and Point keeps
  Shift+Home the editor's, as it was.

## ADR-0051, "F4 cycles the Reference at the caret"

**1. The lead of the readings.**

Old:

```
**Readings, until the seventh Windows run observes them.** The rules above are Excel's documented
behaviour. These are the closest readings of what it does at the edges, taken for the
implementation and asked of Excel in `docs/specs/exsheet/verify-on-windows-7.md`:
```

New:

```
**What Excel does at the edges** *(observed by the seventeenth Windows run, 2026-10-11, Excel
16.0.20430 on Windows 11, `verification/2026-10-11-windows-17/report.md`, Part C; settled here
2026-10-12, as SH-28 and ADR-0202 ask)*. The rules above are Excel's documented behaviour. The
bullets below were first the closest readings of what it does at the edges, taken for the
implementation and asked of Excel in `docs/specs/exsheet/verify-on-windows-7.md`; the seventeenth
run put them to Excel, typed into a cell (Enter mode) and with F2 first (Edit mode). Where a reading
was wrong, the bullet says what Excel does and keeps what was read:
```

**2. "The Reference at the caret".**

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
  name, a number, a string, or text that is not a Formula (observed, cases 11 to 14).
```

**3. "A range cycles as one".**

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
  as it cycles every Reference it covers (below), each to the first end's next form. Whole columns
  and whole rows stay one Reference (below).
```

**4. "The text keeps the case".**

Old:

```
- **The text keeps the case and the order it was typed in**, since only `$` signs are written:
  `=b2` gives `=$b$2`, and `=B2:A1` gives `=$B$2:$A$1`.
```

New:

```
- **The rewritten Reference's column letters are written in upper case; the rest of the text keeps
  the case and the order it was typed in** *(corrected after the seventeenth Windows run, cases 21
  and 22. The reading was that only `$` signs are written, so that `=b2` gave `=$b$2`; Excel gives
  `=$B$2`)*. `=B2:A1` gives `=B2:$A$1`, its order kept. A Sheet qualifier is not rewritten and keeps
  its case: it was not asked.
```

**5. "After F4, the caret is at the end of the rewritten Reference".**

Old:

```
- **After F4, the caret is at the end of the rewritten Reference**, or, from a selection, the
  selection covers what was rewritten.
```

New:

```
- **After F4 in Edit mode the rewritten Reference is selected; in Enter mode a caret stands at its
  end** *(corrected after the seventeenth Windows run. The reading was a caret at the end in every
  mode. With F2 first, Excel selected what it rewrote: `$A$1` in cases 2 to 4, `$B$2` in case 5, the
  whole `$A:$A` and `$1:$1` in cases 8 and 9; typed, in cases 1, 10, 13, 21 and 22, it left a caret
  at the end)*. The Consumer answers the span of what was rewritten, a Sheet qualifier included
  before the first end, for a caret as for a selection. The grid selects it while the edit is in
  Caret, Excel's Edit mode: after F2 in a cell, and in the Formula Bar until F2 takes it to Enter
  mode (above, 2026-09-30). In Overwrite — typed into a cell, or in the bar after F2 — and while
  pointing it places a caret at the span's end. A placement still in flight carries the selection
  as it carries a caret (the rule settled while building, above), so the next F4 cycles the same
  Reference. *(The Formula Bar's own F4 was not asked of Excel; the bar follows the cell's mode
  until it is: Edit mode, and Enter mode after F2. Whether Edit mode's selection starts at a Sheet
  qualifier was not asked either: case 10 was typed.)*
```

Left to the orchestrator, as the triage says: "After F4 while pointing" (case 15b observed `=A3`,
the relative form, the reading's first option), the selection bullet's "still read" parenthetical
(cases 7 and 18 to 20 agree), and the Enter-mode note (← and Home commit before F4 in Enter mode).
The bullets "Whole columns and whole rows have two forms", "A Sheet qualifier is kept" and "A
structured reference does not cycle" read as Excel did (cases 8 to 11) and may take "(observed)".

## ADR-0010, a note under the key table of "The core arbitrates the keys"

Inserted after the table's last row (`| Everything else | the editor |`), before "Bubbling is too
late":

```
*(2026-10-12, after the seventeenth Windows run, Part C, case 7.)* **In Overwrite the core also takes
Shift+Home and the four Shift+arrows.** Typed into a cell, Excel's Enter mode committed `=A1+B2` on
Shift+Home and extended the Selection, which the Shift+→ after it narrowed to `B10:D10`; ExGrid left
the key to the editor, which selected its text, and F4 then rewrote both References. Where the
Consumer says a Reference can go, they point
([ADR-0051](./0051-formula-entry-completion-point-mode-and-the-formula-bar.md)): a Shift+arrow
extends a pointing outline from the edited cell, as Point does with it, and Shift+Home points from
the edited cell to the row's first column (a reading: Excel was not asked where a Reference can go).
Once pointing, Shift+Home stays the editor's, as it was: Point does not take it until Excel is asked.
Elsewhere they commit, and then extend the Selection exactly as the same key does with no edit open
([ADR-0012](./0012-anchor-focus-and-keyboard-navigation.md)); a Reject holds the commit, as it does
for the arrows ([ADR-0034](./0034-validation-is-a-consumer-verdict-enforced-only-at-the-editor.md)),
and a commit whose own handler moved the order has dropped the Selection the key was aimed with, so
the key extends nothing, as Enter and the arrows move nothing then
([ADR-0142](./0142-a-write-lands-as-the-user-entered-it-and-a-change-under-the-editor-is-told.md)).
Caret keeps them the editor's, and so does a list of names open in Overwrite
([ADR-0058](./0058-a-formula-points-across-grids-through-a-pointing-scope.md), Q51). Only Shift+Home
was observed committing; the Shift+arrows are Excel's documented meaning in Enter mode, and the next
ask of Excel confirms them. Shift+End and the Primary Modifier's arrows stay the editor's until it
answers.
```

## Definition of Done

**SH-28's statement** (the Verification and Pass columns unchanged, or "Layer 1 + Layer 2 + Layer 3"
in Verification now that the run is in):

Old:

```
F4 cycles the Reference at the caret as ADR-0051 says: `A1` → `$A$1` → `A$1` → `$A1` → `A1`; a range as one, from its first end; every Reference a selection covers, overlaps or touches at either end (`+` selected in `=A1+B1` gives `=$A$1+$B$1`); whole columns and rows in two forms; the Sheet qualifier kept; a structured reference, a function name, a number and text that is not a Formula unchanged; the caret at the end of the rewritten Reference. Until the seventh Windows run, the edge cases are readings (ADR-0051, 2026-09-29)
```

New:

```
F4 cycles the Reference at the caret as ADR-0051 says: `A1` → `$A$1` → `A$1` → `$A1` → `A1`; of a cell range, the end at the caret, from its own form (`=SUM(A1:B2)`, the caret after `B2`, gives `=SUM(A1:$B$2)`); every Reference, and every end of a range, a selection covers, overlaps or touches at either end, to the first one's next form (`+` selected in `=A1+B1` gives `=$A$1+$B$1`); whole columns and rows in two forms; the Sheet qualifier kept; the rewritten Reference's column letters in upper case (`=b2` gives `=$B$2`); a structured reference, a function name, a number and text that is not a Formula unchanged; the rewritten Reference selected while the edit is in Caret, Excel's Edit mode — after F2 in a cell, and in the Formula Bar until F2 takes it to Enter mode — and a caret at its end in Overwrite and while pointing. The edge cases are the seventeenth Windows run's observations of Excel (ADR-0051, 2026-10-12; `verification/2026-10-11-windows-17/report.md`, Part C)
```

**DC-45's statement**:

Old:

```
writes back the text the function answers and places the selection it answers; while pointing it rewrites the Reference the outline wrote and pointing goes on.
```

New:

```
writes back the text the function answers and places the selection it answers — as a selection while the edit is in Caret, and as a caret at its end in Overwrite and while pointing (ADR-0051, 2026-10-12); while pointing it rewrites the Reference the outline wrote and pointing goes on.
```

**ED-2's statement**:

Old:

```
Both editing states exist and the arrow keys mean different things in each: **Overwrite** commits and moves, **Caret** moves the caret (ADR-0010)
```

New:

```
Both editing states exist and the arrow keys mean different things in each: **Overwrite** commits and moves, and its Shift+arrows and Shift+Home commit and extend the Selection, or point where a Reference can go; **Caret** moves the caret, and the Shift forms select text (ADR-0010, note of 2026-10-12)
```

Optionally, the list before the SH table, which the run has now discharged for SH-28:

Old:

```
- **SH-28's edge cases, and SH-51 and SH-52's by-hand halves, are run on Windows** (the seventh run's
  Parts A and B, ticket 160), and a disagreement is settled in ADR-0051 or ADR-0100 first.
```

New:

```
- **SH-28's edge cases, and SH-51 and SH-52's by-hand halves, are run on Windows** (the seventh run's
  Parts A and B, ticket 160), and a disagreement is settled in ADR-0051 or ADR-0100 first. *(SH-28's
  were run in the seventeenth, Part C, and settled in ADR-0051 on 2026-10-12.)*
```

## Docs Site

**`samples/ExGrid.Docs/Pages/Sheet/SheetFormulas.razor`**, "Completion, hints and pointing":

Old:

```
                arrow, or a drag, writes a range. F4 cycles the Reference at the caret through <code>$A$1</code>,
                <code>A$1</code>, <code>$A1</code> and <code>A1</code>.
```

New:

```
                arrow, or a drag, writes a range. F4 cycles the Reference at the caret through <code>$A$1</code>,
                <code>A$1</code>, <code>$A1</code> and <code>A1</code>; in a range, only the end at the caret
                cycles, and its column letters are written in upper case. In Excel's Edit mode — after
                <kbd>F2</kbd> in a cell, and in the Formula Bar until <kbd>F2</kbd> takes it to Enter mode — the
                rewritten Reference is left selected, so the next F4 cycles it again; typing into a cell, and while
                pointing, the caret stands after it.
```

**`samples/ExGrid.Docs/Pages/KeyboardByHand.cs`**, the F4 row:

Old:

```
        new("Entries and Formulas", "[F4] while a Formula is typed", "Cycle the Reference at the caret through $A$1, A$1, $A1 and A1.",
```

New:

```
        new("Entries and Formulas", "[F4] while a Formula is typed", "Cycle the Reference at the caret through $A$1, A$1, $A1 and A1 — in a range, the end at the caret. After [F2] in a cell, and in the Formula Bar until [F2], it is left selected.",
```

And a row in the ExGrid "Editing" group, after `[Enter] / [Tab] while editing` (the `Editing` route
the rows beside it use; no `Canonical`, as the rows for keys while editing have none):

```
        new("Editing", "Arrow keys, [Shift] + arrow or [Shift] + [Home] while typing over a value",
            "Commit, then move or extend the selection as the same key does with no edit open. After [F2] in the cell they move the caret, or select text.", Editing,
            ["ADR-0010", "ADR-0012"]),
```

**`samples/ExGrid.Docs/Pages/Grid/GridEditing.razor`**, "Editable columns and the Edit Intent", the
first paragraph:

Old:

```
            edit commits. <kbd>Enter</kbd> and <kbd>Tab</kbd> commit and move on, cycling inside the selection, so a
            block can be filled by typing; <kbd>Escape</kbd> cancels.
```

New:

```
            edit commits. <kbd>Enter</kbd> and <kbd>Tab</kbd> commit and move on, cycling inside the selection, so a
            block can be filled by typing; <kbd>Escape</kbd> cancels. While typing over a value, as in Excel's Enter
            mode, the arrow keys, <kbd>Home</kbd> and <kbd>End</kbd> commit and move, and <kbd>Shift</kbd> with an
            arrow or with <kbd>Home</kbd> commits and extends the selection from the edited cell; after
            <kbd>F2</kbd> in the cell they move the caret, or select text, instead.
```

## Readings taken while building, and what the next ask of Excel covers

- **The Formula Bar's F4** (`ask_excel` C-diff-10): the bar is in Caret until F2 takes it to Enter
  mode, so F4 there selects the rewritten Reference, as Excel's Edit mode does in the cell, and after
  F2 in the bar leaves a caret at its end, as Enter mode does. Not yet observed in Excel's bar.
- **Shift+arrows in Enter mode** (`ask_excel` C-diff-10 (vii)): built as commit and extend, Excel's
  documented meaning; only Shift+Home was observed (case 7).
- **Shift+Home where a Reference can go** — a reading this area took to give Overwrite's Shift+Home
  what Point does with the Shift+arrows: from Overwrite, with no outline standing, it points from the
  edited cell to the row's first column (`=` then Shift+Home in D10 writes `=A10:D10`). Point keeps
  its earlier behaviour: its key set does not take Shift+Home, which stays the editor's while
  pointing, and one that reaches the core then neither moves the outline nor commits. *(As first
  built on this branch Point took it too, extending the outline to the row's first column, because
  its key set was Overwrite's and the four Shift+arrows. No run observed that, and the brief did not
  ask it; the verifier's finding withdrew it.)* A list open over Point still leaves Shift+Home to
  the editor (ADR-0058's set is unchanged).
- **The span with a Sheet qualifier** starts at the qualifier (`Sheet2!$A$1` selected in Edit mode):
  case 10 was typed, so Excel's Edit mode was not seen here.
- **Shift+End and the Primary Modifier's arrows** in Overwrite are unchanged (the editor's) until the
  ask answers.

## Questions for the next Windows run

Each is asked of Excel 16 on Windows, typed into a cell (Enter mode) unless it says otherwise, with the
Selection, the Name Box, the cell's text and the caret or selection in it recorded after each key:

1. **The Formula Bar's F4.** `=A1+B2` typed into the bar, the caret after `B2`, F4: is `$B$2`
   selected? Then F2 in the bar (Enter mode), F4 again: a caret at its end, or selected?
2. **The Shift+arrows in Enter mode** (`ask_excel` C-diff-10 (vii)). `=A1+B2` typed into D10, then
   Shift+→ (and Shift+←, Shift+↑, Shift+↓ each from a fresh entry): committed, and the Selection
   extended from D10?
3. **`=` then Shift+Home** in D10: does Excel point `=A10:D10`, commit, or select text?
4. **Shift+Home while pointing.** In D10, `=` then ↓ (pointing `=D11`), then Shift+Home: does the
   outline extend to `A11:D11`, does it move, does the entry commit, or does the key select text? And
   the same after Shift+↓ has already written a range (`=D11:D12`).
5. **F4 in Edit mode on a Reference with a Sheet qualifier.** `=Sheet2!A1` with F2 first, the caret
   after `A1`, F4: does the selection start at `Sheet2!` or at `$A$1`?
6. **Shift+End and Ctrl+arrows in Enter mode** (`=A1+B2` typed into D10): committed and extended, or
   the editor's?

## Files touched outside the area's list

- `tests/ExGrid.Components/ShippedStylesheetTests.cs`: two regexes that pin `movedByUser` and
  `setCaret` as written, updated to the fix (C-diff-7), and the one that pins `pointKeys`, which is now
  written out so that it does not take Overwrite's Shift+Home (C-diff-2).
- `tests/ExGrid.Components/OverwriteShiftKeyTests.cs` is new (named in the list).
