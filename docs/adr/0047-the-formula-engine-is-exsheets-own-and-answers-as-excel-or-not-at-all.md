# The formula engine is ExSheet's own, and answers as Excel does or not at all

*(Decided with the user, 2026-09-27, in the design grilling that started ExSheet —
[ADR-0046](./0046-exsheet-is-a-general-purpose-sheet-drawn-by-exgrid-as-its-consumer.md).)*

**ExSheet computes Formulas with an engine of its own, written in Excel's syntax, that supports a
declared set of functions.** Each function it supports gives the result Excel gives. A function it
does not support gives `#NAME?`. Nothing is approximated.

This is the first principle of the whole design applied to a formula engine: rather than be
quietly wrong, say it cannot be done. The user of a general-purpose sheet brings Excel's
expectations. A function that exists but disagrees with Excel in an edge case produces a number
that looks right and is not. A function that does not exist produces `#NAME?`, which nobody
mistakes for an answer.

## The rule for admitting a function

**A function is added only when it can match Excel, including Excel's edge cases.** That means its
argument coercions, how it treats blanks and text inside ranges, how it propagates Error Values,
and its rounding. The tests for a function are written from Excel's observed behaviour. A known
deviation keeps the function out.

The first set: `SUM`, `AVERAGE`, `MIN`, `MAX`, `COUNT`, `COUNTA`, `IF`, `ROUND`, `IFERROR`,
`ISERROR`, `XLOOKUP`. The last three are there because Linked Tables are read by key
([ADR-0049](./0049-linked-tables-are-the-consumers-data-read-by-key.md)). The list grows one
function at a time, each under the rule above.

## Syntax: Excel's, and invariant

- **Excel's grammar.** `=`, the arithmetic, comparison and `&` operators with Excel's precedence,
  References (`A1`, `$A$1`, `A$1`, `A1:B2`, whole columns `A:A` and whole rows `1:1`), a Sheet
  qualifier (`Sheet2!A1`, parsed and recorded now although one Sheet exists — ADR-0046), and
  Excel's structured references into Linked Tables (`Positions[PV]`).
- **One spelling in every culture.** Function names are English, arguments are separated by `,`
  and the decimal separator is `.`, whatever culture the Sheet is declared in. This is what Excel
  stores in its files. A German Excel shows `;` and localised names, but that is a display layer
  over the same stored form. **Localised formula syntax is not part of the first version.**
  Constants typed into cells are another matter; they follow the Sheet's culture
  ([ADR-0048](./0048-a-sheet-document-holds-entries-and-exsheet-holds-the-one-undo-stack.md)).

## Values

- **Four kinds, as in Excel:** number, text, boolean, Error Value. A blank cell is not a fifth
  kind. It is how Excel treats "nothing here", which is 0 in arithmetic and ignored by `SUM` over a
  range. That behaviour is part of what each function's tests pin.
- **A date is a number.** It is a serial day number in Excel's 1900 date system, shown with a date
  format. That includes Excel's non-existent 29 February 1900: matching Excel's serials is the
  promise, and any serial computed without that day disagrees with Excel after it.
- **Numbers are IEEE doubles**, as in Excel. `decimal` was the alternative: exact in base ten, and
  tempting on a screen of money. It was rejected because a general-purpose sheet promises Excel's
  answer, and Excel's answer to `=0.1+0.2-0.3` is not 0. A `decimal` engine is right where Excel is
  wrong, and so it disagrees with the spreadsheet every user checks it against.
- **Display follows Excel:** at most 15 significant digits. A number that does not fit its column
  becomes `####`, never a shorter number that looks valid
  ([ADR-0016](./0016-column-width-and-overflow.md)).

## Error Values, and the one ExSheet adds

Error Values are data. They propagate through every Formula that uses them, as in Excel
(`#DIV/0!`, `#NAME?`, `#REF!`, `#VALUE!`, `#N/A`, `#NUM!`, `#NULL!`). `#GETTING_DATA` is Excel's
too, and ADR-0049 gives it a stricter meaning.

**A circular reference is `#CIRC!`.** Excel shows 0 and a warning in the status bar, and the 0 is
a number that flows into every dependent Formula: the quietly wrong answer this project refuses.
`#REF!`, which Google Sheets uses, was rejected because it already means "the cells this
Reference named were deleted", and a user repairing one would be looking for the wrong fault.
**`#CIRC!` is the only Error Value ExSheet adds to Excel's set**, and it is documented as that.
Every cell in the cycle shows it, and so does every Formula that depends on one of them.

