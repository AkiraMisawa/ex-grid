Product: ExGrid
Kind: Fixed
ADR: 0230

A key typed straight after the Cell Editor or the Formula Bar changed mode now means what it means typed slowly: Home, End, ↓ or F2 typed as a completion list closes, as Point ends under typing, or as typing changes the mode, were judged in the mode before it, a round trip behind on Blazor Server and a task behind on WebAssembly. The edit's surfaces now say their mode in the render that shows it, and the key gate reads it there. On Blazor Server, a key that reached the grid before the input events of the typing it carries no longer has its answer undone by them: `=SUM(` and Home typed at once point at the row's first cell, and stay pointing (ticket 221).
