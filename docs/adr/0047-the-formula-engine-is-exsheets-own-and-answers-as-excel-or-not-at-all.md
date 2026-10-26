# The formula engine is ExSheet's own, and answers as Excel does or not at all

*(2026-10-25: [ADR-0370](./0370-the-whole-family-moves-to-typescript-and-blazor-only-adapts-it.md) moves the formula engine to TypeScript, proved against Excel's recorded answers,
the C# engine's facts as shared cases and a differential run against the C# engine. Its numbers stay
doubles, as below.)*

*(2026-10-26, decided with the user: the engine computes `EXP`, `LN`, `POWER` and `^`, `ATAN` and the
other functions a platform's math library answers itself, each correctly rounded, and calls no
platform's library for them. A power whose exponent is not whole is still e^(b ln a), as observed below;
a whole power is now the exact power rounded once. See "The platform's math library is not used", at
the end.)*

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
*(2026-10-12, decided with the user: one exception, `PMT`, kept though its result may differ from
Excel's in its last digits, a Known limitation of `0.1.0`. The note of that date, "An exception for
`PMT` alone", says why and how far it goes.)*

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

*(2026-10-12, the seventeenth Windows run: **Excel showed 0 in one cell of a cycle, not in each.** With A1
`=B1+1`, C1 `=A1*2` and D1 `=C1&"x"` entered first and B1 `=A1` last, B1, whose entry closed the cycle, showed 0,
and A1, C1 and D1 kept 1, 2 and `2x` (ERR-078 to 081): the Values they had before, which B1's 0 also gives them. A
Formula entered while a cycle stands showed 0 whatever it computes from a cell of the cycle: `=A1+1`, `=ISNUMBER(A1)`
and `=ERROR.TYPE(A1)` over A1 `=B1`, B1 `=A1` (ERR-084, ISNUMBER-008, ERRORTYPE-020). Which Values Excel shows
depends on the order the cells were entered, which a Sheet does not keep. The decision stands, for the same
reason.)*

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
  *(2026-10-12: `XMATCH`'s binary search refuses as `XLOOKUP`'s does. Over `G1:G5` unsorted, Excel's
  `=XMATCH("Banana",G1:G5,0,2)` is 2 (XMATCH-008), recorded as Excel's answer beside the refusal, as XLOOKUP-116 to
  122 are.)*
  *(2026-10-12, decided with the user: **a refusal is never caught, anywhere.** The refusal is the
  Formula's, and every Formula's that reads its cell. A refused value's Error Value is `#VALUE!` where
  ExSheet cannot answer as Excel, because the readings an evaluation could take part or no run has settled
  the answer. Nothing in the Formula turns it into an answer: `IFERROR`, `ISERROR`, `ERROR.TYPE`, `IFNA`,
  `COUNT`, `COUNTA` and the aggregates that pass over errors included. And it is carried as `#CIRC!` is: a
  Formula that reads a refused cell — directly, through a range, a `LET` name, `INDIRECT`, `OFFSET`, a
  Spill Range, or a Linked Table's column a Consumer pushed it into — is refused in turn, whatever it does
  with it and whether or not the branch that names it is taken. Before, a refusal was caught: with the
  first area pass's corrections, which refused `MOD(1,0.1)`, `=IFERROR(MOD(1,0.1),0)` was 0 where Excel's
  `MOD(1,0.1)` is 0.09999999999999995 (MOD-020); and with B1 `=MROUND(6.05,0.1)`, refused where Excel
  answers 6 (MROUND-001), `=IFERROR(B1,0)` in another cell was 0. A Formula that meets a cycle is still
  `#CIRC!`, and one that meets data on its way still waits. Excel's own `#VALUE!`, such as `=1+"a"`'s, is
  an Error Value any Formula catches. A cell's refused Value is `#VALUE!` to a Consumer, shown and copied
  as one, and says it is a refusal (`Value.IsRefused`); it is no constant an Entry can hold. A copy of one
  range carries the refused cell's Formula; a copy of several ranges, or with the headers, that holds one
  is refused when pasted back into the Sheet, rather than turn the refusal into an Error Value a Formula
  can catch. Where Excel's answer to such a Formula is known, its corpus case records the refusal as a
  difference by this decision (INDIRECT-056); IFERROR-018 to 020, ISERROR-011, ERRORTYPE-022, IFNA-008,
  COUNT-009, COUNTA-009, AGGREGATE-069, REF-015, LET-113, INDIRECT-100 and SEQUENCE-009 ask Excel the
  rest.)*
  *(2026-10-13, decided with the user: such a Formula is a **Refused Formula** (`CONTEXT.md`), and a
  Consumer tells it by `Value.IsRefused`. A call of a function Excel has that ExSheet does not declare — the
  377 names of the catalogue's rows that are not Supported and of its list of Excel's other functions
  (`ExcelFunctionNames`, held to the catalogue by `FunctionCatalogueTests`) — and a declared Linked Table's
  bare name typed (TABLE-024) are Refused Formulas too, which show `#NAME?`: no Formula catches one, and a
  cell that reads one is refused and shows `#VALUE!`. A name Excel does not have stays Excel's own `#NAME?`,
  an Error Value a Formula catches.)*
  *(2026-10-13: how a copy carries a refused cell. ExSheet's clipboard HTML marks it
  (`data-ex-mark="exsheet-refused:C1"`, through ExGrid's `ClipboardData.MarkAttribute` and
  `GridPasteIntent.MarkFor`, ADR-0050 item 9). This Sheet's own copy of one range pastes its Entries, the
  Formula refused again where it lands; any other paste that carries the mark — of several ranges, with
  the headers, or into another Sheet on the page — is refused whole, by name. Another program reads the
  cell as the text it shows, `#VALUE!` (or `#NAME?`).)*
- *(Replaced 2026-10-03 by [ADR-0125](./0125-a-formula-whose-result-is-an-array-spills-as-excel-365s-does.md), decided with the user: a Formula whose result is an array now spills, and the refusal below became the answer it was kept for. The text stands as it was decided.)* **Spilled arrays are not supported.** A Formula whose result is more than one cell gives
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
  deletion changes only the Reference tokens. *(Refined by what Excel was observed to do,
  2026-09-27: whitespace belongs to the token after it. It is kept before a token, and dropped at
  the end of the Formula and before a `,`. Only space, LF and CR may stand between tokens; a tab is
  refused on entry. When a Reference becomes `#REF!`, the whitespace before it goes with it. A
  Reference is written as Excel writes it, so `A1:A1048576` is written `A:A` and `A:XFD` is
  written `$1:$1048576`.)*
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

## Settled while building, third round *(2026-09-27, decided with the user)*

- **The engine takes a column's width in characters of the default font** (Excel's unit; 8.43 by
  default). The component converts from the grid's resolved pixels and digit metrics. Every
  character counts as one digit width. That is exact for digits and an estimate for other
  characters until the case corpus has observed Excel.
  *(2026-10-01, ticket 91: the unit stands, and fitting is charged glyph by glyph.)*
  - The engine and the Sheet Document still count in characters of the grid's digit. The default
    column is still 8.43.
  - What fits inside a width is charged as the grid charges it: each glyph at its own measured
    width (ADR-0016, ticket 83), bold for a bold cell. That covers General's fitting, `####` and a
    key's widening.
  - The twelfth run's case 17 widenings now land within 0.4 characters of Excel's. Before, they
    were up to 1.2 over.
  - **Still apart:** under the core's widths, en-GB's `05-Jan-26` widens to 8.82 where Excel fits it
    at 8.09 (case 19). The core's regular widths cover weight 600, because group and total rows
    paint at 600.
- **The text fitted to a column is for painting only.** The accessible name and a copy take the
  Value as the engine gives it unfitted, as [ADR-0016](./0016-column-width-and-overflow.md) gives
  a screen reader the real value behind `####`.
- **A Formula's format at entry is inferred only where Excel's rule has been observed.** Until the
  corpus observes more, that is `+`, `-`, single-cell references, constants and parentheses.
  Everything else stays General.

## Observed in Excel *(2026-09-27, Microsoft 365 16.0.20326.20158 — verification/2026-09-27-windows-excel)*

**The example at the top of this ADR was wrong, and is corrected here.** Excel's answer to
`=0.1+0.2-0.3` is **0**, not 5.55E-17. Excel sets a *final* addition or subtraction whose result
nearly cancels to 0, and `=0.1+0.2=0.3` is TRUE. It leaves the same arithmetic alone when it is
wrapped in a multiplication: `=1*(0.5-0.4-0.1)` is -2.78E-17. The argument for doubles still
holds: Excel computes in doubles, and "Excel's answer" includes these adjustments, which the
engine must now reproduce. The `decimal` alternative stays rejected.

### What the observation settled *(decided with the user, 2026-09-27)*

- **Every disagreement the oracle found in a declared function, a typed constant, a format or a
  Formula's written form is fixed to Excel's answer.** The list is in
  `verification/2026-09-27-windows-excel/results.md`.
- **A typed Error Value becomes that Error Value**, as in Excel (`#N/A`, `#DIV/0!`, …). The
  exceptions are `#GETTING_DATA` and `#CIRC!`, which are states of a computation and cannot be data
  ([ADR-0049](./0049-linked-tables-are-the-consumers-data-read-by-key.md)); typed, they stay text.
- **A pasted or typed Formula is read in the invariant syntax under every culture.** Under
  `de-DE` Excel reads `=SUM(1.5,2)` as text, because it expects its local syntax. This is a
  deliberate difference, because localised syntax is out of the first version.
- **`XLOOKUP`'s `match_mode` 3 (regular expressions) is supported.** Excel's expressions are
  PCRE2's; .NET's are close but not the same. So a pattern is accepted only when it uses constructs
  whose meaning is the same in both. Any other pattern gives `#VALUE!`, as the admission rule
  requires. Case sensitivity and the constructs in the accepted set are pinned by the case corpus
  against Excel.

