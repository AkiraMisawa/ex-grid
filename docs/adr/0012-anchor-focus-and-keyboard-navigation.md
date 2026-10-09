# Focus / Extent and keyboard navigation — Enter and Tab cycle inside the selection

Selection is driven by two points: the **Focus**, Excel's active cell, which stays fixed while a
range is extended, and the **Extent**, the end that moves. Keyboard behaviour follows Excel. **When
a range is selected, Enter and Tab cycle inside it and never leave it.**

## Focus and Extent

*(Rewritten 2026-09-27 for [ADR-0052](./0052-the-focus-is-excels-active-cell-and-the-extent-is-the-moving-end.md).
This section first made the Focus the moving end of range extension and named the fixed end the
Anchor. Excel keeps its active cell at the fixed end; ExGrid now does too, and the Anchor is
retired.)*

| | Meaning | Moved by |
|---|---|---|
| **Focus** | Excel's active cell: typing enters it, the Cell Editor opens on it, the Name Box names it, `aria-activedescendant` points at it. The end that stays fixed while a range is extended | click, Ctrl+click (a new range), arrows, Ctrl+arrow, Home/End, PageUp/PageDown, Enter / Tab cycling, Ctrl+. |
| **Extent** | the end of the range holding the Focus that moves while it is extended; the grid keeps it in view while extending | Shift+arrow, Shift+click, Ctrl+Shift+arrow, Shift+Home/End, Shift+PageUp/PageDown, a drag |

- **Click** — the Focus is that cell. The selection collapses to one cell
- **Shift+click** (and each move of a drag) — the Focus stays; the range holding it is redrawn
  from the Focus to the clicked cell, which is its Extent
- **Ctrl+click** — on an unselected cell, adds a new range
  ([ADR-0011](./0011-selection-is-rectangles-in-index-space-and-is-dropped-on-reorder.md)) and
  the Focus moves into it. On a selected cell, **takes it out** (see the subsection below)
- **Arrows** — collapse the selection to one cell and move from the Focus
- **Shift+arrow** — the Focus fixed, the Extent moves one step: the edge opposite the Focus moves,
  the range grows or shrinks, and shrinking back through the Focus flips it. On an axis where the
  Focus is on neither edge (Enter or Tab walked it inside), the key changes nothing
- **Ctrl+arrow** — jump to the edge (last / first row vertically, last / first column
  horizontally). Not Excel's "edge of the non-blank block" — query results have no blank rows,
  and finding a block edge would require the whole dataset (ADR-0011)
- **Shift+Ctrl+arrow** — the Extent runs to the edge (or to the Consumer's edge answer, asked for
  the Extent — ADR-0050)
- **Ctrl+Space / Shift+Space** — the range holding the Focus becomes whole columns / whole rows,
  keeping its column / row span (as in Excel); the Focus does not move
- **Ctrl+. (period)** — the Focus moves to the next corner of the range holding it, clockwise; the
  Selection does not change
- **Shift+Backspace** — the Selection collapses to the Focus. **Ctrl+Backspace** — the Focus is
  scrolled into view and nothing else changes

**With disjoint ranges, the range holding the Focus is the one that grows** — the one added last,
unless Enter or Tab has since cycled the Focus into another. Shift+arrow and Shift+click move only
that range, from its own Focus.

### Ctrl+click on a selected cell takes it out

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

*(Rewritten 2026-09-27 for ADR-0052. This first left Anchor and Focus **detached** on the
deselected cell, outside every range, with rules for Shift+arrow, Enter/Tab and the Space pair
from there, and toggling off the last cell left the empty selection. Excel keeps its active cell
inside the Selection, so the detached state is withdrawn.)* After a cell is taken out, the Focus
stays inside the Selection:

- A cell other than the Focus: the Focus stays, and the range holding it is the fragment of its
  old range that contains it. Shift+arrow extends that fragment
- The Focus's own cell: the Focus moves to the next cell of what remains in Tab order — across its
  range, then on into the next range, wrapping — and that cell's range holds it. *(Excel was
  observed for the top-left cell only.)*
- **The only selected cell cannot be taken out**: Ctrl+click on it changes nothing

