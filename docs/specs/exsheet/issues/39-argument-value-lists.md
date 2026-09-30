# 39: Argument value lists, and the list after `Table[`

Status: done

**What to build:** ADR-0058, "Completion, aligned with Excel", and ADR-0051's note of 2026-09-30.
At an argument whose values are a fixed list, completion lists the values, as Excel was seen to
(`verification/2026-09-27-windows-excel/behaviours.md`, item 14). The ninth Windows run
(`verification/2026-09-30-windows-excel-9/pointing.md`, cases 9, 11, 12, 13) added the list after
`Table[` and Backspace, and gave `search_mode`'s texts.

**Blocked by:** None (can start immediately)

- [x] The engine's function declarations can say that an argument takes one of a fixed list of
      values, each with Excel's text (`FunctionDefinition` holds the arguments as one display string
      today) (SH-36)
- [x] `XLOOKUP`'s `match_mode`, with the texts observed: `0 - Exact match`, `-1 - Exact match or next
      smaller item`, `1 - Exact match or next larger item`, `2 - Wildcard character match`,
      `3 - Regex match` (SH-36)
- [x] `XLOOKUP`'s `search_mode`, with the texts observed: `1 - Search first-to-last`,
      `-1 - Search last-to-first`, `2 - Binary search (sorted ascending order)`,
      `-2 - Binary search (sorted descending order)` (SH-36)
- [x] `FormulaEntry.Complete` offers the list when the caret stands at such an argument, with nothing
      typed or with a prefix of a value. Tab writes the value (its number), not the text (SH-36)
- [x] Nothing is listed after `=`, an operator, `(` or `,` at any other argument, so ↓ still points
      (SH-36, ADR-0051)
- [x] After `Table[`, the table's column names, and nothing else: not `@`, `#All`, `#Data`, `#Headers`
      or `#Totals`, which Excel lists and ExSheet's grammar refuses. `FormulaEntry.Complete` returns
      nothing inside brackets today (`token.HasBrackets`) (SH-36)
- [x] Backspace back into a name lists again, as Excel does (case 11). Check first; it may already
      hold (SH-36)
- [x] F3 is not claimed (SH-36)
- [x] Layer 1 over the engine; Layer 2 for ↑/↓, Tab and Escape (SH-36)

## Comments

2026-09-30, implementation: the engine's `FunctionDefinition` gains `Values`, the arguments that
take one of a fixed list by index, and `XLOOKUP` declares `match_mode` (4) and `search_mode` (5)
with the texts the Windows runs observed. The public view is `DeclaredFunction.ValuesOf(index)`,
a list of `ArgumentValue(Value, Text)`, empty for any other argument. `CompletionKind` gains
`LinkedTableColumn` and `ArgumentValue`. `FormulaEntry.Complete` gains an overload with
`columnsOf`, the columns of the table a name names; the overload with names alone lists nothing
after `Table[`, and `Sheet.Complete` passes its own tables' columns.

- **Value lists.** The list opens where the innermost call is a declared function whose argument
  at the caret takes a list, and that argument holds nothing yet, or the beginning of one of its
  values and nothing else (`-` at `search_mode` lists `-1` and `-2`). Accepting writes the value
  over the whole of what the argument holds (`,|1)` replaces the `1`). A value typed whole with
  the caret after it lists nothing: nothing is left to choose, and a list there would come back
  after every Tab, so Tab could never move on. This refines "with a prefix of a value"; it is a
  reading, not a decision recorded anywhere.
- **Columns after `Table[`.** Listed in the table's order, beginning with what is typed there
  (`'` escapes read), inside the one pair of brackets the grammar reads; `#`, `@` or a second `[`
  lists nothing. Accepting writes the column's name, escaped with `'` before `[`, `]`, `#`, `@`
  and `'`, and leaves `]` to the user, as a table's name leaves `[`. What Excel writes on Tab here
  was not observed.
- **Backspace** already held: the grid asks again on every change of the text, and
  `Complete("=Posi")` lists the table. Now pinned at both layers.
- **F3**: nothing claims it. Pinned by reading the shipped listener (no `F3` in it) and the keys
  the grid is told to claim.
- **The core needed no change.** An open list already wins over Point: `GateMode()` answers
  `completion` before `point`, `OnEditingKeyAsync` gives an open list its keys before
  `OnPointKey`, the listener reads a painted list (`listShown()`) before the mode it was told, and
  `WritePointedReference` closes the list when a press points.

Layer 1: `CompletionTriggerTests` (68 cases). Layer 2: `CompletionTriggerWiringTests` (12): the
list and its texts, ↑/↓, Tab writing `-1` and replacing a typed `-`, Escape then ↓ pointing, a
press pointing while the list is open, nothing at another argument, `Table[`, Backspace, F3.
Layer 3 for SH-36 is not written or run here.
