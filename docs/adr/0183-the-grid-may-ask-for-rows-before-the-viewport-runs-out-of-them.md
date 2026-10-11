# The grid may ask for rows before the Viewport runs out of them

*(Decided with the user on 2026-10-11, from the measurement of aligned blocks beside N rows either side,
`verification/2026-10-11-linux-remote-read-ahead-blocks/`. It amends
[ADR-0001](./0001-consumer-pushes-the-window-grid-does-not-fetch.md).)*

ADR-0001 has the grid raise its Range Request once the rows on screen are not all in the Window. That
is the first moment a source can learn that the user is heading for rows it does not hold, and by then
those rows are on screen as Placeholders until the answer arrives. However far a source reads ahead
([ADR-0025](./0025-what-the-bundled-fetching-source-promises.md),
[ADR-0182](./0182-a-remote-reports-window-is-read-ahead-sixty-rows-either-side.md)), each time the
read-ahead runs out, one round trip shows Placeholders.

ag-grid asks earlier. It renders `rowBuffer` = 10 rows outside the Viewport, and its Server-Side row
model asks for a block as soon as those rendered rows reach one it does not hold (ag-grid `latest` at
12c6bb0, `gridOptionsDefault.ts`). Measured over ExPivot's remote report at 150 ms a round trip,
asking 10 rows early did more than asking wider:

| | 60 either side | 120 either side | 60 either side, asked 10 rows early |
|---|---|---|---|
| Held wheel, narrow / wide | 19 / 23% | 10 / 13% | 8 / 13% |
| Held ArrowDown | 8 / 10% | 4 / 5% | 0 / 0% |
| ArrowDown one press at a time: episodes in 480 rows | 7 | 3 | 0 |
| Server projection per live update (wide) | 90 ms | 168 ms | 90 ms |

Held PageDown, which moves 11 to 13 rows a press, gained little: 78 / 56% against 73 / 69%.

## The decision

- **ExGrid takes a Range Request Margin**, a number of rows, 0 by default.
  - At 0 the grid behaves as ADR-0001 says, and FN-3 holds as written.
  - At M, the grid raises its Range Request as soon as the rows on screen plus M rows either side,
    clamped into the total, are not all in the Window. The rows on screen may still be held.
- **The request still names the rows on screen.** Those are the rows the grid needs, and the rows an
  answer must reach (ADR-0025). The grid does not raise it again until the Window moves, as FN-3
  says.
- **A source answers an early request by reading ahead at least M rows.** If it answered only the rows
  on screen, the next move would ask again.
- **ExPivot sets its grid's margin to the smaller of 10 and its report source's read-ahead.** That is
  10 for a remote source at ADR-0182's default, and 0 for a local source, which has no round trip to
  hide. Ten is ag-grid's `rowBuffer`, and the value measured.
- **ExPivot keeps a question in flight while its Window covers the rows on screen,** even when an early
  request for a newer range arrives. The margin is checked against the Window held.
  - The first build superseded the question as soon as the rows on screen plus the margin left its
    Window. Held PageDown then superseded 8 of 9 questions, and showed Placeholders 84% of the time
    (the record's `raw/trial-a657b79`).
- **`GridSource.Fetch` does not use the margin yet.** Its path keeps ADR-0025's rule that a different
  range cancels the fetch in flight, and the measurement found that rule undoes read-ahead under a
  held scroll. Whether ExGrid's bundled fetching source takes the margin, and with it ADR-0182's
  covering rule, is to be measured on its own path first. A Consumer's own source may set the margin
  today.

## Considered options

- **Ask wider instead (N = 120).** It halves the held shares too, at twice the cost of every update.
  Asking early gave as much or more at N = 60's cost.
- **Name the widened range in the request.** Rejected: the rows on screen are what an answer is checked
  against (ADR-0025). A request naming more would refuse an answer that paints everything the user
  sees.
- **A fixed margin inside the grid, on by default.** Rejected. A Consumer that answers exactly the rows
  asked for would be asked again at every move near the edge. So the margin is 0 until whoever reads
  ahead sets it.
- **Blocks with a cache, as ag-grid keeps them.** Rejected in ADR-0182: a live report holds one Window.

## Consequences

- ADR-0001 carries a dated note pointing here.
- FN-3a in the Definition of Done.
- Glossary: **Range Request Margin**.
- Built by ticket 15 of `docs/specs/live-data`. Its ExPivot half needs ticket 14's read-ahead first.
- The early ask's cost when a scroll stops within the margin was not measured. Every sample in the
  record ran past it.
