# Run 17: every ADR and Definition of Done edit, consolidated

Branch `claude/run17-corrections`, 2026-10-12. The ten areas' proposals (`docs/agents/proposals/run17-*.md`), as
corrected after the independent review, hold text for files a background agent does not edit. This file gathers
every such edit to `docs/adr/` and `docs/definition-of-done.md` into one list. Each is an exact old text and its new
text, checked against the files at this branch's tip: every old text occurs once, and the list applies in order
without one edit spoiling another's match. Nothing here edits `CONTEXT.md`, which no proposal touches.

What a background agent may edit — the READMEs, `docs/specs/`, the Docs Site, the Change Notes and the corpus — is
already on the branch, in the commit that adds this file.

## The questions for the user

1. **`NPV`, and `PMT` at small rates (ADR-0202: correct, or refuse the arguments, before the tag).** The full text,
   options and recommendation are at the top of `docs/agents/proposals/run17-financial.md`. In short:
   - `NPV` disagrees with Excel in the last digits of ordinary cash flows (NPV-001: 4 units in the last place, NPV-002:
     32), and no evaluation the search tried gives all three observed doubles.
   - `PMT` at small rates is unobserved. The engine takes the exact payment rounded once, which gives all four observed
     payments; so do 2504 other evaluations. The 304 of those in an x87's 64 bits part from the engine's at small
     rates: by 3,390 units in the last place, from the 13th significant digit, at `=PMT(1/10000000,12,1000)`.
   - Options: (1) ask Excel first, `oracle.ps1 -Area pmt,npv`, minutes, through COM; (2) refuse both now (`NPV` at a
     rate other than 0, `PMT` below some rate); (3) refuse `NPV`, keep `PMT`; (4) for `NPV` alone, the split by
     argument kind, or undeclare it.
   - **Recommendation: 1, and if no run can be made before the tag, 3.** The engine does not change until the user
     decides. If `NPV` is decided, its outcome is a further bullet of edit 11's financial note.
2. **Tickets closed while their own feature's new questions are open (ADR-0208).** ADR-0208 says a ticket does not
   close on "the next Windows run asks", and makes the seventeenth run the one exception. The run-17 corrections
   added `uncertain` cases after that run, as a question is asked when its feature is built. Review caught it for
   ticket 16, which is now `ready-for-human` with a box for `oracle.ps1 -Area subtotal,aggregate` and a `Roadmap:`
   line. Ticket 07 (trigonometry) is reopened the same way on this branch. Tickets 01, 02 and 04 (dates, text,
   financial, `AND`/`OR`), 18 (`INDIRECT`-030 to 042) and the arrays tickets (INDEX-015 to 018, SORT-012 to 014,
   UNIQUE-010 and 011) are still `done` with new `uncertain` cases of their own. Which?
   - (a) Reopen each the same way, with a box and a `Roadmap:` line.
   - (b) **Recommended:** run `oracle.ps1 -Area` over every file the run-17 corrections touched before the tag, which
     ADR-0208's release check requires anyway, and leave them `done`. Reopen 07 and 16 only until then.
   - (c) Leave them as they are. The release check refuses the tag while any case is `uncertain`, so nothing ships
     unasked, but the tickets say "done" with questions open.

## Where two proposals touch the same sentence

- **ADR-0047, "Error Values, and the one ExSheet adds" (edit 3).** logic-error-type's item 2 read ERRORTYPE-020 and
  ISNUMBER-008 as "the 0 does not flow into the dependent Formula as a number; the dependent shows 0 of its own".
  records-and-widths' ERR-078 to 081 show dependents with Values other than 0 (C1 2, D1 `2x`). Both are true of their
  cases. A Formula entered while a cycle stands showed 0. Cells computed before the cycle closed kept their Values,
  which the 0 also gives them. **One note replaces the two**; logic-error-type's item 2 is not applied on its own.
- **ADR-0047, the end of the file, after the note of 2026-10-11 (edit 11).** let-names (1b), dates-text, financial and
  logic-error-type (item 3) each add a note "after the note of 2026-10-11". They are one insertion here, in that order.
  let-names' note supersedes that note's last sentence ("`LET` refuses as a name any of Excel's function names"), and
  draws-rename's optional pointer (edit 10) edits the same note's middle sentence. logic-error-type's item 3 was
  offered "if the orchestrator writes a section for what the seventeenth run settled". It is written as a dated note
  beside the others.
- **ADR-0047, "Following "admitted with some argument values refused", three are refused until Excel answers" (edit
  6).** subtotal-aggregate points `MOD` to its answer, and dates-text answers `EOMONTH` and `EDATE` before 1 March
  1900. One replacement carries both pointers.