- **A colour in a format code is kept, and not yet painted** *(decided with the user, 2026-09-27)*.
  `$5` typed becomes 5 with Excel's `$#,##0_);[Red]($#,##0)`. The code is recorded whole, so it
  goes back to Excel intact. `[Red]` is not painted until per-cell styling has its ADR
  ([ADR-0046](./0046-exsheet-is-a-general-purpose-sheet-drawn-by-exgrid-as-its-consumer.md)). The
  parentheses still mark a negative, so the sign is never lost.
  *(2026-09-30, decided with the user: the colour is now painted, in Excel's colour for its name,
  and it wins over the cell's Font colour, as Excel's does
  ([ADR-0071](./0071-a-sheets-cell-format-is-document-data-painted-on-white-paper.md)). `[ColorN]`
  stays refused.)*
- **`XLOOKUP`'s binary search over duplicate keys answers as Excel was observed to**: the first
  equal key ascending, the last descending. Longer layouts are checked in the next Windows run.
  Unsorted data is still refused.
- **Typed `#SPILL!`, `#CALC!` and Excel's other newer Error Values stay text.** The engine has no
  such Error Values while spilling is out of the first version. They join `#GETTING_DATA` and
  `#CIRC!` as exceptions to "a typed Error Value becomes that Error Value".
  *(Reversed 2026-09-27, second observation below: that answer came through COM. Typed, Excel
  makes them Error Values.)*
- **A number constant in a Formula is written as Excel writes it**: `=1E15&""` is stored as
  `=1000000000000000&""`, with 15 significant digits.

## Observed in Excel, second run *(2026-09-27, the same build — verification/2026-09-27-windows-excel-2)*

The whole corpus, 1056 cases, was asked twice: through COM, and **with real keys**. The two routes
disagree in a dozen places (`results.md`, "COM and the keyboard"). **Where they differ, the keyboard
is Excel's answer for ExSheet**, because a user types. COM's `FormulaLocal` and `Formula2` are the
oracle's convenience, not what a person meets.

### What the second observation settled *(decided with the user, 2026-09-27)*

Each disagreement goes to Excel's answer, except where a refusal is named.

- **Near-cancel follows Excel's shape** (ARITH-076..082). A final addition or subtraction inside
  parentheses is left alone (`=(0.1+0.2-0.3)` is 5.55E-17), `SUM` cancels as a final operation does
  (`=SUM(0.1,0.2,-0.3)` is 0), and comparison treats `1+4E-15` as equal to 1. The threshold is
  narrower than 2⁻⁴⁸ (`=1+3E-15-1` is not zeroed). **Its exact boundary is not known**, so the corpus
  gains cases that bracket it for the next Windows run, and the engine takes the tightest threshold
  consistent with every observed case until then.
- **Text comparison ignores hyphens and reads `ß` as `ss`** (ARITH-087, 088), as Excel's collation
  does.
- **A number turned into text is written as Excel writes it** (ARITH-092, 093): `1E-10` becomes
  `0.0000000001`, and `1.23456789012345E-5` becomes `1.23456789012345E-05`.
- **`^` is the plain double power** (ARITH-098): `=8^(1/3)` is 1.9999999999999998.
- **Typed `#SPILL!`, `#CALC!`, `#FIELD!`, `#BLOCKED!`, `#CONNECT!` and `#UNKNOWN!` are Error
  Values**, in any case (`#spill!` is `#SPILL!`), and `ISERROR` sees them (ERR-096..106). They are
  data only: nothing in the engine produces them. **`#BUSY!` stays text**, as Excel keeps it.
  `#GETTING_DATA` and `#CIRC!` stay text, as decided above.
- **`IFERROR` over an empty cell gives an empty value, not 0** (IFERROR-013): `=IFERROR(A1,"x")&""`
  is `""`.
- **`XLOOKUP`'s regular expressions accept Unicode `\w`, lookahead, `\p{…}` and backreferences**
  (XLOOKUP-097..102). They mean the same in PCRE2 and .NET, so the accepted set was drawn too
  narrowly. The admission rule stands for constructs the two read differently.
- **A Formula is written back as Excel writes it** (TEXT-083..097): a sheet qualifier takes the
  sheet's own name (`sheet1!` → `Sheet1!`), a number constant is written in Excel's form
  (`=1E20` → `=100000000000000000000`, `=1E-10` → `=0.0000000001`,
  `=1.23456789012345E-9` → `=1.23456789012345E-09`), and a CR LF inside a Formula is kept as LF.
- **`=1E308` is refused** (TEXT-103). Excel refuses it through COM and, typed, offers to correct it to
  `=E1308`. ExSheet does not correct a Formula into another one.
- **General shows a negative that rounds to 0 as `-0`, and `####` where even that does not fit**
  (GW-026, 027).
- **Typed constants are read as Excel reads typed ones** (TYPED-032, 047, DATE-013, TYPED-040):
  `-$5` is the number -5; `- item one` becomes the Formula `=- item one`, which is `#NAME?`; a
  two-digit year up to 49 is 20xx and from 50 on is 19xx, as Windows' default does (`1/1/30` is
  2030); `26-Okt` under de-DE is a date shown `26. Okt`.
- **A Formula's result takes a format from what it refers to or contains** (FF-011, 013, 020,
  ARITH-006, 064): a Formula over a date is formatted as a date, `SUM` over dates too, `=10+50%`
  takes `0.0%`, and a Formula over a cell in scientific format takes `0.00E+00`. This widens the
  "format at entry" rule above to what was observed.
- **A number typed into a percent cell is read as a percentage** (LVL-015): `0.5` into a `0%` cell
  is 0.005, Excel's automatic percent entry, which is on by default.
- **A column a typed entry widened is recorded as a custom width** (CW-018), as Excel's file marks
  it (`customWidth`), **and a custom width is not widened again**. *(Settled with the user,
  2026-09-27, when the first reading proved impossible: "custom, yet a longer number still widens
  it" cannot both hold in ADR-0046's model, where custom means "never widened". A third kind of
  width would have changed the Sheet Document for a behaviour nobody had observed.)* Excel's flag
  was observed for a number only. A date's flag, and whether Excel widens a column again for a
  longer number, go to the next Windows run. If Excel widens again, this becomes a third kind of
  width.
- **A format code with `[Color n]` is refused** (FMT-075), as Excel refuses it.

The deliberate differences recorded earlier stand, and the second run gave the same answers for
them: a Linked Table or column that does not exist is `#NAME?` or `#REF!` rather than a refused
entry (TABLE-011, 013), a pasted Formula is read in the invariant syntax under de-DE (COPY-025),
`#CIRC!` (ISERROR-010), and a date-time fill (FILL-033).
*(2026-10-12: the corpus now writes Excel's answer beside each of them, `excelExpect` (TABLE-011
and 013: refused on entry; COPY-025: the text `=SUM(1.5,2)`), as ADR-0208's release check asks of
every case that differs by decision. The seventeenth run gave the same answers.)*

## Observed in Excel, third run *(2026-09-28, the same build — verification/2026-09-28-windows-excel-3)*

The corpus, now 1144 cases, was asked through COM and with real keys again. Excel's answers to the
1056 older cases were the second run's, every one. Of the 88 new cases, 61 agree typed and 25
disagree. The keyboard stays Excel's answer for ExSheet where the two routes differ.

### What the third observation settled *(decided with the user, 2026-09-28)*

Each disagreement goes to Excel's answer, except where a refusal is named.

- **A final addition or subtraction is 0 when its operands are closer than 2⁻⁴⁹ of each** (ARITH-099..105,
  replacing 2⁻⁵¹). `=1+1E-15-1` and `=1.1-1-0.1` are 0; `=1+2E-15-1` is 1.9984014443252818E-15.
  Every observation from both runs brackets the boundary between 1.11E-15 and 1.998E-15 relative,
  and 2⁻⁴⁹ (1.78E-15) is the only power of two inside it.
- **Two numbers compare equal when they are closer than 20 units in the last place of 1 (4.44E-15)
  relative to each** (ARITH-106..112, replacing 2⁻⁴⁷). `=1+4E-15=1` is TRUE (second run) and
  `=1+5E-15=1` is FALSE, so the boundary lies between 3.997E-15 and 4.885E-15. **It is not a power of
  two, and it is not the zero threshold above**: the recommendation to unify the two at 2⁻⁴⁹ was
  withdrawn before it was written, because it contradicts the second run's `=1+4E-15=1`. 4.44E-15
  is a value inside the bracket, taken until the next Windows run narrows it; the corpus gains
  cases between 4.0E-15 and 4.9E-15.
- **Text comparison ignores an apostrophe, as it ignores a hyphen, and `ß` equals `ss` under `=`**
  (ARITH-125, 126), not only in ordering.
- **`^` with an exponent of 0.5 is the square root** (ARITH-133): `=2^0.5` is 1.4142135623730951.
  *(Corrected in implementation: nothing was rounding. The engine computed e^(0.5 ln 2), one unit
  in the last place below. `Math.Pow` for every exponent would break `=8^(1/3)` and `=27^(1/3)`,
  already observed, so every other fractional exponent stays e^(b ln a). ARITH-143, `=3^0.5`, asks
  whether Excel's square root is general.)* *(2026-10-26: e^(b ln a) stays for an exponent that is not
  whole, each step correctly rounded; a whole exponent is the exact power rounded once, and neither calls
  `Math.Pow`. See "The platform's math library is not used", at the end.)*
- **A value_if_error left out is empty, not 0** (IFERROR-016): `=IFERROR(1/0,)&""` is `""`, as an
  empty cell was (IFERROR-013).
- **`XLOOKUP`'s regular expressions accept lookbehind** (XLOOKUP-153), which PCRE2 and .NET read the
  same. **A Unicode script (`\p{Greek}`) and `\w` over a character outside the Basic Multilingual
  Plane stay `#VALUE!`** (XLOOKUP-154, 155), by decision: .NET knows blocks, not scripts, and
  matches UTF-16 code units, so either answer would be an approximation that looks like Excel's and
  is not. They are recorded, not disagreements.
