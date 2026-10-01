# 74: The value list as the thirteenth run saw it

Status: ready-for-agent

**What to build:** ADR-0058, "What the thirteenth Windows run settled", its first two bullets (Q54,
Q55), and SH-36 as rewritten on 2026-10-01. The thirteenth run
(`verification/2026-10-01-windows-13/pointing-scope.md`, Part A groups 1 and 2, and the additions that
typed them into ExSheet) found Excel listing the values for text ExSheet listed names for, or nothing.

**Blocked by:** None.

- [x] At an argument whose values are a fixed list, text that is not a number lists every value, with
      the first selected: at `match_mode`, `A1`, `1+`, `X`, `AV`, `Positions` and `"` each list the five
      values, `0 - Exact match` selected; at `search_mode`, `A` lists its four. Letters there list no
      function and no table (SH-36)
- [x] A number lists the value it is, alone and selected, or nothing when it is no value: `4` at
      `match_mode` and `5` at `search_mode` list nothing; `-1` lists `-1 - Exact match or next smaller
      item` alone, as today. The caret rule of ticket 70 stands: with the caret before or inside a
      value, nothing is listed (SH-36)
- [x] White space after the caret is something of the argument: with the caret before two spaces
      (`=XLOOKUP(1,A2:A4,B2:B4,,  )`, F2, ←←←) nothing is listed, and Tab commits the Formula with the
      spaces kept and moves on, as Tab does with no list open (SH-36)
- [x] Tab on a listed value writes it over the whole typed text of the argument (`X` then Tab writes
      `0`), and closes the list; the list is not opened again on what Tab wrote (SH-36)
- [x] Outside a value-list argument nothing changes: a letter after `=`, an operator, `(` or `,` lists
      functions and Linked Tables by prefix, `Table[` lists columns, and nothing is listed after `=`, an
      operator, `(` or `,` before anything is typed (SH-36)
- [ ] Layer 1 for the rule (each text of the run, at both arguments, and the spaces); Layer 2 for Tab
      over typed text; Layer 3 on `/sheet` under both Chromes: `,,X` lists the five values, `0`
      selected, and Tab writes `0`; `,,4` lists nothing; the spaces case commits (SH-36)

## Comments

2026-10-01, implemented on `agent/ps-74`.

- **The listing rule (Q54, Q55)**, in `FormulaEntry.Complete`. When the innermost parenthesis open at
  the caret is a declared function's, at an argument whose values are a fixed list
  (`ValueArgumentAt`), that argument is completed with its values alone: `CompleteName` and
  `CompleteColumn` are not asked there, so letters list no function and no table. `CompleteValue`
  then lists only while the caret stands at the argument's end: straight before the `,` or `)` that
  ends it, or at the end of the text, with no token running across the caret. White space after the
  caret is now something of the argument (Q55), where ticket 70 had skipped it. A number lists the
  value it is alone, and nothing when it is no value (`4`, `10`, `-2` at `match_mode`, `5` and `3`
  at `search_mode`). Any other text lists every value, the first selected: nothing yet, `-`, `1+`,
  `A1`, `X`, `AV`, `Positions`, `"`, `"a,b`, `SUM(1)`. The span accepting replaces is the whole of
  what was typed, from its first character that is not white space to the caret, so Tab on `X`
  writes `0`. Outside such an argument nothing changed: a letter after `=`, an operator, `(` or `,`
  lists functions and Linked Tables by prefix, at another argument, inside `SUM(` and inside a
  grouping parenthesis; `Table[` lists columns; nothing is listed after `=`, an operator, `(` or
  `,` before anything is typed.
- **Tab over typed text** needed nothing in ExGrid: it writes the candidate over the engine's span,
  and the list is not opened again on what Tab wrote (ticket 44). With the caret before the two
  spaces, no list stands in Tab's way, and Tab commits `=XLOOKUP(1,A2:A4,B2:B4,,  )` with the spaces
  kept (the Formula's stored text keeps white space before a token) and moves to E10.
- **After an operator at `match_mode`** (`1+`) the list now opens where a Reference can go: it is
  open over Point (`completionOverPoint`), as the list after `,,` already was, so ↑ and ↓ choose in
  it, and Escape then ↓ points (`…,,1+D11`).

**Readings, not asked of Excel** *(taken while building this ticket; each is pinned by a layer 1
test, and each is a small change if Excel or the user says otherwise)*:

1. **"A number" is what the grammar reads as a number constant**, with one `+` or `-` before it and
   white space after it, and it lists the value it equals as a number: `1.0`, `+1` and `1 ` list
   `1 - Exact match or next larger item` alone, `-0` lists `0 - Exact match`, `-2E0` at
   `search_mode` lists `-2`. Tab on a number therefore writes the same number or nothing, never
   another. Read as text, these would be numbers that are no value, and list nothing. Text the
   grammar does not read as one signed number (`--1`, `- 1`, `50%`, `1e`) is "any other text", and
   lists every value.
2. **Inside a grouping parenthesis the caret stands in an expression of its own**, as inside a call
   of its own: `,,(` lists nothing, as before, and `,,(A` lists `AVERAGE`. Read literally, "text
   that is not a number lists every value" would list the five values for both. The argument hint
   still names `[match_mode]` there, as before.
3. **`,,Positions[` at `match_mode` lists the five values, not the table's columns**: it is text that
   is not a number, at an argument whose values are a fixed list. `Table[` at any other argument
   lists the columns, as before.

Layer 1: `CompletionTriggerTests`. New: `ADR0058_Q54_text_that_is_not_a_number_lists_every_value`
(15 texts, at both arguments, with the span each replaces), `ADR0058_Q54_a_number_that_is_no_value_lists_nothing`
(6), `ADR0058_Q54_a_number_lists_the_value_it_is` (5, reading 1),
`ADR0058_Q55_white_space_after_the_caret_is_something_of_the_argument` (8, the hint still shown),
`ADR0058_what_stands_after_the_caret_inside_the_argument_lists_nothing` (4),
`ADR0058_Q54_outside_a_value_list_argument_a_letter_lists_names` (9, reading 2) and
`ADR0058_Q54_table_bracket_at_a_value_list_argument_lists_the_values` (reading 3);
`Nothing_is_listed_outside_the_argument` keeps the old theory's cases that still list nothing, and
`A_letter_at_a_value_argument_lists_names`, which pinned the replaced reading, is gone. 30 of the new
cases failed before the change. Layer 2: `CompletionTriggerWiringTests`, new: `X`, `AV`,
`Positions`, `A1` and `"` each list the five values, `0` chosen, not over Point, and Tab writes
`…,,0`, closes the list and leaves the hint, the gate back in Overwrite; `0,A` lists `search_mode`'s
four; `1+` lists the five over Point, and Escape then ↓ points; `4` and `0,5` list nothing with the
hint shown; and 11a (F2 ←←←, nothing listed, Tab commits to E10 with the spaces kept). 8 of them
failed before the change; the two for a number that is no value passed before it too, as the ticket
says. Layers 1 and 2: 826 + 2126 + 88 + 1190 (1 skipped, as before) + 389, all passing.
