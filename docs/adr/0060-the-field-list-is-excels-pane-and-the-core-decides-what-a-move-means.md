# The Field List is Excel's pane, and ExPivot decides what every move in it means

*(Proposed 2026-09-30 with [ADR-0058](./0058-expivot-is-a-pivot-table-drawn-by-exgrid-as-its-consumer.md),
and not yet decided with the user.)*

The **Field List** is where a user builds a report: Excel's "PivotTable Fields" pane. It is
ExPivot's own UI, not ExGrid's, and it stands beside the report, on the right by default. At the
top, every declared Pivot Field with a checkbox and a search; below, the four **Areas** — Filters,
Columns, Rows, Values — each listing the fields placed in it.

## What each gesture means

The rules are the engine's functions (`PivotLayoutEdits`), so the component, a substituted Chrome
and a server apply the same ones ([ADR-0059](./0059-the-pivot-engine-answers-as-excels-pivottable-and-is-the-reference.md)).

- **Ticking a field** that stands nowhere places it where Excel does: a field declared Number goes
  to Values, with its default Aggregation; any other goes to the end of Rows.
- **Unticking a field** removes it from every Area it stands in, each of its Value Fields included.
- **A field stands at most once across Filters, Rows and Columns.** Placing it in one of them moves
  it out of the other two, and **its settings travel with it** — its Hidden Items, order, subtotals
  and collapse state — as a PivotField's do in Excel.
- **A field may stand in Values any number of times**, each a Value Field of its own, and also in
  one of the other Areas: `Region` in Rows and `Count of Region` in Values is an ordinary report.
- **Dragging** a field from the list, or a placed entry from an Area, and dropping it on an Area
  places it at the position it was dropped: a move between Areas, a reorder within one. A field
  dropped on Values from the list is a new Value Field; an entry dropped on Values from another Area
  moves there, and a Value Field dropped on another Area moves there too, as Excel does.
- **Dropping an entry back on the list of fields removes it**, as dragging out of the Areas does in
  Excel.
- **Σ Values** appears in Columns when a second Value Field is placed, and leaves when fewer than
  two remain. It moves between Rows and Columns only, and in the first version it always stands
  innermost in its Area (ADR-0059): a drop anywhere in the other Area moves it there, last.
- **Hiding every Item of a field is refused**, and a Value Field's caption that another already
  carries is refused (ADR-0059).

**Each placed entry has a menu**, Excel's: Move Up, Move Down, Move to Beginning, Move to End; Move
to Report Filter, Row Labels, Column Labels, Values; Remove Field; and Field Settings… or Value Field
Settings…. A field in Rows or Columns also carries the commands Excel puts on its dropdown in the
report — Sort A to Z, Sort Z to A, Filter…, Expand and Collapse Entire Field — because the report's
headers here carry no dropdown (ADR-0058). A field in Filters carries Filter…. **A command that
would change nothing is disabled**, never offered and ignored.

- **Field Settings…** sets a row or column field's subtotals (Automatic or None) and its order (by
  label, ascending or descending, or by a Value Field).
- **Value Field Settings…** sets the caption, the Aggregation, Show Values As and the number format
  (a .NET format string, shown on a sample before it is applied; one that cannot format is refused).
- **Filter…** lists the field's Items, every one of them, in the field's order, each ticked while it
  is shown, with (Select All) and a search that narrows the list. The search only narrows what is
  listed; applying keeps every tick as it stands. *(Excel's search replaces the filter with its
  results unless "Add current selection to filter" is ticked. That rule is later.)* At most 10,000
  Items are listed at once, and beyond that the search narrows them: painting a million checkboxes
  is not something to execute (principle 5 puts the cap on what cannot be).

**The report filter band.** Each field in Filters is shown above the report, as Excel shows its
page fields: its caption and `(All)`, the one Item shown, or `(Multiple Items)`. Its button opens
Filter… there.

**Every change is a new Pivot Layout**, applied at once and raised through `LayoutChanged`
(ADR-0058). A panel's edits are a draft the core holds until OK; Cancel and Escape discard it.

## Drag and drop without new JavaScript

