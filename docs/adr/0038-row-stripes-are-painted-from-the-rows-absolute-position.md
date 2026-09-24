# Row Stripes are painted from the row's absolute position, as one class on the row

[ADR-0027](./0027-appearance-travels-in-css-geometry-travels-in-csharp.md) closed zebra striping
as **not offered**, for two reasons: it cannot be done in CSS (`:nth-child()` counts painted
siblings, so under virtualisation the stripes crawl on every scroll), and "a correct stripe needs
an absolute row index threaded through every row". It pointed at the Focus band as what serves the
need a stripe was for — telling which row you are on.
[ADR-0030](./0030-what-a-design-system-wrapper-owns-and-what-it-may-not-touch.md) meanwhile
listed MudBlazor's `Striped` as **reserved**, with the trigger "a Consumer asking for stripes".
The two disagreed about whether the question was closed.

**The trigger fired on 2026-09-24**, while `ExGrid.MudBlazor`'s remaining work was being decided:
the owner asked for stripes, for the proof-of-concept Consumer page that the Wrapper is now
verified against. And the premise that made them expensive had quietly stopped being true.
[ADR-0033](./0033-the-accessibility-surface-is-owned-by-the-root-not-by-cells.md) threaded the
absolute row index through every row to write `aria-rowindex` — every `ExGridRow` already
receives its position in the whole result, and already re-renders when that position changes.
The parity of that number is free.

**Decision: a Row Stripe is one class on the row, derived from the row's absolute position.**

- **`StripeRows`** (a `bool` on `ExGrid`, default **off**) turns them on. When on, every row
  whose position in the result is odd, counted from zero — the second, the fourth, … — carries
  **`ex-row-stripe`**, and is filled with **`--ex-row-stripe-background`**.
- **Counted in the result, never on the screen.** A stripe stays with its row however far the
  Viewport scrolls; under a pager the count continues across pages
  ([ADR-0015](./0015-paging-is-another-driver-for-range-requests.md)); a Placeholder row is
  striped by its position like any other, so a fling does not flicker the pattern
  ([ADR-0004](./0004-cap-the-cells-touched-per-frame.md)).
- **Pinned cells are striped too.** A Pinned Column's cell carries an opaque ground so that the
  scrolled cells passing beneath it stay hidden; the stripe is laid over that ground rather than
  replacing it, so the band reads straight across the row.
- **Meaning paints over appearance.** Row Kind's grounds
  ([ADR-0024](./0024-row-kind-is-a-declared-role-not-a-hierarchy.md)) and Cell State's
  ([ADR-0006](./0006-grid-owns-a-generic-cell-state-vocabulary.md)) paint over a stripe; a group or
  total row still **counts** in the parity, so the rows around it keep their places. The
  overlays — selection, Focus band, hover band — paint above everything, unchanged
  ([ADR-0008](./0008-selection-is-painted-by-an-overlay.md)).
- **The token's default is a faint neutral**, so a bare grid that turns stripes on shows them;
  under `forced-colors` the block paints no stripe at all, because a stripe carries no meaning
  and forced colours are kept for the things that do (ADR-0027/0029).
- **A Wrapper reaches it the way it reaches the hover band.** `GridPresentationDefaults` gains a
  default for `StripeRows`, which an explicit parameter beats
  ([ADR-0030](./0030-what-a-design-system-wrapper-owns-and-what-it-may-not-touch.md));
  `MudExGridPaper`'s `Striped` maps onto it, and the Wrapper's stylesheet maps the token onto the
  palette's table-stripe colour.

## What it costs

Nothing a row did not already pay. The class is composed from `RowIndex`, interned
([ADR-0027](./0027-appearance-travels-in-css-geometry-travels-in-csharp.md) P5), and a row's
`RowIndex` is already a parameter it re-renders on. Scrolling by one row still re-renders only the
rows that entered or left (RR-4); a row inserted above the Viewport flips the parity of every row
below it, and every one of those rows re-renders anyway, because its `aria-rowindex` changed too.

## Rejected

- **A stripe gradient on the scroll content, anchored to absolute row positions.** No per-row
  work at all — but a Pinned Column's opaque ground, a Row Kind's ground and a Cell State's each
  hide it, and each would need the same gradient re-anchored per element; a Header Group band
  changes the offset it is anchored from. The row class is one rule where this is five.
- **An overlay rectangle per painted row**, the Focus band's mechanism. About twenty elements
  repainted on every scroll frame, against the overlay's economy of one element per rectangle
  ([ADR-0008](./0008-selection-is-painted-by-an-overlay.md)).
- **`:nth-child()`** — the crawl ADR-0027 described, unchanged.

## What this corrects

ADR-0027's "Zebra striping — resolved: not offered" and ADR-0030's "`Striped` — reserved" are both
rewritten to point here, each keeping what it said and why it changed. The Focus band is not
superseded: it answers *where am I*, which a stripe does not — a stripe is appearance, and is off
unless a Consumer asks for it.
