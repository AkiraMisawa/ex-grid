# A Formula whose result is an array spills, as Excel 365's does

*(Decided with the user on 2026-10-03, from the function catalogue's Decide list,
`docs/specs/exsheet-functions/spec.md`. This replaces ADR-0047's "Spilled arrays are not
supported"; that section now points here.)*

ADR-0047 refused every result of more than one cell with `#VALUE!`, and refused implicit
intersection, "so that spilling can arrive later without changing any written sheet". It arrives
here. `FILTER`, `UNIQUE`, `SORT` and the other functions built for arrays need it, and so does
`SUMPRODUCT((A1:A3="x")*B1:B3)`, whose operators work on whole ranges.

## The decision

**A Formula whose result is an array of more than one Value spills, with Excel 365's semantics.**

- **Where it spills.** The Formula's cell, the *anchor*, shows the array's first Value. The others
  show in the cells below and to the right, the *spill range*. Those cells hold no Entry. Their
  Values are the anchor's, and they are not recorded in a Sheet Document, as no Value is
  (ADR-0048).
- **What spills.** Everything Excel 365 spills:
  - a Reference to several cells (`=A1:A3`);
  - an operator applied to a range (`=A1:A3*2`, `=A1:A3="x"`), element by element. Two arrays of
    different sizes are matched as Excel matches them: a single row or column is repeated along
    the other array, and the cells past the shorter of two larger arrays are `#N/A`;
  - the functions built for arrays: `FILTER`, `UNIQUE`, `SORT`, `SORTBY`, `SEQUENCE`, `TRANSPOSE`;
  - a function that returns a range, such as `INDEX` with a row or a column of 0, or `XLOOKUP`
    with a return array of several columns.
- **A function that reduces an array takes one.** `SUM((A1:A3>1)*B1:B3)` and
  `SUMPRODUCT((A1:A3="x")*B1:B3)` evaluate the array and return one Value, as Excel 365 does.
- **An obstacle is `#SPILL!`.** When a cell of the spill range holds an Entry, or another Formula's
  spill, or the range would pass the Sheet's edge, nothing spills and the anchor shows `#SPILL!`.
  Nothing is overwritten. When the obstacle is cleared, the Formula spills. `#SPILL!` is one of
  Excel's Error Values, and it propagates as they do.
- **No limit but the Sheet's edge.** A spill of a million rows is allowed, as Excel allows it. Its
  cost is measured, and a limit, if one is needed, is a later decision.
  - *(2026-10-03, found in implementation and agreed with the user.)* What the engine cannot hold is not a limit on a spill but a
    refusal of what cannot be executed (the design's fifth principle): an array of more than 2^24
    Values (sixteen whole columns) is not built, and whatever would build one is `#NUM!`. Excel
    runs out of resources before it. `=1:1048576*1` would be 17 billion Values.
- **`A1#` names the spill range of the Formula in A1.** It is `#REF!` when A1 does not spill. It
  moves with A1 on an insertion or a deletion, as any Reference does.
- **The `@` operator is refused on entry,** as an unknown syntax is. Implicit intersection stays
  refused: no Formula silently takes one Value of a range.
- **A spilled cell in the editor.** Selecting a cell of a spill range shows the anchor's Formula in
  the Formula Bar, dimmed, as Excel does: the Formula is not that cell's, and it is not edited
  there. Typing into a spilled cell enters an Entry there, as typing into any cell does, and the
  anchor becomes `#SPILL!` until it is cleared. Delete over spilled cells clears nothing, because
  they hold nothing. A copy of spilled cells copies their Values.
- **Dependencies.** A Formula that reads a spilled cell, or `A1#`, depends on the anchor, and is
  recalculated with it. A spill that grows or shrinks recalculates what read the cells it covered
  and the cells it covers now.

## Not yet: functions lifted over arrays

Excel 365 also *lifts* a function that takes one Value when it is given an array: `ROUND(A1:A3, 0)`
rounds each Value and spills three. Every function of the declared set would need its own pass for
this, and it comes as later tickets. Until a function is lifted, an array where it wants one Value
stays `#VALUE!`, as ADR-0047 had it: an Error Value, never another answer.

## What becomes Excel's answer

The ADR-0047 refusals that this replaces become Excel's answers, and their corpus cases change
from a difference by decision to Excel's Value: a Reference to several cells, an operator on a
range, `INDEX` with a row or a column of 0, `XLOOKUP` with a return array of several columns, and
`IF`, `IFERROR`, `ISERROR` and the other functions given a range where they want one Value once
they are lifted.

## Considered options

- **Only the functions built for arrays spill, and `=A1:A3*2` stays `#VALUE!`.** Rejected with the
  user: the operators on ranges are the common half of Excel 365's arrays, and ADR-0047 kept the
  refusal so that it could become the answer.
- **Refuse an Entry typed into a spill range.** Rejected with the user: it is not what Excel does,
  and `#SPILL!` already refuses to overwrite.
- **A limit on a spill's size now.** Rejected with the user, as above: measured first.

## Consequences

- New Error Value: `#SPILL!` (Excel's). New Reference form: `A1#`.
- The Formula Bar of a spilled cell shows text that cannot be edited there. That is a declaration
  ExGrid's Formula Bar needs (dimmed, read-only text for a cell), decided with this ADR's ticket.
- Glossary: **Spill**, **Spill Range**, **Anchor**.
