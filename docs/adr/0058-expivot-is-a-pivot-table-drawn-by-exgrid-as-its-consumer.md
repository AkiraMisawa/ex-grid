# ExPivot is a pivot table, drawn by ExGrid as that grid's Consumer

*(Proposed 2026-09-30, when ExPivot was started, and **not yet decided with the user**. The user
asked for "ExPivot, used like Excel's PivotTable, with a Wrapper for a MudBlazor-like design". The
rest of this record, and ADR-0059 to ADR-0062 beside it, are the proposal the first build follows,
so that there is something concrete to decide over. Where the user decides otherwise, the record
is rewritten and the build follows it, as ADR-0046 was for ExSheet.)*

**ExPivot is Excel's PivotTable inside the application.** The Consumer hands it **Source Records**
and declares the **Pivot Fields** they carry. The user places Pivot Fields into four **Areas** —
Filters, Columns, Rows, Values — through the **Field List**, and ExPivot computes the **Pivot
Report**: one row per Item of the row fields, one column per Item of the column fields, each Value
Field aggregated where they cross, with subtotals and grand totals. **It shows the report by
rendering one ExGrid, as that grid's Consumer**: ExPivot holds the **Pivot Layout** and computes,
ExGrid paints, selects, navigates, copies and reports.

```
application  (ExPivot's Consumer: pushes the Source Records, declares the Pivot Fields,
   ↓            persists the Pivot Layout, shows the records behind a cell)
   ↓ records / fields / layout             ↑ LayoutChanged, OnShowDetails
ExPivot      (the Field List; holds the Pivot Layout; computes the report — ExPivot.Engine)
   ↓ pushes the report as a Window          ↑ Selection, context commands, double click
ExGrid       (painting, Selection, Focus, keyboard, clipboard, Chrome seams)
```

## Why a pivot table at all, when the real Excel sits next to it

The answer is ExSheet's ([ADR-0046](./0046-exsheet-is-a-general-purpose-sheet-drawn-by-exgrid-as-its-consumer.md)),
and it is stronger here:

- **It is inside the application.** Today a user who wants totals by desk and month exports the
  rows, builds a PivotTable in Excel, and reads a file that has no owner, no access control and no
  link back to the data. ExPivot reads the rows the application already shows, under the
  application's own rules.
- **It reads live data.** A new snapshot pushed by the Consumer is Excel's Refresh, done without
  anyone asking for it.
- **Its report obeys the display rules this family was built on.** A number that does not fit is
  `####` ([ADR-0016](./0016-column-width-and-overflow.md)), a copy is refused rather than truncated
  ([ADR-0005](./0005-copy-refuses-rather-than-truncates.md)), and a total is computed from the
  records, never from rounded subtotals ([ADR-0059](./0059-the-pivot-engine-answers-as-excels-pivottable-and-is-the-reference.md)).

## Why ExGrid's Consumer, and not a feature of ExGrid

The third principle of the design says **the grid neither holds nor executes** — not sorting, not
filtering, **not grouping** ([ADR-0001](./0001-consumer-pushes-the-window-grid-does-not-fetch.md)).
[ADR-0024](./0024-row-kind-is-a-declared-role-not-a-hierarchy.md) and
[ADR-0032](./0032-tiered-headers-are-declared-rectangles-not-a-column-tree.md) were written with
pivot-like views in mind and drew the line the same way: **Row Kind** is a role the Consumer
declares, a **Header Group** is a rectangle the Consumer declares, and whoever computes the
grouping and the aggregate is the Consumer. Every Consumer that wanted a pivot would write that
Consumer by hand. **ExPivot is that Consumer, written once and packaged.**

The three shapes ADR-0019 weighed for ExSheet apply unchanged, and the answer is the same:

- **A Consumer of ExGrid** — chosen. What ExPivot needs from the core is one opt-in notification
  ([ADR-0062](./0062-what-expivot-asks-of-exgrids-core.md)); everything else it needs, ExGrid
  already has: Row Kind, Header Groups, Pinned Columns, Template Columns, column virtualisation,
  the Context Menu, copy.
