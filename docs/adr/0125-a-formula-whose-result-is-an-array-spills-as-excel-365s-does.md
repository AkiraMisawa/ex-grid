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

*(2026-10-12: **the refusal is the Formula's**, as ADR-0047 makes a Formula that meets `#CIRC!`
`#CIRC!`. A call that refused returned `#VALUE!` as an Error Value, and what was around it read it as
one: `=IFERROR(LEN(A1:A3),0)` was 0, `=ISERROR(ROUND(A1:A3,0))` TRUE and `=COUNTA(ROUND(A1:A3,0))` 1,
answers about a `#VALUE!` the engine made where Excel 365 lifts the function, though
`=IFERROR(A1:A2,0)` was already `#VALUE!` (IFERROR-009). Now nothing in the Formula reads the
refusal, given the range or around a call that was given it (IFERROR-017).)*

*(2026-10-12, decided with the user: a refusal is never caught, anywhere: a cell that reads such a Formula's cell is refused in
turn, whatever it does with it, as ADR-0047's note of that date says. Before, it read `#VALUE!` as an Error
Value: with B1 `=LEN(A1:A3)`, `=IFERROR(B1,0)` was 0 and `=ISERROR(B1)` TRUE, where Excel's B1 spills a
Value for each cell.)*

*(2026-10-13, decided with the user: **a refused Anchor whose array's shape its text settles spills
refusals over that shape**, so that a cell of it read elsewhere, `=E2+1` or `=ISBLANK(E2)`, is refused
as the Anchor is. The text settles it where the array is References the text names, operators over them,
`TRANSPOSE`, or a call given one Value for each argument. Where the shape could depend on a Value —
`FILTER`, `UNIQUE`, `SORT`, `SORTBY`, `SEQUENCE`, a call that can give a Reference, `LET`, or a function
given an array where it wants one Value, as `=LEN(A1:A3)` — nothing spills, and the cells its array would
cover read as blank: a Known limitation of `0.1.0` (ADR-0202). Where something stands in the way of the
known shape, the Anchor is `#SPILL!`, as Excel's array of that shape would be.)*

## What becomes Excel's answer

The ADR-0047 refusals that this replaces become Excel's answers, and their corpus cases change
from a difference by decision to Excel's Value: a Reference to several cells, an operator on a
range, `INDEX` with a row or a column of 0, `XLOOKUP` with a return array of several columns, and
`IF`, `IFERROR`, `ISERROR` and the other functions given a range where they want one Value once
they are lifted.

*(2026-10-12, after the seventeenth Windows run: **`INDEX` over a Reference of several rows and several columns, with
`column_num` left out, is `#REF!`**, as Excel's `=INDEX(G1:H5,2)` is (INDEX-012); a row or a column of 0 still spills
(INDEX-013), and over an array `row_num` alone is still the whole row, as Microsoft documents (INDEX-014). A Spill
Range of several rows and columns, `A1#` typed or `INDIRECT("A1#")`, is a Reference as a typed range is, and takes the
same `#REF!` (INDEX-017 and 018 ask it). Whether `column_num` left empty, `=INDEX(G1:H5,2,)`, is the whole row, and
whether a row of 0 with it left out is `#REF!` too, are INDEX-015 and 016 for the next run. **A blank among `SORT`'s
and `SORTBY`'s keys, and in `UNIQUE`'s array, stays refused with `#VALUE!`**, as ADR-0047's paragraph admitting them
under this ADR wrote, kept before the tag under ADR-0202. Excel answers 7 for `=SORT(Q1:Q2)` and `=UNIQUE(Q1:Q2)` over 7 and a blank
(SORT-011, UNIQUE-009), and the Anchor shows only that a blank does not sort first as 0 would. Following it would
guess where the blank goes, what its spilled cell shows, how a descending sort places it and whether `UNIQUE` counts
it one of a kind with 0; SORT-012 to 014 and UNIQUE-010 and 011 ask those.)*

*(2026-10-12, after the two area passes, `verification/2026-10-12-windows-area/` and
`verification/2026-10-12-windows-area-2/`: **a blank among `SORT`'s and `SORTBY`'s keys, and in `UNIQUE`'s array, is
answered, and so is the blank they keep, read inside the Formula, where Excel's answers or the agreement of every
reading settle it, or by generalisation from them, which no run has read (this note names them).** The refusal of the
note above is withdrawn. What Excel answered: over 7 and a blank, `=SORT(Q1:Q2)` spills 7 and then 0, ascending or
descending, and over b, a blank and a, `=SORT(A1:A3)` is a first: a blank key sorts after numbers and text in either
order and after a boolean ascending, neither as 0 nor as empty text, and two blank keys keep their order in a
descending sort (SORT-011 to 014, 016 to 020, 023 to 025). `SORTBY` sorts a blank key after a number so, key by key,
in an ascending key (SORTBY-007 to 009). In `UNIQUE` a blank is one of a kind only with another blank, never with 0 or
empty text (UNIQUE-011 to 015). A blank spills as 0 (SORT-012, UNIQUE-010, 016, and `TRANSPOSE`'s, TRANSPOSE-004).
Read inside the Formula, the blank `SORT` keeps is the empty value a blank cell holds:
`=ISBLANK(INDEX(SORT(Q1:Q2),2))` is TRUE (SORT-022), so it is neither the 0 it spills as nor empty text. `UNIQUE`'s is
not the 0 it spills as: `=INDEX(UNIQUE(Q1:Q2),2)=""` is TRUE (UNIQUE-018), which 0 would not be, and
`=SUM(--(LEN(UNIQUE(A1:A3))>0))` over a, a blank and b is 2 (UNIQUE-019), where `LEN(0)` is 1; whether it is blank or
empty text, UNIQUE-020 asks. `COUNTA` counts it in the array: `=COUNTA(SORT(Q1:Q2))` and `=COUNTA(UNIQUE(Q1:Q2))` are
2 (SORT-021, UNIQUE-017). What the engine answers beyond those, and why, none of it Excel's answer until its case is
observed: a blank key sorts after a boolean in a descending sort too, blank keys keep their order ascending, as equal
keys did (SORT-004), and `SORTBY` sorts a blank in its other keys as `SORT` does (SORT-040, 041 and SORTBY-014 ask);
beside an Error Value key no order is answered, and the Formula is `#VALUE!` (SORT-031 asks). `UNIQUE`'s is taken to
be the empty value a blank cell holds, as `SORT`'s is (UNIQUE-020 asks). `SORTBY`, `FILTER` and `TRANSPOSE` keep a
blank as `SORT` does, so theirs is taken to be the same blank, read as `SORT`'s is; before this, the engine's
`=COUNTA(FILTER(...))` passed over a blank that its `=COUNTA(SORT(FILTER(...)))` counted, though a reorder cannot
change a count (SORTBY-011 to 013, FILTER-012 and 013, TRANSPOSE-005 and 006 ask). Most functions that read one Value,
`LEN`, `ISTEXT` and `MATCH`'s and `VLOOKUP`'s lookup value among them, read the blank `INDEX` takes out as a blank
cell's, as `ISBLANK` was seen to read `SORT`'s; the rest, `XLOOKUP`'s and `XMATCH`'s lookup value, `ROUND` and
`ERROR.TYPE` among them, make the Formula `#VALUE!`. An operator reads each Value as a blank cell's, which `=` finding
`UNIQUE`'s equal to empty text fits (UNIQUE-024 asks `&`). `SUMPRODUCT` and `CONCAT` add nothing for it whichever way
they read it, and `SUM` neither, except where it would be the last number, which decides whether a sum that nearly
cancels is 0 (SORT-033 and 037 ask). `VLOOKUP`, `HLOOKUP` and `XLOOKUP` take an array that holds it where it is
neither a key they search nor what they return. Where a function's reading of it parts — `AVERAGE`, `MIN`, `MAX`,
`COUNT` or `PRODUCT` passing over it or taking it as 0, `COUNTA` given it as one Value, a search through it, `IF`,
`CHOOSE`, `IFERROR` or `IFNA` given it, another of the five — the Formula is `#VALUE!`. The refusal is the Formula's,
not an Error Value the call returns, so `IFERROR` cannot catch it and `COUNTA` cannot count it, as a Formula that
meets `#CIRC!` is `#CIRC!` (ADR-0047); SORT-034, 035, 038, 039, UNIQUE-021 to 023, FILTER-014 and TRANSPOSE-007 ask.
`LEN` is not yet lifted (above), so UNIQUE-019 stays `#VALUE!` where Excel gives 2.)*

*(2026-10-14, after run 18a: **the kept blank is read as a blank cell's by the aggregates, `COUNTA`, `IF` and
`SORT`.** Excel's `AVERAGE` and `MIN` passed over the blank `FILTER`, `TRANSPOSE` and `UNIQUE` kept (FILTER-014,
TRANSPOSE-007, UNIQUE-021, 022: 7, not 3.5 or 0), `COUNT` did not count `SORT`'s (SORT-034: 1), and `SUM` passed
over it where reading it as 0 last would have moved its last addition (SORT-037: 0, not 2.78E-17). So every function
that takes its numbers as `SUM` does passes over it, `MAX`, `PRODUCT`, `MEDIAN` and the spreads by generalisation.
`COUNTA` counted the blank `INDEX` took out of `SORT`'s array, and `UNIQUE`'s of one blank cell (SORT-035,
UNIQUE-023: 1), so it counts a kept blank given as one Value too. `IF` handed `SORT`'s array on with its blank, which
`ISBLANK` then read blank (SORT-039: TRUE), so `IF`'s branch is handed on as it is. `SORT` given `FILTER`'s blank
as a key sorted it last, as a blank cell's (SORT-038: 7), so `SORT` takes a kept blank in its array as a blank,
and the other four functions' by generalisation. The rest above stays refused: `CHOOSE`, `IFERROR` and `IFNA` given
it, `MATCH` and `XLOOKUP` over an array that holds it where they read it, and the other array functions given it.)*

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
