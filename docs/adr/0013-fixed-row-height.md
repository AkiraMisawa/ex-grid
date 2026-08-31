# Row height is fixed. No in-row expansion; pivot-like views are supported through Row Kind

Every row has the same height. The height arrives as a **C# parameter** (`RowHeight`), and
settings such as a density option resolve into it. **It must not be possible to change the row
height from CSS alone.**

## Why fixed

Almost every decision so far rests on the row height.

```
virtualisation position   position     = row index × row height
selection overlay         rectangle y  = (row index − first row) × row height   (ADR-0008)
cell editor position      same coordinate system                                (ADR-0010)
scrollbar length          total rows × row height
```

Fixed, all of these are one multiplication. Variable, finding where a row sits requires **summing
the heights of every row above it**, which at a million rows needs something like a prefix-sum
index.

It also **collides head-on with the row memoisation** in
[ADR-0003](./0003-cells-are-plain-markup-by-default-not-components.md). If a row decides its own
height, the height is not known until it is rendered — but memoisation exists to avoid rendering,
so **the height would force the render and memoisation would stop working.**

**The reason the height must go through C# is the same.** Implementing a density option as a pure
CSS class leaves CSS at 32px while C# still believes 24px, and the scroll position, selection
outline and editor **drift slightly out of alignment** — a failure whose cause is hard to see
([ADR-0010](./0010-chrome-seams-column-menu-editor-loading.md)).

## What is lost is in-row expansion. Pivots are not lost

"Expansion" means two different things, and fixed row height rules out only one.

```
Group / pivot expansion — possible with fixed row height
  ▼ Group A              120,450   ← a group row (an ordinary row; same height)
      ▼ Subgroup          80,200   ← a group row
            Detail        45,100   ← a detail row
      ▶ Subgroup          40,250   ← collapsed

  expansion = MORE ROWS. no row changes height

Master-detail in-row expansion — needs variable row height
  Detail    45,100
  ┌────────────────────────────┐
  │ another grid or a chart    │  ← the row itself grows taller
  │ INSIDE the row             │
  └────────────────────────────┘
```

Excel's pivot tables also keep a constant row height; **expansion changes the number of rows**,
not their height.

And the first Consumer has already decided that its bucketed drill-downs are **a dedicated
drill-down view**. Those are two-dimensional matrices — grids in their own right — so navigating
to a separate view is more natural than embedding them inside a row. **The one reason that would
have required variable row height is already gone on the Consumer's side.**

## Pivot-like views need almost nothing new

| What a pivot requires | Where it comes from |
|---|---|
| Columns determined by data | **Column is a runtime object.** A statically listed column and a data-derived one are not distinguished |
| Someone computes grouping and aggregation | The grid does not sort or filter
  ([ADR-0001](./0001-consumer-pushes-the-window-grid-does-not-fetch.md)). **It does not group
  either** — the Consumer computes it and puts it in the Window |
| Expanding and collapsing changes the row count | Treated like a sort change. The grid notifies; the Consumer returns a new Window and `TotalCount` |
| Group rows look different from detail rows | Needs a **Row Kind** (detail / group / total). Designed in [ADR-0024](./0024-row-kind-is-a-declared-role-not-a-hierarchy.md): a role the Consumer declares per row, carrying neither depth nor the aggregate |

Only the last is missing, and it has nothing to do with row height — it is about painting and
about what expands.

## Consequences