- **Grouping in ExGrid's core** — rejected. It would make the grid hold and execute, and ADR-0001's
  four reasons for the push form (a server that must match the reference, no cache to invalidate,
  View State outside to begin with, state libraries) would each be lost for the grids that do not
  pivot.
- **A separate implementation** — rejected, for ADR-0046's reason: it would duplicate
  virtualisation, Selection, the keyboard and the clipboard, and the copies would drift.

## How a Pivot Report maps onto ExGrid

| Pivot Report | ExGrid |
|---|---|
| The report's rows | The whole report is the **Window**, `TotalCount` left null ([ADR-0001](./0001-consumer-pushes-the-window-grid-does-not-fetch.md)) |
| An item row / a group row / a subtotal or grand total row | **Row Kind** Detail / Group / Total ([ADR-0024](./0024-row-kind-is-a-declared-role-not-a-hierarchy.md)) |
| The row labels (one column in the compact form, one per row field otherwise) | **Pinned** **Template Columns**: the label, its indent, and the expand / collapse button ([ADR-0020](./0020-action-and-template-columns.md)) |
| A value column | A Number column whose values carry their own display text (ADR-0059) |
| The column items above the value columns | **Header Groups**, one tier per outer column level ([ADR-0032](./0032-tiered-headers-are-declared-rectangles-not-a-column-tree.md)) |
| The report's sequence of rows | The **Row Sequence Version**, bumped only when the sequence of row keys changes, so a data refresh that changes values keeps the Selection ([ADR-0011](./0011-selection-is-rectangles-in-index-space-and-is-dropped-on-reorder.md)) |
| Excel's pivot context menu | Commands appended to the **Context Menu** ([ADR-0036](./0036-the-context-menu-is-the-column-menu-shape-over-a-selection.md)) |

**The expand / collapse button is a Template Column's, not an Action Column's.** ADR-0024 left the
gesture open and named an Action Column as the likely shape. An Action Column declares the same
actions for every row, and this button differs per row: `+` on a collapsed item, `−` on an expanded
one, nothing on an innermost item, and it stands indented beside the label, where Excel draws it.
A Template Column says all of that, and its cost is paid on one column only. The button carries
`ex-interactive` and `tabindex="-1"`, and takes DOM focus when the core's `FocusRequest` asks
([ADR-0037](./0037-entering-a-cell-never-reaches-into-content-the-core-did-not-render.md)).

**What is deliberately not wired:**

- **ExGrid's sort and filter.** They reorder a view of rows the Consumer owns. In a PivotTable,
  item order and hidden items are part of the Pivot Layout, set from the Field List and the Context
  Menu, and the engine applies them. `OnSortChanged` and `OnFilterChanged` have no delegate, and
  `HeaderClickSelects` gives the header click Excel's meaning: it selects.
