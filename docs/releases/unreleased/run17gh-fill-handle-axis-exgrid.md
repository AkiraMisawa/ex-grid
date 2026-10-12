Product: ExGrid
Kind: Behaviour
Docs: exgrid/editing#bulk-changes--delete-and-undo
ADR: 0050

With `ShowFillHandle`, a Selection of whole columns now shows the fill handle at its first row's top-right corner, beside the column header, and one of whole rows at its first column's bottom-left, beside the Row Headings, as Excel puts them; it stood at the range's bottom-right, a whole column's at its last row, out of reach. Dragging it raises `OnFill` as before, along one axis, and a Selection of every cell keeps the bottom-right.
