// @ex-grid/sheet-xlsx: an ExSheet Workbook opened from and saved to .xlsx (ADR-0330, ADR-0370 section 5), as ExSheet.Xlsx
// opens and saves it, over @ex-grid/data-xlsx. Held by the shared cases under tests/cases/sheet-xlsx, which the .NET runner
// runs against ExSheet.Xlsx too.
export { openXlsxWorkbook } from './reader';
export { maxFormulaLength, maxLinkedTableRows, maxTextLength, saveXlsxWorkbook } from './writer';
export { XlsxOpenError, type XlsxOpenPlace, type XlsxOpenRefusal } from './open-error';
export { OpenReport, type NotCarried, type NotCarriedKind, type UnsupportedFunctionCell, type XlsxOpenResult } from './open-report';
export { XlsxSaveError, type XlsxSaveOptions, type XlsxSavePlace, type XlsxSaveProgress, type XlsxSaveRefusal } from './save-error';