- **A Row Kind has to be introduced** (detail / group / total). It is distinct from Cell State
  ([ADR-0006](./0006-grid-owns-a-generic-cell-state-vocabulary.md)) — that names the state of a
  value per cell; this names the role of a row. Do not conflate them.
  *(Introduced in [ADR-0024](./0024-row-kind-is-a-declared-role-not-a-hierarchy.md). It carries
  the role and nothing else — no depth, no aggregate, no expansion state — and the height stays
  this ADR's fixed one.)*
- **Expanding and collapsing become notifications.** The grid knows neither the row hierarchy nor
  how many rows an expansion adds. The Consumer pushes a new Window.
- **If in-row expansion is ever needed, this ADR is revisited whole.** It cannot be added
  incrementally — the coordinate arithmetic, the memoisation, the selection overlay and the editor
  position would all be rewritten against a variable height.
- **Cell contents are assumed to fit on one line.** Long text is cut
  ([ADR-0016](./0016-column-width-and-overflow.md)). There is no wrapping onto multiple lines.
- **`RowHeight` is a public parameter and must not be overridable from an external stylesheet.**
  It is emitted as a CSS variable, but the truth lives in C#.
- **So is the Viewport's height** *(refined while implementing)*. The same arithmetic needs to
  know how tall the scrolling area is, in order to answer how many rows fit. Measuring the
  element would mean a layout round-trip through JavaScript, which is deliberately not on the
  allowlist ([ADR-0021](./0021-javascript-is-allowlisted-not-minimised.md)) — and a height that
  only CSS knew would drift the painted rows away from the arithmetic in exactly the way this
  ADR is about. `ViewportHeight` is therefore a C# parameter emitted inline, like the row
  height, and the two together are the whole of the vertical geometry.
- **And so is its width** *(refined while implementing horizontal virtualisation)*. Which columns
  are on screen is the same question on the other axis, and `getBoundingClientRect` is off the
  allowlist for the same reason. `ViewportWidth` is a C# parameter alongside the height.
- **`ViewportHeight` includes the header, which is exactly one row tall** *(refined while
  implementing horizontal virtualisation)*. The header moved inside the one scroll container so
  it can be held by `position: sticky` beside the Pinned Columns
  ([ADR-0004](./0004-cap-the-cells-touched-per-frame.md)), which makes it part of the scrollable
  content: it occupies the content's first row height **and** covers the Viewport's first row
  height. The two cancel, so `first row = floor(scrollTop / RowHeight)` is unchanged and the rows
  simply get `ViewportHeight − RowHeight` to fill. Reusing the row height rather than adding a
  header-height parameter is a deliberate simplification, and **the thing that would overturn it
  is tiered headers** — a header several rows deep stops being one row tall, and the meaning of
  `ViewportHeight` changes with it. That is recorded as open in ADR-0004.
  *(One consequence is not merely left standing but refused: the header band spends one row height
  of the browser's 2^25 px scrolling budget, so the true ceiling is one row below the
  `MaxScrollHeightPx` guard — about 1.19 million rows either way, and paging carries anything
  longer. `ViewportGeometry` knows only about rows and cannot see this, so **the component checks
  the spacer's real height itself**: recording the edge in prose and letting the browser clamp the
  last row away in silence is the failure this ceiling exists to refuse.)*
- **`ViewportHeight` and `ViewportWidth` are the element's outer size, and the browser tells the
  grid what its scrollbars take out of it** *(refined while implementing selection; the second
  half rewritten once the keyboard existed — see below)*. Where the platform draws classic
  scrollbars rather than overlay ones, roughly 15px of each axis goes to its bar: the element
  stays the size it declared and its **`clientWidth` shrinks**. The parameters keep meaning the
  outer size, because that is what the CSS is written from; the **Scrollbar Gutter** is subtracted
  once, in `ViewportBox`, and both geometry types are built from what is left.

  > **The first version of this bullet said the allowance was the Consumer's to add**, on the
  > grounds that the grid cannot measure a scrollbar without the layout read the allowlist
  > refuses. Wiring up the keyboard showed both halves to be wrong. The Consumer cannot make the
  > allowance: **the same application ships to macOS, where the gutter is 0, and to Windows, where
  > it is about 15px**, so any constant written into `ViewportHeight` is wrong for half its users
  > — and the bullet's own "nothing breaks" was already false, because with the Focus at the last
  > row and the last column, 15px of it sits behind each bar and
  > [ADR-0012](./0012-anchor-focus-and-keyboard-navigation.md)'s "the Focus must always be
  > visible" is broken on two platforms out of three. Invisibly so on the third, which is where
  > this component is developed. And the grid does not measure anything: the browser **reports**
  > the gutter when it changes, which is a different mechanism from a layout read and is
  > allowlisted separately ([ADR-0021](./0021-javascript-is-allowlisted-not-minimised.md)).
  > CSS could not have closed this: `scrollbar-gutter: stable` reserves the vertical strip only,
  > and does nothing at all where the scrollbars are overlays.

- **The gutter's own arithmetic cannot feed itself, and one frame is spent behind it.** The
  scrollable area (`.ex-spacer`) is sized from the total row and column count, never from the
  Viewport, so a narrower visible box does not resize the content that made the bar appear —
  there is no loop for the observation to chase, and nothing writes to the DOM inside the
  callback, so the browser's own "ResizeObserver loop" warning has no way to arise either.
  What is accepted is the other end: **the frame between a bar appearing and the report landing
  is painted from the old visible size**, one slice too generous on a grid that has just gained a
  scrollbar. The alternative is a layout read of our own before every paint, which is the round
  trip this whole ADR exists to avoid.
- **A scrollbar can leave a Viewport with nothing to paint in, and that is refused by name.** A
  declared height is checked against `RowHeight` when it arrives; the same rule is checked again
  after the gutter comes off, and the message names the scrollbar rather than the height — the
  number the Consumer actually wrote is a perfectly ordinary one until a bar is drawn inside it,
  and on a Mac the same grid would have painted.
- **"Scroll to this row" is arithmetic here too, and it is not symmetric with the column axis**
  *(refined while implementing)*. `ViewportGeometry.ScrollTopToReveal` and
  `ColumnGeometry.ScrollLeftToReveal` exist so that nothing outside them adds or subtracts a
  header height or a pinned width — the same reason the Auto/Fixed branch lives in one place
  ([ADR-0016](./0016-column-width-and-overflow.md)). The header cancels out, as above; a Pinned
  Column does **not**, because it covers the Viewport's edge without occupying anything ahead of
  the content, so its width has to come off the offset or the revealed column lands underneath
  it. The browser's `scrollIntoView()` knows neither, which is why it is not used.
