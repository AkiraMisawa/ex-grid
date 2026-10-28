Product: ExGrid
Kind: Added
ADR: 0410

`ExGrid.Script.ExGrid` takes `ShowFormulaBar`. `MudGridChrome` draws the Formula Bar's text field and its Name Box over the Script Core through the new `IGridChrome.FormulaBarText(GridFormulaBarTextView)` and `NameBox(GridNameBoxView)`, `ScriptSlots.FormulaBarText` and `ScriptSlots.NameBox`, marked with `ScriptSlotAttributes.FormulaBarField` and `NameBoxField`. The grid reads both fields in the browser, so text typed at once over Blazor Server is what the Enter commits. `MudSheetChrome` gives `ExSheet.Script.ExSheet` MudBlazor's Name Box, and the Sheet keeps its own Formula Bar text, which colours a formula's References.
