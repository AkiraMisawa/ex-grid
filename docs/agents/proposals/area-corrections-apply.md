# The area passes' corrections: every edit a background agent does not make, in one list

Branch `claude/area-corrections`, 2026-10-13. It holds the five area branches of the second pass
(`claude/area2-financial`, `-sort-and-unique`, `-dates-and-text`, `-indirect`, `-kth-and-mod`), merged, and
`origin/main` after them. Each branch left a proposal file; this list replaces the five
(`area-financial.md`, `area-sort-and-unique.md`, `area-dates-and-text.md`, `area-indirect.md`,
`area-kth-and-mod.md`, deleted in the commit that adds it), with this branch's own edits for the rule decided
on 2026-10-12 that a refusal is never caught, anywhere. Their findings, searches and case tables are in their
branches' history (`git show <branch>:docs/agents/proposals/<file>`); what they ask of the files below is here.

The targets: ADR-0047, ADR-0049, ADR-0120, ADR-0124, ADR-0125 and ADR-0202, the Definition of Done,
`CONTEXT.md`, the engine's README, the function catalogue (`docs/specs/exsheet-functions/spec.md`), tickets 01,
04, 05, 11, 16 and 18, and the Docs Site's Formulas page and Roadmap; after edit 65, ADR-0130, ADR-0050 and the
Docs Site's Design, Functions, Overview, Linked Tables and Clipboard pages. Checked by script at the branch's tip:
every old text is whole lines and occurs once in its target as the edits before it leave it, and the whole list
applies in order. Applied to a scratch copy of the tip, `ExGrid.Docs.Generator.Tests` and the Docs Site's build
pass there. The edits after 65 carry the decisions of 2026-10-13 to texts no earlier edit reaches: `CONTEXT.md`'s
**Refusal**, ADR-0130, ADR-0050, the Definition of Done's SM-12, and the Docs Site's other pages.

## Decided with the user, and where each is written

The brief names these as decided with the user on 2026-10-12; each is written as a dated note where it lands.

- **`PMT` is kept under an exception to ADR-0047 for `PMT` alone, its last digits a Known limitation.** ADR-0047
  (the admission rule's pointer and the note "An exception for `PMT` alone"), ADR-0202's Known limitations, the
  Definition of Done (§27's SH-7 bullet, its third bullet, SH-7's row), the Formulas page and the Roadmap.
- **`NPV` stays undeclared** (`#NAME?`), as ADR-0202's note of that day decided once the pass had answered:
  ADR-0202's note and Known limitations, the Definition of Done's third bullet, the README, the Formulas page.
- **`INDIRECT` refuses a declared Linked Table's bare name**, in either style: ADR-0049, ADR-0124's note,
  ADR-0202's Known limitations, SH-68, the Formulas page.
- **In R1C1 style a row or column number past the Sheet's edge is refused until Excel is asked**; A1 text past
  the edge stays `#REF!`: ADR-0124's note, ADR-0202's Known limitations, SH-68, the Formulas page.
- **A refusal is never caught, anywhere**: ADR-0047, next to its rule admitting refused argument values;
  ADR-0125, where it speaks of the unlifted refusal; the Definition of Done's SH-7 bullet; `CONTEXT.md`; the
  README; the catalogue's `IFERROR`, `ISERROR`, `IFNA` and `ERROR.TYPE` rows; ticket 18; the Formulas page.

Decided with the user on 2026-10-13, the answers to the questions this list put to the user (it puts none now);
each is written as a dated note credited to the user where it lands:

1. **`PMT`'s exception, restated as observed** (the question of the 15th digit, option (a)): Excel's double and
   the exact payment rounded once differ by 1 to 11 units in the last place, and at PMT-013 and PMT-026 that
   shows in General's 15th digit; `PMT` is kept there too. ADR-0047 (edit 12), ADR-0202 (edits 23 and 24), the
   README (edit 38), the Formulas page (edit 63), the Release Notes lines below; ticket 13, the catalogue's note
   16 and `pmt.json`, edited in the branch.
2. **The bound of 2^6 is kept** where a reading magnifies its own roundings (option (a)): ADR-0047 (edit 12);
   ticket 13 and PMT-047 to 053's descriptions, edited in the branch.
3. **`PMT`'s payment at the start of each period is the exact payment at the start rounded once** (option (a)):
   ADR-0047 (edit 12), the README (edit 38), the Formulas page (edit 63), ticket 04 (edit 51); ticket 13, the
   catalogue, `pmt.json` and `spikes/exsheet-financial-evaluation/README.md`, edited in the branch. Excel's
   double now differs from the engine's at 23 of the 30 observed payments, at two of them in the 15th digit.
4. **The term Refused Formula**, for a Formula's refused `#VALUE!` (the term question): `CONTEXT.md` (edits 29
   and 66), ADR-0047 (edit 2). `Value.IsRefusal` is renamed `Value.IsRefused`.
5. **A call of a function Excel has that ExSheet does not declare, and a declared Linked Table's bare name typed,
   is a Refused Formula that shows `#NAME?`** (decided after the term): no Formula catches it, and a cell that
   reads it is refused and shows `#VALUE!`; a name Excel does not have stays Excel's own `#NAME?`. The names
   are the catalogue's 377 (`ExcelFunctionNames`, held to the catalogue by `FunctionCatalogueTests`).
   ADR-0047 (edit 2), ADR-0049 (edit 14), ADR-0124 (edit 19), SH-7's row (edit 27), `CONTEXT.md` (edit 29), the
   README (edit 32), the Docs Site (edits 62, 63, 70 to 74 and 76); TABLE-024's description, edited in the
   branch.
6. **A refused Anchor whose array's shape its text settles spills refusals over that shape**; where the shape
   could depend on a Value nothing spills and those cells read as blank, a Known limitation of `0.1.0`; in the
   way, the Anchor is `#SPILL!`, as Excel's array of that shape would be: ADR-0125 (edit 20), ADR-0202's Known
   limitations (edit 24), the README (edit 32), the Formulas page (edit 78), the Release Notes lines below.
7. **The Selection Summary shows no numeric figure over a refused cell, and says why**: ADR-0130 (edit 67),
   SM-12 (edit 69), the Sheet's Overview (edit 75), the Formulas page (edit 57). The implementer's, written
   uncredited: Count is withheld too (ADR-0130's "a partial figure is never shown"), the reason names the cell
   and what it shows, and its words are `SheetCommandIds.SummaryRefused`, asked of a substituted Chrome and then
   of `CommandLabel` for another language (ADR-0203).
8. **A serial between -1 and 0** under `DATEDIF`, `YEARFRAC`, `HOUR`, `MINUTE` and `SECOND` is refused until
   Excel is asked, -1 and below staying `#NUM!`, and **`SORT`'s fractional `sort_index`** is refused with SORT-042
   `uncertain`: the orchestrator's readings, written uncredited. ADR-0047 (edits 4 and 13), the README (edits 35
   and 40), tickets 05 and 11 (edits 52 and 53), the Formulas page (edit 78); the catalogue, edited in the
   branch.
9. **`MOD`'s judgment** that only the evaluations at 53 and 64 bits are modelled is accepted, the orchestrator's
   reading of the user's rule, written uncredited: ADR-0047 (edit 7). This branch did not find that rule in the
   ADRs or this list, so the note names none.

**For the user to confirm: a new ExGrid public seam implemented ahead of an ADR.** ExGrid's
`ClipboardData.MarkAttribute` (`data-ex-mark`) and `GridPasteIntent.MarkFor` let a Consumer mark a cell of the
family's own clipboard HTML and read the mark back on paste; ExSheet marks a refused cell with it. ADR-0050 item
9 (edit 68) and the Clipboard page (edit 77) describe it in an uncredited note; ExGrid's ADRs record no decision
for it.

## This branch's choices (not the user's)

- **How a refusal is carried.** A refused cell's Value is `#VALUE!` with a mark the engine reads
  (`Value.IsRefused`), equal to `#VALUE!` as a Value and shown, copied and compared as one; a Refused Formula
  that calls a function Excel has and ExSheet does not declare, or names a declared Linked Table bare, is `#NAME?`
  with the same mark. A Formula that reads a refused cell is refused as one that reads a cycle is `#CIRC!`: by
  every cell its text names, whether or not the branch naming it is taken (`=IF(TRUE,1,B1)`), and by every cell or
  Linked Table column a computed Reference reads (`INDIRECT`, `OFFSET`, `INDEX`, `A1#`). A call of an undeclared
  function in a branch not taken is not evaluated, and refuses nothing (`=IF(TRUE,1,NPV(0.1,1,2))` is 1). A cycle
  comes before a refusal, and a wait before one. A Linked Table's column holding a refusal the Consumer pushed
  refuses its readers. `Entry.FromValue` throws for a refusal, as for `#CIRC!`. A cell whose `#VALUE!` turns from
  Excel's own into a refusal, or back, is in `SheetChange.ValueChanges`, and a Spill Range takes the change;
  `Value.Equals` is unchanged.
- **A refused Anchor's spill.** The shape its text settles (`KnownShape`): a constant, a Reference the text names
  to a rectangle of this Sheet, an operator over those, `TRANSPOSE` of a known shape, and a call whose every
  argument is one Value of a function that cannot give a Reference or an array from one Value. Everything else,
  `INDIRECT`, `OFFSET` and `IF` among the calls that can give a Reference, is of a shape the text does not settle.
- **Copy and paste.** The clipboard's text writes a refused cell as `#VALUE!`, and ExSheet's HTML marks it
  (`data-ex-mark="exsheet-refused:C1"`, ExGrid's `ClipboardData.MarkAttribute`, read back through
  `GridPasteIntent.MarkFor`). This Sheet's own copy of one range carries Entries, so its paste brings the Formula,
  refused again where it still refuses. Any other paste that carries the mark — a copy of several ranges, or with
  the headers, or a paste into another Sheet on the page — is refused whole by name ("Nothing was pasted: the copy
  holds C1, whose Formula ExSheet refused…"). Another program reads the HTML's text, or the plain text, as
  the cell shows it, `#VALUE!` (or `#NAME?`), Excel's Error Value.
- **The display.** The cell, the Formula Bar and the clipboard show `#VALUE!` (or `#NAME?`); ExSheet has no error
  popover for a Value. The Selection Summary over a refused cell shows no figure, Count included, and its reason
  names the cell. The Formulas page says what a refusal is.
- **Where the audit drew the line.** A `#VALUE!` is a refusal where its comment, its corpus case or the ADRs call it
  refused: `MOD`, `MROUND`, `CEILING.MATH` and `FLOOR.MATH`'s boundary, `RANDBETWEEN`, the criteria functions'
  unsettled domains and a computed footprint, `UPPER`, `LOWER`, `PROPER` and `SEARCH` outside the observed
  characters, `TEXT` with a code outside the cell formats' subset, `DATEDIF`'s MD and YD, `DATEDIF`, `YEARFRAC`,
  `HOUR`, `MINUTE` and `SECOND` between -1 and 0, `EDATE`, `EOMONTH`, `NETWORKDAYS` and `WORKDAY`'s booleans and
  serial-0 months, `PMT`, `PV` and `XNPV`, `SORT`'s fractional `sort_index`, the legacy lookups' refused searches,
  `XLOOKUP`'s and `XMATCH`'s binary search and regular expressions, `INDIRECT`'s refused forms and bare name,
  `AGGREGATE`'s unsupported numbers, and a range where one Value is wanted. A layer-1 test checks every corpus
  `#VALUE!`: a refusal exactly where the case is `uncertain` or differs by decision. Two observed cases are refused
  although Excel gave `#VALUE!` too, XLOOKUP-103 and 157: the engine cannot tell a pattern PCRE2 rejects from one
  it reads otherwise. `XNPV` refuses any cell of its values or dates that is not a number (text, a boolean or a
  blank), whatever else is among them, which its doc already called a refusal. Kept as Excel's own `#VALUE!`: an
  `XLOOKUP` or `XMATCH` mode outside Excel's list, observed at XLOOKUP-058 and by generalisation elsewhere, the
  regular expression mode under a binary search (XLOOKUP-104), a `SUMIFS` shape mismatch, `MROUND`'s boolean,
  `YEARFRAC`'s boolean, `TEXT(-1,"yyyy")`, `EOMONTH`'s months and `EDATE`'s start as a boolean, and the documented
  `#VALUE!` of the text functions.
- **The financial area's wording of the `PMT` decision.** Its edits wrote the exception as "digits no format
  displays", which PMT-013 and PMT-026 show untrue. Here the dated notes say "its last digits", and the ADR's
  note keeps the decision's own words as what it was taken on, beside the user's restatement of 2026-10-13.
- **New cases placed here**: the dates area's five (DATEDIF-048, YEARFRAC-058, HOUR-005, MINUTE-003,
  SECOND-003, each `uncertain`, the engine refusing), SORT-042 (`uncertain`, refused) and the `INDIRECT` area's
  TABLE-024 (the typed bare name, a Refused Formula showing `#NAME?`), added to the corpus in this branch.
- **Tickets, by ADR-0208's note of 2026-10-12.** A ticket that is done stays done when the corrections add
  `uncertain` cases to it: the one `oracle.ps1 -Area` pass before `0.1.0-rc.1` asks them, and the release check
  refuses the tag while any is `uncertain`. Ticket 16, reopened before that note, stays `ready-for-human`, and its
  open box takes SMALL-011 (the kth area offered a box in ticket 04 instead) and AGGREGATE-069. Tickets 01, 04, 05,
  11 and 18 get comments, no box. No ticket closes: none's last question is answered.
- **Applied in the branch, not listed here** (files a background agent may edit): the run-17 Change Note the
  `INDIRECT` area corrected (`run17-lookup-arrays-indirect-observed-forms.md`), ticket 16's two pointers to
  `area-kth-and-mod.md`, which now point to this file, ticket 13's comments of 2026-10-13, the catalogue's rows
  and note 16 for the decisions of 2026-10-13, the corpus's descriptions (`pmt.json`, TABLE-024) and
  `spikes/exsheet-financial-evaluation/README.md`.

## Where two proposals edited the same text

