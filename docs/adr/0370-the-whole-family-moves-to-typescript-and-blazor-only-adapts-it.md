# The whole family moves to TypeScript, and Blazor only adapts it

*Decided with the user, 2026-10-25. The user first set the scope: React support covers the whole
family, ExSheet, ExPivot, ExChart and ExBoard as well as ExGrid, and the products may be built in
parallel. Then the user set the end state: everything moves to TypeScript, the engines included, and
Blazor is built over it as the user put it, "as a wrapper", the way Blazor's package for ApexCharts
sits over that JavaScript library. This ADR calls that package Blazor's Framework Adapter, because the
glossary keeps "Wrapper" for a design system's package. Section 10's locale, and section 9's layout being the last step, were settled with the user
the same day, from the research note on AG Grid's locales
(https://claude.ai/artifact/9yXJqpjYpBMiBpB3JbsNhc).*

*(Rewritten the same day, before it merged. The first draft, "Every product reaches React as ExGrid
does, and asks its engine across one boundary", recommended option A below: the UIs move to TypeScript
and the engines stay C#, reached as .NET WebAssembly in a Worker or on a server. The user asked whether
moving everything was the fastest, and why a port would need Excel again when the C# engine's tests
already hold Excel's answers. It does not, which section 2 records. The user then chose B. Sections 1, 2,
3, 4, 6 and 7 changed; the products' UIs moving to Script Cores of their own, the packages' names and
the Docs Site did not. Section 3's server pivot was settled with the user last, the same day.
Section 11, and section 10's words, were added with the user after this ADR merged, and section 2's
note on the platform's math library on 2026-10-26. Section 3's live server data was settled with the user
on 2026-10-26 too: the Consumer's transport tells the source by a notice or a Change Batch. Section 9's
layout was settled with the user on 2026-10-26.)*

## Context

[ADR-0350](./0350-exgrids-core-moves-to-typescript-and-react-and-blazor-both-take-it.md) moved
ExGrid's core to TypeScript, the Script Core, and made React and Blazor both adapters over it. It kept
ExSheet, ExPivot, ExChart, ExBoard and the MudBlazor Wrappers as Blazor products, left "whether any of
them reaches React" to a later decision (its section 1), and kept the engines in C# until a measurement
said otherwise (its section 8). This is that decision, for the products and for their engines.

Measured on `main` at eaea3b2, each product splits into a UI that depends on Blazor and an engine that
does not:

| Product | UI (Razor package: C# and Razor) | Script beside it | Engine (no Razor) |
|---|---|---|---|
| ExSheet | ~12,500 (`ExSheet`) | none of its own | ~20,900 `ExSheet.Engine`, ~3,300 `ExSheet.Xlsx` |
| ExPivot | ~7,000 (`ExPivot`) | none of its own | ~15,500 `ExPivot.Engine` |
| ExChart | ~3,500 (`ExChart`) | 859 (`ex-chart.js`: pointer, 3D surface, PNG) | ~5,900 `ExChart.Engine` |
| ExBoard | ~3,500 (`ExBoard`) | none | none of its own; it draws the others |
| Data | | | ~9,900 `ExGrid.Data`, ~2,500 `.Arrow`, ~4,300 `.Xlsx` |
| Wrappers | ~5,000 (`ExSheet.MudBlazor`, `ExPivot.MudBlazor`, `ExChart.MudBlazor`), which stay Razor | none | |

Each product UI is already ExGrid's Consumer through ExGrid's public surface
([ADR-0046](./0046-exsheet-is-a-general-purpose-sheet-drawn-by-exgrid-as-its-consumer.md),
[ADR-0059](./0059-expivot-is-a-pivot-table-drawn-by-exgrid-as-its-consumer.md),
[ADR-0063](./0063-what-expivot-asks-of-exgrids-core.md),
[ADR-0250](./0250-exchart-is-a-chart-of-the-familys-data-in-three-packages-drawn-as-its-own-svg.md)).
ExSheet's and ExPivot's engines answer as Excel or not at all
([ADR-0047](./0047-the-formula-engine-is-exsheets-own-and-answers-as-excel-or-not-at-all.md),
[ADR-0060](./0060-the-pivot-engine-answers-as-excels-pivottable-and-is-the-reference.md)).

What Excel answered is already data, not only C# test code. ExSheet's expectations observed in Excel
are JSON: `tests/ExSheet.Engine.Tests/ExcelCases` (164 files, one per function area) and
`tests/ExSheet.Engine.Tests/ExcelOracle` (results recorded from Excel under en-GB, de-DE and ja-JP). The
C# engine's other facts, about 18,400 lines of ExSheet.Engine's tests and 9,000 of ExPivot.Engine's,
which has no Excel JSON of its own, are xUnit code.

The render-bench baseline of 2026-10-25 (`spikes/render-bench/results/20261025-core/`) put an ExSheet
edit that recalculates 20,000 cells at 130 ms on Blazor Server and 3.3 s on Blazor WebAssembly. About
three quarters of the 3.3 s was the baseline serving a build configured for debugging; a published
client takes about 1.0 s (section 7's record).

## Options considered

| | A. UIs in TypeScript, engines stay C# | B. UIs and engines in TypeScript, Blazor adapts them (chosen) | C. A React UI beside each Razor UI | D. Engines on a server only |
|---|---|---|---|---|
| UI | Each product's UI moves as ExGrid's did; React and Blazor adapt it | As A | A TypeScript UI for React; Blazor keeps its Razor UI | As A |
| Engines in the browser | .NET WebAssembly in a Worker | TypeScript | As A | None: a server the application runs |
| Cost | ~26,500 lines of UI and ~5,900 of chart layout; an Arrow reader; the engines' WebAssembly build | A's, less the WebAssembly build, and ~53,800 more lines of engine and data ported, re-proved against Excel's recorded answers (section 2) | A React UI per product, the Razor ones kept | A's, less the WebAssembly build |
| What it leaves | Two languages in the browser; a .NET runtime downloaded where an engine runs there | One language for everything the family does in a browser; Blazor a thin adapter | Two UIs per product, kept alike by hand | No ExSheet or local ExPivot without a .NET server |

The first draft recommended A, so as not to re-prove the engines before a measurement said
WebAssembly could not hold. The user chose B. It gives the family one implementation in one language,
no .NET runtime to download in a browser, and a Blazor package that only adapts, which is how Blazor
already takes the JavaScript libraries it uses. The cost of re-proving is smaller than the first draft
said, because Excel's answers are recorded and need no Excel to run again. C keeps two UIs per product
in step by hand. D makes a .NET backend the price of a spreadsheet.

## Decision

### 1. Everything the family does in a browser is TypeScript, and Blazor only adapts it

Each product gets a Script Core of its own: ExSheet's, ExPivot's, ExChart's and ExBoard's. It holds what
the Razor package holds today, the component and the logic beside it (ExSheet's Pointing, Format Cells,
Sheet Tabs and Find; ExPivot's Field List, Details and toolbar; ExChart's legend, tooltip, Chart
Filters and its bridge from a grid selection; ExBoard's layout, links, outputs and Board Diff), and
decides what each input means. It depends on no UI framework. Its engine is a TypeScript package of its
own (section 2).

A product's Script Core uses ExGrid's as the Razor product uses ExGrid today: ExSheet's and ExPivot's
draw through it as that grid's Consumer, ExChart's draws its own SVG and takes ExGrid only for its
bridge from a selection (ADR-0250), and ExBoard's lays out the others. Each reaches ExGrid through its
Script Core's public surface and nothing private, so what it asks of ExGrid is stated as ADR-0063
states ExPivot's, and is a member of `@ex-grid/core`'s surface. ADR-0350 sections 2 to 6 hold for it as
written: it owns the DOM inside its own root, writes the product's DOM contract and reads its
stylesheet unchanged, and its layer-3 specs run against a React host as against both Blazor hosts
until its step 3 holds, and then on the hosts section 11 gives them.

Each product has a React adapter and a Blazor adapter, both Framework Adapters as ADR-0350 section 3
defines them. The Blazor adapter is thin in the way Blazor's package over a JavaScript library is: the
component mounts the TypeScript package on the element it renders, hands it its parameters as options
and its data as data, raises the package's events as `EventCallback`s, and fills Chrome and Template
slots with Razor components. It decides nothing and computes nothing. Blazor's ExGrid, ExSheet, ExPivot,
ExChart and ExBoard become these adapters, which replaces ADR-0350 section 14's "rewritten against the
new surface" for the products: the rewrite is the move.

The scripts beside the products today (`ex-chart.js`'s pointer, 3D surface and PNG entries,
[ADR-0255](./0255-two-javascript-entries-the-charts-pointer-and-the-3d-surface.md),
[ADR-0282](./0282-a-thirteenth-javascript-entry-paints-a-chart-to-png-and-saves-its-files.md)) become
part of their product's Script Core, under ADR-0350 section 6's rules rather than ADR-0021's allowlist.

The MudBlazor Wrappers stay Razor. Each becomes a Wrapper over its product's Blazor adapter, as
`ExGrid.MudBlazor` does over ExGrid's (ADR-0350 section 9), and fills the Chrome seams with MudBlazor's
components. Each MUI Wrapper is new work beside it (section 5).

### 2. The engines move to TypeScript, re-proved against what Excel already answered

ExSheet.Engine with ExSheet.Xlsx, ExPivot.Engine, ExChart.Engine, ExGrid.Data with ExGrid.Data.Xlsx,
and the Snapshot's Arrow form move to TypeScript; ExGrid.Data and its Arrow form also stay in C# for a
.NET server (section 3). ADR-0350 section 8 is replaced: its "decided on a
recorded measurement" is now the check of section 7, not a gate on the port. ADR-0047 and ADR-0060 hold
as written: the TypeScript engine answers as Excel or not at all, and where it cannot answer exactly it
refuses rather than approximates. Until a C# engine is removed it is the oracle its TypeScript engine is proved
against; from then the TypeScript pivot engine is the reference ADR-0060 names, and the TypeScript
formula engine is the engine ADR-0047 describes.

A TypeScript engine is proved in three ways, and none of them runs Excel again:

1. **Excel's recorded answers run as they are.** `ExcelCases` and `ExcelOracle` are read by the
   TypeScript engine's runner as they are read by xUnit's, in every locale recorded.
2. **Every other fact becomes a shared case first**, green on the C# engine, as ADR-0350 section 5 does
   for ExGrid: recalculation order, references, names, errors, the pivot's layouts and its live folds.
   A case transcribed is deleted from xUnit's code; the data stays.
3. **A differential run against the C# engine reaches what the cases do not.** Random workbooks,
   formulas, Snapshots and Change Batches are answered by both engines, and every difference is a
   defect in one of them. It runs where the two languages differ most. A number is computed in the
   type the C# engine computes it in: ExSheet's are doubles (ADR-0047), and ExPivot's Sum of a decimal
   column is exact, falling back to a double where C#'s does (ADR-0060). JavaScript has no decimal, so
   the TypeScript engine computes the exact ones by counting decimal places, as a Summary's Sum does
   (ADR-0350 section 16), never by adding doubles; and it computes the doubles in doubles, never more
   exactly than Excel does. The run also reaches `Math` functions near a rounding boundary and number
   to text.

   *(2026-10-26, decided with the user, when the run's first seed found about 250 differences, nearly
   all the last bit of a math function: **neither engine calls the platform's math library for `EXP`,
   `LN`, `LOG`, `LOG10`, `POWER` and `^`, `ATAN`, `ASIN`, `ACOS`, `ATAN2` and the hyperbolic functions.**
   glibc's, the Windows runtime's and V8's each round some arguments differently, and so would Safari's
   and Firefox's. Both engines compute e^x, ln x, log10 x, atan, atan2, asin, acos, sinh, cosh, tanh,
   asinh, acosh and atanh as the exact value rounded once to the nearest double, ties to even, in `BigReal` and
   `PortableMath` (`big-real.ts`, `portable-math.ts`) by Ziv's strategy from 128 bits, doubling up to
   4096, while e^x and ln x keep `CorrectlyRounded`'s double-double path; `LOG` to another base, `ACOT`,
   `ACOTH`, `COTH`, `CSCH` and `SECH` are IEEE arithmetic on those. A whole power with |b| at most 64 is
   computed exactly and rounded once; a power whose exponent is not whole is still exp(b·ln a), each step
   correctly rounded, except a square root. The platform's `log2` is read only to tell that a whole power
   lies far past the doubles' range, where its last bit cannot move the answer. Both engines are held to `tests/cases/sheet/portable-math.json`, 779 answers from
   mpmath at 400 bits, and the run is the gate: CI runs it at seed 20261026, and a last bit apart fails
   it. ADR-0047's "The platform's math library is not used" records the decision and its reasons.)*

An engine is removed only once it is held: its C# facts are shared cases green in TypeScript, and,
where it has a differential run, that run has found no difference over a run recorded in the pull
request that removes it. ExChart's layout and the `.xlsx` readers and writers have no differential run
and are held by their cases alone.

Under Blazor Server this moves the engine from the server to the browser. A Sheet Document and a
pivot's Snapshot cross the circuit to the browser, and an edit or a field moved no longer does. What
crosses is measured on the Server host before a product's Blazor step is called done.

### 3. What stays in C# is a .NET server's side

Nothing the family draws stays in C#. What stays is what a .NET server uses to feed a browser:

- **ExGrid.Data and ExGrid.Data.Arrow**, with which a .NET server makes a Snapshot and sends it
  ([ADR-0065](./0065-a-snapshot-travels-as-apache-arrow.md)). `@ex-grid/data` runs the same shared
  cases, and both runners stay, as they do today.
- **The readers of the Chart Spec and the Board Spec**
  ([ADR-0320](./0320-an-ai-draws-a-chart-by-writing-a-chart-spec-that-names-data-it-never-holds.md),
  [ADR-0340](./0340-an-ai-composes-a-board-once-and-a-workflow-reuses-it-without-ai.md)), with which a
  .NET host answers an AI's tool. ExChart.Engine keeps the Chart Definition and the Chart Spec's
  reading when its layout leaves; the Board Spec's reading leaves the ExBoard adapter for a package a
  server takes without Blazor, `ExBoard.Spec`. Each TypeScript reader runs the same cases, refusals and
  their paths included.
- **The Leaf Aggregate contract**, with which a .NET server answers a server pivot (below). The
  contract's types and the pass that answers it over records stay in a small C# package,
  `ExPivot.Server`, held to the TypeScript engine's local pass by shared cases; Excel's rules for the
  report do not.

#### The server pivot answers Leaf Aggregates, and the TypeScript engine lays out the report

*(Decided with the user, 2026-10-25, after comparing AG Grid's server-side row model, where the
application's server returns every group row's and pivot cell's figure and the grid shows it unchecked.
Three other ways were put to the user: ExPivot.Engine kept in C# as the server's engine, which keeps two
implementations of Excel's rules for good; the TypeScript engine on a JavaScript server, which asks an
ASP.NET backend to run one beside it; and no server pivot.)*

[ADR-0066](./0066-expivot-asks-a-pivot-source-and-a-server-answers-with-leaf-aggregates.md)'s boundary
returns for server data, and
[ADR-0151](./0151-server-pivots-send-report-windows-and-share-the-local-engine.md)'s server report
Windows end with ExPivot.Engine at step 3. The server answers one `GROUP BY` at the leaves, the parts
each Value Field asks for, in any language and against any database; the TypeScript engine computes
every cell, subtotal and grand total from them and lays out, sorts, collapses and applies Show Values
As in the browser. So Excel's rules for a report have one implementation, a total is computed from its
records and never from the totals below it (ADR-0060), and a parent and its children cannot disagree,
which is the error a server that returns each level's figures can make without anyone seeing it. Past
the cap, Level Aggregates (below) give that up for size, and are checked where Leaf Aggregates need no
check.
ADR-0151 chose report Windows for WebAssembly's first transfer and retained memory; both are measured
again with the TypeScript engine (section 7) before ExPivot.Engine is removed.

What a server cannot answer from leaves is refused, as ADR-0066 has it: a source declares the
Aggregations it answers, and Value Field Settings offers the others disabled with the reason.

**A layout over the leaf cap is refused by name, and the user is told as early as it can be known.**
This is the first build, and stays for a source that does not answer Level Aggregates (below).
ADR-0066's cap stands (`MaxLeaves`, 200,000 by default, the Consumer's to change; re-measured on the
TypeScript engine before it is settled again). The leaves grow with the product of the placed fields'
Items, not with the records, so the refusal names the fields that make it:

- **Before a field is placed.** A server's source may declare that it answers each field's count of
  Items, under the Source Version it counted; one that does is asked once when it is opened, and again
  under a new Source Version, and the Field List shows the count beside the field's name. Counting is
  a cost a source chooses: one that does not declare it gets no mark and no count, only the refusal
  at the drop.
- **While a field is dragged.** The product of the counts, less the Items the layout hides, is an
  upper bound the browser knows at once. A drop target where it passes the cap is marked while the
  field is over it. The bound is a hint for the mark only, never a refusal, so a count that has gone
  stale moves the mark, not the answer.
- **When the drop is made.** Where the bound is under the cap, the question is asked as it is today.
  Where it passes the cap, or no bound is known, the question is asked with `MaxLeaves`, and the
  source answers it or stops at the leaf that passes the cap, as ADR-0066 has it: no second query and
  no second round trip. A source that stops refuses, the move is refused in the Field List, and the
  report stays on the layout it had. The refusal says only what is known: the field, its count of
  Items where the source gave one, and the cap, for example "Customer (1,020,000 items) in Rows makes
  more than 200,000 combinations, the most server data allows", and the ways that remain: Customer in
  Filters, or Customer filtered first.
- **A move made while a question is out** is taken against the layout that question asks for. If that
  question is refused, every move taken against it is refused with it, named, and the Field List
  returns to the last layout answered (spine rule 6). Under Defer Layout Update the question is asked
  at Update, and the refusal is said there.

#### Over the cap, the engine asks level by level and checks every level it is given

*(Decided with the user, 2026-10-25, as the target the cap's refusal grows into. The first build is the
refusal above; this is built after it, on the same contract.)*

Leaf Aggregates fail at size by their count, not by their correctness: the records may be any number,
but the leaves cross the wire. So a source may declare that it answers **Level Aggregates**, and for one
that does, a layout past `MaxLeaves` is not refused:

- **The source's answer decides, per question, in one round trip.** A question that may pass the cap
  (the bound of the counts passes it, or no bound is known) is asked with `MaxLeaves` and with the
  groups the report would show. The source answers with the leaves when they fit, and with those
  groups' Level Aggregates when they do not. Every new question is asked the same way, so a filter that
  brings the leaves under the cap brings Leaf Aggregates back. While a field is dragged, the mark
  over a target past the bound says the report will be answered level by level, not that the drop is
  refused.
- **The engine names what it shows.** It asks for the groups it will draw: the grand total, each
  collapsed group as one row, and the children of each group the user expands, crossed with the column
  groups the same way. Every question names the whole screen, so the parent of every group shown is in
  the same answer as its siblings. The server answers in one query (`GROUPING SETS` in SQL), under one
  Source Version, with ADR-0066's parts: the count of values, the count of numbers, the sum, the
  extremes, the product and the running variance, and for a double's sum also its sum of absolute
  values. A Value Field whose Aggregation does not compose from parts (a distinct count, a median) is
  offered disabled with the reason, as ADR-0066 has it.

  *(The question's and the answer's shape, written on 2026-10-25 as the contract is built.)* The groups
  shown are said as the layout says them, so a server can turn them into `GROUPING SETS` without knowing
  the report: the question carries, for each row and each column field, whether its Items are collapsed
  and the Items toggled from that (`PivotLevels`, one rule a field). A group is shown when every Item
  above it is expanded, and the grand total always is; a cell is a shown row group crossed with a shown
  column group, and the answer holds every such cell some record reaches, each named by its Items down
  to its depth and nothing below. A source that declares Level Aggregates answers with the leaves when
  they fit `MaxLeaves`, and otherwise with these cells, at most `MaxLeaves` of them, or refuses. The
  answer says which it is; a level answer to a question that asked for none, or a cell the rules do not
  show, or a shown cell without its parent, is refused as another question's answer (ADR-0066). The
  records of a cell are checked like its parts: its children's add up to it, exactly.

  *(Built on 2026-10-25 in `@ex-grid/pivot-engine`.)* A report source asks every question of a source
  that declares Level Aggregates with the levels, not only one whose bound passes the cap: the source
  answers with the leaves whenever they fit, so the bound would decide nothing the answer does not, and
  it stays the drag hint above. A cube made from leaves holds every collapse of its layout; one made from
  Level Aggregates holds only the groups it was asked for: a layout that collapses some of them is laid
  out from it, and one that shows a group below them is asked again.
- **Excel's rules stay in the engine.** Averages, variances, Show Values As, subtotals and the layout
  are computed from the parts in the browser, by the same code as Leaf Aggregates.
- **Every group shown is checked against its parent.** The engine composes each sibling set's parts and
  compares every part with the parent's: counts, extremes and exact decimal sums (ADR-0060) exactly,
  and a double part (a sum, a product, a running variance) within the rounding its composition can
  make. A sum's bound comes from its sum of absolute values; the bound for the product and the running
  variance is fixed with shared cases before Level Aggregates are built. A part that is not finite is
  `#NUM!` in the parent and its children alike, as ADR-0060 has it, and a check whose bound is not
  finite refuses. A mismatch is refused naming the group, as "the server's answer for Region = EU does
  not add up", and no figure from that answer is shown (spine rule 1). What the engine cannot check is
  a collapsed group against children it does not show, and that each level's `GROUP BY` is the right
  question; the second is the premise of Leaf Aggregates too.

  *(The bounds, fixed on 2026-10-25 by `test/level-check.test.ts` in `@ex-grid/pivot-engine`, before the
  rest is built, as this item asked.)* With u = 2⁻⁵³ and γ = k·u / (1 − k·u), k the parent's numbers plus
  the children composed: any order of summing is within γ·Σ|x| of the true sum and any order of
  multiplying within γ·|Π x|, and each side may be that far off. So a double sum agrees within 2γ·Σ|x|,
  Σ|x| being the parent's absolute sum, which must not be below |sum|, and the children's absolute sums,
  where every child carries one, within 2γ of the parent's; a product within 2γ of the larger, two
  products both below the normal range agreeing, since their rounding has no relative bound and both show
  0. A running variance is bounded through S = m2 + n·mean², the parent's Σx²: the mean within 4γ·√S and m2
  within 8γ·S, which admit both Welford's merge and the m2 = Σx² − (Σx)²/n a SQL server computes. The
  test makes honest answers both ways, each group in an order of its own, over cancellation, magnitudes
  eighteen orders apart and a 10⁹ offset under a unit spread; the largest error it met was 8% of its bound,
  and a part moved by 10⁻⁹ of its scale is refused.
- **One answer is one Source Version.** An answer under a newer Source Version than the report shows
  replaces the whole screen, because it names the whole screen; it is never refused for its version,
  and the check never spans two answers. So an expand on a live source shows the newer data with the
  children, whenever it arrives (spine rule 6).
- **What crosses the wire is what is shown.** The transfer grows with the rows on screen, not with the
  leaves: a million customers under a collapsed region are one row. Each expand and each sort by value
  is a round trip, which is the price of Level Aggregates, and why they are answered only past the cap.
- **A sibling set too large to fetch whole is answered as a window with what lies on either side.**
  *(Decided with the user, 2026-10-25, over a cap on a group's children and over trusting the server's
  window functions unchecked.)* Where a group's children pass what the screen can hold (a million
  customers under one region, expanded), the server answers the children in the window on screen, in
  the order the engine sorts siblings (the sort's key, then the Items' order), with two more sets of
  parts: those of the siblings before the window and those after it, each with its count of siblings
  and the key of its nearest sibling to the window. The engine checks that before, window and after
  compose to the parent, part by part, as above; that the nearest key before the window does not sort
  after the window's first sibling, and the nearest key after it does not sort before its last; and
  that the counts give the window's place. A running total then starts from the parts before the
  window, a rank is the count before the window plus the place within it, and a Top 10 is a window
  whose after-set's nearest key does not sort ahead of its tenth. Any mismatch is refused naming the
  group, as above. What the engine cannot check is that the nearest keys the server states are the
  nearest; that is the `GROUP BY` premise again, held by the same shared cases as the rest of
  `ExPivot.Server`'s pass.

  *(Narrowed with the user on 2026-10-26: windows by value first.)* A window is answered only for a
  sibling set ordered by a value. The engine's order of Items by label is its culture's collation over
  the labels it formats, and a Consumer's Order Key in the browser; a server cannot reproduce it, and a
  server sorting otherwise would put siblings before the window that the engine would not. The order
  would hold at every edge the screen shows, but a running total started from those parts would be
  wrong with nothing to show it. A value the engine and a server compute from the same parts, so a set
  ordered by one is checkable at every edge. A set ordered by label past the cap stays refused by name,
  as above. Windows by label can be added once a server can be held to the engine's order.

  *(The question's and the answer's shape, written on 2026-10-26 as the contract is built.)*

  - **The question.** A row field's rule may say how its Items are ordered: by a field in Values, under
    an Aggregation, ascending or descending, read at the grand total of the columns as the report sorts
    them (`PivotLevelOrder`). A column field's rule says none. The levels may carry a window
    (`PivotLevelWindow`), with two parts:
    - its `Limit`: the most siblings a set ordered by a value is answered with whole;
    - for each parent, where its window starts among the siblings.

    A set with more siblings than the limit is answered as a window starting there: at the first sibling
    when the question names none, and no further than the limit from the end.
  - **The siblings answered.** The window's siblings are those from its start, up to the limit, and
    every sibling tied with its first or its last. Siblings whose value is not a number are tied with
    one another, and they follow every number in either direction. Beside the window, the server answers
    the nearest sibling on each side: the last before it and the first after it. These margins are
    answered as any shown group is, with the groups below them, and are not shown. They are what the
    engine checks a tie at the edge against.
  - **What lies on either side.** The answer says, for each windowed parent, how many siblings come
    before the first one shown and after the last (`PivotAnswerWindow`); a margin counts among them. The
    siblings beyond each margin are answered as rest cells: their parts merged, one rest cell for each
    shown column group they have records under. A rest cell is named by its parent's Items and says its
    window and its side. Its siblings have no cells of their own.
  - **The checks.** Before laying a window out, the engine checks five things, and refuses the answer
    by name at the first that fails:
    - before, margins, window and after compose to the parent, for every column group, as any sibling
      set does;
    - each margin sorts strictly before or after its edge of the window, so no tie straddles an edge;
    - the counts put the window where it was asked;
    - a set with more siblings than the limit came as a window;
    - a set with no more than the limit came whole.
  - **The bundled sources** answer a window from the cells they merge, ordering each set by the value
    and, among ties, by the order the leaves first reached them. The rest cells' parts are merged in
    that order, so the C# and the TypeScript answers are the same, bit for bit.

  *(Decided with the user on 2026-10-26: every sibling has its row, and scrolling to one asks again.)*
  A windowed set is laid out with a row for each of its siblings, so the report's row count, and the
  scrollbar drawn from it, are the whole set's. A sibling outside the window, a margin or one merged
  into a rest cell, has a pending row: no label and no value, never painted. The report source never
  answers a report Window with a pending row. When a Window it is asked for reaches pending rows, it
  asks the question again with each such set's window started so that it holds them, and answers once
  that answer is checked. A Copy, a Selection Summary or the records behind a cell that reach a pending
  row are refused by name. The rest follows from that:
  - **Which sets are windowed.** A set is windowed only where its siblings show no groups below them:
    its field is the innermost in Rows, or its Items are collapsed and none is toggled. Each sibling's
    rows are then known without its answer, and the row count is exact. The question carries the order
    only for such a field, and only when the report sorts it by a value shown as it is computed: a
    percentage of a total reorders, or ties, what the server sorts. An expanded set too large to
    answer whole stays refused by name past the cap.
  - **When windows are asked.** A question is asked whole first. Only once the source refuses it past
    the cap is it asked again with windows, and from then on for that question, so a report that fits
    is never windowed and its Copy reaches every row.
  - **The limit** is 1,000 siblings, or the rows of the largest Window asked for, where that is more.
    A Window of rows from one set is then always held by one answer.
  - **Where a window starts.** Each set a Window reaches pending is started so that the siblings the
    Window's rows stand for lie in the middle of it, no further than the limit from the end; the other
    sets keep theirs. A new layout starts every window again at its first
    sibling. Under the same data a sibling stands at the same place whichever window was answered, so
    moving a window keeps the rows' sequence, and the selection with it (ADR-0011).
  - **Rejected: the window's siblings alone**, with a row on each side saying how many more and asking
    for them when pressed. It would window expanded sets too, but it scrolls unlike any other report
    and puts a control of the grid's own among the report's rows.

**Live server data: the Consumer's transport tells the source, by a notice or a Change Batch** *(decided
with the user, 2026-10-26)*. The user asked whether how the server's data is sent depends on the server's
application, and how this differs from AG Grid, and took the recommendation. ExGrid opens no connection,
as ADR-0066 has it: `PivotSource.Fetch`'s fetchers take a `subscribe` callback, which the source calls when
its first listener subscribes, and whose returned function it calls when its last one leaves. The
Consumer's transport — WebSocket, server-sent events, SignalR, polling — calls the `notify` it was handed
with one of two things:

- **A notice**, `changed`, with the new Source Version where the Consumer knows it: the data moved on, and
  the question is asked again — ADR-0066's `Changed`, and AG Grid's `refreshServerSide`. The re-ask near
  the cap is measured against ADR-0151's record before ExPivot's step 1 is called done.
- **A Change Batch**, `changeBatch`: the records the data lost and the records it gained between two
  Source Versions, `from` and `to`, each with its values in the source's field order. A changed record is
  the old one lost and the new one gained, as a Snapshot's batch's `removed` and `added` are. The engine
  reads them as the bundled source reads records, into Leaf Aggregates of their own under the question it
  holds, and folds them into the answer it holds: held, less what was lost, plus what was gained, leaf by
  leaf — AG Grid's `applyServerSideTransaction`, with the totals made in the browser.

What a fold keeps, and when the question is asked again instead:

- **A part is folded only where subtraction and addition keep it as the server answers it**: the counts,
  an exact sum while it stays within the 128 bits an exact sum is kept in, and the extremes where the batch
  brings numbers to a leaf and takes none from it. A batch that touches a sum that is a double, a product, a
  variance, an extreme it takes a number from, or a non-finite number is not folded: the question is asked
  again, whole. ADR-0067's bundled source recomputes such a leaf from its records; a server's source holds
  none.
- **A batch is folded only into the answer held under its `from`**, and batches only in a chain from it.
  One that starts elsewhere — a batch missed, or one older than the answer — lets the answer go, and the
  question is asked again. A leaf a batch would leave with fewer than no records, or more numbers than
  values, does not match the answer, and is asked again too; so is a fold that passes the cap, which the
  server then refuses or answers with levels.
- **Level Aggregates are not folded.** A Sibling Window's places depend on the order of its whole set, and
  a cell above the leaves on records the batch cannot place: under them, live data re-asks what is shown,
  checked as above.
- **Batches are gathered, and folded when the question is next asked**, so batches that arrive faster than
  the report redraws cost one fold (ADR-0067's 250 ms). An answer fetched while a notice or a batch arrived
  is shown, and not held, so the next question goes to the server. An answer is held only while the
  transport runs: with no listener, nothing would say it went stale. Refresh lets go of what is held and asks
  the server, through the source's `refresh`, which a source wrapping another passes on.

This is not ADR-0066's refused answer naming the leaves that changed. There the server named the leaves and
their new parts, and a server that named too few painted totals that did not add up, with nothing to say
so. Here the server names records, as a Snapshot's Change Batch does, and the engine finds their leaves by
the binding that answers the question, so a leaf cannot be named wrongly, and what it cannot keep exactly it
asks again. The server stays trusted for what only it knows — that the records it sends are all that
changed — as a Snapshot's Change Batch is trusted to be the batch.

- **Rejected: notices alone** (AG Grid's `refreshServerSide` and nothing else). Every change asks the whole
  question again, up to `MaxLeaves` leaves from the server; the user wants totals made in the browser.
- **Rejected: the server sends the changed leaves' parts.** ADR-0066's refusal stands.
- **Rejected: ExGrid owns the transport**, a WebSocket client of its own. Authentication, retries and the
  protocol are the Consumer's, as every fetcher's are (ADR-0066).

*(Until 2026-10-26 this paragraph read "Live server data re-asks what is shown": under a new Source Version
the engine asked the question again — the whole question under Leaf Aggregates, the screen under Level
Aggregates — and how the Consumer told the source was left open. Re-asking stands for a notice, for Level
Aggregates and for every batch the engine cannot fold.)*

### 4. Where an engine runs

A Script Core asks its engine through an **Engine Host**: the page's own thread or a Web Worker. The
protocol is asynchronous messages whose answers are data (a Window of Values, a report Window, a
Snapshot as Arrow), so the engine and its caller do not change with the host. A server is not an
Engine Host: it answers an engine's questions about server data (a pivot's Leaf Aggregates, section
3) and lays out nothing.

- **The page's thread or a Worker, by measurement.** The protocol is asynchronous and versioned on
  both, so moving an engine between them changes no caller (the record's pages did not change when
  its engine moved). An engine runs on the page's thread unless its port's record measures an
  operation past a frame there; then it runs in a Worker, so the grid keeps its frame while the engine
  works.
- **An answer carries what it was asked against.** Each request names the version of the document or
  the Snapshot it was taken on, and the answer names it back. Nothing waits on a clock.
- **Writes are never refused for being late, and reads coalesce.** An edit, a Change Batch or a field
  moved is a write: the host takes every one in the order it was made. A recalculation or a report in
  flight is not restarted; when it ends, the next one runs for the newest version.
- **An older answer is shown only as older.** An answer for an older version than the one shown is
  drawn under the product's calculating mark, as Excel's status bar says "Calculating" over the values
  it has, and never as the current one; the mark leaves when the shown version is answered. So a
  stream of edits faster than the engine is answered at the engine's pace rather than not at all, and
  no figure is passed off as current. A gesture taken against an older answer is refused if that
  answer no longer holds (spine rule 6). The mark is the product's ticket's to design.
- **The Consumer owns the server's transport and authentication**, as ADR-0066 says.

### 5. The packages

Each NuGet package that holds what moves has an npm counterpart of the same name under ADR-0350
section 9's scope, and each package's direction is enforced by its dependencies as NuGet's are by
project references. The engines touch no DOM, so they run in a Worker as on the page.

| npm | Holds | NuGet it moves from |
|---|---|---|
| `@ex-grid/data`, `@ex-grid/data-xlsx` | The Snapshot, Change Batches, CSV and Arrow; the `.xlsx` reader | `ExGrid.Data`, `.Arrow`, `.Xlsx` |
| `@ex-grid/sheet`, `-react`, `-mui` | ExSheet's Script Core, its React adapter, its MUI Wrapper | `ExSheet`, `ExSheet.MudBlazor` |
| `@ex-grid/sheet-engine`, `@ex-grid/sheet-xlsx` | The formula engine; opening and saving `.xlsx` | `ExSheet.Engine`, `ExSheet.Xlsx` |
| `@ex-grid/pivot`, `-react`, `-mui` | ExPivot's | `ExPivot`, `ExPivot.MudBlazor` |
| `@ex-grid/pivot-engine` | The pivot engine | `ExPivot.Engine` |
| `@ex-grid/chart`, `-react`, `-mui` | ExChart's, its layout included | `ExChart`, `ExChart.Engine`, `ExChart.MudBlazor` |
| `@ex-grid/board`, `-react` | ExBoard's | `ExBoard` |

Source lives at `src/npm/<name>` until section 9's move. A Blazor adapter carries its product's built
script as a shipped asset of its own (ADR-0350 section 9, ADR-0123), so a Blazor Consumer still installs
nothing from npm. Each MUI Wrapper is held to ADR-0030 as `@ex-grid/mui` is.

### 6. The order inside 0.3.0

ADR-0350 section 7's three steps hold for ExGrid as written, and each product takes the same three,
with a fourth for the repository:

1. **React.** The product's Script Core, its engine and its React adapter pass its layer-3 specs on the
   React host, in both browsers, and its engine's cases of section 2 are green. A product's UI starts
   once ExGrid's Script Core has what it asks of ExGrid. Its engine waits on nothing and may start at
   once, beginning with the transcription of its C# facts into shared cases.
2. **Blazor.** The product's Blazor adapter replaces its Razor component once its step 1 holds and
   ExGrid's Blazor adapter stands. Its specs pass on both Blazor hosts, and so do those of every product
   it draws or is drawn by. ExGrid's step 2 waits only on ExGrid's step 1, as ADR-0350 has it.
3. **Removal.** The C# that moved goes: ExGrid's core as ADR-0350 has it, each product's Razor logic,
   and each engine once its differential run is clean (section 2), except what section 3 keeps.
   ADR-0021 then governs no script and is superseded; what it protected is held by ADR-0350 section 6
   for every Script Core.
4. **The repository's layout** (section 9), once every product's step 3 holds.

0.3.0 is not tagged until every step holds (ADR-0350 section 7), and until the React package of every
product, all five, runs stably *(decided with the user, 2026-10-25: "0.3.0 ships once the React version
of every product works stably")*: its layer 3 green on CI with no red open against it that passed on a
re-run (ADR-0220). No product ships as 0.3.0 alone ahead of the others. Prereleases publish from tags meanwhile
([ADR-0042](./0042-prereleases-ship-before-sign-off-and-only-a-stable-version-waits-for-it.md)), so a
product reaches React developers as a prerelease when its step 1 holds.

### 7. Each engine is measured beside the C# record

The Engine Host record of 2026-10-25 (`spikes/engine-host/results/20261025-engine-host/`, one container,
one run) measured the C# engines and a TypeScript spike of only what `/core/sheet` and `/core/pivot`
exercise, answering the same Values as the C# engine (no mismatch over 1.0 million cells and 260,000
report values, decimal cents included):

| | Published Blazor WebAssembly | .NET in a Worker, interpreted | .NET in a Worker, AOT | TypeScript in a Worker | TypeScript on the page |
|---|---:|---:|---:|---:|---:|
| Edit A1, 20,000 recalculated | 1,018 ms | 1,045 ms | 556 ms | 11 ms | 15 ms |
| Pivot columns swapped | 140 ms | 120 ms | 37 ms | 16 ms | 18 ms |
| Downloaded | 4.01 MB | 2.74 MB | 5.67 MB | 58 KB | 58 KB |
| First Sheet cell | 5.0 s | 5.0 s | 2.0 s | 0.19 s | 0.19 s |

The spike computes Values only, so its time is a floor, not the cost of a port. A port carries the full engine's
bookkeeping, so the better guide to its time is the C# engine under the .NET JIT (162 ms for the same
edit, PR #283's record): from it a port is estimated at three to six times faster than .NET WebAssembly
on recalculation and one to four times on the pivot, with the start time and the download the gains
that are sure. That is an estimate, not a measurement, and the user chose the move on it (2026-10-25). That record is the line
the TypeScript engines are held to. Before a product's step 1 is called done, render-bench runs its
engine in the same scenarios, on the same machine as a fresh C# run: the time to the screen, the bytes
downloaded, the time to the first answer, and the time on the page's thread, which settles section 4's
Worker rule. On the page's thread the spike blocked for 8 ms on the edit and 14 ms on the swap, inside a
frame; a port at ten times that cost would not be, so the rule is the record's per engine, not assumed.
A scenario where TypeScript is slower than the C# record is said in the record and the Change Notes, and
fixed or accepted with the user (ADR-0350 section 15). Performance still never gates CI.

### 8. The Docs Site shows every product under both frameworks

ADR-0350 section 12's framework switch reaches every product's pages: each Example exists in both
frameworks, and a page whose feature is not in the React packages yet says so where its Example would
be, as every page outside ExGrid says today.

### 9. The repository is laid out by platform once the move is done

*(Settled with the user, 2026-10-26, over two other layouts below. The first version of this section
named only `packages/`, `dotnet/` and `tests/cases/`, as a recommendation; the user asked for the layout
to be thought through, and then that the .NET-specific files, `ExGrid.slnx` among them, live under
`dotnet/` as well.)*

The user asked whether `src/npm` is a good name. It names the registry, not what it holds: a fair name
while TypeScript was a corner of the repository, and a misleading one once TypeScript is where the
family is implemented. After step 3, the repository is laid out by where a thing ships, and what judges
both platforms sits between them:

```
package.json, package-lock.json, tsconfig.base.json   the npm workspace's root
packages/<name>   what npm publishes, and nothing else: core, react, mui, data, sheet, sheet-engine, …,
                  each with its own Vitest layers 1 and 2
dotnet/           everything .NET, and the place every dotnet command runs from:
  ExGrid.slnx, global.json, dotnet-tools.json, Directory.Build.props
  src/            what NuGet publishes: the Blazor adapters, the MudBlazor Wrappers, section 3's
                  server packages
  tests/          their xUnit and bUnit projects, and the package smoke check
  samples/        the Blazor DemoHosts, the DemoApi, the Docs Site and the ExChart Showcases
  tools/          the release tool
tests/            what judges both platforms: cases/ (the shared cases), browser/ (layer 3, today's
                  ExGrid.Browser), the glyph width records
samples/          the React DemoHost
tools/            the workspace's lint and build scripts, the asset minifier, the README media
```

- **The repository's root is the npm workspace.** Today six folders each carry a lockfile of their own
  (`src/npm`, the React DemoHost, layer 3, two tools and a spike), and the React DemoHost reaches the
  packages through an alias per package written by hand. One workspace at the root takes the hosts, layer
  3 and the tools as members and resolves the packages by name; a spike stays outside it. Packages that
  are not published, the lint and the build scripts, leave `packages/` for `tools/`, as MUI keeps
  `packages-internal/` apart, so that `packages/` holds exactly what npm publishes.
- **`dotnet/` holds every C# project and the files that configure them.** Half a move breaks quietly:
  MSBuild looks for `Directory.Build.props` upward from each project, so a C# project left outside
  `dotnet/` would lose `IsPackable=false` and `TreatWarningsAsErrors`, and the SDK and the tool
  manifest look for `global.json` and `dotnet-tools.json` upward from the directory a command runs in,
  so a command run from the root would take whichever SDK is installed. Every dotnet command therefore
  runs in `dotnet/`: CI's working directory, the path `.github/actions/dotnet` reads `global.json` from,
  the directory layer 3 starts the Blazor hosts in, and the commands in `CLAUDE.md`. Today's
  `src/Directory.Build.props`, which turns packing back on, moves to `dotnet/src/`, so only `dotnet/src`
  packs (ADR-0042), as only `packages/` publishes.
- **`tests/` holds only what judges both platforms.** The shared cases, layer 3, which runs the same
  catalogue on every host, and the glyph width records that both cores are held to. A test of one
  platform's code lives beside it: Vitest in its package, xUnit and bUnit in `dotnet/tests`. Layer 3's
  folder is named for what it judges, `browser/`, not after a .NET project.
- **The hosts split by platform.** The Blazor hosts and the Docs Site, a Blazor WebAssembly app
  (ADR-0350 section 12), are C# and live in `dotnet/samples`; the React DemoHost stays in `samples/`.
  Layer 3 already names each host by its path, so the split costs only that the hosts are not listed in
  one folder.
- **The names.** `packages/` is the name JavaScript monorepos give their workspace (AG Grid's, MUI's,
  React's and TanStack's among them), and keeping in it only what is published keeps the name true.
  NuGet's packages are also packages, so `release.yml`'s directory of `.nupkg` files, named `packages`
  today, is renamed `nupkg` in the same move. `dotnet/` is named for the platform rather than for
  Blazor, because it also holds section 3's server packages, which are not Blazor; Semantic Kernel lays
  its .NET side out the same way, with its solution and `global.json` inside `dotnet/`.

Two other layouts were put to the user. **`src/ts/` and `src/dotnet/`** was the smallest change and kept
"only `src/` ships" as it reads, but left the six lockfiles, `src` twice in every package's path, and
`tests/` mixing one platform's tests with the shared ones. **A folder per product** (`grid/`, `sheet/`,
`pivot/`, …, each with its TypeScript packages and its Blazor adapter), the way ASP.NET Core keeps
SignalR's TypeScript client beside its C#, puts two toolchains in every folder for a Blazor part that is
a thin shell, and draws boundaries the dependencies do not follow, since every product depends on
ExGrid's core and on `data`. Two sub-repositories side by side, each with its own samples and tests,
fits languages that implement the same thing independently; here NuGet carries npm's build and layer 3
runs every host, so it was not put forward.

The move is one pull request of renames after every step 3, not before (the user, 2026-10-25), so that
the threads working in parallel do not rebase over it and the history is tidied once. Until then a new
file is written where today's layout puts it.

### 10. A Script Core is told its locale, and Excel's text is kept where `Intl`'s differs

A Script Core reads text in a culture: a filter's value, a pasted cell's displayed text, a constant
typed into a sheet, and ExSheet's formats. C# reads `CultureInfo`; TypeScript has no current culture.

- **The locale is an option, said once.** Every product's options take `locale`, a BCP 47 tag or
  `"invariant"`, which reads as .NET's `InvariantCulture` does, from the core's own table and not from
  `Intl`; a culture with no name, `InvariantCulture`'s, is passed as `"invariant"`, never as an empty
  tag. A
  React adapter given none passes the browser's language, read once when the instance is made; a
  Blazor adapter passes the name of the `CultureInfo` the component reads today. The core never reads
  `navigator.language` or calls `Intl` without a locale on its own, feature by feature, so one
  instance never reads two cultures. The engines take the locale from the Script Core that asks
  them.
- **ADR-0023's reading holds as written.** Text in another culture's separators, or text that reads
  two ways, is refused, never read as some other number. A culture's separators come from
  `Intl.NumberFormat`'s `formatToParts` where ICU agrees with Windows, and from the core's table where
  it does not (fr-FR's group separator is U+00A0 on Windows and U+202F in ICU), and the cases pin them.
- **Where `Intl` differs from Excel, Excel's text wins, as data.** The browser's `Intl` is ICU's data,
  not Windows', and Excel's text is Windows'. The corrections ExSheet carries over .NET's data today
  ([ADR-0071](./0071-a-sheets-cell-format-is-document-data-painted-on-white-paper.md): Windows'
  month names, en-US's negative currency in brackets) move to TypeScript as tables, not as code
  paths. Shared cases under `tests/cases/` pin the expected strings for en-GB, de-DE and ja-JP,
  `ExcelOracle`'s locales, and run on C#, Node and Chrome alike, because each carries its own ICU
  version and a figure that changes with the runtime is the quiet error the spine's first rule
  refuses.
- **The words a product shows are English by default and Japanese by choice.** *(Decided with the
  user, 2026-10-25, over English alone with every other language the Consumer's, AG Grid's way, and
  over words that follow `locale` on their own.)* Every word the family shows, commands, panels,
  refusals, notices and ExSheet's sentences, has an id and built-in English, and a Consumer replaces any
  of them through the product's label function, as ADR-0036 and ADR-0203 have it for ExGrid. Each
  product also ships its Japanese, chosen in one line as `PivotWords.Japanese` is today (ADR-0060): the
  words of Excel's Japanese edition where Excel shows the same thing, and the family's own Japanese
  elsewhere. The choice never follows `locale` or the browser's language, because a screen whose
  language changed unasked is the surprise ADR-0060 refuses, and a Consumer's label function still wins
  over it. The tables live in the TypeScript packages, so a Blazor adapter takes the same words. The MUI
  Wrappers take MUI's own localised text where MUI has a key, as the MudBlazor Wrappers take MudBlazor's
  (WR-3), and the label function otherwise. ExSheet's sentences, fixed in English today with no seam,
  move behind its label function with its port. Other languages are a Consumer's, through the same
  function.

### 11. Layer 3 judges a behaviour once, and a Blazor host what Blazor touches

*(Decided with the user, 2026-10-25, after this ADR merged. Asked what else was left, the thread had
estimated that layer 3 would take nearly twice its time, every product's full specs running on a third
host. The user answered that Blazor would be little more than an adapter, so the doubling made no sense.
For the end state it does not: the estimate counted the same code judged three times.)*

A behaviour is the Script Core's code under either adapter, drawing the same DOM contract (ADR-0350
section 4), so running its spec on three hosts judges the same code three times. Once a product's
step 3 holds, its layer 3 is split by what a spec judges:

- **Behaviour specs** run on the React host, in both browsers: keys, selection, copy and paste, the
  engines' answers on screen and the rest, under the built-in Chrome and with data the page already
  holds.
- **Blazor specs** run on both Blazor hosts, in both browsers. A spec is one when anything it judges
  passes through Blazor: a parameter reaching the Script Core; an event or callback reaching the
  Consumer; data handed, changed or answered across the Server host's circuit, a Window asked for by
  a scroll included (spine rule 6 holds at that boundary); a Chrome or Template slot filled with
  Razor, the MudBlazor Wrappers' editors, popovers and panels among them; Blazor's own runtime at the
  root, its delegated events (ADR-0240), prerender and attach; and the Docs Site's Blazor Examples.
  `@ex-grid/mui`'s specs run on the React host in the same way.
- **A spec says which it is**, in its title's tag, and the run refuses a spec that says neither, so no
  spec runs on a host by default and none is left out by default.

**A layer-3 test earns its run.** *(Decided with the user, 2026-10-25: "do not add useless layer-3
tests, remove the redundant ones, and make the tests ones that develop the product and raise its
value".)* A layer-3 test judges what only a browser shows (the table of layers in `CLAUDE.md`) and
what a user or a Consumer would meet; each one names the criterion or the defect it holds. A test that
repeats a judgement layer 1 or 2 already makes, or another layer-3 test already makes on the same
path, is removed, not added, and a port to the React host moves a test rather than copying it. A
removal keeps every Definition of Done criterion checked: it names the test that still checks it.

Until a product's step 3 holds, its C# UI still ships, so its specs keep running on the Blazor hosts as
they do today. Its step 2 holds when its full specs pass on both Blazor hosts, and they keep running
there until step 3, which is the proof that the adapter carries every behaviour (section 6). CI
therefore grows while a product's two implementations ship side by side. Whether it comes out lighter
than today once step 3 holds is measured, not assumed: each step 3 records layer 3's minutes before and
after in its pull request. ADR-0350 section 4's three hosts, ADR-0041's per-host jobs and the
Definition of Done's criteria checked "on the Server host" are read with this split from each product's
step 3, and those criteria are rewritten then, naming the host each is checked on, without dropping any.

## Consequences

- Every product has one implementation in one language from 0.3.0. Until each step 3, the C# code still
  ships on `main`, and the shared cases, the differential runs and the shared layer 3 keep the two from
  drifting.
- 0.3.0 grows by ~26,500 lines of UI, ~5,900 of chart layout and ~53,800 of engine and data to move, three MUI Wrappers written
  new, and the transcription of ~27,000 lines of C# engine tests into shared cases.
- A React or Blazor application downloads no .NET runtime for the family's engines, and Blazor
  WebAssembly's 1.0 s published edit is replaced by the TypeScript engine's.
- On Blazor Server the engines leave the server for the browser, so a large workbook or Snapshot now
  crosses the circuit once instead of its edits crossing it each time (section 2).
- A .NET server keeps the packages of section 3 in C#: ExGrid.Data, ExGrid.Data.Arrow, ExPivot.Server,
  ExBoard.Spec and the reduced ExChart.Engine, each held to its TypeScript counterpart by shared cases.
- A server pivot past the cap is refused by name until Level Aggregates are built, and is then asked
  for the groups it shows, a round trip per expand, with every level checked against its parent. Live
  server data reaches the source through the Consumer's transport, as a notice that re-asks what is shown
  or a Change Batch of records the engine folds into the Leaf Aggregates it holds. A sibling set too large to fetch is a window checked with what lies
  on either side (section 3).
- CI's layer 3 grows while a product's C# UI and Script Core both ship. From its step 3, behaviour runs
  on the React host and Blazor's hosts run what passes through Blazor; each step 3 records the minutes
  (section 11).
- The family publishes nineteen npm packages, and the release workflow publishes them from the same
  tag as NuGet's.
