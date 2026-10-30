Product: ExSheet
Kind: Added
ADR: 0330

`ExSheet.Components.ExSheet`, Blazor's ExSheet drawn by its TypeScript Script Core, now carries `OpenAsync` and `SaveAsync`. Each takes its place among the Sheet's inputs in the browser, as the Razor component's does, and calls back into .NET in its turn: the application's read runs in .NET, and an Escape that cancels the opening cancels its token; the write is handed .NET's own copy of the browser's Workbook, made from its document, each Linked Table's rows, its seed and count of changes, its Sheet Day and the moment `NOW` read, so that it computes the Values the browser shows. A read's or a write's failure is thrown as it was. `SheetWire.ReadRows`, and the Script Core's `writeLinkedRows`, carry a Linked Table's rows, a refusal included.
