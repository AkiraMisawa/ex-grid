# A popover stays inside its grid's box, and scrolls rather than being cut

[ADR-0017](./0017-target-chromium-browsers-only.md) and
[ADR-0018](./0018-multiple-instances-must-be-independent.md) settled where the grid's popovers —
the column menu, the filter panel, the Context Menu — would live: in the browser's **top layer**
through the Popover API, placed with CSS Anchor Positioning. Because both are driven by
attributes and CSS, [ADR-0021](./0021-javascript-is-allowlisted-not-minimised.md) counted popovers
among the things that need no script at all.

**What was built is something else**, and nothing recorded the difference: every popover is an
absolutely positioned element under the instance root, outside the scroll container. That is the
option ADR-0017 listed as rejected ("confine them inside the root … the panel is cut off at the
edge"), and it passed UX-11 because UX-11 asks only that the *scroll container* not clip. It was
found on 2026-09-24, when WR-7 put a grid inside a `MudDialog`: the dialog's scrolling content cut
off the bottom of the column menu.

## Why the recorded answer does not work

Measured in Chromium on the proof-of-concept page, with the column menu of a grid in a
`MudDialog`:

- **The top layer needs script to enter.** A popover reaches the top layer through
  `showPopover()` or through a click on a button that names it with `popovertarget`. The column
  menu also opens from `Alt+↓` ([ADR-0039](./0039-a-popover-takes-the-keyboard-and-may-hold-popups-of-its-own.md)),
  and the Context Menu from a right-click, `Shift+F10` and the ContextMenu key
  ([ADR-0036](./0036-the-context-menu-is-the-column-menu-shape-over-a-selection.md)). None of those
  is a click on a button, so each would need a new entry on the allowlist.
- **The top layer buries Inner Popups.** It sits above everything a design system draws at any
  `z-index`. A popup drawn at the highest `z-index` over a top-layer popover was hidden beneath
  it. `MudSelect`'s list and `MudDatePicker`'s calendar — the Inner Popups ADR-0039 set out to
  support — would open underneath the Wrapper's own filter panel, unusable.
- **`position: fixed` with anchor positioning is still cut.** It escapes an ancestor's overflow
  only where no ancestor carries a transform, and `.mud-dialog` carries one
  (`matrix(1, 0, 0, 1, 0, 0)` — an identity, but a transform), which contains fixed descendants.
  The corners were still covered.

## Decision

**A popover stays under the instance root, and never extends past the grid's own box.**

- The core writes each popover's **`max-height` inline**, from geometry it already holds: from
  the popover's top to the Viewport's bottom. A popover whose contents are taller **scrolls inside
  itself**. Nothing is measured; the numbers are the ones the popover is already placed with
  ([ADR-0027](./0027-appearance-travels-in-css-geometry-travels-in-csharp.md)).
- **The Context Menu opens on the side of the pointer with more room** within the grid, and is
  bounded by that room.
- **In a filter panel, the value list is what gives.** It shrinks and scrolls, so the search
  and the actions — Apply above all — stay in view, under either Chrome.
- The consequence is the point: **any ancestor that shows the grid whole shows its popovers
  whole** — a dialog, a tab, a card with `overflow: hidden`. The Inner Popups a seam's contents
  open are their design system's, drawn outside the root, and stack above the dialog as that
  design system's own popups do.
- **No script is added**, and the grid still never measures.
- **A popover with no room closes** *(decided with the user, 2026-09-25)*. The `max-height` is
  recomputed when the box changes size. Shrink the box far enough and the room falls to nothing:
  the popover would stay open, invisible, and still holding the keyboard
  ([ADR-0039](./0039-a-popover-takes-the-keyboard-and-may-hold-popups-of-its-own.md)). **When a
  popover's room falls below one `RowHeight`, it closes as a Cancel** and the keyboard returns to
  the root. Only a shrink closes it: a popover opened in a box already that small is the
  Consumer's box, and closing it on the next scroll would close it for no change at all. The
  Context Menu's room is the side of the pointer it opened on, the header band included, so it
  closes only once the whole box is under a row. A popover that cannot show one item is unusable, so nothing is lost. Closing every
  popover on any size change was rejected: a Drawer opening or a banner pushing the layout
  changes the box without the user doing anything to the popover.

## What it costs

In a short grid a menu or a panel is cramped, and scrolls, even on a page with room to spare
below it; Excel's own drop-down is bounded by nothing. That is accepted. A popover that is cut
off is quietly wrong — its last items, or its Apply, are simply not there, and nothing says so —
while one that scrolls shows that it holds more. It is the same choice as `####` over a clipped
number.

## Rejected

- **The top layer, entered by `showPopover()`.** Nothing clips it, but it costs a new script
  entry and it buries every design system's popups beneath the grid's own.
- **`position: fixed` with CSS Anchor Positioning.** No script, but any transformed ancestor still
  contains and clips it — and design systems animate their dialogs with transforms.
- **Letting the Wrapper draw the popover through the design system's own (`MudPopover`).** It
  would escape the dialog the way MudBlazor's own menus do, with Inner Popups stacking correctly.
  But the popover would leave the root: the capture-phase gate would no longer hear the keys
  inside it, and each Wrapper would have to re-implement ADR-0039's table — the opposite of
  substituting Chrome changing nothing. The built-in Chrome would still be cut.
- **Restating WR-7 so that the Consumer makes room.** A requirement weakened to fit the
  implementation.

## What this changes elsewhere

- **ADR-0017** §1 and **ADR-0018** §4 point here, each keeping what it said and why it was
  replaced.
- **ADR-0021**'s "Popovers" entry under *What deliberately stays out of JavaScript* is corrected:
  popovers need no script because they stay in the root, not because the Popover API is driven by
  attributes.
- **UX-11** asks that no ancestor cut a popover, not only the scroll container; **WR-7**'s dialog
  clause reads "opens its popovers whole, inside the grid's box, and its Inner Popups above the
  dialog".
