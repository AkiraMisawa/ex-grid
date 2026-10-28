import { boardOfChart, type BoardReading } from '@ex-grid/board';
import { BoardInput, BoardRefusedError, type Snapshot } from '@ex-grid/data';
import { deskBoardData } from './DeskBoard';

/** A spec draw_chart took while an AI composed, over a table a capability returned as a Data Handle. */
export const chartSpec = `{"version": 1, "title": "P&L by day", "axis": {"values": "dates"},
 "series": [{"id": "pnl", "name": "P&L", "kind": "observation", "data": "h-7f3a", "x": "day", "y": "pnl"}]}`;

// The table behind the handle, as the host holds it this conversation.
const trades: Snapshot = new BoardInput('h-7f3a', [
    { name: 'desk', kind: 'text' }, { name: 'trader', kind: 'text' }, { name: 'day', kind: 'date' }, { name: 'pnl', kind: 'decimal' },
]).fromJson(deskBoardData);

/** The spec as a Board, each handle it names an input declared with its table's shape; or the refusal, in the spec's own path. */
export function readChartSpecBoard(spec: string):
    | { readonly board: BoardReading; readonly data: Readonly<Record<string, Snapshot>>; readonly refusal: undefined }
    | { readonly board: undefined; readonly data: undefined; readonly refusal: string } {
    try {
        const board = boardOfChart(spec, handle => handle === 'h-7f3a' ? trades : undefined);
        return { board, data: { 'h-7f3a': trades }, refusal: undefined };
    } catch (error) {
        if (!(error instanceof BoardRefusedError)) throw error;
        // A Chart Spec's refusal says its path in its sentence, as the tool answers the AI.
        return { board: undefined, data: undefined, refusal: error.message };
    }
}
