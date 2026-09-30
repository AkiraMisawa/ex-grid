# ExPivot is a pivot table, drawn by ExGrid as that grid's Consumer

*(Proposed 2026-09-30, when ExPivot was started. The user asked for "ExPivot, used like Excel's
PivotTable, with a Wrapper for a MudBlazor-like design". The first build followed this proposal, so
that there was something concrete to decide over.*

*Decided with the user the same day, in a grilling of the requirements that took the build as its
starting point. The shape held: a Consumer of ExGrid, Excel's rules, three packages. What changed is
marked **Changed when decided** below, each with its reason:*

- *where the aggregation runs — a Pivot Source, which a server may answer
  ([ADR-0065](./0065-expivot-asks-a-pivot-source-and-a-server-answers-with-leaf-aggregates.md));*
- *what the records are held in — a Snapshot
  ([ADR-0063](./0063-the-snapshot-is-the-familys-immutable-data-held-in-columns.md));*
- *live data ([ADR-0066](./0066-live-data-a-change-batch-makes-the-next-snapshot-and-expivot-folds-it-in.md));*
- *where Show Details goes;*
- *what the first version holds.)*

**ExPivot is Excel's PivotTable inside the application.**

- The Consumer hands it a **Pivot Source** and the **Pivot Fields** that source offers.
- The user places Pivot Fields into four **Areas** — Filters, Columns, Rows and Values — through
  the **Field List**.
- ExPivot then computes the **Pivot Report**. The report has one row per Item of the row fields and
  one column per Item of the column fields. Each Value Field is aggregated where a row and a column
  cross, and the report carries subtotals and grand totals.

**It shows the report by rendering one ExGrid, as that grid's Consumer.** ExPivot holds the **Pivot
Layout**, asks its source and computes. ExGrid paints, selects, navigates, copies and reports.

```
application  (ExPivot's Consumer: hands over the Pivot Source — a Snapshot, or its server —
   ↓            persists the Pivot Layout, and may show the records behind a cell itself)
   ↓ source / layout                        ↑ LayoutChanged, OnShowDetails
ExPivot      (the Field List and toolbar; holds the Pivot Layout; asks the source for the
   ↓            Leaf Aggregates and lays the report out — ExPivot.Engine)
   ↓ pushes the report as a Window          ↑ Selection, context commands, double click
ExGrid       (painting, Selection, Focus, keyboard, clipboard, Change Highlight, Chrome seams)
```

## Why a pivot table at all, when the real Excel sits next to it

The answer is ExSheet's
([ADR-0046](./0046-exsheet-is-a-general-purpose-sheet-drawn-by-exgrid-as-its-consumer.md)), and it
is stronger here:

- **It is inside the application.** Today a user who wants totals by desk and month exports the
  rows, builds a PivotTable in Excel, and reads a file that has no owner, no access control and no
  link back to the data. ExPivot reads the data the application already has, under the
  application's own rules.
- **It reads live data.** A Change Batch, or a server saying its data moved on, is Excel's Refresh
  done without anyone asking for it, and the cells that changed are marked (ADR-0066).
- **Its report obeys the display rules this family was built on.**
  - A number that does not fit is `####`
    ([ADR-0016](./0016-column-width-and-overflow.md)).
  - A copy is refused rather than truncated
    ([ADR-0005](./0005-copy-refuses-rather-than-truncates.md)).
  - A total is computed from the records, never from rounded subtotals
    ([ADR-0059](./0059-the-pivot-engine-answers-as-excels-pivottable-and-is-the-reference.md)).

## Why ExGrid's Consumer, and not a feature of ExGrid

The third principle of the design says **the grid neither holds nor executes**: not sorting, not
filtering, **not grouping** ([ADR-0001](./0001-consumer-pushes-the-window-grid-does-not-fetch.md)).
[ADR-0024](./0024-row-kind-is-a-declared-role-not-a-hierarchy.md) and
[ADR-0032](./0032-tiered-headers-are-declared-rectangles-not-a-column-tree.md) were written with
pivot-like views in mind, and drew the line in the same place:

- **Row Kind** is a role the Consumer declares.
- A **Header Group** is a rectangle the Consumer declares.
- Whoever computes the grouping and the aggregate is the Consumer.

Every Consumer that wanted a pivot would write that Consumer by hand. **ExPivot is that Consumer,
written once and packaged.**

