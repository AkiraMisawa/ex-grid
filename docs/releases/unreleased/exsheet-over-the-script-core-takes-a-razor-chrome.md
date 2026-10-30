Product: ExSheet
Kind: Added
ADR: 0410

`ExSheet.Components.ExSheet` takes a `Chrome`, and the parts of the Sheet's grid it draws in Razor stand inside the Sheet the browser draws, as under `ExGrid.Components.ExGrid`: `MudSheetChrome` draws the Context Menu with MudBlazor's components. The find panel stays the Sheet's own, with Within, as it does under the Razor component; the Sheet's own slots of an `ISheetChrome` (Toolbar Items, Format Cells, the Sheet Tabs' faces, its find panel) and `ToolbarContent` are not carried yet. In TypeScript, `SheetHandle` gains `focus`, `inOrder` and `holdSettled`, the grid's own, and React's `ExSheetApi` carries `focus` and `inOrder`.