## Recalculation

The engine keeps a dependency graph from each cell to the cells whose Formulas read it. After a
change it recalculates only what depends on the change. **No Value is ever shown from a
recalculation that has not finished**: a Window pushed to ExGrid carries Values that are all from
one completed recalculation. How fast this is at a large Sheet is measured, not asserted. The
Definition of Done will name the case.

## The engine is its own package, with no UI

**`ExSheet.Engine` has no dependency on Blazor, ExGrid or anything of ours.** `ExSheet`, the
component, references it. The reason is a consequence of ADR-0048: a Sheet Document records
Entries, never Values, so **anyone who wants a saved sheet's numbers has to run the engine.** A
server that validates a submitted sheet, a job that reports on saved ones, and a test all need it
without a browser.

This is [ADR-0007](./0007-edits-are-an-overlay-owned-by-the-consumer.md)'s argument again. If the
value on screen and the value a server computes come from two implementations, they drift, and
each looks right on its own. There is one implementation, and it is the one that runs in both
places.

## Considered options

- **An existing .NET formula library.** It was rejected for three reasons. Its error semantics and
  its gaps against Excel would be its authors' and not ours to pin, so the admission rule above
  could not be enforced. Its licence would travel with every copy of ExSheet. And the question it
  answers, "evaluate this string", is the small part: References that rewrite themselves on
  insertion, Linked Tables, and recalculating only what changed are most of the work anyway.
- **A large function library from day one.** Rejected by the admission rule. A hundred functions
  at "mostly Excel" are a hundred places to be quietly wrong.

## Settled while building *(2026-09-27, decided with the user)*

- **A function may be admitted with some argument values refused**, provided every refused value
  gives an Error Value and never a different answer. `XLOOKUP`'s binary search (`search_mode` 2
  and −2) answers only over data that is sorted as the mode says, and gives `#VALUE!` otherwise:
  over unsorted data, which row Excel returns depends on an algorithm that is not documented.
  Which of several equal keys it returns is to be observed in Excel first.
- **Spilled arrays are not supported.** A Formula whose result is more than one cell gives
  `#VALUE!`. Excel 365 would spill it. Implicit intersection, Excel 2019's behaviour, would return
  one value instead, and was rejected: it would silently change what a Formula means on the day
  spilling arrives. A refusal can become an answer later without changing any sheet already
  written. Spilling brings `#SPILL!`, `A1#`, `@` and the functions built for it, so it gets an ADR
  of its own.
- **`IFERROR` and `ISERROR` do not catch `#CIRC!`**, as they do not catch `#GETTING_DATA`
  ([ADR-0049](./0049-linked-tables-are-the-consumers-data-read-by-key.md)). A cycle is a fault in
  the Sheet's construction, and `=IFERROR(A1, 0)` over one would show the 0 that this ADR refuses.
  A dependent is a Formula whose text references a cycle member, whether or not the branch
  reading it is taken: `=IF(TRUE, 1, A1)` is `#CIRC!` too. That is strict, and it is the safe side,
  because a branch that is not taken today may be taken tomorrow.
- **A Formula keeps the whitespace it was typed with.** Rewriting References on an insertion or a
  deletion changes only the Reference tokens.
- **The example above, `=0.1+0.2-0.3`, is under verification.** Excel is reported to set a final
  addition or subtraction that nearly cancels to 0. Ticket 19 asks a real Excel, and this ADR is
  corrected by the answer.

## Settled while building, second round *(2026-09-27, decided with the user)*

- **The General format fits its column, as Excel's does.** A number shown in General is rounded
  to the digits the column can show, and switches to scientific notation where Excel's does. So
  `=1/3` reads `0.333333` in a default column, not `####`. The Value is untouched; only its text
  depends on the width, which the component hands to the engine. **A number or date typed into a
  column still at its default width widens the column when it does not fit**, as Excel does. Both
  rules are checked against Excel by the case corpus, and by hand.
- **Formats live at three levels, as in Excel: cell over row over column.** Formatting a whole
  column or row records one entry, not a million. The component's interim cap on formatting
  commands goes once this exists.
- **A Formula's result takes a format at entry, as Excel's does.** For example, a date plus a
  number shows as a date. The exact inference rule is Excel's, observed by the case corpus. It is
  applied only when the cell's format is General, and only when the Formula is entered.
