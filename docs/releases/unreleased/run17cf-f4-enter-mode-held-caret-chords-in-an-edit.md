Product: ExGrid
Kind: Fixed
Docs: exgrid/editing#editable-columns-and-the-edit-intent
ADR: 0010

A key typed straight after the one that opened or switched an edit, and replayed into the editor once it stands, is now applied as the field would apply it: Ctrl with ← or → moves the caret by a word (Option on a Mac, where ⌘ goes to the text's start or end), Shift with an arrow extends the selection, and Ctrl+Backspace and Ctrl+Delete delete a word. Before, a Ctrl+← typed with F2 and End was dropped and a Shift+← collapsed the selection, so where the caret landed depended on whether the keys arrived before the grid's answer (seen on Edge, WebAssembly).
