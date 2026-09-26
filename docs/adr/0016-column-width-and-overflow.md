# Column width and overflow — numbers are not truncated, they become `####`; Auto widths are not persisted

A column carries `Width = Auto | Fixed(px)` together with `MinWidth` / `MaxWidth`. **When a value
does not fit within the maximum, text is cut with an ellipsis and numbers and dates are shown as
`####`** (as Excel does).

## What may be truncated and what may not

```
Text      "Very Long Descript…" → visibly truncated                       … an ellipsis is fine
Number    "1,234,5…"           → looks like a VALID number three digits short … dangerous
```

A user misreads the magnitude. This is the same class of failure as the copy truncation in
[ADR-0005](./0005-copy-refuses-rather-than-truncates.md) and the "colour changed, value stale"
case in [ADR-0007](./0007-edits-are-an-overlay-owned-by-the-consumer.md): **quietly wrong**.

Excel's `####` **shows unreadability in an unreadable form** — it refuses to be half-read. That is
the principle used throughout this design, adopted as-is.

*(Refined while implementing: **Boolean is classified with Text** — a truncated `fal…` is
visibly truncated, not a plausible other value, and letters are proportional, so the
tabular-digit estimate below cannot apply to it anyway. Text and Boolean never become `####`.
The core's decision is total over all column types and answers "show the value" for both, so
no consumer re-derives the classification; the ellipsis stays pure CSS presentation.)*

Rejected:
- **Truncate numbers with an ellipsis too** — consistent presentation, but it looks like a
  different number.
- **Widen the column automatically when a value does not fit** — it prevents misreading, but
  changes a width the user chose. And column widths are View State owned by the Consumer
  ([ADR-0001](./0001-consumer-pushes-the-window-grid-does-not-fetch.md)), so the grid would be
  notifying width changes and **rewriting the user's saved view without their knowledge**.
- **Drop the formatting** (remove thousands separators, use exponent notation) — `1.23E+09` is
  unreadable as a monetary amount, and losing the thousands separator invites a different
  misreading of its own. *(Checked against Excel on 2026-09-25, with the user.)* This is the
  one place Excel and this grid look different, and less different than it seems. Excel does
  this only for the **General** format. It rounds, then switches to exponent notation, and it
  still shows `####` for any number with a fixed format and for every date. This grid has no
  General format. Every column carries its format, so Excel's own rule for a formatted number
  is the rule here.
- **Let text spill into the empty cell to its right**, as Excel does *(rejected with the user,
  2026-09-25)*. Whether a value is readable would then depend on its neighbour being empty, so
  the same column reads differently row by row, and a neighbour scrolled out of the Viewport
  would still change what is painted. Text is cut with an ellipsis: visibly cut, and the same
  in every row.

## Three ways to see the real value behind `####`

- **Show the focused cell's full value at all times.** The same role as Excel's formula bar, and
  the place for it is the selection count display added in
  [ADR-0014](./0014-paste-shape-rules-and-selection-count.md). Excel users already know to look
  there when a cell is unreadable, so nothing has to be learned.
- **A tooltip on hover.** The value is in the Window, so no extra fetch is needed.
- **Widen the column.** By dragging, or through the column menu's "size to fit".

`####` is presentation only, so **selecting the cell and copying yields the correct value** (per
ADR-0005, the raw value goes on the clipboard as `text/html`). **Screen readers are given the
actual value, not `####`.**

## Auto widths are not persisted

