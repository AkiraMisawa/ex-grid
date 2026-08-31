# ExGrid

A tabular UI component for Blazor. The goal is Excel-like operability, packaged so it can be
reused as a screen component inside line-of-business applications.

The name follows the `ag-grid` convention — **the prefix states the product's claim**. Where
`ag` stated "AGnostic" (framework independence), `Ex` states **Excel-like operability**. Naming
a premise instead, as `ko-grid` (Knockout) and `ng-grid` (Angular) did, ages with the premise.

## Language

### The two products

**ExGrid**:
A grid whose main purpose is to **show** large numbers of rows quickly. The data is owned
elsewhere; the grid only reflects it. Virtual scrolling, pinned columns, sorting, filtering and
aggregation are its territory. **This is the one that is specified.**
_Avoid_: DataGrid (fine as a common noun, but the product is ExGrid), table, list, list view

**ExSheet**:
A grid whose main purpose is to reproduce Excel's **editing** behaviour. It owns a mutable cell
model of its own and may have a fill handle, row/column insertion and deletion, and formulas.
**Future.**
_Avoid_: Sheet (fine as a common noun), spreadsheet, worksheet

> **How far do formulas go?** What people call "a formula" splits three ways, and **two of them
> are already possible in ExGrid**.
>
> | | ExGrid | How |
> |---|---|---|
> | Computed column (PV × FX rate converted to a reporting currency) | **Yes** | A `Column` holds a **function** that extracts the value from a row. Compute and return |
> | Total / subtotal rows | **Yes** | **Row Kind** has "total". The Consumer computes the value |
> | A user typing `=A1+B2` | **No** | Requires owning a mutable cell model and a dependency graph. **ExSheet's territory** |
>
> The third one is absent by design, not because the name overreaches — ExGrid does not own the
> data ([ADR-0001](./docs/adr/0001-consumer-pushes-the-window-grid-does-not-fetch.md)). And with
> the real Excel sitting next to it, there is no reason to reimplement a worse formula engine
> (copy round-trips with the raw value preserved,
> [ADR-0005](./docs/adr/0005-copy-refuses-rather-than-truncates.md)).

> The **only** grounds separating ExGrid from ExSheet are **data ownership** (reflecting
> something external versus holding it) and **whether there is a formula engine**. Virtual
> scrolling, selection, keyboard behaviour and the clipboard can be common to both. The
> rendering technique (DOM / Canvas) is not grounds for the split.
> **Neither is cell editing** — if the grid only reports the intent to edit and does not own the
> data, an editable DataGrid is not a contradiction
> ([ADR-0007](./docs/adr/0007-edits-are-an-overlay-owned-by-the-consumer.md)).
> Undo/redo likewise follows from editing, and has nothing to do with data ownership.

### Display structure

**Viewport**:
The rectangle of rows × columns the grid is actually painting. Render cost is decided by what
fits in here, not by the size of the data.
_Avoid_: visible area, visible range, window

**Scrollbar Gutter**:
How much of the declared Viewport its own scrollbars occupy. A classic scrollbar is drawn
**inside** the box the element declares and takes about 15px off that axis; an overlay scrollbar
(macOS) takes nothing. It is neither assumed nor measured — **the browser reports it when it
changes**, and the grid subtracts it before any geometry is computed
([ADR-0013](./docs/adr/0013-fixed-row-height.md),
[ADR-0021](./docs/adr/0021-javascript-is-allowlisted-not-minimised.md)). `ViewportWidth` and
`ViewportHeight` keep meaning the **outer** size.
_Avoid_: scrollbar width, padding, inset (the width is per platform and per user setting, and the
strip is not padding — it belongs to the browser)

**View State**:
The non-data settings a user has applied to a screen — column widths, column order, pinned
columns, sort order, filter conditions. **Being serialisable for external persistence is a
requirement** (the Consumer stores it as a personal setting). **Anything decided automatically
is excluded** — an Auto width is not persisted; only the intent "this column is Auto" is
([ADR-0016](./docs/adr/0016-column-width-and-overflow.md)).
_Avoid_: layout, settings, preferences

**Saved View**:
A named View State. A user keeps several and switches between them.
_Avoid_: preset, template

### The consumer side

**Consumer**:
The application that embeds this component in a screen. The specification is driven from here.
_Avoid_: host, client (a "user" is the human beyond the Consumer)

**Row Model**:
The shape of the data the Consumer hands over as one row.
_Avoid_: record, entity, item