- **A format code with a lower-case `[color n]`, or with `[Color n]` in any section, is refused**
  (FMT-076, 077), as FMT-075 already is. Only COM was asked; the next run asks it typed, and asks
  `[Color3]0` in the first section (FMT-078), which the refusal covers but no run has observed.
- **Typed constants** (TYPED-051, 052, 054):
  - `$-5` is the number -5 in Excel's currency format, `$#,##0_);[Red]($#,##0)`, as `-$5` is.
  - `5-Oct` under en-GB shows `05-Oct`: the built-in `d-mmm` is shown in the culture's own form.
  - `-B2 C2` is **refused by name**, as a Formula using the intersection operator, which the
    engine does not implement. Excel makes it the Formula `=-B2 C2` (`#NULL!`). Taking it as text
    would show a plausible entry where Excel shows an error.
- **A Formula's result format follows every observed case** (FF-023..029, with FF-011, 013, 020,
  027 and ARITH-006, 064): a date referred to gives a date for `/` and inside `SUM` as for `+`
  (`=A1/2`, `=SUM(A1,5)`); a percent constant alone or multiplied gives General (`=50%`,
  `=2*50%`), where added to a number it gives `0.0%` (`=10+50%`); a cell in percent multiplied gives
  General (`=A1*2`); a date plus a percent gives General (`=A1+50%`). The rule written into the
  engine is the smallest one consistent with all of these, and the corpus gains cases that bracket
  it (a percent cell added to, subtracted from, divided; a date multiplied; a percent constant
  subtracted).
