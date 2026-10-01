# The Field List is Excel's pane, and ExPivot decides what every move in it means

*(Proposed 2026-09-30 with [ADR-0059](./0059-expivot-is-a-pivot-table-drawn-by-exgrid-as-its-consumer.md),
and decided with the user the same day. The grilling kept the pane and its rules as built. It added
three things, each marked **Changed when decided** below:*

- *the toolbar above the report, with the Layout menu and the pane's toggle (Q4, Q17);*
- *Defer Layout Update (Q14);*
- *a correction to how a drop target says what it accepts, which brings the text in line with what
  was built.)*

The **Field List** is where a user builds a report: Excel's "PivotTable Fields" pane. It is
ExPivot's own UI, not ExGrid's, and it stands beside the report, on the right by default.

- **At the top**, it lists every Pivot Field the source offers, each with a checkbox, and a search.
- **Below that**, it shows the four **Areas** — Filters, Columns, Rows and Values — each listing the
  fields placed in it.
- **At the foot**, it has Excel's **Defer Layout Update** checkbox and its **Update** button.

## What each gesture means

The rules are the engine's functions (`PivotLayoutEdits`), so the component, a substituted Chrome
and a server all apply the same ones
([ADR-0060](./0060-the-pivot-engine-answers-as-excels-pivottable-and-is-the-reference.md)).

- **Ticking a field** that stands nowhere places it where Excel does. A field declared Number goes
  to Values, with its default Aggregation. Any other field goes to the end of Rows.
- **Unticking a field** removes it from every Area it stands in, each of its Value Fields included.
- **A field stands at most once across Filters, Rows and Columns.** Placing it in one of them moves
  it out of the other two, and **its settings travel with it** — its Hidden Items, order, subtotals
  and collapse state — as a PivotField's do in Excel.
- **A field may stand in Values any number of times**, each time as a Value Field of its own, and
  also in one of the other Areas. `Region` in Rows with `Count of Region` in Values is an ordinary
  report.
- **Dragging** a field from the list, or a placed entry from an Area, and dropping it on an Area
  places it where it was dropped. That is a move between Areas, or a reorder within one.
  - A field dropped on Values from the list becomes a new Value Field.
  - An entry dropped on Values from another Area moves there.
  - A Value Field dropped on another Area moves there too, as Excel does.
- **Dropping an entry back on the list of fields removes it**, as dragging out of the Areas does in
  Excel.
- **Σ Values** appears in Columns when a second Value Field is placed, and leaves when fewer than two
  remain.
  - It moves between Rows and Columns only.
  - In the first version, it always stands innermost in its Area (ADR-0060), so a drop anywhere in
    the other Area moves it there, last.
- **Some edits are refused**: hiding every Item of a field, and giving a Value Field a caption that
  another already carries (ADR-0060).

**Each placed entry has a menu, Excel's.**

- It offers Move Up, Move Down, Move to Beginning and Move to End.
- It offers Move to Report Filter, to Row Labels, to Column Labels and to Values.
- It offers Remove Field, and Field Settings… or Value Field Settings….
- **A field in Rows or Columns also carries the commands Excel puts on its dropdown in the report**:
  Sort A to Z, Sort Z to A, Filter…, and Expand and Collapse Entire Field. The report's headers here
  carry no dropdown (ADR-0059).
- A field in Filters carries Filter….
- **A command that would change nothing is disabled.** It is never offered and then ignored.

The panels:

- **Field Settings…** sets a row or column field's subtotals (Automatic or None) and its order: by
  label, ascending or descending, or by a Value Field.
- **Value Field Settings…** sets the caption, the Aggregation, Show Values As and the number format.
  - The number format is a .NET format string. It is shown on a sample before it is applied, and one
    that cannot format is refused.
  - **An Aggregation the Pivot Source does not answer is offered disabled, with the reason**
    ([ADR-0066](./0066-expivot-asks-a-pivot-source-and-a-server-answers-with-leaf-aggregates.md)).
