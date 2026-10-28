/**
 * ExSheet's formula engine in TypeScript (ADR-0370, section 2): a Workbook of Sheets of Entries, Excel's formula
 * syntax, a declared set of functions that answer as Excel does or refuse, incremental recalculation, number
 * formats, Cell Formats, the edits a Sheet takes with their undo steps, and the documents that carry a Sheet and a
 * Workbook. It is held to the same cases as ExSheet.Engine.
 *
 * @packageDocumentation
 */
export { CellAddress, columnCount, rowCount } from './cell-address';
export {
    BorderLine, CellBorders, CellColour, CellFill, CellFont, CellFormat,
    type BorderLineStyle, type CellDisplay, type HorizontalAlignment, type RangeEdgeLines,
} from './cell-format';
export { BorderChange, CellFormatChange, type BorderChangeParts, type CellFormatChangeParts } from './cell-format-change';
export { CellRange } from './cell-range';
export { Culture } from './culture';
export { serialFromDate } from './date-serial';
export { DeclaredFunction } from './declared-function';
export { Entry } from './entry';
export {
    completeFormula, constantText, cycleReference, formulaHintAt, formulaPointAt, formulaReferences, LinkedTableColumn,
    lookupFinds, lookupText, referenceText, structuredReferenceText,
    type ArgumentHint, type CompletionCandidate, type CompletionKind, type FormulaCompletion, type FormulaReference,
    type PointSite, type ReferenceCycle,
} from './formula-entry';
export { FormatError } from './format-error';
export { FormulaSyntaxError } from './formula-syntax-error';
export type { SheetDay, SheetMoment } from './formulas/cell-reader';
export type { ArgumentValue } from './formulas/function-definition';
export { NumberFormat, numberFormatColourRgb, type NumberFormatColour, type NumberFormatReading } from './number-format';
export { LinkedTable, Sheet, SheetBlock, SheetCopy, SheetStep, type EntryOfCell, type FillDirection, type TypedCell } from './sheet';
export {
    RepeatedKeyError, SheetChange, SheetColumnWidth, SheetRefusal, SheetRefusedError,
    type SheetColumnWidthKind, type SheetRefusalReason,
} from './sheet-change';
export { SheetDocument, type SheetDocumentLayout } from './sheet-document';
export { SheetDocumentAxisFormat, SheetDocumentCell, SheetDocumentColumnWidth, SheetDocumentError, SheetDocumentTable } from './sheet-document-parts';
export { SheetEdit } from './sheet-edit';
export { readCellFormatChange, readLinkedRows, readSheetEdit, writeCellFormat, writeRepeatedKey } from './sheet-wire';
export { Value, type ErrorValue, type ValueKind } from './value';
export { Workbook, WorkbookChange, WorkbookStep } from './workbook';
export { WorkbookDocument } from './workbook-document';
