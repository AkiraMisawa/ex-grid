Product: ExGrid
Kind: Behaviour
Docs: exgrid/editing#editable-columns-and-the-edit-intent
ADR: 0010

Typing over a cell's value, Shift+Home and Shift with an arrow now commit the edit and extend the selection from that cell, as Excel's Enter mode does, where they selected the editor's text; a value the column's verdict rejects holds the edit, as for the arrows, and where the grid's `PointAt` says a Reference can go they point a range instead. In Caret — after F2 in a cell (or a double-click), and in the Formula Bar until F2 takes it to Enter mode — they still select the text, as Shift+Home does while pointing, and Shift+End is unchanged.