- **Filter…** lists all of the field's Items, in the field's order, each ticked while it is shown,
  with (Select All) and a search that narrows the list.
  - The search only narrows what is listed. Applying keeps every tick as it stands. *(Excel's search
    replaces the filter with its results unless "Add current selection to filter" is ticked. That
    rule is later.)*
  - At most 10,000 Items are listed at once, and beyond that the search narrows them. Painting a
    million checkboxes is not something to execute: principle 5 puts the cap on what cannot be.
  - The Items come from the source, under the report's Source Version (ADR-0066).

**Every change is a new Pivot Layout**, applied at once and raised through `LayoutChanged`
(ADR-0059). A panel's edits are a draft the core holds until OK; Cancel and Escape discard it.

**Defer Layout Update.** *Changed when decided* (Q14). The proposal left it for later, "measured
first". The measurement came: over a million records, one layout change took the first engine 7 to
11 s in the browser. **While the checkbox is ticked, the pane's changes build a pending layout that
the report does not follow.**

- The pane shows the pending layout.
- The report stays on the layout it has, and no question is asked of the source.
- **Update** applies the pending layout in one change.
- Unticking the checkbox applies the pending layout too, as Excel does.

`LayoutChanged` is raised for the layout the report shows, never for a pending one.

## The toolbar above the report

*Changed when decided* (Q4, Q17). The engine held every report-wide setting from the start, but
nothing on screen changed them: Excel's Design tab had no counterpart, and only the Consumer's code
could set a form. The user also asked that the pane be shown and hidden by the user. A menu in the
pane's heading would be out of reach while the pane was hidden, so **the settings stand in a toolbar
above the report instead.**

- **On the left is the report filter band.** Each field in Filters is shown as Excel shows its page
  fields: its caption, followed by `(All)`, the one Item shown, or `(Multiple Items)`. Its button
  opens Filter… under the band.
- **On the right, in this order:**
  - **Layout ▾**, a menu of Excel's Design tab settings, under Excel's names: Subtotals (Do Not Show,
    Show at Bottom, Show at Top), Grand Totals (Off for Rows and Columns, On for Rows and Columns, On
    for Rows Only, On for Columns Only), and Report Layout (Compact, Outline, Tabular, Repeat All
    Item Labels, Do Not Repeat Item Labels).
    - Excel's Blank Rows is left out: the engine has no blank row.
    - The menu works as a placed entry's menu does: the current choice is marked, and a choice that
      would change nothing is disabled. Report Layout's label choices, for example, are disabled in
      the Compact form, which has no outer label columns to repeat into.
  - **Refresh**, when the source can be asked again (ADR-0066).
  - **The Field List's toggle.** The pane's visibility is a parameter the Consumer can bind
    (`@bind-ShowFieldList`), so the Consumer can remember it. The Context Menu still offers
    Show / Hide Field List, as Excel's does.
- **Under the toolbar**, when there is one, is the Stale Report's notice
  ([ADR-0067](./0067-live-data-a-change-batch-makes-the-next-snapshot-and-expivot-folds-it-in.md)),
  with Retry.

## Drag and drop without new JavaScript

The drag uses Blazor's own drag events: `dragstart`, `dragenter`, `drop` and `dragend`. `dragover`'s
default is prevented by a directive, not by a handler. These are the framework's events, as
`onclick` is, and they add nothing to
[ADR-0021](./0021-javascript-is-allowlisted-not-minimised.md)'s list. On a Server circuit a drag is
therefore a few messages, not one per frame, because `dragover` has no handler.

*Changed when decided:* **every drop target prevents `dragover`'s default, always**, and a drop that
means nothing changes nothing. **Whether a target takes what is being dragged is shown by how the
target looks.**

The proposal had a target that cannot take the dragged item leave the default alone, so the browser
would show its no-drop cursor. That turned out to be wrong on a Server circuit. Whether to prevent
the default is decided when the page renders, and the render that follows a `dragstart` arrives a
round trip later. For that round trip, the targets would still carry the previous drag's answer, and
a drop there would be lost. The build did what this paragraph now says, and the rules above still
check every drop that arrives.

**Every drag has a keyboard route**: the entry's menu moves it, and a field's checkbox places it.
The target browsers are Chromium only
([ADR-0017](./0017-target-chromium-browsers-only.md)), which start a drag without data set on
`dragstart`. What is dragged is held in C#, never in the `DataTransfer`.

## Menus and panels open in place

