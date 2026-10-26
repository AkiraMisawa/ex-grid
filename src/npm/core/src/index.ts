/**
 * ExGrid's Script Core: the grid's state and what each input means, in TypeScript and free of any UI
 * framework (ADR-0350). A Framework Adapter, `@ex-grid/react` or Blazor's ExGrid, mounts it.
 *
 * @packageDocumentation
 */
export type { CellPosition, GridDirection, GridExtent } from './selection/cell';
export type { SelectionRange } from './selection/range';
export type { GridSelection } from './selection/selection';
export { createGrid } from './dom/create-grid';
export type { GridHandle } from './dom/create-grid';
export type { GridHeaderGroup, GridOptions, ViewportSize } from './dom/grid-view';
export type { ActionRefusalReason, FindRefusalReason, GridRefusal } from './dom/grid-refusal';
export type { GridColumnWidthChange } from './dom/column-gestures';
export type { GridCommand, GridContextMenuContext } from './dom/grid-command';
export { gridCommandIds } from './chrome/command-ids';
export { markLabelIds } from './chrome/mark-words';
export type { CopyRefusalReason, PastePlan, PasteRefusalReason, PasteShape, SourceCell } from './clipboard/clipboard-rules';
export type { PasteFieldOrigin } from './clipboard/clipboard-parse';
export type { GridPasteIntent } from './clipboard/paste-intent';
export type { GridClearIntent } from './clipboard/clear-intent';
export type { CommitRefusalReason, EditDiscardReason, EditVerdict, GridEditIntent, GridOverwriteNotice } from './cells/edit-intent';
export { actionColumn, markColumn } from './dom/grid-column';
export type {
    AutoColumnWidth, FixedColumnWidth, GridAction, GridActionEvent, GridColumn, GridColumnWidth, GridTemplate, GridTemplateCell,
} from './dom/grid-column';
export type { FilterUi } from './dom/filter-panel';
export type { TemporalLike } from './chrome/operand-reading';
export { GridSource } from './source/grid-source';
export type { ColumnType, DateType, QueryColumn } from './query/columns';
export type { CellAlign } from './paint/cell-classes';
export type { GridDensity } from './grid-metrics';
export type { DistinctValues, GridSourceState } from './source/grid-source';
export type { GridFindRequest, GridFindResult } from './finding/find';
export type { GridSummaryRequest, GridSummaryResult } from './summary/grid-summary';
export type { MarkRefusalReason, RowMarkHeaderState, RowMarks } from './rows/row-marks';
export type { RowRange } from './viewport/row-range';
export type { RowKeyOf } from './source/row-key';
export type { GridFilter } from './query/filter';
export type { SortSpec } from './query/sort';
export type { InMemorySourceOptions } from './source/in-memory-source';
export { InMemoryGridSource } from './source/in-memory-source';
export type { FetchingSourceOptions, GridPage, GridQuery } from './source/fetching-source';
export { FetchingGridSource } from './source/fetching-source';
export type { AggregateResult, SummaryFigure } from './summary/summary-accumulator';
export type { CellChangedAt } from './source/cell-change-times';
export type { FetchFailedEvent, FetchingSourceState, QueryInForce } from './source/fetching-source';
export type { FilterSpec } from './query/filter';
export type { GridChangeBatch } from './source/change-batch';
export type { GridClock } from './source/grid-clock';
export type { GridFindPattern } from './finding/find';
export type { GridReplaceAnswer, GridReplaceRequest, GridReplaceWrite } from './finding/replace';
export { InMemoryRowMarks } from './source/in-memory-row-marks';
export type { RowKey } from './source/row-key';
export type { RowKind } from './paint/row-classes';
export { RowMarkCounts } from './rows/row-marks';
export type { RowMarkIntent } from './rows/row-marks';
export type { SortDirection } from './query/sort';
export type { AggregateErrorKind } from './summary/summary-accumulator';
export type { ExactDecimal } from './summary/exact-decimal';
export type { FilterClause, FilterCombinator, FilterOperator } from './query/filter';
export type { GridReplaceResult, GridReplacedRow } from './finding/replace';
export type { MarksHost } from './source/in-memory-row-marks';
export type { GridReplaceRefusedCell } from './finding/replace';
