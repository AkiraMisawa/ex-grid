Product: ExGrid
Kind: Fixed
ADR: 0009

A substituted Chrome's filter panel can no longer apply an operator the core did not offer the column: a `FilterSpec` handed to `FilterPanelContext.Apply` with a clause whose operator is not in `Allowed` is refused, and the panel stands, as an empty value list already was. It reached the Consumer before, and `GridSource.From` threw on it.
