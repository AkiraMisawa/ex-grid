Product: ExSheet
Kind: Added
ADR: 0330, 0370

`@ex-grid/sheet-xlsx` opens and saves an ExSheet Workbook as `.xlsx` in TypeScript as `ExSheet.Xlsx` does, over `@ex-grid/data-xlsx`: `openXlsxWorkbook(file, culture, options)` answers the Workbook Document and its Open Report, or an `XlsxOpenError` naming the Sheet, the cell and what it held, and `saveXlsxWorkbook(workbook, options)` answers the file as a `Blob`, from a snapshot taken at the call, or an `XlsxSaveError`. Both pace themselves in slices, report progress and are cancelled through an `AbortSignal`. They carry, refuse and list what `ExSheet.Xlsx` does, in the same words, because both run the cases under `tests/cases/sheet-xlsx/`, to which `ExSheet.Xlsx`'s own tests moved. It is published with 0.3.0; ExSheet's TypeScript screen does not open or save a file through it yet.