The three shapes ADR-0019 weighed for ExSheet apply unchanged, and the answer is the same:

- **A Consumer of ExGrid — chosen.** ExPivot needs two opt-in declarations from the core: a double
  click where no edit opens ([ADR-0062](./0062-what-expivot-asks-of-exgrids-core.md)), and the
  Change Highlight
  ([ADR-0067](./0067-change-highlight-is-asked-of-the-consumer-and-painted-without-animation.md)).
  ExGrid already has everything else it needs: Row Kind, Header Groups, Pinned Columns, Template
  Columns, column virtualisation, the Context Menu, copy, and the loading seam.
- **Grouping in ExGrid's core — rejected.** It would make the grid hold and execute. ADR-0001 gave
  four reasons for the push form: a server that must match the reference, no cache to invalidate,
  View State kept outside from the start, and state libraries. Each would be lost for the grids that
  do not pivot.
- **A separate implementation — rejected**, for ADR-0046's reason. It would duplicate
  virtualisation, Selection, the keyboard and the clipboard, and the copies would drift.

## How a Pivot Report maps onto ExGrid

| Pivot Report | ExGrid |
|---|---|
| The report's rows | The whole report is the **Window**, with `TotalCount` left null ([ADR-0001](./0001-consumer-pushes-the-window-grid-does-not-fetch.md)) |
| An item row, a group row, a subtotal or grand total row | **Row Kind** Detail, Group or Total ([ADR-0024](./0024-row-kind-is-a-declared-role-not-a-hierarchy.md)) |
| The row labels: one column in the Compact form, one per row field otherwise | **Pinned** **Template Columns**, carrying the label, its indent, and the expand / collapse button ([ADR-0020](./0020-action-and-template-columns.md)) |
| A value column | A Number column whose values carry their own display text (ADR-0059) |
| The column Items above the value columns | **Header Groups**, one tier per outer column level ([ADR-0032](./0032-tiered-headers-are-declared-rectangles-not-a-column-tree.md)) |
| The report's sequence of rows | The **Row Sequence Version**, bumped only when the sequence of row keys changes, so a data refresh that changes values keeps the Selection ([ADR-0011](./0011-selection-is-rectangles-in-index-space-and-is-dropped-on-reorder.md)) |
| A question out to the Pivot Source | The grid's **loading seam**, `IsLoading` ([ADR-0010](./0010-chrome-seams-column-menu-editor-loading.md)) |
| A value that changed with the data | **`CellChangedAt`**, the Change Highlight (ADR-0067) |
| Excel's pivot context menu | Commands appended to the **Context Menu** ([ADR-0036](./0036-the-context-menu-is-the-column-menu-shape-over-a-selection.md)) |

**The expand / collapse button is a Template Column's, not an Action Column's.** ADR-0024 left the
gesture open, and named an Action Column as the likely shape. An Action Column, though, declares the
same actions for every row, and this button differs per row:

- `+` on a collapsed Item;
- `−` on an expanded one;
- nothing on an innermost Item.

It also stands indented beside the label, where Excel draws it. A Template Column says all of that,
and its cost is paid on one column only. The button carries `ex-interactive` and `tabindex="-1"`,
and takes DOM focus when the core's `FocusRequest` asks
([ADR-0037](./0037-entering-a-cell-never-reaches-into-content-the-core-did-not-render.md)).

**What is deliberately not wired:**

- **ExGrid's sort and filter.** They reorder a view of rows the Consumer owns. In a PivotTable, the
  Item order and the Hidden Items are part of the Pivot Layout, set from the Field List and the
  Context Menu.
  - `OnSortChanged` and `OnFilterChanged` have no delegate.
  - `HeaderClickSelects` gives the header click Excel's meaning: it selects.
