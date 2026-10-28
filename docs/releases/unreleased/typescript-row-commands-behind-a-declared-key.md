Product: ExGrid
Kind: Added
ADR: 0050, 0240

In `@ex-grid/core`, `onDeclaredKey` may answer with a promise, and every input made in the grid after the key is held until it settles, as Blazor's awaited `OnDeclaredKey` holds them. A row Action or row Mark click held behind earlier input is noted with its row at the click and is refused as `rowLeftTheWindow` at its turn where that row left the Window while it waited, even if the row came back, where before it acted on whatever row stood at its point by then. It is published with 0.3.0.
