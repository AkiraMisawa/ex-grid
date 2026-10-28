Product: ExSheet
Kind: Added
ADR: 0370

`ExSheet.Script.ExSheet`, Blazor's ExSheet drawn by its TypeScript Script Core, now carries the commands that edit and format and the Linked Tables: `DoAsync` (on the active Sheet or a named one), `SetCellFormatAsync`, `SetNumberFormatAsync`, `SetAlignmentAsync`, `DeclareLinkedTableAsync` and `PushLinkedTableAsync`, and, awaited where the Razor component answers at once, `CellFormatAtAsync` and `LinkedTablesAsync`. What the Sheet in the browser refuses or throws is thrown as the C# engine throws it. A `SheetEdit` crosses as the new `SheetWire` writes it, the text both engines' shared cases now send every edit through; `SheetWire` also reads a Cell Format back. `SetCellFormatFromFocusAsync` is not carried yet. A script that does not load, the circuit failing as it loads included, is now refused as another version's is, so a command waiting on the Sheet is told so instead of waiting on.
