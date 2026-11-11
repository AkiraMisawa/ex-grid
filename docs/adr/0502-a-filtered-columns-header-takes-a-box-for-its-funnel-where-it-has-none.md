# A filtered column's header takes a box for its funnel where it has none

*Decided with the user on 2026-11-11, in the project thread "Show filter mark on hideColumnMenu columns"
(the exchange was in Japanese; it is given here in English).
[ADR-0501](./0501-a-filtered-columns-menu-button-is-a-funnel-in-the-standard-header-too.md) section 3 left
a standard header without the column menu showing no mark of a Filter, and said so as a limit. Asked
whether that is a problem, the user was told what closing it costs: a small funnel beside the column's
name in that case only, and the filtered columns alone a little wider. The user said to do it. Where the
funnel stands, what it is charged, and the one compact header that had the same gap are this ADR's, inside
that frame; they are stated in the pull request, and a different answer is a rewrite of this ADR.*

This amends [ADR-0501](./0501-a-filtered-columns-menu-button-is-a-funnel-in-the-standard-header-too.md)
(section 3, which it replaces, and section 2 for these headers),
[ADR-0500](./0500-a-header-may-be-drawn-compact-with-one-box-for-the-menu-and-the-columns-state.md)
(sections 2 and 3: the header with no box, and what a header is charged),
[ADR-0016](./0016-column-width-and-overflow.md) ("What a fit counts in the header") and
[ADR-0029](./0029-the-presentation-surface-is-a-short-list-of-classes-and-tokens.md) (where
`ex-header-state` is drawn).

## Context

A Consumer may leave the ▾ off every header with `hideColumnMenu` and filter its rows through controls of
its own: a filter bar above the grid, a saved view, a query built elsewhere. That grid is still given the
Filter, and its rows are still fewer than its data.

After ADR-0501 its header said so only under the Compact Header Display. Under the Standard one, the
default, a header without the ▾ has no box at all beside its label: its charge is the label, the slack
and the sort mark's box. ADR-0501 weighed a funnel there against its price as it then stood, room charged
to **every** such column at every moment, refused it, and wrote the gap down as a limit.

The gap is the one the spine's first rule is about. A reader of a table of money cannot tell a short
table from a filtered one, in exactly the grids whose filter is set somewhere the header does not show.
And the price ADR-0501 refused was not the only one on offer: the room can be charged to a column only
while it is filtered.

One compact header had the same gap. With `hideColumnMenu`, a column that cannot be sorted has no Header
Box (ADR-0500 section 2), so a Filter naming it was marked nowhere. The grid's own query never filters
such a column, but a Consumer's own source may.

## Decision

### 1. A header with no box takes one while its column is filtered

| Header | Not filtered | Filtered |
|---|---|---|
| standard, with the ▾ | ▾ in its band | the ▾ painted as the funnel (ADR-0501) |
| **standard, `hideColumnMenu`** | no box | **the ▾'s band, holding the funnel and nothing to press** |
| compact, with the ▾ or sortable | the Header Box | the funnel in the Header Box (ADR-0500) |
| **compact, `hideColumnMenu`, not sortable** | no box | **the Header Box, holding the funnel** |

So wherever the Filter the grid is given holds a spec for a column that is painted, that column's header
shows the funnel. There is no header left that stays silent.

- **It stands where every other funnel stands**: at the cell's right edge, in the box the column menu's
  button would have (16px wide, 6px from the edge at every Density), the same picture in the header's own
  colour at full strength. In a column as narrow as its header that is beside the name; in a wider one
  it is where the eye already finds a filtered column's mark under every other setting. The standard
  header's sort mark stays in its own box after the label, so a column that is sorted and filtered shows
  both, as it does with the ▾.
- **It is a mark and nothing to press.** There is no column menu to open, so the box is the `span` the
  compact header already uses where it has no button (`ex-header-state`): it takes no pointer, and a
  click there is the header's, as anywhere else on the cell.
- **It says so in words**: an image named *Filtered* (`gridLabelIds.columnFiltered`, through
  `commandLabel`, with the Japanese word in the bundled table), as the compact header's mark without a
  button is named.
- **It follows the Filter the grid is given**, as every mark of a Filter does (ADR-0501 section 1): the
  column names of `filter.columns`, whoever set them.

### 2. It is charged only while it is there

```
standard:  text + 2·padding + em + menu band (when ▾, or filtered) + sort box (when sortable)
compact:   text + 2·padding + em + (▾ or sortable or filtered ? max(0, menu band − padding) : 0)
```

