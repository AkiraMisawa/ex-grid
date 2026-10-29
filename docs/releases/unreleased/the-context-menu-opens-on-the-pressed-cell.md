Product: ExGrid
Kind: Fixed
ADR: 0036

A right-click on another cell while an edit is open opens the Context Menu on the cell that was pressed, even when committing the edit makes the page move before the browser's `contextmenu` event arrives. Before, the TypeScript core read the cell under the pointer at that event, so the menu could act on a cell above or below the one pressed.
