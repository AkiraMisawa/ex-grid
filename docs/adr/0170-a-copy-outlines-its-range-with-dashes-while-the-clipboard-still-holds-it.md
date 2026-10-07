# A copy outlines its range with dashes while the clipboard still holds it

*(Decided with the user, 2026-10-07. The question was theirs: in Excel a copied range keeps a
dashed border, so the user can see what is on the clipboard, and ExGrid showed nothing.)*

After Ctrl+C, Excel draws a moving dashed border around the copied cells. It stays while the user
goes on working, survives a paste, and goes when the user presses Escape, starts an edit, or puts
something else on the clipboard. ExGrid wrote the clipboard and showed nothing, so nothing on
screen said what a paste elsewhere would bring.

*(What Excel does is taken from its documented behaviour. It has not been observed on the Windows
host for this ADR; a Windows run that reaches it records what it sees.)*

## The decision

**Once a copy lands, the grid outlines each rectangle it copied with dashes: the Copied Range.**

- **A copy lands** when its write is made. On the `copy` event route that is the event setting its
  data, which cannot fail once made. On the asynchronous route, from Ctrl+C past the Window or from
  a menu, it is the browser's write resolving
  ([ADR-0005](./0005-copy-refuses-rather-than-truncates.md)).
- **A copy that is refused, or a write the browser rejects, lands nothing and changes no outline.**
  The clipboard still holds what the previous outline marks, so that outline stays true.
- **The dashes stand still.** Excel's border moves; the grid's cannot: nothing inside the Viewport
  transitions or animates ([ADR-0027](./0027-appearance-travels-in-css-geometry-travels-in-csharp.md)
  P8, UX-6).
- **It is painted by the overlay** ([ADR-0008](./0008-selection-is-painted-by-an-overlay.md)): one
  element per copied rectangle in each layer, cut to the painted rows, as the fill handle's target
  is. No row knows about it.

A copied multi-range selection outlines each of its rectangles, as Excel's border does. A copy with
headers outlines the cells only, since the header row is not a cell of the grid.

## How long it stays: while the clipboard still holds it

The outline says one thing: **this is what is on the clipboard.** It goes at the first of these.

1. **The clipboard changes by any write other than this grid's own copy**: another application,
   another grid on the page, a text field (inside the grid or out), or a later copy of this grid's
   that was refused. Chrome and Edge report this with the `clipboardchange` event (shipped in 143).
   A change made while the page did not have the keyboard is reported once the page has it again.
   So the user can copy, paste into Excel and come back, and the outline is still there, unless
   something else was copied in between.
2. **Escape**, as one layer of its own in [ADR-0012](./0012-anchor-focus-and-keyboard-navigation.md)'s
   peeling: after an open popover, a control inside a cell and an Interactive cell, and before the
   way out. One press, one dismissal. The next Escape releases Tab and raises `OnLeave`, as before
   ([ADR-0070](./0070-a-consumer-gives-the-keyboard-back-and-hears-escape-leave.md)).
3. **An edit opens**, in the Cell Editor or the Formula Bar. Excel ends copy mode when an edit
   starts.
4. **The coordinates stop meaning what they meant**: the Row Sequence Version or the visible-column
   set changes, the two triggers that drop the Selection
   ([ADR-0011](./0011-selection-is-rectangles-in-index-space-and-is-dropped-on-reorder.md)).
5. **A copied cell shows other text than it was copied with.** The grid keeps, for each copied row,
   a fingerprint of the copied cells' painted text. When a row in the Window is a different
   instance from the one last checked, its copied cells are read again, and a difference drops the
   outline. This is the comparison [ADR-0142](./0142-a-write-is-refused-when-what-the-user-saw-of-its-target-changed.md)
   makes for a write: the painted text, not the row object. Under a Row Key a row whose P&L moves
   keeps the outline over its notional, as long as the notional reads the same. A row the grid does
   not hold is checked when it comes into the Window.
6. **Another copy lands**, whose outline replaces this one.

A paste into the grid (Ctrl+V) leaves the outline, as Excel's does: the clipboard is unchanged, and
the same block can be pasted again. A paste that changes a copied cell drops it under rule 5. DOM
focus leaving the grid leaves the outline too: rule 1 drops it if anything else is copied while the
grid is away.

### Where the browser cannot say that the clipboard changed: no outline

