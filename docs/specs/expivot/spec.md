# ExPivot, first version

Status: ready-for-human

Proposed by [ADR-0058](../../adr/0058-expivot-is-a-pivot-table-drawn-by-exgrid-as-its-consumer.md)
to [ADR-0062](../../adr/0062-what-expivot-asks-of-exgrids-core.md), which are **not yet decided with
the user**: the status says so, and the first thing to do with this spec is to put those five ADRs
in front of them. The vocabulary is `CONTEXT.md`'s "Pivots" section. The exit criteria are §29 of
`docs/definition-of-done.md`, and DC-52 in §26 for the core. This spec synthesises those decisions;
where it and they disagree, they win.

## Problem Statement

A user of a line-of-business application wants to ask their data a question the screen was not
built for: P&L by desk and product, notional by month and currency, how many trades are still
unconfirmed per book. Today they export the rows to Excel, build a PivotTable there, and read the
answer in a file that has left the application — stale by the time anyone reads it, and with no
link back to the records behind each number.

ExGrid cannot answer it. It shows rows the application owns and reports what the user does; it
neither holds nor groups nor aggregates, by design (ADR-0001, the spine's third principle). A
developer who wants a pivot inside the application embeds a third-party one, whose keyboard,
selection, clipboard and look differ from the ExGrid beside it.

## Solution

**ExPivot**: Excel's PivotTable, drawn by one ExGrid as that grid's Consumer (ADR-0058).

- The application hands ExPivot its **Source Records** and declares the **Pivot Fields** they
  carry. The user builds a report in the **Field List** — Excel's "PivotTable Fields" pane — by
  ticking fields or dragging them between **Filters, Columns, Rows and Values**, and reads it with
  ExGrid's Selection, keyboard and clipboard.
- The numbers are Excel's: `ExPivot.Engine` aggregates with Excel's semantics (ADR-0059), money is
  summed exactly, a total comes from its records, and a number that does not fit is `####`.
- The **Pivot Layout** is View State: the application stores it, as JSON if it likes, and hands it
  back.
- **Show Details** hands the application the records behind a value, for it to show where it shows
  records.
- **`ExPivot.MudBlazor`** dresses the whole of it in MudBlazor (ADR-0061).

## User Stories

### Building a report

1. As a user, I want to see every field my data carries, with a checkbox, so that I know what I can ask about.
2. As a user, I want to tick a field and have it go where Excel puts it — a number into Values as a Sum, anything else into Rows — so that a first report takes two clicks.
3. As a user, I want to drag a field onto Filters, Columns, Rows or Values, so that I place it exactly.
4. As a user, I want to drag an entry within its Area to reorder it, to another Area to move it, and back onto the list of fields to remove it, so that rearranging a report is direct.
5. As a user, I want every drag to have a keyboard route — each entry's menu, and each field's checkbox — so that I can build a report without a mouse.
6. As a user, I want each placed field's menu to offer Excel's commands, with the ones that would change nothing disabled, so that I never wonder why a command did nothing.
7. As a user, I want to search the list of fields, so that a record with many fields is usable.

### Reading a report

8. As a user, I want the report laid out as Excel's Compact form by default, with Outline and Tabular available, so that it reads as I expect.
9. As a user, I want subtotals and grand totals, and to turn them off per field, so that I see the totals I need.
10. As a user, I want to collapse and expand an Item with its `−`/`+` button, the Context Menu or a double click on its label, and to collapse a whole field, so that I can drill in and out.
11. As a user, I want to sort a field's Items by label or by a Value Field, so that the biggest numbers come first.
12. As a user, I want to hide Items with Filter…, and a report filter above the report showing `(All)`, the one Item, or `(Multiple Items)`, so that I report on a subset and can see that I am.
13. As a user, I want several Value Fields side by side (Σ Values), in rows or columns, so that one report shows notional and P&L together.
14. As a user, I want to change a Value Field's Aggregation, its caption, Show Values As and its number format in Value Field Settings…, and to be told plainly when a caption is taken or a format cannot be used, so that I never get a report that is quietly different from what I asked for.
15. As a user, I want to select, copy and paste the report into Excel as numbers, so that it goes on working where I take it.
16. As a user, I want to double-click a value, or use Show Details, to see the records behind it, so that I can check a number.

### Owning it

17. As a developer, I want to hand ExPivot my records and field declarations and nothing more, so that adopting it is small.
18. As a developer, I want the layout raised to me on every change and accepted back, so that I can save a user's reports.
19. As a developer, I want a new list of records to refresh the report and keep the user's Selection when only values changed, so that a live screen stays usable.
20. As a developer, I want the engine usable on a server with no UI, so that a scheduled report computes the same numbers.
21. As a developer, I want to replace every word, so that the pivot speaks my users' language.
22. As a developer on MudBlazor, I want one parameter to dress the pane, the menus, the panels and the report in MudBlazor, following the theme and dark mode, so that the pivot looks like the rest of my application.
23. As a developer on another design system, I want to substitute the Field List's drawing without re-implementing its rules, so that my pivot behaves exactly like everyone else's.

## Implementation Decisions

All recorded in the ADRs; summarised here.

- **Three packages** (ADR-0058): `ExPivot.Engine` (no dependency), `ExPivot` (→ Engine, → ExGrid),
  `ExPivot.MudBlazor` (→ ExPivot, → ExGrid.MudBlazor, → MudBlazor). Not part of the ExGrid release;
  the package check packs them into a feed of their own.
- **The report is one ExGrid** (ADR-0058): the whole Pivot Report is its Window; label columns are
  pinned Template Columns carrying the indent and the `±` button; column Items are Header Groups;
  group and total rows are Row Kinds; nothing is Editable; the header click selects; there is no
  column menu, sort or filter; copy is ExGrid's, with the full-precision number in the raw form.
- **The engine** (ADR-0059): Items, their order and Hidden Items; the eleven Aggregations with an
  exact `decimal` path and a `double` fallback; totals from records; Compact, Outline and Tabular;
  subtotals top, bottom or off; grand totals; collapsed Items; Σ Values innermost; Show Values As;
  captions; a cube kept across re-layouts; lazy cells; caps on number formats.
- **The Field List** (ADR-0060): its rules are pure functions (`PivotLayoutEdits`); drag and drop is
  Blazor's own events, with what is dragged held in C#; menus and panels open under their entry,
  as wide as the pane, with drafts held by ExPivot until OK; one at a time; the `IPivotChrome` seam,
  with the built-in markup as the fallback and the report grid's Chrome supplied through it.
- **The Wrapper** (ADR-0061): `MudPivotChrome` and `mud-ex-pivot.css`, on `ExGrid.MudBlazor`'s
  `MudExGridPaper`; no forwarding component; the `±` stays plain markup.
- **The one core change** (ADR-0062): `OnCellDoubleClick`, raised where no edit opens; a double
  click on a control in an Action, Template or Mark cell stops at that cell.

## Testing Decisions

- **Layer 1** holds the engine to Excel: `tests/ExPivot.Engine.Tests`, one named test per rule, the
  Aggregation tables transcribed from ADR-0059.
- **Layer 2** holds the component: `tests/ExPivot.Components` through the built-in markup and a
  substituted Chrome, `tests/ExPivot.MudBlazor.Tests` through MudBlazor's controls, and the same
  gestures must make the same layout under each (PV-9). Render counts hold PV-15.
- **Layer 3** (`tests/ExGrid.Browser/pivot.spec.mjs`) drags in a real browser — only a browser can
  say a drop lands — and runs `/pivot` under both Chromes on both hosts, with a MudBlazor list's
  Escape and both schemes.
- **Excel's behaviour was read, not yet observed.** `excel-behaviours.md` lists every reading, to be
  run beside Excel on Windows as ExSheet's were.

## Out of Scope

ADR-0058's "Later" column: Σ Values at any position but the innermost; collapsing one column Item;
grouping dates and numbers; label and value filters and Top 10; the other Show Values As;
calculated fields and items; Defer Layout Update; a Field List outside the component; a pivot query
answered by the Consumer (reserved, with its trigger); and writeback, which is refused.
