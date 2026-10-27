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
export type { GridPopoverContext } from './dom/consumer-popover';
export type { GridDeclaredKeyPress, GridHeaderGroup, GridOptions, GridViewRead, ViewportSize } from './dom/grid-view';
export { cellTextMeasure, type CellPaintOptions } from './dom/cell-paint';
export { formulaBarHeight, type FormulaBarOptions } from './dom/formula-bar';
export type { BorderStyle, CellAppearance, CellBorder, CellTextMeasure, EdgeBorderOf } from './cells/cell-appearance';
export type { ActionRefusalReason, FindRefusalReason, GridRefusal, PlacementRefusalReason } from './dom/grid-refusal';
export type { GridColumnWidthChange } from './dom/column-gestures';
export type { GridCommand, GridContextMenuContext } from './dom/grid-command';
export { builtInLabelIds } from './chrome/command-labels';
export type {
    GridChrome, GridChromeSlot, GridColumnMenuSlotContext, GridContextMenuSlotContext, GridFilterPanelSlotContext, GridMenuSlotContext,
} from './dom/grid-chrome';
export type {
    FilterPanelCondition, FilterPanelConditions, FilterPanelEntry, FilterPanelValueList, FilterPanelView,
} from './chrome/filter-panel-model';
export type { SelectAllState } from './chrome/filter-panel-choices';
export type { MenuKey, MenuKeyKind } from './chrome/menu-keys';
export { gridCommandIds, gridLabelIds } from './chrome/command-ids';
export { markLabelIds } from './chrome/mark-words';
export { selectionLabelIds } from './chrome/selection-words';
export type { CopyOrientation, CopyPlan, CopyRefusalReason, PastePlan, PasteRefusalReason, PasteShape, SourceCell } from './clipboard/clipboard-rules';
export { assembleCopy, invariantMarker, markAttribute, type CopyFlavours, type CopyValues, type GridCopyAnswer, type GridCopyRequest } from './clipboard/clipboard-data';
export { parseClipboard } from './clipboard/clipboard-parse';
export type { PasteFieldOrigin } from './clipboard/clipboard-parse';
export type { GridPasteIntent } from './clipboard/paste-intent';
export type { GridFillIntent } from './selection/fill-intent';
export type { GridClearIntent } from './clipboard/clear-intent';
export type { CommitRefusalReason, EditDiscardReason, EditVerdict, GridEditIntent, GridOverwriteNotice } from './cells/edit-intent';
export { GridRuleset, type Breach, type BreachCell, type GridErrorStepRequest, type RuleViolation } from './cells/grid-ruleset';
export { actionColumn, markColumn } from './dom/grid-column';
export type {
    AutoColumnWidth, FixedColumnWidth, GridAction, GridActionEvent, GridColumn, GridColumnWidth, GridTemplate, GridTemplateCell,
} from './dom/grid-column';
export type { FilterUi } from './dom/filter-panel';
export type { TemporalLike } from './chrome/operand-reading';
export { GridSource } from './source/grid-source';
export type { ColumnType, DateType, QueryColumn } from './query/columns';
export { foldCase } from './query/text-casing';
export type { CellAlign, CellState, CellTone } from './paint/cell-classes';
export type { GridDensity } from './grid-metrics';
export { densityText } from './density-text';
export type { DensityText } from './density-text';
export { gridHeaderRequiredPx, gridTextGeometry } from './text-geometry';
export type { GridTextGeometry } from './text-geometry';
export type { DistinctValues, GridSourceState } from './source/grid-source';
export { findMatches, type GridFindRequest, type GridFindResult } from './finding/find';
export { answeredSummary, declinedSummary, summaryOf, type GridSummaryRequest, type GridSummaryResult } from './summary/grid-summary';
export type { SelectionSummary, SelectionSummaryStatus } from './dom/selection-summary';
export { summaryLabelIds } from './chrome/summary-words';
export type { MarkRefusalReason, RowMarkHeaderState, RowMarks } from './rows/row-marks';
export type { RowRange } from './viewport/row-range';
export type { RowKeyOf } from './source/row-key';
export type { GridFilter } from './query/filter';
export type { SortSpec } from './query/sort';
export type { InMemorySourceOptions } from './source/in-memory-source';
export { InMemoryGridSource } from './source/in-memory-source';
export type { FetchingSourceOptions, GridPage, GridQuery } from './source/fetching-source';
export { FetchingGridSource } from './source/fetching-source';
export { defaultSummaryFigures, summaryFigureOrder, type AggregateResult, type SummaryFigure } from './summary/summary-accumulator';
export type { CellChangedAt } from './source/cell-change-times';
export type { FetchFailedEvent, FetchingSourceState, QueryInForce } from './source/fetching-source';
export type { FilterSpec } from './query/filter';
export type { GridChangeBatch } from './source/change-batch';
export { systemClock, type GridClock } from './source/grid-clock';
export { defaultChangeHighlightMs } from './cells/change-highlight';
export type { GridFindPattern } from './finding/find';
export { refusedCells, replacedCells, replacedEntry, type GridReplaceAnswer, type GridReplaceRequest, type GridReplaceWrite } from './finding/replace';
export { InMemoryRowMarks } from './source/in-memory-row-marks';
export { FetchingRowMarks } from './source/fetching-row-marks';
export type { FetchingMarksHost, RowMarkAdapter } from './source/fetching-row-marks';
export { RowMarkState } from './rows/row-mark-state';
export type { RowMarkSnapshot, RowMarkStep } from './rows/row-mark-state';
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
export { FormatCode } from './shaped/format-code';
export type { FormatCodeReading, FormatCodeValue } from './shaped/format-code';
export { GridViewState, sameGridFilter, sameGridQuery } from './view-state/view-state';
export { engineFilterOf, exactDecimalDigits, gridNumberOf, instantOf, plainDateOf, plainDateTimeOf, significantDigits } from './view-state/engine-values';
export type { FilteredColumn } from './view-state/engine-values';
export type {
    ColumnViewState, ColumnViewStateWidth, GridViewStateOptions, ViewStateColumnSource, ViewStateDateTimeKind, ViewStateIntegerType,
    ViewStateOperand,
} from './view-state/view-state';
export {
    GridViewStateJson, GridViewStateJsonError, readGridQueryJson, readGridViewStateJson, writeGridQueryJson, writeGridViewStateJson,
} from './view-state/view-state-json';