- **Editing, paste and fill.** A report is computed, and Excel refuses to change one ("Cannot
  change this part of a PivotTable report"). No column is Editable, so every write is refused by
  the `Editable` gate ([ADR-0035](./0035-paste-and-fill-respect-the-editable-declaration.md)).
- **The column menu.** Its commands (sort, filter, hide, pin) are the display grid's. The resize
  grips stay (`HideColumnMenu`, [ADR-0050](./0050-what-exsheet-asks-of-exgrids-core.md) item 12).

**Copy is ExGrid's, unchanged.** A value cell's `text/plain` is its display text and its
`text/html` the full-precision number, so the report pastes into Excel as numbers
([ADR-0005](./0005-copy-refuses-rather-than-truncates.md)).

## Who owns what

- **The Source Records are the Consumer's.** ExPivot holds the list it was handed, by reference —
  Excel's pivot cache — and never writes to it. **A new list instance is a refresh**; a list
  rewritten in place is not seen, which is Row Identity's rule for the same reason
  ([ADR-0003](./0003-cells-are-plain-markup-by-default-not-components.md)).
- **The Pivot Layout is View State** (`CONTEXT.md`): which fields stand in which Areas, in what
  order, each field's settings, the report's form and totals. It is serialisable, it is the
  Consumer's to persist, and a named one is a **Saved View**. ExPivot applies a user's change at
  once and raises `LayoutChanged`; a layout the Consumer hands in replaces the one on screen.
- **Aggregation runs in ExPivot, in process, over the snapshot** (ADR-0059), which is Excel's own
  model. A Consumer whose records are too many to hold answers a **pivot query** of its own instead
  — reserved, with a named trigger below — and is then held to the engine's answers as a server
  Grid Source is held to `GridSource.From`
  ([ADR-0023](./0023-filter-and-sort-semantics-of-the-reference-implementation.md)).

## Packages

```
ExPivot.Engine    no dependency: the Pivot Layout, the aggregation, the report; a server can run it
ExPivot           the component: → ExPivot.Engine, → ExGrid
ExPivot.MudBlazor the Wrapper: → ExPivot, → ExGrid.MudBlazor, → MudBlazor (ADR-0061)
```

The direction is one-way, as ADR-0019 requires. Nothing references `ExPivot` but its Wrapper, the
demo pages and the tests; `ExPivot.Engine` references nothing of ours. **ExPivot is not part of
the release**, as ExSheet is not (Definition of Done §2): the package check packs it into a feed of
its own, and its criteria (§29) judge ExPivot and never gate ExGrid. The one thing ExPivot asks of
the core (ADR-0062) is ExGrid code, and gates ExGrid like every other declaration (§26).

## What the first version holds

| | First version | Later | Why later |
|---|---|---|---|
| Four Areas, drag and drop, the field menus, ticking a field | ✓ | | |
| Sum, Count, Average, Max, Min, Product, Count Numbers, StdDev, StdDevp, Var, Varp | ✓ | | |
| Compact, Outline and Tabular forms; subtotals at top or bottom, or off; grand totals | ✓ | | |
| Expand and collapse row items, and whole fields on either axis | ✓ | | |
| Hidden items (the item filter), on every Area | ✓ | | |
| Sort by label, ascending or descending, and by a Value Field | ✓ | | |
| Show Values As: % of Grand Total, % of Column Total, % of Row Total | ✓ | | |
| Show Details (the records behind a cell) | ✓ | | |
| Several Value Fields, **Σ Values** in Rows or Columns | ✓ | | |
| Σ Values at any position but the innermost | | ✓ | its subtotal rules differ, ADR-0059 |
| Collapsing one column item | | ✓ | the ± sits in a header cell, which ExGrid draws |
| Grouping dates (Years, Quarters, Months) and numbers into ranges | | ✓ | a Pivot Field the Consumer declares does it today |
| Label filters, value filters, Top 10 | | ✓ | |
| The other Show Values As (% of parent, difference from, running total, rank) | | ✓ | |
| Calculated fields and calculated items | | ✓ | a formula language, which ExSheet owns |
| Defer Layout Update | | ✓ | the report is recomputed in process; measured first |
| A Field List placed outside the component | | ✓ | |
| A pivot query answered by the Consumer (server-side aggregation) | | reserved | trigger: records the process cannot hold |
| Editing values in the report (writeback) | | ✗ | a report is computed; Excel refuses it too |

## Consequences

- **`ExPivot` and its vocabulary enter `CONTEXT.md`** — Source Record, Pivot Field, Area, Pivot
  Layout, Item, Hidden Item, Value Field, Aggregation, Σ Values, Pivot Report, Report Form,
  Field List, Show Details.
- **A Definition of Done section, §29 (PV)**, states what "ExPivot works" means. Like §27 it
  judges ExPivot and never gates ExGrid.
- **ADR-0024's open question about the expand / collapse gesture is answered for a pivot**: a
  Template Column's button, and the Context Menu. The grid still computes nothing and holds no
  expansion state; the collapse state is the Pivot Layout's.
- **ExPivot is built alongside ExGrid and ExSheet.** A change it needs in the core is made through
  an ADR and must be right for a plain ExGrid Consumer as well.
