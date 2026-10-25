/**
 * ExPivot's engine in TypeScript (ADR-0370, section 2): Excel's PivotTable over a Snapshot or records in
 * memory. A Pivot Source answers a question with the Leaf Aggregates; the engine builds from them the row
 * and column trees and every total, exactly where the numbers are exact (ADR-0060), and lays them out into
 * a Pivot Report in the Compact, Outline or Tabular form. It is the C# engine's port, held to it by shared
 * cases and a differential run.
 *
 * @packageDocumentation
 */
export type { PivotLabel } from './words/pivot-words';
export { japanesePivotWords, pivotWordIdList, pivotWordIds, unitedKingdomPivotWords } from './words/pivot-words';
export { pivotLocales } from './culture/culture-data';
export { PivotFormatError, PivotOrderKeyError, PivotVersionError } from './errors';
export type {
    PivotAggregation, PivotArea, PivotAxis, PivotColumnRole, PivotDatePart, PivotFieldType, PivotItemKind, PivotReportForm, PivotRowRole,
    PivotShowValuesAs, PivotSortDirection,
} from './layout/pivot-enums';
export { pivotAggregations, pivotItemKinds } from './layout/pivot-enums';
export type { PivotItemKey } from './items/item-key';
export {
    blankItemKey, booleanItemKey, dateItemKey, errorItemKey, itemKeysEqual, numberItemKey, pivotErrorText, pivotItemKey, textItemKey,
} from './items/item-key';
export type {
    PivotFieldPlacement, PivotFieldPlacementInput, PivotLayout, PivotLayoutInput, PivotSort, PivotValueField, PivotValueFieldInput,
} from './layout/pivot-layout';
export {
    createPivotFieldPlacement, createPivotLayout, createPivotValueField, emptyPivotLayout, isEmptyLayout, isItemCollapsed, pivotSort,
    placementOf, placementsIn, placesField, sortsEqual,
} from './layout/pivot-layout';
export { pivotLayoutJsonVersion, readPivotLayoutJson, writePivotLayoutJson } from './layout/layout-json';
export type { PivotEditResult, PivotEntry, PivotFieldInfo, PivotLayoutChoice, PivotRefusal } from './layout/layout-edits';
export {
    chooseLayout, defaultAggregation, isLayoutChosen, isValuesPseudoField, layoutChoiceChanges, moveEntry, pivotLayoutChoices, placeField,
    refusalWord, removeEntry, setCollapsed, setFieldCollapsed, setHiddenItems, setSort, setSubtotals, setValueField, tickField,
    untickField, valuesPseudoField,
} from './layout/layout-edits';
export type {
    PivotField, PivotFieldInput, PivotOrderKey, PivotOrderKeyValue, PivotRecordField, PivotRecordFieldInput,
} from './fields/pivot-field';
export { datePartField, pivotField, pivotRecordField } from './fields/pivot-field';
export type {
    PivotDetailsQuery, PivotFieldItem, PivotItemsQuery, PivotPart, PivotQuery, PivotQueryField, PivotQueryInput, PivotQueryValue,
} from './cube/query';
export { defaultMaxItems, defaultMaxLeaves, pivotDetailsQuery, pivotItemsQuery, pivotParts, pivotQuery, queryForLayout } from './cube/query';
export type { PivotNumber, PivotSourceRefusal, PivotSourceRefusalKind } from './cube/answer';
export { PivotAnswer, PivotAnswerAxis, PivotAnswerBuilder, PivotAnswerValues, PivotRefusals } from './cube/answer';
export { PivotCube } from './cube/cube';
export type { PivotDetailRecord, PivotDetailValue, PivotFetchers, PivotSource } from './source/pivot-source';
export {
    FetchingPivotSource, PivotDetailPage, PivotItemPage, PivotSourceFeatures, RecordPivotSource, SnapshotPivotSource,
} from './source/pivot-source';
export type { PivotOptions } from './report/labels';
export type { PivotValue } from './report/value';
export type { PivotHeaderSpan, PivotLabelColumn, PivotRowLabel, PivotToggle } from './report/report';
export { PivotReport, PivotReportColumn, PivotReportRow, PivotRowKey } from './report/report';
export type { PivotItemInfo } from './engine';
export {
    aggregatePivot, computePivot, itemsOfCube, itemsOfPage, pivotCube, pivotReport, recordsBehind, validateLayout,
} from './engine';