**Row Identity**:
The basis on which a row counts as "the same row". The grid decides whether to repaint from a
**change of identity**, not from a rewrite of contents, so when data changes the Consumer must
**return a different instance** or bump the row's version. An in-place rewrite does not reach the
screen ([ADR-0003](./docs/adr/0003-cells-are-plain-markup-by-default-not-components.md)).
_Avoid_: key, id (Row Identity is the test for sameness, not the value itself)

**Placeholder**:
A row not yet painted with real data. Two reasons, one mechanism — waiting for data from the
Consumer, and deliberate skipping during fast scrolling
([ADR-0004](./docs/adr/0004-cap-the-cells-touched-per-frame.md)).
_Avoid_: skeleton, loading row, dummy row

**Range Request**:
The notification the grid raises to say "I need rows in this range". The grid does not wait, and
does not fetch anything itself. Data arrives when the Consumer hands over a new Window
([ADR-0001](./docs/adr/0001-consumer-pushes-the-window-grid-does-not-fetch.md)).
_Avoid_: fetch, request, load

**Window**:
The contiguous block of rows the Consumer pushes in. Taken wider than the Viewport
(read-ahead). With a pager, one page is the Window — **paging only changes what drives Range
Requests** ([ADR-0015](./docs/adr/0015-paging-is-another-driver-for-range-requests.md)).
_Avoid_: page, chunk, batch

**Row Sequence Version**:
A version identifying the **order** of rows — not their values. The Consumer pushes it, and
**selection is cleared when it changes**. It is not bumped when only values change within the
same set of rows, so selection survives the frequent kind of update
([ADR-0011](./docs/adr/0011-selection-is-rectangles-in-index-space-and-is-dropped-on-reorder.md)).
_Avoid_: data version, generation (this names the order only)

**Grid Source**:
The bundled convenience layer that sits on top of the push interface. It wraps an in-memory
array (`GridSource.From`) or a server query (`GridSource.Fetch`) and takes over holding the
Window and answering Range Requests.
**`GridSource.From` is also the reference implementation of Filter and Sort semantics** — it
defines whether `contains` is case-sensitive and whether nulls sort first or last, and
server-side implementations match it.
_Avoid_: data provider, repository, feed, data source (which suggests the grid pulls)

**Query**:
The whole of what the grid asks for — which range, under which Filter, under which Sort. Being
serialisable is a requirement
([ADR-0002](./docs/adr/0002-filters-are-a-serializable-model-not-linq-expressions.md)).
_Avoid_: request, criteria, conditions

### Filtering

**Filter**:
A structured model of `{column, operator, value}` combined with AND/OR. The condition is that
the grid understands it well enough to render UI for it. Anything the model cannot express goes
alongside as an **Opaque Filter**.
_Avoid_: predicate, narrowing

**Opaque Filter**:
A condition only the Consumer understands. The grid renders no UI for it and passes it straight
through.
_Avoid_: custom filter (confusable with grid-rendered custom UI)

**Filter UI Mode**:
Whether a column's filter can offer a list of values, only conditions, or both. **Declared by
the Consumer in the column definition** — only the Consumer knows the cardinality
([ADR-0009](./docs/adr/0009-filter-panel-contract.md)).
_Avoid_: filter kind, filter type

**Blank**:
The absence of a value as Filter and Sort see it — the Column's value accessor returned null.
Excel's word. A Blank matches only `IsBlank` (and an `In` list that explicitly contains it),
and sorts last in both directions
([ADR-0023](./docs/adr/0023-filter-and-sort-semantics-of-the-reference-implementation.md)).
An empty string is a value, not a Blank.
_Avoid_: null (implementation word, not user-facing), empty (an empty string is a value)

### Rendering and interaction

