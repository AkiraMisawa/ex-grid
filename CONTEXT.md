# ExGrid

A tabular UI component for Blazor. The goal is Excel-like operability, packaged so it can be
reused as a screen component inside line-of-business applications.

The name follows the `ag-grid` convention — **the prefix states the product's claim**. Where
`ag` stated "AGnostic" (framework independence), `Ex` states **Excel-like operability**. Naming
a premise instead, as `ko-grid` (Knockout) and `ng-grid` (Angular) did, ages with the premise.

## Language

### The products

**ExGrid**:
A grid whose main purpose is to **show** large numbers of rows quickly. The data is owned
elsewhere; the grid only reflects it. Virtual scrolling, pinned columns, sorting, filtering and
aggregation are its territory. **This is the one that is specified.**
_Avoid_: DataGrid (fine as a common noun, but the product is ExGrid), table, list, list view

**ExSheet**:
A grid whose main purpose is to reproduce Excel's **editing** behaviour, for general use rather
than for one screen's data. It holds a **Sheet** — cells addressed `A1`, their **Entries**, and
a formula engine that computes their **Values** — and it is drawn by ExGrid, as that grid's
**Consumer**: ExSheet holds and computes, ExGrid paints and reports. It may have a fill handle,
row/column insertion and deletion, and **Formulas**. **Being specified** ([ADR-0046](./docs/adr/0046-exsheet-is-a-general-purpose-sheet-drawn-by-exgrid-as-its-consumer.md)).
_Avoid_: spreadsheet, worksheet, and **Sheet**, which names what ExSheet holds, not the product

**ExPivot**:
Excel's PivotTable inside the application. The Consumer gives it a **Pivot Source** over the
**Source Records** and declares their **Pivot Fields**; the user places Pivot Fields into **Areas**
through the **Field List** to define the **Pivot Report**. It is drawn by ExGrid, as that grid's
**Consumer**: ExPivot holds the **Pivot Layout** and supplies the report, ExGrid paints and reports.
**Decided** with the user, 2026-09-30
([ADR-0059](./docs/adr/0059-expivot-is-a-pivot-table-drawn-by-exgrid-as-its-consumer.md)); local and
server reports follow the same meaning
([ADR-0151](./docs/adr/0151-server-pivots-send-report-windows-and-share-the-local-engine.md)).
_Avoid_: pivot grid, OLAP grid, cube (the cube is how the engine keeps what it aggregated, not the
product), and **Pivot Report**, which names what ExPivot computes, not the product

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
> [ADR-0005](./docs/adr/0005-copy-refuses-rather-than-truncates.md)). ExSheet answers that sentence
> for itself — it lives inside the application, and its Formulas read the application's data
> ([ADR-0046](./docs/adr/0046-exsheet-is-a-general-purpose-sheet-drawn-by-exgrid-as-its-consumer.md)).

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

