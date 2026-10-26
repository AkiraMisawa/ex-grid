Product: ExSheet
Kind: Added
ADR: 0049

ExSheet in TypeScript takes a Linked Table from the Consumer, as the Blazor Sheet does: `declareLinkedTable(name, columns, key)` on the Sheet's handle declares it, and `pushLinkedTable(name, rows)` hands it a snapshot of rows. Until the first push its readers show `#GETTING_DATA`. A push repaints only the rows that read the table, raises no document and is no step to undo. A key repeated in a push throws `RepeatedKeyError` and leaves the readers waiting again. Under React the same two calls are on the `ExSheetApi` a `ref` on `<ExSheet>` receives, beside `workbook`, `undo` and `redo`.
