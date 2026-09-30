# 39: Argument value lists, and the list after `Table[`

Status: ready-for-agent

**What to build:** ADR-0058, "Completion, aligned with Excel", and ADR-0051's note of 2026-09-30.
At an argument whose values are a fixed list, completion lists the values, as Excel was seen to
(`verification/2026-09-27-windows-excel/behaviours.md`, item 14). The ninth Windows run
(`verification/2026-09-30-windows-excel-9/pointing.md`, cases 9, 11, 12, 13) added the list after
`Table[` and Backspace, and gave `search_mode`'s texts.

**Blocked by:** None (can start immediately)

- [ ] The engine's function declarations can say that an argument takes one of a fixed list of
      values, each with Excel's text (`FunctionDefinition` holds the arguments as one display string
      today) (SH-36)
- [ ] `XLOOKUP`'s `match_mode`, with the texts observed: `0 - Exact match`, `-1 - Exact match or next
      smaller item`, `1 - Exact match or next larger item`, `2 - Wildcard character match`,
      `3 - Regex match` (SH-36)
- [ ] `XLOOKUP`'s `search_mode`, with the texts observed: `1 - Search first-to-last`,
      `-1 - Search last-to-first`, `2 - Binary search (sorted ascending order)`,
      `-2 - Binary search (sorted descending order)` (SH-36)
- [ ] `FormulaEntry.Complete` offers the list when the caret stands at such an argument, with nothing
      typed or with a prefix of a value. Tab writes the value (its number), not the text (SH-36)
- [ ] Nothing is listed after `=`, an operator, `(` or `,` at any other argument, so ↓ still points
      (SH-36, ADR-0051)
- [ ] After `Table[`, the table's column names, and nothing else: not `@`, `#All`, `#Data`, `#Headers`
      or `#Totals`, which Excel lists and ExSheet's grammar refuses. `FormulaEntry.Complete` returns
      nothing inside brackets today (`token.HasBrackets`) (SH-36)
- [ ] Backspace back into a name lists again, as Excel does (case 11). Check first; it may already
      hold (SH-36)
- [ ] F3 is not claimed (SH-36)
- [ ] Layer 1 over the engine; Layer 2 for ↑/↓, Tab and Escape (SH-36)
