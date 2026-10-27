Product: ExGrid
Kind: Added
ADR: 0058

The TypeScript grid takes `pointedAt`, a `GridPointedAt` declaration (now exported by `@ex-grid/core`): while it says the grid is pointed at, a press on the rows or a column header is handed over as the cell, the column or a shape to refuse, and moves neither the keyboard nor the Selection; its dashes and outlined columns are painted, and it answers the steps a Pointing Scope asks for. `onPointArrowFromOutside` hears the arrows pressed over text written from outside.
