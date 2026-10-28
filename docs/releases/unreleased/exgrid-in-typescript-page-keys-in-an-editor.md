Product: ExGrid
Kind: Fixed
ADR: 0010

In Caret, in the Cell Editor or the Formula Bar, `@ex-grid/core` now takes PageUp and PageDown and does nothing with them, and on macOS answers Home and End itself, as the C# grid does (ticket 32). Left to the browser, they could scroll the grid away from the open edit, as Edge did with PageUp and PageDown.
