# Find reaches the column headers, and marks a header it finds

*(Decided with the user, 2026-10-21, card by card. The user chose to make ExPivot's Find reach its
column labels now rather than narrow [ADR-0332](./0332-find-reaches-every-product-replace-is-a-write-asked-of-the-consumer-and-the-matches-in-view-are-marked.md)
§1, chose against the recommendation that headers be searched only where a Consumer asks (§2), and
took the recommendation on how a header match is shown (§1) and on ExSheet's column letters (§2).)*

ADR-0332 §1 says ExPivot's Find matches "every cell that is laid out (row and column labels, values
and totals)". ExPivot draws its column Items and value captions as ExGrid's column headers and Header
Groups, and [ADR-0055](./0055-find-is-asked-of-the-consumer-like-sort-and-filter.md)'s Find lands only
on data cells: the Focus cannot stand on a header. So a column label on screen answered "No match", and
ticket 03 shipped with that said on the page as not done yet. The question was whether Find reaches
headers at all, how a header it finds is shown, and where.

## Decision

### 1. A header Find finds is marked, and the Focus stays where it is

- **A match in a column header or a Header Group label becomes the current match.** It wears the
  current Match Mark (ADR-0332 §4), is scrolled into view horizontally, and the panel says it was
  found in a header. **The Focus does not move**: a header is not a cell, and moving the Focus to a
  cell under it would show something that did not match.
- **The next step continues from that header**, in the order of §3.
- **Every header in view that matches wears the Match Mark**, as matching cells do, judged by the
  reference rule (`GridFind.Matches`) over the header's displayed text: the grid holds that text, so
  no Consumer's match function is involved.

### 2. Headers are searched in every grid, except ExSheet's column letters

- **Every ExGrid searches its visible column headers and Header Group labels**, ExPivot's column
  labels and value captions included. This changes ExGrid's Find: a step may now stop on a header.
- **ExSheet's column letters (A, B, C…) are not searched.** They are addresses, not content, and
  Excel's Find never stops on them. ExSheet turns header searching off through a grid parameter
  (`FindsHeaders`, default on); it is the only product that does.
- A hidden column's header is not searched, as its cells are not.

### 3. The order: the header rows first, then the data rows, by rows, wrapping

- **The headers are rows above the first data row**: the top Header Group row first, down to the
  column header row, each left to right in the grid's current column order. A forward walk goes from
  the current match through the rest of the data rows, wraps to the header rows, and comes back
  through the data rows to where it began; backward is the reverse. A lone match finds itself, as
  ADR-0055 has it.
- **The grid searches the headers itself; the Consumer is still asked only about rows.** The
  Consumer's request and answer are unchanged. The grid reads whether the Consumer's answer wrapped
  (an answer at or before the step's start, forward) and, when the walk passes the header rows first,
  takes the first header match instead. Starting from a header match, the grid asks the Consumer from
  the start of the rows (the end, backward).
- **A selection as the scope (more than one cell selected) searches no header**: the scope is cells.

### 4. Replace never writes a header

- Replace (one) with a header as the current match writes nothing and finds the next match; Replace
  All asks about cells only, as before. A header is not an entry.

## As built *(find ticket 05, 2026-10-21)*

- **The panel names the header it found** ("Found in the header Trader"), so two header finds in a row are both
  announced by the panel's live region; the current header also carries `aria-current="true"`. A Chrome reads the
  name from `FindContext.FoundHeader`. The outcome clears when the current header is forgotten.
- **A header is current by identity, not by position**: a column header by its column's name, a Header Group by its
  tier and its first member. A reorder keeps it current and the next step goes on from it; a header no longer shown
  (its column removed, its group no longer declared, headers turned off or hidden) stops being current.
- **A step whose headers changed while the Consumer answered** lands on a header as the headers now read, and is
  refused as the order having changed only where a header it would land on moved; with headers turned off meanwhile,
  the Consumer's answer stands alone.

## Consequences

- ADR-0332 §1 holds as written for ExPivot, now that the column labels are reached.
- ExGrid's own Find changes behaviour for every Consumer: a search for text that is also a column's
  header stops on that header once per round. That is a Change Note.
- The panel's outcome gains a "found in a header" wording, in both Chromes and every product's words.
- The Match Mark has to be painted on headers, and the current one apart, at Device Pixel boundaries
  as on cells (ADR-0090).
