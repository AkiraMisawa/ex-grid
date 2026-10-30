Product: ExSheet
Kind: Added
ADR: 0370

Blazor's ExSheet hands .NET only what changed in a document it raises: the cells whose Entry or Cell Format changed since the one before, and the addresses of those emptied, where it handed the whole document after every edit; an edit of one cell in a Sheet of 20,000 Formulas went from about a second to 24 ms on WebAssembly. `DocumentChanged` and `WorkbookChanged` still hand the Consumer each document whole. `ExSheet.Engine` gains `DocumentChangesReader`, which reads such changes against the document it read before, for any adapter over the Script Core, and `WorkbookDocument.ToSheetDocument()`, a Workbook of one Sheet as that Sheet's Sheet Document; `@ex-grid/sheet-engine` gains `DocumentChanges`, which writes them.
