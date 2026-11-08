Product: ExGrid
Kind: Fixed
Docs: exgrid/selection#driving-the-grid-from-a-script
ADR: 0240

On Blazor, a key typed straight after Delete, Ctrl+Z, Ctrl+Y, a row Action or a Name Box entry waits until .NET has
carried it out and its rows have reached the page. It was taken at once, against the rows from before: an F2 typed
after Ctrl+Z opened the editor on the value the undo was taking back, and its commit wrote that value again.
