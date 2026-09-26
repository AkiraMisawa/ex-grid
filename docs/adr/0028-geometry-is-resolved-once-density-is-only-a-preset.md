# Geometry is resolved once, in C#. Density is a preset that resolves into it, and an explicit value always wins

[ADR-0013](./0013-fixed-row-height.md) made the row height a C# parameter. This ADR extends that
to **the whole of the geometry**: every number the virtualisation arithmetic, the overlays, the
editor placement or the width estimates read is resolved **once**, into one value, and everything
downstream — the arithmetic *and* the DOM — reads that value and nothing else.

```
Consumer / Wrapper parameters            (RowHeight, Density, CellMetrics, ...)
        ↓  resolved once, per precedence
GridMetrics                              the single resolved geometry
        ↓                       ↓
virtualisation arithmetic       inline --ex-* tokens on the instance root
(ViewportGeometry,              (what the stylesheet lays out with)
 ColumnGeometry, overlays,
 editor, AutoWidth, ####)
```

One-directional, so the two consumers of a number can never disagree: the DOM lays out with the
same value the arithmetic computed from, because both are projections of the same object.

## What geometry consists of — and what it deliberately does not

`GridMetrics` (a new term for `CONTEXT.md`) carries:

| Value | Read by | Today |
|---|---|---|
| **RowHeightPx** | everything ([ADR-0013](./0013-fixed-row-height.md)) | a parameter already |
| **HeaderHeightPx** | the vertical arithmetic, the spacer, the sticky header | hard-wired to `RowHeight` — see below |
| **FontSizePx** | the stylesheet; states the size `DigitWidthPx` is true at | hard-coded `14px` in `ex-grid.css` |
| **DigitWidthPx** | `####` and Auto width ([ADR-0016](./0016-column-width-and-overflow.md)) | `CellMetrics` parameter |
| **CellPaddingXPx** | the same estimates, and the cells' own padding | `8` in C# **and** `8px` in CSS, paired by a comment |
| **ActionPaddingXPx / ActionBorderPx / ActionGapPx** | an Auto Action Column's width ([ADR-0020](./0020-action-and-template-columns.md)) | `ActionButtonChromePx` in C# **and** three literals in CSS, paired by a comment |
| **MenuButtonWidthPx / MenuButtonInsetPx** *(added later, when review caught the pairing re-entering through the ▾ button)* | an Auto column's **header** estimate, which must clear the menu button ([ADR-0010](./0010-chrome-seams-column-menu-editor-loading.md)/0016) | was `16px`/`6px` in CSS with no C# side at all — the estimate ignored the button |

The last two rows are the point as much as the first: today the C# constant and the stylesheet
literal are kept equal **by a comment saying they must move together**. Two of those pairings
exist already; a Wrapper would add more. Resolving them into `GridMetrics` and emitting them as
inline tokens (`--ex-cell-padding-x`, …) that the stylesheet *consumes* removes the class of
defect, not the instances.

**Deliberately not geometry:**

- **Vertical cell padding does not exist.** Text is centred by `line-height: RowHeight`; a
  vertical padding would be a second way to say the same thing, and two ways to place text in a
  fixed row is one way to misplace it. Denser text in a tall row is a font size, not a padding.
- **LineHeight is derived** (equal to `RowHeightPx`), never supplied.
- **EditorHeight does not exist.** The Cell Editor floats over the focused cell
  ([ADR-0010](./0010-chrome-seams-column-menu-editor-loading.md)) in a box the core sizes:
  exactly the cell — `RowHeight` by the resolved column width. A Chrome whose control cannot fit
  that box is the wrong control for a cell ([ADR-0030](./0030-what-a-design-system-wrapper-owns-and-what-it-may-not-touch.md)).
  Anything taller — a validation message, a picker — is a popover anchored to that box, outside
  the row flow ([ADR-0018](./0018-multiple-instances-must-be-independent.md) already puts
  popovers outside the scroll container).
- **BorderWidth does not exist, because rules are painted, never laid out.** `ex-grid.css`
  already paints the total row's rule as a 1px background gradient, precisely because a border
  would make that row one pixel taller than the arithmetic says (the comment in the stylesheet
  records it). This ADR promotes that from a local trick to the rule: **nothing may add to the
  box of a row or a cell** — no border, no margin, no undeclared padding. Row and column rules
  are gradients or inset shadows, whose width is therefore a *visual* token
  ([ADR-0029](./0029-the-presentation-surface-is-a-short-list-of-classes-and-tokens.md)): it
  changes pixels, and no layout.

## Header Height stops being hard-wired to Row Height

ADR-0013 reused the row height for the header as a deliberate simplification. The refinement that
makes a separate `HeaderHeightPx` safe is that **the cancellation ADR-0013 relies on holds for
any header height**, not only for one row's worth: the sticky header occupies the content's first
`HeaderHeight` *and* covers the Viewport's first `HeaderHeight`, so for a row band placed at
`top: HeaderHeight`,