- **Editing, paste and fill.** A report is computed, and Excel refuses to change one ("Cannot change
  this part of a PivotTable report"). No column is Editable, so the `Editable` gate refuses every
  write ([ADR-0035](./0035-paste-and-fill-respect-the-editable-declaration.md)).
- **The column menu.** Its commands (sort, filter, hide, pin) are the display grid's. The resize
  grips stay (`HideColumnMenu`, [ADR-0050](./0050-what-exsheet-asks-of-exgrids-core.md) item 12).
- **A dropdown on the report's headers**, Excel's "Row Labels ▾" (Q5, Q16). Excel sorts and filters
  from the report's own headers. That needs two things the core does not have: a Consumer's commands
  in a column header's menu, and a menu on a Header Group. It would also move ExGrid's release
  criteria. The same commands are on each placed field's menu in the Field List, and in the Context
  Menu; the toolbar's toggle brings the Field List back whenever it is hidden (ADR-0060). The
  dropdown waits for an ADR on ExGrid's header menus.

**Copy is ExGrid's, unchanged.** A value cell's `text/plain` is its display text, and its
`text/html` is the full-precision number, so the report pastes into Excel as numbers
([ADR-0005](./0005-copy-refuses-rather-than-truncates.md)).

## Who owns what

*Changed when decided.* The proposal had ExPivot hold the Consumer's record list by reference and
aggregate it in process, with a Consumer-answered "pivot query" reserved for records too many to
hold. The user asked for the server model at once: "a million-row CSV is normal" (Q2). The measured
cost of the in-process pass (6.8 s per aggregation in the browser) confirmed the request.

- **The data is the Consumer's, behind a Pivot Source** (ADR-0065).
  - **The bundled source holds a Snapshot** (ADR-0063): an immutable copy of the Consumer's records,
    or of a CSV, a database query, or an Arrow stream. A new Snapshot, or a Change Batch, is a
    refresh.
  - **A server's source keeps the data on the server** and answers with the Leaf Aggregates.
  - ExPivot never writes to the data.
- **The Pivot Layout is View State** (`CONTEXT.md`). It says which fields stand in which Areas and
  in what order, each field's settings, and the report's form and totals.
  - It is serialisable, and it is the Consumer's to persist. A named one is a **Saved View**.
  - ExPivot applies a user's change at once and raises `LayoutChanged`.
  - A layout the Consumer hands in replaces the one on screen.
- **The aggregation runs in the Pivot Source**, never in the component. The bundled source runs
  ExPivot.Engine over its Snapshot, in the browser under WebAssembly or in the host's process under
  Blazor Server. A server runs the same engine over a Snapshot of its own, or answers from SQL and
  is held to the engine's answers, as a server Grid Source is held to `GridSource.From`
  ([ADR-0023](./0023-filter-and-sort-semantics-of-the-reference-implementation.md)).

## Show Details goes where the Consumer chooses

*Changed when decided* (Q10, Q18, Q26). The proposal only handed the records to the Consumer, and
offered no command when the Consumer did not listen. The user asked for Excel's own experience —
the records on a sheet of their own — and for the application to choose. There are three
destinations:

- **A tab, the default.** A tab opens at the report's foot, where Excel's sheet tabs are.
  - It is titled by the cell ("Details: Americas / Credit / Bond").
  - It can be closed.
  - It is not part of the Pivot Layout.
  - Its records are an ExGrid of the Pivot Fields, fetched from the source in pages
    ([ADR-0025](./0025-what-the-bundled-fetching-source-promises.md)), so the grand total of a
    million records opens without freezing.
  - The tab keeps the Source Version it was opened under. The bundled source's tab therefore always
    adds up. A server's tab says "the data has changed" once its source can no longer answer under
    that version (ADR-0065).
- **A dialog**, when the Consumer asks for one, with the same grid in it.
- **The Consumer.** When it listens to `OnShowDetails`, it takes the records to show where it shows
  records, and neither the tab nor the dialog opens.

## Packages

*Changed when decided:* `ExGrid.Data` joined, under the engine (ADR-0063).

```
ExGrid.Data        no dependency: the Snapshot, its builders, the Change Batch (the family's, ADR-0063)
ExGrid.Data.Arrow  → ExGrid.Data, → Apache.Arrow: a Snapshot as Arrow (optional, ADR-0064)
ExPivot.Engine     → ExGrid.Data: the Pivot Layout, the Pivot Source, the aggregation, the report;
                     a server can run it
ExPivot            the component: → ExPivot.Engine, → ExGrid
ExPivot.MudBlazor  the Wrapper: → ExPivot, → ExGrid.MudBlazor, → MudBlazor (ADR-0061)
```

**The references point one way**, as ADR-0019 requires.

- Nothing references `ExPivot` except its Wrapper, the demo pages and the tests.
- `ExGrid.Data` references nothing of ours.

**ExPivot is not part of the release**, as ExSheet is not (Definition of Done §2).

- The package check packs ExPivot into a feed of its own, together with `ExGrid.Data` and
  `ExGrid.Data.Arrow`.
- ExPivot's criteria (§29) and the data packages' criteria (§30) judge them, and never gate ExGrid.
- What ExPivot asks of the core (ADR-0062, ADR-0067) is ExGrid code, and gates ExGrid like every
  other declaration (§26).

## What the first version holds

*Changed when decided* (Q3, Q4, Q6, Q11, Q13, Q14, Q17, Q18, Q44, Q59). Five rows moved into the
first version, and five were added. The user added the server model, live data, the highlight, the
Details tabs and the Layout menu. Defer Layout Update and the date parts came in with the
measurements, and the Japanese words with Q6.

| | First version | Later | Why later |
|---|---|---|---|
| Four Areas, drag and drop, the field menus, ticking a field | ✓ | | |
| Sum, Count, Average, Max, Min, Product, Count Numbers, StdDev, StdDevp, Var, Varp | ✓ | | |
| Compact, Outline and Tabular forms; subtotals at the top or bottom, or off; grand totals | ✓ | | |
| The **Layout menu** that sets them, and the **toolbar** above the report (ADR-0060) | ✓ | | |
| Expand and collapse row Items, and whole fields on either axis | ✓ | | |
| Hidden Items (the item filter), on every Area | ✓ | | |
| Sort by label, ascending or descending, by a Value Field, and by an **Order Key** (ADR-0059) | ✓ | | |
| Show Values As: % of Grand Total, % of Column Total, % of Row Total | ✓ | | |
| Show Details, into a **tab**, a **dialog** or the Consumer | ✓ | | |
| Several Value Fields, and **Σ Values** in Rows or Columns | ✓ | | |
| **A Pivot Source answered by a server** (ADR-0065) | ✓ | | |
| **Live data**: Change Batches, and a server that says its data moved on (ADR-0066) | ✓ | | |
| **Change Highlight** on values that changed with the data (ADR-0067) | ✓ | | |
| **Defer Layout Update**, and asking without blocking (ADR-0065) | ✓ | | |
| **Caps** on the leaves, the rows and the columns (ADR-0065) | ✓ | | |
| **Date parts**: year, quarter and month declared as fields in one line, in calendar order (ADR-0059) | ✓ | | |
| **The words of Excel's Japanese edition**, chosen by the Consumer (ADR-0059) | ✓ | | |
| Σ Values at any position but the innermost | | ✓ | its subtotal rules differ, ADR-0059 |
| Collapsing one column Item | | ✓ | the ± would sit in a header cell, which ExGrid draws |
| Excel's Group… command: ranges of numbers, and dates grouped from the report | | ✓ | the date parts serve the common case |
| Label filters, value filters, Top 10 | | ✓ | |
| The other Show Values As (% of parent, difference from, running total, rank) | | ✓ | |
| Calculated fields and calculated items | | ✓ | a formula language, which ExSheet owns |
| A Field List placed outside the component | | ✓ | |
| A dropdown on the report's headers ("Row Labels ▾") | | ✓ | ExGrid's header menus first (above) |
| A source that writes its own SQL | | ✓ | one dialect per database, ADR-0065 |
| Editing values in the report (writeback) | | ✗ | a report is computed; Excel refuses it too |

## Consequences

- **`ExPivot` and its vocabulary are in `CONTEXT.md`**: Source Record, Pivot Field, Area, Pivot
  Layout, Item, Hidden Item, Value Field, Aggregation, Σ Values, Pivot Report, Report Form, Field
  List and Show Details. The grilling added Pivot Source, Source Version, Leaf Aggregate, Order Key,
  Stale Report and Defer Layout Update, and the family's Snapshot, Change Batch, Record Key and
  Change Highlight.
- **§29 of the Definition of Done (PV)** states what "ExPivot works" means, and §30 (DA) states it
  for the data packages. Like §27, neither gates ExGrid.
- **ADR-0024's open question about the expand / collapse gesture is answered for a pivot**: a
  Template Column's button, and the Context Menu. The grid still computes nothing and holds no
  expansion state; the collapse state is the Pivot Layout's.
- **ExPivot is built alongside ExGrid and ExSheet.** A change it needs in the core is made through
  an ADR, and must be right for a plain ExGrid Consumer as well.
