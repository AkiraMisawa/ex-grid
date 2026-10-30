Product: ExSheet
Kind: Behaviour
ADR: 0370

`ExSheet.Components.ExSheet`'s commands that edit and format and its Linked Tables (`DoAsync`, `SetCellFormatAsync`, `SetNumberFormatAsync`, `SetAlignmentAsync`, `DeclareLinkedTableAsync`, `PushLinkedTableAsync`) cross to the Sheet in the browser, and what it refuses or throws is thrown as the C# engine throws it. A `SheetEdit` crosses as the new `SheetWire` writes it, the text both engines' shared cases send every edit through; `SheetWire` also reads a Cell Format back. On Blazor Server, a document, a save's Workbook or any other message to .NET larger than 16 KB crosses as a stream, in its turn; one larger than 256 MB is refused. A script that does not load, the circuit failing as it loads included, is refused as another version's is, so a command waiting on the Sheet is told so instead of waiting on.