| | How the width is decided | Persisted in View State? |
|---|---|---|
| **Fixed** | dragged by the user, or the result of "size to fit" | **yes** (it is the user's intent) |
| **Auto** | computed from content, clamped to `[MinWidth, MaxWidth]` | **no** (only the intent "this column is Auto") |

This is what resolves the objection to "widen automatically" above. **A width decided
automatically is not persisted**, so a saved view is never rewritten behind the user's back; what
is persisted is the intent.

**`MaxWidth` is what gives `####` meaning.** Without an upper bound the column would simply keep
growing and overflow would never occur.

## Auto only ever grows

In the push form, **Auto can only be computed from the rows that have been fetched**
([ADR-0001](./0001-consumer-pushes-the-window-grid-does-not-fetch.md)). If the width moved up and
down as longer values appeared while scrolling, **the columns would judder**.

```
initially  : computed from the first Window
scrolling  : a longer value appears → widen (up to MaxWidth)
             a screen with only short values → DO NOT narrow
```

Not narrowing means it cannot oscillate, and after a few screens it settles in practice. To
narrow, the user presses "size to fit" explicitly (which fixes the width at the content of that
moment).

*(Refined while implementing: the width is measured from **what has been painted**, not from the
whole Window. A Window can hold far more rows than the Viewport shows, and measuring all of them
on every push would cost more than virtualisation saves. Because observation is a monotone
maximum, seeing fewer rows can never narrow a column — a wide value simply grows it as it
scrolls into the Viewport, which is this diagram's direction anyway. Measuring is also driven by
the same signals rendering is — a different Window instance, a different slice on screen,
different columns — and never by a render alone: re-reading the rows every time would let a
value **rewritten in place** grow a column and repaint through the width path, which is the
repaint [ADR-0003](./0003-cells-are-plain-markup-by-default-not-components.md) promises will not
happen.)*

*(Refined while implementing: before the first Window an Auto column stands at `MinWidth` —
the diagram's "computed from the first Window" is the first observations growing that floor.
Observation is per value and order-independent, and what is observed is the value's **full
required cell width, padding included** — the same unit as the resolved column width and the
estimate below — never the inner content width, which is 2×padding narrower. The defaults are `MinWidth` 40px and
`MaxWidth` 400px: at typical grid metrics that is three `#` glyphs at minimum and roughly 48
digits at maximum, so an untouched Auto column effectively never hashes — `####` appears when
the user or the Consumer narrows a column, as in Excel. Both are per-column overridable. A
declared `Fixed` width outside `[MinWidth, MaxWidth]` is refused at construction, not
clamped — a declaration that contradicts its own bounds is an error, not an intent.)*
*(Narrowed on 2026-09-25 to the lower bound; see the end of "Resizing by dragging".)*

**"Size to fit" is an approximation.** It can only fit the rows that have been fetched, not all of
them. The column menu entry in
[ADR-0010](./0010-chrome-seams-column-menu-editor-loading.md) is written on that basis. A true fit
would require the Consumer to compute the maximum width server-side and pass it in; that is
overkill for now.

*(Settled with the user on 2026-09-25.)* "Fetched" means **the whole Window**, not only the rows
being painted. Auto measures the painted rows because it runs on every push. Size to fit runs
once, when somebody asks for it, so a pass over the Window costs nothing that matters. Measuring
only the painted rows would fit the column and then show `####` a few rows further down. After an
explicit "fit", that is the wrong surprise.

## Resizing by dragging — decided

The three assumptions above are now a gesture. **A grip in the right-hand edge of the header cell;
a guide line follows the pointer; the width is applied on release.** Not a live resize:
`ColumnStyles` composes the width as an inline `style` per cell, so a live drag would rebuild every
painted row's markup on every `pointermove`. The guide line is one absolutely positioned element
moving over the top — the same mechanism, and the same reason, as the selection Overlay
([ADR-0008](./0008-selection-is-painted-by-an-overlay.md)). It is also what Excel does, which is
the operability this component's name claims.

Excel also shows the width as a number beside the pointer ("Width: 8.43 (64 pixels)"). **This
grid does not** *(decided with the user, 2026-09-25)*. Setting a width by its number is
something people do when laying out a sheet for print, not when reading a grid. The guide line
already shows where the edge will land.

`new width = width at drag start + (clientX − clientX at drag start)` needs no layout read, so
[ADR-0021](./0021-javascript-is-allowlisted-not-minimised.md) is untouched. A drag ends a column's
Auto-ness: the resulting width is Fixed, because it is the user's intent and the table above
already says the user's intent is persisted.

### `MaxWidth` bounds what the grid computes. It does not bound what the user asks for

The question this ADR left open — *may a drag exceed `MaxWidth`?* — was posed as a dilemma, and the
dilemma does not survive being looked at. `MaxWidth` was given two jobs above, and only the first
of them is about the grid acting on its own:

1. **It clamps the Auto computation** — a bound on what the grid does unasked.
2. **It is "what gives `####` meaning"** — without an upper bound a column would keep growing and
   overflow could never occur.

The second job only ever held for **Auto** columns. A Fixed column hashes whenever its value does
not fit, with `MaxWidth` playing no part — this ADR says as much above, where an untouched Auto
column "effectively never hashes". Stopping a drag at `MaxWidth` would therefore be the grid
overruling an explicit request with a number written for a different purpose.

**A drag is bounded below by `MinWidth` and is not bounded above.** `MaxWidth` goes on clamping the
Auto computation and `SizeToFit`, and goes on refusing a *declared* Fixed width outside the bounds
— `ColumnWidthSpec`'s message already says **declared**, and the check is narrowed to match the
word it already uses.

*(Rewritten on 2026-09-25, decided with the user.)* That last clause did not hold up. The grid
reports a drag and changes nothing. The Consumer records it, and the only way to record a width
is a Fixed `ColumnWidthSpec`. So a drag past `MaxWidth` became a declaration outside the bounds,
and the constructor refused it. The same refusal met a saved view when it was restored. The
DemoHost worked round it by raising `MaxWidth` with every drag, which moved the ceiling on Auto
and `SizeToFit` as well. A declared Fixed width and a recorded drag cannot be told apart, and
should not be: both are a width somebody asked for. **So `MaxWidth` bounds only what the grid
computes — Auto and `SizeToFit` — and a Fixed width is refused only below `MinWidth`.** Above
`MaxWidth` it is kept. That is the rule this section states for the gesture, now also stated for
the value the gesture leaves behind. A Consumer's typo such as `Fixed(4000)` is no longer
refused. It shows as a column plainly too wide, not as something quietly wrong.

*(Also decided with the user, the same day: where the refusal happens.)* `ColumnWidthSpec` is
built before the column it goes into, so its refusal could not say which column it was. That
matters for a ladder of generated columns. **The column refuses a Fixed width below its
`MinWidth`, naming itself.** It also refuses a `default(ColumnWidthSpec)`, which has no bounds.
The spec still checks its own bounds (`MinWidth` positive, `MaxWidth` at least `MinWidth`),
since those need no column. A spec on its own may therefore hold a Fixed width below its
`MinWidth` until it is given to a column. The grid only ever uses a spec through a column.

The asymmetry is not an oversight, and the rule behind it is worth stating on its own: **the bound
that stops a gesture is the one the same gesture cannot undo.** A column crushed to 2px has no grip
left to grab; a column dragged too wide is dragged back. `MinWidth` protects against something
irreversible, `MaxWidth` against nothing.

And "a widened column can never hash again" is not a loss. If the user has widened it until every
value fits, nothing hashing is the correct outcome. `####` is a refusal that appears when it is
needed, not a feature to be preserved.

*(The gesture for **reordering** columns is settled in
[ADR-0011](./0011-selection-is-rectangles-in-index-space-and-is-dropped-on-reorder.md).)*

## The estimate charges per character class, and the default was finally measured

This ADR originally charged **every character at one tabular-digit width**, and `CellTextMetrics`
states the contract that makes that safe: the width supplied is "contractually at least as wide as
any glyph the column's formats emit". The default — `DigitWidthPx: 9`, against the `font-size: 14px`
the stylesheet pins — had never been measured against it. Measured in Chrome, `system-ui`,
`font-variant-numeric: tabular-nums`:

| glyph | weight 400 | weight 600 | against 9px |
|---|---|---|---|
| `0`–`9`, `$`, `¥`, `£`, `−`, `+`, `#` | 8.668 | **9.058** | under by 0.06 |
| `€` | 8.668 | **9.331** | under by 0.33 |
| **`%`** | **12.804** | **13.836** | **under by 4.84** |
| `,` `.` `(` `)` `/` `:` | 4.01–5.20 | 4.40–5.63 | the documented margin |

Two findings, and the smaller one is the one that had been predicted.

- **At weight 600 the digits themselves are 9.058px.** The estimate errs the **unsafe** way on
  exactly the rows this project paints bold — `.ex-row-group` and `.ex-row-total`
  ([ADR-0027](./0027-appearance-travels-in-css-geometry-travels-in-csharp.md)) — which are the rows
  a reader is most likely to be taking a number off. Twelve digits come out 0.69px short. Small,
  and pointing the wrong way: the rule stated above is that an early `####` costs a hover while a
  clipped number costs a misread.
- **`%` is 13.836px, so the contract breaks by 54% the moment a percent column exists** — which in
  a position-and-risk grid is immediately. This had not been noticed at all.

**One number cannot fix it, because `CellMetrics` is one parameter for the whole grid.** A grid
holding both a twelve-digit amount column and a percent column would have to choose between
inflating the amounts by half and clipping the percentages.

**So the estimate charges per character class.** `CellTextMetrics` carries three widths — wide,
digit, narrow — and `EstimatePx` takes the text rather than a character count. It stays O(n) over
the text with no layout read, so neither ADR-0021 nor the "estimate, never measure" premise moves;
only the accuracy does. The three defaults are the measured numbers above, not guesses.

**The stylesheet gives `--ex-font-family` a default, so that the defaults are true of each other.**
It pinned `font-size` and left the family to inherit, while its own comment said that inheriting the
host's font "would silently break the >= any-glyph contract" — and a family at 14px settles the
glyph widths exactly as firmly as the size does.
[ADR-0027](./0027-appearance-travels-in-css-geometry-travels-in-csharp.md) already makes
`--ex-font-family` a metrics-bearing token and obliges whoever sets it to hand back new
`CellMetrics`; what was missing is that **with nobody setting it, the host page's
`body { font-family: … }` was taking the decision and owing nothing.** The core now declares
`font-family: var(--ex-font-family, system-ui, sans-serif)`, which is the stack the widths above
were measured against. A Wrapper overrides the token and pays the obligation, exactly as ADR-0027
and [ADR-0030](./0030-what-a-design-system-wrapper-owns-and-what-it-may-not-touch.md) already
describe — nothing in the Wrapper contract changes except that it now hands back three numbers.

### The defaults have to hold on every platform, not on one machine — decided

*(Decided with the user, 2026-09-25.)* The paragraph above calls `system-ui, sans-serif` "the
stack the widths above were measured against". **A stack is not a font.** `system-ui` is a
different family on each operating system, so the table above holds for the machine it was
measured on and says nothing about the others. Measured again on Linux (Chromium 141, headless,
`system-ui` resolving to **DejaVu Sans**, 14px, tabular digits):

| glyph | weight 400 | weight 600 | against the defaults |
|---|---|---|---|
| `0`–`9`, `$`, `¥`, `£`, `€` | 8.908 | **9.742** | digit class under by 0.68 |
| **`−`, `+`, `#`** | **11.731** | **11.731** | **digit class under by 2.67** |
| `%` | 13.303 | **14.028** | wide class under by 0.19 |
| `-` (hyphen-minus) | — | 5.811 | narrow class under by 0.18 |
| `,` `.` `(` `)` `/` `:` | up to 5.463 | up to **6.398** | narrow class under by 0.77 |
| full-width (`評`, `あ`, `１`, …) | 14.000 | 14.000 | charged as a digit: under by 4.94 |

This is the failure the whole ADR exists to prevent. A total row's `123,456,789,012.50` paints
**157.7px** against an estimate of **155.0px**. The core decides it fits, the stylesheet cuts it,
and the reader sees a number ending in an ellipsis. `#` at 11.731px breaks the fill as well:
`floor(content width / digit width)` hashes are wider than the cell, so the `####` itself gets an
ellipsis.

**Each default is the widest value measured for its class on any platform the grid supports**
(Chrome and Edge on Windows, macOS and Linux —
[ADR-0017](./0017-target-chromium-browsers-only.md)). That is this ADR's rule — an early `####`
costs a hover, a clipped number costs a misread — applied across platforms instead of inside one.
It costs a slightly early `####` on the narrower families, which is the direction the rule allows.
Windows and macOS are measured by hand, like the other checks only a person can run, and the
numbers are recorded here. Until they are, the defaults are the widest measured so far.

**`−`, `+` and `#` move to the wide class** *(decided with the user, 2026-09-25)*. At 11.731px
they break the digit class by more than a quarter, and the wide class already covers them. On a
family where they are digit-sized, the cost is about 5px of early `####` per sign, and a number
carries at most one sign. A separate sign class would be more exact but would add a fourth number
for every Wrapper to hand back. **The `####` fill counts `#` at its own width**, so the hashes fit
the cell they stand in.

**A header is charged one full-width em of slack** *(decided with the user, 2026-09-25)*. A header
is proportional letters, and no per-class charge can bound them: `W` is 15.44px, `m` 14.59px and
`i` about 4px in the same family. Charged at the digit width, ordinary mixed-case labels come out
over (`Commission`, `Market Value`, `Counterparty`), but short and capitalised ones come out under
(`Amount` by 3.2px, `MARKET VALUE` by 6.9px). One em covers every shortfall measured. It makes
every header-sized column that much wider, which is harmless for a label. A longer all-capitals
label can still come out short. It is then cut with a visible ellipsis, like any Text, and never
quietly wrong. Measuring the label in JavaScript was rejected: it is the text measurement
[ADR-0021](./0021-javascript-is-allowlisted-not-minimised.md) keeps out.

## Size to fit on a double-click, and what a fit has to count — decided

*(Decided with the user, 2026-09-25, after comparing the gestures with Excel's.)*

**A double-click on a column's grip is Size to fit**, the same command as the column menu's. It
is bounded by `[MinWidth, MaxWidth]`, like the menu entry. The user asked for a fit, but the
width is one the grid computed, and `MaxWidth` bounds what the grid computes (see "Resizing by
dragging" above). The consequence is accepted knowingly. A column dragged out to 600px (above the
default `MaxWidth` of 400) comes back to 400 on a double-click, and a value needing 500 shows
`####` again. The bound exists for text: a description column double-clicked to 2000px is the
case it prevents. A number needing more than about 48 digits does not occur in practice. Excel's
AutoFit has no bound; this is a deliberate difference.

**A press and release on the grip without movement reports nothing.** The threshold is the 4px
the reorder gesture already uses
([ADR-0011](./0011-selection-is-rectangles-in-index-space-and-is-dropped-on-reorder.md)). Before
this, a click reported the column's current width, which turns an Auto column Fixed and puts it
in the saved view — rewriting the view behind the user's back, as rejected at the top of this
ADR. A double-click would do it on every first click.

**Several whole columns move together**, as in Excel. When the grabbed column is covered by a
whole-column range (every row, [ADR-0012](./0012-anchor-focus-and-keyboard-navigation.md)'s
Ctrl+Space), a drag gives every column covered by a whole-column range the new width, and a
double-click fits each of them to its own content. The grid reports one `ColumnWidthChange` per
column, so the Consumer's side does not change. A range that does not span every row selects
cells, not columns, and does not take part.

**What a fit counts in the header.** The header's required width is its label, **plus the menu
button's band**, **plus the sort indicator's room when the column can be sorted**, whether it is
sorted now or not. Auto counted the band and Size to fit did not; neither counted the indicator.
The indicator is always counted because a column that grew when it was sorted would move every
column to its right on a header click, and one that did not would chop its label.
*(Refined while implementing, 2026-09-26: review found the indicator's glyph living as a
stylesheet literal, `content: " ▲"`, beside a C# charge for it — the pairing
[ADR-0027](./0027-appearance-travels-in-css-geometry-travels-in-csharp.md) dissolved. So the mark
stands in a box C# sizes, `--ex-sort-mark-width`, as the menu button's ▾ does: an em for the
glyph and a 6px gap, 20px at 14px. The header estimate charges that box, and the stylesheet sizes
the mark with the same number.)*

**Full-width characters are a fourth class, charged at 1em** — the font size. These are the
characters whose East Asian Width is Wide or Fullwidth. CJK fonts are drawn on the em square, so
this is the one width that does not depend on the family: 14.000px at 14px in every weight
measured. Charged as a digit, they were 35% short, which chopped every Japanese header and left
every Japanese text value's Auto width short.
*(Refined while implementing, 2026-09-26.)* The grid knows the em, because it emits the font
size, so it charges full-width characters at least the font size whatever metrics it was
given — a theme's explicit `CellMetrics` included. Metrics built on their own, outside a grid,
cannot know the em: the uniform form keeps its one width for everything, which is its contract,
and the three-class form charges full-width at twice the digit. A tabular digit is at least half
an em in the text faces measured (0.636em for DejaVu Sans at weight 400), so twice it covers the
em without being told it. That fallback is reasoned, not measured across faces, and nothing the
grid paints depends on it.

## Columns appearing and disappearing, and saved views

Data-derived columns come and go at runtime. Representing them is not a problem — **Column is a
runtime object** (`CONTEXT.md`). Only the interaction with saved views needs settling.

- **A column absent from the saved view** (newly appeared) → **default width, visible, placed at
  the end.** Do not hide it — hiding makes new information silently disappear.
- **A column present in the saved view but no longer existing** → **ignore it.** Not an error.

## Consequences

- **`####` changes only how the cell is painted.** Selection, copy, editing and screen readers all
  deal with the real value.
- **The focused-cell value display may live in Chrome.** Producing the value is the core's;
  painting it is Chrome's (the rule in
  [ADR-0010](./0010-chrome-seams-column-menu-editor-loading.md)).
- **`MinWidth` is also the lower bound for dragging.** A column cannot be crushed until it
  disappears. To hide one, use the column menu's "hide this column", which records the intent
  clearly and goes into the saved view.
- **Deciding `####` requires knowing the text width.** In a non-monospaced face, whether it fits
  is not known until it is painted. Numeric columns assume
  `font-variant-numeric: tabular-nums` and estimate from the digit count (as
  `spikes/render-bench` already does) — which is also why this needs no measurement round-trip
  through JavaScript ([ADR-0021](./0021-javascript-is-allowlisted-not-minimised.md)).
  *(Refined while implementing: the estimate is `character count × digit width + horizontal
  padding`, every character counted at one tabular-digit width. Separators (`.` `,` `-` `/`)
  are narrower in practice, so the estimate errs toward showing `####` one glyph early — the
  safe direction: an early `####` costs a hover, a clipped number costs a misread. The digit
  width the theme supplies is contractually **at least as wide as any glyph the column's
  formats emit** — a currency symbol wider than a digit would otherwise flip the error into
  the dangerous direction. A value estimating exactly at the resolved width fits and is
  shown; hashing is strictly past the bound. The `####` fill is `floor(content width / digit
  width)` hashes, minimum one.)*
- **A column that paints no value is outside both mechanisms.** Action and Template columns
  never hash — `####` is about a value that does not fit, and there is no value being
  painted ([ADR-0020](./0020-action-and-template-columns.md)) — and the rows never grow
  them either: observing their empty cell text would fold in the empty string and hold the
  column at `MinWidth`. *(Decided while implementing.)* A **Template Column's Auto width is
  its header alone**: what the Consumer paints in there is the Consumer's to size. An
  **Action Column's is measured from what it declares** — the cell's own padding once, then
  each button's label at the digit width plus that button's own box (its padding, border
  and the gap to the next), summed because the buttons stand side by side. Both terms are
  needed: the cell consumes its padding before any button is reached, and a button is wider
  than its text; leaving either out resolves a short label like "Open" to a column narrower
  than the single button it holds, which `overflow: hidden` then clips. That estimate and
  the header's are two separate observations, so the column settles at the **larger** of the
  two rather than their sum — a header and the buttons under it never occupy the same row.
  The button's box is a constant paired with `ex-grid.css`, exactly as the cell padding is
  paired with `DefaultCellMetrics`: the two must move together, or the column stops fitting
  the buttons it was measured for. A theme painting icons instead has no text to estimate
  and declares a Fixed width.
- **The digit width covers every painted variant, not only every format** *(refined while
  designing the presentation contract)*. "At least as wide as any glyph the column's formats
  emit" was too narrow: `.ex-row-group` and `.ex-row-total` paint at 600 weight (ADR-0024), and
  a bold tabular digit is wider than a regular one in most families, so a number that estimated
  as fitting can clip on exactly the rows most read. The default digit width must cover the
  boldest weight the grid itself paints, and a Theme that raises a family or weight owes new
  metrics — the metrics-bearing obligation in
  [ADR-0027](./0027-appearance-travels-in-css-geometry-travels-in-csharp.md). *(That
  measurement has since been made — see "The estimate charges per character class" below: 9px
  did **not** cover weight 600 (9.058px), and `%` at 13.836px broke the single-width contract
  outright, which is what moved the estimate to per-class widths rather than a retuned
  constant.)*
- **Alignment is a closed enum, not a stylesheet hook** *(added with the tiered-header design)*.
  `CellAlign { Auto, Left, Center, Right }` on the column (`Align`, and `HeaderAlign` for its
  header cell): `Auto` derives from the type — Number/Date right, Text/Boolean left, exactly
  today's `ex-cell-numeric` behaviour — and an explicit value beats the derivation, which is
  safe: `####`, the ellipsis rule and copy are all alignment-blind. Painted as interned
  `ex-align-*` classes (allocation-free, the `CellClasses` mechanism); a Header Group's label
  defaults to Center ([ADR-0032](./0032-tiered-headers-are-declared-rectangles-not-a-column-tree.md)).
  There is no vertical alignment anywhere: a single-line fixed row centres by construction, and
  a multi-tier header cell centres in its rectangle by arithmetic.
