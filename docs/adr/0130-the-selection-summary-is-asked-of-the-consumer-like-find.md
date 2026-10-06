# The Selection Summary is asked of the Consumer, like Find, and answers as Excel's status bar

*(Decided with the user on 2026-10-05. Amends [ADR-0014](./0014-paste-shape-rules-and-selection-count.md),
which said sums and averages were the Consumer's to produce and left it there.)*

Select a few numbers in Excel and the status bar says `Average: 12.5  Count: 4  Sum: 50`. Users
arriving from Excel expect it, and for the first Consumer — position and risk figures — it is the
quickest check there is: select a block, read the total. ADR-0014 decided the grid can show the
selected-cell **count** on its own, because the count is the sum of rectangle areas and needs no
data, and that sums and averages are the Consumer's because only the Consumer has the data. That
is still true. What it left undecided is **how** the Consumer answers, and left undecided, every
Consumer would answer differently — and a total that is quietly computed another way is the failure
this component exists to avoid ([ADR-0005](./0005-copy-refuses-rather-than-truncates.md)).

## The decision

**The grid asks; whoever holds the data answers; the grid shows only an answer to the current
question.** The shape is Find's ([ADR-0055](./0055-find-is-asked-of-the-consumer-like-sort-and-filter.md)).

- **The Selection Summary is Excel's six status-bar figures**: Average, Count, Numerical Count,
  Min, Max and Sum. Excel shows Average, Count and Sum by default, and so does the grid.
- **The request** carries the selected ranges, the Row Sequence Version they were read under, the
  visible columns in the current order (as Find's `Columns`), and **the figures shown** — a
  Consumer computes only what is asked, as ExPivot's sources do (PV-26).
- **Three answerers, as for Find:**
  - a Grid Source, through `IGridSource.CanSummarize` and `SummarizeAsync`, whose default is
    "cannot". **`InMemoryGridSource` is the reference implementation** of what every figure
    means, as `GridSource.From` is for filter, sort and Find (ADR-0023);
  - **`GridSource.Fetch`, through an optional `summarize` delegate** — typically SQL on a server.
    A fetching source must give, question for question, the answer the reference gives over the
    same rows; the demo API server's SQL source is held to that, as PV-22 holds ExPivot's;
  - a push-mode Consumer, through an `OnSummarize` parameter. `OnSummarize` beside a bound Source
    is refused by name, as `OnFind` is.
- **The answer is dropped unless it answers the current question.** A change of selection clears
  the figures at once and shows a pending mark; the question in flight is cancelled and its late
  answer discarded (ADR-0025's rule). An answer under a stale Row Sequence Version shows nothing.
  **The previous selection's figures are never left standing** beside a new selection: they would
  read as its total. A pending mark shown after a delay was rejected for the same reason Find's
  answer is not timed: the outcome would depend on timing (the sixth principle).
- **The rows changing under a standing selection asks again.** A new Window, a Source's change, an
  edit — anything that tells the grid the values may have moved — clears the figures and asks
  again. **The grid does not throttle**; a Consumer with a fast-moving live feed throttles its own
  answers. The values summed are the ones on screen, the Overlay applied
  ([ADR-0007](./0007-edits-are-an-overlay-owned-by-the-consumer.md)): a total that still showed the
  value before an edit would be a lie the user just watched being told.
- **There is no cap.** Selection is cheap and is not capped (the fifth principle). A Consumer that
  will not sum a million rows **declines with a reason**, and the reason is shown in place of the
  figures. **A partial figure is never shown.** `InMemoryGridSource` never declines.
- **With nobody to answer, nothing is shown** beyond the count the grid makes itself. Unlike
  Ctrl+F, the user asked for nothing, so a refusal would be noise. `CanSummarize` tells a Consumer
  whether the figures can appear.

## What each figure means

**The figures mean what Excel's do, and what ExPivot's Aggregations of the same names mean.** One
definition, in one place — `ExGrid.Data` — serves both, so a pivot cell's Sum and the status bar's
Sum over the same records cannot disagree.

| Value in a cell | Count | Numerical Count | Sum, Average, Min, Max |
|---|---|---|---|
| A number (any CLR numeric type) | counted | counted | included |
| Text, including `"123"` | counted | — | — |
| A Boolean | counted | — | — |
| A date | counted | — | — |
| Blank (null) | — | — | — |
| An error (ExSheet's and ExPivot's error values) | counted | — | no figure |

- **A date is not a number** — ExPivot's rule (ADR-0060/0064), not Excel's. Excel's dates are
  serial numbers; ExGrid's are `DateTime` and `DateTimeOffset`, and a serial number for one needs
  an epoch and a time zone the grid would have to invent. A plausible number made from an invented
  basis is the quiet wrong answer. *(This reverses an answer given earlier in the same grilling,
  which had dates summed as Excel sums them; the conflict with ExPivot surfaced when the shared
  definition was placed.)* A date's Min and Max may be added later, by an ADR, if they are wanted.
- **Integer and Decimal values are summed exactly**, as PV-4 sums them. Double values are summed
  as ExPivot sums them.
- **An error among the values leaves only Count.** That is what Excel is understood to do; it is
  confirmed in a Windows run before the criterion is signed off.
- **A cell covered by two ranges counts once.** Hidden columns are not summarised. An Action Column
  is blank. A Template Column contributes its value accessor's answer, never its markup — the rule
  copy follows ([ADR-0005](./0005-copy-refuses-rather-than-truncates.md)).
- **What is visible is summed, whatever it is.** A Total row ([ADR-0024](./0024-row-kind-is-a-declared-role-not-a-hierarchy.md))
  and a pivot's subtotals are summed with the rest when they are selected, as Excel sums them. Row
  Kind carries no aggregate meaning, and a summary that skipped some visible cells would break the
  one promise the status bar makes.
- **The figures are formatted with the Focus's column's format** until Excel's own rule is read in
  a Windows run; then they follow Excel.

## Each product answers for itself

- **ExGrid** asks its answerer as above.
- **ExSheet** answers from its own cells, which it holds: the values of formulas, not their text,
  and every cell of a spilled array. Its formula engine keeps its own arithmetic — IEEE doubles,
  answering as Excel does ([ADR-0047](./0047-the-formula-engine-is-exsheets-own-and-answers-as-excel-or-not-at-all.md))
  — and does not move to the shared definition in this decision.
- **ExPivot** answers from the cells it lays out, subtotals and grand totals included.

## Where it is shown

**The core produces the figures; Chrome draws them** ([ADR-0010](./0010-chrome-seams-column-menu-editor-loading.md),
ADR-0014). A new Chrome seam draws the Selection Summary; the built-in Chrome puts it in the status
line, which is shown whenever two or more cells are selected, and the MudBlazor Wrapper draws its
own. The figures are also exposed to the Consumer, who may put them in an application-wide status
bar and resolves which grid is meant ([ADR-0018](./0018-multiple-instances-must-be-independent.md)).

- **Which figures are shown is the Consumer's state.** The built-in Chrome offers Excel's
  right-click menu over the status line; the grid reports the choice and holds nothing.
- **It is not announced.** The figures are plain text in the status line, read when the reader goes
  to them. A live region that spoke a total at every arrow key would be noise; a Consumer that wants
  it announced builds it from the exposed figures.

## The shared definition, and what it costs

**`ExGrid.Data` holds the definitions of ExPivot's eleven Aggregations**: what each counts and
includes, and how a result is finished from its parts (counts, sums, sums of squares). ExPivot's
columnar accumulator stays in `ExPivot.Engine`, shaped for Snapshot slices, and produces those
parts. The Selection Summary uses six of the eleven.

**ExGrid therefore references `ExGrid.Data`.** This is not ExGrid adopting the Snapshot, which
[ADR-0064](./0064-the-snapshot-is-the-familys-immutable-data-held-in-columns.md) still leaves to an
ADR of its own after measuring: a grid's rows are still the Consumer's objects, and nothing here
changes Row Identity. It does make DA-1 untrue as written, and it puts the aggregation definitions'
criteria inside ExGrid's release (Definition of Done §2).

## Refined while implementing *(2026-10-05)*

- **The strip stands whenever the grid can summarise**, figures or none; the figures appear once
  two or more cells are selected. Shown only while two cells were selected, the status line would
  take its strip from the Viewport at the first Shift+arrow and give it back at the next click — a
  Fill-height grid's rows would jump under the user's hand. A grid that cannot summarise is
  unchanged. The summary box keeps one line's height while it says nothing.
- **What "the rows moved" means to the grid.** The grid compares the rows a new Window holds with
  the rows the old one held at the same positions, by the row type's equality, and the row count;
  a Window that only scrolled moves nothing. An edit, a paste, a fill or a clear the grid hands
  over asks again after the Consumer's handler. A change the grid cannot see — outside the Window,
  a recalculation — is told through `RefreshSummaryAsync()`, which ExSheet calls after every change
  to the Sheet.
- **The figures menu is offered only where `SummaryFiguresChanged` has a delegate**, since the grid
  holds no choice and nobody else would. ExSheet and ExPivot are the grid's Consumers and hold the
  choice themselves, so the menu works on every Sheet and every report.
- **ExSheet's dates are numbers.** A Sheet date is a serial number shown with a date format
  (ADR-0047), so summing it invents nothing; the table's "a date is not a number" is about a
  `DateTime` in a grid row, which has no serial number.
- **A server's parts merge into the same definition.** `AggregateAccumulator.Merge` folds a
  server's `COUNT`, `SUM`, `MIN` and `MAX` in by the arithmetic that merges two pivot leaves; the
  demo API server answers that way in SQL and is held to the reference (SM-8).
- **The figures wear the Focus's format, and the answerer may write it** *(2026-10-06, found
  when the Docs Site's budget recording showed `Sum: 136.159396402943` under cells that read
  `(51)` and `-1.0%`)*. A Sheet's formats are its cells', not its columns', so "the Focus's
  column's format" could not reach them. The request now carries the **Focus**, and an answer may
  carry each figure's **text**: ExSheet writes Average, Min, Max and Sum in the Focus cell's Number
  Format, ExPivot in the Focus cell's Value Field format, and the grid formats the rest itself.
  Excel shows its status bar in the active cell's format, so this is Excel's rule, not a guess;
  the Windows run still reads what Excel does across mixed formats.
- **A Focus column's format that cannot take the figure** — one written as `(int)v` — falls back
  to the figure's own text rather than to nothing. Counts are whole numbers in the invariant
  culture; an unformatted `double` shows ten significant digits, as Excel's status bar does.
- **SM-7 and the format rule were implemented without waiting for the Windows run**, as the user
  asked: an error among the values leaves Count, and the Focus's column's format is used. Both
  stand to be corrected by the run.

## The strip can be switched off *(decided with the user, 2026-10-06)*

The strip that stands whenever the grid can summarise (above) reaches every grid bound to
`GridSource.From` without its Consumer asking — a visible change to grids that were finished
before the Selection Summary existed. **`ShowSelectionSummary` switches it, and it is on by
default**, so a new grid has Excel's status bar as Excel does, and an existing page that wants its
old look back writes one parameter. Off takes the strip and its figures off the grid. A Consumer that
listens to `OnSelectionSummaryChanged` is still told, because a status bar of the application's own
is the other place the figures belong (ADR-0018); with nobody listening either, nothing is asked.
ExSheet and ExPivot carry the same parameter to the grid they draw. *(Corrected the same day: it
first read "off means off entirely… `OnSelectionSummaryChanged` stays at None", which left a page
that shows the figures in its own footer — the Docs Site's blotter — with no way to have them.)*

Rejected: **opt-in** (off by default), which would hide the feature from every grid that could
have it for free; and **a strip only while two cells are selected**, rejected above for moving the
Viewport.

## Considered options

- **The grid sums what is in the Window, and shows nothing past it** — rejected. The figures would
  appear and disappear as the user scrolled, and the outcome would depend on where the Window
  happened to be.
- **Leave it wholly to the Consumer** (ADR-0014 as it stood) — rejected. Every Consumer would
  define a total its own way, and nothing would hold a remote source to the reference.
- **Keep the previous figures until the new answer arrives** — rejected: they read as the new
  selection's.
- **A separate implementation in ExGrid, held to ExPivot's by a shared table of tests** — rejected
  in favour of one definition in one place.
- **The definitions in ExGrid, with `ExPivot.Engine` referencing ExGrid** — rejected: the engine
  runs on a server with no UI, and a Blazor component package is the wrong thing for it to depend
  on.
- **Moving ExSheet's SUM, AVERAGE and the rest onto the shared definition** — not now. ExSheet's
  arithmetic is the double Excel uses, by contract; ExPivot's and the grid's is exact. Whether
  ExSheet shares the classification of values is ExSheet's decision to take.
- **Excel's dates as serial numbers** — rejected, above.
- **Clicking a figure to copy it**, as current Excel does — deferred. The clipboard is an
  allowlisted use (ADR-0021), so nothing stands in the way of adding it later.

## Consequences

- New public types and members, named when implemented: a request and result for the summary, a
  decline reason, the figures enumeration, `IGridSource.CanSummarize` and `SummarizeAsync` with
  defaults, `GridSource.Fetch`'s `summarize`, `OnSummarize`, the shown-figures parameter and its
  change callback, and the Chrome seam.
- **ADR-0014's "sums and averages are the Consumer's job" now reads: the Consumer computes them,
  and the grid asks for them by this ADR.**
- **ADR-0060's Aggregation definitions move to `ExGrid.Data`**; their meaning is unchanged.
- **ADR-0019's "ExGrid has no dependencies" gains its one exception**, `ExGrid.Data`, which has none
  of its own.
- **The glossary gains Selection Summary.**
- The Definition of Done gains §31 (SM), and DA-1 is amended.