```
first visible row = floor((scrollTop + HeaderHeight − HeaderHeight) / RowHeight)
                  = floor(scrollTop / RowHeight)        — unchanged
rows band height  = VisibleHeight − HeaderHeight
spacer height     = HeaderHeight + TotalRows × RowHeight
```

Every use of "one row height for the header" generalises by substitution; the arithmetic gains a
constant and loses nothing. The default stays `HeaderHeight = RowHeight`, so an untouched grid is
bit-for-bit what it was. What this buys is real: a Dense body under a comfortable header is an
ordinary design-system request, and the tiered-header question left open in
[ADR-0004](./0004-cap-the-cells-touched-per-frame.md) shrinks — the header's *depth* becomes a
number that already has a home, leaving only the absolutely-positioned group cells unresolved.

## Density is a preset. It resolves into metrics and then gets out of the way

```csharp
public enum GridDensity { Comfortable, Standard, Compact, Excel }
```

| Preset | RowHeight | HeaderHeight | FontSize | DigitWidth | CellPaddingX |
|---|---|---|---|---|---|
| Comfortable | 40 | 40 | 14 | 9 | 12 |
| Standard | 32 | 32 | 14 | 9 | 8 |
| **Compact** (default) | **28** | **28** | **14** | **9** | **8** |
| Excel | 20 | 20 | 12 | 8 | 4 |

- **Compact is the default because 28px is the default today.** A grid that names no density
  changes in nothing.
- **A density is a full metric set, and that is the whole reason the core has the concept.** A
  Wrapper could set a row height by itself; what it cannot do from outside is pick five numbers
  that are consistent *with each other* — a 20px row at 14px type, or a 12px font measured at a
  14px digit width, are the kind of mistake a preset exists to make impossible.
- **`Excel` is a claim, not a mood.** The other three name comfort; this one names the thing it
  reproduces — Excel's default row (15pt ≈ 20px), which is what a user coming from Excel calls
  "normal". It does not mean the other presets are less Excel-*like*: density is spacing,
  operability is the product's claim everywhere.
- **The core's density is nobody else's density.** MudBlazor's `Dense` maps to **Compact**, not
  to Excel — Material's dense table is nowhere near a spreadsheet row. The mapping belongs to the
  Wrapper ([ADR-0030](./0030-what-a-design-system-wrapper-owns-and-what-it-may-not-touch.md)),
  and the core never sees the word `Dense`.
