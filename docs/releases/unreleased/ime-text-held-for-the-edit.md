Product: ExGrid
Kind: Fixed
ADR: 0080, 0010

When the Cell Editor an IME's text opened takes the keyboard, the text the grid was still holding for it goes in before anything else reaches it. A composition started in the editor straight away could come ahead of that text, and the cell then held `かなｄ感じです` where `かな感じです` was typed (seen on WebAssembly with the IME driven by a script).
