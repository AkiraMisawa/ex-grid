Product: ExGrid
Kind: Fixed
ADR: 0080, 0010

Typing with an IME on a selected cell: the Cell Editor that a composition's text opened takes the keyboard once the IME's key has finished (its keyup), not a moment after the composition ends, so a key that ends one composition and starts the next always leaves the next in the grid's own field, however far apart the browser tells the two. A key whose keyup never reaches the page, as Alt+Tab mid-composition sends it to the other window, finishes as the keyboard leaves the field. And when the editor takes the keyboard, the text the grid was still holding for it goes in before anything else reaches it. Before, either could go the other way: the cell held `かなｄ感じです` where `かな感じです` was typed (seen on WebAssembly with the IME driven by a script).