**Chrome**:
The parts of the grid's own UI that can be substituted — the filter panel, the column menu, the
cell editor, the loading indicator. **It renders and calls back; it does not decide meaning**
(which operators exist, and what a filter means, are the core's). Substituting it does not change
behaviour.
_Avoid_: theme, skin (those name appearance only), template

**Primary Modifier**:
The modifier key that means "add to what is selected" — Ctrl+click adding a range, Ctrl+A
selecting everything. **Control everywhere, and Command as well on an Apple platform**; the
Meta key is the OS's own elsewhere (Win+Arrow snaps a window), so the grid does not answer to
it there. Which platform it is can only be answered by the browser, so it is asked once and
the keyboard and the mouse read the same answer
([ADR-0012](./docs/adr/0012-anchor-focus-and-keyboard-navigation.md)).
_Avoid_: Ctrl (that names one platform's key), Cmd, accel key

**Cell Editor**:
The single input floated over the cell being edited. **Never placed inside the row** — that
breaks row memoisation
([ADR-0010](./docs/adr/0010-chrome-seams-column-menu-editor-loading.md)). This is where
uncommitted text lives.
_Avoid_: input, editing cell

**Overwrite / Caret**:
The two states of cell **editing** (three modes in total, with **Interactive**). **Overwrite** is
entered by typing straight onto a selected cell: the original value is replaced, and **the arrow
keys commit and move to the neighbouring cell**. **Caret** is entered with F2 or a double click:
the original value stays and **the arrow keys move the caret within the text**. The distinction
is Excel's, and without it "type, arrow to the next cell" does not work as continuous entry.
_Avoid_: input mode / edit mode (both read as "editing" and the distinction disappears)

**Interactive**:
The state of being **inside** a cell. Entered with Space and left with Esc, on an Action Column
with several actions or on a Template Column. **The third mode alongside Overwrite / Caret**, and
it matches the ARIA grid pattern.
_Avoid_: focus mode, edit mode (confusable with Caret)

**Cell State**:
The generic vocabulary the grid understands for "this cell is in an unusual state" — normal /
stale / missing / error / modified. The appearance belongs to the theme; any accompanying data
(tooltip text, for instance) stays opaque and is rendered by the Consumer. **The Consumer's own
vocabulary does not enter the grid**
([ADR-0006](./docs/adr/0006-grid-owns-a-generic-cell-state-vocabulary.md)).
_Avoid_: cell status, flag, decoration

**Cell Metadata**:
Information a cell carries that is not the value itself. It affects display and decoration but is
never sorted or aggregated on. Example: an as-of stamp, where two metrics in the *same row* can
come from different batch runs, so it cannot be expressed per row.
**It is not stored on the cell; it is asked for by (row, column)** — an as-of stamp is a function
of (book × metric) and the Consumer can answer it.
_Avoid_: attribute, tag, annotation

**Overflow**:
The state of a value not fitting the column width. **Text is cut with an ellipsis; numbers and
dates become `####`** — truncated text is visibly truncated, whereas a truncated number **looks
like a different, perfectly valid number**
([ADR-0016](./docs/adr/0016-column-width-and-overflow.md)).
_Avoid_: truncation, ellipsis (that names the permitted behaviour, not the state)

### Editing

**Overlay**:
A sparse diff laid over an immutable base. It holds only the overridden columns and never copies
rows. Reset is **deleting an entry** (the original is still in the base, so nothing needs
saving).
_Avoid_: diff, patch, change set, draft

**Edit Intent**:
The notification the grid raises when a user commits an edit — (row identity, column, new value).
The grid changes nothing itself. The screen changes when the Consumer returns new row instances.
_Avoid_: change event, commit, update

### Selection

**Selection**:
The set of cells a user has selected. Held as a **list of rectangles** whose coordinates are
**positions in the current order**, not row identities (the grid does not know identities outside
the Window). Disjoint multi-range selection is supported. **Cleared when the Row Sequence Version
changes** — a sort or filter change that leaves the visible sequence identical keeps it — **and
when the visible-column set changes** (the holder's own trigger; the version names row order only)
([ADR-0011](./docs/adr/0011-selection-is-rectangles-in-index-space-and-is-dropped-on-reorder.md)).
**Rows know nothing about it** — painting is done by an overlay, and it is never mixed into Row
Identity ([ADR-0008](./docs/adr/0008-selection-is-painted-by-an-overlay.md)).
_Avoid_: highlight, active cell (the single point inside a Selection is **Focus**)

**Focus**:
The one cell that keyboard operations start from. The **moving** end of range extension; the
fixed end is the **Anchor**. Enter / Tab cycling moves only the Focus, and **the range stays
selected** ([ADR-0012](./docs/adr/0012-anchor-focus-and-keyboard-navigation.md)). It must always
be visible; if it leaves the Viewport the grid scrolls to it.
_Avoid_: cursor, current cell, selected cell

**Held Selection**:
A Selection paired with the Row Sequence Version it was made under. Reconciling it
against the current version is what drops the selection on reorder — the rule lives in
this pairing, not in each holder's discipline
([ADR-0011](./docs/adr/0011-selection-is-rectangles-in-index-space-and-is-dropped-on-reorder.md)).
_Avoid_: selection snapshot, selection cache (nothing is restored from it)

**Anchor**:
The **fixed** end of range extension. Moved by a click and by Ctrl+click. When there are disjoint
ranges, Shift+arrow extends **the range the Anchor belongs to**. After Ctrl+click deselects a
cell, Anchor and Focus stand **detached** — on that cell, outside every range — and the next
extension starts a new range ([ADR-0012](./docs/adr/0012-anchor-focus-and-keyboard-navigation.md)).
_Avoid_: origin, base cell

### Columns

**Column**:
A runtime object. Beyond the header's appearance it holds **how to extract the value from a
row**, the type (which decides the filter UI and the default format), and the width
(`Auto | Fixed` plus `MinWidth` / `MaxWidth`). A statically listed column and a column generated
from data (each tenor of a tenor ladder) are the same Column, not distinguished.
_Avoid_: field, column definition

**Pinned Column**:
A column held against the Viewport's edge while the others pan under it — Excel's frozen panes.
It stands **outside virtualisation and is always painted**, so it is the landmark that survives
panning sideways, and it **costs directly**: pin enough columns and the resident cell count is
back where virtualisation found it
([ADR-0004](./docs/adr/0004-cap-the-cells-touched-per-frame.md)). Which columns are pinned is
**View State**, not a property of the Column — combined with the column order the Consumer owns,
"the leading N" expresses it.
_Avoid_: frozen, sticky, locked (those name the mechanism or Excel's wording, not the state)

**Row Kind**:
What a row represents — detail / group / total. **Distinct from Cell State**: that names the
state of a value per cell, this names the role of a row. Needed to paint group rows differently
from detail rows and to decide what expands and collapses in pivot-like views
([ADR-0013](./docs/adr/0013-fixed-row-height.md)).
_Avoid_: row type, level, hierarchy (Row Kind is the role, not the depth)

**Action Column**:
A column whose cells carry **declared actions**. The Consumer supplies the icon or label, but the
meaning — "pressing this fires something" — belongs to the core. **Painted as plain markup and
adds no component boundary**
([ADR-0020](./docs/adr/0020-action-and-template-columns.md)).
_Avoid_: button column, command column

**Template Column**:
A column whose cell contents the Consumer paints with arbitrary markup. **The cell becomes a
component, so it costs more** — which is why it is opt-in per column. A value accessor is
**still required** (sorting and filtering need it).
_Avoid_: custom column, render column

## Flagged ambiguities

- **"Grid" on its own does not say whether ExGrid or ExSheet is meant.** When it is ambiguous,
  always commit to one. To mean both, write "the Ex family". Do not invent a single umbrella
  noun (`ag-grid` has none either).
- **"User" gets used two ways** — the developer embedding this component, and the end user
  touching the screen. The former is the **Consumer**; the latter is the **user**.

## Example: a conversation between a Consumer developer and the component designer

> **Consumer developer**: I want this on our position screen. What do I hand over as rows?
>
> **Designer**: The **Window** you want on screen right now. The grid does not fetch anything. If
> it needs more it raises a **Range Request** saying which range, and you hand over the next
> Window.
>
> **Consumer developer**: Isn't that a nuisance? Let me just pass an array.
>
> **Designer**: Then pass `GridSource.From(rows)`. The bundled **Grid Source** manages the Window
> for you. For server paging, `GridSource.Fetch(...)`. Use the bare interface only when you are
> on a state-management library and want to hold everything yourself.
>
> **Consumer developer**: If I click the sort in a column header, does it sort?
>
> **Designer**: **The grid does not sort.** It tells you the header was clicked, and you hand
> back the sorted result as a Window. When there are overridden values, you are the only one who
> can order them correctly — you know both the base and the diff.
>
> **Consumer developer**: When the batch runs and values update, does rewriting the row objects
> update the screen?
>
> **Designer**: **No.** The grid judges by **Row Identity** and does not look at rewritten
> contents. Return different instances, or bump the row version. Snapshots that are immutable per
> feed version fit this directly.
>
> **Consumer developer**: And when a user edits a cell?
>
> **Designer**: The grid raises an **Edit Intent** and changes nothing. You record it in the
> **Overlay** and hand back a new Window with it applied. Use the bundled helper to apply it —
> hand-writing it and rewriting in place produces the failure where **the cell is coloured as
> "modified" while the value is still the old one**.
>
> **Consumer developer**: Within one row, one metric can be intraday while another is
> close-of-business. Do I put that on the row?
>
> **Designer**: Not on the row. **Cell Metadata** is **asked for** by (row, column), and the grid
> asks when it needs it. What you return is a **Cell State** — normal / stale / missing / error /
> modified. The timestamp itself goes in the accompanying data, on the tooltip.
>
> **Consumer developer**: Will it handle a million rows?
>
> **Designer**: The row count itself does not reach the grid — render cost is decided by what
> fits in the **Viewport**, and the grid neither sorts nor filters. What does reach it is the
> **column** count. Twenty visible columns is comfortable; fifty without horizontal
> virtualisation drops below 60fps.
