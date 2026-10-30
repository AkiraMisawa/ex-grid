// The public surface of @ex-grid/data-xlsx (ADR-0370, section 4): the cells of an .xlsx workbook read and written as
// a stream, as ExGrid.Data.Xlsx reads and writes them (ADR-0330), held to the same shared cases.

export { openXlsx, type XlsxReader, type XlsxWorksheet } from './reader';
export { batchSize, type XlsxSheetReader } from './sheet-reader';
export { defaultMaxCells, defaultMaxUncompressedBytes, type XlsxProgress, type XlsxReadOptions } from './pacer';
export { XlsxSharedStrings } from './shared-strings';
export { XlsxError, type XlsxPlace, type XlsxRefusal } from './xlsx-error';
export { createXlsxWriter, type XlsxDefinedNameInput, type XlsxSheetWriter, type XlsxWriteOptions, type XlsxWriter } from './writer';
export { columnName, formatCell, maxColumns, maxRows, parseCell, type XlsxPlaceOfCell } from './cell-reference';
export {
    elementMaxChildren, elementMaxDepth, elementMaxText, isValidTableName, referenceOf, tableReference,
    type XlsxCell, type XlsxCellInput, type XlsxCellType, type XlsxColumn, type XlsxColumnInput, type XlsxDefinedName, type XlsxElement,
    type XlsxFormula, type XlsxFormulaInput, type XlsxFormulaKind, type XlsxRow, type XlsxSheetInfo, type XlsxSheetKind,
    type XlsxSheetVisibility, type XlsxTable,
} from './model';
export {
    automaticColor, defaultAlignment, defaultStyles, firstCustomNumberFormat, gray125Fill, indexedColor, noBorder, noBorderEdge, noFill,
    rgbColor, solidFill, themeColor,
    type XlsxAlignment, type XlsxBorder, type XlsxBorderEdge, type XlsxBorderStyle, type XlsxCellFormat, type XlsxColor, type XlsxColorKind,
    type XlsxFill, type XlsxFont, type XlsxHorizontalAlignment, type XlsxStyles, type XlsxUnderline, type XlsxVerticalAlignment,
} from './styles';
export { applyTint, defaultIndexedColors, XlsxTheme } from './theme';
