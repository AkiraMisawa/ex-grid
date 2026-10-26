/**
 * ExBoard in TypeScript (ADR-0340, ADR-0370): a Board, JSON an AI composes once and a workflow runs without AI, read
 * and checked against its inputs' declarations before anything is drawn, each refusal naming its path in the words
 * ExBoard's C# reader uses.
 *
 * @packageDocumentation
 */
export type {
    BoardCell, BoardChartView, BoardGridView, BoardLink, BoardOutputDeclaration, BoardPivotView, BoardReading, BoardRow, BoardView,
} from './board-spec';
export { boardDefaultRowHeight, boardInput, boardVersion, boardView, readBoard } from './board-spec';
export type { GridViewSpecReading, ShapedColumn } from './grid-view-spec';
export { readGridViewSpec } from './grid-view-spec';
export type { ShapedRow } from './shaped-grid';
export { dateFormat, dateTimeFormat, integerFormat, shapedColumns, shapedRows } from './shaped-grid';
export type { BoardValue } from './link-condition';
export { LinkCondition, narrowedPositions, narrowedSnapshot } from './link-condition';
export { BoardOutput } from './board-output';
export type { BindBoardOptions, BoundView, PivotCellItems, SelectedRows } from './bound-board';
export { BoundBoard, BoundChartView, BoundGridView, BoundPivotView, bindBoard } from './bound-board';
export type { BoardColumnKinds, BoardDiffParts, BoardDiffReading } from './board-diff';
export { BoardChartChange, BoardDiff, BoardGridChange, BoardPivotChange } from './board-diff';
export type { BoardHandle, BoardOptions } from './create-board';
export { boardGap, boardTitleHeight, createBoard } from './create-board';
