Product: ExGrid
Kind: Behaviour
Docs: exgrid/appearance#visual-tokens
ADR: 0206

A selected range's shade is now an element of the grid's own, painted beneath the range's box, and `ex-range` names that box and its outline only. A stylesheet that coloured the shade through `.ex-range::before` no longer reaches it: set `--ex-selection-fill` instead.
