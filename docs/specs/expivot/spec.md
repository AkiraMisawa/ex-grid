# ExPivot, first version

Status: done — but for [ticket 07](issues/07-observe-beside-excel-on-windows.md), the run beside
Excel on Windows (`ready-for-human`). Built in #30, and carried on by #63, #64 and #70. *(Set on
2026-10-10.)*

Decided by [ADR-0059](../../adr/0059-expivot-is-a-pivot-table-drawn-by-exgrid-as-its-consumer.md) to
[ADR-0069](../../adr/0069-the-demo-pages-call-a-demo-api-server-both-hosts-share.md), with the user,
in the grilling of 2026-09-30. ADR-0059 to ADR-0063 were proposed when the first build was made; the
grilling decided them, kept each change and its reason in the text, and added ADR-0064 to ADR-0069.

These ADRs were numbered 0058 to 0069 until 2026-10-01. ExSheet's Pointing Scope reached the shared
branch first and kept ADR-0058 (#38), so ExPivot's moved up by one, and its criteria DC-52 to DC-55
became DC-59 to DC-62. On 2026-10-02 ExSheet's Cell Format reached it first again, with DC-57 to
DC-60 (#42), so ExPivot's criteria in §26 moved up by four: DC-57 to DC-62 became DC-61 to DC-66.
Commit messages written before then use the old numbers.

- The vocabulary is `CONTEXT.md`'s "Pivots" section, with the family's Snapshot, Change Batch,
  Record Key, Schema and Change Highlight.
- The exit criteria are in `docs/definition-of-done.md`: §29 for ExPivot, §30 for the data packages,
  and DC-61 to DC-66 in §26 for the core.

This spec synthesises those decisions; where it and they disagree, they win. The Snapshot has a spec
of its own (`docs/specs/exgrid-data`), and so does the Change Highlight
(`docs/specs/change-highlight`).

## Problem Statement

A user of a line-of-business application wants to ask their data a question the screen was not
built for: P&L by desk and product, notional by month and currency, rate delta by curve and tenor,
how many trades are still unconfirmed per book. Today they export the rows to Excel, build a
PivotTable there, and read the answer in a file that has left the application. By the time anyone
reads it, it is stale, and it has no link back to the records behind each number.

- **The data is often large.** A CSV of a million rows is normal, and a database may hold more than a
  process can.
- **It is often live.** Trades are added and amended every few seconds.

ExGrid cannot answer the question. It shows rows the application owns and reports what the user
does; by design, it neither holds, nor groups, nor aggregates (ADR-0001, the spine's third
principle). A developer who wants a pivot inside the application embeds a third-party one, whose
keyboard, selection, clipboard and look differ from the ExGrid beside it.

## Solution

**ExPivot: Excel's PivotTable, drawn by one ExGrid as that grid's Consumer** (ADR-0059).

- **The data comes through a Pivot Source** (ADR-0066).
  - The bundled source holds a **Snapshot**
    ([ADR-0064](../../adr/0064-the-snapshot-is-the-familys-immutable-data-held-in-columns.md)). A
    Snapshot is built from the application's objects, a CSV, a database query or an Arrow stream,
    and aggregated in the browser or in the host's process.
  - A server's source answers the same questions with the **Leaf Aggregates**, from its own Snapshot
    or from SQL.
  - Either way, the answer carries its Source Version, so the records behind a cell always add up.
- **The user builds a report in the Field List** — Excel's "PivotTable Fields" pane — by ticking
  fields or dragging them between Filters, Columns, Rows and Values.
- **The Pivot Toolbar above the report** holds the report filter band, the Layout menu (Excel's
  Design tab), Refresh, and the pane's toggle.
- **The user reads the report with ExGrid's Selection, keyboard and clipboard.**
- **The numbers are Excel's** (ADR-0060).
  - Money is summed exactly.
  - A total comes from its records.
  - A number that does not fit is `####`.
  - Tenors order by an Order Key, and dates by the calendar.
  - The words can be Excel's Japanese edition's.
- **Asking never blocks.** A slow answer shows a loading indication, a newer question cancels the
  old one, and Defer Layout Update holds a large rebuild until Update. A report too large to read is
  refused by name.
- **Live data is folded in.** Changes are gathered and redrawn four times a second, and the values
  that changed are marked with a Change Highlight
  ([ADR-0067](../../adr/0067-live-data-a-change-batch-makes-the-next-snapshot-and-expivot-folds-it-in.md),
  [ADR-0068](../../adr/0068-change-highlight-is-asked-of-the-consumer-and-painted-without-animation.md)).
  When the newest data cannot be shown, the report says so, with the time of the version on screen.
- **The Pivot Layout is View State.** The application stores it, as JSON if it likes, and hands it
  back.
- **Show Details** opens the records behind a value in a tab at the report's foot, in a dialog, or
  hands them to the application.
- **`ExPivot.MudBlazor`** dresses all of it in MudBlazor (ADR-0062).

## User Stories

### Building a report

1. As a user, I want to see every field my data carries, each with a checkbox, so that I know what I
   can ask about.
2. As a user, I want to tick a field and have it go where Excel puts it — a number into Values as a
   Sum, anything else into Rows — so that a first report takes two clicks.
3. As a user, I want to drag a field onto Filters, Columns, Rows or Values, so that I place it
   exactly.
4. As a user, I want to drag an entry within its Area to reorder it, to another Area to move it, and
   back onto the list of fields to remove it, so that rearranging a report is direct.
5. As a user, I want every drag to have a keyboard route — each entry's menu, and each field's
   checkbox — so that I can build a report without a mouse.
6. As a user, I want each placed field's menu to offer Excel's commands, with the ones that would
   change nothing disabled, so that I never wonder why a command did nothing.
7. As a user, I want to search the list of fields, so that a record with many fields is usable.
8. As a user with a million records, I want to defer the layout's update while I rearrange, and to
   keep working while an answer is on its way, so that a large report never freezes my screen.

### Reading a report

9. As a user, I want the report laid out as Excel's Compact form by default, and to switch to
   Outline or Tabular, subtotals and grand totals from a Layout menu, so that it reads as I expect.
10. As a user, I want to collapse and expand an Item with its `−`/`+` button, the Context Menu or a
    double click on its label, and to collapse a whole field, so that I can drill in and out.
11. As a user, I want to sort a field's Items by label, by a Value Field, or in their natural order —
    tenors from ON to 30Y, months in calendar order — so that the report reads as my trade does.
12. As a user, I want to hide Items with Filter…, and a report filter band showing `(All)`, the one
    Item, or `(Multiple Items)`, so that I report on a subset and can see that I am.
13. As a user, I want several Value Fields side by side (Σ Values), in rows or columns, so that one
    report shows notional and P&L together.
14. As a user, I want to change a Value Field's Aggregation, caption, Show Values As and number
    format, and to be told plainly when a caption is taken, a format cannot be used, or the source
    cannot compute an Aggregation, so that I never get a report quietly different from what I asked
    for.
15. As a user, I want to select, copy and paste the report into Excel as numbers, so that it goes on
    working where I take it.
16. As a user, I want to double-click a value, or use Show Details, to see the records behind it in a
    tab, so that I can check a number the way I would in Excel.
17. As a user of a live screen, I want the values that changed to be marked for a moment, and the
    report to keep my selection when only values change, so that I can watch it.
18. As a user, I want to be told when the report cannot show the newest data, why, and as of when,
    so that I never mistake old numbers for new ones.
19. As a Japanese user, I want the pivot's words to be Excel's Japanese edition's, so that it reads
    like the Excel I know.

### Owning it

20. As a developer, I want to hand ExPivot my records and typed field declarations and nothing more,
    so that adopting it is small.
21. As a developer, I want to feed it a CSV, a database query or an Arrow stream through a Snapshot,
    so that I use the data I have.
22. As a developer whose data is on a server, I want to answer ExPivot's questions there — from a
    Snapshot with the same engine, or from SQL — over my own transport, so that the data never
    leaves the server.
23. As a developer, I want the layout raised to me on every change and accepted back, so that I can
    save a user's reports.
24. As a developer with live data, I want to apply Change Batches, or tell ExPivot my server's data
    moved on, so that the report stays current.
25. As a developer, I want the engine usable on a server with no UI, so that a scheduled report
    computes the same numbers.
26. As a developer, I want to replace every word, so that the pivot speaks my users' language.
27. As a developer on MudBlazor, I want one parameter to dress the pane, the Pivot Toolbar, the
    menus, the panels, the tabs and the report in MudBlazor, following the theme and dark mode, so
    that the pivot looks like the rest of my application.
28. As a developer on another design system, I want to substitute the drawing without
    re-implementing its rules, so that my pivot behaves exactly like everyone else's.
29. As a developer, I want a demo page for each case — basic, CSV, database, live, risk, and a plain
    live grid — so that I can copy what I need.

## Implementation Decisions

All of these are recorded in the ADRs; they are summarised here.

- **Packages** (ADR-0059, ADR-0064, ADR-0065):
  - `ExGrid.Data`, with no dependency;
  - `ExGrid.Data.Arrow` (→ `ExGrid.Data`, → `Apache.Arrow`), optional;
  - `ExPivot.Engine` (→ `ExGrid.Data`);
  - `ExPivot` (→ Engine, → ExGrid);
  - `ExPivot.MudBlazor` (→ ExPivot, → ExGrid.MudBlazor, → MudBlazor).

  None of them is in the ExGrid release; the package check packs them into a feed of their own.
- **The Pivot Source** (ADR-0066).
  - The source offers `Fields` and `Features`, and answers `AggregateAsync`, `ItemsAsync`,
    `DetailsAsync` and `RefreshAsync`, and raises `Changed`.
  - `PivotSource.From(snapshot)` is the reference, and `PivotSource.From(records, fields)` is the
    short path.
  - `PivotSource.Fetch(...)` carries the Consumer's transport.
  - `PivotJson` versions every message.
  - The Source Version pins Items and Details, `MaxLeaves` caps an answer, and the features name the
    Aggregations a source offers.
- **The engine** (ADR-0060).
  - It aggregates a Snapshot's columns and accumulates only the parts that are asked for; Integer and
    Decimal are summed exactly.
  - It keeps the Items' rules, and the Order Key and the date parts.
  - It lays out the Compact, Outline and Tabular forms; subtotals and grand totals; collapse;
    Σ Values; and Show Values As, from the held answer.
  - It computes cells lazily, and applies the caps.
- **Live data** (ADR-0067).
  - The bundled source folds a Change Batch into its answer, and every leaf equals a fresh
    aggregation.
  - The component gathers changes and redraws every 250 ms, keeps the Selection when only values
    change, marks the values that changed, and shows a Stale Report when it must.
- **The Field List and the Pivot Toolbar** (ADR-0061).
  - The rules are pure functions (`PivotLayoutEdits`).
  - Drag and drop uses Blazor's own events, with every target preventing `dragover`'s default.
  - Menus and panels open under their entry, at the pane's width.
  - Defer Layout Update sits at the pane's foot.
  - The Pivot Toolbar holds the report filter band, Layout ▾, Refresh, and the pane's toggle.
  - `IPivotChrome` has one member per surface.
- **Show Details** (ADR-0059) goes to a tab at the foot, a dialog, or the Consumer. The records are
  paged from the source under the report's Source Version.
- **The Wrapper** (ADR-0062): `MudPivotChrome` and `mud-ex-pivot.css`, on `ExGrid.MudBlazor`'s
  `MudExGridPaper`. The ± button stays plain markup.
- **The core's changes** (ADR-0063, ADR-0068): `OnCellDoubleClick`, and the Change Highlight's
  `CellChangedAt`.
- **The demo** (ADR-0069): six pages on both hosts, and a demo API server with SQLite and SignalR.

## Testing Decisions

- **Layer 1** holds the engine and the bundled source to Excel and to themselves, in
  `tests/ExPivot.Engine.Tests`, with one named test per rule.
  - A property test holds live folding to a fresh aggregation.
  - The Pivot Source's answers are held equal across `From`, `Fetch` and the demo's SQL source.
- **Layer 2** holds the component, in `tests/ExPivot.Components`, through the built-in markup and a
  substituted Chrome.
  - It drives the asking with a source that answers on demand, and the time with a fake
    `TimeProvider`.
  - `tests/ExPivot.MudBlazor.Tests` runs the same through MudBlazor's controls.
  - Render counts hold PV-15.
- **Layer 3** (`tests/ExGrid.Browser`) drags in a real browser and runs the six pages under both
  Chromes on both hosts, with the demo API server started by the run.
- **Excel's behaviour was read, not yet observed.** `excel-behaviours.md` lists every reading, to be
  run beside Excel on Windows, as ExSheet's readings were.

## Out of Scope

ADR-0059's "Later" column:

- Σ Values at any position but the innermost;
- collapsing one column Item;
- Excel's Group… command;
- label filters, value filters and Top 10;
- the other Show Values As;
- calculated fields and calculated items;
- a Field List outside the component;
- a dropdown on the report's headers;
- a source that writes its own SQL;
- writeback, which is refused.
