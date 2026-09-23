# Anchor / Focus and keyboard navigation — Enter and Tab cycle inside the selection

Selection is driven by two points: the **Anchor** (fixed end) and the **Focus** (moving end).
Keyboard behaviour follows Excel. **When a range is selected, Enter and Tab cycle inside it and
never leave it.**

## Anchor and Focus

| | Meaning | Moved by |
|---|---|---|
| **Anchor** | the fixed end of range extension | click, Ctrl+click (a new range — or a toggle-off, which detaches it) |
| **Focus** | where keyboard operations start from; the moving end | arrows, Shift+arrow, Enter / Tab cycling |

- **Click** — Anchor = Focus = that cell. The selection collapses to one cell
- **Shift+click** — Anchor stays; Focus moves to the clicked cell and the range is redrawn
- **Ctrl+click** — on an unselected cell, adds a new range
  ([ADR-0011](./0011-selection-is-rectangles-in-index-space-and-is-dropped-on-reorder.md));
  Anchor and Focus move to the new range. On a selected cell, **toggles it off** (see the
  subsection below)
- **Arrows** — collapse the selection to one cell and move
- **Shift+arrow** — Anchor fixed, Focus moves, the range grows or shrinks
- **Ctrl+arrow** — jump to the edge (last / first row vertically, last / first column
  horizontally). Not Excel's "edge of the non-blank block" — query results have no blank rows,
  and finding a block edge would require the whole dataset (ADR-0011)
