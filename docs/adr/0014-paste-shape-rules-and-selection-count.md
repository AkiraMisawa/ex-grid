# Paste never spills outside the selection. The selection size is shown in the status area

When the shape of what was copied does not match the shape of the target selection, **only two of
Excel's four rules are adopted.**

| | Excel | Adopted |
|---|---|---|
| **1×1 → range** (copy one cell, paste into a range) | the whole range fills with that value | **yes** |
| **Multiple** (copy 2 rows, paste into 6) | repeats three times | **yes** |
| **range → 1 cell** (copy 3×2, paste onto one cell) | expands from that cell as the top-left, **spilling outside the selection** | **no — refused** |
| **Ragged** (copy 3 rows, paste into 5) | refused | refused |

And **the current selected-cell count is displayed at all times** (as Excel does in its status
bar).

## Why "range → 1 cell" is dropped

Everything so far has been designed on the premise that **only what is selected is operated on** —
the copy cap ([ADR-0005](./0005-copy-refuses-rather-than-truncates.md)), bulk entry into disjoint
ranges, and Enter cycling that never leaves the range
([ADR-0012](./0012-anchor-focus-and-keyboard-navigation.md)). "Range → 1 cell" is the only one
that breaks that premise and **writes to rows that were not selected**.

And the result of a paste is recorded as an Overlay and used in downstream computation
([ADR-0007](./0007-edits-are-an-overlay-owned-by-the-consumer.md)). Selecting ten rows and having
five hundred rows change because the clipboard held five hundred is the same class of accident
ADR-0005 avoids, and **the screen looks normal**.

Excel can allow it because all the data is in front of the user and the spill is visible. Here it
can spill **outside the Window**.

It also means **the count shown in the status area and the number of rows actually written would
disagree** — the number on display would be a lie.

The price is behaving differently from Excel, but **it refuses, so nothing breaks quietly.**

## Showing the selection count

Ctrl+Shift+Down looks like "select down to the bottom", but the bottom can be a million rows away.
**Not being able to see how much is selected** is the central danger in bulk editing.

**The grid can produce the selected-cell count on its own** — it is the sum of rectangle areas and
needs no data. Sums and averages cannot be produced (they need data; if they are wanted, that is
the Consumer's job, since only the Consumer has it).

## Rows that are invisible or not yet fetched are included in the selection

Selecting a whole column makes the selection one rectangle covering rows 0 to `TotalCount - 1`,
which includes rows not on screen and rows not yet fetched
([ADR-0011](./0011-selection-is-rectangles-in-index-space-and-is-dropped-on-reorder.md)). Pasting
one value there rewrites every row. **This is intended.**

Copy refusing while paste goes through is not a contradiction. **What each needs is different.**

| | What the grid needs | Possible? |
|---|---|---|
| Copy | to **gather the values** of the selection | it does not have them → refuse |
| Paste | to **write an intent**: "rows N–M, this column, this value" | it can → the Consumer resolves it |

> **The grid refuses because it cannot, not because something is large.**

There are three brakes: **show the scale** (the selection count), **confirm** (the Consumer's job,
since only it knows what a million overrides mean), and **undo** (a bulk paste is one Edit Intent,
so one Ctrl+Z, ADR-0007).

## Consequences

- **There are now three grounds for refusing.** A copy that is too large, disjoint ranges whose
  shapes do not line up
  ([ADR-0011](./0011-selection-is-rectangles-in-index-space-and-is-dropped-on-reorder.md)), and a
  paste whose shape does not match. **Say which one when refusing.**
- **Users arriving from Excel will try "range → 1 cell" and be refused.** The message should say
  that reselecting a target of the same shape will work.
- *(Refined while implementing:)* **The two refused rows carry distinct refusal reasons**, so
  Chrome can attach the "reselect a target of the same shape" message to the single-cell
  case specifically. The multiple rule is formally: a target of M×N accepts a source of m×n
  iff m divides M and n divides N. **A source larger than 1×1 into a disjoint target is
  refused** even when every range is individually a multiple — Excel refuses the same
  operation, and 1×1 (which fills every range: the bulk-entry shape of
  [ADR-0011](./0011-selection-is-rectangles-in-index-space-and-is-dropped-on-reorder.md))
  stays the only multi-range paste. An empty target refuses with its own reason, and
  nothing happens.
- **The selection count display may live in Chrome.** Counting is the core's; displaying is
  Chrome's, per the rule in
  [ADR-0010](./0010-chrome-seams-column-menu-editor-loading.md).
- *(Added later, by [ADR-0035](./0035-paste-and-fill-respect-the-editable-declaration.md):)* **A
  fifth refusal reason joins the four above** — the target covers a column that is not `Editable`.
  It is the first that is a declaration ("may not") rather than a shape ("cannot"), so it is
  checked *before* the shape rules, whose "reselect a target of the same shape" advice would
  otherwise send the user after something that can never succeed.