`GridMetrics.headerRequiredPx` takes `filtered`, and it changes the answer only for the two headers in
bold above: 22px for the standard one at every Density, and 14px for the compact one (10px at
Comfortable). `gridHeaderRequiredPx`, the entry a Consumer charges a header by, takes it too.

- **An Auto width takes the header's new need while the column is filtered, and gives it back when the
  Filter is cleared.** This is the rule ADR-0500 section 4 made for a change of display: what the header
  needs is held apart from what the values taught the column, and replaced. A column its header decides is
  22px wider while filtered; one its values decide does not move unless the header with its funnel is
  now the wider; and the way back is the width the column had. Nothing is measured and nothing is
  reported, because an Auto width is not a width somebody set.
- **A width somebody set is never changed.** A `Fixed` width stands, and no `onColumnWidthChanged` is
  raised. In a column narrower than the charge the box stays whole at the right edge and the label gives
  way, as ADR-0500 section 2 has it for every narrow column. A sortable column fitted before it was
  filtered lends the label its empty sort box, so the label is still whole; once the column is also
  sorted the sort mark no longer fits, and the label is cut with an ellipsis in its place (observed in
  Chrome on `/header-display` with `CCY` declared 80px wide, its fitted width: the funnel whole, 6px from
  the edge, `CCY` whole while filtered and `CC…` once sorted too). The Sort is then read from `aria-sort`
  by assistive technology, or on screen after the column is widened. Size to fit is the way out, as
  everywhere.
- **Size to fit charges the header as it stands**: fitted while filtered, the column is fixed at a width
  that holds the funnel, and keeps it after the Filter is cleared, because that width was set.
- **The width follows the Filter, never the order things happened in.** The same columns, values and
  Filter give the same widths however they were arrived at (the spine's sixth rule).

**This is the one header where a Filter moves a column.** ADR-0016 reserves the sort mark's box whether a
column is sorted or not "so a sort moves no column", and ADR-0501 section 2 said the same of a Filter. Both
still hold of every header that has a box. Here the choice was between a column that moves when its Filter
is set or cleared, a charge on every column of every grid that hides the menu, and no mark; the user took
the first, knowing it.

### 3. What is written to the DOM

- A standard header with `hideColumnMenu` holds `<span class="ex-header-state" role="img"
  aria-label="Filtered">` while its column is filtered, and nothing in its place otherwise: an unfiltered
  one is the markup it was. The cell wears `data-ex-filtered`, as it did.
- A compact header with `hideColumnMenu` draws the same `span` for a column that can be sorted, as
  before, and now also for one that cannot while it is filtered.
- The stylesheet keeps the label out from under the `span` as it does from under the button: the band is
  added to the cell's right padding, the number the charge adds.

### 4. Nothing is measured, and nothing new is listened to

The header's need is charged where the grid lays its columns out, from the Grid Metrics, whether there is
a ▾, the Header Display and the names the Filter holds: all things the grid is told. No layout is read
(rule 2 of the guide for agents), no listener is added, and a hover still changes nothing.

## Considered

- **The band reserved on every column that has no ▾, at every moment.** No column would ever move. It is
  what ADR-0501 refused: 22px on every column of every grid that hides the menu, to mark a state most
  columns are never in. ExSheet and ExPivot draw their grids with `hideColumnMenu` and hand no Filter;
  each of their columns would have paid for a mark they never show.
- **The funnel inline, straight after the label, as the standard sort mark is.** It reads as "beside the
  name" in a wide left-aligned column too. But the same picture would then stand in two places depending
  on an option the reader cannot see, and after a right-aligned label it would sit between the label and
  its sort mark. Refused: one mark, one place.
- **The funnel in the sort mark's box while the column is not sorted.** No charge until a column is
  sorted and filtered at once, and then it needs both boxes anyway, so the column would move on a sort,
  which ADR-0016 holds it never does; and a column that cannot be sorted has no such box.
- **The funnel painted over the label's slack, charged nothing.** ADR-0500's design B, refused there for
  the labels it cuts or covers.
- **No mark, as ADR-0501 section 3 had it.** Decided otherwise by the user.

## Consequences

- **A grid that hides the column menu and is given a Filter changes its look**: its filtered columns
  show the funnel, and those whose Auto width their header decides are 22px wider while filtered (14px
  for the compact header's case). This is a behaviour change with its own Change Note.
- ExSheet and ExPivot hide the column menu and hand their grids no Filter, so nothing of theirs changes.
- The Wrappers set nothing for it: the picture and the box are the ones ADR-0500 and ADR-0501 gave.
- ADR-0501's sentence that the page documenting `hideColumnMenu` says the standard header shows no mark
  is turned round: that page now says the mark is there, and what it costs.