The drag uses Blazor's own drag events — `dragstart`, `dragenter`, `drop`, `dragend` — and
`dragover`'s default is prevented by a directive, not a handler. These are the framework's, as
`onclick` is, and add nothing to [ADR-0021](./0021-javascript-is-allowlisted-not-minimised.md)'s
list. On a Server circuit a drag is a few messages, not one per frame, because `dragover` has no
handler. A drop target that cannot take what is dragged does not prevent `dragover`'s default, and
a drop that still arrives is checked by the rules above and changes nothing.

**Every drag has a keyboard route**: the entry's menu moves it, and a field's checkbox places it.
The target browsers are Chromium only ([ADR-0017](./0017-target-chromium-browsers-only.md)), which
start a drag without data set on `dragstart`; what is dragged is held in C#, never in the
`DataTransfer`.

## Menus and panels open in place

ExPivot measures nothing (the rule ADR-0021 states for the grid). **A menu or a panel opens
directly under the entry that opened it, as wide as the pane and over what follows**, as Excel's
menu drops down over its pane. Its width is the pane's; its height on the page is its static
position — an absolutely positioned box whose top is left to the browser stands where the flow would
have put it — so nothing is measured and nothing needs a position ExPivot computes. The pane
scrolls, nothing is clipped by it, and a menu's first command scrolls into view as it takes the
keyboard. A Chrome places the frame under its entry and puts no positioned element of its own
between them and the Field List, or the frame would take that element's width instead.

*Revised 2026-09-30, before it was decided.* The first version opened a menu or panel in the flow,
pushing what follows down. Seen in a browser, a panel under an entry was only as wide as that
entry's Area — half the pane — and Value Field Settings cut its words to "Summarize v…"; and it
pushed the Areas below it out of the pane's view, where a drag cannot reach them. The static
position keeps what the in-flow version was for, nothing measured and nothing clipped, and drops
both.

The report filter band's Filter… opens under the band, over the report, with a backdrop that closes
it on a press elsewhere. Escape, Cancel and the backdrop close it; the keyboard goes back to the entry
that opened it. One menu or panel is open at a time in each ExPivot, and two ExPivots on a page
share nothing ([ADR-0018](./0018-multiple-instances-must-be-independent.md)).

## The seam: Chrome renders and calls back

ExPivot's Chrome, `IPivotChrome`, has one member per surface — the Field List, the report filter
band, a menu, Filter…, Field Settings…, Value Field Settings… — and each returns the content to
draw, or null for ExPivot's built-in plain markup. It is ADR-0010's shape:

- **ExPivot owns** the rules, every piece of state (the open menu, a panel's draft, what is being
  dragged), the frames of menus and panels (where they open, what closes them, their role and
  name) and the words.
- **The Chrome draws** what it is handed and calls back: the commands with their `Enabled` states,
  the drafts with their setters, the drop targets with what each accepts. It decides nothing, and
  **swapping it changes no behaviour**.
- **The report's grid has its own Chrome**, ExGrid's `IGridChrome`. An `IPivotChrome` may supply
  one too, so that one Chrome dresses both; a `Chrome` written on ExPivot beats it. ExPivot hands
  it the words of its Context Menu commands — the Consumer's through ExPivot's `Label`, then
  ExPivot's English, null for an id that is not ExPivot's — because a grid Chrome that words its
  menus itself never consults the grid's `CommandLabel` (ADR-0036), and without them it would paint
  `remove-named-field:Region` where the built-in says `Remove "Region"`. ExPivot asks once per
  Pivot Chrome and holds the answer: a grid Chrome made per render would be a new parameter every
  time, and the grid would re-render with each
  ([ADR-0003](./0003-cells-are-plain-markup-by-default-not-components.md)).
- A Chrome's content takes DOM focus when the core's `FocusRequest` asks, as the grid's popovers do
  ([ADR-0039](./0039-a-popover-takes-the-keyboard-and-may-hold-popups-of-its-own.md)), and a design
  system's own popup inside a panel — a select's options — is an Inner Popup of that panel.

## Consequences

- **The Field List's rules are layer-1 tests**, against the engine's functions; the component's
  layer-2 tests drive them through the built-in markup and through a substituted Chrome, and expect
  the same layouts.
- **`ShowFieldList="false"`** leaves the pane out, for a report whose layout the Consumer fixes;
  the Context Menu then offers Show Field List, as Excel's does.
- **Layer 3 drags in a real browser**, because only a browser can say that a drop lands where the
  pointer was.
