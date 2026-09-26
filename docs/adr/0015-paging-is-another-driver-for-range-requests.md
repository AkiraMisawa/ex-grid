# Paging only changes what drives Range Requests. The architecture does not change

A pager (`1 2 3 … Next`) is available as an optional presentation. **The interface stays push and
rides the same machinery as scrolling.**

```
Scrolling : the visible range moved       → raise "I need rows 500–560"
Paging    : a page button was pressed     → raise "I need rows 500–550"
```

From the grid's side these are the same thing, and on the Consumer's side it becomes `OFFSET` /
`LIMIT`. **Paging and scrolling are not exclusive** — if a page is tall, it scrolls within the
page. The only difference is whether the Consumer passes a `PageSize`.

Nothing else is affected (at most, the scrollbar arithmetic of total rows × row height from
[ADR-0013](./0013-fixed-row-height.md) becomes unnecessary).

## Paging removes the worst case that was left open

[ADR-0004](./0004-cap-the-cells-touched-per-frame.md) found that **a fling over 2000 cells
exceeds the budget in every rendering mode** (25–33 ms on real hardware) and prescribed horizontal
virtualisation and throttling. **A pager has no fling.**

```
Scrolling : full-cell rebuilds happen frame after frame → 25–33 ms, sustained = sluggish
Paging    : one rebuild at the moment a page is clicked → 25–33 ms, once = unnoticed
```

It is sustained repetition that becomes perceptible; a single discrete occurrence is not.
**On screens that use paging, horizontal virtualisation can wait.**

## Ctrl+A selects within the page; selecting everything is offered explicitly

[ADR-0011](./0011-selection-is-rectangles-in-index-space-and-is-dropped-on-reorder.md) settled
Ctrl+A as every row after filtering, but a pager changes the context. A pager gives a strong
impression that "this fifty rows is the world", so Ctrl+A taking ten thousand rows is a large
surprise.

**Select within the page first, then offer "select all N rows"** (the pattern webmail uses). The
offer goes in the selection count display added in
[ADR-0014](./0014-paste-shape-rules-and-selection-count.md).

```
50 cells selected   [ Select all 10,000 rows ]
```

The rule stays single — **Ctrl+A selects the context that is visible, and going beyond it is
requested explicitly.** While scrolling, the visible range is a continuum of the whole result, so
Ctrl+A is everything; while paging, the context is the page.

Rejected:
- **Always everything** (ADR-0011 unchanged) — consistent, but jars against the pager's context.
- **Always within the page, with a separate select-all operation** — the same Ctrl+A would mean
  different things in different modes. Rejected for the reason ADR-0011 rejected the "disjoint
  whole rows only" option: the rule cannot be explained.

## The keyboard crosses pages; dragging stops at the boundary

**Shift+arrow and Shift+Ctrl+arrow turn the page and the selection continues.**
[ADR-0012](./0012-anchor-focus-and-keyboard-navigation.md)'s "the Focus must always be visible"
simply becomes "turn the page" under paging. Enter / Tab cycling behaves the same way.

This does not contradict the Ctrl+A decision above. **The principle is that a single operation
does not silently vault past the visible context**, and Shift+arrow is deliberate extension one
row at a time.

**Dragging stops at the page boundary.**

```
Scrolling : the pointer passes the bottom edge → auto-scroll
            continuous; overshoot and dragging back undoes it
Paging    : the pointer passes the bottom edge → turn the page?
            discrete; the content under the pointer is replaced wholesale
            dragging back bounces to the previous page and oscillates
```

Mixing a discrete transition into a continuous gesture makes it impossible to stop where intended.
Pager UIs where dragging turns pages are also vanishingly rare. **The accurate way to build a
long cross-page selection is to turn the page and Shift+click**, which is what people already do
in file listings.

When a page is tall enough to scroll internally, **auto-scroll works normally within the page**.
Only the page boundary stops it.

## Turning the page keeps the selection. Being off-screen is shown

Selection is rectangles over positions in the whole result (ADR-0011), so turning the page does
not move it. What changes is only what is displayed. **It has to stay, or "turn the page and
Shift+click" does not work.**

The danger is pasting while the selection is invisible.

```
1. Select 50 rows on page 1     2. Turn to page 3
3. Nothing appears selected     4. Paste a value
5. → the invisible 50 rows on page 1 are rewritten
```

**Close it with one more line in the selection count display** from ADR-0014. The test is whether
the selection rectangles intersect the visible range — **no data required**.

```
selection on screen    50 cells selected
selection off screen   50 cells selected (outside the visible range)  [ Go to selection ]
```

**This works the same way for scrolling** — it appears after scrolling far from a selection too.
It is not a paging-specific fix; it closes a hole that was already there.

## Consequences

- **A `PageSize` produces a pager; its absence produces continuous scrolling.** Internally both
  are driven by the same Range Request.
- **`TotalCount` feeds the pager's display (`3 / 200`).** It is already being passed in the push
  form.
- **The "select all N rows" offer may live in Chrome.** Counting is the core's, displaying is
  Chrome's, per [ADR-0010](./0010-chrome-seams-column-menu-editor-loading.md).
- **Show the off-screen indicator while scrolling as well.** Do not make it paging-specific.
- *(Added later, by [ADR-0043](./0043-row-marks-belong-to-identity-and-are-held-by-the-consumer.md):)* **The header of a Mark Column follows the Ctrl+A rule.** Under a
  pager it marks the page, and the whole filtered result is offered explicitly — "mark all N"
  beside "select all N".