Which range holds the Focus is **stored, never inferred from geometry** — fragments and overlaps
make it unanswerable by coordinates.

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

*(Withdrawn by ADR-0052, 2026-09-27. This said a Shift+arrow from a range the Anchor was not in
re-anchored at the Focus and started a new range. The Focus is now always in the range that
extends, so a Shift+arrow extends the range holding the Focus from the edge opposite it, and
Shift+click redraws that range from the Focus to the cell pointed at.)*

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

### Whole columns stay whole, and Shift+click on a header extends them

*(Decided with the user, 2026-09-25, while comparing column resizing with Excel. Resizing
several whole columns at once
([ADR-0016](./0016-column-width-and-overflow.md)) depends on both.)*

**A range spanning every row keeps spanning every row under Shift+← / Shift+→**, and a range
spanning every column keeps spanning every column under Shift+↑ / Shift+↓. This is what Excel
does. Before this, Shift+→ after Ctrl+Space redrew the range between the Anchor and the Focus
(ADR-0012's model before ADR-0052), which are two cells, and the whole-column selection collapsed to one row. A user extending a
column selection never asked for that. Only the axis the range already spans in full is kept;
the other axis moves the Extent. *(Refined while implementing, 2026-09-26: the
same holds for the other extensions along an axis — Ctrl+Shift+arrow runs whole columns to the
edge, as it does in Excel, and Shift+PageUp / PageDown keeps a whole-row range whole. They are
the same gesture at a different stride, and leaving them out would collapse the range on one
key and not the next.)*

**Shift+click on a column header selects whole columns**, from the Focus's column to the
clicked one; the Focus stays (ADR-0052). This is Excel's gesture, and it is the mouse route to several whole columns. The
plain click stays a sort, as decided above; the modifier had no meaning on a header before. It
takes the gesture many data grids give to multi-column sorting. That is acceptable because
multi-column sorting here is expressed through the `Sorts` model or the column menu, never
through a header click (see "What one header click does to the Sorts list" below). A Shift+click
does not sort. From Empty it selects the clicked column alone, with the Focus on the first visible row,
as the first key after focus is (KB-9), so the Viewport does not move for a header click.
*(The last two sentences were settled while implementing, 2026-09-26; the first draft read "from
Empty, or with nothing anchored", which named neither the row nor the detached case.)*

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

*(Rewritten 2026-10-01, decided with the user.)* **Escape with nothing left to dismiss releases
Tab, not DOM focus.** The fifteenth Windows run (`verification/2026-10-01-windows-15/`) saw the cost of
the blur in ExSheet: an Escape typed twice out of Excel's habit, once to cancel the edit and once more,
sent the keyboard to `body`, while the cell still looked selected, and the next keys reached nothing.
Excel's Escape with nothing to cancel does nothing. So the root keeps the keyboard, and the next Tab
or Shift+Tab is left to the browser, which moves to the next or the previous element of the page:
a keyboard user still leaves by Escape and Tab, as before, and is never trapped. Any other key after
the Escape means what it always means, and ends the release: a character opens an edit in the
selected cell, an arrow moves, and a later Tab cycles inside the selection again. So does a press
on the grid *(decided with the user the same day, when ticket 77 was built)*: a user who clicks back
into the grid has come back to it. A press elsewhere on the page is not heard, since that would need
a listener outside the grid (ADR-0018, ADR-0021). Escape there is a
change of the keys the gate claims (ADR-0010: a mode change is a different set), so the keys typed
after it are held until it is answered, as after any mode change. It holds for ExGrid and ExSheet
alike, since the reason above holds for both. The options set aside: ExSheet ignoring that Escape, as
Excel does, which brings the trap back and would need another way out (Excel's F6); and keeping the
blur, which loses the keyboard silently.
*(Added 2026-10-02, [ADR-0080](./0080-a-keyboard-field-holds-the-keyboard-so-an-ime-can-start-on-a-selected-cell.md).)* **The release also ends when DOM focus leaves the grid.** The
root's `focusout` tells it, on every grid. So Escape, a press elsewhere on the page and Tab back into
the grid leave no release standing, and the next Tab cycles; ticket 77 had recorded that case as
open. On a grid that edits, the keyboard is held by its Keyboard Field, which is the grid's one tab
stop; the released Tab and Shift+Tab leave from there.

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

*(An Inner Popup — a select's options or a picker's calendar, opened by a seam's contents and
drawn by their design system outside the root — is the innermost layer of all, and the only one
the grid does not close. Its Escape is the design system's, which closes the popup; the next
Escape is the grid's
([ADR-0039](./0039-a-popover-takes-the-keyboard-and-may-hold-popups-of-its-own.md)). *(Corrected
2026-09-24: this said DOM focus would be outside the root, so the gate would never see that
Escape. MudBlazor keeps focus on its control, inside the popover, so the contents now report
the popup, and while it is open the gate leaves a descendant's Escape to it.)*)*

*(Refined 2026-10-01, building
[ADR-0070](./0070-a-consumer-gives-the-keyboard-back-and-hears-escape-leave.md).)* **A held Escape is
one press.** The press peels its one layer, and the browser's repeats of it dismiss nothing more. A
held key's repeats used to peel a layer each: holding Escape closed a Formula Entry's list and then
cancelled the edit under it, losing what was typed, and closing ExPivot's details dialog with
Escape handed the report the keyboard only for a repeat to release it again. *(With the rewrite
above, which met this note on 2026-10-02:)* a held Escape with nothing left to dismiss releases Tab
with its press, and its repeats leave the release standing. They are the same press, not the other
key that ends a release, so the gate does not count them.

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

Shift+PageUp / Shift+PageDown move the Extent by N and the Viewport with it; the Focus stays and
may be left off screen (ADR-0052).

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

*(Refined 2026-09-25, when the grid first ran under Blazor Server.)* **"Newer" is what the
answer describes, not when it arrived.** On a circuit a read of the offsets and a write can
cross on the wire: the browser answers the read before a write sent after it has arrived,
and the answer reaches the grid after the write. Taken as the last event, it reset "where
the scroller is going" to where it had been. `Ctrl+End` then `Ctrl+Home` faster than a round
trip left the view at the last row with the Focus on the first, which is the same failure
this paragraph closed, reopened by arrival order. On WebAssembly it cannot happen, because
the answer to a read cannot overtake an earlier write. The grid therefore counts its writes.
A read that a write overtook is set aside and asked again, and the new read reaches the
browser behind the write. A crossed answer is not half-applied either: the event mirror is
also the model an edge auto-scroll compounds on (ADR-0008), and an old offset would pull a
drag back.

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
  which modifier adds a range.)* And **a take-out does
  not begin a drag**: the Focus it leaves is not the cell the press pointed at, and dragging on
  would redraw a range from there. A drag is not a transition of its own — mouse down is `Click` and the
  moves are `ExtendTo`, as `GridSelection` says.
- **All of this rides on the capture-phase key handling in
  [ADR-0010](./0010-chrome-seams-column-menu-editor-loading.md).** Outside editing, the arrows,
  Enter and Tab belong to the core. While editing, only the arrows change hands according to
  Overwrite / Caret; Enter and Tab are always the core's (commit, then move by these rules).
- **The Focus must always be visible.** If cycling or Ctrl+arrow takes it out of the Viewport, the
  grid scrolls to it. While a range is extended the grid keeps the **Extent** in view instead,
  and the Focus may be left off screen, as in Excel; Ctrl+Backspace brings it back (ADR-0052). *(Refined once the keyboard existed: "visible" means inside what the
  **scrollbars left readable**, not inside the box the Consumer declared. Where the platform draws
  classic scrollbars they take about 15px out of that box, and the first implementation
  right-aligned the Focus against the declared edge — putting 15px of it behind the bar on Windows
  and Linux at Ctrl+End, on both axes. On macOS, where the bars are overlays and take nothing,
  this was not visible at all. The gutter is now reported by the browser and comes off the
  geometry before any of these transitions are computed
  ([ADR-0013](./0013-fixed-row-height.md), [ADR-0021](./0021-javascript-is-allowlisted-not-minimised.md)),
  and `tests/ExGrid.Browser/scrollbar.spec.mjs` is what holds it.)*
  *(Scoped with the user, 2026-09-25.)* **"Always visible" is a rule about the moves the grid
  makes, not about the box.** When the box shrinks, because the window is resized or a Drawer
  opens, the Focus may end up outside the Viewport, and **the grid does not chase it**. The
  scroll offset stays, so what was at the top left stays at the top left. The next key that moves
  the Focus reveals it where it lands. Chasing it would make the Viewport jump on every frame of a
  window drag, which is the user resizing, not the user moving. One case stays revealed: a
  scrollbar that appears because the content began to overflow, and covers the Focus where it
  stood. Nobody asked for that change, so the grid owes the reveal.
- **When the Focus leaves the Window, a Range Request is raised**
  ([ADR-0001](./0001-consumer-pushes-the-window-grid-does-not-fetch.md)). A range taller than the
  Window will move the Focus onto rows that have not been fetched. The Focus cell is a Placeholder
  meanwhile, so **editing does not start until the data arrives**.
- **A change of the Row Sequence Version discards the Focus and Extent too** (ADR-0011 drops
  the whole selection; a change that leaves the visible sequence identical keeps it —
  [ADR-0023](./0023-filter-and-sort-semantics-of-the-reference-implementation.md)).
- **With disjoint ranges, cycling visits them in creation order.** Excel also cycles through all
  ranges, but the exact ordering was not verified. Check against Excel during implementation.
- **Excel's detail where Enter after a run of Tabs returns to the starting column is not
  adopted.** The implementation cost outweighs the benefit. Add it if it turns out to be wanted.

## Added later: what a Consumer may declare instead *(2026-09-27)*

[ADR-0050](./0050-what-exsheet-asks-of-exgrids-core.md) lets a Consumer take Excel's behaviour in
two places where this ADR chose otherwise, and nothing changes for a Consumer who does not ask:
**a header click that selects the column** (with Row Headings beside the rows), and **an edge
answer for Ctrl+arrow**, so it stops where the data ends rather than at the grid's edge.
[ADR-0051](./0051-formula-entry-completion-point-mode-and-the-formula-bar.md) adds **Point
mode**, in which the arrow keys point at cells for a Formula instead of committing. It applies
only while the Consumer's predicate says the caret is at a place where a Reference can go.
Everything above stands for a display grid. ExSheet is the Consumer that makes these declarations.

*(2026-09-27: [ADR-0052](./0052-the-focus-is-excels-active-cell-and-the-extent-is-the-moving-end.md)
reversed this ADR's Focus and Anchor; the text above has been rewritten to it.)*

## Added after the fifth Windows run: a reveal paints where it is going *(2026-09-29, decided with the user)*

**What happened.** On the Server host, a reveal that jumped far showed a Viewport with no rows for
one round trip. Ctrl+↓ to a row past the Window is one such jump. The blank lasted about 50–100 ms at
no added latency, and about 220 ms at 60 ms added. For the same time the status line said
"1 cells selected (outside the visible range)".

**Why.** The reveal rendered the Focus move first and wrote the scroll offset afterwards. The
slice of rows then followed the browser's scroll event, as the paragraphs above describe. Nothing
was lost, but for one round trip the grid showed neither the rows it had left nor the rows it was
going to. The status line also judged "outside" against where the scroller had been, although this
ADR already says a reveal is judged against where the scroller is going. The DC-13 investigation
found it (`verification/2026-09-29-windows-5/results.md`).

**The decision:** the render that writes a reveal's scroll offset also paints the slice at that
offset. That slice holds the rows the Consumer has, and Placeholders for the rows it does not
(ADR-0004, ADR-0001), and the Range Request goes out as it always did. The status line and
anything else that asks "is the Focus in view" read the offset the scroller is going to. The
browser's scroll event still updates the mirrored offsets. When it arrives it confirms the slice
already painted, so it re-renders nothing.

This is not the one-round-trip blank that ADR-0028 accepts for a window drag and ADR-0045 for the
first frame after a resize. In those, the size itself is news from the browser. Here, the grid
chose the offset and knows it.

*(Implemented 2026-09-29.)* The render that paints the target slice sends the scroll write as well.
On Blazor Server the write and the render's batch still travel as two messages, so the write is held
in the browser until the root carries the render's reveal number (ADR-0021, the scroll-offset
entry). Without the hold, about half the far reveals at a 150 ms round trip painted one empty frame
between the two messages.

## Added later: a drag and Ctrl+click on the header *(2026-09-29, decided with the user)*

ADR-0050 gave Headings Excel's drag and Ctrl+click (item 1, 2026-09-29), and **a display grid takes
both on its column header too**. Shift+click already made the header the mouse route to whole
columns; the drag and Ctrl+click finish that route. The plain click still sorts, so the header must
tell a click from the start of a drag, and here it differs from a Heading:

- **A press on a header selects nothing by itself.** Released on the same column, with no other
  column's header crossed, it is a click and sorts, as above. **The drag begins when the pointer
  reaches another column** — its header, or its cells below — and from then on it is a Heading drag
  as ADR-0050 describes, from the pressed column to the pointer's. **A press that became a drag
  never sorts**, even if it is released back over the column it started on. The click a browser
  fires on the common ancestor after a press and a release on different headers is therefore not a
  sort either.
- **Ctrl+click on a header adds or takes out the whole column and does not sort**; Ctrl+drag adds
  whole columns. The modifier had no meaning on a header before: it was ignored and the click
  sorted. Multi-column sorting is still never a header click (above).
- Selecting a column this way is positional, so an unsortable column (an Action Column) is selected
  like any other, as Shift+click already does.
- **Where the Consumer wires a column reorder (`OnColumnOrderChanged`), a header drag stays the
  reorder** [ADR-0011](./0011-selection-is-rectangles-in-index-space-and-is-dropped-on-reorder.md)
  gave it, a Ctrl+drag included. There Shift+click and Ctrl+click still select, and whole columns
  are selected by those. *(Decided with the user the same day, when the implementation met the
  reorder: the two readings of a header drag collided, and the one a Consumer asked for by wiring it
  wins. Google Sheets' way, where a drag on a column already selected moves it and any other drag
  selects, was not taken: it would change the reorder gesture every existing Consumer has.)*

*(2026-10-07, [ADR-0170](./0170-a-copy-outlines-its-range-with-dashes-while-the-clipboard-still-holds-it.md).)*
**The Copied Range is one more layer Escape peels.** It comes after an open popover, a control inside
a cell and an Interactive cell, and before the way out: with a copy outlined, Escape removes the
outline and does nothing else, and the next Escape releases Tab and raises `OnLeave` as before.
Excel's Escape ends copy mode in the same place.

## Added after a run under Citrix: a reveal is repainted *(2026-10-08, decided with the user)*

**What happened.** On a Windows PC reached through Citrix, with the browser's hardware acceleration
disabled by the organisation (`edge://gpu`: Compositing and Rasterization "Software only. Hardware
acceleration disabled"; Edge 151 and Chrome 152, at a scale of 1), Ctrl+↓, Ctrl+↑ and PageDown on the
Docs Site's blotter left the Viewport white. The Focus had moved, and the DOM was right after every
reveal: a trace of each scroll write (`spikes/vdi-reveal-lab` on the branch `lab/vdi-reveal`) read the
offset written, the slice painted at it, and both still there two seconds later. The screen was not.
Price updates inside the slice were not drawn either. As ArrowUp moved the Focus row's band, the rows
under it appeared one by one, in their right places. A scroll by the user, a resize, or any later move
of the offset painted everything. A scroll written from the console, where the offset moves first and
the core places the rows after it on the scroll event, painted at once.

**What was tried there and did not help:** writing the offset a frame after the rows (twice: the first
try landed in the same frame, because the key's task outlasted a frame, and the second waited two);
writing it before the rows land; repainting the scroller (opacity for a frame) or the Viewport (a
background for a frame); making the scroller a layer of its own (`will-change: scroll-position`) or
opaque; compositing the Viewport (`will-change: transform`) or placing it by `top` instead of its
transform. **What helped:** moving the offset again after the reveal — one pixel and back over the
next two frames, or the same 300 ms later.

**Not reproduced anywhere else.** At home every condition that could be matched was matched, one at a
time and together: Chrome for Testing 152, the installed Edge with `--disable-gpu` showing the same
`edge://gpu` status, a real scale of 1, field trials on (the browser launched by hand, not by
Playwright), the CPU slowed tenfold, and the window captured with `PrintWindow` rather than through
the browser, which redraws for a screenshot. All of them painted. What is left is Citrix's own display
path, which this project cannot run. *(2026-10-09: not the only explanation left. The same PC shows the site
through a remote browser isolation mirror; see "the white Viewport was seen through a remote browser
isolation mirror" below.)*

**The decision: a reveal is repainted.** In the frame after the reveal's, the offset moves one pixel
away from the edge it stands at (across, where the rows cannot scroll), and in the frame after that it
moves back. That is the move that painted the rows there.

- **Ordered by frames, never by a wait.** The 300 ms form also helped and is rejected: a delay that
  works is a window that happened to be wide enough (the spine's sixth principle).
- **The core hears nothing of it.** A read of the offset in between answers where the reveal left the
  scroller, so the core paints no slice for the pixel and the reveal's echo still renders nothing
  (above).
- **The user's scroll stands.** If the offset is not where the repaint left it, the user has scrolled.
  The move back is then not made, and the core is told to read the offset again, through the
  scroller's own scroll event, because a read it already made may have answered the reveal's offset.
- **Only reveals are repainted.** The page turn, the edge auto-scroll of a drag and the anchor kept
  across a geometry change write the offset too. None was seen white, so none is repainted. Any that
  is seen white joins.
- **It is a remedy for a fault outside the grid, and recorded as one.** It hides the symptom on the one
  environment where it was seen, and nothing explains why that environment drops the paint. Elsewhere
  the frame after a reveal is drawn one pixel off and the next one back. A test that reads `scrollTop`
  within those two frames sees the pixel, so layer 3 waits for them where it measures right after a
  reveal (`revealRepainted`). If a later run under Citrix shows the repaint no longer needed, it
  comes out.
- **Only that PC can verify it.** Layer 3 checks the mechanism: the order of the writes, that the core
  paints nothing for them, and that a scroll in between stands. Whether it paints under Citrix is
  checked by hand there (VZ-18). *(Checked 2026-10-08 on that PC, with a build of v0.1.0-beta.2
  carrying only this change published to the Docs Site: Ctrl+↓, Ctrl+↑ and PageDown painted.)*

## Added later: the white Viewport was seen through a remote browser isolation mirror *(2026-10-09)*

*(A finding, not a decision. The section above stands as decided.)*

The PC where the white Viewport was seen does not run the Docs Site in its own browser. Its
organisation's remote browser isolation service runs the site in a cloud browser. The PC's Edge and
Chrome show a mirror of that page, in which none of the site's scripts runs. On the mirror,
`typeof Blazor` is `"undefined"` and `ex-grid.min.js` is never loaded. An internal site on the same
PC is not isolated. This came out of a second fault on that PC: an arrow key moved the Focus and also
scrolled the page. The mirror takes the key first, nothing there takes it, and the local browser
scrolls by it, while the key is also forwarded to the grid in the cloud. The grid cannot prevent
that from where it runs, and no CSS tried keeps the scroll off the page.
[`verification/2026-10-09-windows-remote-isolation`](../../verification/2026-10-09-windows-remote-isolation/README.md)
records the investigation.

**What this changes in the section above.** "What is left is Citrix's own display path" is no longer
the only explanation left. Every symptom recorded there fits a mirror that falls behind the page it
mirrors and catches up on a scroll event:

- The DOM was right and the screen was white.
- Price updates were not drawn.
- The rows appeared one by one under ArrowUp.
- A user's scroll painted everything.
- One pixel away and back painted the rows.

The trace read the cloud page, and the screen showed the mirror. It is not settled which of the two
layers, the remote desktop or the mirror, drops the paint. The run that settles it is: the Docs Site
served from that PC's own `localhost`, which the service should not isolate, with the repaint off.
The verification record gives its two outcomes. Until it is run, the repaint stays, and VZ-18 is
checked as it is written.
