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

**"Size to fit" is an approximation.** It can only fit the rows that have been fetched, not all of
them. The column menu entry in
[ADR-0010](./0010-chrome-seams-column-menu-editor-loading.md) is written on that basis. A true fit
would require the Consumer to compute the maximum width server-side and pass it in; that is
overkill for now.

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