- **The numbers above are declared, not measured.** They are recorded so the first measurement
  knows what it is changing (the same footing as ADR-0004's fling thresholds).

### Precedence — per value, not all-or-nothing

```
explicit parameter  >  Density preset  >  core default (Compact)
```

`Density="Excel" RowHeight="22"` is a 22px row with Excel's font, padding and digit width.
`CellMetrics` keeps its place as the explicit override for the measurement pair, above the
preset. Resolution happens once, in the core, at parameter time — a Wrapper passes words and
numbers in, and exactly one `GridMetrics` comes out. There is no way to hold two opinions.

## Changing geometry at runtime

Any change that reaches `GridMetrics` triggers **one** recompute — the same `PrepareRender` path
a scroll takes — in the same batch that repaints the DOM. There is no path on which the token
changes and the arithmetic does not, or the reverse, because both are projections of the new
value. Beyond that, four things need an answer:

- **The scroll anchor is the first visible row, not the pixel offset.** At 1,000,000 rows,
  changing 28px rows to 20px while keeping `scrollTop` would silently teleport the user 30% of
  the dataset away. The core computes `newScrollTop = firstVisibleRow × newRowHeight` and sets it
  through the allowlisted scroll write ([ADR-0021](./0021-javascript-is-allowlisted-not-minimised.md)),
  then lets the ordinary reveal pass run: if the Focus was visible before, it is visible after
  ([ADR-0012](./0012-anchor-focus-and-keyboard-navigation.md)); if it was off-screen before, it
  stays off-screen — a density change is not a navigation.
- **Selection and Focus do not move, because they are not pixels.** They are positions in index
  space ([ADR-0011](./0011-selection-is-rectangles-in-index-space-and-is-dropped-on-reorder.md)),
  and the overlays are re-derived from the new metrics on the same render. A geometry change is
  not a reorder: the Row Sequence Version does not move and the selection is not dropped.
- **An active edit survives.** The uncommitted text lives in the editor, not in geometry; the
  editor's box is re-derived like the overlays. A density change mid-edit moves the input with
  its cell and changes nothing else.
- **A geometry change is not a fling.** Placeholders are for rows that cannot be painted
  ([ADR-0004](./0004-cap-the-cells-touched-per-frame.md)); these rows can.

**And one refusal.** The browser scrolls 2^25 px and no further, and the guard from ADR-0013
still stands: 1,000,000 rows fit at 28px (28,000,028px) and do **not** fit at Comfortable's 40px
(40,000,040px). Switching density on a grid that tall therefore **throws, by the existing rule**
— rather than quietly clamping the tail away. So the Consumer can refuse *first*, the core
exposes the bound instead of making everyone re-derive it:

```csharp
GridMetrics.LargestRowHeightFor(totalRowCount, headerHeightPx)
    == (MaxScrollHeightPx − headerHeightPx) / totalRowCount
```

A Consumer offering a density menu greys out what its row count cannot carry; the exception
remains for the one who did not ask.

**Changes that are not the grid's to handle:** browser zoom changes no CSS pixel, so the
arithmetic holds; what zoom changes is the scrollbar's worth, and that is already observed
([ADR-0021](./0021-javascript-is-allowlisted-not-minimised.md), asserted in the browser layer). A
web font finishing its load changes glyph widths *without* telling C# — the answer is to declare
metrics for the font the grid will end up with, not the fallback (the safe error direction
absorbs the fallback's narrower glyphs). Observing `document.fonts.ready` would be a fifth
allowlist entry; it fits the "the browser reports, the grid never measures" shape, but no
Consumer has hit the problem, so the entry is **not** added — recorded here so the argument is
ready if one does.

## The Viewport can be told to fill its box

`ViewportWidth` / `ViewportHeight` as literal numbers are why the grid cannot yet sit in a
resizable layout: a Wrapper putting the grid in a card cannot write `100%`, because a percentage
is a fact only the browser knows. That fact is **already being reported**: the gutter observer
watches the scroller's content box and calls in when it changes
([ADR-0021](./0021-javascript-is-allowlisted-not-minimised.md)). The same notification carries
the box's size — the observer gains no new reason to exist and no new moment to fire; the report
grows a field. This is a *use* of the fourth allowlist entry, not a fifth entry.

So the parameters become

```
ViewportHeight = 480    the Consumer declares the size; the report only subtracts the gutter (today's behaviour)
ViewportHeight = Fill   the Consumer sizes the box in CSS however it likes; the report IS the size
```

- Under `Fill`, the CSS may say anything — `height: 100%`, a grid track, a flex basis — because
  the element's size stops being an input the Consumer asserts and becomes an observation the
  browser delivers. The invariant is intact: **the arithmetic still never reads the DOM; it is
  told.** One frame is painted from the previous size on every resize, which is the same accepted
  frame the gutter already costs (ADR-0013), and for the same reason: the alternative is a layout
  read before every paint.
  *(Wrong as written, found 2026-09-26 and rewritten with the user; see "Which element takes the
  box" below. "The CSS may say anything" named no element it could say it to: the root and the
  scroller are internal, and [ADR-0029](./0029-the-presentation-surface-is-a-short-list-of-classes-and-tokens.md)/[0030](./0030-what-a-design-system-wrapper-owns-and-what-it-may-not-touch.md)
  keep them off-limits.)*
- The feedback loop the gutter analysis worried about still cannot form: `.ex-spacer` is sized
  from totals, never from the Viewport, so a reported size changes nothing that would re-change
  the size. *(Wrong as written, found 2026-09-26: a loop did form, on the height. With no height
  given to it, the scroller is as tall as its content. Before the first report that content is the
  header band alone, because nothing is sliced until a positive height arrives. The browser
  reports the band, the band leaves no room for a row, and the grid settles there, painting
  nothing, in a 300px box or any other. Measured on the container's Chromium.)*

### Which element takes the box — decided

*(Decided with the user, 2026-09-26.)* **Under `Fill`, the core gives its own elements the
parent's size.** It writes inline, as it writes every other piece of geometry
([ADR-0027](./0027-appearance-travels-in-css-geometry-travels-in-csharp.md)):

- on a Fill **height**: `height: 100%` on the root and on the scroller, and
  `flex: 1 1 auto; min-height: 0` on the root;
- on a Fill **width**: `min-width: 0` on the root, since a block already takes its parent's width.

The `flex` and `min-` values do nothing in an ordinary block parent. In a flex parent they let
the grid take what is left beside a toolbar and shrink below its content. So one declaration
serves both of the layouts a Consumer actually writes. **The Consumer's side of the contract is
one sentence: give the parent a definite height** — pixels, a percentage of something definite,
a grid track, or a flex item.

Measured with those values on the container's Chromium: a 300px box, and the remainder of a
360px flex column under a 60px toolbar, each paint a 285px Viewport with 11 rows.

Rejected:
- **CSS on `.ex-grid` or `.ex-scroller` in the Consumer's stylesheet.** It works, and it is the
  internal-class styling ADR-0029/0030 refuse.
- **`Class` / `Style` on the root.** Refused by ADR-0029 for reasons that still hold.
- **`position: absolute; inset: 0` on the root.** It needs a positioned parent and takes the grid
  out of the flow, so a toolbar above it would have to be positioned too.

### A parent with no height is named, not guessed at — decided

*(Decided with the user, 2026-09-26.)* `height: 100%` of a parent whose own height is `auto`
is `auto`. A flex item left at its default `min-height: auto` can behave the same way. The
scroller is then as tall as its content, the loop above settles on the header band, and nothing
is painted. **The grid paints nothing and writes one warning** naming the cause and the fix: the
parent of a Fill-height grid has no definite height. The signature is exact: under a Fill
height, the reported height equals the header band. It is written through Blazor's own
`ILogger`, once per instance until a real height arrives, so no script is added
([ADR-0021](./0021-javascript-is-allowlisted-not-minimised.md)). A correct layout never writes
it, which keeps CON-3 at zero.

Rejected:
- **Throwing, as a declared height too small for a row does.** A declared number is wrong when
  it is written. A reported one passes through states: a parent sized a moment after the grid,
  or a collapse animated through the band's height. An exception would take the grid down on a
  transient, and a layout that settles a frame later would never recover.
- **A minimum of a few rows.** Something usable would show, at a size nobody asked for, and the
  layout mistake would stay hidden.
- **Nothing at all.** An empty grid is not a plausible wrong value, so this was never quietly
  wrong. But it was silent about why, and the fix is one line the grid can name.
- A reported size of zero — the grid is in a hidden tab, a `display: none` ancestor — paints
  nothing and throws nothing. Today's validation ("ViewportHeight must exceed RowHeight") is
  right for a number a Consumer *wrote* and wrong for one the browser reported: a declared 12px is
  a bug to refuse, a measured 0px is a Tuesday. The refusal keys on which of the two it was.

## Consequences

- **`GridMetrics` and `Density` are new terms for `CONTEXT.md`**, and `CellTextMetrics` becomes a
  projection of `GridMetrics` — the pure width/overflow layer of ADR-0016 keeps its type and its
  tests untouched.
- **The stylesheet loses its literals.** `font-size: 14px`, `padding: 0 8px` and the action
  button's `6px/1px/4px` become `var(--ex-…)` reads of tokens the core emits; the "must move
  together" comments come out, because the pairing becomes structural.
- **A row-height-only change re-renders no existing row.** `ExGridRow` takes no `RowHeight`
  parameter — height reaches rows through the token — so the root re-renders, a few rows mount or
  unmount at the edges, and the rest skip ([ADR-0003](./0003-cells-are-plain-markup-by-default-not-components.md)).
  A change that moves `DigitWidth` or padding does re-render every visible row, and must: the
  overflow decisions in the cells are stale. That difference falls straight out of what each
  value feeds, and the render-count layer can pin both.
- **Fixed row height is untouched.** This ADR distributes ADR-0013's rule to more values; the
  variable-height question was re-examined and re-refused there, where the decision lives.

## Open

- ~~The preset numbers~~ — confirmed as **deliberately provisional**, Compact default included:
  they are constants in one resolution table, cheap to move now precisely because no Consumer
  ships yet (moving them later shifts every preset-reliant screen at once). The measurement that
  firms them up — against Excel at several zoom levels, and `DigitWidthPx` for the system stack
  at 600 weight ([ADR-0027](./0027-appearance-travels-in-css-geometry-travels-in-csharp.md)'s
  trap) — is an implementation-phase task, not an open decision.
- ~~Tiered headers~~ — resolved: the other half is designed in
  [ADR-0032](./0032-tiered-headers-are-declared-rectangles-not-a-column-tree.md); `HeaderHeight`
  here is the height of one tier.
- ~~`Fill`'s parameter shape~~ — resolved: a small `ViewportSize` value type with an implicit
  conversion from a number of pixels and a `ViewportSize.Fill` case, so `ViewportHeight="480"`
  keeps compiling unchanged and `ViewportHeight="ViewportSize.Fill"` says what it means. A
  nullable double (null = fill) was rejected as unreadable, and a separate `FillViewport` flag
  was rejected because it lets a Consumer write the contradiction `ViewportHeight=480
  FillViewport=true` — the same reason Source-plus-Window is refused by name (ADR-0001): a type
  that cannot express the contradiction beats a check that catches it.
- ~~Whether `Fill` should debounce~~ — resolved: **no debounce until a measurement asks for
  one**. The observer fires at most once a frame, a report is one root render, and the rows
  skip (ADR-0003). If a window drag stutters on real hardware, that measurement — not this
  reasoning — adds the debounce (the ADR-0003/0008 rule).
