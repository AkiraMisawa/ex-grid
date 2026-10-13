Product: ExGrid
Kind: Breaking
ADR: 0201

`ExGrid.Columns.HeaderReorder` is no longer public: it is where the grid lets a dragged header land among the header groups. Nothing replaces it; a Consumer hears a reorder through the grid's column-order notification as before.
