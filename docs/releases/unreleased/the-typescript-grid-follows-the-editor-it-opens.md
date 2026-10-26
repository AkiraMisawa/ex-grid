Product: ExGrid
Kind: Fixed
ADR: 0204

On `@ex-grid/core`'s grid, an editor opened by a character, F2 or a double click on a cell scrolled out of the browser's window opened where nobody could see it. Opening the editor now brings the cell into the window at the nearest edge, as a move of the Focus does and as the Blazor grid does.
