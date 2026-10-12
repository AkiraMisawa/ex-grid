Product: ExGrid
Kind: Behaviour
Docs: exgrid/editing#bulk-changes--delete-and-undo
ADR: 0050

With `ShowFillHandle`, a Selection of one or several whole columns now shows the fill handle at the top-right of the last of them, beside the column header, and one of whole rows at the bottom-left of the last row, beside the Row Headings, as Excel puts them (one of each observed by the Windows run of 2026-10-11, several by the readings of 2026-10-12); it stood at the range's bottom-right, out of reach. With that first row or column scrolled away the handle stays at the edge of the part in view, as Excel's did for a column (the readings of 2026-10-12; for a row by generalisation), a Selection of every cell shows none, as Excel showed none in view, and dragging it raises `OnFill` as before, along one axis.
