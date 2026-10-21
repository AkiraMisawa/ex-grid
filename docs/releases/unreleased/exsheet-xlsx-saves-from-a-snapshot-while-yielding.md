Product: ExSheet
Kind: Added
ADR: 0330

`XlsxWorkbookWriter.WriteAsync` takes its snapshot of the Workbook before it first yields and then builds the file from that snapshot a slice at a time, as a read does: `XlsxSaveOptions` gains `Progress` (`XlsxSaveProgress`), `SliceBudget` and `Yield`, and the cancellation token stops the write between slices with nothing written to the stream. `XlsxSaveOptions.MaxCells` now also counts each row a row format writes, so a row format over every row, which writes 1,048,576 row elements, is bounded with the cells.
