# 10: Completion and the argument hint

Status: ready-for-agent

**What to build:** The core reports the editor's text and caret as the user types (a Blazor input event). The
Consumer answers with candidates and a hint. Chrome paints the list as the editor's Inner Popup,
inside the grid's box, and the hint beneath it. ↑/↓ choose, Tab accepts, and Escape closes the list
before it cancels. ExSheet supplies the function list and, once ticket 16 lands, the Linked Tables'
names. Candidates that come back for text that has since changed are dropped.

**Blocked by:** 04, 09

- [ ] `=SU` offers `SUM` and `SUMIF`-style candidates from the declared list; Tab accepts (ADR-0051)
- [ ] Escape closes the list and leaves the edit open
- [ ] The list stays inside the grid's box (ADR-0040) under the built-in Chrome and `ExGrid.MudBlazor`'s
- [ ] A stale candidate list is never shown (layer 2 with a delayed answer)
- [ ] Works from the Formula Bar as from the cell

## Comments

2026-09-27, engine half: `FormulaEntry.Complete(text, caret, linkedTables)` (and
`Sheet.Complete(text, caret)`, which passes the Sheet's own Linked Tables) returns the
declared functions and Linked Table names beginning with the name being typed at the caret,
without regard to case, in one alphabetical list, with the span of text accepting a candidate
replaces and what it writes (`SUM(` for a function, the name for a table). It offers nothing
inside text in quotes, a Reference with `$`, a number, a column in brackets, a name that does
not stand where an operand can start, or when nothing matches. `FormulaEntry.HintAt(text,
caret)` returns the innermost declared function whose argument list holds the caret, the
argument index counted by the commas at its own depth, and that argument's name as
`DeclaredFunction.Arguments` lists it. Both work on unfinished text and never require the
Formula to parse (`FormulaEntryTests`). The first criterion's `=SU` offers `SUM` only: the
declared set has no `SUMIF` (ADR-0047). **What remains is the component's:** reporting the
text and caret as the user types, the Inner Popup and the hint painted by Chrome, ↑/↓/Tab/Escape,
dropping a stale list, and all of it from the Formula Bar too.