- **ADR-0047, "The catalogue's last Decide rows" (edits 8 and 9).** subtotal-aggregate's note follows the
  `SUBTOTAL`/`AGGREGATE` bullet, and logic-error-type's closes the `ERROR.TYPE` bullet just below it. They are separate
  edits and do not overlap. subtotal-aggregate's note replaces, in meaning, that bullet's third sub-bullet ("Any other
  call of either function inside the range makes the result `#VALUE!`"). The edit leaves that sub-bullet in place as
  the record of what was decided, as the ADR's other dated notes do; strike it too if wanted.
- **Definition of Done, SH-68 (edit 39).** subtotal-aggregate (nesting), logic-error-type (`ERROR.TYPE`'s numbers) and
  lookup-arrays (`INDIRECT`'s forms) each rewrite a clause of the one row, and each adds a dated note. They are one
  replacement of the whole row.
- **Outside the ADRs, already applied on the branch:** the engine README's bullet "Asked of Excel before they are
  answered" took edits from six areas (dates-text, subtotal-aggregate, financial, lookup-arrays, trigonometry,
  logic-error-type) and is now one bullet. `/exsheet/formulas`' "Supported functions" list took items from seven areas.
  `docs/specs/exsheet-functions/spec.md` took rows from seven. None of their edits overlapped textually; each was
  checked against the text it replaced.

## Corrected after review, and so different from the proposals as first written

- **lookup-arrays.** The first version said the volatile Formulas were computed "twice" after a layout; review found
  64 passes, and a blocked volatile Anchor showing its array's first Value. Now a volatile Formula is computed once,
  and once more after each layout that changes something (edit 15). `INDEX`'s `#REF!` covers a Spill Range (edit 16). The `SORT`/`UNIQUE` refusal is cited to
  ADR-0047's paragraph admitting them (edit 16).
- **financial.** The claim that ~106 bits give an x87 64-bit evaluation's double "except within about 2^-60 of a
  rounding boundary" is gone. The note says the engine took the exact model among several that fit, and how far they
  part (edit 11).
- **let-names.** ADR-0181's lead is reworded (edit 31). ADR-0047 gains a note (edit 11). The offer to keep the
  decision's text and carry the change in the note alone is withdrawn.
- **subtotal-aggregate.** The completion list's difference is recorded in ADR-0058 (edit 12).
- **records-and-widths.** The cycle note (edit 3).
- **trigonometry.** No ADR text changed. Its catalogue `LOG` row is applied on the branch.
- **dates-text.** ADR-0120's by-decision note also points to the fractions and conditions refused by decision, as the
  corpus cites ADR-0120 for TEXTFN-019 and 020 (edit 14).

## Found in review, not settled here

- **A Formula that reads its own Spill Range never settles** (lookup-arrays). With A1 `=IF(A2=0,SEQUENCE(2),5)`, each
  layout changes what the next pass computes. The recalculation stops at its cap of 64 passes with 5 in A1 and A2
  blank. ADR-0125's "Dependencies" makes a reader of a spilled cell depend on the Anchor, which would make this a cycle
  (`#CIRC!`), but the dependency graph does not route a spilled cell to its Anchor. It was there before run 17. It
  wants a ticket, not a decision.
- **414 `observed` corpus cases still say "Engine's answer, Excel to be asked"** in their descriptions: `-Update`
  marks a case observed without rewording it. Review flagged AND-010 and LET-101, and both are corrected. The rest are
  a separate sweep.

## How to apply

Apply the edits in order. Each "Replace" block occurs exactly once in its file at this branch's tip. Edits marked
*(optional)* are the proposals' own optional items. Leave them out if not wanted: no other edit depends on them.


## `docs/adr/0046-exsheet-is-a-general-purpose-sheet-drawn-by-exgrid-as-its-consumer.md`

### 1. A note on "A recorded width says whether the user set it", after the note of 2026-09-28

From: records-and-widths §1.

Replace

````text
  reopens with it as the user's; accepted, since no such document exists outside this repository.)*
````

with

````text
  reopens with it as the user's; accepted, since no such document exists outside this repository.)*
  *(2026-10-12, the seventeenth Windows run, b10: **a column inserted to the right of a width widened
  by entry takes that width as set by the user.** Excel wrote the inserted column `customWidth`
  without `bestFit`, while the column to its left kept `bestFit` (CW-023). `bestFit` is the flag
  ECMA-376 gives a column that a typed number widens again, so Excel's inserted column holds the
  width as one the user set, not one an entry widened. Deletion moves a width with its kind (CW-025:
  `bestFit` kept), and undo puts a width back of its kind (CW-026: `bestFit` back). A Sheet Document
  saved before keeps the kinds it recorded. Whether a longer number then leaves the inserted column
  as it is, and still widens the column to its left, goes to the next keys run (CW-030 to 032).)*
````


## `docs/adr/0047-the-formula-engine-is-exsheets-own-and-answers-as-excel-or-not-at-all.md`

### 2. "Settled while building": XMATCH beside XLOOKUP's binary search *(optional)*

From: lookup-arrays (optional).

Replace

````text
  Which of several equal keys it returns is to be observed in Excel first.
````

with

````text
  Which of several equal keys it returns is to be observed in Excel first.
  *(2026-10-12: `XMATCH`'s binary search refuses as `XLOOKUP`'s does. Over `G1:G5` unsorted, Excel's
  `=XMATCH("Banana",G1:G5,0,2)` is 2 (XMATCH-008), recorded as Excel's answer beside the refusal, as XLOOKUP-116 to
  122 are.)*
````

### 3. "Error Values, and the one ExSheet adds": what Excel showed in a cycle (one note in place of two)

From: records-and-widths §2b + logic-error-type ADR-0047 item 2, merged.

Replace

````text
Every cell in the cycle shows it, and so does every Formula that depends on one of them.
````

with

````text
Every cell in the cycle shows it, and so does every Formula that depends on one of them.

*(2026-10-12, the seventeenth Windows run: **Excel showed 0 in one cell of a cycle, not in each.** With A1
`=B1+1`, C1 `=A1*2` and D1 `=C1&"x"` entered first and B1 `=A1` last, B1, whose entry closed the cycle, showed 0,
and A1, C1 and D1 kept 1, 2 and `2x` (ERR-078 to 081): the Values they had before, which B1's 0 also gives them. A
Formula entered while a cycle stands showed 0 whatever it computes from a cell of the cycle: `=A1+1`, `=ISNUMBER(A1)`
and `=ERROR.TYPE(A1)` over A1 `=B1`, B1 `=A1` (ERR-084, ISNUMBER-008, ERRORTYPE-020). Which Values Excel shows
depends on the order the cells were entered, which a Sheet does not keep. The decision stands, for the same
reason.)*
````

### 4. The second run's "The deliberate differences recorded earlier stand": Excel's answers beside them *(optional)*

From: records-and-widths §2 (optional).

Replace

````text
`#CIRC!` (ISERROR-010), and a date-time fill (FILL-033).
````

with

````text
`#CIRC!` (ISERROR-010), and a date-time fill (FILL-033).
*(2026-10-12: the corpus now writes Excel's answer beside each of them, `excelExpect` (TABLE-011
and 013: refused on entry; COPY-025: the text `=SUM(1.5,2)`), as ADR-0208's release check asks of
every case that differs by decision. The seventeenth run gave the same answers.)*
````

### 5. The third run's bullet "A column a typed entry widened is widened again by a longer entry"

From: records-and-widths §2.

Replace

````text
  set stops widening.
````

with

````text
  set stops widening.
  *(2026-10-12, the seventeenth Windows run, b10: typed, `12345678901` widened a standard column to
  11.18 at 150% display scale, written `width="11.81640625"`. The width an entry gives is Excel's
  pixels turned back into characters, and depends on the display's scale and the font. The engine
  harness widens to whole characters (11), and the component to its own pixels. So the corpus never
  names a width an entry gave: a typed case bounds it (`widthAtLeast`, `widthAtMost`), wide enough
  for Excel's at any scale and for the engine's, and narrow enough to tell it from the default width
  and from a width the user set (CW-021, 023, 025, 026). A width set by a command is still compared
  exactly (CW-022: 11).)*
````

### 6. "Following "admitted with some argument values refused", three are refused": pointers to their answers

From: subtotal-aggregate §1 (MOD) + dates-text (EOMONTH, EDATE).

Replace

````text
`MOD` whose quotient is 2^27 or more (`#NUM!`, the answer older versions of Excel were known to
give); `EOMONTH` and `EDATE` from or to a day before 1 March 1900, where the day that never existed
lies; and a boolean typed into `EOMONTH` or `EDATE`.
````

with

````text
`MOD` whose quotient is 2^27 or more (`#NUM!`, the answer older versions of Excel were known to
give) *(answered by the seventeenth Windows run: see the note of 2026-10-12 under "The catalogue's last
Decide rows")*; `EOMONTH` and `EDATE` from or to a day before 1 March 1900, where the day that never
existed lies *(answered by the seventeenth Windows run, but for a start of serial 0 or 60: see the
note of 2026-10-12 at the end)*; and a boolean typed into `EOMONTH` or `EDATE` *(Excel's answer too, the same
note)*.
````

### 7. After the paragraph "*(2026-10-03)* Admitted as the catalogue's ticket 07"

From: trigonometry.

Replace

````text
and its kin, and `ACOTH` gives `#NUM!` where its page names both `#NUM!` and `#VALUE!`, until a
Windows run asks Excel.
````

with

````text
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
````

### 8. "The catalogue's last Decide rows": the `SUBTOTAL` and `AGGREGATE` bullet

From: subtotal-aggregate §1.

Replace

````text
  - `AGGREGATE`'s function numbers for functions the engine does not support are `#VALUE!`.
````

with

````text
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
    and stay `#VALUE!` (AGGREGATE-061).
  - **`LARGE`'s k that is not whole is rounded up**: `LARGE(A1:B5,1.5)` and `AGGREGATE(14,6,A1:A11,1.5)`
    are each the 2nd largest (LARGE-007, AGGREGATE-038), which rules out truncation; ODF 1.3's `LARGE`
    (6.18.40), LibreOffice and Gnumeric round it up, and rounding to nearest cannot be told from it at .5
    (LARGE-008 and AGGREGATE-059 ask 1.2). `SMALL`'s k that is not whole, where LibreOffice and ODF round
    down and Gnumeric up, and a k between 0 and 1, where they disagree too, stay `#VALUE!` until Excel is
    asked (SMALL-004, 005, AGGREGATE-060, LARGE-011). k is read at 15 significant digits first, as both
    implementations do, so `1+2^-52` is 1 (LARGE-012, SMALL-006).

  The `MOD` refusal of "admitted with some argument values refused" above is replaced: Excel 365 answers
  `MOD(2^27,1)` with 0 (MOD-008). Microsoft documents no limit, and names
  `MOD(n, d) = n - d*INT(n/d)`, which the engine computes in doubles and which gives Excel's digits on every
  case read (MOD-009's 0.09999999999999998 among them). A Microsoft 365 user reported on Microsoft's Q&A
  (2021) that `MOD(1125899999999,1)` is 0 and `MOD(1125900000000,1)` is `#NUM!`, a limit Excel 2010 brought
  in; the engine answers a quotient below 1,125,900,000,000 and is `#NUM!` from it, and MOD-010 to MOD-017
  ask Excel on both sides of it.
````

### 9. "The catalogue's last Decide rows": the `ERROR.TYPE` bullet's last line

From: logic-error-type ADR-0047 item 1.

Replace

````text
    is the table's answer for a Value that is no error, and would read as one.
````

with

````text
    is the table's answer for a Value that is no error, and would read as one.
  *(2026-10-12, the seventeenth Windows run: Excel numbers them `#SPILL!` 9, `#CONNECT!` 10,
  `#BLOCKED!` 11, `#UNKNOWN!` 12, `#FIELD!` 13 and `#CALC!` 14. The typed ones were asked with real
  keys; through COM Excel reads each as `#N/A`, which is not its answer. `ERROR.TYPE` now gives those
  numbers, as the admission rule requires. An Error Value the engine gains later is `#VALUE!` until
  Excel is asked. `#GETTING_DATA` and `#CIRC!` are still given back, and a typed `#BUSY!` is still
  text, so its `ERROR.TYPE` is `#N/A`.)*
````

### 10. The note of 2026-10-11: the two questions it left are answered *(optional)*

From: draws-rename (optional).

Replace

````text
(what an undo does to a draw, and whether a rename draws again, ADR-0180)
````

with

````text
(what an undo does to a draw, and whether a rename draws again, ADR-0180; answered by the
seventeenth Windows run: ADR-0180, note of 2026-10-12)
````

### 11. At the end, after the note of 2026-10-11: the seventeenth run's notes of 2026-10-12

From: let-names §1b, dates-text, financial §1, logic-error-type item 3 (one insertion; their order is this list's).

Replace

````text
`LET` refuses as a name any of Excel's function names, not only the declared ones
([ADR-0181](./0181-let-binds-names-inside-one-formula-as-excel-accepts-them-from-the-keyboard.md), note of
this date).
````

with

````text
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
  calendar, and stays `#VALUE!` until Excel is asked. A boolean typed into either is `#VALUE!`, and that is
  Excel's answer too (EOMONTH-009, EDATE-009).
- **`NETWORKDAYS` and `WORKDAY`** count every serial from 0. Each takes the day of the week `WEEKDAY` gives it:
  serial 0 a Saturday, 1 a Sunday, 60 a Wednesday. `NETWORKDAYS(30,100)` is 51 (NETWORKDAYS-005). `WORKDAY`
  reads its dates as `NETWORKDAYS` does, and a result before serial 0 is `#NUM!`. The cases that tell
  `WEEKDAY`'s day from the real calendar's are `uncertain` until a run asks them.
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
  (PMT-008). A fractional one stays `#VALUE!` until Excel is asked.
- Following "admitted with some argument values refused", at a rate other than 0 an `nper` that is
  not a whole number from 0 to 2^53 is `#VALUE!`: squaring raises only to a whole number, and how
  Excel raises 1+rate to any other has not been observed. At a rate of 0 no power is raised, and any
  `nper` is answered.

The cases the search could not settle — which of two orders of the payment's factors, and which of
the evaluations that also give PMT-001 to 008 — are `uncertain` in the corpus, with the engine's
double, for the next Windows run.

*(2026-10-12, the seventeenth Windows run)* **`AND`, `OR` and `XOR` pass over typed text that spells
neither TRUE nor FALSE** (AND-011), as they pass over text inside a range; with nothing that counts, the
result is still `#VALUE!`. The corpus had refused it with `#VALUE!` on the engine's guess. OR-009,
XOR-006 and AND-014 ask Excel the rule's other cases. That typed text spelling TRUE or FALSE still
counts is the engine's answer, which no observation settles (AND-010 is TRUE either way); AND-015 and
OR-010 ask it.
````


## `docs/adr/0058-a-formula-points-across-grids-through-a-pointing-scope.md`

### 12. Beside "`=SUM(A` lists the functions ExSheet computes"

From: subtotal-aggregate §8 (added after review).

Replace

````text
  chosen, as a candidate the grammar refuses would. A deliberate difference.
````

with

````text
  chosen, as a candidate the grammar refuses would. A deliberate difference.
- **An argument's list offers only the values ExSheet computes** *(2026-10-12, the seventeenth Windows
  run, Part B's b4)*. Excel's list for `AGGREGATE`'s `function_num` offers 13 (`MODE.SNGL`) and 16 to
  19 (`PERCENTILE.INC`, `QUARTILE.INC`, `PERCENTILE.EXC`, `QUARTILE.EXC`). ExSheet's leaves them out,
  because it answers them `#VALUE!` (ADR-0047's note of 2026-10-10): offering them would make the
  Formula an error the moment one was chosen, as above. A deliberate difference.
````


## `docs/adr/0120-text-reads-its-format-code-in-the-invariant-spelling-as-a-cell-format-shows-it.md`

### 13. "Refused until a Windows run answers", after its list

From: dates-text.

Replace

````text
- **An empty `format_text`** is `#VALUE!`.
````

with

````text
- **An empty `format_text`** is `#VALUE!`.

*(2026-10-12)* **The seventeenth Windows run answered two of these, and they follow Excel's answer.**

- **`General` is General fitted to 11 characters**, as a cell's General is fitted to its column:
  `TEXT(1/3,"General")` is `0.333333333` (TEXTFN-017). So `TEXT(0.00001,"General")` is `0.00001`, as a cell
  shows it, and not General in full, `1E-05`.
  - The run asked a positive number only. Two readings it did not settle stay `#VALUE!` until Excel is asked.
  - A negative number whose General differs as its minus sign takes one of the 11 characters or not
    (`-1/3`: `-0.33333333` or `-0.333333333`; TEXTFN-025).
  - A number under a lone `@`, which shows numbers in General, whose fitted General is not its General in full
    (`1/3`; TEXTFN-028).
- **An empty `format_text` shows a number as empty text** (TEXTFN-018). Text and booleans are coerced as for any
  code without a text section: text stays as it is, and a boolean stays `TRUE` or `FALSE` (TEXTFN-023 and 024,
  `uncertain`).
- **Fractions and conditions stay refused.** Excel answers `TEXT(4.34,"# ?/?")` with `4 1/3` and
  `TEXT(5,"[<=9]0;0.0")` with `5` (TEXTFN-019, 020). ADR-0202 lets a function refuse the arguments where it
  disagrees, and the reason is this ADR's "One renderer serves both".
  - `TEXT` shows what a cell format shows. Admitting a fraction or a condition is a cell-format feature (Format
    Cells, a document's codes, painting), not a correction to `TEXT`.
  - One fraction does not pin Excel's approximation: which denominators it searches, and how it breaks a tie.
  - Both cases record Excel's answer beside the refusal.
````

### 14. "What differs from Excel, by decision", after its paragraph

From: dates-text (its last sentence added after review).

Replace

````text
  en-US, en-GB and ja-JP, Excel's codes are the invariant ones, and the answers are the same.
````

with

````text
  en-US, en-GB and ja-JP, Excel's codes are the invariant ones, and the answers are the same.

*(2026-10-12)* The seventeenth run read it under de-DE. German Excel answers `TEXT(1234.5,"#,##0.00")` with
`1234,5000`, because it reads `,` as its decimal separator and `.` as its grouping, so the invariant code is
another code to it. ExSheet shows `1.234,50`. The case records Excel's answer beside ExSheet's (TEXTFN-009). Fractions and conditions are refused by decision too, with Excel's
answers recorded beside the refusals (TEXTFN-019, 020; the note under "Refused until a Windows run
answers").
````


## `docs/adr/0124-volatile-functions-are-recalculated-after-every-change.md`

### 15. The end of "`RAND`, `RANDBETWEEN` and `INDIRECT`"

From: lookup-arrays (corrected after review).

Replace

````text
    ([ADR-0046](./0046-exsheet-is-a-general-purpose-sheet-drawn-by-exgrid-as-its-consumer.md)).
````

with

````text
    ([ADR-0046](./0046-exsheet-is-a-general-purpose-sheet-drawn-by-exgrid-as-its-consumer.md)).

*(2026-10-12, after the seventeenth Windows run: **the three refused forms were observed, and `INDIRECT` reads them as
Excel does.** `INDIRECT("R3C2",FALSE)` and `INDIRECT("R3C2",0)` are 45, `INDIRECT("Positions[PV]")` is 250.5 and
`SUM(INDIRECT("A1#"))` over `=SEQUENCE(3)` is 6 (INDIRECT-023 to 026). R1C1 text — `a1` FALSE, 0, a blank or left
empty — naming an absolute cell, or a range of two such cells, is the Reference it names, with or without the Sheet's
own qualifier. A structured reference's text is the Linked Table's column, read as `Table[Column]` typed is: a table
still waiting for its data waits, even under `IFERROR`, and a table never declared, or a column it does not have,
names no place, `#REF!`. `A1#` is the Spill Range of the Formula in A1, read as `A1#` typed is, and `#REF!` where it
does not spill. **R1C1 text in a form no run has observed stays `#VALUE!`**: relative (`R[1]C[-1]`, `RC`), whole rows
or columns (`R3`, `C2`), a number with a leading 0, and structured or `#` text in R1C1 style. INDIRECT-030 to 042 put
the forms generalised from the observed ones to the next run. Excel reads R1C1 text in the letters of its display
language (`Z1S1` in a German Excel); ExSheet reads R and C in every culture, as its function names are English in
every culture, and the run's Excel was English. **A volatile Formula is computed once more after each layout of the
spills that changes something**, since what it computes a Reference to is no Dependent of the cells the layout
changed: a Reference `INDIRECT` or `OFFSET` computes into a Spill Range follows it when it is blocked or freed, as a
typed one does. An Anchor computed again with an array of the shape its layout blocked stays `#SPILL!`, so the
recalculation ends on a layout that changes nothing: a change that blocks or frees one spill computes each volatile
Formula twice, and one that moves no spill once.)*
````


## `docs/adr/0125-a-formula-whose-result-is-an-array-spills-as-excel-365s-does.md`

### 16. The end of "What becomes Excel's answer"

From: lookup-arrays (corrected after review).

Replace

````text
`IF`, `IFERROR`, `ISERROR` and the other functions given a range where they want one Value once
they are lifted.
````

with

````text
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
````


## `docs/adr/0130-the-selection-summary-is-asked-of-the-consumer-like-find.md`

### 17. The table's last column heading *(optional)*

From: selection-summary (optional).

Replace

````text
| Value in a cell | Count | Numerical Count | Sum, Average, Min, Max |
````

with

````text
| Value in a cell | Count | Numerical Count | Sum, Average, Min, Max (the figures that read numbers) |
````

### 18. "What each figure means": the error bullet

From: selection-summary.

Replace

````text
- **An error among the values leaves only Count.** That is what Excel is understood to do; it is
  confirmed in a Windows run before the criterion is signed off.
````

with

````text
- **An error among the values leaves Count and Numerical Count**, and no other figure: Excel's status
  bar, as the seventeenth Windows run read it (below). *(It first read "leaves only Count", what Excel
  was understood to do.)*
````

### 19. "What each figure means": the format bullet

From: selection-summary.

Replace

````text
- **The figures are formatted with the Focus's column's format** until Excel's own rule is read in
  a Windows run; then they follow Excel.
````

with

````text
- **The figures are formatted with the Focus's format**: Excel's rule, read in a Windows run (below),
  is the active cell's format for every figure but the counts. On a grid the Focus's format is its
  column's.
````

### 20. "Refined while implementing": the format bullet's last sentence

From: selection-summary.

Replace

````text
  the Windows run still reads what Excel does across mixed formats.
````

with

````text
  the Windows run still reads what Excel does across mixed formats. *(Read 2026-10-12: the active
  cell's format, whatever the others'.)*
````

### 21. "Refined while implementing": SM-7 and the format rule

From: selection-summary.

Replace

````text
  asked: an error among the values leaves Count, and the Focus's column's format is used. Both
  stand to be corrected by the run.
````

with

````text
  asked: an error among the values leaves Count, and the Focus's column's format is used. Both
  stand to be corrected by the run. *(The run, 2026-10-12, corrected SM-7 — Numerical Count stands
  beside Count — and confirmed the format rule.)*
````

### 22. A new section after "The walk of a new Window"

From: selection-summary.

Replace

````text
  the Window.

## Considered options
````

with

````text
  the Window.

## Read in a Windows run *(2026-10-12)*

The seventeenth Windows run read Excel's status bar under the gestures this decision left to it
(`verification/2026-10-11-windows-17/`, Part B's b5 to b8), all six figures ticked.

- **An error among the values leaves Count and Numerical Count.** Over `1`, `2`, `=1/0`, `x` and a blank, Excel
  shows `Count: 4    Numerical Count: 2` and no other figure (b5); with the error cleared, all six (b6). An error
  is counted and is never a number, and every figure that reads numbers is shown nowhere. The family followed:
  `Aggregation.CountNumbers` answers beside `Count` where an error is among the values, in ExSheet, in ExPivot
  (its own cells and a server's parts alike) and through `GridSummaryCells`. *(It read "leaves only Count", as
  Excel was understood to do; the run showed Numerical Count beside it.)*
- **A Boolean is counted and not numerically, and ExSheet's dates are numbers**, as the table and the refinement
  above say: over `1`, `TRUE`, the date 46306 and `x`, Excel shows `Count: 4`, `Numerical Count: 2`, `Max.: 46306`
  and `Sum: 46307` (b7). One cell shows no figure.
- **Every figure but the two counts wears the active cell's format, whatever the other cells' formats.** Over
  `1,000.00` and `2000%`, Excel shows `Sum: 1,020.00` from the first and `Sum: 102000%` from the second (b8). This
  is the rule ExSheet and ExPivot already followed, to the cell; a grid's formats are its columns', so the Focus's
  column's format is the same rule on a grid, and it stays.

## Considered options
````


## `docs/adr/0180-rand-and-randbetween-draw-from-the-sheet-seed-and-draw-again-at-each-change.md`

### 23. The list of changes

From: draws-rename.

Replace

````text
  - an edit of an Entry, and an insertion or a deletion;
  - the undo or the redo of one of those;
  - a Linked Table's snapshot;
````

with

````text
  - an edit of an Entry, an insertion or a deletion, and a rename of the Sheet (2026-10-12, below);
  - the undo or the redo of one of those;
  - a Linked Table's snapshot, whatever its rows (2026-10-12, below);
````

### 24. "Undo draws again."

From: draws-rename.

Replace

````text
  Entries' with new draws, not the draws seen before. Excel's answer was not observed; the next
  Windows run asks it.
````

with

````text
  Entries' with new draws, not the draws seen before. Excel's undo does the same: on the seventeenth
  Windows run every Ctrl+Z and Ctrl+Y, two rounds of each, drew a number not shown before, never the
  draw before the edit nor the one after it (Part B, b1).
````

### 25. After the first note of 2026-10-11 (renaming the Sheet)

From: draws-rename.

Replace

````text
those draws come out the same. Whether Excel draws again on a rename is asked on the sign-off's Windows
run, and this follows Excel's answer.)*
````

with

````text
those draws come out the same. Whether Excel draws again on a rename is asked on the sign-off's Windows
run, and this follows Excel's answer.)*

*(2026-10-12, from the seventeenth Windows run, Part B b2: Excel drew again on the rename, offered its
undo, and drew again when Ctrl+Z put the name back. As the note above said, this follows Excel's
answer. A rename is a change, and so are its undo and redo, each counted once; it is in the list
above. A rename to the name the Sheet already has changes nothing and draws nothing, nor do its undo
and redo. The decision of 2026-10-11 is replaced; `INDIRECT`'s text still reads the Sheet by the name
it has.)*
````

### 26. After the second note of 2026-10-11 (a Linked Table's declaration)

From: draws-rename.

Replace

````text
asked on the seventeenth Windows run, and this follows Excel's answer.)*
````

with

````text
asked on the seventeenth Windows run, and this follows Excel's answer.)*

*(2026-10-12, from the seventeenth Windows run, Part B b3: each Refresh All drew again, twice with the
file unchanged, once after it changed and once more unchanged. A refresh brings data, and ExSheet's
counterpart is a Linked Table's snapshot, which already counted whatever its rows; nothing changes.
The same declaration again still draws nothing: a refresh does not declare its query again.)*
````

### 27. Considered options, "Undo brings back the draws it undoes"

From: draws-rename.

Replace

````text
  count of changes. Not taken while Excel's answer is unknown.
````

with

````text
  count of changes. Not taken: Excel's undo draws again (the seventeenth Windows run).
````

### 28. Consequences

From: draws-rename.

Replace

````text
- The next Windows run asks Excel what an undo does to a draw.
````

with

````text
- The seventeenth Windows run asked Excel what an undo, a rename and a refresh do to a draw: each
  draws again. A rename now counts as a change (2026-10-12).
````


## `docs/adr/0181-let-binds-names-inside-one-formula-as-excel-accepts-them-from-the-keyboard.md`

### 29. "What Excel was observed to do", Names

From: let-names §1.

Replace

````text
  - Accepted from the keyboard: `_x`, `c`, `r`, `名前` and `é`.
  - Refused: `A1`, `a.b`, `1`, a name bound twice, a name of 250 characters, and a 127th pair.
````

with

````text
  - Accepted from the keyboard: `_x`, `c`, `r`, `名前` and `é`, and, in the seventeenth run, `SUM`,
    `IRR`, `N`, `R1C1` and `RC`.
  - Refused: `A1`, `a.b`, `1`, a name bound twice, a name of 250 characters, and a 127th pair;
    `TRUE`, `R2` and `C3` (the seventeenth run).
````

### 30. "What Excel was observed to do", Values

From: let-names §1.

Replace

````text
  - An unused binding that holds an Error Value does not reach the result: `LET(x,NA(),1)` is 1.
````

with

````text
  - An unused binding that holds an Error Value does not reach the result: `LET(x,NA(),1)` is 1.
  - A value may be left empty: `LET(x,,x)` is 0 (the seventeenth run).
````

### 31. The decision's lead

From: let-names §1 (reworded after review).

Replace

````text
**ExSheet admits `LET` as Excel's keyboard accepts it, and refuses on entry what Excel was not seen to
accept.**
````

with

````text
**ExSheet admits `LET` as Excel's keyboard accepts it, and refuses on entry what that keyboard refuses.**
*(Until the seventeenth run the lead ended "and refuses on entry what Excel was not seen to accept". The
run saw the keyboard take a function's name, an R1C1 shape and a value left empty, and the rule now goes
past the shapes it observed: the name of any function, any R1C1 shape that reads as no A1 Reference, and
any value left empty, read as the empty argument would be read where the name stands. The note of
2026-10-11, run 17, says which were observed.)*
````

### 32. The decision: the bullet "A name that could mean something else is refused", and a new bullet after it

From: let-names §1.

Replace

````text
- **A name that could mean something else is refused**, until a Windows run shows that Excel accepts
  it:
  - a name that reads as a Reference: `A1`, `XFD1048576`, and the R1C1 shapes `R1C1`, `R2`, `C3`;
  - the name of a function;
  - `TRUE` and `FALSE`.

  `c` and `r` alone are names, as observed.
````

with

````text
- **A name that reads as something else is refused**, as Excel's keyboard refuses it: one that reads
  as an A1 Reference (`A1`, `XFD1048576`, `R2`, `C3`), and `TRUE` and `FALSE`.
  - The name of a function, declared or not, is a name, and so is an R1C1 shape that reads as no A1
    Reference (`R1C1`, `RC`); a name followed by `(` is the function's call (the note of 2026-10-11, run
    17). `c` and `r` alone are names, as observed.
- **A value left empty is bound.** The name reads as that empty argument would read where it stands,
  so `LET(x,,x)` shows 0, as `IF(TRUE,)` does. A missing calculation is still refused.
````

### 33. "What differs from Excel, by decision": "Unobserved names are refused"

From: let-names §1.

Replace

````text
- **Unobserved names are refused.** Excel may accept a name it was not seen to accept: a function's
  name, `TRUE`, an R1C1-shaped name. Each is refused until it is observed.
````

with

````text
- *(Until 2026-10-11: unobserved names were refused — a function's name, `TRUE`, an R1C1-shaped name.
  The seventeenth run observed each; see the note.)* No name of letters, digits and `_` is refused for
  being unobserved any more.
````

### 34. A note at the end of "What differs from Excel, by decision"

From: let-names §1.

Replace

````text
Refusing only the declared ones would let a Sheet saved today with `LET(IRR, …)` stop opening on the
day `IRR` is declared.)*
````

with

````text
Refusing only the declared ones would let a Sheet saved today with `LET(IRR, …)` stop opening on the
day `IRR` is declared.)*

*(2026-10-11, the seventeenth Windows run, typed with real keys under en-US: Excel's keyboard entered a
function's name as a `LET` name, whether ExSheet declares it or not — `LET(SUM,2,SUM+1)` is 3,
`LET(IRR,1,IRR)` 1 and `LET(N,1,N)` 1 (LET-101, 111, 112) — the R1C1 shapes `R1C1` and `RC` (LET-103,
106), and a value left empty: `LET(x,,x)` is 0 (LET-107). It refused `TRUE`, `R2` and `C3` (LET-102, 104,
105). Under this ADR's own rule, each shape it entered is now a name: the name of any function, and an
R1C1 shape that does not read as an A1 Reference. A name followed by `(` is still the function's call,
so `LET(SUM,2,SUM(SUM,1))` is 3, which the run did not type. `R2` and `C3` stay refused because they read as A1 References, and
`TRUE` and `FALSE` because they read as booleans. A value left empty binds the name to that empty
argument, and where the name stands it reads as the empty argument would read there; only
`LET(x,,x)` has been observed. **The note above is superseded**: its reason, that a Sheet saved with
`LET(IRR, …)` would stop opening on the day `IRR` is declared, disappears once no function's name is
refused.)*
````


## `docs/definition-of-done.md`

### 35. SH-26

From: records-and-widths §3.

Replace

````text
| **SH-26** | MUST | A recorded column width is one of three kinds: default (not recorded), widened by entry (recorded, marked custom as Excel's file marks it, and widened again by a longer entry), or set by the user (recorded, marked, never widened by an entry); a user's width replaces an entry's, never the reverse (ADR-0046, 2026-09-28; ADR-0047) | Layer 1 against the case corpus (CW-018, 027..029); Layer 2 | as Excel |
````

with

````text
| **SH-26** | MUST | A recorded column width is one of three kinds: default (not recorded), widened by entry (recorded, marked custom as Excel's file marks it, and widened again by a longer entry), or set by the user (recorded, marked, never widened by an entry); a user's width replaces an entry's, never the reverse; a column inserted to the right of a recorded width takes it as set by the user, as Excel's file holds it (CW-023) (ADR-0046, 2026-09-28 and 2026-10-12; ADR-0047) | Layer 1 against the case corpus (CW-018, 021 to 029); Layer 2 | as Excel |
````

### 36. SH-59 *(optional)*

From: lookup-arrays (optional).

Replace

````text
| **SH-59** | MUST | A Formula that calls a volatile function (`OFFSET`, `NOW`; and, since 2026-10-11, `INDIRECT`, `RAND` and `RANDBETWEEN`) is recalculated in every recalculation, with what reads it; a Reference it computes is read as it stands after the recalculation, a cell still to compute being computed first, and a Formula that reaches itself that way is `#CIRC!`; the engine reads the moment from `Sheet.NowSource` once per recalculation (ADR-0124) | Layer 1 | as stated |
````

with

````text
| **SH-59** | MUST | A Formula that calls a volatile function (`OFFSET`, `NOW`; and, since 2026-10-11, `INDIRECT`, `RAND` and `RANDBETWEEN`) is recalculated in every recalculation, with what reads it; a Reference it computes is read as it stands after the recalculation, a spill's layout included, a cell still to compute being computed first, and a Formula that reaches itself that way is `#CIRC!`; the engine reads the moment from `Sheet.NowSource` once per recalculation (ADR-0124) | Layer 1 | as stated |
````

### 37. SH-66

From: draws-rename.

Replace

````text
| **SH-66** | MUST | `RAND` draws in [0,1) and `RANDBETWEEN` a whole number between its bounds, both bounds included. Each draw is a function of the Sheet Seed, the cell, the call's place in its Formula and the count of changes since the Sheet was opened, so the same Entries, seed and count give the same draws whatever order cells are computed in. Two calls draw twice, a `LET` binding draws once, and everything that reads a draw in one recalculation reads the same Value. An edit, an insertion or deletion, the undo or redo of one, and a Linked Table's snapshot draw again; a recalculation by the clock and a change of format draw nothing. A Sheet with no seed answers `#GETTING_DATA`. ExSheet takes the Consumer's `Seed`, or chooses one when it opens a Sheet Document, which records neither seed nor draw. `RANDBETWEEN` gives `#VALUE!` for a fractional bound and for one above 2^53 in magnitude, and otherwise answers as observed (ADR-0180) | Layer 1 + Layer 2 | as stated |
````

with

````text
| **SH-66** | MUST | `RAND` draws in [0,1) and `RANDBETWEEN` a whole number between its bounds, both bounds included. Each draw is a function of the Sheet Seed, the cell, the call's place in its Formula and the count of changes since the Sheet was opened, so the same Entries, seed and count give the same draws whatever order cells are computed in. Two calls draw twice, a `LET` binding draws once, and everything that reads a draw in one recalculation reads the same Value. An edit, an insertion or deletion, a rename of the Sheet, the undo or redo of one, a Linked Table's snapshot whatever its rows, and a Linked Table's declaration draw again; a recalculation by the clock, a change of format and a rename to the Sheet's own name draw nothing. A Sheet with no seed answers `#GETTING_DATA`. ExSheet takes the Consumer's `Seed`, or chooses one when it opens a Sheet Document, which records neither seed nor draw. `RANDBETWEEN` gives `#VALUE!` for a fractional bound and for one above 2^53 in magnitude, and otherwise answers as observed (ADR-0180) | Layer 1 + Layer 2 | as stated |
````

### 38. SH-67

From: let-names §5.

Replace

````text
| **SH-67** | MUST | A `LET` name is seen by the values bound after it and by its own calculation only; an inner `LET` may bind it again, and a name read before its binding or inside its own value is `#NAME?`. Names are compared without regard to case, start with a letter or `_`, go on with letters, digits and `_`, and are at most 249 characters; a `LET` holds at most 126 pairs. Refused on entry: a name that reads as a Reference in A1 or R1C1 shape, a function's name, `TRUE`, `FALSE`, a name bound twice, an even number of arguments and a missing calculation. `Table[Column]` inside a `LET` reads the Linked Table whatever is bound. Each binding is computed at most once, when first read, and an unused binding's Error Value does not reach the result. A `LET` depends on everything its text names, so an unused binding over a cycle is `#CIRC!` and one over a waiting Linked Table is `#GETTING_DATA` (ADR-0181) | Layer 1 (the case corpus, the observed `LET` cases included) + Layer 2 (refusal on entry) | as stated |
````

with

````text
| **SH-67** | MUST | A `LET` name is seen by the values bound after it and by its own calculation only; an inner `LET` may bind it again, and a name read before its binding or inside its own value is `#NAME?`. Names are compared without regard to case, start with a letter or `_`, go on with letters, digits and `_`, and are at most 249 characters; a `LET` holds at most 126 pairs. Refused on entry: a name that reads as an A1 Reference, `TRUE`, `FALSE`, a name bound twice, an even number of arguments and a missing calculation. A function's name, declared or not, and an R1C1 shape that reads as no A1 Reference are names, and a value left empty is bound (`LET(x,,x)` is 0), as Excel's keyboard was observed to take them (the seventeenth run). `Table[Column]` inside a `LET` reads the Linked Table whatever is bound. Each binding is computed at most once, when first read, and an unused binding's Error Value does not reach the result. A `LET` depends on everything its text names, so an unused binding over a cycle is `#CIRC!` and one over a waiting Linked Table is `#GETTING_DATA` (ADR-0181) | Layer 1 (the case corpus, the observed `LET` cases included) + Layer 2 (refusal on entry) | as stated |
````

### 39. SH-68 (three areas in one row)

From: subtotal-aggregate §2 + logic-error-type + lookup-arrays.

Replace

````text
| **SH-68** | MUST | `SUBTOTAL` and `AGGREGATE` answer their corpus cases while no row can be hidden, and a test named for ADR-0047 fails on the day one can. Inside their range, only a cell whose whole Formula is a `SUBTOTAL` (and, for `AGGREGATE`'s options 0 to 3, one whose whole Formula is an `AGGREGATE`) is skipped as nested; any other call of either makes the result `#VALUE!`, as does an `AGGREGATE` function number for a function the engine does not support. `ERROR.TYPE` gives `#GETTING_DATA` and `#CIRC!` back, and `#VALUE!` for an Error Value outside Microsoft's table. `INDIRECT` reads A1-style text naming a place on the same Sheet, keeps its text through an insertion or deletion, gives `#VALUE!` for R1C1, structured and `A1#` text, and `#REF!` for text that names no place (ADR-0047's and ADR-0124's notes of 2026-10-10) *(2026-10-11, decided with the user: for `AGGREGATE`'s options 4 to 7, a cell in range whose whole Formula is a `SUBTOTAL` makes the result `#VALUE!` until the sign-off's Windows run reads Excel's answer, which Microsoft documents as counted; then it follows Excel.)* | Layer 1 | as stated |
````

with

````text
| **SH-68** | MUST | `SUBTOTAL` and `AGGREGATE` answer their corpus cases while no row can be hidden, and a test named for ADR-0047 fails on the day one can. Inside their range, a cell whose Formula, or whose Spill Range's Anchor's, calls `SUBTOTAL` or `AGGREGATE` anywhere is nested: `SUBTOTAL` and `AGGREGATE`'s options 0 to 3 pass over it and options 4 to 7 count its Value, as the seventeenth Windows run read Excel's answers; an `AGGREGATE` function number for a function the engine does not support is `#VALUE!`, and a Formula that gives one of their reference-only arguments something that can never be a Reference is refused on entry. `ERROR.TYPE` gives `#GETTING_DATA` and `#CIRC!` back, and Excel's numbers for its newer Error Values: `#SPILL!` 9, `#CONNECT!` 10, `#BLOCKED!` 11, `#UNKNOWN!` 12, `#FIELD!` 13, `#CALC!` 14. `INDIRECT` reads A1-style text naming a place on the same Sheet, R1C1 text naming an absolute cell or a range of two, a Linked Table's column as structured text (waiting while the table does) and `A1#`, keeps its text through an insertion or deletion, gives `#VALUE!` for R1C1 text that is relative or names whole rows or columns, and `#REF!` for text that names no place, a table or a column that does not exist included (ADR-0047's and ADR-0124's notes of 2026-10-10, ADR-0124's of 2026-10-12) *(2026-10-11, decided with the user: for `AGGREGATE`'s options 4 to 7, a cell in range whose whole Formula is a `SUBTOTAL` makes the result `#VALUE!` until the sign-off's Windows run reads Excel's answer, which Microsoft documents as counted; then it follows Excel.)* *(2026-10-12: the seventeenth run read it — 6, counted — and the engine follows Excel, ADR-0047's note of that date.)* *(2026-10-12, the seventeenth Windows run read `ERROR.TYPE`'s numbers for Excel's newer Error Values, which the row had refused with `#VALUE!` until Excel was asked; it now follows Excel, ADR-0047's note of this date.)* | Layer 1 | as stated |
````

### 40. SM-7

From: selection-summary.

Replace

````text
| **SM-7** | MUST | An error among the values leaves Count alone, as Excel's status bar does — once a Windows run has read Excel's answer and recorded it in `verification/` (ADR-0130) | Layer 1; the Windows record | as recorded |
````

with

````text
| **SM-7** | MUST | An error among the values leaves Count and Numerical Count, and no other figure, as Excel's status bar does (ADR-0130). *(2026-10-12: Excel's rule recorded in `verification/2026-10-11-windows-17/`, Part B's b5 and b6; the criterion read "leaves Count alone … once a Windows run has read Excel's answer" until then.)* | Layer 1 (`AggregateAccumulatorTests`, `GridSummaryTests`), Layer 2 (`SheetSummaryTests`), Layer 3 (`selection-summary.spec.mjs`, the Sheet case); the Windows record | as recorded |
````

### 41. SM-9

From: selection-summary.

Replace

````text
| **SM-9** | MUST | The built-in Chrome shows Average, Count and Sum by default in the status line whenever two or more cells are selected — the line standing whenever the grid can summarise, so selecting never moves the Viewport — formatted by the Focus's column's format until Excel's rule is recorded; the right-click menu reports a change of figures and the grid holds none (ADR-0130/0010) | Layer 2 + Layer 3 under both Chromes | as stated |
````

with

````text
| **SM-9** | MUST | The built-in Chrome shows Average, Count and Sum by default in the status line whenever two or more cells are selected — the line standing whenever the grid can summarise, so selecting never moves the Viewport — with every figure but the counts in the Focus's format, as Excel's are in the active cell's whatever the other cells' formats (the Focus cell's on a Sheet and a report, the Focus's column's on a grid); the right-click menu reports a change of figures and the grid holds none (ADR-0130/0010) *(2026-10-12: Excel's rule recorded in the seventeenth Windows run, Part B's b7 and b8, and pinned in `SheetSummaryTests`.)* | Layer 2 + Layer 3 under both Chromes | as stated |
````
