Product: ExSheet
Kind: Added
ADR: 0410

An `ISheetChrome` handed to `ExSheet.Components.ExSheet` can draw Format Cells: `SheetScriptSlots.FormatCells` asks for the new `FormatCells(SheetFormatCellsView)` overload, whose contents stand in the grid's popover frame and mark their controls with the new `SheetScriptSlotAttributes.Format…` attributes. The draft stays the Sheet's, in the browser: every choice is read there as the browser fires it, so OK pressed at once after a choice sets it. `MudSheetChrome` fills it with MudBlazor's type and buttons over native controls.
