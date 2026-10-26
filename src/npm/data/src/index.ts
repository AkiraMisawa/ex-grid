/**
 * ExGrid's Snapshot in TypeScript: the family's immutable data at one version, held in columns (ADR-0064),
 * read from and written to Apache Arrow's IPC format as ExGrid.Data.Arrow reads and writes it (ADR-0065,
 * ADR-0370 section 3), so a Snapshot travels between a .NET server and a browser exactly. It carries no
 * dependency: the Arrow it reads and writes is its own.
 *
 * @packageDocumentation
 */
export type {
    BooleanColumn, DateColumn, DecimalColumn, DoubleColumn, IntegerColumn, Snapshot, SnapshotColumn, SnapshotColumnBase,
    SnapshotColumnInput, SnapshotColumnInputBase, SnapshotInput, SnapshotKind, TemporalPlainDateLike, TextColumn,
} from './snapshot';
export { createSnapshot } from './snapshot';
export type { ChangeBatch, ChangeBatchInput, SnapshotChange } from './change-batch';
export { applyChangeBatch, createChangeBatch } from './change-batch';
export type { SnapshotDecimal } from './decimal';
export { decimalFromText, decimalText } from './decimal';
export { ticksFromText, ticksOf, ticksOfTemporal, ticksText } from './dates';
export type { SnapshotErrorPlace } from './snapshot-error';
export { SnapshotError } from './snapshot-error';
export type { SnapshotProgress, SnapshotReadOptions } from './pacer';
export type { DataShape, DataShapeColumn } from './arrow/reader';
export { arrowStreamMediaType, readArrowShape, readArrowSnapshot, snapshotArrowMetadata } from './arrow/reader';
export { writeArrowSnapshot } from './arrow/writer';
export type { CsvColumn, CsvEncoding, CsvSchema, CsvSeparator } from './csv/schema';
export { csvExcelFalse, csvExcelTrue, csvIsoDateFormats } from './csv/schema';
export { readCsvSnapshot } from './csv/csv-read';
export type { JsonObject, JsonValue } from './json';
export { JsonNumber, JsonTextError, isJsonArray, isJsonObject, jsonMember, jsonMemberNames, jsonRawText, readJsonText } from './json';
export { jsonDoubleText, jsonStringText, writeJsonText } from './json-write';
export type { ShapedSource } from './board-input';
export { BoardInput, BoardRefusedError, boardKindNames, shapeColumn } from './board-input';