ExPivot measures nothing (the rule ADR-0021 states for the grid). **A menu or a panel opens directly
under the entry that opened it, as wide as the pane and over what follows**, as Excel's menu drops
down over its pane.

- Its width is the pane's.
- Its height on the page is its static position. An absolutely positioned box whose top is left to
  the browser stands where the flow would have put it, so nothing is measured and nothing needs a
  position ExPivot computes.
- The pane scrolls, and nothing is clipped by it.
- A menu's first command scrolls into view as the menu takes the keyboard.
- A Chrome places the frame under its entry, and puts no positioned element of its own between the
  frame and the Field List. Otherwise the frame would take that element's width instead.

*Revised 2026-09-30, before it was decided.* The first version opened a menu or panel in the flow,
pushing what follows down. Seen in a browser, that failed in two ways:

- A panel under an entry was only as wide as that entry's Area, which is half the pane. Value Field
  Settings cut its words to "Summarize v…".
- It pushed the Areas below it out of the pane's view, where a drag cannot reach them.

The static position keeps what the in-flow version was for — nothing measured and nothing clipped —
and drops both failures.

**The toolbar's popups** — the report filter band's Filter… and the Layout menu — open under the
toolbar, over the report, with a backdrop that closes them on a press elsewhere.

- Escape, Cancel and the backdrop close them.
- The keyboard goes back to the button that opened them.
- One menu or panel is open at a time in each ExPivot, and two ExPivots on one page share nothing
  ([ADR-0018](./0018-multiple-instances-must-be-independent.md)).

## The seam: Chrome renders and calls back

ExPivot's Chrome, `IPivotChrome`, has one member per surface. Each member returns the content to
draw, or null for ExPivot's built-in plain markup. The surfaces are:

- the Field List;
- the toolbar with its report filter band;
- a menu, which also serves the Layout menu;
- Filter…, Field Settings… and Value Field Settings…;
- the Details tabs;
- the Stale Report's notice.

It is [ADR-0010](./0010-chrome-seams-column-menu-editor-loading.md)'s shape:

- **ExPivot owns** the rules and every piece of state: the open menu, a panel's draft, what is being
  dragged, the pending layout, and the pane's visibility. It also owns the frames of menus and
  panels — where they open, what closes them, their role and their name — and the words.
- **The Chrome draws** what it is handed, and calls back. It is handed the commands with their
  `Enabled` states, the drafts with their setters, and the drop targets with what each accepts. It
  decides nothing, and **swapping it changes no behaviour**.
- **The report's grid has its own Chrome, ExGrid's `IGridChrome`.**
  - An `IPivotChrome` may supply one too, so that one Chrome dresses both. A `Chrome` written on
    ExPivot beats it.
  - ExPivot hands that grid Chrome the words of its Context Menu commands: the Consumer's through
    ExPivot's `Label` first, then ExPivot's own words, and null for an id that is not ExPivot's.
  - It must, because a grid Chrome that words its menus itself never consults the grid's
    `CommandLabel` (ADR-0036). Without the words, it would paint `remove-named-field:Region` where
    the built-in markup says `Remove "Region"`.
  - ExPivot asks once per Pivot Chrome, and holds the answer. A grid Chrome made on each render
    would be a new parameter every time, and the grid would re-render with each one
    ([ADR-0003](./0003-cells-are-plain-markup-by-default-not-components.md)).
- **A Chrome's content takes DOM focus when the core's `FocusRequest` asks**, as the grid's popovers
  do ([ADR-0039](./0039-a-popover-takes-the-keyboard-and-may-hold-popups-of-its-own.md)).
- **A design system's own popup inside a panel**, such as a select's options, is an Inner Popup of
  that panel.

## Consequences

- **The Field List's rules, the Layout menu's choices and Defer Layout Update are layer-1 tests**,
  against the engine's functions. The component's layer-2 tests drive the same rules through the
  built-in markup and through a substituted Chrome, and expect the same layouts.
- **`ShowFieldList="false"`** leaves the pane out, for a report whose layout the Consumer fixes. The
  toolbar's toggle and the Context Menu bring the pane back.
- **Layer 3 drags in a real browser**, because only a browser can say that a drop lands where the
  pointer was.
