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
  whether Excel's square root is general.)*
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
