Product: ExGrid
Kind: Added
ADR: 0332

While the find panel holds a search, every matching cell on screen is marked (`ex-match-mark`), and the current match apart (`ex-match-mark-current`), in the Visual Tokens `--ex-match-mark-fill` and `--ex-match-mark-current-fill`; rows scrolled into view are marked as they are painted, and the marks clear when the panel closes or its text is emptied. With a Grid Source the reference rule marks; a Consumer that answers `OnFind` itself passes the rule it finds by as the new `FindMatch` parameter (`FindMatchOf<TRow>` over a `GridFindPattern`), and without one nothing is marked. `GridFind.Matches` is the reference rule for one cell, and `GridFindRequest.Pattern` a step's text and options.
