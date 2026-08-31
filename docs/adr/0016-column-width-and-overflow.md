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
  misreading of its own.

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

**"Size to fit" is an approximation.** It can only fit the rows that have been fetched, not all of
them. The column menu entry in
[ADR-0010](./0010-chrome-seams-column-menu-editor-loading.md) is written on that basis. A true fit
would require the Consumer to compute the maximum width server-side and pass it in; that is
overkill for now.

## Resizing by dragging — open

This ADR **assumes** dragging three times over — "widen the column by dragging", "a dragged
width is persisted as Fixed", "`MinWidth` is the lower bound for dragging" — and **nowhere
specifies the gesture itself**. That is not a decision made here; it is a decision nobody has
made yet. Its own ADR settles at least these:

- **May a drag exceed `MaxWidth`?** This ADR calls `MinWidth` the lower bound outright, but
  gives `MaxWidth` a second job — it is *what gives `####` meaning* — and never says whether it
  also stops a drag. Letting a drag past it means a column the user widened can never hash;
  stopping the drag there means the user cannot see a long value by widening, which is one of
  the three escapes from `####` listed above.
- **Whether it can be done inside the allowlist.** It looks like it can: `pointermove` carries
  `clientX`, C# already holds the current resolved width, and
  `new width = width at drag start + (clientX − clientX at drag start)` needs no layout read, so
  [ADR-0021](./0021-javascript-is-allowlisted-not-minimised.md) would not be touched. **That is
  a reading of the APIs, not a measurement** — nothing has been built or tried.
- **Dragging an Auto column makes it Fixed.** The table above reads that way (a dragged width is
  the user's intent and is persisted), so a drag ends the column's Auto-ness. Worth stating
  outright rather than leaving to be inferred from a table.

*(The gesture for **reordering** columns is unspecified in the same way; the note is in
[ADR-0011](./0011-selection-is-rectangles-in-index-space-and-is-dropped-on-reorder.md), which is
the ADR that already treats reordering as a trigger without saying how it is performed.)*

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
