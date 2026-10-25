/**
 * ExSheet's formula engine in TypeScript (ADR-0370, section 2): a Sheet of Entries, Excel's formula syntax,
 * a declared set of functions that answer as Excel does or refuse, incremental recalculation, number formats
 * and the documents that carry a Sheet and a Workbook. It is held to the same cases as ExSheet.Engine.
 *
 * @packageDocumentation
 */
export { CellAddress, columnCount, rowCount } from './cell-address';
export { CellRange } from './cell-range';
export { Culture } from './culture';
export type { ErrorValue, ValueKind } from './value';
export { Value } from './value';
