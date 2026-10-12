Product: ExGrid
Kind: Behaviour
Docs: exgrid/editing#editable-columns-and-the-edit-intent
ADR: 0010

Typing over a cell's value, Shift+Home and Shift with an arrow now commit the edit and extend the selection from that cell, as Excel's Enter mode does, where they selected the editor's text; a value the column's verdict rejects holds the edit, as for the arrows. Where the grid's `PointAt` says a Reference can go, they point a range instead. After F2 (or a double-click), and in the Formula Bar, they still select the text, and Shift+End is unchanged.
