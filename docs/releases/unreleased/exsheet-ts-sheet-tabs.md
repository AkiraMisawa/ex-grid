Product: ExSheet
Kind: Added
ADR: 0331

ExSheet in TypeScript shows the Sheet Tabs at its foot, one per Sheet of the Workbook, as the Blazor Sheet does (`showSheetTabs: false` hides them). A press on a tab, or Ctrl+PageUp and Ctrl+PageDown, shows another Sheet; + adds one after the Sheet shown and Shift+F11 one before it, each one step to undo; on the tab list the arrows, Home and End select a tab, and Escape or Enter gives the keyboard back to the grid. Each Sheet keeps its Selection and scroll position, and one shown for the first time opens on A1. While a cell is edited no Sheet is switched or added, and the notice says why. Renaming, the tab's menu and dragging a tab follow.