- **A column a typed entry widened is widened again by a longer entry** (CW-028), and Excel's file
  still marks it `customWidth` (CW-018, CW-027 for a date, CW-029 for a Formula's number). This is
  the third kind of width the second run anticipated: see ADR-0046, where a width **widened by
  entry** is recorded and marked as Excel marks it, and still widens, and only a width the user
  set stops widening.
  *(2026-10-12, the seventeenth Windows run, b10: typed, `12345678901` widened a standard column to
  11.18 at 150% display scale, written `width="11.81640625"`. The width an entry gives is Excel's
  pixels turned back into characters, and depends on the display's scale and the font. The engine
  harness widens to whole characters (11), and the component to its own pixels. So the corpus never
  names a width an entry gave: a typed case bounds it (`widthAtLeast`, `widthAtMost`), wide enough
  for Excel's at any scale and for the engine's, and narrow enough to tell it from the default width
  and from a width the user set (CW-021, 023, 025, 026). A width set by a command is still compared
  exactly (CW-022: 11).)*

Recorded, not changed: `=1E308` and `=-1E308` (TEXT-103, 113) stay refused, where Excel typed offers
to correct them into `=E1308` and `=-E1308`; and the deliberate differences of the earlier runs
(TABLE-011, 013, COPY-025, ISERROR-010, FILL-033).

## Settled by the equality run *(2026-09-29, decided with the user — verification/2026-09-29-windows-excel-equality)*

The third run's rule for comparing numbers above ("closer than 20 units in the last place of 1,
relative to each") is **replaced**.

The fourth run found Excel counting `1+x` equal to 1 up to x = 4.8E-15 (ARITH-137..139, where the
engine said FALSE). Two readings fitted every observation up to then:

- a relative threshold of about 4.9E-15;
- a comparison at 15 significant digits.

The equality run asked four cases chosen where the two disagree. Excel answered as the 15-digit
reading in all four, through COM and typed alike:

- `=9+3E-14=9` is FALSE;
- `=1+4E-15=1+6E-15` is FALSE;
- `=9+5E-15=9` is FALSE;
- `=1000+3.6E-12=1000` is TRUE.

Rounding both sides to 15 significant digits, half away from zero on the double's exact decimal
value, gives Excel's answer in all thirteen cases asked.

**The decision:** two numbers compare as they read at 15 significant digits. Each side is rounded
to 15 significant digits, and the rounded values are compared. **Every comparison operator** reads
through this rule (`=`, `<>`, `<`, `>`, `<=`, `>=`), so two numbers that compare equal are neither
less nor greater. Ordering was then observed in the fifth Windows run, and it agrees: ARITH-150..152 (`=1+4.4E-15>1`
FALSE, `=9+3E-14>9` TRUE, `=1+4.8E-15<=1` TRUE), through COM and typed
(`verification/2026-09-29-windows-excel-5/results.md`).

The final-addition rule (2⁻⁴⁹) is unchanged. It is a different adjustment, and the GRID
reimplementation of Excel documents the same boundary for it.

The equality run also settled ADR-0014's amendment. Plain text pasted over B2:C3 from C3 went into
B2, the top-left, and not into the active cell.

## The list grows: the first additions *(2026-10-03, decided with the user)*

The queue after the first set, with each candidate's status and priority, is catalogued in
[`docs/specs/exsheet-functions/spec.md`](../specs/exsheet-functions/spec.md). The catalogue orders
the queue; this ADR's rule still decides each admission, and a test holds the catalogue's
**Supported** rows equal to the declared set.

Admitted under that rule, as the catalogue's first ticket: `AND`, `OR`, `NOT`, `IFNA`, `ISBLANK`,
`ISNUMBER`, `ROUNDUP`, `ROUNDDOWN`, `ABS`, `INT`, `MOD`, `DATE`, `YEAR`, `MONTH`, `DAY`,
`EOMONTH`, `EDATE`, `LEFT`, `RIGHT`, `MID`, `LEN`, `TRIM`, `CONCAT` and `INDEX` (the array form).
Each case Microsoft documents is in the corpus as `documented`; each case it leaves open is there
as `uncertain`, with the engine's answer, for the next Windows run to ask.

Following "admitted with some argument values refused", three are refused until Excel answers: a
`MOD` whose quotient is 2^27 or more (`#NUM!`, the answer older versions of Excel were known to
give) *(answered by the seventeenth Windows run: see the note of 2026-10-12 under "The catalogue's last
Decide rows")*; `EOMONTH` and `EDATE` from or to a day before 1 March 1900, where the day that never
existed lies *(answered by the seventeenth Windows run and the area passes, but for `EDATE` from serial 0 to
January 1900, March 1900 or January 10000: see the notes of 2026-10-12 at the end)*; and a boolean typed into
`EOMONTH` or `EDATE` *(Excel's answer too as `EOMONTH`'s months and as `EDATE`'s start, the arguments the
seventeenth run typed one into; as `EOMONTH`'s start and as `EDATE`'s months it is refused until Excel is asked:
see the notes of 2026-10-12 at the end)*. The functions that answer about any Value —
`IFNA`, `ISBLANK`, `ISNUMBER` — give `#GETTING_DATA` and `#CIRC!` back, as `IFERROR` and `ISERROR`
do, and `INDEX` with a row or column of 0 over more than one cell is the spill refusal above.

`TEXT` is admitted under [ADR-0120](./0120-text-reads-its-format-code-in-the-invariant-spelling-as-a-cell-format-shows-it.md),
which settles the one thing this ADR left open for it: its format code is read in the invariant
spelling, under every culture.

`TODAY` is admitted under [ADR-0121](./0121-today-is-the-sheet-day-and-exsheet-keeps-it.md): it
answers the Sheet Day, which the engine is given as data and never reads from a clock.

Admitted as the catalogue's ticket 04, the P2 functions that were Ready: `PRODUCT`, `MEDIAN`,
`LARGE`, `SMALL`, `ISTEXT`, `ISNA`, `IFS`, `SWITCH`, `NA`, `XMATCH`, `CHOOSE`, `ROW`, `COLUMN`, `TRUNC`,
`POWER`, `SQRT`, `WEEKDAY`, `DAYS`, `NETWORKDAYS`, `WORKDAY`, `TEXTJOIN`, `CONCATENATE`, `SUBSTITUTE`,
`REPLACE`, `FIND`, `PMT`, `PV`, `FV` and `NPV`. The refusals they add are listed in the engine's
README. `ROW()` and `COLUMN()` read their own cell's place, so an insertion or a deletion that moves
the cell recalculates them though their text is unchanged.

Admitted as ticket 05, the P3 functions that were Ready: `RANK.EQ`, `STDEV.S`, `STDEV.P`, `VAR.S`,
`VAR.P`, `XOR`, `ROWS`, `COLUMNS`, `SIGN`, `EXP`, `LN`, `LOG10`, `PI`, `TIME`, `HOUR`, `MINUTE`,
`SECOND`, `REPT`, `EXACT`, `NUMBERVALUE` and `XNPV`. With them, every function the catalogue marked
Ready is Supported; what remains waits on a Windows run (Observe) or on a decision (Decide).

`VALUE` is admitted under [ADR-0124](./0124-volatile-functions-are-recalculated-after-every-change.md):
it reads its text under the Sheet's culture, as the arithmetic operators read text. `OFFSET` and
`NOW` are admitted under the same ADR, as volatile functions.

`FILTER`, `UNIQUE`, `SORT`, `SORTBY`, `SEQUENCE`, `TRANSPOSE` and `SUMPRODUCT` are admitted under
[ADR-0125](./0125-a-formula-whose-result-is-an-array-spills-as-excel-365s-does.md), which made a
result of more than one Value spill. With it, `INDEX` with a row or a column of 0, `XLOOKUP` with a
return array of several columns, and `ROW` and `COLUMN` over several rows or columns give Excel's
answer, and their corpus cases changed from a difference by decision to it. `UNIQUE` refuses a blank
in its array, and `SORT` and `SORTBY` a blank or an Error Value among their keys, with `#VALUE!`
until Excel is asked how it orders them. *(2026-10-12, after the two area passes: the blank is
answered among `SORT`'s and `SORTBY`'s keys and in `UNIQUE`'s array, and read inside the Formula,
where Excel's answers or the agreement of every reading settle it, or by generalisation from them,
which no run has read (ADR-0125's note of that day names them); where a function's readings of it
part and no generalisation covers it, the Formula is `#VALUE!`. Over 7 and then `#N/A`, Excel's
`=SORT(A1:A2)` shows 7 (SORT-015): an Error Value key is neither `SORT`'s result nor sorted before a
number. The `#N/A` followed the 7 already, so the answer fits a key sorted after the numbers and one
that keeps its place alike. `SORT` answers a key only where every such reading gives the one order —
its Error Values all one, and beside them nothing, or, sorted ascending, numbers alone, none after
an Error Value — and the Formula is `#VALUE!` with a number after it, beside text, a boolean, a
blank or another Error Value, and in a descending sort (SORT-026, 028 to 032 ask). `SORTBY` refuses
every Error Value key: a `by_array`'s could as well be the result, as `FILTER`'s `include`'s is
(SORTBY-010 asks). Each refusal is the Formula's, as a Formula that meets `#CIRC!` is `#CIRC!`, so
`IFERROR` and `COUNTA` around it give no answer.)* *(2026-10-13: a `sort_index` that is not a whole
number is refused. `=SORT(G1:H5,1.5)` was truncated to sort by the first column; rounded it would sort
by the second, and no run has read which Excel does, or whether it takes a fraction there at all.
SORT-042 asks.)*
*(2026-10-14, after run 18a: **an Error Value key is ordered, and a fractional `sort_index` truncated.** Over
`#N/A` and then 7 Excel's `SORT` shows 7 first (SORT-026), so an Error Value sorts after the numbers and does not
keep its place; it sorts after text and after a boolean (SORT-029, 030), before a blank (SORT-031), and first in a
descending sort (SORT-028), the order of the kinds reversed and the blank still last. `SORTBY` orders a
`by_array`'s Error Value the same way, not as the result (SORTBY-010). Over 7, `#N/A` and `#DIV/0!`, the second is
`#DIV/0!` (SORT-032): two different Error Values do not keep their order, and sort as `ERROR.TYPE` numbers them or
as their texts sort, which that one answer does not tell apart. So two different Error Values are ordered ascending
where the two readings agree, and refused with `#VALUE!` where they part, beside an Error Value `ERROR.TYPE` does
not number, and in a descending sort, which no run has read. `=SORT(G1:H3,1.5)` sorts by the first column
(SORT-042): a `sort_index` that is not a whole number is truncated, not rounded.)*

*(2026-10-03)* Admitted as the catalogue's ticket 07: `LOG`, `SIN`, `COS`, `TAN`, `COT`, `CSC`,
`SEC`, `ASIN`, `ACOS`, `ATAN`, `ATAN2`, `ACOT`, `SINH`, `COSH`, `TANH`, `COTH`, `CSCH`, `SECH`,
`ASINH`, `ACOSH`, `ATANH`, `ACOTH`, `DEGREES` and `RADIANS`. Where Microsoft's pages leave a case
open they refuse it: `SIN`, `COS` and `TAN` give `#NUM!` from 2^27, the limit documented for `COT`
and its kin, and `ACOTH` gives `#NUM!` where its page names both `#NUM!` and `#VALUE!`, until a
Windows run asks Excel.

*(2026-10-12)* The seventeenth Windows run asked ticket 07's open cases. `SIN`, `COS` and `TAN` give
`#NUM!` from 2^27, as `COT`, `CSC` and `SEC` are documented to (SIN-005, COS-005, TAN-005), so the
refusal is Excel's answer; `LOG` to base 1, and `CSC`, `COTH` and `CSCH` at 0, are `#DIV/0!`, and
`ACOTH` of an absolute value not above 1 is `#NUM!`, the answers the engine gave. `COTH`, `CSCH` and `SECH` are not limited: Excel answers `COTH(2^27)` with 1 and `CSCH` and
`SECH` of it with 0, past the limit Microsoft documents for them, so the engine answers them too. And
Excel reduces an angle by pi rounded to the x87's 64-bit significand, not by `PI()`'s double:
`SIN(PI())` is the difference between the two, 1.22514845490862E-16, where the double's own sine is
1.2246467991473532E-16. `SIN`, `COS` and `TAN` are the doubles nearest the exact values of the angle so
reduced, and `COT`, `CSC` and `SEC` 1 over them, divided as doubles; that reproduces every number Excel
returned for the six, and the engine computes it without the platform's math library, so every runtime
gives the same doubles. `spikes/exsheet-trig-reduction/` records each candidate and what it reproduced.
More than one construction of `TAN`, `COT` and `SEC` fits every observation; the engine takes the one
that rounds least, and TAN-006 and 007, COT-006 to 008 and SEC-005 ask the next run which is Excel's.

## Functions admitted from the October 3 observations *(2026-10-03)*

The admission rule is unchanged. The Windows run at
`verification/2026-10-03-windows-functions/` recorded all 24 Observe functions, plus LET and the
two random functions. Implementation tickets 08–12 admit these 21 functions:

- `SUMIF`, `SUMIFS`, `COUNTIF`, `COUNTIFS`, `AVERAGEIF`, `AVERAGEIFS`, `MAXIFS`, `MINIFS`,
  `COUNTBLANK`;
- `VLOOKUP`, `HLOOKUP`, `MATCH`;
- `MROUND`, `CEILING.MATH`, `FLOOR.MATH`;
- `YEARFRAC`, `DATEDIF`;
- `SEARCH`, `UPPER`, `LOWER`, `PROPER`.

The cases keep the observed input kinds: an absent cell, formula-empty text, numeric text, a
number, a boolean and an Error Value are not interchangeable. Exact expected doubles come from
the recorded round-trip spelling, checked against their bits, not PowerShell's rounded JSON
numeric spelling. Existing Supported functions' uncertain cases were outside this run.

The following apply the existing permission to refuse argument domains, rather than infer an
answer the evidence does not establish:

- Criteria admit ASCII equality/wildcards and numeric comparisons. Culture-sensitive numeric
  spellings, numeric text over 15 mantissa digits, non-ASCII matching, text relational ordering
  and criteria over 255 characters are refused with `#VALUE!`. Error Values can themselves be
  criteria. The strict propagation of `#CIRC!` and `#GETTING_DATA` is unchanged.
- `SUMIF` and `AVERAGEIF` read a result footprint the size of the criteria range, starting at the
  result range's top-left. For literal References this implicit footprint is also a dependency:
  changing it recalculates, cycles cannot hide in it, and structural edits rebuild it. Resizing a
  computed Reference remains `#VALUE!` until its dependency footprint can be established. IFS
  ranges must agree in both dimensions, not just cell count.
- Legacy approximate lookups retain the sorted-data refusal and the conservative homogeneous,
  nonblank, error-free domain. Exact text is ASCII; ordered text contains only ASCII letters,
  digits and spaces. Unobserved outcomes across Error Values, descending nearest non-equal
  duplicate keys in MATCH, and keys over 255 characters are refused. These functions' observed
  duplicate choices are distinct from XLOOKUP's and do not change it.
- `MROUND` admits whole-number multiples. A nonzero fractional multiple with a nonzero operand
  of the same sign is `#VALUE!`: Excel's observed decimal midpoint direction is not reproduced
  by an ordinary binary rounding operation. Opposite signs still give `#NUM!`, and zero gives
  zero. No guessed epsilon is used.
- `CEILING.MATH` and `FLOOR.MATH` refuse an unobserved integer boundary with `#VALUE!`: a
  division result that is nonintegral in binary64 but integral at the existing 15-digit
  precision. That test refuses the case rather than choosing a numeric answer. Underflow
  of a nonzero quotient is `#NUM!`.
- `DATEDIF` keeps the existing `MD` refusal. `YD` answers only before a completed anniversary;
  longer intervals are `#VALUE!` until which calendar supplies the remaining leap day is pinned.
- Casing and SEARCH answer the explicit character domains documented in the engine's README.
  Other characters and unobserved sigma contexts give `#VALUE!`, including supplementary-plane
  cases whose observed answers do not establish the general matching or casing algorithm.

The corpus retains Excel's answer alongside each deliberate refusal. Broadening a domain is
future work, never a reason to loosen a comparison.

`IRR`, `XIRR` and `RATE` remain undeclared: the reproducible experiment in
`spikes/exsheet-financial-solvers/` found differences in chosen root, stopping result and failure
cases at the required 15 significant digits. It does not prove that a matching algorithm is
impossible. LET's Formula-local names and random functions' reproducibility/persistence still
need their own decisions. Observing those functions did not admit them.

## The catalogue's last Decide rows *(decided with the user, 2026-10-10)*

The rows of the function catalogue that still waited on a decision were put to the user together
(`docs/specs/exsheet-functions/spec.md`, tickets 13 to 18).

- **`RAND` and `RANDBETWEEN`** draw from the Sheet Seed, and draw again at each change:
  [ADR-0180](./0180-rand-and-randbetween-draw-from-the-sheet-seed-and-draw-again-at-each-change.md).
- **`LET`** binds names inside one Formula, as Excel's keyboard accepts them:
  [ADR-0181](./0181-let-binds-names-inside-one-formula-as-excel-accepts-them-from-the-keyboard.md).
- **`IRR`, `XIRR` and `RATE` stay undeclared, and no work goes into them for now.**
  - Microsoft documents only their iteration's limits and tolerances. Those are eight to ten
    orders of magnitude coarser than the fifteen digits this ADR compares, and the iteration
    itself is not documented.
  - So no domain of their arguments can be shown to match Excel from what is documented, and none
    is admitted with the rest refused.
  - They wait for an implementation that reproduces every observed case.
- **`SUBTOTAL` and `AGGREGATE` are admitted while a Sheet cannot hide a row.**
  - A Sheet has no hidden or filtered row yet
    ([ADR-0046](./0046-exsheet-is-a-general-purpose-sheet-drawn-by-exgrid-as-its-consumer.md)). So
    the codes that skip hidden rows (101–111, and `AGGREGATE`'s options that do) give what the
    others give, as they do in Excel over such a sheet.
  - A test names this, and fails on the day a row can be hidden, as SH-37's does for its refusal.
  - Microsoft documents that both skip nested subtotals, but not which cells count as nested. A
    cell whose whole Formula is a `SUBTOTAL` is skipped by `SUBTOTAL`. A cell whose whole Formula
    is a `SUBTOTAL` or an `AGGREGATE` is skipped by `AGGREGATE`'s options 0 to 3. Any other call
    of either function inside the range makes the result `#VALUE!` until a Windows run asks Excel:
    a call inside a larger Formula, or an `AGGREGATE` inside a `SUBTOTAL`'s range.
  - `AGGREGATE`'s function numbers for functions the engine does not support are `#VALUE!`.
  *(2026-10-12)* **What the seventeenth Windows run read, and the engine now follows.** As this note and
  SH-68's said it would once a run had read Excel's answer:

  - **A cell is nested when its Formula calls `SUBTOTAL` or `AGGREGATE` anywhere**, or, for a cell of a
    Spill Range, when its Anchor's does: the whole Formula, inside a larger expression, in parentheses,
    under `IF`, plus 0 (SUBTOTAL-020, 021, 035 to 038; AGGREGATE-026, 052 to 054). `SUBTOTAL`, whatever its
    function number, and `AGGREGATE`'s options 0 to 3 pass over it. Options 4 to 7 count its Value, in the
    reference form and the array form alike (AGGREGATE-025, 042 to 051), as Microsoft documents. No
    `#VALUE!` for nesting remains. The rule reads the Formula's text: a call in a branch `IF` never takes
    is nested too, which SUBTOTAL-039 asks Excel.
  - **A function number that is not whole is truncated toward zero**, and the range checked after:
    `SUBTOTAL(9.5,…)` is `SUBTOTAL(9,…)` (SUBTOTAL-027). `AGGREGATE`'s function number and option are
    truncated the same way, as LibreOffice reads both; AGGREGATE-055 and 056 ask Excel.
  - **An argument Excel takes only as a Reference refuses, on entry, what can never be one.** Excel would
    not enter `SUBTOTAL(9,SEQUENCE(3))`, `SUBTOTAL(9,5)`, `RANK.EQ(1,1)` or `AGGREGATE(14,6,A1:A11,1,2)`,
    and entered `AGGREGATE(9,6,A1:A11*1)`, `AGGREGATE(14,6,5,1)` and a number as `AGGREGATE`'s fourth
    argument. So `SUBTOTAL`'s references, `RANK.EQ`'s `ref`, and `AGGREGATE`'s fifth and later arguments
    refuse a constant, an operator's result (parentheses around a Reference excepted) and a call of a
    function that never gives a Reference, as an argument count is refused. A call of `INDIRECT`,
    `OFFSET`, `INDEX`, `CHOOSE`, `IF`, `IFS`, `SWITCH`, `XLOOKUP` or `LET`, a name, an Error Value (where a
    deletion left `#REF!`) and an argument left empty are taken, and judged when computed. A Sheet
    Document holding a Formula so refused is refused when it is read, and pasted such a Formula is text,
    as one that cannot be read is (ADR-0048). The other functions' reference arguments (`SUMIF`'s
    `range`, `COUNTBLANK`'s, `OFFSET`'s `reference`, `ROW`'s and `COLUMN`'s) have not been asked, and keep
    their `#VALUE!` until they are.
  - **`AGGREGATE`'s array form takes a number given directly** as an array of that one Value
    (AGGREGATE-039: `AGGREGATE(14,6,5,1)` is 5). Text and a boolean given directly have not been asked,
    and stay `#VALUE!` (AGGREGATE-061). *(2026-10-12, the area passes: text given directly is such an
    array too, and holds no number, since it is not coerced: `AGGREGATE(14,6,"5",1)` is `#NUM!`, neither
    5 nor `#VALUE!` (AGGREGATE-061), and so are a boolean and text that spells no number (AGGREGATE-064,
    065). An Error Value given directly is the result, as an Error Value argument is, and option 6 does
    not pass over it: `AGGREGATE(14,6,1/0,1)` is `#DIV/0!` (AGGREGATE-067). `LARGE` and `SMALL`
    themselves coerce a Value given directly: `LARGE("5",1)` is 5 and `SMALL(TRUE,1)` 1 (LARGE-014,
    SMALL-010). The engine follows each.)*
  - **`LARGE`'s k that is not whole is rounded up**: `LARGE(A1:B5,1.5)` and `AGGREGATE(14,6,A1:A11,1.5)`
    are each the 2nd largest (LARGE-007, AGGREGATE-038), which rules out truncation; ODF 1.3's `LARGE`
    (6.18.40), LibreOffice and Gnumeric round it up, and rounding to nearest cannot be told from it at .5
    (LARGE-008 and AGGREGATE-059 ask 1.2). `SMALL`'s k that is not whole, where LibreOffice and ODF round
    down and Gnumeric up, and a k between 0 and 1, where they disagree too, stay `#VALUE!` until Excel is
    asked (SMALL-004, 005, AGGREGATE-060, LARGE-011). k is read at 15 significant digits first, as both
    implementations do, so `1+2^-52` is 1 (LARGE-012, SMALL-006). *(2026-10-12, the area passes: Excel
    truncates `SMALL`'s k, as LibreOffice and ODF do: `SMALL(A1:A9,1.5)` and `SMALL(A1:A9,1.2)` are each
    the smallest (SMALL-004, 005), `SMALL(A1:A9,3.9)` the 3rd (SMALL-009), and `AGGREGATE(15,6,A1:A11,1.2)`
    is 30 (AGGREGATE-060). `LARGE`'s 1.2 is the 2nd largest (LARGE-008, AGGREGATE-059), so `LARGE` rounds
    up, not to nearest. A k below 1 or above the count of the numbers is `#NUM!`, whole or not, before it
    is rounded: `LARGE(A1:B5,0.5)` and `LARGE(A1:B5,0.99)` are `#NUM!`, not rounded up to 1 (LARGE-011,
    013), as `SMALL(A1:A9,0.5)` is (SMALL-007), and `SMALL(A1:A9,9.5)` over nine numbers is `#NUM!`, not
    truncated to the 9th smallest (SMALL-008). The two are one rule — the k-th largest with k rounded up
    is the (n+1-k)-th smallest with that index truncated, and k lies within 1 to n exactly where n+1-k
    does. The engine follows it, and no k is `#VALUE!` any more. SMALL-011 and AGGREGATE-068 ask a k above
    the count again.)*

  The `MOD` refusal of "admitted with some argument values refused" above is replaced: Excel 365 answers
  `MOD(2^27,1)` with 0 (MOD-008). Microsoft documents no limit, and names
  `MOD(n, d) = n - d*INT(n/d)`, which the engine computes in doubles and which gives Excel's digits on every
  case read (MOD-009's 0.09999999999999998 among them). A Microsoft 365 user reported on Microsoft's Q&A
  (2021) that `MOD(1125899999999,1)` is 0 and `MOD(1125900000000,1)` is `#NUM!`, a limit Excel 2010 brought
  in; the engine answers a quotient below 1,125,900,000,000 and is `#NUM!` from it, and MOD-010 to MOD-017
  ask Excel on both sides of it. *(2026-10-12, the area passes: the limit holds on both sides (MOD-010 to
  016), but the doubles are not Excel's on every case. Excel's remainder is exact:
  `MOD(123456789012,0.7)` is 0.5000078322611261 (MOD-017), where the identity in doubles gives
  0.5000152587890625, and `MOD(1,0.1)` is 0.09999999999999995 (MOD-020), where the double quotient, 10,
  gives 0. A remainder a hair from 0 is itself, with no cancellation: `MOD(1.1,0.1)` is
  2.7755575615628914E-17 (MOD-030). Where the divisor is added to a remainder of the other sign, the sum
  is not rounded once: `MOD(-0.087626,6630)` is 6629.9123739999995 (MOD-029), the sum held at 64 bits,
  rounded to nearest or truncated, and then stored as a double, where rounded once it is 6629.912374;
  and a sum stored as the divisor itself is 0: `MOD(-1E-20,1)` is 0 (MOD-032). The first pass's answers
  left seven classes of evaluation, and the second pass's ruled out all seven. Of 75 evaluations tried
  against the 32 cases Excel answered (`spikes/exsheet-financial-evaluation/mod-search.cs`), 34 give
  every one. At the hardware's widths, a double's 53 bits and an x87 register's 64, three: the divisor
  added in an x87 register's 64 bits, or truncated at 64 bits, and stored as a double, a sum that is the
  divisor taken as 0 in the double or, for the x87's, in the register. The other 31 are the same with
  the sum held at a width no hardware holds, 54 to 63 bits rounded or 54 to 65 truncated. The three at
  64 bits part where the sum is the divisor as a double but not at 64 bits (`MOD(-1E-17,1)` is 0 or 1)
  and where truncating at 64 bits stores another double than rounding there (`MOD(-0.084474,2270)` is
  2269.915526 or 2269.9155259999998). So, as "admitted with some argument values refused" and ADR-0202
  require, the engine answers as the three where they agree and is `#VALUE!` where they part, 13 of
  1,915,648 random inputs of up to five digits, until a Windows run asks MOD-033 to MOD-036. It does not
  model the other widths: that only the hardware's formats are natural intermediates is a judgment, which
  no observed answer settles, and on it the engine answers 65,972 of those inputs (3.4%) where one of the
  31 gives another double, about 2% at 54 bits; MOD-039 and MOD-040 ask two.)* *(2026-10-13: the
  judgment is accepted, and the engine models the widths of 53 and 64 bits alone.)* *(2026-10-14, after run 18a:
  **`MOD` refuses nothing.** Excel's `MOD(-1E-17,1)` is 0, `MOD(-0.084474,2270)` 2269.915526,
  `MOD(-0.008511,81200)` 81199.99148900001 and `MOD(0.071472,-276)` -275.92852800000003 (MOD-033 to 036): of the
  three evaluations at 64 bits, the sum rounded to nearest in the register, stored as a double and taken as 0 where
  that double is the divisor (`B9`), alone gives all four, and the engine computes it everywhere. MOD-039 and 040
  agree with it, where the widths no hardware holds give another double. MOD-039 was reported as a disagreement
  only because the oracle read the corpus's decimal through .NET Framework's `Double.Parse`, which is not correctly
  rounded beside a midpoint; `oracle.ps1` now rounds the decimal once, as the engine's tests do.)*
- **`ERROR.TYPE` gives `#GETTING_DATA` and `#CIRC!` back**, as `IFNA` and `ISBLANK` do (above), and
  never a number for them.
  - Microsoft's table numbers `#GETTING_DATA` 8. Answering 8 would let a Formula show a number
    while the data it needs is still on its way
    ([ADR-0049](./0049-linked-tables-are-the-consumers-data-read-by-key.md)).
  - An Error Value outside Microsoft's table is `#VALUE!` until Excel is asked: `#SPILL!`,
    `#CALC!`, and the typed `#FIELD!`, `#BLOCKED!`, `#CONNECT!` and `#UNKNOWN!`. Not `#N/A`, which
    is the table's answer for a Value that is no error, and would read as one.
  *(2026-10-12, the seventeenth Windows run: Excel numbers them `#SPILL!` 9, `#CONNECT!` 10,
  `#BLOCKED!` 11, `#UNKNOWN!` 12, `#FIELD!` 13 and `#CALC!` 14. The typed ones were asked with real
  keys; through COM Excel reads each as `#N/A`, which is not its answer. `ERROR.TYPE` now gives those
  numbers, as the admission rule requires. An Error Value the engine gains later is `#VALUE!` until
  Excel is asked. `#GETTING_DATA` and `#CIRC!` are still given back, and a typed `#BUSY!` is still
  text, so its `ERROR.TYPE` is `#N/A`.)*
- **`INDIRECT`** is admitted for A1-style text naming a place on the same Sheet:
  [ADR-0124](./0124-volatile-functions-are-recalculated-after-every-change.md)'s note of this date.

*(2026-10-11)* Admitted as the catalogue's tickets 14 to 18, as decided above: `LET`, `RAND`,
`RANDBETWEEN`, `SUBTOTAL`, `AGGREGATE`, `ERROR.TYPE` and `INDIRECT`. The questions each leaves for Excel
are uncertain cases in the corpus, but for two of `RAND` and `RANDBETWEEN` that the corpus cannot ask
(what an undo does to a draw, and whether a rename draws again, ADR-0180; answered by the
seventeenth Windows run: ADR-0180, note of 2026-10-12), and the sign-off's Windows run
puts them all to Excel
([ADR-0202](./0202-what-0-1-0-holds-of-exsheet-and-expivot-and-where-it-says-what-it-does-not-do.md)).
`LET` refuses as a name any of Excel's function names, not only the declared ones
([ADR-0181](./0181-let-binds-names-inside-one-formula-as-excel-accepts-them-from-the-keyboard.md), note of
this date).

*(2026-10-12, the seventeenth Windows run: **`LET` refuses no function's name any more.** Excel's keyboard took
`SUM`, `IRR` and `N` as `LET` names, and an R1C1 shape and a value left empty with them, so ADR-0181's note of
2026-10-11 is superseded. A name is refused on entry only where it reads as an A1 Reference, or as `TRUE` or
`FALSE` (ADR-0181, note of 2026-10-11, run 17).)*

*(2026-10-12)* **The seventeenth Windows run answered three of this ADR's refusals and guesses, and they
follow Excel's answer** (`verification/2026-10-11-windows-17/`, ADR-0202).

- **`EOMONTH` and `EDATE` before 1 March 1900** count months on the real calendar, where February 1900 has 28
  days, and a day before 1 March is its own serial, 1 to 59. `EOMONTH(31,1)` is 59, 28 February, and not 60;
  `EDATE(DATE(1900,3,15),-1)` is 46 (EOMONTH-008, EDATE-008). A start of serial 0 or 60 is no day of that
  calendar, and stays `#VALUE!` until Excel is asked *(answered by the area passes, but for `EDATE` from serial
  0 to January 1900, March 1900 or January 10000: see the note of 2026-10-12, the area passes)*. A boolean typed
  as `EOMONTH`'s months or as `EDATE`'s start is `#VALUE!`, and that is Excel's answer too (EOMONTH-009,
  EDATE-009) *(the bullet said "typed into either" until the area passes; the run typed it into these two
  arguments alone, and as `EOMONTH`'s start and `EDATE`'s months the engine's `#VALUE!` is a refusal until Excel
  is asked: see the note of 2026-10-12, the area passes)*.
- **`NETWORKDAYS` and `WORKDAY`** count every serial from 0. Each takes the day of the week `WEEKDAY` gives it:
  serial 0 a Saturday, 1 a Sunday, 60 a Wednesday. `NETWORKDAYS(30,100)` is 51 (NETWORKDAYS-005). `WORKDAY`
  reads its dates as `NETWORKDAYS` does, and a result before serial 0 is `#NUM!`. The cases that tell
  `WEEKDAY`'s day from the real calendar's are `uncertain` until a run asks them *(the area pass asked them,
  and Excel's day is `WEEKDAY`'s: NETWORKDAYS-006 to 008, WORKDAY-006 to 008)*.
- **`SUBSTITUTE` with `instance_num`** counts the occurrences at every character, overlapping ones included:
  `SUBSTITUTE("aaaa","aa","b",2)` is `aba` (SUBSTITUTE-005). The engine counted them without overlapping, which
  was a guess, and wrong. Without `instance_num`, every occurrence is replaced left to right, none overlapping.

*(2026-10-12)* **PV and FV give Excel's double, and PMT every double Excel was seen to give.** The
seventeenth Windows run read Excel's double for every financial case. Seven of PMT's, PV's and FV's
differed from the engine's in the 13th to 15th significant digit, and PMT-008 was refused: the
engine evaluated Microsoft's identity in doubles with a correctly rounded power, and Excel does
neither. `spikes/exsheet-financial-evaluation/` searched for the evaluation that gives every
observed double, the agreeing cases included, among some 13,000 candidates:

- **PV and FV** are evaluated in doubles, in the identity's order, with 1+rate raised to `nper` by
  squaring from the exponent's lowest bit. Excel's FV-001 lies 39 units in the last place from the
  correctly rounded power's evaluation; squaring gives FV-001 to 003, PV-001 and PV-003 to the bit.
- **PMT** is not. Excel's payments equal the payment at the end of each period carried beyond a
  double and rounded once, and, at the start, that double divided by 1+rate, unrounded, and rounded
  once (PMT-001 to 003 and 008). The engine takes the exact model: the payment computed exactly over
  Excel's doubles and rounded once. It gives all four observed doubles, and so do 2504 of the
  evaluations the search tried, so the observations do not choose it. 304 of those are in an x87's
  64 bits. They agree with the exact model at the rates observed, 0.5% to 10% a period, and part
  from it as the rate shrinks, because an x87's 1+rate keeps only the rate's bits above 2^-64. At PMT-016, 38 of them are one unit in the
  last place away. At a daily rate over a year they give four doubles across 7 units (PMT-018). At a
  rate of 10^-7 they give nine across 3,390 units, from the 13th significant digit (PMT-017). PMT-013
  to 018 ask Excel.
- **A `type` that is a whole number other than 0 is payment at the start**, as Excel read 2
  (PMT-008). A fractional one stays `#VALUE!` until Excel is asked. *(Answered by the area passes of
  2026-10-12: `type` is a truth value. Excel gave the payment at the start for 0.5, −1 and 0.1 (PMT-009,
  PMT-010, PMT-019), so a fraction is not truncated or rounded, and any `type` other than 0 is payment at
  the start, in PMT, PV and FV.)*
- Following "admitted with some argument values refused", at a rate other than 0 an `nper` that is
  not a whole number from 0 to 2^53 is `#VALUE!`: squaring raises only to a whole number, and how
  Excel raises 1+rate to any other has not been observed. At a rate of 0 no power is raised, and any
  `nper` is answered. *(Answered by the area passes of 2026-10-12 for PV and FV. Excel's doubles are
  those of raising 1+rate to a negative `nper` as 1 over the power to its absolute value (FV-007,
  FV-022), to 0.5 by the square root (FV-016), and to any other fraction as e^(nper ln(1+rate)) with
  the exponential correctly rounded (FV-006, PV-005; XNPV-013 tells it from glibc's) and the logarithm
  correctly rounded too, which no observed double tells from glibc's (FV-023 and PV-016 ask). Excel
  gave a negative 1+rate to a fraction whose reciprocal is odd its odd root (FV-017) and to another
  fraction `#NUM!` (PV-011), and 1+rate of 0 to the power 0 `#NUM!` (FV-018) and to a negative power
  `#DIV/0!` (FV-020, FV-021, PV-012). PV and FV now answer every `nper` so, but PV at a rate of −1 with
  an `nper` above 0, which divides by a growth of 0 and is refused until Excel is asked (PV-013). PMT
  answers any `nper` at a rate above −1 where it does not refuse, under the exception for PMT below,
  whose note says what it gives and where it refuses (PMT-011, PMT-012); below −1 a fraction whose
  reciprocal is not odd is `#NUM!`, and the rest at −1 and below is refused until Excel is asked. Past
  2^53, where every double is a whole number, all three still refuse.)*

The cases the search could not settle — which of two orders of the payment's factors, and which of
the evaluations that also give PMT-001 to 008 — are `uncertain` in the corpus, with the engine's
double, for the next Windows run.

*(2026-10-12, the two area passes, `verification/2026-10-12-windows-area/` and `-area-2/`.)* **What
those cases answered.** The order of the payment's factors: Excel's doubles are the payment times
the product of 1+rate·type and ((g−1)/rate) (FV-009, PV-007, where the engine's order was one unit in
the last place below), and with a present value as well the sum as written, not a fused multiply-add
(FV-014, FV-015). PV and FV raise 1+rate by one power, the note's squaring at a whole number and the
bullet above otherwise: `spikes/exsheet-financial-evaluation/` searched 480 evaluations of FV and 480
of PV, and this power is the one that gives all 17 and 9 of their observed doubles and their Error
Values with the exponential XNPV-013 tells apart. XNPV divides each value by that power of its days
over 365 and sums from the first value: of 2,592 candidates, 4 give its nine observed doubles, two
evaluations, this one and the same with Excel's last-addition adjustment, which parts from it only
where the last addition nearly cancels and is refused there until asked. None of the four gives
XNPV's `#NUM!` at a negative rate: the engine checks the rate before it evaluates and is `#NUM!`
below 0, as Excel was at −0.5, −1, −1.5 and −2, its own answer at other negative rates until
XNPV-020 is read, and refuses a rate of 0 until asked. The engine now computes e^x and ln x correctly
rounded on every platform; the platform's `Math.Exp` is a unit in the last place from Excel's
discount at XNPV-013, and no observed double tells the logarithms apart. **PMT's evaluation is not
settled**: of 39,602 evaluations searched, none gives all 29 observed payments at a rate other than
0 and the best give 16; 19 of the 30 observed payments were 1 to 11 units in the last place from the
engine's, and 23 are since the payment at the start is rounded once (the note of 2026-10-13 below), which
PMT keeps by the exception that follows.

*(2026-10-12, decided with the user)* **An exception for `PMT` alone: it is kept though its result
may differ from Excel's in its last digits, a Known limitation of `0.1.0`.** Two area passes asked Excel
29 payments at a rate other than 0, and some 58,000 evaluations were searched; none gives them all. The
decision was taken as the engine giving the exact payment over Excel's doubles, rounded once, and
Excel's payment differing from the engine's at 19 of the 30 observed, by 1 to 11 units in the last
place (PMT-026, a relative error of about 1e-15), in digits taken to be ones no format displays, and to
be Excel's own rounding. `PMT` is kept with that difference.
Its corpus cases record each as a difference by this decision, with Excel's double beside the
engine's. `NPV` is not part of it, and stays undeclared (ADR-0202's note of this date). `0.1.0` lists
it under Known limitations (ADR-0202). *(2026-10-12, later, found by the financial area and put to
the user: at the start of each period the engine's payment is the payment at the end's double divided
by 1+rate and rounded again, not the exact payment rounded once; it is Excel's double at PMT-008, 009,
010 and 019, where the exact payment at the start rounded once is a unit away, and a unit from the
exact payment rounded once at PMT-014, 015, 025 and 029. At PMT-013 and PMT-026, at the end, Excel's
double and the exact payment rounded once differ in the 15th significant digit, which General shows,
and at PMT-025 the engine's double differs there from both, so that "no format displays" does not
hold there.)*

*(2026-10-13, decided with the user: **the exception, restated as observed.** Excel's double and the
exact payment rounded once differ by 1 to 11 units in the last place, and at PMT-013 and PMT-026 that
shows in General's 15th significant digit; `PMT` is kept there too. The payment at the start is the
exact payment at the start rounded once, held between bounds as the payment at the end is, and the
second rounding that gave Excel's double at PMT-008, 009, 010 and 019 is withdrawn: those four now
differ from Excel's by a unit in the last place, and PMT-014, 015, 025 and 029 lie a unit nearer it.
Excel's double differs from the engine's at 23 of the 30 observed payments, by 1 to 11 units in the
last place, at two of them, PMT-013 and PMT-026, in the 15th significant digit; at PMT-025 the two agree
to 15 digits. Where a reading magnifies its own roundings more than 2^6 times, `PMT` stays refused.)*

*(2026-10-12, how the engine applies the exception: the financial area's choices, not the user's;
three of them were put to the user, and decided on 2026-10-13.)* Where `PMT` answers, the payment at
the end of each period is
held between two bounds, interval arithmetic over exact dyadic numbers with (1+rate)^nper − 1 raised
from the rate itself, and its double, the exact payment rounded once however small the rate, is given
only where both bounds round to it (PMT-011, PMT-012, each one unit in the last place from Excel's);
the payment at the start is the exact payment at the start, held between bounds in the same way and
rounded once *(2026-10-13, decided with the user; it was that double divided by 1+rate exactly and
rounded a second time, which is not everywhere the exact payment at the start rounded once)*.
`spikes/exsheet-financial-evaluation/pmt_exact.py` checked 20,000 inputs against Python's decimal at
1200 digits. PMT's readings are taken as the evaluations within 64 units in the last place of Excel's
at every observed payment, and where they part in kind, a number against an Error Value or one Error
Value against another, or beyond the last digits, `PMT` is refused with `#VALUE!` until Excel is
asked (PMT-034 to PMT-053; `Search.cs --refusals` and `--pmt-readings` run them with a double's
range):

- at a rate of −1 with an `nper` other than 0, and below −1 with a whole `nper` or a fraction whose
  reciprocal is odd; any other fraction there is `#NUM!`, which every reading gives;
- where a quantity a reading computes passes a double's range or falls below its normal numbers, or
  the growth lies below 2^−52, which a reading that takes it as 1 + (g − 1) holds as 0;
- with no periods at the start, where LibreOffice's reading divides by a rounding's remainder;
- where a reading magnifies its own roundings more than 2^6 times, so that the readings give 0,
  numbers of the other sign or of a few significant digits, or an Error Value: where pv·g and fv
  nearly cancel, where a growth far below 1 is taken as 1 + (g − 1), where a rounding of the exponent
  moves the payment, and at the start where LibreOffice's divisor comes out as 0 or far from
  (1+rate)·((1+rate)^nper − 1), as at a tiny `nper`, or where the payment at the end less its rate's
  share cancels.

Where it answers, every reading run lies within 93 units in the last place of its payment.

*(2026-10-14, after run 18a: **what Excel answered inside `PMT`'s, `PV`'s and `XNPV`'s refusals.**
`verification/2026-10-14-windows-readings-18a/area/summary.md` has each double.

- **`PMT` at a rate of -1 or below is `#NUM!`**: at -1 with periods after and before (PMT-034, 037), at -2 with 2
  periods (PMT-035) and at -9 with a third of one (PMT-036). PV's and FV's power gives a number at PMT-036 and
  `#DIV/0!` at PMT-037, so `PMT` does not raise 1+rate as they do; a power through ln(1+rate) has no logarithm there
  and gives `#NUM!` at every such rate, and the engine gives it at every rate of -1 or below, elsewhere than the four
  by generalisation. No periods at the start is `#NUM!` too (PMT-046), as at the end.
- **The exact payment rounded once is Excel's where a quantity passed a double's range** at PMT-038 to 041 and 044,
  and where the growth lay below 2^-52 at PMT-045. Those quantities, and that bound, no longer refuse; the rest of
  the range rule stands as the engine's.
- **Where Excel's answer needs another evaluation, `PMT` stays refused**, under the exception and the bound of 2^6
  decided with the user, and the corpus keeps Excel's answer beside each refusal: `#NUM!` where the growth falls below
  the normal numbers (PMT-042, where the exact payment rounded once is -495), and, inside the bound, an answer 1 or
  2 units in the last place from the exact payment at PMT-047, 051 and 053, the exact payment at PMT-050, and
  another evaluation's at PMT-048 (its 10th significant digit), PMT-049 (the other sign) and PMT-052 (0, for
  -9.3E-299). No evaluation searched gives PMT-048, 049, 051 or 052. *(2026-10-16, decided with the user: the bound
  is not narrowed and PMT-042's `#NUM!` is not given; all eight stay refused, Excel's answers beside them.)* PMT-043, which COM would not enter (0x800A03EC), stays `uncertain`
  for the keys pass (`docs/specs/exsheet/run-18/area-oracle.md`).
- **`PV` at a rate of -1 with periods is `#DIV/0!`** (PV-013): it divides by the growth, 0. PV-014 and 015, and
  FV-023 and PV-016, agree with the power the note of 2026-10-12 chose and the correctly rounded logarithm.
- **`XNPV` sums plainly and is `#NUM!` at a rate of 0.** Excel's XNPV-018 is 1.1368683772161603E-13, where the
  last-addition adjustment gives 0, so of the two evaluations the plain sum is Excel's; XNPV-019 is `#NUM!`, so the
  check is "0 or below". XNPV-021 to 030 settle part of the order of its checks: the rate before the counts and before
  the values, the counts before the values, a date's Error Value before a value's at the same place, an Error Value
  among either before a later date before the first, and the first of two Error Values among the values. The orders
  that give all ten are kept, and `XNPV` answers where every one gives the same answer and refuses with `#VALUE!`
  where they part; a cell that is not a number stays refused where an order reaches it.)*

*(2026-10-12, the seventeenth Windows run)* **`AND`, `OR` and `XOR` pass over typed text that spells
neither TRUE nor FALSE** (AND-011), as they pass over text inside a range; with nothing that counts, the
result is still `#VALUE!`. The corpus had refused it with `#VALUE!` on the engine's guess. OR-009,
XOR-006 and AND-014 ask Excel the rule's other cases. That typed text spelling TRUE or FALSE still
counts is the engine's answer, which no observation settles (AND-010 is TRUE either way); AND-015 and
OR-010 ask it.

*(2026-10-12, the area passes)* **`EOMONTH` and `EDATE` from serial 0 or 60, and from a start between -1 and 0,
follow Excel's answers** (`verification/2026-10-12-windows-area/` and `-area-2/`, ADR-0202). `EOMONTH(0,1)` is 59,
`EDATE(60,1)` is 89 and `EDATE(0,1)` is 31 (EOMONTH-010, EDATE-010, EDATE-016), and `EOMONTH(-0.5,0)` is `#NUM!`
(EOMONTH-016).

- **The start is read as `YEAR`, `MONTH` and `DAY` read it**: cut down to its day, with serial 0 0 January 1900 and
  serial 60 29 February 1900 (YEAR-003, MONTH-002 and 004, DAY-002 and 004). The months are counted from its year
  and month on the real calendar, as the seventeenth run's note says, and `EDATE` keeps its day, or the month's last
  day where the month is shorter (EOMONTH-012 to 015, EDATE-013 to 015).
- **A date between -1 and 0 is cut down to -1, and is `#NUM!`**, under `YEAR`, `MONTH`, `DAY`, `WEEKDAY`, `DAYS`,
  `NETWORKDAYS`, `WORKDAY`, `EOMONTH` and `EDATE` alike (YEAR-008, MONTH-005, DAY-005, WEEKDAY-008, DAYS-005,
  NETWORKDAYS-009, WORKDAY-010, EOMONTH-016 and 017, EDATE-017). Under them, Excel does not truncate it to
  serial 0. `DATEDIF`, `YEARFRAC`, `HOUR`, `MINUTE` and `SECOND` give `#NUM!` for a serial of -1 or below and do
  not cut it down; Excel was observed to at -1 under three of them (DATEDIF-045, YEARFRAC-056, MINUTE-002), and
  below -1, and under `HOUR` and `SECOND` at all, it is the engine's answer, by generalisation. Between -1 and 0
  their readings part: cut down to -1 it is `#NUM!`, and read as a date or a time of day it is an answer
  (`DATEDIF(-0.5,1,"D")` 1, truncated to serial 0; `HOUR(-0.5)` 12, as 12:00), so they refuse it with `#VALUE!` until Excel is asked
  *(2026-10-13; DATEDIF-048, YEARFRAC-058, HOUR-005, MINUTE-003, SECOND-003 ask)*.
- **`EDATE` from serial 0 keeps its day 0, which is the day before the 1st of the month reached** (EDATE-016). Six
  of the 112 readings `spikes/exsheet-date-readings/` writes out give every observed answer of the two functions, and
  they part at three months: the day before 1 January 1900 is serial 0 by `DATE`'s arithmetic and 31 December 1899,
  `#NUM!`, on the real calendar; the day before 1 March 1900 is 60, the day that never existed, or 28 February, 59;
  and January 10000 is past the last year, while the month before ends on 31 December 9999. **`EDATE` from serial 0
  to January 1900, March 1900 or January 10000 stays `#VALUE!`** until Excel is asked (EDATE-018 to 020); EDATE-021
  and 022 ask two answers on which the six agree.
- **A boolean typed into `EOMONTH` or `EDATE` was observed in one argument of each**: `#VALUE!` as `EOMONTH`'s
  months and as `EDATE`'s start (EOMONTH-009, EDATE-009, the seventeenth run). Excel's reading of a boolean typed as
  a start date differs between functions, 1 under `DATEDIF` (DATEDIF-046) and `#VALUE!` under `YEARFRAC`
  (YEARFRAC-054), so neither observation answers the other argument: a boolean typed as `EOMONTH`'s start or as
  `EDATE`'s months stays `#VALUE!` until Excel is asked (EOMONTH-018, EDATE-023). `NETWORKDAYS` and `WORKDAY` still
  refuse a boolean typed as a date with `#VALUE!`; NETWORKDAYS-010 and WORKDAY-011 ask it.

*(2026-10-14, after run 18a: **the date and time refusals above are answered.** Excel gives `#NUM!` at -0.5 under
`DATEDIF`, `YEARFRAC`, `HOUR`, `MINUTE` and `SECOND` (DATEDIF-048, YEARFRAC-058, HOUR-005, MINUTE-003, SECOND-003):
it does not truncate the serial to 0, and the five give `#NUM!` for any serial below 0, between -1 and 0 by
generalisation. `EDATE(0,0)` is 0, `EDATE(0,2)` 60 and `EDATE(0,97200)` `#NUM!` (EDATE-019, 018, 020): of the six
readings, the one by `DATE`'s arithmetic alone gives all three, and `EDATE` from serial 0 takes it. A boolean is
`#VALUE!` as `EOMONTH`'s start, as `EDATE`'s months and as a date of `NETWORKDAYS` and `WORKDAY` (EOMONTH-018,
EDATE-023, NETWORKDAYS-010, WORKDAY-011), and NETWORKDAYS's end, read as its start is, by generalisation. These are
Error Values a Formula catches, Excel's, and no longer refusals.)*

## How deep a Formula may go *(2026-10-21, decided with the user)*

A chain of about 4,100 operators, which fits in Excel's 8,192 characters, overflowed the process's stack:
Excel's operators group to the left, so `=1+1+…+1` is a tree as deep as the chain is long, and every walk over
it recursed. The evaluator, the parser and the walks now loop along an operator chain, so a chain of any length
computes. What still nests is bounded by refusal, as this ADR refuses whatever it cannot answer as Excel does:

- **More than 64 levels of nested functions is refused as the Formula is typed**, with its reason. It is Excel's
  documented limit, `LET` counted as one function.
- **More than 256 levels of nesting is refused as the Formula is typed**, with its reason. This is the engine's
  own limit, not Excel's: parentheses, signs, `%`, functions, an operation on the right of another and a `LET`
  name (its value's depth plus one) each add a level, and an operator's left operand does not. 256 keeps the
  deepest Formula within about 600 KB of stack on a 1 MB thread, measured on unoptimised code. Excel may accept
  some Formulas it refuses (three nested `LET`s that each chain 100 names); a refusal is the honest answer to
  those, and a later measurement may raise the number. Whether Excel limits parentheses apart from functions
  has not been asked.
- **Where the stack still runs short**, on a host whose threads are smaller, the parser refuses the Formula on
  entry and a function call refuses it as a refused `#VALUE!`, instead of stopping the process. A chain of cells
  computed on demand through `INDIRECT` is caught the same way. This depends on the host's stack size, not on
  timing; within the limits above it does not happen on a 1 MB thread.

## The platform's math library is not used *(2026-10-26, decided with the user)*

**`EXP`, `LN`, `LOG`, `LOG10`, `POWER` and `^`, `ATAN`, `ATAN2`, `ASIN`, `ACOS`, `SINH`, `COSH`, `TANH`,
`ASINH`, `ACOSH`, `ATANH`, and the functions built from them (`ACOT`, `COTH`, `CSCH`, `SECH`, the
financial functions' powers), are computed by the engine itself**, as `SIN`, `COS` and `TAN` already were
(the note of 2026-10-12 under "The list grows"). Each answer is the exact value of the function at its
argument's double, rounded once to the nearest double, ties to even. The C# engine on any runtime and the
TypeScript engine of ADR-0370 in any browser therefore give the same double.

**Why.** A platform's math library rounds some arguments the other way, and not the same ones as another's:
glibc's (.NET on Linux), the Windows runtime's and V8's each have their own, and Safari's and Firefox's would
be others again. ADR-0370's differential run, the TypeScript engine on V8 against the C# engine on glibc, found
about 250 differences on its first seed, and nearly all of them were the last bit of one of these functions.
A workbook whose last digit differs between two browsers, and whose `ROUND` or `=` built on that digit
answers differently, is the quietly wrong answer this ADR refuses. The engine already computed e^x and ln x
itself for the financial functions, because Excel's own exponential is correctly rounded where glibc's is
not (XNPV-013, `CorrectlyRounded.cs`). Two other ways were considered: the browser's `Math`, with the
differential run accepting a few units in the last place for these functions, leaves the last digit to the
browser; and V8's algorithms copied into C# match Chrome, but Safari and Firefox still answer otherwise.

**How.**

- **e^x and ln x** keep the double-double path of `CorrectlyRounded.cs` (`correctly-rounded.ts`), about 100
  bits, computed again in 256-bit fixed point when a rounding boundary lies within its error.
- **Every other function** is computed in `BigReal` (`big-real.ts`), a binary number carried to a chosen
  precision, to 128 bits. It is rounded when every number within the computation's error rounds to the same
  double; otherwise it is computed again to twice the bits, up to 4096 (Ziv's strategy). An argument still
  undecided at 4096 bits is an internal error, never a guess. `LOG10` takes the double-double path first and
  `BigReal` only near a boundary, and 10^k is k exactly. `PortableMath.cs` (`portable-math.ts`) holds them.
- **A whole power is computed exactly**: where |b| is at most 64, a^b is counted in integers and rounded
  once (and so is any whole power of a power of two); beyond 64 it is e^(b ln |a|) in `BigReal`, rounded
  once. 1, 2, −1 and 10^k up to 10^22 are each one correctly rounded IEEE operation.
- **A power whose exponent is not whole is still exp(b·ln a)**, as the second and third runs observed
  (ARITH-098: `=8^(1/3)` is 1.9999999999999998), each step correctly rounded: ln a, the product as a double,
  and its exponential. An exponent of 0.5 stays the square root, which IEEE arithmetic rounds correctly.
- The platform's `log2` is read in one place only: to tell that a power lies far past the doubles' range,
  where its last bit cannot move the answer to overflow or to 0.

**What holds it.** Both engines are held to `tests/cases/sheet/portable-math.json`: 779 arguments over the
functions `BigReal` computes and whole powers, each answer mpmath's at 400 bits from the argument's own
double (`PortableMathCaseTests` in xUnit, `portable-math.test.ts` in Vitest).
`tests/cases/sheet/differential-findings.json` pins an argument per function where glibc, V8 or both round
the other way. e^x and ln x are held by `CorrectlyRoundedTests` and by every Excel case that raises a power.
**The gate is ADR-0370's differential run in CI**, at seed 20261026 (`.github/workflows/ci.yml`): any
difference between the two engines, a last bit included, fails it.