- **Shift+Ctrl+arrow** — extend the range to the edge
- **Ctrl+Space / Shift+Space** — select the whole column / whole row (as in Excel).
  *(Refined while implementing: the growing range expands to full height / full width
  keeping its column / row span, so a range spanning three columns becomes three whole
  columns — Excel's behaviour. Anchor and Focus stay where they are.)*

**With disjoint ranges, the most recently created one is the one that grows.** Both Shift+arrow
and Shift+click move only the range the Anchor belongs to.

### Ctrl+click on a selected cell toggles it off

*(Added while implementing the selection model; the original text said only "adds a new
range".)* **Ctrl+click on an already-selected cell deselects it, as Excel 365 does.** The
cell is subtracted from **every** range containing it — a rectangle splits into at most
four — and the cell is fully unselected afterwards.

The counter-argument was representation cost: subtraction fragments the rectangle list,
which reads like the degradation
[ADR-0011](./0011-selection-is-rectangles-in-index-space-and-is-dropped-on-reorder.md)
refused when it rejected re-mapping through insertions. It does not hold here. ADR-0011's
case was **data-driven** — a feed refresh could scatter a thousand insertions through a
selection with no user action. A toggle costs a physical click, so the fragment count
grows by at most three per **deliberate gesture** — the same order as Ctrl+click adding
ranges — and cannot humanly reach the scale at which the one-overlay-per-range measurement
([ADR-0008](./0008-selection-is-painted-by-an-overlay.md)) degrades.

What tipped the decision was what append-only loses. A mis-click while assembling a
scattered selection could not be corrected without starting over — and scattered is the
natural case (ADR-0011). Holes could not be punched in a selection before a bulk fill
(select all, exclude two rows, Ctrl+Enter). And the duplicate overlapping ranges that
append-only accumulates would paint twice through the translucent overlay.

After a toggle-off, Anchor and Focus stand **detached** — on the deselected cell, outside
every range — as Excel behaves after a deselect. The detachment is **stored, never inferred
from geometry**: the subtraction fragments make "which range is the Anchor's" unanswerable
by coordinates. From the detached state:

- Shift+arrow / Shift+click starts a **new** range (appended, so it is the one that grows)
- Enter / Tab enters the first range at its first cell (backward: the last range at its
  last cell)
- Ctrl+Space / Shift+Space starts a new **whole-column / whole-row** range at the
  detached cell (expanding an arbitrary subtraction fragment instead would select a
  column the user never pointed at)
- Toggling off the last selected cell leaves the empty selection

## Enter and Tab cycling

```
Range B2:D4 selected, Focus at B2

Enter (column-major)          Tab (row-major)

  B2 → B3 → B4 ┐              B2 → C2 → D2 ┐
  ┌─────────────┘              ┌────────────┘
  C2 → C3 → C4 ┐              B3 → C3 → D3 ┐
  ┌─────────────┘              ┌────────────┘
  D2 → D3 → D4 ─→ back to B2   B4 → C4 → D4 ─→ back to B2
```

**Enter runs down columns, Tab runs across rows.** Both wrap at the edge and return to the start
after the last cell. Shift+Enter and Shift+Tab run backwards.

**The range stays selected while cycling**; only the Focus moves. That is what makes "select a
block and just keep typing" work.

With no range (a single cell), Enter moves down and Tab moves right, and the selection
follows. *(Refined while implementing: at the last row / last column the Focus clamps and
stays put, as at Excel's sheet edge — no wrap target is invented.)*

*(Refined while implementing: cycling can park the Focus in a range the Anchor is not in.
A Shift+arrow from there **re-anchors at the Focus and starts a new range** — redrawing
the Anchor's range would bridge the two ranges into one block with a single keystroke,
selecting cells the user never touched. Shift+click keeps the Anchor and redraws its
range: its target is the absolute cell the user pointed at, not a step from the Focus.)*

### Why the cycling matters

The two editing modes in
[ADR-0010](./0010-chrome-seams-column-menu-editor-loading.md) exist to make "type, arrow to the
next cell" work. **This cycling is what receives that continuous entry**; implementing only one of
the two achieves nothing.

And **confining entry to the range is a safety device**. Filling five rows with a value works
because, with the range selected, the fifth Enter returns to the top. A design that always moves
down would put the Focus on a sixth row and **edit it without the user noticing**.

Rejected:
- **Always move down / right, ignoring the range** — minimal to implement, but entry spills out of
  the range.
- **Cycle within the range but with Enter and Tab in the same direction** — Excel users
  distinguish the two, so one of them is always going to feel wrong.

## Clicking a column header sorts

**The data-grid convention (click = sort) is adopted; Excel's (click = select the whole column) is
not.** This component is used by people carrying both expectations, so **the tie is broken by
frequency** — what happens first on a data screen is sorting and filtering; selecting a whole
column comes later and occasionally. The frequent operation gets the single click.

Column selection is not lost. **There are three ways.**

- **Ctrl+Space** (Excel's own shortcut)
- **Ctrl+Shift+Down** — extending from the first row to the edge is effectively the whole column
- Clicking the corner above the row-number column (select all)

Rejected:
- **Click = select the column, with sort in the column menu** — sorting becomes two steps. The
  "sort ascending/descending" entries in the column menu
  ([ADR-0010](./0010-chrome-seams-column-menu-editor-loading.md)) would be the only route, and
  opening a menu every time is noticeably heavy.
- **Split the header into regions** (the text sorts, a thin strip selects) — both become single
  clicks, but the hit target is fiddly and users never discover the strip.

## What the implementation settled

*(Written while wiring the keyboard. Three of these are decisions this ADR did not make.)*

**Home and End.** Never mentioned here, and Excel-shaped: Home and End are the row's first
and last cell, Shift extends to them, and Ctrl+Home / Ctrl+End are the whole result's first
and last. They resolve to the same `MoveToEdge` / `ExtendToEdge` transitions the Ctrl+arrows
use, so nothing new enters the selection model.

**Escape leaves the grid, because Tab never leaves the selection.** Taken literally, "Enter
and Tab cycle inside it and never leave it" traps the keyboard: a user who tabbed into the
grid could not tab out, which contradicts
[ADR-0020](./0020-action-and-template-columns.md)'s own "the ARIA grid pattern is exactly
the grid as one tab stop". Excel is an application; this is one component on someone's
page. **Escape releases the grid's DOM focus** — it has no other meaning outside editing,
and when the Interactive and Overwrite modes arrive it stays the outermost of them.

An open popover — a column menu or a filter panel — sits **inside** that layering *(recorded
when dismissal was wired, after the first manual session left a filter panel with no way to
close it)*: while one is open, Escape closes it and hands the keyboard back to the grid,
**wherever focus sits** — on the root, on the ▾ button, or inside the panel. A focusable
descendant keeps its keys (above) with exactly one exception: the capture-phase listener
forwards a descendant's **Escape**, marked as coming from one, because Escape is
[ADR-0020](./0020-action-and-template-columns.md)'s way out of the cell — the core answers it
by closing whatever popover stands and taking the keyboard back, never by leaving the grid.
Only an Escape with nothing left to dismiss, pressed on the root itself, releases the DOM
focus. The other ways a popover closes belong to
[ADR-0010](./0010-chrome-seams-column-menu-editor-loading.md).

*(The Interactive layer arrived with
[ADR-0037](./0037-entering-a-cell-never-reaches-into-content-the-core-did-not-render.md), and
sits between the two: a popover closes first, then an Interactive cell is left — the grid
keeps the keyboard and the Focus does not move — and only then does Escape release the DOM
focus. Inside a cell with several actions, ← and → choose rather than move and Home / End
choose the first and last; every other key the grid claims leaves the cell and keeps the
meaning this ADR gives it, Enter and Tab included, so nothing about cycling changes.)*

**With focus but no selection, the first key only places the Focus.** `GridSelection.Move`
is a no-op on an empty selection, deliberately — there is no Focus to start from. But
"focus on the grid, nothing selected" is an ordinary state: reached by tabbing in, by
clicking the header, and above all **by a sort or filter, which drops the selection**
([ADR-0011](./0011-selection-is-rectangles-in-index-space-and-is-dropped-on-reorder.md)).
Left as a no-op, every key would be dead after every reorder until the user reached for the
mouse. So the component — which knows what is on screen, as the selection model does not —
puts the Focus on **the first visible cell**, without moving the Viewport, and lets that be
the whole of the keypress: the first Down selects the first cell, the way the first Down in
any list selects its first item. Keys that name a whole region (Ctrl+A, the Space pair,
Ctrl+Home) need no starting point and still do what they say.

**"Go to the beginning" is a different request from "make this cell visible", and
Pinned Columns are where the two come apart.** Found by pressing Ctrl+Home on a grid
with two pinned columns: the Focus went to the first cell and **the Viewport did not
move sideways at all**, leaving the user reading columns 60-70 with the pinned pair on
the left. Measured, same data, same keystroke, only `PinnedColumnCount` differing:

```
pinned = 2   Ctrl+End → scrollLeft 8170   Ctrl+Home → scrollLeft 8170
pinned = 0   Ctrl+End → scrollLeft 8170   Ctrl+Home → scrollLeft 0
```

So **pinning was changing what the key meant** — which is the argument that settles it.
`ColumnGeometry.ScrollLeftToReveal` is not wrong: a pinned column covers the Viewport's
left edge, so it is already whole on screen and revealing it correctly moves nothing.
Home and Ctrl+Home simply are not asking for that. **A key that names the start of the
row or of the result puts the Viewport's left edge at the start** — Home, Shift+Home,
Ctrl+Left, Ctrl+Shift+Left, Ctrl+Home — which is what already happened when nothing was
pinned, so the two cases agree instead of the pinned one getting a behaviour of its own.
The right-hand end needed nothing: End and Ctrl+End right-align against the readable
area either way.

The obvious fix is the wrong one, and is worth naming because it passes every test
about Home. **Making `ScrollLeftToReveal` answer 0 for a pinned column would break
ordinary navigation**: a reveal runs after *every* keyboard move, including the ones
that name no column at all, so a user who had scrolled right and pressed Down would be
yanked back to the first column. The intent belongs to the transition, not to the
geometry. (One limit, the same with or without pinning: a press that moves the Focus
nowhere — it is already at the first column — renders nothing and so reveals nothing,
even if the user has since scrolled away with the mouse.)

**PageUp / PageDown move the Focus and the Viewport by the same number of rows.** N is the number
of rows fully visible — `ViewportBox.VisibleHeightPx` less the header band, floored, so the
Scrollbar Gutter is already off it ([ADR-0013](./0013-fixed-row-height.md)). The Focus keeps its
position within the Viewport, which is what Excel does, and which is the difference between pressing
the key repeatedly and having the Focus pinned to the bottom row from the second press onward.

**This is the grid's only scroll that is not a reveal**, and that is the price of the decision rather
than an oversight: everywhere else, writing `scrollTop` means "make the Focus visible". At the top
and the bottom the scroll clamps and the relative position cannot be held, so the transition
degrades to exactly a reveal there — the Focus is still visible, which is the invariant that
actually matters.

The transitions are named **`MoveByViewport` / `ExtendByViewport`**, and deliberately not "page":
`CONTEXT.md` puts *page* on the `_Avoid_` list under **Window**, and
[ADR-0015](./0015-paging-is-another-driver-for-range-requests.md) has already spent the word on a
Consumer's pager. The keys keep the names the browser reports; the vocabulary does not take them on.

**`Ctrl`+PageUp / `Ctrl`+PageDown are neither handled nor prevented.** The browser uses them to
switch tabs. The capture-phase listener exists so that this grid sees its own keys before a cell
editor does ([ADR-0018](./0018-multiple-instances-must-be-independent.md)) — not so that it can take
keys away from the browser around it.

**A reveal is judged against where the scroller is going to be, never against the last
scroll event alone** *(recorded when the zoom stress run caught the gap)*. The offsets the
component mirrors follow the browser's scroll events, and a reveal's own write is
asynchronous — so between write and echo, the mirror is one step behind. A reveal that
compared its target to the mirror was dropped as "already there" exactly when a previous
reveal's write was still in flight, and that write then landed after it: Ctrl+Home
followed by Ctrl+End faster than one round-trip left the Focus at the far corner with the
view parked at the top, and — since a selection already at the corner changes no more —
nothing ever armed a recovery. The no-op check therefore reads the newer of "last event"
and "last write" (a move that truly needs no scroll still writes nothing, which is the
economy the tests pin).

**What one header click does to the Sorts list** *(settled while wiring the click; "clicking a
column header sorts" above says nothing about the cycle)*. The cycle is **unsorted → ascending →
descending → unsorted**, and a click **replaces the whole list** with at most that one column. The
third state exists so the Consumer's own order is reachable again without reloading. A click on a
column that is not the single sorted one — unsorted, another column, or a multi-column list a
Consumer set up — starts over at ascending on the clicked column: the click means "sort by this",
not "amend what was there". Multi-column sorting stays expressible through the `Sorts` model for a
Consumer or a column menu ([ADR-0010](./0010-chrome-seams-column-menu-editor-loading.md)); the
single click stays the single-column gesture it was given for. The pure cycle lives in `SortCycle`,
and an Action Column — unsortable by declaration
([ADR-0020](./0020-action-and-template-columns.md)) — ignores the click rather than sorting by
nothing. The header is one delegated click target the way the row Viewport is: its cells are
`pointer-events: none` and the column is read from the geometry, not from two hundred per-cell
handlers ([ADR-0004](./0004-cap-the-cells-touched-per-frame.md)'s economy).

## Consequences

- **The mouse resolves to these same three transitions, and three details had to be settled**
  *(added while implementing the mouse)*. Shift outranks Ctrl, so Ctrl+Shift+click extends the
  range being built and leaves the others standing — which is what Excel does and what
  `ExtendTo` already means. **Meta counts as Ctrl where Meta is Command**, because the gesture
  a Mac user makes for "add a range" is Cmd+click. *(Corrected while wiring the keyboard: the
  original said "Meta counts as Ctrl" flatly, which is wrong off an Apple keyboard. **On
  Windows and Linux the Meta key is the OS's** — Win+Arrow snaps a window, Super+A opens a
  shell — and folding it there means the grid acts on whichever of those chords the window
  manager happens not to grab. So Control is the primary modifier everywhere, Meta is primary
  only on an Apple platform, and a Meta held anywhere else keeps the key **out** of the table
  rather than letting it pass for an unmodified one: a Win+Down that canonicalised to a bare
  ArrowDown would move the selection under a gesture aimed at the desktop. Which platform it
  is can only be answered by the browser, so it is asked once at attach and passed in — the
  keyboard and the mouse read the same answer, or Ctrl+click and Ctrl+A would disagree about
  which modifier adds a range.)* And **a toggle-off does
  not begin a drag**: it leaves Anchor and Focus detached, so dragging on from there would append
  a range nobody asked for. A drag is not a transition of its own — mouse down is `Click` and the
  moves are `ExtendTo`, as `GridSelection` says.
- **All of this rides on the capture-phase key handling in
  [ADR-0010](./0010-chrome-seams-column-menu-editor-loading.md).** Outside editing, the arrows,
  Enter and Tab belong to the core. While editing, only the arrows change hands according to
  Overwrite / Caret; Enter and Tab are always the core's (commit, then move by these rules).
- **The Focus must always be visible.** If cycling or Ctrl+arrow takes it out of the Viewport, the
  grid scrolls to it. *(Refined once the keyboard existed: "visible" means inside what the
  **scrollbars left readable**, not inside the box the Consumer declared. Where the platform draws
  classic scrollbars they take about 15px out of that box, and the first implementation
  right-aligned the Focus against the declared edge — putting 15px of it behind the bar on Windows
  and Linux at Ctrl+End, on both axes. On macOS, where the bars are overlays and take nothing,
  this was not visible at all. The gutter is now reported by the browser and comes off the
  geometry before any of these transitions are computed
  ([ADR-0013](./0013-fixed-row-height.md), [ADR-0021](./0021-javascript-is-allowlisted-not-minimised.md)),
  and `tests/ExGrid.Browser/scrollbar.spec.mjs` is what holds it.)*
- **When the Focus leaves the Window, a Range Request is raised**
  ([ADR-0001](./0001-consumer-pushes-the-window-grid-does-not-fetch.md)). A range taller than the
  Window will move the Focus onto rows that have not been fetched. The Focus cell is a Placeholder
  meanwhile, so **editing does not start until the data arrives**.
- **A change of the Row Sequence Version discards the Anchor and Focus too** (ADR-0011 drops
  the whole selection; a change that leaves the visible sequence identical keeps it —
  [ADR-0023](./0023-filter-and-sort-semantics-of-the-reference-implementation.md)).
- **With disjoint ranges, cycling visits them in creation order.** Excel also cycles through all
  ranges, but the exact ordering was not verified. Check against Excel during implementation.
- **Excel's detail where Enter after a run of Tabs returns to the starting column is not
  adopted.** The implementation cost outweighs the benefit. Add it if it turns out to be wanted.