**A browser without `clipboardchange` draws no Copied Range at all.** Without the event, the grid
cannot know whether the clipboard still holds the copy, and an outline around a range that is no
longer on the clipboard is quietly wrong. Chrome and Edge are the targets
([ADR-0017](./0017-target-chromium-browsers-only.md)) and update themselves, so only an old version
takes this path, and the copy works there as before.

### Telling the grid's own write from everyone else's

The event fires for this grid's own write as well. It was measured on Chrome 152
(2026-10-07): one event per write on both routes, about 7 ms after the `copy` event set its data,
and just after the asynchronous write resolved. A text field's copy on the same page fires one too.

**Each instance counts the writes of its own copy that are in flight.** The `copy` event route counts
one when it sets its data. The asynchronous route counts one before it writes, and gives it back if
the write fails, so the count does not depend on whether the event or the promise is first. An event
that meets a count above zero takes one and is the grid's own. Any other event is a change, and the
grid is told.

A foreign change that arrives while a large asynchronous copy is still being gathered is taken for
the grid's own write. The grid's own event then arrives with nothing counted and drops the outline,
although that write landed last. The race costs a missing outline and never a wrong one.

The event's `changeId` is not used. It is the same in every document of the origin, so it cannot
tell one grid's write from another's.

## Considered Options

- **A moving border, as Excel's.** Rejected. Nothing inside the Viewport animates (P8), and the
  dashes alone say which range was copied.
- **Only while the grid holds the keyboard.** This was offered first, because a page cannot know
  what another application does with the clipboard: focus leaving the grid would have ended the
  outline. The user wanted Excel's lifetime and asked whether the grid could notice a change instead.
  `clipboardchange` does that without a permission prompt, so the outline now ends when the
  clipboard changes.
- **Drop it only when the order changes, as the Selection is dropped.** Rejected by the user. Under
  live data a value changes without the order changing, and the outline would stand around a number
  the clipboard does not hold.
- **Fall back to "while the grid holds the keyboard" where the event is missing.** Rejected by the
  user. It would make two lifetimes, and a test for each, for browsers the targets have already
  left.
- **Compare row instances rather than painted text.** Rejected. A row fetched again, or replaced
  under a Row Key, is a new instance with the same text, and the outline would go for nothing.

## The surface

- **Internal class** ([ADR-0029](./0029-the-presentation-surface-is-a-short-list-of-classes-and-tokens.md)):
  `ex-copied-range`, one element per copied rectangle in each overlay layer.
- **No new token.** The dashes take `--ex-focus-outline`, as the dashes of a grid pointed at do
  ([ADR-0058](./0058-a-formula-points-across-grids-through-a-pointing-scope.md));
  `--ex-selection-outline` stays the range outline's alone. They are drawn over a band of the grid's
  ground (`--ex-background`), so they still read where they lie on the Selection's own outline,
  which a copy usually does. Under forced colours they are restated in system colours.
- **JavaScript.** The listener is the clipboard entry of
  [ADR-0021](./0021-javascript-is-allowlisted-not-minimised.md), not a new one: `clipboardchange` is
  the asynchronous Clipboard API's own event, and Blazor has no way to hear it. It is added to
  `navigator.clipboard` per instance and removed with the instance. Each instance counts its own
  writes, so two grids stay independent
  ([ADR-0018](./0018-multiple-instances-must-be-independent.md)). One grid's copy is a change to the
  other, which is the point. A change crosses to .NET only while the instance has an outline to drop.
  No layout is read, and nothing runs per render.

## Consequences

- **ExSheet and ExPivot draw it too**, since the copy is ExGrid's. ExSheet's stylesheet gives the
  dashes the green it gives its outline where no Theme or Wrapper names `--ex-focus-outline`, so
  they are never the core's CanvasText round Excel's green outline. A Consumer that answers the copy
  itself (`CopyAnswer`) gets the outline over the rectangles it was asked for.
- **What a `CopyAnswer` copies past the Window has no fingerprint** for the rows outside it, because
  the grid never held them. Those rows are not compared, and the other five rules still apply. A
  Sheet changes only through edits and pastes, and an edit already ends the outline.
- **No cut.** ExGrid makes no cut ([ADR-0010](./0010-chrome-seams-column-menu-editor-loading.md)),
  so there is no cut outline, and no paste ends one.
- **The criteria are CP-26 to CP-32** in the Definition of Done.
- **Pressing Enter does not paste.** Excel's Enter pastes the copied range and ends copy mode. In
  the grid, Enter moves the Focus as it always has.