- **ADR-0202's Known limitations.** The financial area's line (`NPV`, `PMT`'s last digits) and the `INDIRECT`
  area's (the bare name) both followed the note of SH-19. One edit holds both, and adds the R1C1 number past the
  edge, which the brief lists as a Known limitation decided with the user.
- **The README's "Asked of Excel before they are answered".** Four areas rewrote clauses of one bullet; one edit
  rewrites it whole, each area's clause as it proposed it, and the closing sentence says the refusal is never
  caught.
- **ADR-0125's note on the unlifted refusal.** The sort area's note ended "A cell that reads such a Formula's cell
  still reads `#VALUE!` as an Error Value", which the rule of 2026-10-12 makes false; a dated note replaces that
  sentence.
- **The Definition of Done's SH-7 bullet.** The financial area's note and this branch's are one edit.
- **Tickets 04 and 05.** The financial, kth and dates areas' comments are one edit for each.

## The Known limitations lines for `0.1.0`'s Release Notes (not an edit; for the cut)

```
- `NPV` is not supported and is `#NAME?`, as `IRR`, `XIRR` and `RATE` are: its last digits differ from
  Excel's, and no evaluation found gives every result Excel was seen to give
  ([Formulas](exsheet/formulas#supported-functions)).
- `PMT`'s payment can differ from Excel's in its last digits: 23 of the 30 payments Excel was asked differ
  from ExSheet's by 1 to 11 units in the last place, a relative difference of at most about 1.3e-15, which
  at two of them shows in the 15th significant digit; and where the evaluations Excel's payments leave
  part, `PMT` is `#VALUE!` ([Formulas](exsheet/formulas#supported-functions)).
- `INDIRECT` over a Linked Table's name on its own, `INDIRECT("Positions")`, is `#VALUE!`, where Excel reads
  the table's data: a Linked Table is read by key, never by position
  ([Formulas](exsheet/formulas#supported-functions)).
- `INDIRECT`'s R1C1 text with a row or column number past the Sheet's edge, `INDIRECT("R1048577C2",FALSE)`,
  is `#VALUE!` until Excel has been asked whether it names no place or a place
  ([Formulas](exsheet/formulas#supported-functions)).
- A Formula ExSheet refused whose array's shape could depend on a Value, such as a refused `FILTER` or
  `=LEN(A1:A3)`, spills nothing, and the cells its array would cover read as blank
  ([Formulas](exsheet/formulas#supported-functions)).
```

## For the next `oracle.ps1 -Area` pass (not an edit)

At this tip 137 cases are `uncertain` and askable through COM, in `aggregate`, `count`, `counta`, `datedif`,
`edate`, `eomonth`, `error-type`, `filter`, `fv`, `hour`, `iferror`, `ifna`, `indirect`, `iserror`,
`linked-tables`, `minute`, `mod`, `networkdays`, `pmt`, `pv`, `references`, `second`, `sequence`, `small`, `sort`,
`sortby`, `transpose`, `unique`, `workday`, `xnpv` and `yearfrac`; LET-113 is asked by keys.

## The edits

Each edit is `### N. <path>`, a line naming what it changes and where it comes from, and an `Old:` and a `New:`
fence of whole lines with the target's own indentation. Each old text occurs once in its target as the edits
before it leave it, and the list applies in order.

### 1. docs/adr/0047-the-formula-engine-is-exsheets-own-and-answers-as-excel-or-not-at-all.md

The admission rule: a pointer to the exception for `PMT` (from: financial 1, its wording of the decision changed (this branch's choices, above)).

Old:

````
**A function is added only when it can match Excel, including Excel's edge cases.** That means its
argument coercions, how it treats blanks and text inside ranges, how it propagates Error Values,
and its rounding. The tests for a function are written from Excel's observed behaviour. A known
deviation keeps the function out.
````

New:

````
**A function is added only when it can match Excel, including Excel's edge cases.** That means its
argument coercions, how it treats blanks and text inside ranges, how it propagates Error Values,
and its rounding. The tests for a function are written from Excel's observed behaviour. A known
deviation keeps the function out.
*(2026-10-12, decided with the user: one exception, `PMT`, kept though its result may differ from
Excel's in its last digits, a Known limitation of `0.1.0`. The note of that date, "An exception for
`PMT` alone", says why and how far it goes.)*
````

### 2. docs/adr/0047-the-formula-engine-is-exsheets-own-and-answers-as-excel-or-not-at-all.md

"Settled while building": the refusal is never caught, next to the rule that admits refused values (from: this branch (point 2)).

Old:

````
  *(2026-10-12: `XMATCH`'s binary search refuses as `XLOOKUP`'s does. Over `G1:G5` unsorted, Excel's
  `=XMATCH("Banana",G1:G5,0,2)` is 2 (XMATCH-008), recorded as Excel's answer beside the refusal, as XLOOKUP-116 to
  122 are.)*
````

New:

````
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
````

### 3. docs/adr/0047-the-formula-engine-is-exsheets-own-and-answers-as-excel-or-not-at-all.md

"The list grows: the first additions": `EOMONTH` and `EDATE` (from: dates-and-text ADR-0047 1).

Old:

````
existed lies *(answered by the seventeenth Windows run, but for a start of serial 0 or 60: see the
note of 2026-10-12 at the end)*; and a boolean typed into `EOMONTH` or `EDATE` *(Excel's answer too, the same
note)*. The functions that answer about any Value —
````

New:

````
existed lies *(answered by the seventeenth Windows run and the area passes, but for `EDATE` from serial 0 to
January 1900, March 1900 or January 10000: see the notes of 2026-10-12 at the end)*; and a boolean typed into
`EOMONTH` or `EDATE` *(Excel's answer too as `EOMONTH`'s months and as `EDATE`'s start, the arguments the
seventeenth run typed one into; as `EOMONTH`'s start and as `EDATE`'s months it is refused until Excel is asked:
see the notes of 2026-10-12 at the end)*. The functions that answer about any Value —
````

### 4. docs/adr/0047-the-formula-engine-is-exsheets-own-and-answers-as-excel-or-not-at-all.md

The paragraph admitting `FILTER` to `SUMPRODUCT` under ADR-0125: the blank and the Error Value key (from: sort-and-unique 3).

Old:

````
answer, and their corpus cases changed from a difference by decision to it. `UNIQUE` refuses a blank
in its array, and `SORT` and `SORTBY` a blank or an Error Value among their keys, with `#VALUE!`
until Excel is asked how it orders them.
````

New:

````
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
````

### 5. docs/adr/0047-the-formula-engine-is-exsheets-own-and-answers-as-excel-or-not-at-all.md

"The catalogue's last Decide rows": `AGGREGATE`'s array form (from: kth-and-mod 1a).

Old:

````
  - **`AGGREGATE`'s array form takes a number given directly** as an array of that one Value
    (AGGREGATE-039: `AGGREGATE(14,6,5,1)` is 5). Text and a boolean given directly have not been asked,
    and stay `#VALUE!` (AGGREGATE-061).
````

New:

````
  - **`AGGREGATE`'s array form takes a number given directly** as an array of that one Value
    (AGGREGATE-039: `AGGREGATE(14,6,5,1)` is 5). Text and a boolean given directly have not been asked,
    and stay `#VALUE!` (AGGREGATE-061). *(2026-10-12, the area passes: text given directly is such an
    array too, and holds no number, since it is not coerced: `AGGREGATE(14,6,"5",1)` is `#NUM!`, neither
    5 nor `#VALUE!` (AGGREGATE-061), and so are a boolean and text that spells no number (AGGREGATE-064,
    065). An Error Value given directly is the result, as an Error Value argument is, and option 6 does
    not pass over it: `AGGREGATE(14,6,1/0,1)` is `#DIV/0!` (AGGREGATE-067). `LARGE` and `SMALL`
    themselves coerce a Value given directly: `LARGE("5",1)` is 5 and `SMALL(TRUE,1)` 1 (LARGE-014,
    SMALL-010). The engine follows each.)*
````

### 6. docs/adr/0047-the-formula-engine-is-exsheets-own-and-answers-as-excel-or-not-at-all.md

"The catalogue's last Decide rows": `LARGE` and `SMALL` (from: kth-and-mod 1b).

Old:

````
  - **`LARGE`'s k that is not whole is rounded up**: `LARGE(A1:B5,1.5)` and `AGGREGATE(14,6,A1:A11,1.5)`
    are each the 2nd largest (LARGE-007, AGGREGATE-038), which rules out truncation; ODF 1.3's `LARGE`
    (6.18.40), LibreOffice and Gnumeric round it up, and rounding to nearest cannot be told from it at .5
    (LARGE-008 and AGGREGATE-059 ask 1.2). `SMALL`'s k that is not whole, where LibreOffice and ODF round
    down and Gnumeric up, and a k between 0 and 1, where they disagree too, stay `#VALUE!` until Excel is
    asked (SMALL-004, 005, AGGREGATE-060, LARGE-011). k is read at 15 significant digits first, as both
    implementations do, so `1+2^-52` is 1 (LARGE-012, SMALL-006).
````

New:

````
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
````

### 7. docs/adr/0047-the-formula-engine-is-exsheets-own-and-answers-as-excel-or-not-at-all.md

"The catalogue's last Decide rows": `MOD` (from: kth-and-mod 1c).

Old:

````
  The `MOD` refusal of "admitted with some argument values refused" above is replaced: Excel 365 answers
  `MOD(2^27,1)` with 0 (MOD-008). Microsoft documents no limit, and names
  `MOD(n, d) = n - d*INT(n/d)`, which the engine computes in doubles and which gives Excel's digits on every
  case read (MOD-009's 0.09999999999999998 among them). A Microsoft 365 user reported on Microsoft's Q&A
  (2021) that `MOD(1125899999999,1)` is 0 and `MOD(1125900000000,1)` is `#NUM!`, a limit Excel 2010 brought
  in; the engine answers a quotient below 1,125,900,000,000 and is `#NUM!` from it, and MOD-010 to MOD-017
  ask Excel on both sides of it.
````

New:

````
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
  judgment is accepted, and the engine models the widths of 53 and 64 bits alone.)*
````

### 8. docs/adr/0047-the-formula-engine-is-exsheets-own-and-answers-as-excel-or-not-at-all.md

The seventeenth run's note: `EOMONTH` and `EDATE`, the boolean (from: dates-and-text ADR-0047 2).

Old:

````
  calendar, and stays `#VALUE!` until Excel is asked. A boolean typed into either is `#VALUE!`, and that is
  Excel's answer too (EOMONTH-009, EDATE-009).
````

New:

````
  calendar, and stays `#VALUE!` until Excel is asked *(answered by the area passes, but for `EDATE` from serial
  0 to January 1900, March 1900 or January 10000: see the note of 2026-10-12, the area passes)*. A boolean typed
  as `EOMONTH`'s months or as `EDATE`'s start is `#VALUE!`, and that is Excel's answer too (EOMONTH-009,
  EDATE-009) *(the bullet said "typed into either" until the area passes; the run typed it into these two
  arguments alone, and as `EOMONTH`'s start and `EDATE`'s months the engine's `#VALUE!` is a refusal until Excel
  is asked: see the note of 2026-10-12, the area passes)*.
````

### 9. docs/adr/0047-the-formula-engine-is-exsheets-own-and-answers-as-excel-or-not-at-all.md

The seventeenth run's note: `WEEKDAY`'s day (from: dates-and-text ADR-0047 3).

Old:

````
  `WEEKDAY`'s day from the real calendar's are `uncertain` until a run asks them.
````

New:

````
  `WEEKDAY`'s day from the real calendar's are `uncertain` until a run asks them *(the area pass asked them,
  and Excel's day is `WEEKDAY`'s: NETWORKDAYS-006 to 008, WORKDAY-006 to 008)*.
````

### 10. docs/adr/0047-the-formula-engine-is-exsheets-own-and-answers-as-excel-or-not-at-all.md

The financial `type` bullet (from: financial 2).

Old:

````
- **A `type` that is a whole number other than 0 is payment at the start**, as Excel read 2
  (PMT-008). A fractional one stays `#VALUE!` until Excel is asked.
````

New:

````
- **A `type` that is a whole number other than 0 is payment at the start**, as Excel read 2
  (PMT-008). A fractional one stays `#VALUE!` until Excel is asked. *(Answered by the area passes of
  2026-10-12: `type` is a truth value. Excel gave the payment at the start for 0.5, −1 and 0.1 (PMT-009,
  PMT-010, PMT-019), so a fraction is not truncated or rounded, and any `type` other than 0 is payment at
  the start, in PMT, PV and FV.)*
````

### 11. docs/adr/0047-the-formula-engine-is-exsheets-own-and-answers-as-excel-or-not-at-all.md

The financial `nper` bullet (from: financial 3).

Old:

````
- Following "admitted with some argument values refused", at a rate other than 0 an `nper` that is
  not a whole number from 0 to 2^53 is `#VALUE!`: squaring raises only to a whole number, and how
  Excel raises 1+rate to any other has not been observed. At a rate of 0 no power is raised, and any
  `nper` is answered.
````

New:

````
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
````

### 12. docs/adr/0047-the-formula-engine-is-exsheets-own-and-answers-as-excel-or-not-at-all.md

Two notes after the paragraph that ends the financial note: what the area passes answered, and the exception for `PMT` (from: financial 4, its heading and the decision's sentence reworded (this branch's choices, above)).

Old:

````
The cases the search could not settle — which of two orders of the payment's factors, and which of
the evaluations that also give PMT-001 to 008 — are `uncertain` in the corpus, with the engine's
double, for the next Windows run.
````

New:

````
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
````

### 13. docs/adr/0047-the-formula-engine-is-exsheets-own-and-answers-as-excel-or-not-at-all.md

A note at the end: `EOMONTH` and `EDATE` from serial 0 or 60, a serial between -1 and 0, the boolean (from: dates-and-text ADR-0047 4).

Old:

````
OR-010 ask it.
````

New:

````
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
````

### 14. docs/adr/0049-linked-tables-are-the-consumers-data-read-by-key.md

"Settled while building": a table's bare name through `INDIRECT` is refused (from: indirect 1).

Old:

````
  Refusing on entry would make a saved document impossible to reopen before its tables arrive.
````

New:

````
  Refusing on entry would make a saved document impossible to reopen before its tables arrive.

- **A table's bare name read through `INDIRECT` is refused** *(2026-10-12, decided with the user:
  `#VALUE!`, in either style)*. Excel reads `INDIRECT("Positions")` as the table's data, every column
  and every row without the header (INDIRECT-053 to 057, observed by the second area pass of that day),
  and a Formula could then read the table by position, `INDEX(INDIRECT("Positions"),1,1)`, which rule 2
  does not offer. It is `#VALUE!` in either style, Excel's answers are recorded beside the refusal as a
  difference by decision, and it is a Known limitation of `0.1.0`
  ([ADR-0202](./0202-what-0-1-0-holds-of-exsheet-and-expivot-and-where-it-says-what-it-does-not-do.md)).
  Typed, the bare name is a Refused Formula that shows `#NAME?` (TABLE-024) *(2026-10-13, decided with the
  user: no Formula catches it, and a cell that reads it is refused)*; `Table[Column]`, typed or as
  `INDIRECT`'s text, reads the column.
````

### 15. docs/adr/0120-text-reads-its-format-code-in-the-invariant-spelling-as-a-cell-format-shows-it.md

The note of 2026-10-12: the two readings it left open (from: dates-and-text ADR-0120 1).

Old:

````
  - The run asked a positive number only. Two readings it did not settle stay `#VALUE!` until Excel is asked.
````

New:

````
  - The run asked a positive number only. Two readings it did not settle stay `#VALUE!` until Excel is asked
    *(both answered by the area pass: see the note of 2026-10-12, the area passes, below)*.
````

### 16. docs/adr/0120-text-reads-its-format-code-in-the-invariant-spelling-as-a-cell-format-shows-it.md

The same note: the empty code's bullet (from: dates-and-text ADR-0120 2).

Old:

````
  code without a text section: text stays as it is, and a boolean stays `TRUE` or `FALSE` (TEXTFN-023 and 024,
  `uncertain`).
````

New:

````
  code without a text section: text stays as it is, and a boolean stays `TRUE` or `FALSE` (TEXTFN-023 and 024,
  observed by the area pass).
````

### 17. docs/adr/0120-text-reads-its-format-code-in-the-invariant-spelling-as-a-cell-format-shows-it.md

A note after it: `General` and a lone `@` (from: dates-and-text ADR-0120 3).

Old:

````
  - Both cases record Excel's answer beside the refusal.
````

New:

````
  - Both cases record Excel's answer beside the refusal.

*(2026-10-12, the area passes)* **Both readings the seventeenth run left open are answered, and follow Excel's
answer** (`verification/2026-10-12-windows-area/` and `-area-2/`, ADR-0202). `TEXT(-1/3,"General")` is
`-0.333333333` (TEXTFN-025) and `TEXT(1/3,"@")` is `0.333333333` (TEXTFN-028).

- **`General` is General at its own limit, 11 characters, as a wide column shows a positive number (GW-004), and
  the minus sign is not one of them**: a negative number takes one character more than its magnitude, of eleven
  digits and of twelve alike (TEXTFN-029, 030).
- **A code of a text section alone, such as `@`, shows a number as `TEXT`'s General does**, fitted to those 11
  characters, and not as General in full: a negative number, twelve digits, a small number and a section with a
  literal, which is left out (TEXTFN-031 to 034).
- Every case of `text.json` is `observed`.
````

### 18. docs/adr/0124-volatile-functions-are-recalculated-after-every-change.md

The seventeenth run's sentence on R1C1 text (from: indirect 2).

Old:

````
does not spill. **R1C1 text in a form no run has observed stays `#VALUE!`**: relative (`R[1]C[-1]`, `RC`), whole rows
or columns (`R3`, `C2`), a number with a leading 0, and structured or `#` text in R1C1 style. INDIRECT-030 to 042 put
the forms generalised from the observed ones to the next run. Excel reads R1C1 text in the letters of its display
````

New:

````
does not spill. **R1C1 text in a form no run has observed stays `#VALUE!`**: relative (`R[1]C[-1]`, `RC`), whole rows
or columns (`R3`, `C2`), a number with a leading 0, and structured or `#` text in R1C1 style (the two area passes of
the same day observed a form of each; the note below says what is read since, and what stays refused). INDIRECT-030 to
042 put the forms generalised from the observed ones to the next run. Excel reads R1C1 text in the letters of its display
````

### 19. docs/adr/0124-volatile-functions-are-recalculated-after-every-change.md

A note at the end of "`RAND`, `RANDBETWEEN` and `INDIRECT`" (from: indirect 3).

Old:

````
Formula twice, and one that moves no spill once.)*
````

New:

````
Formula twice, and one that moves no spill once.)*

*(2026-10-12, after the two area passes (ADR-0208's remedy): **R1C1 text is read as Excel was observed to read it,
and in the forms that follow from its answers by the rule they settle; a declared Linked Table's bare name stays
refused, by decision.** The first pass read, in C2, `INDIRECT("R[1]C[-1]",FALSE)` as 45, B3's Value,
`SUM(INDIRECT("R3",FALSE))` as 45 and `SUM(INDIRECT("C2",FALSE))` as 118.333 (INDIRECT-036 to 038). The second read
the forms those left open: `RC[-1]` from C2 is 1.333 and `R[2]C` from B1 is 45, so an R or a C with no number is the
Formula's own row or column, not every one (INDIRECT-044, 051, 066); `R[0]C[-1]` and `R[-0]C[-1]` are 1.333 and
`R[+1]C[-1]` is 45, so an offset is a signed number, 0 included (INDIRECT-063 to 065); `R03C2` is 45, so a number is
read with its leading 0s (INDIRECT-059); `R2C2:R3` and `R3:C2` are `#REF!`, so parts of two kinds joined name no place
(INDIRECT-058, 062); `R1C1#` is the Spill Range, as `A1#` is (INDIRECT-060); and `Positions[PV]` read in R1C1 style is
the column, as in A1 style (INDIRECT-061). INDIRECT-045 to 050, the forms the first pass's answers gave, agreed. What
follows is read until a run asks it (INDIRECT-067 to 072, 075 to 081, 093): R and C each take a number, an offset in
brackets or nothing, in any combination; a row part alone is a whole row, a column part alone a whole column; two
parts of the same kind joined by `:` name the rectangle they span, and two of different kinds name no place, whichever
comes first; a `#` after any one cell on its own is its Spill Range; another Sheet's name names no place, whatever follows it, as
it was observed to in A1 style (INDIRECT-008); a Linked Table's text is read alike whatever `a1` says. `R` alone is
therefore the Formula's whole row, `C` its whole column and `RC` its own cell, each a cycle. **Still `#VALUE!`**, where
the observed answers leave the readings apart: an offset past the Sheet's edge, which Excel reads as a place
(`R[-5]C[-1]` from C2 is 0, a blank cell, not `#REF!`, INDIRECT-052) though no run has told whether it wraps round to
the Sheet's far side or stops at the edge, since both cells are blank there; a row or column number 0; a row or column
number past the Sheet's edge (`R1048577C2`, `R2C16385`), which could name no place, as `XFE1` does in A1 style
(INDIRECT-017), or a place, as an offset past the edge does (2026-10-12, decided with the user: in R1C1 style a row or
column number past the Sheet's edge is refused with `#VALUE!` until Excel is asked, and INDIRECT-074 asks it; A1 text
past the edge stays `#REF!`, as `XFE1` was observed to be); and, in either style, a `#` after a range, a row or a
column, whatever the parts joined (`A1:A2#`, `A:A#`, `R2C2:R3C2#`, `R2C2:R3#`), which no run has observed in either
style and which could name no place, the first cell's Spill Range, the cells themselves or the first cell joined to
the second's Spill Range, `A1:(A2#)`; and, in either style, a `#` inside a join, after its first part (`A1#:A2`,
`R1C1#:R2C1`), which no run has observed either and which could name no place, the first cell's Spill Range joined to
the second part, `(A1#):A2`, as `A1#` is read, or the cells themselves with the `#` passed over (INDIRECT-073, 082 to
084, 090 to 092 and 094 to 099 ask them). Other A1 text
that goes on past its Reference names no place, as the intersection Excel was observed to refuse does (INDIRECT-021):
the engine's rule, which no case asks. **A declared Linked Table's bare name is `#VALUE!` by decision** (2026-10-12,
decided with the user: the bare name stays `#VALUE!`, in either style): Excel reads it as the table's data — over a table of two rows,
`COLUMNS(INDIRECT("Positions"))` is 2, `ROWS` 2, `INDEX(…,1,1)` the first row's first Value, `COUNTA` 4, and the name
as a Formula's result spills the data (INDIRECT-053 to 057), which also gives the first pass's `SUM`, 250.5
(INDIRECT-043) — but a Formula could then read the table by position, and
[ADR-0049](./0049-linked-tables-are-the-consumers-data-read-by-key.md)'s rule 2 reads a Linked Table by key, never by
position. The six cases hold Excel's answers beside the refusal, a difference by decision, and the refusal is a Known
limitation of `0.1.0` (ADR-0202). Typed, a table's bare name shows `#NAME?` and is refused, which no Formula
catches (2026-10-13, decided with the user; TABLE-024).)*
````

### 20. docs/adr/0125-a-formula-whose-result-is-an-array-spills-as-excel-365s-does.md

"Not yet: functions lifted over arrays": the refusal is the Formula's, and every Formula's that reads its cell (from: sort-and-unique 2, its last sentence replaced (where two proposals edited the same text, above)).

Old:

````
this, and it comes as later tickets. Until a function is lifted, an array where it wants one Value
stays `#VALUE!`, as ADR-0047 had it: an Error Value, never another answer.
````

New:

````
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
````

### 21. docs/adr/0125-a-formula-whose-result-is-an-array-spills-as-excel-365s-does.md

A note after the note of 2026-10-12 under "What becomes Excel's answer" (from: sort-and-unique 1).

Old:

````
guess where the blank goes, what its spilled cell shows, how a descending sort places it and whether `UNIQUE` counts
it one of a kind with 0; SORT-012 to 014 and UNIQUE-010 and 011 ask those.)*
````

New:

````
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
````

### 22. docs/adr/0202-what-0-1-0-holds-of-exsheet-and-expivot-and-where-it-says-what-it-does-not-do.md

"The run ExGrid's own by-hand rows need anyway": `MOD` as the example (from: kth-and-mod 2).

Old:

````
  - **A function that disagrees is corrected before the tag, or the arguments where it disagrees are
    refused with an Error Value**, as `MOD` refuses a quotient of 2^27 or more (ADR-0047). A width, a
    format or an undo that disagrees is corrected.
````

New:

````
  - **A function that disagrees is corrected before the tag, or the arguments where it disagrees are
    refused with an Error Value**, as `MOD` refuses a quotient of 2^27 or more (ADR-0047). A width, a
    format or an undo that disagrees is corrected. *(2026-10-12: Excel answers a quotient of 2^27 or
    more, and so does `MOD` now; its `#NUM!` from 1,125,900,000,000 is Excel's own answer. `MOD` is an
    example still: where the evaluations at 64 bits that give every answer Excel gave part, as at
    `MOD(-1E-17,1)`, it is `#VALUE!` until a Windows run asks (ADR-0047's note of that date).)*
````

### 23. docs/adr/0202-what-0-1-0-holds-of-exsheet-and-expivot-and-where-it-says-what-it-does-not-do.md

The note on `NPV` and `PMT`: what the answers decided (from: financial 5).

Old:

````
  to the one `oracle.ps1 -Area` pass before `0.1.0-rc.1` (ADR-0208's note of this date), and the answers
  decide. If no such pass can be made before the tag, `NPV` is refused for `0.1.0` as `IRR` is, listed
  under Known limitations, and `PMT` stays as it is.
````

New:

````
  to the one `oracle.ps1 -Area` pass before `0.1.0-rc.1` (ADR-0208's note of this date), and the answers
  decide. If no such pass can be made before the tag, `NPV` is refused for `0.1.0` as `IRR` is, listed
  under Known limitations, and `PMT` stays as it is.
  *(2026-10-12, what the answers decided. Two passes were made, `verification/2026-10-12-windows-area/`
  and `-area-2/`. **`NPV` is refused for `0.1.0`, as `IRR` is** (`#NAME?`), and listed under Known
  limitations: Excel answers the same values alike typed or in a range, which leaves 18 distinct observed
  doubles, and none of the 5,796 evaluations searched gives them all; the best give 7 of 21. **`PMT` at
  small rates agrees** (PMT-017, at 10^-7). **`PMT` at ordinary rates does not**: 19 of its 30 observed
  payments were 1 to 11 units in the last place from the engine's (23 since the payment at the start is
  rounded once, ADR-0047's note of 2026-10-13), and none of the 39,602 evaluations
  searched gives every payment Excel gave. Decided with the user the same day: **`PMT` is kept, under
  ADR-0047's exception for `PMT` alone** (its note of this date), and its last digits are listed under
  Known limitations. **`XNPV` agrees at every observed double**: 4 of the 2,592 candidates searched give
  all nine, two evaluations that part only where a last addition nearly cancels, where the engine
  refuses; none of them gives Excel's `#NUM!` at a negative rate, which the engine gives by a check of
  the rate. `spikes/exsheet-financial-evaluation/` holds the searches.)*
````

### 24. docs/adr/0202-what-0-1-0-holds-of-exsheet-and-expivot-and-where-it-says-what-it-does-not-do.md

The Known limitations: `NPV`, `PMT`'s last digits, `INDIRECT`'s bare name and an R1C1 number past the edge (from: financial 6 and indirect 4, one edit (where two proposals edited the same text, above)).

Old:

````
  *(2026-10-12, decided with the user: also a whole-column `SUM` on every row of a large Sheet. SH-19 is the
  case it decided, a 10,000 × 20 block with its twenty whole-column totals beside it, where an edit takes
  tens of milliseconds; with such a `SUM` on every row instead, an edit was measured at about 40 to 48
  seconds and holds the circuit that long. `verification/2026-10-11-linux-sh19/` has both.)*
````

New:

````
  *(2026-10-12, decided with the user: also a whole-column `SUM` on every row of a large Sheet. SH-19 is the
  case it decided, a 10,000 × 20 block with its twenty whole-column totals beside it, where an edit takes
  tens of milliseconds; with such a `SUM` on every row instead, an edit was measured at about 40 to 48
  seconds and holds the circuit that long. `verification/2026-10-11-linux-sh19/` has both.)*
  *(2026-10-12: also `NPV`, which stays undeclared by the note of that date above; and, decided with the
  user, `PMT`'s last digits, which may differ from Excel's, under ADR-0047's exception for `PMT` alone.)*
  *(2026-10-12, decided with the user: also `INDIRECT` over a declared Linked Table's bare name,
  `INDIRECT("Positions")`, which is `#VALUE!` where Excel reads the table's data: a Formula could then read
  the table by position, which ADR-0049's rule 2 does not offer; and, in R1C1 style, a row or column number
  past the Sheet's edge, `INDIRECT("R1048577C2",FALSE)`, which is `#VALUE!` until Excel is asked whether it
  names no place or a place. ADR-0124's note of that date has Excel's answers and the readings.)*
  *(2026-10-13, decided with the user: also a refused Anchor whose array's shape could depend on a Value —
  a refused `FILTER`, `UNIQUE`, `SORT`, `SORTBY`, `SEQUENCE` or `LET`, a call that can give a Reference, or
  a function given an array where it wants one Value, `=LEN(A1:A3)`: nothing spills, and the cells its
  array would cover read as blank (ADR-0125's note of that date). And `PMT`'s last digits are 1 to 11
  units in the last place at 23 of the 30 observed payments, at two of them in General's 15th digit.)*
````

### 25. docs/definition-of-done.md

§27's note of 2026-10-11, the SH-7 bullet: the exception for `PMT`, and the refusal never caught (from: financial 7 (reworded) and this branch (point 2)).

Old:

````
- **SH-7 is met as written**: every case under `tests/ExSheet.Engine.Tests/ExcelCases/` whose source is
  `uncertain` or `documented` is put to Excel on the sign-off's Windows run (`oracle.ps1 -Update`), and
  each that disagrees is corrected, or its arguments refused with an Error Value, before the tag.
````

New:

````
- **SH-7 is met as written**: every case under `tests/ExSheet.Engine.Tests/ExcelCases/` whose source is
  `uncertain` or `documented` is put to Excel on the sign-off's Windows run (`oracle.ps1 -Update`), and
  each that disagrees is corrected, or its arguments refused with an Error Value, before the tag.
  *(2026-10-12, decided with the user: one exception, `PMT`, kept though its result may differ from
  Excel's in its last digits, by ADR-0047's note of that date. Its cases that differ are recorded as
  differences by that decision, with Excel's double beside the engine's, and it is a known limitation.)*
  *(2026-10-12, decided with the user: a refusal is never caught, anywhere. An argument refused so
  refuses the Formula, and every Formula that reads its cell, whatever it does with it; ADR-0047's note
  of that date.)*
````

### 26. docs/definition-of-done.md

The note's third bullet: `NPV` and `PMT`'s last digits (from: financial 8).

Old:

````
- **SH-66 to SH-68 are built.** `IRR`, `XIRR` and `RATE` (ticket 13) and dragging a Reference Outline
  (ticket 31) are not in `0.1.0`, and are said as known limitations.
````

New:

````
- **SH-66 to SH-68 are built.** `IRR`, `XIRR` and `RATE` (ticket 13) and dragging a Reference Outline
  (ticket 31) are not in `0.1.0`, and are said as known limitations. *(2026-10-12: so is `NPV`, refused
  by ADR-0202's note of that date, and so are `PMT`'s last digits, kept by ADR-0047's.)*
````

### 27. docs/definition-of-done.md

SH-7's row (from: financial 9 (reworded)).

Old:

````
| **SH-7** | MUST | Each function of ADR-0047's set matches a table of Excel's observed results, blanks, text and Error Values in ranges included; an unknown name is `#NAME?` (ADR-0047) | Layer 1 | every row of every table |
````

New:

````
| **SH-7** | MUST | Each function of ADR-0047's set matches a table of Excel's observed results, blanks, text and Error Values in ranges included; an unknown name is `#NAME?` (ADR-0047), and a call of a function Excel has that ExSheet does not declare is a Refused Formula, which shows `#NAME?` *(2026-10-13, decided with the user)*. *(2026-10-12, decided with the user: `PMT` but in its last digits, ADR-0047's exception for `PMT` alone; its rows that differ are recorded as differences by that decision.)* | Layer 1 | every row of every table |
````

### 28. docs/definition-of-done.md

SH-68's row: `INDIRECT` (from: indirect 5).

Old:

````
| **SH-68** | MUST | `SUBTOTAL` and `AGGREGATE` answer their corpus cases while no row can be hidden, and a test named for ADR-0047 fails on the day one can. Inside their range, a cell whose Formula, or whose Spill Range's Anchor's, calls `SUBTOTAL` or `AGGREGATE` anywhere is nested: `SUBTOTAL` and `AGGREGATE`'s options 0 to 3 pass over it and options 4 to 7 count its Value, as the seventeenth Windows run read Excel's answers; an `AGGREGATE` function number for a function the engine does not support is `#VALUE!`, and a Formula that gives one of their reference-only arguments something that can never be a Reference is refused on entry. `ERROR.TYPE` gives `#GETTING_DATA` and `#CIRC!` back, and Excel's numbers for its newer Error Values: `#SPILL!` 9, `#CONNECT!` 10, `#BLOCKED!` 11, `#UNKNOWN!` 12, `#FIELD!` 13, `#CALC!` 14. `INDIRECT` reads A1-style text naming a place on the same Sheet, R1C1 text naming an absolute cell or a range of two, a Linked Table's column as structured text (waiting while the table does) and `A1#`, keeps its text through an insertion or deletion, gives `#VALUE!` for R1C1 text that is relative or names whole rows or columns, and `#REF!` for text that names no place, a table or a column that does not exist included (ADR-0047's and ADR-0124's notes of 2026-10-10, ADR-0124's of 2026-10-12) *(2026-10-11, decided with the user: for `AGGREGATE`'s options 4 to 7, a cell in range whose whole Formula is a `SUBTOTAL` makes the result `#VALUE!` until the sign-off's Windows run reads Excel's answer, which Microsoft documents as counted; then it follows Excel.)* *(2026-10-12: the seventeenth run read it — 6, counted — and the engine follows Excel, ADR-0047's note of that date.)* *(2026-10-12, the seventeenth Windows run read `ERROR.TYPE`'s numbers for Excel's newer Error Values, which the row had refused with `#VALUE!` until Excel was asked; it now follows Excel, ADR-0047's note of this date.)* | Layer 1 | as stated |
````

New:

````
| **SH-68** | MUST | `SUBTOTAL` and `AGGREGATE` answer their corpus cases while no row can be hidden, and a test named for ADR-0047 fails on the day one can. Inside their range, a cell whose Formula, or whose Spill Range's Anchor's, calls `SUBTOTAL` or `AGGREGATE` anywhere is nested: `SUBTOTAL` and `AGGREGATE`'s options 0 to 3 pass over it and options 4 to 7 count its Value, as the seventeenth Windows run read Excel's answers; an `AGGREGATE` function number for a function the engine does not support is `#VALUE!`, and a Formula that gives one of their reference-only arguments something that can never be a Reference is refused on entry. `ERROR.TYPE` gives `#GETTING_DATA` and `#CIRC!` back, and Excel's numbers for its newer Error Values: `#SPILL!` 9, `#CONNECT!` 10, `#BLOCKED!` 11, `#UNKNOWN!` 12, `#FIELD!` 13, `#CALC!` 14. `INDIRECT` reads A1-style text naming a place on the same Sheet; R1C1 text naming a cell, a whole row or column, the rectangle two parts of the same kind span, or one cell's Spill Range with `#`, each R and C a number, an offset in brackets from the Formula's own cell, or none, the own row or column; a Linked Table's column as structured text (waiting while the table does), whatever `a1` says; and `A1#`. It keeps its text through an insertion or deletion; gives `#VALUE!`, until a run observes them, for R1C1 text with a row or column number 0 or past the Sheet's edge or an offset past the Sheet's edge, and for a `#` after a range, a row or a column, or inside a join, in either style, whatever the parts joined, and, by decision, for a declared Linked Table's bare name (ADR-0049's rule 2); and `#REF!` for text that names no place, a table or a column that does not exist, A1 text past the Sheet's edge and R1C1 parts of two kinds joined included (ADR-0047's and ADR-0124's notes of 2026-10-10, ADR-0124's of 2026-10-12) *(2026-10-12, decided with the user: a declared Linked Table's bare name stays `#VALUE!`, in either style; in R1C1 style, a row or column number past the Sheet's edge is `#VALUE!` until Excel is asked; A1 text past the edge stays `#REF!`, as `XFE1` was observed to be.)* *(2026-10-11, decided with the user: for `AGGREGATE`'s options 4 to 7, a cell in range whose whole Formula is a `SUBTOTAL` makes the result `#VALUE!` until the sign-off's Windows run reads Excel's answer, which Microsoft documents as counted; then it follows Excel.)* *(2026-10-12: the seventeenth run read it — 6, counted — and the engine follows Excel, ADR-0047's note of that date.)* *(2026-10-12, the seventeenth Windows run read `ERROR.TYPE`'s numbers for Excel's newer Error Values, which the row had refused with `#VALUE!` until Excel was asked; it now follows Excel, ADR-0047's note of this date.)* | Layer 1 | as stated |
````

### 29. CONTEXT.md

Error Value, and the new term **Refused Formula** (decided with the user on 2026-10-13).

Old:

````
**Error Value**:
A Value that is an error — `#DIV/0!`, `#NAME?`, `#REF!`, `#VALUE!`, `#N/A` and the rest of
Excel's set, plus `#CIRC!` for a Formula that depends on itself, where Excel would show 0 —
produced by a Formula and carried into every Formula that uses it. It is data, like a number. **Not the Cell State Error**, which is the Consumer's verdict on a value, painted and
never computed ([ADR-0047](./docs/adr/0047-the-formula-engine-is-exsheets-own-and-answers-as-excel-or-not-at-all.md)).
_Avoid_: error (that is a Cell State), exception
````

New:

````
**Error Value**:
A Value that is an error — `#DIV/0!`, `#NAME?`, `#REF!`, `#VALUE!`, `#N/A` and the rest of
Excel's set, plus `#CIRC!` for a Formula that depends on itself, where Excel would show 0 —
produced by a Formula and carried into every Formula that uses it. It is data, like a number. The `#VALUE!` or
`#NAME?` of a **Refused Formula** looks like one and is not Excel's: no Formula catches it. **Not the Cell State Error**, which is the Consumer's verdict on a value, painted and
never computed ([ADR-0047](./docs/adr/0047-the-formula-engine-is-exsheets-own-and-answers-as-excel-or-not-at-all.md)).
_Avoid_: error (that is a Cell State), exception

**Refused Formula**:
A Formula ExSheet does not answer because it cannot answer it as Excel would. Its cell shows `#VALUE!`, or
`#NAME?` where it calls a function Excel has that ExSheet does not declare, or names a declared Linked Table
bare, and every Formula that reads it is refused too; no Formula catches it, in its own cell or in any cell that reads it. A
Consumer tells it from Excel's own Error Value by `Value.IsRefused`
([ADR-0047](./docs/adr/0047-the-formula-engine-is-exsheets-own-and-answers-as-excel-or-not-at-all.md)). **Not a
Refusal**, which is the grid's "no" to an operation and never computes a Value. *(2026-10-13, decided with the user.)*
_Avoid_: refusal (for the Formula; that is the grid's), refused value
````

### 30. src/ExSheet.Engine/README.md

The table of functions: `NPV` goes (from: financial 12).

Old:

````
| `PV`, `FV` | `rate, nper, pmt, [fv or pv], [type]` |
| `NPV` | `rate, value1, [value2], ...` |
| `XNPV` | `rate, values, dates` |
````

New:

````
| `PV`, `FV` | `rate, nper, pmt, [fv or pv], [type]` |
| `XNPV` | `rate, values, dates` |
````

### 31. src/ExSheet.Engine/README.md

The paragraph on spilling: a blank, and an Error Value key (from: sort-and-unique 5).

Old:

````
spill. Operators work element by element over ranges, and a function that reduces (`SUM`,
`SUMPRODUCT`) takes an array whole.
````

New:

````
spill. Operators work element by element over ranges, and a function that reduces (`SUM`,
`SUMPRODUCT`) takes an array whole. A blank in a spilled array shows 0. Among `SORT`'s and
`SORTBY`'s keys a blank sorts after numbers, text and booleans, in either order, blanks keeping
their order: Excel's `SORT` was seen to sort it so after numbers and text in either order and after
a boolean ascending, keeping two blank keys in their order in a descending sort, and its `SORTBY`
after a number in an ascending key; the rest is the engine's, by generalisation, until Excel is
asked. Beside an Error Value key the Formula is `#VALUE!`. In `UNIQUE` a blank is one of a kind only
with another blank, never with 0. Read inside a Formula, the blank `UNIQUE`, `SORT`, `SORTBY`,
`FILTER` and `TRANSPOSE` keep is read as the empty value a blank cell holds: `COUNTA` counts it in
the array, `INDEX` takes it out as a blank, and an operator, or most functions that read one Value
(`LEN`, `ISTEXT`, `MATCH`'s lookup value), read it as a blank cell's; the rest, `XLOOKUP`'s lookup
value and `ROUND` among them, make the Formula `#VALUE!`. Excel was seen to give answers that fit
this in three cases: `ISBLANK` found the blank `INDEX` took out of `SORT`'s blank (SORT-022), `=`
found the one it took out of `UNIQUE`'s equal to empty text (UNIQUE-018), and `COUNTA` counted it in
both arrays (SORT-021, UNIQUE-017); the rest is the engine's, carried from those until Excel is
asked. `SUMPRODUCT` and `CONCAT` add nothing for it, `SUM` neither except where that moves its last
addition, and `VLOOKUP`, `HLOOKUP` and `XLOOKUP` take an array that holds it where they never read
it, each because every reading gives the one answer. A function that would read it some other way no
run has settled makes the Formula `#VALUE!`. `SORT` answers an Error Value key that already follows
every number, the one shape Excel was asked (SORT-015).
````

### 32. src/ExSheet.Engine/README.md

"Where the engine cannot give Excel's answer": a refusal is never caught (from: this branch (point 2)).

Old:

````
Where the engine cannot give Excel's answer, it gives an Error Value and never a different
answer:
````

New:

````
Where the engine cannot give Excel's answer, it gives an Error Value and never a different
answer. Such a `#VALUE!` is a refusal, and no Formula catches it: not `IFERROR`, `ISERROR`, `COUNT` or
any other function around it in the same Formula, and not a Formula in another cell that reads it —
directly, through a range, a name, `INDIRECT`, `OFFSET`, a Spill Range or a Linked Table — which is
refused in turn, as one that reads a cycle is `#CIRC!` (ADR-0047, 2026-10-12, decided with the user).
A cell's refused Value is `#VALUE!` in every other way, and `Value.IsRefused` says it is one;
`Entry.FromValue` refuses it. Excel's own `#VALUE!`, `=1+"a"`'s, is caught as Excel's is. A call of a
function Excel has that ExSheet does not declare (`ExcelFunctionNames`), and a declared Linked Table's
bare name typed, are refused too and show `#NAME?` (2026-10-13, decided with the user); a cell that
reads one is refused and shows `#VALUE!`, and a name Excel does not have is Excel's own `#NAME?`. A
refused Anchor whose array's shape its text settles spills refusals over that shape; where the shape
could depend on a Value, nothing spills (ADR-0125, 2026-10-13). The refusals:
````

### 33. src/ExSheet.Engine/README.md

"Functions are not lifted over arrays yet" (from: sort-and-unique 6).

Old:

````
- **Functions are not lifted over arrays yet** (ADR-0125). An array where a function wants one
  Value — `ROUND(A1:A3,0)`, `IF(A1:A3>1,…)` — is `#VALUE!`, where Excel 365 applies the function to
  each Value and spills. `IFERROR` and `ISERROR` do not turn that refusal into a fallback. The `@`
  operator is refused on entry.
````

New:

````
- **Functions are not lifted over arrays yet** (ADR-0125). An array where a function wants one
  Value — `ROUND(A1:A3,0)`, `IF(A1:A3>1,…)` — makes the Formula `#VALUE!`, where Excel 365 applies
  the function to each Value and spills. The refusal is the Formula's: `IFERROR`, `ISERROR`,
  `COUNTA` and the rest do not turn it into an answer, given the range or around a call that was
  given it, so `=IFERROR(LEN(A1:A3),0)` is `#VALUE!`. The `@` operator is refused on entry.
````

### 34. src/ExSheet.Engine/README.md

The bullet "`TEXT` reads its code in the invariant spelling" (from: dates-and-text README 1).

Old:

````
  format would under the Sheet's culture (ADR-0120). `General` is fitted to 11 characters, as a
  cell's General is fitted to its column, and an empty code shows a number as empty text, as Excel's
  do. A code outside the cell formats' subset is `#VALUE!`, a fraction or a condition among them,
  though Excel answers both. Until Excel is asked, so are a negative number whose minus sign would
  cost its General a digit, and a number under a lone `@` whose fitted General is not its General in
  full.
````

New:

````
  format would under the Sheet's culture (ADR-0120). `General` is fitted to 11 characters, as a
  cell's General is fitted to a wide column, and a minus sign is not one of them
  (`TEXT(-1/3,"General")` is `-0.333333333`); a lone `@` shows a number as that General does; and an
  empty code shows a number as empty text, as Excel's do. A code outside the cell formats' subset is
  `#VALUE!`, a fraction or a condition among them, though Excel answers both.
````

### 35. src/ExSheet.Engine/README.md

"Asked of Excel before they are answered": one edit for the four areas' clauses (from: dates-and-text README 2, kth-and-mod 3a, financial 13 and sort-and-unique 4, one edit (where two proposals edited the same text, above)).

Old:

````
- **Asked of Excel before they are answered**, each refused until the next Windows run says what
  Excel gives: `EOMONTH` and `EDATE` from serial 0 or from serial 60, the day that never existed
  (`#VALUE!`); `NETWORKDAYS` and `WORKDAY` with a boolean typed as a date (`#VALUE!`); `SMALL` with a
  `k` that is not a whole number, and `LARGE` with one between 0 and 1; `PMT`, `PV` and `FV` with a
  fractional `type`, and, at a rate other than 0, with an `nper` that is not a whole number from 0 to
  2^53 (`#VALUE!`); `UNIQUE` with a blank in its array, and `SORT` and `SORTBY` with a blank or an
  Error Value among their keys (`#VALUE!`; the seventeenth run read only the Anchor of a blank's
  sort).
````

New:

````
- **Asked of Excel before they are answered**, each refused until the next Windows run says what
  Excel gives: `EDATE` from serial 0 to January 1900, March 1900 or January 10000, where the readings
  that give Excel's other answers part, `EOMONTH` with a boolean typed as its start, and `EDATE` with
  one typed as its months (`#VALUE!`); `NETWORKDAYS` and `WORKDAY` with a boolean typed as a date
  (`#VALUE!`); `MOD` where the evaluations at 64 bits that give every answer Excel gave part, as at
  `MOD(-1E-17,1)` and `MOD(-0.084474,2270)` (`#VALUE!`; MOD-033 to 036); `PV` at a rate of -1 with an
  `nper` above 0; `PMT` at a rate of -1 with periods, below -1 with a whole `nper` or a fraction whose
  reciprocal is odd, where a quantity the evaluations Excel's payments leave compute passes a double's
  range or falls below its normal numbers, at a growth below 2^-52, with no periods at the start, and
  where one of them magnifies its own roundings more than 2^6 times; `PMT`, `PV` and `FV` with an
  `nper` past 2^53 at a rate other than 0; and `XNPV` at a rate of 0, where its last addition nearly
  cancels, and where an Error Value in its rate or ranges fails beside another check that gives
  another answer (`#VALUE!`); `SORT` with an Error Value key that has a number after it, or beside
  text, a boolean, a blank or another Error Value, or in a descending sort, and `SORTBY` with any
  Error Value key (`#VALUE!`); `SORT` with a `sort_index` that is not a whole number (`#VALUE!`;
  SORT-042); `DATEDIF`, `YEARFRAC`, `HOUR`, `MINUTE` and `SECOND` with a serial between -1 and 0
  (`#VALUE!`; DATEDIF-048, YEARFRAC-058, HOUR-005, MINUTE-003, SECOND-003); a blank `UNIQUE`, `SORT`, `SORTBY`, `FILTER` or `TRANSPOSE` keeps, read
  by a function whose readings of it part, such as `AVERAGE`, `MIN` or `COUNT` (`#VALUE!`). Each of
  these refusals is the Formula's, and every Formula's that reads its cell: no Formula catches it.
````

### 36. src/ExSheet.Engine/README.md

"Answered as the seventeenth Windows run read Excel": `MOD` (from: kth-and-mod 3b).

Old:

````
- **Answered as the seventeenth Windows run read Excel.** `MOD` answers a quotient of 2^27 or more, and
  is `#NUM!` from 1,125,900,000,000, the limit a Microsoft 365 user reported (MOD-010 to 017 ask
  Excel). `LARGE`, and `AGGREGATE`'s 14, round a `k` that is not whole up: `LARGE(A1:B5,1.5)` is the
````

New:

````
- **Answered as the seventeenth Windows run read Excel.** `MOD` answers a quotient of 2^27 or more, and
  is `#NUM!` from 1,125,900,000,000, the limit a Microsoft 365 user reported, as the area pass of
  2026-10-12 read Excel on both sides of it (MOD-010 to 016). `LARGE`, and `AGGREGATE`'s 14, round a
  `k` that is not whole up: `LARGE(A1:B5,1.5)` is the
````

### 37. src/ExSheet.Engine/README.md

A bullet after it: what the area passes read (from: kth-and-mod 3c).

Old:

````
  an operator's result, a call such as `SEQUENCE(3)` — as Excel does.
````

New:

````
  an operator's result, a call such as `SEQUENCE(3)` — as Excel does.
- **Answered as the area passes of 2026-10-12 read Excel.** `SMALL`, and `AGGREGATE`'s 15, truncate a
  `k` that is not whole: `SMALL(A1:A9,1.5)` is the smallest. A `k` below 1 or above the count of the
  numbers is `#NUM!` for `LARGE` and `SMALL` alike, whole or not, before it is rounded:
  `SMALL(A1:A9,9.5)` over nine numbers is `#NUM!`. `AGGREGATE`'s array form takes text or a boolean
  given directly as an array holding no number: `AGGREGATE(14,6,"5",1)` is `#NUM!`. `MOD`'s remainder is
  exact, as Excel's is: `MOD(123456789012,0.7)` is 0.5000078322611261 and `MOD(1,0.1)`
  0.09999999999999995, where `number - divisor*INT(number/divisor)` in doubles gives 0.5000152587890625
  and 0. Where the divisor is added to a remainder of the other sign, the sum is held at 64 bits before
  it is stored as a double, which gives Excel's `MOD(-0.087626,6630)`, 6629.9123739999995, and a sum
  stored as the divisor itself is 0, as Excel's `MOD(-1E-20,1)` is. Where the evaluations at 64 bits
  that give every answer Excel gave still part, `MOD` is `#VALUE!` (above). The same evaluations with the
  sum held at a width no hardware holds (54 to 63 bits rounded, 54 to 65 truncated) give those answers
  too; the engine does not model them, and answers as 64 bits do where they would give another last
  digit, about 3 inputs of up to five digits in 100, a judgment Excel has yet to settle (MOD-039, 040).
````

### 38. src/ExSheet.Engine/README.md

The financial bullet (from: financial 14).

Old:

````
- **`PMT`, `PV` and `FV` give every double Excel was observed to give, to the bit** (ADR-0047, the
  seventeenth run). `PV` and `FV` evaluate Microsoft's identity in doubles, raising 1+rate by squaring.
  `PMT` computes the payment at the end exactly and rounds it once, and the payment at the start
  divides that double by 1+rate, rounded once. For `PMT` that is one of several evaluations that fit
  the four payments observed, at rates of 0.5% to 10% a period. At smaller rates they part, from the
  13th significant digit at a rate of 10^-7, and PMT-016 to 018 ask Excel. A `type` that is a whole
  number other than 0 is payment at the start, as 1 is.
````

New:

````
- **`PV`, `FV` and `XNPV` give every double and Error Value Excel was observed to give, to the bit**
  (ADR-0047, the seventeenth run and the area passes of 2026-10-12). `PV` and `FV`
  evaluate Microsoft's identity in doubles, the payment times (1+rate·type)·((g−1)/rate), with g the
  financial power: squaring to a whole `nper`, 1 over the power to a negative one, the square root at
  0.5, and e^(nper ln(1+rate)) otherwise, the exponential and the logarithm correctly rounded
  (`CorrectlyRounded`, the same on every platform; Excel's exponential is the correctly rounded one at
  XNPV-013, and no observed double tells logarithms apart); a negative 1+rate has an odd root where
  1/nper is odd and is `#NUM!` otherwise, and 1+rate of 0 is `#NUM!` to the power 0 and `#DIV/0!` to a
  negative one. `XNPV` divides each value by that power of its days over 365 and sums from the first;
  it checks the rate before it evaluates and is `#NUM!` below 0, Excel's answer at the rates asked
  (-0.5 to -2) and the engine's at others, refuses any cell of its values or dates that is not a
  number (text, a boolean or a blank), whatever else is among them, and refuses an Error Value in its
  rate or ranges beside another check that would give another answer. `type` is a truth value: any number other than 0, 0.5
  included, is payment at the start. **`PMT` is kept under ADR-0047's exception for `PMT` alone**: where
  it answers, the payment at the end is the exact payment rounded once, held between two bounds that
  decide its double (`Enclosure`, interval arithmetic over exact dyadic numbers, (1+rate)^nper - 1
  raised from the rate itself), and the payment at the start the exact payment at the start rounded
  once, held between bounds as well (2026-10-13, decided with the user). Excel's payment differs from
  ExSheet's at 23 of the 30 observed, by 1 to 11 units in the last place, at PMT-013 and PMT-026 in the
  15th significant digit, and no evaluation searched gives every one (a Known limitation of 0.1.0). Where the
  evaluations Excel's payments leave part in kind, or beyond the last digits, `PMT` refuses
  (`ExactPayment`, `Functions.Financial.cs`).
````

### 39. src/ExSheet.Engine/README.md

"`SUBTOTAL` and `AGGREGATE` while no row can be hidden": the array form (from: kth-and-mod 3d).

Old:

````
  array form takes a number given directly. `#VALUE!`: an argument that is no reference when it is
````

New:

````
  array form takes a Value given directly as an array of that one Value: a number is that number, and
  text or a boolean holds none (`#NUM!`). `#VALUE!`: an argument that is no reference when it is
````

### 40. src/ExSheet.Engine/README.md

The bullet "Before 1 March 1900" (from: dates-and-text README 3).

Old:

````
  (`NETWORKDAYS(30,100)` is 51), as Excel was observed to. A `WORKDAY` that falls before serial 0
  is `#NUM!`.
````

New:

````
  (`NETWORKDAYS(30,100)` is 51), as Excel was observed to. A `WORKDAY` that falls before serial 0
  is `#NUM!`. `EDATE` and `EOMONTH` read a start of serial 0 or 60 as `MONTH` and `DAY` read it, in
  January 1900 and on the 29th of February: `EOMONTH(0,1)` is 59 and `EDATE(60,1)` is 89, and from
  serial 0 `EDATE` keeps day 0, the day before the 1st, so `EDATE(0,1)` is 31. A date between -1 and
  0 is cut down to -1, and is `#NUM!`, under `YEAR`, `MONTH`, `DAY`, `WEEKDAY`, `DAYS`,
  `NETWORKDAYS`, `WORKDAY`, `EOMONTH` and `EDATE`, as Excel was observed to answer at -0.5
  (YEAR-008, MONTH-005, DAY-005, WEEKDAY-008, DAYS-005, NETWORKDAYS-009, WORKDAY-010, EOMONTH-016
  and 017, EDATE-017). `DATEDIF`, `YEARFRAC`, `HOUR`, `MINUTE` and `SECOND` give `#NUM!` for a
  serial of -1 or below, which they do not cut down. Excel was observed to at -1 under three of them
  (DATEDIF-045, YEARFRAC-056, MINUTE-002); below -1, and under `HOUR` and `SECOND` at all, it is the
  engine's answer, by generalisation. Between -1 and 0 their readings part — cut down to -1, `#NUM!`,
  or read as a date or a time of day, an answer — and they refuse it with `#VALUE!` until Excel
  is asked (DATEDIF-048, YEARFRAC-058, HOUR-005, MINUTE-003, SECOND-003).
````

### 41. src/ExSheet.Engine/README.md

The functions that stay undeclared: `NPV` (from: financial 15).

Old:

````
`IRR`, `XIRR` and `RATE` remain undeclared (`#NAME?`). The solver experiment in
`spikes/exsheet-financial-solvers/` does not yet reproduce Excel's chosen roots, stopping results
and failures. The October 3 observation run did not resolve existing Supported functions' other
`uncertain` corpus cases.
````

New:

````
`IRR`, `XIRR` and `RATE` remain undeclared (`#NAME?`). The solver experiment in
`spikes/exsheet-financial-solvers/` does not yet reproduce Excel's chosen roots, stopping results
and failures. `NPV` is undeclared too (ADR-0202, note of 2026-10-12): no evaluation in
`spikes/exsheet-financial-evaluation/` gives every double Excel gave for it. The October 3
observation run did not resolve existing Supported functions' other `uncertain` corpus cases.
````

### 42. docs/specs/exsheet-functions/spec.md

The `LARGE` and `SMALL` rows (from: kth-and-mod 4).

Old:

````
| `LARGE` | Supported | — | A `k` that is not whole is rounded up, as Excel's (ADR-0047, 2026-10-12) |
| `SMALL` | Supported | — | A `k` that is not whole is `#VALUE!` until observed |
````

New:

````
| `LARGE` | Supported | — | A `k` that is not whole is rounded up, and one below 1 or above the count of the numbers is `#NUM!`, as Excel's (ADR-0047, 2026-10-12) |
| `SMALL` | Supported | — | A `k` that is not whole is truncated, and one below 1 or above the count of the numbers is `#NUM!`, as Excel's (ADR-0047, 2026-10-12) |
````

### 43. docs/specs/exsheet-functions/spec.md

The `AGGREGATE` row (from: kth-and-mod 4).

Old:

````
| `AGGREGATE` | Supported | — | As `SUBTOTAL`; options 0 to 3 pass over a nested cell and 4 to 7 count it, and a function number for a function the engine does not support (13, 16 to 19) is `#VALUE!`. Ticket 16 |
````

New:

````
| `AGGREGATE` | Supported | — | As `SUBTOTAL`; options 0 to 3 pass over a nested cell and 4 to 7 count it, and a function number for a function the engine does not support (13, 16 to 19) is `#VALUE!`; text or a boolean given as the array holds no number. Ticket 16 |
````

### 44. docs/specs/exsheet-functions/spec.md

The `IFERROR` and `ISERROR` rows: a refusal is never caught (from: this branch (point 2)).

Old:

````
| `IFERROR` | Supported | — | Does not catch the refusal of a function given an array where it wants one Value, which is the Formula's, nested inside it or not (ADR-0125, IFERROR-009, 017) |
| `ISERROR` | Supported | — | |
````

New:

````
| `IFERROR` | Supported | — | Does not catch a refusal, which is the Formula's, nested inside it or not, nor a refused cell it reads, directly or through a range, a name, `INDIRECT`, a Spill Range or a Linked Table (ADR-0047, 2026-10-12, decided with the user; ADR-0125; IFERROR-009, 017 to 020) |
| `ISERROR` | Supported | — | Does not read a refusal, nor a refused cell, as an error: the Formula is refused (ADR-0047, 2026-10-12; ISERROR-011) |
````

### 45. docs/specs/exsheet-functions/spec.md

The `IFNA` row (from: this branch (point 2)).

Old:

````
| `IFNA` | Supported | — | The partner of `XLOOKUP` and `MATCH`. Like `IFERROR`, it catches neither `#CIRC!` nor `#GETTING_DATA` (ADR-0047, ADR-0049) |
````

New:

````
| `IFNA` | Supported | — | The partner of `XLOOKUP` and `MATCH`. Like `IFERROR`, it catches neither `#CIRC!` nor `#GETTING_DATA` (ADR-0047, ADR-0049), nor a refusal (ADR-0047, 2026-10-12; IFNA-008) |
````

### 46. docs/specs/exsheet-functions/spec.md

The `ERROR.TYPE` row (from: this branch (point 2)).

Old:

````
| `ERROR.TYPE` | Supported | — | `#GETTING_DATA` and `#CIRC!` are given back, never numbered; Excel's newer Error Values take Excel's numbers, 9 to 14, read by the seventeenth Windows run (ADR-0047, 2026-10-10 and its note of 2026-10-12). Ticket 17 |
````

New:

````
| `ERROR.TYPE` | Supported | — | `#GETTING_DATA` and `#CIRC!` are given back, never numbered; Excel's newer Error Values take Excel's numbers, 9 to 14, read by the seventeenth Windows run (ADR-0047, 2026-10-10 and its note of 2026-10-12); a refusal is not numbered, and the Formula is refused (ADR-0047, 2026-10-12; ERRORTYPE-022). Ticket 17 |
````

### 47. docs/specs/exsheet-functions/spec.md

The `MOD` row (from: kth-and-mod 4).

Old:

````
| `MOD` | Supported | — | Excel's sign follows the divisor; `#NUM!` from a quotient of 1,125,900,000,000 |
````

New:

````
| `MOD` | Supported | — | Excel's sign follows the divisor. The exact remainder; where the divisor is added to it, the sum held at 64 bits and then stored as a double, and a sum stored as the divisor 0, as Excel's (MOD-017 to 032); `#VALUE!` where the evaluations at 64 bits that give Excel's answers still part, as at `MOD(-1E-17,1)` and `MOD(-0.084474,2270)`, until Excel is asked (MOD-033 to 036; ADR-0047, 2026-10-12); where only evaluations at widths no hardware holds give another double, the 64 bits' answer, on a judgment Excel has yet to settle (MOD-039, 040); `#NUM!` from a quotient of 1,125,900,000,000 |
````

### 48. docs/specs/exsheet-functions/spec.md

The `EOMONTH` to `WORKDAY` rows (from: dates-and-text catalogue).

Old:

````
| `EOMONTH` | Supported | — | |
| `EDATE` | Supported | — | |
| `WEEKDAY` | Supported | — | |
| `NETWORKDAYS` | Supported | — | |
| `WORKDAY` | Supported | — | |
````

New:

````
| `EOMONTH` | Supported | — | A start of serial 0 is in January 1900, as `MONTH` reads it; a boolean typed as the start is `#VALUE!` until Excel is asked (ADR-0047) |
| `EDATE` | Supported | — | From serial 0 to January 1900, March 1900 or January 10000, and with a boolean typed as the months, `#VALUE!` until Excel is asked (ADR-0047) |
| `WEEKDAY` | Supported | — | |
| `NETWORKDAYS` | Supported | — | A boolean typed as a date is `#VALUE!` until Excel is asked (ADR-0047) |
| `WORKDAY` | Supported | — | A boolean typed as a date is `#VALUE!` until Excel is asked (ADR-0047) |
````

### 49. docs/specs/exsheet-functions/spec.md

The paragraph on the retained evidence: a refusal is never caught (from: this branch (point 2)).

Old:

````
observed result in `excelExpect` and names ADR-0047 in `engineDiffersByDecision`. The engine's
README lists the admitted domains; broadening one requires evidence, not a relaxed assertion.
````

New:

````
observed result in `excelExpect` and names ADR-0047 in `engineDiffersByDecision`. A refusal is the
Formula's, and every Formula's that reads its cell, which no Formula catches (ADR-0047, 2026-10-12,
decided with the user). The engine's
README lists the admitted domains; broadening one requires evidence, not a relaxed assertion.
````

### 50. docs/specs/exsheet-functions/issues/01-p1-ready-functions.md

Ticket 01: a comment on `MOD` (from: kth-and-mod 6, without the box it offered (this branch's choices, above)).

Old:

````
under `YEARFRAC` (YEARFRAC-054), so neither answers the other argument.
````

New:

````
under `YEARFRAC` (YEARFRAC-054), so neither answers the other argument.

2026-10-12, with the area passes: MOD-010 to 016 agreed, so the limit at a quotient of
1,125,900,000,000 holds on both sides. The doubles did not: Excel's remainder is exact
(`MOD(123456789012,0.7)` is 0.5000078322611261, MOD-017; `MOD(1,0.1)` is 0.09999999999999995, MOD-020,
where the identity in doubles gave 0.5000152587890625 and 0), a remainder a hair from 0 is itself
(`MOD(1.1,0.1)` is 2.7755575615628914E-17, MOD-030, where it gave 0), the divisor added to a remainder
of the other sign is not rounded once (`MOD(-0.087626,6630)` is 6629.9123739999995, MOD-029, the sum
held at 64 bits and then stored as a double), and a sum stored as the divisor itself is 0
(`MOD(-1E-20,1)`, MOD-032). At 64 bits three evaluations give all 32 answers Excel has given, and 31
more at widths no hardware holds (the sum held at 54 to 63 bits rounded, or 54 to 65 truncated); none
evaluates the identity in doubles (`spikes/exsheet-financial-evaluation/mod-search.cs`,
`mod-search.txt`). The engine answers as the three at 64 bits where they agree, and `#VALUE!` where they
part: where the sum is the divisor as a double but not at 64 bits (`MOD(-1E-17,1)`), and where
truncating at 64 bits stores another double than rounding there (`MOD(-0.084474,2270)`), 13 of 1,915,648
random inputs of up to five digits. It does not model the other widths, on the judgment that only the
hardware's formats are natural, and so answers 65,972 of those inputs (3.4%) where one of them gives
another double. MOD-033 to 036 ask where the three part, MOD-037 and 038 two answers all 34 share, and
MOD-039 and 040 two the judgment answers (ADR-0047's note of that date; `KthAndModTests`). The ticket
stays done: these cases are asked by the one `oracle.ps1 -Area` pass before `0.1.0-rc.1` (ADR-0208's
note of 2026-10-12), whose release check refuses the tag while any is `uncertain`.
````

### 51. docs/specs/exsheet-functions/issues/04-p2-ready-functions.md

Ticket 04: comments on `PMT`, `PV`, `FV`, `NPV`, `LARGE` and `SMALL` (from: financial (ticket 04), kth-and-mod 5 and dates-and-text (ticket 04)).

Old:

````
the user's question (`docs/agents/proposals/run17-financial.md`).
````

New:

````
the user's question (`docs/agents/proposals/run17-financial.md`).

2026-10-12, the area passes: `NPV` is refused for 0.1.0 (ADR-0202's note of that date) and moves to
ticket 13. `PV` and `FV` give every double and Error Value Excel was observed to give: they read any
`type` as Excel does and raise 1+rate to any `nper` by the financial power, with the exponential and
the logarithm correctly rounded (`spikes/exsheet-financial-evaluation/`, `Search.cs --refusals`); PV at
a rate of -1 with an `nper` above 0 is refused until asked (PV-013). `PMT` is kept under ADR-0047's
exception for `PMT` alone (decided with the user): its result may differ from Excel's in its last
digits. Where it answers, the branch gives the exact payment rounded once at the end of each period,
and the exact payment rounded once at the start too (decided with the user, 2026-10-13); it refuses where the evaluations its observed payments leave part in kind or beyond the
last digits, until asked (PMT-034 to 053).

2026-10-12, with the area passes (`verification/2026-10-12-windows-area/report.md` and
`verification/2026-10-12-windows-area-2/report.md`): Excel truncates `SMALL`'s k that is not whole
(SMALL-004, 005, 009: 1.5 and 1.2 are each the smallest, 3.9 the 3rd), rounds `LARGE`'s up (LARGE-008,
009), and a k below 1 or above the count of the numbers is `#NUM!` before it is rounded (LARGE-011, 013:
0.5 and 0.99; SMALL-007: 0.5; SMALL-008: 9.5 over nine numbers, which truncated would be the 9th
smallest; LARGE-010: 10.5 over ten). `LARGE` and `SMALL` coerce a Value given directly as the array
(LARGE-014: `LARGE("5",1)` is 5; SMALL-010: `SMALL(TRUE,1)` is 1). The engine reads k so for `LARGE`,
`SMALL` and `AGGREGATE`'s 14 and 15 (`KthOf`), and no k is `#VALUE!` any more (ADR-0047's note of that
date; `KthAndModTests`). SMALL-011 (`SMALL(A1:A9,9.2)`, `#NUM!`) is a new `uncertain` case: a k a little
above the count, which rounded to nearest would be 9. It is asked with ticket 16's open box, beside
AGGREGATE-068, and this ticket stays done (ADR-0208's note of 2026-10-12). NETWORKDAYS-010 and
WORKDAY-011 ask the refusal of a boolean typed as a date, which no case asked before; WEEKDAY-008,
DAYS-005, NETWORKDAYS-009 and WORKDAY-010 are `#NUM!` in Excel, as the engine answers.
````

### 52. docs/specs/exsheet-functions/issues/05-p3-ready-functions.md

Ticket 05: comments on `XNPV` and on `HOUR`, `MINUTE` and `SECOND` below 0 (from: financial (ticket 05) and dates-and-text (the time parts)).

Old:

````
function is first is the declared set's order, and the test is about the caret.
````

New:

````
function is first is the declared set's order, and the test is about the caret.

2026-10-12, the area passes: `XNPV` gives every double Excel was observed to give: the days over 365,
whole years by squaring and e^(years ln(1+rate)) correctly rounded otherwise, each value divided,
summed from the first (XNPV-001, 004 to 006, 011, 013 to 016), one of the two evaluations that give
them. It checks the rate before it evaluates: `#NUM!` below 0, Excel's answer at -0.5 to -2 (XNPV-007
to 010, 012, 017), which no evaluation gives, and the engine's at other negative rates (XNPV-020
asks). It refuses a rate of 0, a last addition that nearly cancels, and an Error Value in its rate or
ranges beside another check that would give another answer, until Excel is asked (XNPV-018, XNPV-019,
XNPV-021 to 030; `spikes/exsheet-financial-evaluation/`).

2026-10-12, the area passes: `HOUR`, `MINUTE` and `SECOND` give `#NUM!` for any serial below 0, and do not
cut it down to its day as `YEAR` and its kin do. Excel gave `#NUM!` at -1 under `MINUTE` (MINUTE-002). At
-0.5, between -1 and 0, the readings part: cut down to -1 it is `#NUM!`, and read as a time of day -0.5 is
12:00, where `HOUR` would be 12. The engine refuses a serial between -1 and 0 with `#VALUE!` until Excel is
asked (2026-10-13), and HOUR-005, MINUTE-003 and SECOND-003 ask it.
The ticket stays done: they are asked by the one `oracle.ps1 -Area` pass before `0.1.0-rc.1` (ADR-0208's note
of 2026-10-12).
````

### 53. docs/specs/exsheet-functions/issues/11-yearfrac-and-datedif.md

Ticket 11: a comment on a serial between -1 and 0 (from: dates-and-text (the five cases)).

Old:

````
Basis choices follow Microsoft's [YEARFRAC documentation](https://support.microsoft.com/en-us/excel/functions/yearfrac-function).
````

New:

````
Basis choices follow Microsoft's [YEARFRAC documentation](https://support.microsoft.com/en-us/excel/functions/yearfrac-function).

2026-10-12, the area passes: `DATEDIF` and `YEARFRAC` give `#NUM!` for any serial below 0, and do not cut it
down to its day as the nine functions the second area pass asked at -0.5 do. Excel gave `#NUM!` at -1
(DATEDIF-045, YEARFRAC-056); between -1 and 0 the engine refuses it with `#VALUE!` until Excel is asked
(2026-10-13), and DATEDIF-048 and YEARFRAC-058 ask it. The readings part under `DATEDIF`: cut down to -1, or refused below 0,
`DATEDIF(-0.5,1,"D")` is `#NUM!`; truncated to serial 0, it is 1, and `DATEDIF` reads a boolean typed as a
date as 1 (DATEDIF-046), where `YEARFRAC` gives `#VALUE!` (YEARFRAC-054), so its coercion is not shown to be
the nine functions'. The ticket stays done: the cases are asked by the one
`oracle.ps1 -Area` pass before `0.1.0-rc.1` (ADR-0208's note of 2026-10-12).
````

### 54. docs/specs/exsheet-functions/issues/16-subtotal-and-aggregate.md

Ticket 16: its `Roadmap:` line (from: this branch (ADR-0208's precedent)).

Old:

````
Roadmap: ExSheet — `AGGREGATE`'s `SMALL` checked against Excel for a k above the count of its
numbers, which `SMALL` refuses with `#NUM!`.
````

New:

````
Roadmap: ExSheet — `AGGREGATE`'s and `SMALL`'s k checked against Excel above the count of the
numbers, which they refuse with `#NUM!`, and `AGGREGATE` over a cell ExSheet refused.
````

### 55. docs/specs/exsheet-functions/issues/16-subtotal-and-aggregate.md

Ticket 16: its open box (from: kth-and-mod 5 (SMALL-011 here, not a box in ticket 04) and this branch (AGGREGATE-069)).

Old:

````
- [ ] `oracle.ps1 -Area aggregate` before the tag (ADR-0208): AGGREGATE-068, the `uncertain` case the
      second area pass's answers led to — `SMALL`'s k above the count of the numbers, which Excel's
      `SMALL` refuses before truncating it (SMALL-008). The engine agrees with the answer, or is
      corrected to it, before this ticket is done
````

New:

````
- [ ] `oracle.ps1 -Area aggregate,small` before the tag (ADR-0208): AGGREGATE-068, the `uncertain` case the
      second area pass's answers led to — `SMALL`'s k above the count of the numbers, which Excel's
      `SMALL` refuses before truncating it (SMALL-008) — with SMALL-011, `SMALL`'s own k a little above
      the count (ticket 04's comment of 2026-10-12), and AGGREGATE-069, option 6 over a cell ExSheet
      refused, which refuses the Formula (ADR-0047, 2026-10-12, decided with the user: a refusal is
      never caught, anywhere). The engine agrees with each answer, or is corrected to it, or records a
      refusal by that decision, before this ticket is done
````

### 56. docs/specs/exsheet-functions/issues/18-indirect.md

Ticket 18: `COUNTA` over the bare name's refusal (from: this branch (point 2)).

Old:

````
bare name reads, typed and through `INDIRECT`, then wants a decision. `COUNTA` over the refusal counts
it as one Value, as it counts any Error Value (INDIRECT-056 records 1).
````

New:

````
bare name reads, typed and through `INDIRECT`, then wants a decision. `COUNTA` over the refusal counts
it as one Value, as it counts any Error Value (INDIRECT-056 records 1). *(2026-10-12, later, decided with the user: a refusal
is never caught, anywhere. `COUNTA` over it is `#VALUE!`, the Formula's refusal, and INDIRECT-056 records
that, Excel's 4 beside it.)*
````

### 57. samples/ExGrid.Docs/Pages/Sheet/SheetFormulas.razor

"Supported functions": a refusal is never caught (from: this branch (point 2)).

Old:

````
        <p>Where the engine cannot give Excel's answer, it gives an Error Value instead of a different answer:</p>
````

New:

````
        <p>
            Where the engine cannot give Excel's answer, it gives an Error Value instead of a different answer.
            Such a <code>#VALUE!</code> is a refusal, and nothing turns it into an answer: not <code>IFERROR</code>,
            <code>ISERROR</code>, <code>COUNT</code> or any other function around it, and not a Formula in another
            cell that reads it, directly, through a range, <code>INDIRECT</code>, a Spill Range or a Linked Table,
            which is refused in turn, as one that reads a circular reference is <code>#CIRC!</code>. Excel's own
            <code>#VALUE!</code>, such as <code>=1+"a"</code>'s, is caught as in Excel. A copy writes a refused
            cell as <code>#VALUE!</code>, which another program reads as Excel's Error Value. Pasted back into the
            Sheet it was copied from, a copy of one range brings its Formula; any other paste of a copy that holds
            one, of several ranges, with the headers, or into another Sheet on the page, is refused rather than
            pasted as a value. The Selection Summary shows no figure over a refused cell, and says why.
        </p>
        <p>The refusals, and the other Error Values the engine gives in place of an answer:</p>
````

### 58. samples/ExGrid.Docs/Pages/Sheet/SheetFormulas.razor

The bullet on a function given a range where it takes one Value (from: sort-and-unique 7).

Old:

````
            <li>
                <b>A function given a range where it takes one Value.</b> Excel 365 applies such a function to each
                Value and spills the results, so <code>=ROUND(B2:E2, -3)</code> gives four. ExSheet does not yet: it is
                <code>#VALUE!</code>.
            </li>
````

New:

````
            <li>
                <b>A function given a range where it takes one Value.</b> Excel 365 applies such a function to each
                Value and spills the results, so <code>=ROUND(B2:E2, -3)</code> gives four. ExSheet does not yet: the
                Formula is <code>#VALUE!</code>, and nothing around the call turns that into an answer, so
                <code>=IFERROR(ROUND(B2:E2, -3), 0)</code> is <code>#VALUE!</code> too.
            </li>
````

### 59. samples/ExGrid.Docs/Pages/Sheet/SheetFormulas.razor

The bullet on a blank among `SORT`'s keys, which becomes three (from: sort-and-unique 8).

Old:

````
            <li>
                <b>A blank among <code>SORT</code>'s or <code>SORTBY</code>'s keys, or in <code>UNIQUE</code>'s
                array,</b> is <code>#VALUE!</code>. Excel sorts it, but where it goes and what its cell shows have not
                been read yet.
            </li>
````

New:

````
            <li>
                <b>An Error Value among <code>SORT</code>'s keys</b> is sorted where it already follows every number:
                over 7 and then <code>#N/A</code>, Excel's Anchor shows 7, which fits an Error Value sorted after the
                numbers and one that keeps its place alike. With a number after it, beside text, a boolean, a blank or
                another Error Value, in a descending sort, and among <code>SORTBY</code>'s keys, the Formula is
                <code>#VALUE!</code> until Excel has been asked where it goes.
            </li>
            <li>
                <b>A blank among <code>SORT</code>'s or <code>SORTBY</code>'s keys</b> sorts after numbers, text and
                booleans, in either order, and in <code>UNIQUE</code> a blank is one of a kind only with another blank;
                it spills as 0. Excel's <code>SORT</code> was seen to sort it after numbers and text in either order
                and after a boolean ascending, its <code>SORTBY</code> after a number ascending, and its
                <code>UNIQUE</code> to count it one of a kind only with another blank, and both <code>SORT</code> and
                <code>UNIQUE</code> spilled it as 0; the rest is ExSheet's own reading until Excel has been asked.
                Beside an Error Value key the Formula is <code>#VALUE!</code>.
            </li>
            <li>
                <b>A blank <code>UNIQUE</code>, <code>SORT</code>, <code>SORTBY</code>, <code>FILTER</code> or
                <code>TRANSPOSE</code> keeps, read inside a Formula.</b> <code>COUNTA</code> counts it and
                <code>INDEX</code> takes it out as a blank. Excel was seen to count it with <code>COUNTA</code>
                in <code>SORT</code>'s and <code>UNIQUE</code>'s array, to find it blank with <code>ISBLANK</code>
                once <code>INDEX</code> took it out of <code>SORT</code>'s, and equal to <code>""</code> once taken
                out of <code>UNIQUE</code>'s; the rest, the other three included, is ExSheet's own reading until
                Excel has been asked. A function whose readings of it part, such as <code>AVERAGE</code>, <code>MIN</code> or
                <code>COUNT</code>, makes the Formula <code>#VALUE!</code>, which <code>IFERROR</code> does not catch,
                so <code>=AVERAGE(FILTER(A2:A9, B2:B9="North"))</code> over a blank is <code>#VALUE!</code>.
            </li>
````

### 60. samples/ExGrid.Docs/Pages/Sheet/SheetFormulas.razor

The `SUBTOTAL` and `AGGREGATE` bullet: text given as the array (from: kth-and-mod 7).

Old:

````
                <code>MODE.SNGL</code>, is <code>#VALUE!</code>.
````

New:

````
                <code>MODE.SNGL</code>, is <code>#VALUE!</code>. Given text or a boolean in place of its array,
                <code>AGGREGATE</code>'s <code>LARGE</code> and <code>SMALL</code> find no number, as Excel's do:
                <code>=AGGREGATE(14,6,"5",1)</code> is <code>#NUM!</code>.
````

### 61. samples/ExGrid.Docs/Pages/Sheet/SheetFormulas.razor

The bullet on `LARGE` and `SMALL`'s k, and `MOD` (from: kth-and-mod 7).

Old:

````
                <b><code>LARGE</code> and <code>SMALL</code>'s k.</b> <code>LARGE</code> rounds a k that is not whole
                up, as Excel does: <code>=LARGE(A1:B5,1.5)</code> is the 2nd largest. <code>SMALL</code>'s, and a k
                between 0 and 1, are <code>#VALUE!</code> until Excel has been asked. <b><code>MOD</code></b> is
                <code>#NUM!</code> from a quotient of 1,125,900,000,000, the limit reported of Excel 365.
````

New:

````
                <b><code>LARGE</code> and <code>SMALL</code>'s k.</b> A k that is not whole is rounded up by
                <code>LARGE</code> and truncated by <code>SMALL</code>, as Excel does: <code>=LARGE(A1:B5,1.5)</code>
                is the 2nd largest, and <code>=SMALL(A1:A9,1.5)</code> the smallest. A k below 1 or above the count
                of the numbers is <code>#NUM!</code>, whole or not: <code>=SMALL(A1:A9,9.5)</code> over nine numbers
                is <code>#NUM!</code>. <b><code>MOD</code></b>'s remainder is exact, as Excel's is:
                <code>=MOD(1,0.1)</code> is 0.09999999999999995, where
                <code>number - divisor*INT(number/divisor)</code> in doubles would give 0. Where the divisor is
                added to a remainder of the other sign, the sum is held at 64 bits before it is stored, which gives
                Excel's <code>=MOD(-0.087626,6630)</code>, 6629.9123739999995, and a sum stored as the divisor is 0,
                as Excel's <code>=MOD(-1E-20,1)</code> is. Where the ways of computing it at 64 bits that give every
                answer Excel was seen to give part, it is <code>#VALUE!</code> until Excel has been asked: a sum that
                is the divisor only once stored, as in <code>=MOD(-1E-17,1)</code>, and a sum a hair from the
                midpoint between two doubles, as in <code>=MOD(-0.084474,2270)</code>, about one input of up to five
                digits in 150,000. Holding the sum at some other widths wider than a double's, which no hardware
                holds, would give those answers too, and another last digit for about 3 inputs of up to five digits
                in 100, as in <code>=MOD(-0.024901,2350)</code>; there it answers as 64 bits do, and Excel has yet
                to be asked. It is <code>#NUM!</code> from a quotient of 1,125,900,000,000, as Excel 365's.
````

### 62. samples/ExGrid.Docs/Pages/Sheet/SheetFormulas.razor

The `INDIRECT` bullet (from: indirect 6).

Old:

````
                rewrites the References beside it, never the text. It also reads what Excel was seen to read: R1C1
                text (<code>a1</code> FALSE) naming a cell or a range, <code>R3C2</code> or <code>R2C2:R5C2</code>; a
                Linked Table's column as text, <code>Positions[PV]</code>, which waits while the table does; and a
                Spill Range, <code>A1#</code>. R1C1 text that is relative, <code>R[1]C[-1]</code>, or names whole rows
                or columns, and a Linked Table's name on its own (<code>"Positions"</code>, which Excel reads as the
                table's data), are <code>#VALUE!</code> until Excel has been asked; text that names no place, another
                Sheet's name or a table that does not exist included, is <code>#REF!</code>, as in Excel.
````

New:

````
                rewrites the References beside it, never the text. It also reads what Excel was seen to read: R1C1
                text (<code>a1</code> FALSE) naming a cell or a range, <code>R3C2</code> or <code>R2C2:R5C2</code>,
                counted from the Formula's own cell by offsets in brackets, <code>R[1]C[-1]</code>, or from its own
                row or column where R or C has no number, <code>RC[-1]</code>; naming whole rows and columns,
                <code>R3</code>, <code>C[-1]</code> or <code>R2:R3</code>; or a Spill Range, <code>R1C1#</code>.
                Other combinations of these forms are read by the same rule until Excel has been asked them. It
                reads a Linked Table's column as text, <code>Positions[PV]</code>, which waits while the table
                does, and a Spill Range, <code>A1#</code>. R1C1 text that joins a cell to a whole row, or a row to
                a column, <code>R2C2:R3</code> or <code>R3:C2</code>, names no place and is <code>#REF!</code>, as in
                Excel; so is text naming another Sheet or a table that does not exist, as Excel's A1 text is. Some
                text is <code>#VALUE!</code> until Excel has told what it names: in R1C1 style, an offset past the
                Sheet's edge, <code>R[-5]C[-1]</code> from row 2, which Excel reads as a place without saying yet
                which, and a row or column number 0, <code>R0C2</code>, or one past the Sheet's edge,
                <code>R1048577C2</code>, while A1 text past the edge, <code>XFE1</code>, is <code>#REF!</code>, as
                in Excel; and, in either style, <code>#</code> after a range, a row or a column,
                <code>A1:A2#</code>, <code>R3#</code> or <code>R2C2:R3#</code>, or inside a join,
                <code>A1#:A2</code> or <code>R1C1#:R2C1</code>. A Linked
                Table's name on its own, <code>"Positions"</code>, is
                <code>#VALUE!</code>, though Excel reads the table's data: a Linked Table is read by its columns
                and by key, never by position, so that a Formula never reads a row by where it stands. Typed in a
                Formula, the name on its own is <code>#NAME?</code>, which no Formula catches.
````

### 63. samples/ExGrid.Docs/Pages/Sheet/SheetFormulas.razor

The bullet on `PMT`, `PV` and `FV`, and `XNPV` and `NPV` (from: financial 10).

Old:

````
            <li>
                <b><code>PMT</code>, <code>PV</code> and <code>FV</code></b> give, to the last digit, every result Excel
                was seen to give for them. Which result that is depends on how Excel raises 1+rate to the number of
                periods. For <code>PMT</code> it rests on payments at rates of 0.5% to 10% a period. At much smaller
                rates Excel has not been asked, and its payment may differ from ExSheet's from the 13th significant
                digit. At a rate other than 0, a number of periods that is not a whole number, or is negative, is
                <code>#VALUE!</code> until Excel has been asked how it raises to one; so is a <code>type</code> that is
                not a whole number. Any other <code>type</code> but 0, 2 as well as 1, is payment at the start of each
                period, as Excel reads it.
            </li>
````

New:

````
            <li>
                <b><code>PV</code>, <code>FV</code> and <code>XNPV</code></b> give, to the last digit, every result Excel
                was seen to give for them, Error Values included. 1+rate is raised to the number of periods by
                squaring to a whole number, as 1 over the power to a negative one, by the square root at half a
                period, and otherwise as e<sup>n·ln(1+rate)</sup> with the exponential and the logarithm correctly
                rounded, which ExSheet computes the same on every platform. A negative 1+rate has an odd root, as
                <code>(-8)^(1/3)</code> does, and no other fractional power (<code>#NUM!</code>); at a rate of -1 no
                periods is <code>#NUM!</code> and a negative number of periods <code>#DIV/0!</code>, as Excel gave
                them. <code>PV</code> at a rate of -1 with periods is <code>#VALUE!</code> until Excel has
                been asked. <code>type</code> is read as a truth value: any number but 0, 0.5 and 2 as well as 1, is
                payment at the start of each period, as Excel reads it. <code>XNPV</code> counts the years as the days
                over 365 and sums from the first value. A rate below 0 is <code>#NUM!</code>, which is Excel's answer
                at every negative rate it was asked, -0.5 to -2, and ExSheet's own between -0.5 and 0 until Excel has
                been asked; a rate of exactly 0, or values whose last addition nearly cancels, are
                <code>#VALUE!</code> until Excel has been asked. An Error Value in the rate, the values or the dates is
                that Error Value, but <code>#VALUE!</code> where another check would give another answer (a rate below
                0 or of 0, counts that differ, a date before the first, or a different Error Value anywhere among
                them), since the order Excel checks them in has not been observed. A cell of the values or the dates
                that is not a number, whether text, a truth value or blank, is <code>#VALUE!</code> until Excel has
                been asked, whatever else is among them.
                <b><code>PMT</code></b>, where it answers, gives the exact payment, rounded once, at the end of each
                period and at the start; it reads
                <code>type</code> as <code>PV</code> and <code>FV</code> do. Excel's own payment can differ from
                ExSheet's in its last digits: of the 30 payments Excel was asked, 23 differ by one to eleven units in
                the last of a double's digits, a relative difference of at most about 1.3·10<sup>-15</sup>, which at
                two of them shows in the 15th significant digit. No evaluation found gives every payment Excel gave,
                and this is a known limitation of 0.1.0. Where the evaluations Excel's payments leave give different
                kinds of answer, or answers far apart, <code>PMT</code> is <code>#VALUE!</code> until Excel has been
                asked: at a rate of -1 or below (there a number of periods that is a fraction whose reciprocal is not
                odd is <code>#NUM!</code>), where a number it works with would pass a double's range or fall below its
                normal numbers, as in <code>=PMT(1,1020,1000)</code>, where 1+rate raised lies below
                2<sup>-52</sup>, with no periods at the start of each period, and where the evaluations would lose more
                than a few of their last digits, as where the present value grown and the future value nearly cancel
                (<code>=PMT(0.1,2,1000,-1210)</code>) or a number of periods is tiny at the start of each period.
                <b><code>NPV</code></b> is <code>#NAME?</code>, which no Formula catches, as <code>IRR</code>'s is: its last digits differ from
                Excel's, and no evaluation found gives every result Excel was seen to give.
            </li>
````

### 64. samples/ExGrid.Docs/Pages/Sheet/SheetFormulas.razor

The `TEXT` bullet (from: dates-and-text (the Docs Site)).

Old:

````
                <b><code>TEXT</code></b> reads its code in the invariant spelling and shows what a cell in that format
                would show, under the Sheet's culture. A code a cell format does not have is <code>#VALUE!</code>,
````

New:

````
                <b><code>TEXT</code></b> reads its code in the invariant spelling and shows what a cell in that format
                would show, under the Sheet's culture. <code>General</code>, and <code>@@</code> alone, fit a number to 11
                characters, as a wide column's General does, and a minus sign is not one of them:
                <code>TEXT(-1/3,"General")</code> is <code>-0.333333333</code>. A code a cell format does not have is
                <code>#VALUE!</code>,
````

### 65. samples/ExGrid.Docs/Pages/RoadmapByHand.cs

The Roadmap's sentence on Known limitations (from: financial 11).

Old:

````
        new("What the stable version does not do is said where a reader meets it: on the product's page, and under Known limitations in its Release Notes. Among them: IRR, XIRR and RATE, which it does not support, and a Reference Outline, which cannot be dragged.",
````

New:

````
        new("What the stable version does not do is said where a reader meets it: on the product's page, and under Known limitations in its Release Notes. Among them: NPV, IRR, XIRR and RATE, which it does not support, PMT's last digits, which may differ from Excel's, and a Reference Outline, which cannot be dragged.",
````

### 66. CONTEXT.md

**Refusal**: told from the **Refused Formula** (decided with the user on 2026-10-13, the term of edit 29).

Old:

````
[ADR-0035](./docs/adr/0035-paste-and-fill-respect-the-editable-declaration.md)).
_Avoid_: rejection, validation failure, error (that is a Cell State), denial
````

New:

````
[ADR-0035](./docs/adr/0035-paste-and-fill-respect-the-editable-declaration.md)).
Not a **Refused Formula**, which is a Formula ExSheet does not answer and never an operation.
_Avoid_: rejection, validation failure, error (that is a Cell State), denial
````

### 67. docs/adr/0130-the-selection-summary-is-asked-of-the-consumer-like-find.md

"There is no cap": ExSheet declines the summary over a refused cell (decided with the user on 2026-10-13; Count included, the implementer's reading, uncredited).

Old:

````
  figures. **A partial figure is never shown.** `InMemoryGridSource` never declines.
````

New:

````
  figures. **A partial figure is never shown.** `InMemoryGridSource` never declines.
  *(2026-10-13, decided with the user: ExSheet shows no numeric figure when the Selection holds a cell whose
  Formula it refused (a Refused Formula, ADR-0047), and says why.)* *(2026-10-13: it declines, Count withheld
  too, as every other figure is, since a partial figure is never shown; its reason names the cell and what it
  shows: "No figures: ExSheet refused the Formula in A3 with #VALUE! because it cannot answer it as Excel does,
  so a figure over it would not be Excel's." `SheetCommandIds.SummaryRefused` words it in another language,
  asked of a substituted Chrome first and then of `CommandLabel` (ADR-0203), `{0}` standing for the cell and
  `{1}` for what it shows.)*
````

### 68. docs/adr/0050-what-exsheet-asks-of-exgrids-core.md

Item 9: the mark a Consumer writes into the family's own clipboard HTML (this branch's implementation, ahead of an ADR; the list's header puts it to the user).

Old:

````
  put exactly that block there, and even then it pastes the Entries that showed exactly those
  Values.
````

New:

````
  put exactly that block there, and even then it pastes the Entries that showed exactly those
  Values.
- *(2026-10-13: a cell of the family's own clipboard HTML may carry a mark, `ClipboardData.MarkAttribute`
  (`data-ex-mark`), which the grid hands back beside the pasted field (`GridPasteIntent.MarkFor`) and
  gives no meaning; another program ignores it. ExSheet marks a refused cell,
  `data-ex-mark="exsheet-refused:C1"`, so that a paste of it as a Value is refused by name, wherever on
  the page it lands, rather than turn a Refused Formula into an Error Value a Formula can catch
  (ADR-0047).)*
````

### 69. docs/definition-of-done.md

SM-12's row: a refused cell leaves no figure (decided with the user on 2026-10-13; Count included, uncredited).

Old:

````
| **SM-12** | MUST | ExSheet answers from its own cells: a formula's value, every cell of a spilled array, by the same table (ADR-0130) | Layer 1 | as stated |
````

New:

````
| **SM-12** | MUST | ExSheet answers from its own cells: a formula's value, every cell of a spilled array, by the same table (ADR-0130). *(2026-10-13, decided with the user: a Selection holding a cell whose Formula ExSheet refused shows no numeric figure, and says why.)* *(2026-10-13: it is declined, no figure shown, Count included, its reason naming the cell in `SheetCommandIds.SummaryRefused`'s words.)* | Layer 1; Layer 2 (`SheetSummaryTests`) | as stated |
````

### 70. samples/ExGrid.Docs/Pages/Design.razor

The rule's bullet: a Formula ExSheet cannot answer as Excel does (decided with the user on 2026-10-13).

Old:

````
            <li>A Formula ExSheet cannot compute as Excel does is <code>#NAME?</code>, never an approximation.</li>
````

New:

````
            <li>
                A Formula ExSheet cannot compute as Excel does is <code>#VALUE!</code>, or <code>#NAME?</code> for a
                function Excel has that it does not support, never an approximation, and no Formula catches it.
            </li>
````

### 71. samples/ExGrid.Docs/Pages/Sheet/SheetFunctions.razor

The page's lead (decided with the user on 2026-10-13).

Old:

````
          Lead="Every function ExSheet's engine supports, by category, with its arguments as Excel writes them. Each gives Excel's result; a name outside this list is #NAME?, never an approximation.">
````

New:

````
          Lead="Every function ExSheet's engine supports, by category, with its arguments as Excel writes them. Each gives Excel's result; a function outside this list is #NAME?, never an approximation, and where Excel has it, no Formula catches that #NAME?.">
````

### 72. samples/ExGrid.Docs/Pages/Sheet/SheetFunctions.razor

The functions not yet answered (decided with the user on 2026-10-13).

Old:

````
                Excel functions the catalogue lists, which the engine does not answer yet: in a Formula each is
                <code>#NAME?</code> today. Each links the place on the Roadmap that holds the work on it.
````

New:

````
                Excel functions the catalogue lists, which the engine does not answer yet: in a Formula each is
                <code>#NAME?</code> today, which no Formula catches: <code>=IFERROR(NPV(0.1,A1:A3),0)</code> is not
                0, and a cell that reads such a Formula is <code>#VALUE!</code>. Each links the place on the Roadmap
                that holds the work on it.
````

### 73. samples/ExGrid.Docs/Pages/Sheet/SheetFormulas.razor

"Supported functions": a name outside the set (decided with the user on 2026-10-13).

Old:

````
            Each function gives Excel's result. A name outside this set is <code>#NAME?</code>, never an
            approximation of what the function might have meant.
````

New:

````
            Each function gives Excel's result. A name outside this set is <code>#NAME?</code>, never an
            approximation of what the function might have meant. Where Excel has the function, that
            <code>#NAME?</code> is ExSheet's refusal, which no Formula catches; a name Excel does not have either is
            Excel's own <code>#NAME?</code>.
````

### 74. samples/ExGrid.Docs/Pages/Sheet/SheetOverview.razor

"It answers as Excel does, or says it cannot" (decided with the user on 2026-10-13).

Old:

````
                <b>It answers as Excel does, or says it cannot.</b> A function outside the supported set is
                <code>#NAME?</code>, a circular reference is <code>#CIRC!</code>, and a number that does not fit its
````

New:

````
                <b>It answers as Excel does, or says it cannot.</b> A function Excel has that ExSheet does not support
                is <code>#NAME?</code>, which no Formula catches, a name Excel does not have is Excel's own
                <code>#NAME?</code>, a circular reference is <code>#CIRC!</code>, and a number that does not fit its
````

### 75. samples/ExGrid.Docs/Pages/Sheet/SheetOverview.razor

"Status-bar figures": a refused cell (decided with the user on 2026-10-13).

Old:

````
            leaves Count and Numerical Count, and no other figure, as in Excel. Average, Min, Max and Sum wear the
````

New:

````
            leaves Count and Numerical Count, and no other figure, as in Excel. A cell whose Formula ExSheet refused
            leaves no figure at all, and the line says why, naming the cell;
            <code>SheetCommandIds.SummaryRefused</code> words it in another language, asked of a substituted
            Chrome and then of <code>CommandLabel</code>, its <code>{0}</code> the cell and <code>{1}</code> what it shows. Average, Min, Max and Sum wear the
````

### 76. samples/ExGrid.Docs/Pages/Sheet/SheetLinkedTables.razor

A declared table's bare name typed (decided with the user on 2026-10-13).

Old:

````
            <li>A column the table does not have is <code>#REF!</code>; a table that is not declared is <code>#NAME?</code>.</li>
````

New:

````
            <li>
                A column the table does not have is <code>#REF!</code>; a table that is not declared is
                <code>#NAME?</code>. A declared table's name typed on its own is <code>#NAME?</code> as well, which no
                Formula catches: a Linked Table is read by its columns, never whole.
            </li>
````

### 77. samples/ExGrid.Docs/Pages/Grid/GridClipboard.razor

"Paste": the mark a Consumer writes into the family's own HTML (this branch's implementation, ADR-0050 item 9's note of 2026-10-13).

Old:

````
            arrive at full precision, and <code>Origins</code> tells you whether a field is invariant-culture text
            from Excel's own data or text as it was shown.
````

New:

````
            arrive at full precision, and <code>Origins</code> tells you whether a field is invariant-culture text
            from Excel's own data or text as it was shown. In the family's own HTML a cell may carry
            <code>ClipboardData.MarkAttribute</code>; <code>MarkFor</code> hands its value to the Consumer that
            wrote it, and another program ignores it. ExSheet marks a refused cell so that a paste into a Sheet is
            refused.
````

### 78. samples/ExGrid.Docs/Pages/Sheet/SheetFormulas.razor

"Supported functions": three refusals of 2026-10-13 — `SORT`'s fractional `sort_index`, a serial between -1 and 0 under five functions, and a refused Anchor's spill (decided with the user: the spill).

Old:

````
                <code>[&lt;=9]</code>.
            </li>
        </ul>
````

New:

````
                <code>[&lt;=9]</code>.
            </li>
            <li>
                <b><code>SORT</code>'s <code>sort_index</code></b> that is not a whole number,
                <code>=SORT(G1:H5,1.5)</code>, is <code>#VALUE!</code> until Excel has been asked whether it truncates
                it, rounds it or takes no fraction at all.
            </li>
            <li>
                <b>A serial between -1 and 0</b> under <code>DATEDIF</code>, <code>YEARFRAC</code>, <code>HOUR</code>,
                <code>MINUTE</code> and <code>SECOND</code> is <code>#VALUE!</code> until Excel has been asked: cut
                down to -1 it would be <code>#NUM!</code>, and read as a time of day <code>HOUR(-0.5)</code> would be
                12. At -1 and below they are <code>#NUM!</code>, as Excel was seen to answer at -1.
            </li>
            <li>
                <b>A refused Formula that would spill.</b> Where its text settles the shape of its array, as
                <code>=B1:B3*2</code> over a refused B2 does, every cell the array covers is refused with it, so a
                Formula that reads one is refused too; where something stands in the way, it is <code>#SPILL!</code>. Where the shape could depend on a Value, as with <code>FILTER</code>,
                <code>UNIQUE</code>, <code>SORT</code>, <code>SEQUENCE</code>, <code>LET</code> or
                <code>=LEN(A1:A3)</code>, nothing spills, and the cells its array would cover read as blank: a known
                limitation of 0.1.0.
            </li>
        </ul>
````
