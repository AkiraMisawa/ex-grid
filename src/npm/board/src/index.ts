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