**Stretch**:
A Viewport axis taken from the parent's box instead of declared as a number. The browser reports
the size, and the parent must have a definite size on that axis
([ADR-0028](./docs/adr/0028-geometry-is-resolved-once-density-is-only-a-preset.md)). Named
**Fill** until 2026-09-26, when that word went to Excel's gesture.
_Avoid_: Fill (that is the editing gesture), auto (that is a column width), 100%, responsive
(that is ADR-0045's "following the box")

**Layout Ceiling**:
The tallest element the browser will lay out, in CSS pixels. It is 2²⁵ px divided by the display
scale and the page zoom, so it shrinks at 150%. Above it the grid compresses its scroll height
([ADR-0053](./docs/adr/0053-the-scroll-height-is-compressed-above-the-browsers-layout-ceiling.md)).
It is reported by the browser, like the Scrollbar Gutter.
_Avoid_: max height, scroll limit (VZ-8's refusal is a different, fixed ceiling)

**Device Pixel**:
One point of the screen. How many of them a CSS pixel covers depends on the display scale and the
page zoom: one and a half at 150%, two and a quarter at 150% zoomed to 150%. Excel's lines are
counted in them, so a thin line is one Device Pixel at every scale. The browser reports it when it
changes, and the grid puts its column edges on it
([ADR-0090](./docs/adr/0090-the-grid-is-told-its-device-pixel-and-puts-column-edges-on-it.md)).
_Avoid_: screen pixel, physical pixel, pixel on its own (a pixel with no qualifier is a CSS pixel)

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
_Avoid_: key, id (Row Identity is the test for sameness, not the value itself; the value that names a
row across versions is its **Row Key**)

**Row Key**:
The value the Consumer declares that tells one row from every other and stays the same when the
row's data changes. A source finds a changed or removed row by it, the grid pairs a row's next
version with the one it painted by it, and a Row Mark follows a row across versions by it. It never
says whether a row changed; Row Identity does
([ADR-0140](./docs/adr/0140-a-row-key-names-a-row-across-versions-and-the-grid-repaints-a-changed-row-in-place.md)).
_Avoid_: Row Identity (the test for sameness), id, primary key (the database's), Record Key (a
Snapshot's declared column, from which a Snapshot's rows take their Row Key)

**Vouch**:
A source's or a Consumer's promise that a Window holds no Row Key twice. A bundled source vouches
because it refuses a repeated key as each change comes; a Consumer that pushes its Window vouches
by declaring it. The grid then does not walk the whole Window for a repeat, and still checks the
rows it paints ([ADR-0141](./docs/adr/0141-exgrids-bundled-sources-take-live-data-by-row-key-on-expivots-rules.md)).
_Avoid_: guarantee, trust, validate (that is an Edit Verdict's)

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

**Paint**:
What the grid's rows show between two renders that change it — another Source, another order, other
row instances or other columns make a new paint — named on the Viewport, so that a gesture (a key, a
paste, a press on an Action or a mark) says which paint it was taken against. The grid judges the
gesture against that paint, never against what it holds when the gesture arrives, which on a circuit
is a round trip later
([ADR-0142](./docs/adr/0142-a-write-lands-as-the-user-entered-it-and-a-change-under-the-editor-is-told.md),
[ADR-0011](./docs/adr/0011-selection-is-rectangles-in-index-space-and-is-dropped-on-reorder.md)).
_Avoid_: frame (the browser's), render (a render that paints the same is the same paint), snapshot

**Grid Source**:
The bundled convenience layer that sits on top of the push interface. It wraps an in-memory
array (`GridSource.From`) or a server query (`GridSource.Fetch`) and takes over holding the
Window and answering Range Requests.
**`GridSource.From` is also the reference implementation of Filter and Sort semantics** — it
defines whether `contains` is case-sensitive and whether nulls sort first or last, and
server-side implementations match it.
_Avoid_: data provider, repository, feed, data source (which suggests the grid pulls)

**Snapshot**:
An immutable copy of the Consumer's tabular data at one version, which the Ex family's bundled
sources read. A change to the data is a new Snapshot, never a rewrite of this one. ExPivot's bundled
Pivot Source aggregates one; ExGrid's and ExSheet's bundled sources may read one, each adopting it
by an ADR of its own.
_Avoid_: DataTable, DataSet (.NET's own types), data frame, table (a Linked Table is ExSheet's),
pivot cache (Excel's word, for a pivot's alone)

**Schema**:
How a delimited text file is read into a Snapshot: each column's header, kind and format and the
strings that count as a Blank, and the file's encoding and separator. It is declared, never
guessed; one suggested from a file's first rows is used only once the user has confirmed it
([ADR-0064](./docs/adr/0064-the-snapshot-is-the-familys-immutable-data-held-in-columns.md)).
_Avoid_: import settings, mapping, dialect, type inference (there is none)

**Change Batch**:
The records added, the records changed and the keys removed since one version of the data, applied
as one to make the next: a Snapshot's next Snapshot, by Record Key, or a bundled Grid Source's next
rows, by Row Key. A component shows the version before it or the one after it, never a batch half
applied
([ADR-0141](./docs/adr/0141-exgrids-bundled-sources-take-live-data-by-row-key-on-expivots-rules.md)).
_Avoid_: delta (a risk measure here — rate delta, credit delta), diff, patch, transaction

**Record Key**:
The declared column whose value tells one record of a Snapshot from every other. A
Change Batch changes and removes records by it, and two records under one key are refused.
_Avoid_: Row Identity (the grid's test for sameness), Row Key (the Consumer's value over its rows),
id, primary key (the database's)

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
Context Menu, the cell editor, the cell's message, the loading indicator. **It renders and calls
back; it does not decide meaning**
(which operators exist, and what a filter means, are the core's). Substituting it does not change
behaviour. Each of those places is a **Chrome seam**: the core owns its frame — where it appears,
how it opens and closes — and hands the Chrome the contents to draw. One seam, ExSheet's **Format
Cells**, leaves its frame to the Chrome ([ADR-0071](./docs/adr/0071-a-sheets-cell-format-is-document-data-painted-on-white-paper.md)).
_Avoid_: skin (appearance only is a **Theme**, a term of its own below), template

**Inner Popup**:
A popup that a Chrome seam's contents open for themselves and that their design system draws
outside the grid — a select's list of options, a date picker's calendar. It closes before the
popover that holds it, and with it
([ADR-0039](./docs/adr/0039-a-popover-takes-the-keyboard-and-may-hold-popups-of-its-own.md)).
The grid's own popovers are never one.
_Avoid_: nested popover, portal (a mechanism, not the thing)

**Theme**:
The appearance of one instance — the values of its Visual Tokens. It travels entirely in CSS,
set on any element wrapping the instance root, and never through C#: a palette edit or a
dark/light switch costs no render
([ADR-0027](./docs/adr/0027-appearance-travels-in-css-geometry-travels-in-csharp.md)).
Distinct from Chrome — Chrome renders behavioural UI and calls back; a Theme only colours what
is already painted.
_Avoid_: skin, style, look and feel

**Wrapper**:
A package that adapts one design system to the presentation contract: it maps the system's
palette to a Theme, its density words onto Density, supplies Cell Metrics for its font, and adds
the system's outer chrome around the instance root. It owns no geometry, no DOM and no state
([ADR-0030](./docs/adr/0030-what-a-design-system-wrapper-owns-and-what-it-may-not-touch.md)).
A Consumer's own CSS file doing the same is a minimal Wrapper.
_Avoid_: theme package, skin, integration (ExGrid.Fluxor integrates a store and wraps nothing)

**Density**:
A named preset — Comfortable / Standard / Compact / Excel — resolving into a complete Grid
Metrics, so its numbers are consistent with each other. An explicitly passed value beats the
preset, per value; the default is Compact, which is today's numbers
([ADR-0028](./docs/adr/0028-geometry-is-resolved-once-density-is-only-a-preset.md)). No design
system's density word is the grid's: a Wrapper maps its own onto these.
_Avoid_: spacing, size, compact mode (Compact is one preset, not the concept)

**Grid Metrics**:
The single resolved geometry of an instance — row and header heights, font size, digit width,
paddings. The virtualisation arithmetic, the overlays, the editor box, Auto width and `####`
all read it, and the Geometry Tokens the DOM lays out with are emitted from it, so arithmetic
and paint cannot disagree
([ADR-0028](./docs/adr/0028-geometry-is-resolved-once-density-is-only-a-preset.md)).
_Avoid_: layout, dimensions, theme (metrics are geometry, not appearance)

**Geometry Token / Visual Token**:
The two kinds of `--ex-*` custom property. A **Geometry Token** is written inline on the
instance root from the Grid Metrics and is read-only — an inline declaration outranks any
stylesheet, and overriding one is unsupported. A **Visual Token** is only ever read by the
stylesheet, with a default, and is the whole surface a Theme sets. The metrics-bearing Visual
Tokens — font family and weight — oblige whoever sets them to supply new Cell Metrics
([ADR-0027](./docs/adr/0027-appearance-travels-in-css-geometry-travels-in-csharp.md),
[ADR-0029](./docs/adr/0029-the-presentation-surface-is-a-short-list-of-classes-and-tokens.md)).
_Avoid_: CSS variable (the mechanism, not the contract), design token (suggests a
design-system-wide vocabulary; these are one component's)

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

**Keyboard Field**:
The unseen text field of a grid's own that holds the keyboard while a cell is selected and no edit
is open, on a grid with an editable column, so that an IME can start there. It stands over the
Focus cell; a composition in it is drawn there, and its end opens the **Cell Editor** holding the
composed text. It is the grid's one tab stop. A display-only grid has none
([ADR-0080](./docs/adr/0080-a-keyboard-field-holds-the-keyboard-so-an-ime-can-start-on-a-selected-cell.md)).
_Avoid_: hidden input, hidden textarea, proxy input

**Overwrite / Caret**:
The two states of cell **editing** (four modes in total, with **Interactive** and **Point**). **Overwrite** is
entered by typing straight onto a selected cell: the original value is replaced, and **the arrow
keys commit and move to the neighbouring cell**. **Caret** is entered with F2 or a double click:
the original value stays and **the arrow keys move the caret within the text**. The distinction
is Excel's, and without it "type, arrow to the next cell" does not work as continuous entry.
_Avoid_: input mode / edit mode (both read as "editing" and the distinction disappears)

**Point**:
The editing state in which the arrow keys and the mouse **point at cells for a Formula** instead
of committing: a **Reference Outline** moves over the grid and its Reference is written at the
caret. It holds only while the Consumer says the caret stands where a Reference can go; F2 switches
between it and Caret. The Selection and the Focus do not move ([ADR-0051](./docs/adr/0051-formula-entry-completion-point-mode-and-the-formula-bar.md)).
Through a **Pointing Scope**, a press on another grid that shows a Linked Table points too
([ADR-0058](./docs/adr/0058-a-formula-points-across-grids-through-a-pointing-scope.md)).
_Avoid_: reference mode, pick mode

**Reference Outline**:
The coloured outline drawn over the cells a **Reference** names, for each Reference in the Formula
being edited, while the text of that Reference wears the same colour in the Cell Editor and the
Formula Bar. It shows in every editing state while a Formula is open, not only in **Point**; the
outline Point moves is the Reference Outline of the Reference it is writing. It is gone when the
edit commits or is cancelled. A structured reference's outline is drawn over the **Linked Table**'s
column by whichever grid the Consumer shows that table in, told the colour by ExSheet
([ADR-0057](./docs/adr/0057-references-are-outlined-in-colour-while-a-formula-is-edited.md)).
_Avoid_: range finder (Excel's name for it; nothing is found), highlight (the word is kept out of
Selection's vocabulary), pointing outline

**Formula Bar**:
A band inside the grid's root, above the header, showing the **Name Box** and the Focus cell's
full text — the Entry on a Sheet, the full value on a display grid. It is the Cell Editor's second
surface: one uncommitted text, shown in two places ([ADR-0051](./docs/adr/0051-formula-entry-completion-point-mode-and-the-formula-bar.md)).
_Avoid_: edit bar, input bar, toolbar

**Name Box**:
The Formula Bar's field that says where the Focus is, in the Consumer's words (`D200` on a Sheet);
while a Formula is pointing, it names the pointed cell instead, as Excel does. Typing an address into
it moves the Selection there ([ADR-0051](./docs/adr/0051-formula-entry-completion-point-mode-and-the-formula-bar.md)).
_Avoid_: address bar, cell reference box

**Interactive**:
The state of being **inside** a cell. Entered with Space and left with Esc, on an Action Column
with several actions or on a Template Column. **The third mode alongside Overwrite / Caret**, and
it matches the ARIA grid pattern. **Two mechanisms, one contract**: over the grid's own actions it
is a mode the core holds — the keyboard stays on the grid, the arrows choose an action and Space
fires it; in a Template cell it is the Consumer's control holding DOM focus, taken when the core
asks and never by the core reaching in. Either way Enter never fires, and Esc leaves
([ADR-0037](./docs/adr/0037-entering-a-cell-never-reaches-into-content-the-core-did-not-render.md)).
_Avoid_: focus mode, edit mode (confusable with Caret)

**Cell State**:
The generic vocabulary the grid understands for "this cell is in an unusual state" — normal /
stale / missing / error / modified. The appearance belongs to the theme; any accompanying data
(tooltip text, for instance) stays opaque and is rendered by the Consumer. **The Consumer's own
vocabulary does not enter the grid**
([ADR-0006](./docs/adr/0006-grid-owns-a-generic-cell-state-vocabulary.md)).
_Avoid_: cell status, flag, decoration

**Tone**:
What a Column's rule says a value means — positive / negative — for the theme to paint. The
value-derived half of "why a cell looks different": decided by looking at the value and declared
by the Consumer on the Column, where a Cell State cannot be derived from the value and is asked
for from outside. A tone names a meaning, never a colour; the grid paints none of its own
([ADR-0006](./docs/adr/0006-grid-owns-a-generic-cell-state-vocabulary.md)).
_Avoid_: colour, conditional formatting, style

**Cell Metadata**:
Information a cell carries that is not the value itself. It affects display and decoration but is
never sorted or aggregated on. Example: an as-of stamp, where two metrics in the *same row* can
come from different batch runs, so it cannot be expressed per row.
**It is not stored on the cell; it is asked for by (row, column)** — an as-of stamp is a function
of (book × metric) and the Consumer can answer it.
_Avoid_: attribute, tag, annotation

**Change Highlight**:
A cell's mark, held for a short time, that its displayed value has just changed with the data —
never with a change of layout, sort or collapse. The Consumer says when a cell changed, as it
answers Cell Metadata; the grid paints the mark and takes it away, without animating either.
_Avoid_: flash, blink, tick (it does not animate), Mark (that is a Row Mark), Cell State (a state
lasts; this passes)

**Overflow**:
The state of a value not fitting the column width. **Text is cut with an ellipsis; numbers and
dates become `####`** — truncated text is visibly truncated, whereas a truncated number **looks
like a different, perfectly valid number**
([ADR-0016](./docs/adr/0016-column-width-and-overflow.md)).
_Avoid_: truncation, ellipsis (that names the permitted behaviour, not the state)

**Prerendered**:
A grid painted before it can hear what it will hear — its rows are on screen, but a key pressed
into it would be lost: a server's prerender, and the moment after it before the grid is listening. It shows it is not ready rather than looking live
([ADR-0029](./docs/adr/0029-the-presentation-surface-is-a-short-list-of-classes-and-tokens.md),
[ADR-0033](./docs/adr/0033-the-accessibility-surface-is-owned-by-the-root-not-by-cells.md)).
_Avoid_: not interactive, static (both collide — see Flagged ambiguities)

### Editing

**Overlay**:
A sparse diff laid over an immutable base. It holds only the overridden columns and never copies
rows. Reset is **deleting the override** (the original is still in the base, so nothing needs
saving).
_Avoid_: diff, patch, change set, draft

**Editable**:
A column's declaration that a write may land in it. It gates both the Cell Editor opening and the
writes that arrive without one — a paste, or a Ctrl+Enter fill. A target covering a column that is
not Editable is refused whole, never applied in part
([ADR-0035](./docs/adr/0035-paste-and-fill-respect-the-editable-declaration.md)).
_Avoid_: read-only, locked, protected, disabled

**Edit Intent**:
The notification the grid raises when a user commits an edit — (row identity, column, new value).
The grid changes nothing itself. The screen changes when the Consumer returns new row instances.
_Avoid_: change event, commit, update

**Fill**:
Excel's gesture of writing one value, or one row or column of values, over the rest of a
selection. The keys: Ctrl+Enter writes the editor's text into every selected cell, Ctrl+D copies a
range's top row down it and Ctrl+R its left column across it; the grid assembles the values and
raises them as one paste intent, judged by the paste gate
([ADR-0035](./docs/adr/0035-paste-and-fill-respect-the-editable-declaration.md)). The fill
*handle* — the drag from the Selection's corner — raises a Fill Intent instead
([ADR-0050](./docs/adr/0050-what-exsheet-asks-of-exgrids-core.md)).
_Avoid_: copy down, autofill, stretch (that is a Viewport axis)

**Fill Intent**:
The notification the grid raises when a user drags the fill handle — the source range, the target
range, and the direction. The grid writes nothing; what a fill means is the Consumer's, and on a
Sheet a pattern ExSheet does not implement is refused rather than filled with copies
([ADR-0050](./docs/adr/0050-what-exsheet-asks-of-exgrids-core.md)).
_Avoid_: autofill, drag-fill, fill-down (Ctrl+D's fill is a paste-shaped write, not this)

**Clear Intent**:
The notification Delete raises over the Selection: these positions should hold **no value**. It
carries no value at all, which is what separates it from a paste of empty text — on an amount
column the two mean different things
([ADR-0054](./docs/adr/0054-delete-raises-a-clear-intent-not-a-paste-of-nothing.md)).
_Avoid_: delete (that is the key, and suggests removing rows), erase, empty paste

**Edit Verdict**:
The Consumer's judgement on one commit, asked by the grid at the moment of committing —
**Accept** / **Flag** (applied, painted as Error with a message) / **Reject** (the editor stays
open; Escape remains the only exit without applying). A Reject judges **the value**, which is why
the place to correct it stays open and every gesture that would commit that value stops — as
against a **Refusal**, which judges the operation
([ADR-0034](./docs/adr/0034-validation-is-a-consumer-verdict-enforced-only-at-the-editor.md)).
_Avoid_: validation result, error (that is a Cell State), refusal (that judges the operation)

**Context Menu**:
The menu a secondary click opens over the grid. Its items are Commands the core decides and the
Consumer extends, and Chrome only lays them out — the same ownership the column menu has. A
secondary click outside the Selection moves the Focus onto the cell it lands on first, so that
what a Command will act on is what the user can see
([ADR-0036](./docs/adr/0036-the-context-menu-is-the-column-menu-shape-over-a-selection.md)).
_Avoid_: right-click menu, popup menu, shortcut menu

**Edit Discard**:
Text a user typed into the Cell Editor and never committed, thrown away because the grid can no
longer place it — the order changed beneath the editor, or the row left the Window. The grid
raises the reason rather than losing it quietly, and the loss is not the user's own doing
([ADR-0011](./docs/adr/0011-selection-is-rectangles-in-index-space-and-is-dropped-on-reorder.md)).
Distinct from a **Refusal**, which stops an operation before anything is lost, and from an
**Edit Verdict**'s Reject, which keeps the text where the user can correct it.
_Avoid_: cancel (that is Escape), rollback, revert

**Refusal**:
The grid's own "no", raised on **the operation** — its target, its shape, its size — and never on
the value being written: a copy cap, a misaligned selection, a paste shape, a target covering a
column that is not Editable, a paste past its size ceiling, a clipboard the browser would not let
it write, a write whose row left the Window, whose order moved, or whose Source was replaced before it
landed
([ADR-0142](./docs/adr/0142-a-write-lands-as-the-user-entered-it-and-a-change-under-the-editor-is-told.md)). A value that changed under a write is not one: the write lands. Because a Refusal never looked at what the user typed, it stops only
the operation it named: a fill refused for covering a non-editable column leaves the editor open
and the single-cell Enter still available. Contrast an **Edit Verdict**'s Reject, which judges the
value ([ADR-0005](./docs/adr/0005-copy-refuses-rather-than-truncates.md),
[ADR-0014](./docs/adr/0014-paste-shape-rules-and-selection-count.md),
[ADR-0035](./docs/adr/0035-paste-and-fill-respect-the-editable-declaration.md)).
_Avoid_: rejection, validation failure, error (that is a Cell State), denial

**Overwrite Notice**:
The grid's notice that a Cell Editor commit replaced text that changed while the editor was open —
the one change a user cannot see, because the editor covers the cell. The commit lands. The notice
names the cell, the text the user saw when the editor opened and the text the commit replaced, and
Chrome words it ([ADR-0142](./docs/adr/0142-a-write-lands-as-the-user-entered-it-and-a-change-under-the-editor-is-told.md)).
_Avoid_: conflict, warning, Refusal (nothing was refused), Change Highlight (that marks a change
on screen)

**Find**:
Moving the Focus to the next cell whose displayed text matches what the user typed, searching
every row — not only the painted ones. The grid asks and the Consumer answers with a position,
as it answers for sort and filter; the grid only moves the Focus
([ADR-0055](./docs/adr/0055-find-is-asked-of-the-consumer-like-sort-and-filter.md)).
_Avoid_: search (that is the filter panel's box over the value list), filter (that hides rows;
Find hides nothing), lookup

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
The one cell a user is on — Excel's **active cell**. Typing enters it, the Cell Editor opens on it,
the Name Box names it and the Formula Bar edits it; Enter / Tab cycling moves it within the
Selection, and **the range stays selected**. While a range is extended, the Focus is the end that
stays fixed; the end that moves is the **Extent**. It must always be visible; if it leaves the
Viewport the grid scrolls to it
([ADR-0052](./docs/adr/0052-the-focus-is-excels-active-cell-and-the-extent-is-the-moving-end.md),
which redefines ADR-0012's Focus; the implementation follows once Excel's remaining answers are in).
_Avoid_: cursor, current cell, selected cell, active cell (Excel's name for it — say Focus)

**Extent**:
The end of a range that moves while it is extended — by Shift+arrow, Shift+click, Ctrl+Shift+arrow
or a drag. The grid keeps it in view while extending, as Excel does. The fixed end is the Focus
([ADR-0052](./docs/adr/0052-the-focus-is-excels-active-cell-and-the-extent-is-the-moving-end.md)).
_Avoid_: anchor (retired), moving end, cursor

**Held Selection**:
A Selection paired with the Row Sequence Version it was made under. Reconciling it
against the current version is what drops the selection on reorder — the rule lives in
this pairing, not in each holder's discipline
([ADR-0011](./docs/adr/0011-selection-is-rectangles-in-index-space-and-is-dropped-on-reorder.md)).
_Avoid_: selection snapshot, selection cache (nothing is restored from it)

**Selection Summary**:
Excel's status-bar figures over the Selection — Average, Count, Numerical Count, Min, Max and Sum —
meaning what the **Aggregations** of the same names mean. The grid asks and whoever holds the data
answers, as for **Find**; a figure is shown only as the answer to the current Selection, never a
previous one's and never a partial one
([ADR-0130](./docs/adr/0130-the-selection-summary-is-asked-of-the-consumer-like-find.md)). Distinct
from the selected-cell count, which the grid makes itself from the rectangles' areas.
_Avoid_: aggregate (ExPivot's word), status bar sum, footer, total row (a Row Kind)

**Copied Range**:
What the grid last copied, outlined with dashes for as long as the clipboard still holds it —
Excel's moving border, standing still. It goes when the clipboard changes by anything but the
grid's own copy, on Escape, when an edit opens, when the Selection would be dropped, and when a
copied cell comes to read other text than it was copied with. A browser that cannot say the
clipboard changed shows none
([ADR-0170](./docs/adr/0170-a-copy-outlines-its-range-with-dashes-while-the-clipboard-still-holds-it.md)).
_Avoid_: marquee, marching ants (they move; this does not), copy mode, cut-copy mode (Excel's
names for a state the grid does not have: its paste never reads from the outline)

**Anchor** *(retired by ADR-0052)*:
ADR-0012's name for the fixed end of range extension. Under ADR-0052 the fixed end is the
**Focus** and the moving end is the **Extent**. It has left the code and the criteria; older ADRs
that say it mean the fixed end.
_Avoid_: using it for anything new

**Row Mark**:
A row the user has singled out for an action that follows — the checkbox beside a row.
**Distinct from Selection**: a Selection is positions in the current order and is dropped on
reorder; a Row Mark belongs to the row's identity, is **held by the Consumer**, and survives
sorting and scrolling. The grid reports the intent to mark and **asks, row by row, whether a row
is marked**; it does not hold the marks. Marking "all" from the header means every row of the
current result — after filtering — whether or not it is on screen; under a pager it marks the page
first and offers the whole result explicitly, as Ctrl+A does
([ADR-0015](./docs/adr/0015-paging-is-another-driver-for-range-requests.md)).
"All" is **the result as it stood when the header was pressed**: a row that arrives later is not
marked, and the header shows "some". Changing the filter keeps the marks, and **marks outside the
current result are always counted aloud**, never acted on silently
([ADR-0043](./docs/adr/0043-row-marks-belong-to-identity-and-are-held-by-the-consumer.md)).
_Avoid_: checked row, selected row, tick (a "selected" row is Selection's word)

**Mark Column**:
The one column per grid whose cells show and toggle **Row Marks**, and whose header marks all.
Its meaning belongs to the core, like an **Action Column**'s, and it is painted as plain markup.
Space on it applies to every selected row at once. Only Detail rows carry a mark. Not a boolean
field shown as a checkbox — that is the row's data, not a Row Mark
([ADR-0043](./docs/adr/0043-row-marks-belong-to-identity-and-are-held-by-the-consumer.md)).
_Avoid_: checkbox column, selection column

### Columns

**Column**:
A runtime object. Beyond the header's appearance it holds **how to extract the value from a
row**, the type (which decides the filter UI and the default format; a date column also says
which kind of date it holds), an optional display
format that replaces the default, and the width (`Auto | Fixed` plus `MinWidth` /
`MaxWidth`). A statically listed column and a column generated
from data (each tenor of a tenor ladder) are the same Column, not distinguished.
_Avoid_: field, column definition

**Size to fit**:
Fixing a column's width at what its header and the fetched values need at that moment, bounded by
its `MinWidth` / `MaxWidth`. It is asked for by the user, through a double-click on the column's
edge or the column menu, and **it leaves a Fixed width**, unlike Auto, which is the grid's own and
is not persisted ([ADR-0016](./docs/adr/0016-column-width-and-overflow.md)).
_Avoid_: autofit, auto-size (Excel's word, easily confused with an **Auto** width)

**Pinned Column**:
A column held against the Viewport's edge while the others pan under it — Excel's frozen panes.
It stands **outside virtualisation and is always painted**, so it is the landmark that survives
panning sideways, and it **costs directly**: pin enough columns and the resident cell count is
back where virtualisation found it
([ADR-0004](./docs/adr/0004-cap-the-cells-touched-per-frame.md)). Which columns are pinned is
**View State**, not a property of the Column — combined with the column order the Consumer owns,
"the leading N" expresses it.
_Avoid_: frozen, sticky, locked (those name the mechanism or Excel's wording, not the state)

**Header Group**:
A labelled rectangle over adjacent leaf columns in the tiers above the header row, declared by
**member column names** — `colspan`/`rowspan` expressiveness without a column tree. It paints, and
it is the **drag unit** for reordering (a leaf drag clamps at its group's edge); it carries no
data behaviour — no collapse, no group sort, no aggregate — the line Row Kind draws for rows, on
the other axis. A column no tier covers has its leaf header stretch the full band; members no
longer adjacent, an unknown member, or a rectangle straddling the pinned boundary are refused by
name ([ADR-0032](./docs/adr/0032-tiered-headers-are-declared-rectangles-not-a-column-tree.md)).
_Avoid_: column group (no data behaviour is grouped), banded header, merged cells

**Row Kind**:
What a row represents — detail / group / total. **Distinct from Cell State**: that names the
state of a value per cell, this names the role of a row. Needed to paint group rows differently
from detail rows and to decide what expands and collapses in pivot-like views
([ADR-0013](./docs/adr/0013-fixed-row-height.md)).
_Avoid_: row type, level, hierarchy (Row Kind is the role, not the depth)

**Row Stripe**:
The alternate background on every second row, counted by the row's position in the whole
result — not on the screen — so a stripe stays with its row however far the Viewport scrolls.
Off unless the Consumer asks for it. Appearance only: it marks no role (that is **Row Kind**)
and no state.
_Avoid_: banded rows (a band here is an overlay rectangle — the Focus band, the hover band),
zebra, striped (MudBlazor's word, which its Wrapper maps onto this)

**Action Column**:
A column whose cells carry **declared actions**. The Consumer supplies the icon or label, but the
meaning — "pressing this fires something" — belongs to the core. **Painted as plain markup and
adds no component boundary**
([ADR-0020](./docs/adr/0020-action-and-template-columns.md)).
_Avoid_: button column, command column

**Template Column**:
A column whose cell contents the Consumer paints with arbitrary markup. It costs whatever that
markup costs — the fragment rides inside the row's own boundary rather than adding one per cell
([ADR-0020](./docs/adr/0020-action-and-template-columns.md), which predicted otherwise and records
the correction) — so it is opt-in per column. A value accessor is **still required** (sorting and
filtering need it). A control inside it that should be reachable by keyboard **focuses itself**
when the core asks
([ADR-0037](./docs/adr/0037-entering-a-cell-never-reaches-into-content-the-core-did-not-render.md)).
_Avoid_: custom column, render column

### Sheets

**Sheet**:
The grid of cells ExSheet holds, addressed by column letter and row number (`A1`) over Excel's
extent. The product is **ExSheet**; a Sheet is what it holds. Its rows and columns are places:
inserting a row changes what the rows below hold, and moves none of them ([ADR-0046](./docs/adr/0046-exsheet-is-a-general-purpose-sheet-drawn-by-exgrid-as-its-consumer.md)).
_Avoid_: worksheet, tab, spreadsheet

**Entry**:
What a user put into a cell — a constant (`42`, `Tokyo`, `TRUE`) or a **Formula**. It is what a
**Sheet Document** records, and what the user sees again when they edit the cell. Distinct from
the **Value** the cell shows ([ADR-0048](./docs/adr/0048-a-sheet-document-holds-entries-and-exsheet-holds-the-one-undo-stack.md)).
_Avoid_: input (that is the Cell Editor's element), content, raw value (that is a copy's
unformatted value)

**Value**:
What a cell evaluates to: a number, text, a boolean, or an **Error Value**. A constant Entry is
its own Value; a Formula's Value is its result. A date is a number shown with a date format, as
in Excel. It is never recorded — it is computed again wherever a Sheet Document is opened
([ADR-0047](./docs/adr/0047-the-formula-engine-is-exsheets-own-and-answers-as-excel-or-not-at-all.md)).
_Avoid_: result, cached value, computed value

**Sheet Day**:
The calendar day `TODAY()` answers in a Sheet. It is a fixed day the Consumer gives, or else the day
it is now in a time zone: the Consumer's, or else the browser's. Until one is known, `TODAY()` is
`#GETTING_DATA`. The engine never reads a clock. ExSheet keeps the day and moves it at midnight
([ADR-0121](./docs/adr/0121-today-is-the-sheet-day-and-exsheet-keeps-it.md)).
_Avoid_: system date, server date, current date

**Volatile Function**:
A function whose answer can change while no cell its Formula names changes — `OFFSET`, which reads
a Reference it computes, and `NOW`. A Formula that calls one is recalculated in every recalculation
([ADR-0124](./docs/adr/0124-volatile-functions-are-recalculated-after-every-change.md)).
_Avoid_: dynamic function, live function

**Spill**:
What a Formula whose result is an array does: its first Value shows in the Formula's own cell, the
**Anchor**, and the rest in the cells below and to the right, the **Spill Range**, which hold no
Entry. A cell of the Spill Range that holds an Entry stops it, and the Anchor shows `#SPILL!`
([ADR-0125](./docs/adr/0125-a-formula-whose-result-is-an-array-spills-as-excel-365s-does.md)).
_Avoid_: array formula (Excel's older, entered-with-Ctrl+Shift+Enter kind), overflow

**Anchor**:
The cell whose Formula **Spill**s. `A1#` names its Spill Range.
_Avoid_: origin, parent cell

**Spill Range**:
The cells an **Anchor**'s array covers, itself included.
_Avoid_: spill area, array range

**Formula**:
An Entry beginning with `=`, written in Excel's syntax, that computes a Value from other cells'
Values. A function ExSheet does not know yields `#NAME?`; it is never guessed at
([ADR-0047](./docs/adr/0047-the-formula-engine-is-exsheets-own-and-answers-as-excel-or-not-at-all.md)).
_Avoid_: expression, calculation, computed column (that is ExGrid's — a Column whose accessor
computes)

**Reference**:
The part of a Formula that names cells — `A1`, `$A$1`, `A1:B2`, and with the Sheet named,
`Sheet2!A1`. A relative Reference shifts when its Formula is copied or filled. Inserting or
deleting rows and columns rewrites every Reference so that it keeps naming the same cells; one
whose cells are deleted becomes `#REF!` ([ADR-0047](./docs/adr/0047-the-formula-engine-is-exsheets-own-and-answers-as-excel-or-not-at-all.md)).
_Avoid_: link, pointer, address (an address is where a cell is; a Reference is how a Formula
names it)

**Error Value**:
A Value that is an error — `#DIV/0!`, `#NAME?`, `#REF!`, `#VALUE!`, `#N/A` and the rest of
Excel's set, plus `#CIRC!` for a Formula that depends on itself, where Excel would show 0 —
produced by a Formula and carried into every Formula that uses it. It is data, like a number. **Not the Cell State Error**, which is the Consumer's verdict on a value, painted and
never computed ([ADR-0047](./docs/adr/0047-the-formula-engine-is-exsheets-own-and-answers-as-excel-or-not-at-all.md)).
_Avoid_: error (that is a Cell State), exception

**Linked Table**:
A named table of rows the Consumer supplies to ExSheet, which Formulas read with Excel's
structured references — `SUM(Positions[PV])`. Its rows are reached by key through functions
(`XLOOKUP`), never by position: another grid's order is its user's to change, and a positional
Reference into it would change value without anyone editing it. ExSheet never reads another
component instance; what a Linked Table holds comes from the Consumer, pushed as one whole
snapshot. Until it has arrived, a Formula that reads it shows `#GETTING_DATA` — never 0, never an
older value — and `IFERROR` does not catch the wait. The Consumer may declare one of its columns, or
several together, as its key; a snapshot in which a key repeats is refused, and the table waits again ([ADR-0049](./docs/adr/0049-linked-tables-are-the-consumers-data-read-by-key.md), [ADR-0058](./docs/adr/0058-a-formula-points-across-grids-through-a-pointing-scope.md)).
_Avoid_: external reference (Excel's name for a reference into another workbook), data
connection, link

**Pointing Scope**:
The Sheets and grids a Consumer groups so that a Formula can **Point** across instances. For each
grid in it, the Consumer says which **Linked Table** the grid shows and which of the grid's columns
are which of the table's; the key column is the table's own, declared with it. While a Sheet in the
scope is pointing, a press on one of its grids moves neither DOM focus nor that grid's Selection. It
writes what reads the pressed cell by key (`XLOOKUP("R-4471", Positions[Id], Positions[PV])`, or
`XLOOKUP(1, (Cds[Entity]="ACME")*(Cds[Tenor]="5Y"), Cds[Spread])` for a key of several columns) or the pressed
column's name (`Positions[PV]`). Only the Sheet that holds the keyboard points. Nothing on a page
is joined unless the Consumer put it in the same scope, and a grid in no scope behaves as it
always does ([ADR-0058](./docs/adr/0058-a-formula-points-across-grids-through-a-pointing-scope.md)).
_Avoid_: link (it sounds like one pair), workbook (ADR-0049 keeps that for Sheets that read each
other)

**Headings**:
The column letters and row numbers framing a Sheet — Column Headings and Row Headings. Clicking
one selects its whole column or row, as in Excel. The Consumer may hide either; a Sheet shown
without them still addresses its cells `A1`. The Row Headings are a band beside the rows, never a
column ([ADR-0050](./docs/adr/0050-what-exsheet-asks-of-exgrids-core.md)).
_Avoid_: header (that is ExGrid's column header, whose click sorts), labels, row numbers

**Size Tip**:
The label at the Headings that says how many rows and columns a drag across them covers
(`1048576R x 3C`), shown at the Heading the Extent is on while the drag covers more than one. The
Name Box is empty while it shows, as in Excel. A drag over cells shows its size in the Name Box
instead ([ADR-0052](./docs/adr/0052-the-focus-is-excels-active-cell-and-the-extent-is-the-moving-end.md)).
_Avoid_: tooltip, ScreenTip (Excel's name for any hover label), badge

**Sheet Document**:
The serialisable form of a Sheet that ExSheet hands to its Consumer and takes back. It holds
Entries, never Values, with constants already parsed — so opening it under another culture
cannot change a number. The Consumer persists it; ExSheet does not ([ADR-0048](./docs/adr/0048-a-sheet-document-holds-entries-and-exsheet-holds-the-one-undo-stack.md)).
_Avoid_: file, workbook, Snapshot (that is the Consumer's tabular data), save data

### Pivots

**Source Record**:
One record of the data a pivot aggregates. The bundled Pivot Source holds it in a Snapshot and
never writes to it, and a new Snapshot or a Change Batch is a refresh, as in Excel; behind a
server's Pivot Source it stays on the server
([ADR-0059](./docs/adr/0059-expivot-is-a-pivot-table-drawn-by-exgrid-as-its-consumer.md)).
_Avoid_: row (a row is the report's), item (that is a field's distinct value), fact, entity

**Pivot Source**:
What supplies ExPivot with a report or the aggregates from which it is computed, a field's Items,
and the Source Records behind a cell. The bundled one is the reference implementation, as
`GridSource.From` is ExGrid's; a Consumer's server may answer instead, under the same rules
([ADR-0151](./docs/adr/0151-server-pivots-send-report-windows-and-share-the-local-engine.md)).
_Avoid_: data source, provider, backend, pivot cache (Excel's word; here that is the Snapshot)

**Source Version**:
Which state of the data a Pivot Source's answer came from. The records behind a cell and a field's
Items are asked for under the version the report was computed from, and a source that can no
longer answer under it refuses rather than answer from newer data.
_Avoid_: data version (it names nothing here), Row Sequence Version (that is the grid's order),
timestamp, revision

**Report Version**:
Which settled state of a Pivot Report an answer or an operation refers to: its Source Version,
Pivot Layout and display settings. Reports made from the same data can have different Report
Versions. The Row Sequence Version names only the row keys and their order, not the report's
values or settings
([ADR-0152](./docs/adr/0152-report-apis-may-change-and-remote-reports-recover-their-baseline.md)).
_Avoid_: Source Version (that names the data alone), Row Sequence Version (that names row order),
timestamp

**Window Digest**:
A digest of a Pivot Report's Window as it stands after Window Changes — every row of the Window, its
key, labels and shown texts, with the Window's extent and Report Version — that the changes carry, and
that the component computes again before it shows the result. Window Changes whose digest is missing or
differs are never painted: the complete Window is asked for instead
([ADR-0152](./docs/adr/0152-report-apis-may-change-and-remote-reports-recover-their-baseline.md)).
_Avoid_: checksum, hash (the mechanism, not the term), signature (it proves no sender)

**Window Changes**:
The rows of a Pivot Report's Window that changed since a Baseline Window — each row whole, subtotals
and grand totals included — sent with the Report Version and the Window Digest of the Window they make,
in place of the whole Window. The component applies them to the Baseline Window it holds and shows the
result only once the digest matches
([ADR-0152](./docs/adr/0152-report-apis-may-change-and-remote-reports-recover-their-baseline.md)).
_Avoid_: delta (a risk measure here), diff, patch

**Baseline Window**:
The Window, under a Report Version, that Window Changes are applied to: the one the component holds
and names when it asks. A source that no longer holds it answers with the complete Window instead
([ADR-0152](./docs/adr/0152-report-apis-may-change-and-remote-reports-recover-their-baseline.md)).
_Avoid_: base, previous Window (the baseline is named, never assumed)

**Leaf Aggregate**:
For each combination of the row and column fields' Items that has records, the parts each Value
Field's Aggregation is computed from — counts, sums, extremes. Every cell, subtotal and grand
total is computed from these parts, whether the report is computed locally or on a server
([ADR-0066](./docs/adr/0066-expivot-asks-a-pivot-source-and-a-server-answers-with-leaf-aggregates.md),
[ADR-0151](./docs/adr/0151-server-pivots-send-report-windows-and-share-the-local-engine.md)).
_Avoid_: cube (the engine's own word for what it holds), summary, pre-aggregate, rollup

**Question**:
What a Pivot Source is asked to aggregate for a Pivot Report: the part of its Pivot Layout that
decides the Leaf Aggregates — the row and column fields in order, the Hidden Items of each placed
field, and for each field in Values the parts its Aggregations are computed from. A change to any of
them asks a new Question, which supersedes one still unanswered; any other change — collapse, order,
form, totals, Show Values As, a caption or a format — lays the held answer out again, and newer data
is folded into that answer or answers the same Question again
([ADR-0066](./docs/adr/0066-expivot-asks-a-pivot-source-and-a-server-answers-with-leaf-aggregates.md),
[ADR-0067](./docs/adr/0067-live-data-a-change-batch-makes-the-next-snapshot-and-expivot-folds-it-in.md)).
_Avoid_: query (the grid's **Query** asks for rows), request, and Pivot Layout (a layout asks a
Question, and holds more)

**Pivot Field**:
A named attribute of the Source Records the user can place in an Area: its caption, how it is read
from a record, and its declared type, which decides where a ticked field goes and which
Aggregation it takes by default. The same Pivot Field may stand in Rows and, as a Value Field, in
Values ([ADR-0061](./docs/adr/0061-the-field-list-is-excels-pane-and-the-core-decides-what-a-move-means.md)).
_Avoid_: field alone (ExGrid avoids it for a Column), column (that is the grid's), dimension and
measure (a Pivot Field is either, by the Area it stands in)

**Area**:
One of the four places a Pivot Field stands — **Filters**, **Columns**, **Rows**, **Values** —
Excel's names. A field stands at most once across Filters, Rows and Columns
([ADR-0061](./docs/adr/0061-the-field-list-is-excels-pane-and-the-core-decides-what-a-move-means.md)).
_Avoid_: zone, well, shelf, drop box

**Pivot Layout**:
Which Pivot Fields stand in which Areas and in what order, with each one's settings — Hidden Items,
order, subtotals, collapsed Items, a Value Field's Aggregation — and the report's form and totals.
It is ExPivot's **View State**: serialisable, persisted by the Consumer, a **Saved View** when
named. Nothing in it is a value
([ADR-0059](./docs/adr/0059-expivot-is-a-pivot-table-drawn-by-exgrid-as-its-consumer.md)).
_Avoid_: configuration, definition, pivot settings, and layout alone (that is also the browser's)

**Item**:
One distinct value of a Pivot Field in Rows, Columns or Filters — a row label, a column label, a
choice in a filter. Text Items are told apart ignoring case; a Blank is the Item `(blank)`. A
**collapsed** Item hides the Items under it and shows their totals
([ADR-0060](./docs/adr/0060-the-pivot-engine-answers-as-excels-pivottable-and-is-the-reference.md)).
_Avoid_: member (OLAP's), label (the text an Item is painted with), category

**Hidden Item**:
An Item the user unticked in its field's filter. Every record carrying it is left out of the
report, totals included. The layout holds what is hidden, not what is shown, so an Item that first
appears later is shown
([ADR-0060](./docs/adr/0060-the-pivot-engine-answers-as-excels-pivottable-and-is-the-reference.md)).
_Avoid_: filtered item, excluded value, and Filter (that is ExGrid's model of conditions)

**Order Key**:
A Pivot Field's function from a value to what its Items are ordered by, ascending — a tenor to its
length. An Item it gives no key comes after the keyed ones. It orders Items and never makes two
values one Item: `18M` and `1Y6M` stay two Items, side by side.
_Avoid_: comparer, custom sort, custom list (that is a field's declared Item order, Excel's word)

**Value Field**:
A Pivot Field placed in Values, with its **Aggregation**, its caption (`Sum of Amount`), its number
format and how its values are shown (**Show Values As**: % of Grand Total and the rest). One Pivot
Field may be several Value Fields
([ADR-0060](./docs/adr/0060-the-pivot-engine-answers-as-excels-pivottable-and-is-the-reference.md)).
_Avoid_: data field (Excel's older name), measure, metric

**Aggregation**:
How a Value Field summarises the records at a cell — Sum, Count, Average, Max, Min, Product, Count
Numbers, StdDev, StdDevp, Var, Varp; Excel's "Summarize Values By". A total is aggregated from its
records, never from the totals below it
([ADR-0060](./docs/adr/0060-the-pivot-engine-answers-as-excels-pivottable-and-is-the-reference.md)).
_Avoid_: function (that is a Formula's), rollup, reduce

**Σ Values**:
The pseudo-field that says where the Value Fields' captions stand when there are two or more — in
Columns, where Excel puts it, or in Rows. In the first version it is always innermost
([ADR-0060](./docs/adr/0060-the-pivot-engine-answers-as-excels-pivottable-and-is-the-reference.md)).
_Avoid_: data field, measures dimension

**Pivot Report**:
What ExPivot computes and ExGrid paints: rows for Items, **group rows**, **subtotals** and the
**grand total**, label columns, and value columns under the column Items' Header Groups. It is
computed and read-only
([ADR-0059](./docs/adr/0059-expivot-is-a-pivot-table-drawn-by-exgrid-as-its-consumer.md)).
_Avoid_: pivot table (the whole product on screen), result (ExGrid's word for rows after a
filter), view

**Stale Report**:
A Pivot Report left on the last version of the data it could be computed from, because the newest
cannot be shown — the layout would break a cap, or the source failed — and saying so: what
happened, and as of when
([ADR-0067](./docs/adr/0067-live-data-a-change-batch-makes-the-next-snapshot-and-expivot-folds-it-in.md)).
_Avoid_: cached report, outdated, frozen (it is not stopped; it is waiting for an answer)

**Defer Layout Update**:
Excel's switch at the foot of the Field List: while it is on, the pane's changes build a pending
Pivot Layout that the report does not follow until Update
([ADR-0061](./docs/adr/0061-the-field-list-is-excels-pane-and-the-core-decides-what-a-move-means.md)).
_Avoid_: manual mode, batch edit, draft layout (a draft is a panel's, until OK)

**Report Form**:
How a Pivot Report sets out its row labels — **Compact** (one indented label column, Excel's
default), **Outline** or **Tabular** (a label column per row field). Excel's "Report Layout"
([ADR-0060](./docs/adr/0060-the-pivot-engine-answers-as-excels-pivottable-and-is-the-reference.md)).
_Avoid_: layout (that is the Pivot Layout), view, mode

**Field List**:
The pane where the user builds the report: every Pivot Field with a checkbox and a search, and the
four Areas, with drag and drop and each placed field's menu — Excel's "PivotTable Fields". ExPivot
decides what each gesture means; its Chrome draws it
([ADR-0061](./docs/adr/0061-the-field-list-is-excels-pane-and-the-core-decides-what-a-move-means.md)).
_Avoid_: field chooser, designer, pivot panel

**Pivot Toolbar**:
The band above a Pivot Report: the report filter band on its left, and on its right Layout ▾
(Excel's Design tab: Subtotals, Grand Totals, Report Layout), Refresh when the source can be asked
again, and the Field List's toggle. A Stale Report's notice stands beneath it. ExPivot decides
what each of them means; its Chrome draws it
([ADR-0061](./docs/adr/0061-the-field-list-is-excels-pane-and-the-core-decides-what-a-move-means.md)).
_Avoid_: toolbar on its own (a Sheet's is the Sheet Toolbar), ribbon, Design tab (Excel's, which
the Layout menu stands in for)

**Show Details**:
The Source Records behind one cell of the report — Excel's drill-down, from a double click on a
value or the Context Menu — which ExPivot shows in a tab beside the report or in a dialog, or hands
to the Consumer to show, as the Consumer chooses
([ADR-0063](./docs/adr/0063-what-expivot-asks-of-exgrids-core.md)).
_Avoid_: drill-through, drill-down (Excel's older name), underlying data

**Cell Format**:
How a Sheet's cell is shown, recorded apart from its Entry: its **Number Format**, **Alignment**,
**Font**, **Fill** and **Border**. It is recorded at three levels, cell over row over column, so
formatting a whole column records one thing, and a cell may hold a Cell Format and no Entry. It is
document data, recorded in the Sheet Document: a colour in it is the user's choice and is painted
as recorded. Not ExGrid's Column `Format`, which turns a value into the text shown for it
([ADR-0071](./docs/adr/0071-a-sheets-cell-format-is-document-data-painted-on-white-paper.md)).
_Avoid_: style (Excel's named Cell Styles, and the CSS attribute), formatting (the act of setting
one), format on its own

**Number Format**:
Excel's format code that turns a Value into the text a cell shows — `#,##0.00`, `yyyy-mm-dd`,
`0%`, and General, which fits its column. A date is a number with a date Number Format. A code
ExSheet does not read is refused, never shown as General
([ADR-0047](./docs/adr/0047-the-formula-engine-is-exsheets-own-and-answers-as-excel-or-not-at-all.md)).
_Avoid_: format string, display format (that is ExGrid's Column `Format`)

**Alignment**:
Where a cell's text sits across its width: General (Excel's: numbers right, text left, booleans
and Error Values centred), left, centre or right. Horizontal only — every row has one height, so
there is nothing to align vertically.
_Avoid_: justification, text-align

**Font**:
The colour and emphasis of a cell's text: its colour, bold, italic, underline and strikethrough.
Not its size or typeface: every row has one height, and one digit width decides what fits. An
**Automatic** colour is the **Ink**, not a recorded colour ([ADR-0071](./docs/adr/0071-a-sheets-cell-format-is-document-data-painted-on-white-paper.md)).
_Avoid_: text style, typeface

**Fill**:
The one solid colour behind a cell's text. No patterns and no gradients ([ADR-0071](./docs/adr/0071-a-sheets-cell-format-is-document-data-painted-on-white-paper.md)).
_Avoid_: background, shading, highlight

**Border**:
A line on one side of a cell — top, bottom, left or right — in one of Excel's line styles and a
colour. Each cell records its own four sides, as Excel's files do, but the line between two cells is
one line: setting it from either cell replaces it for both, and the later setting wins. Where both
cells still record a line, after a copy or a deletion, the upper or left cell's is the one shown.
No diagonals ([ADR-0071](./docs/adr/0071-a-sheets-cell-format-is-document-data-painted-on-white-paper.md)).
_Avoid_: gridline (the Sheet's own faint lines, which are not recorded), outline (that is a
**Reference Outline**), frame

**Paper / Ink**:
The ground a Sheet's cells lie on, and the colour of text whose Font colour is Automatic: Excel's
white and black, in every colour scheme. A dark page does not darken them, as Excel's cells stay
white under its Black theme, so a colour a user recorded reads as it did when it was chosen. What
lies on the Paper — the Focus, the Selection, Reference Outlines, the editor in the cell — takes
its light-scheme appearance; what frames it — the Headings, the Name Box, the Formula Bar,
popovers — follows the colour scheme. Both are Visual Tokens: a Consumer may change them, and
takes on what that does to recorded colours ([ADR-0071](./docs/adr/0071-a-sheets-cell-format-is-document-data-painted-on-white-paper.md)).
_Avoid_: background, canvas

**Format Cells**:
Excel's dialog for setting a Cell Format, opened by Ctrl+1 or from the Context Menu: Number,
Alignment, Font, Border and Fill. ExSheet decides what it offers and what OK means, and OK sets only
what the user touched. It is the one Chrome seam whose frame is the Chrome's: a popover inside the
Sheet's box under the built-in Chrome, a page-level dialog under MudBlazor's ([ADR-0071](./docs/adr/0071-a-sheets-cell-format-is-document-data-painted-on-white-paper.md)).
_Avoid_: format dialog, properties, style editor

**Sheet Toolbar**:
The bands of buttons a Sheet shows above its Formula Bar when asked to, and only then. It belongs
to one Sheet and acts on that Sheet's Selection. What it holds, and in which order, is the
Consumer's to declare; while an edit is open, every Toolbar Item in it is unavailable ([ADR-0100](./docs/adr/0100-the-sheet-toolbar-ships-as-an-opt-in-part-of-exsheet.md)).
_Avoid_: ribbon, ToolBarContent (the slot of ExGrid's MudBlazor paper, outside the grid), toolbar on
its own

**Toolbar Row**:
One band of a Sheet Toolbar. Every Toolbar Row of a Sheet is the same height, so the Sheet Toolbar's
height is a count of rows. A Toolbar Row is what a ribbon tab is to a KeyTip ([ADR-0100](./docs/adr/0100-the-sheet-toolbar-ships-as-an-opt-in-part-of-exsheet.md)).
_Avoid_: toolbar line, section

**Toolbar Item**:
One element of a Toolbar Row: a formatting command ExSheet offers, or a Consumer's own action. It
declares what it means; the Chrome decides how it looks, so swapping the Chrome keeps every item's
meaning ([ADR-0100](./docs/adr/0100-the-sheet-toolbar-ships-as-an-opt-in-part-of-exsheet.md)).
_Avoid_: tool, toolbar button (a Toolbar Item need not be a button)

**KeyTip**:
Excel's letter over a Toolbar Row or a Toolbar Item, shown when Alt is released alone or F10 is
pressed; typing the letters runs the item, as Alt, H, 1 sets bold. A letter is Excel's where Excel
has one, and otherwise ExSheet's own or the Consumer's declared one, never assigned by position
([ADR-0100](./docs/adr/0100-the-sheet-toolbar-ships-as-an-opt-in-part-of-exsheet.md)).
_Avoid_: access key, accelerator, mnemonic, shortcut (a shortcut is a chord, a KeyTip is a sequence)

### Documentation

**Docs Site**:
The family's documentation, published to GitHub Pages: one site for ExGrid, ExSheet, ExPivot and
the data packages, where each component has a page of description, **Examples** and its API. It
runs the real components in the reader's browser, and it is a Consumer like any other
([ADR-0110](./docs/adr/0110-the-docs-site-is-a-webassembly-app-on-github-pages-and-its-examples-show-the-code-they-run.md)).
_Avoid_: demo, demo pages (those are layer 3's fixture), playground

**Example**:
A live component on a Docs Site page, with beneath it the code it runs, read from its own source.
It shows one thing, and where a Wrapper exists it switches between the built-in Chrome and
MudBlazor's
([ADR-0110](./docs/adr/0110-the-docs-site-is-a-webassembly-app-on-github-pages-and-its-examples-show-the-code-they-run.md)).
_Avoid_: sample, snippet (a snippet is code that does not run)

**Showcase**:
A Docs Site page for one use case — a trade blotter, a budget sheet, a sales analysis — that fills
the window and carries no prose. The README's recordings are made from it
([ADR-0110](./docs/adr/0110-the-docs-site-is-a-webassembly-app-on-github-pages-and-its-examples-show-the-code-they-run.md)).
_Avoid_: demo, hero

## Flagged ambiguities

- **"Grid" on its own does not say whether ExGrid, ExSheet or ExPivot is meant.** When it is
  ambiguous, always commit to one. To mean all of them, write "the Ex family". Do not invent a
  single umbrella noun (`ag-grid` has none either).
- **"User" gets used two ways** — the developer embedding this component, and the end user
  touching the screen. The former is the **Consumer**; the latter is the **user**.
- **"Consumer" has two levels once ExSheet or ExPivot is involved.** ExSheet and ExPivot are
  each ExGrid's Consumer, and the application embedding one is that one's. Unqualified,
  **Consumer** is always the application; write "ExSheet, as ExGrid's Consumer" (or ExPivot) when
  that relationship is meant.
- **"Row" means two things in a pivot.** A **Source Record** is what the Consumer pushes; a row is
  the Pivot Report's, one per Item, subtotal or grand total. Never call a Source Record a row.
- **"Filter" means two things in a pivot.** ExGrid's **Filter** is a model of conditions the grid
  hands its Consumer; a pivot's filter is its **Hidden Items**, held in the Pivot Layout, and
  **Filters** is the Area. The report's grid is handed no Filter at all.
- **"Question" is the pivot's; the grid asks a Query.** A **Question** is what a Pivot Source is
  asked to aggregate. What ExGrid asks of its source is its **Query** — range, Filter and Sort —
  and where its pages say "question" for the Query, a Find or the Selection Summary, it is the
  everyday word. Write **Question** only for the pivot's.
- **"Seam" means two things.** A **Chrome seam** is one of the places Chrome is substituted
  into. In talk about tests, a seam is the public boundary a test observes behaviour through —
  write **test seam** for that, and never "seam" alone where either could be meant.
- **"Focus" names a cell; "DOM focus" names an element.** The **Focus** is the cell keyboard
  operations start from, and it is described, never held: the grid root holds the browser's
  focus and `aria-activedescendant` points at the Focus cell
  ([ADR-0033](./docs/adr/0033-the-accessibility-surface-is-owned-by-the-root-not-by-cells.md)).
  **DOM focus** is the browser's — which element receives the keys. The two meet only when a
  Template's control takes DOM focus inside the Focus cell (**Interactive**). Write "DOM focus"
  whenever the browser's is meant; `FocusRequest` in the Chrome and template contexts asks for
  DOM focus, not for a Focus move.
- **"Caret" names an editing state here, and the text cursor in everyday speech.** **Caret**
  (with Overwrite) is the state F2 enters, in which the arrow keys move within the text. The
  blinking insertion point itself is the **caret position** — write that, never "the Caret", when
  the position is meant: completion and Point both act at the caret position, in any state.
- **"Paper" is a Sheet's ground here, and a Material surface in MudBlazor.** The Wrapper's
  outer element, `MudExGridPaper`, is named after `MudPaper`: a surface around one or more
  grids that follows the colour scheme. A Sheet's **Paper** does not. Write the component's name
  for the surface, and **Paper** only for the Sheet's ground.
- **"Interactive" names a cell mode here, and a render mode in Blazor.** Blazor calls a
  component that has connected and can handle events "interactive", and its render modes are
  `InteractiveServer` / `InteractiveWebAssembly`. In this project **Interactive** is only the
  in-cell mode. A grid that is painted but not yet connected is **Prerendered**; write the
  render mode's full name (`InteractiveServer`) when the render mode is meant.

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
