Product: ExGrid
Kind: Behaviour
Docs: exgrid/editing#editable-columns-and-the-edit-intent
ADR: 0010

Typing over a cell's value, Shift+Home, Shift with an arrow, and the Primary Modifier (Ctrl, or ⌘ on a Mac) with an arrow, with Shift or without, now commit the edit and then extend the selection, or move or extend it to the edge, from that cell, where they selected the editor's text or moved its caret — as Excel's Enter mode was seen to do with Shift+Home, Shift+→, Shift+↓, Shift+←, Ctrl+→ and Ctrl+Shift+→, the other directions by generalisation; a value the column's verdict rejects holds the edit, as for the arrows. Where the grid's `PointAt` says a Reference can go, the Shift keys point a range instead and the Primary Modifier's arrows do nothing, and while pointing Shift+Home extends the pointed range to the row's first column, where it selected text. In Caret — after F2 in a cell (or a double-click), and in the Formula Bar until F2 takes it to Enter mode — they still move the caret or select the text, and Shift+End is unchanged.
