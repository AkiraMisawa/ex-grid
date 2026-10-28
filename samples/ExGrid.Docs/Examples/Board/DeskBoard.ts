import { BoardDiff, boardInput, readBoard, type BoardReading } from '@ex-grid/board';
import { BoardRefusedError, type Snapshot } from '@ex-grid/data';

/** A Board an AI composed once. */
export const deskBoardJson = `{"version": 1, "title": "Desk P&L",
 "inputs": {"trades": {"columns": [
   {"name": "desk", "type": "text"}, {"name": "trader", "type": "text"},
   {"name": "day", "type": "date"}, {"name": "pnl", "type": "decimal"}]}},
 "views": [
   {"id": "blotter", "title": "Trades", "grid": {"data": "trades",
     "columns": [{"column": "day", "header": "Day"}, {"column": "desk", "header": "Desk"},
                 {"column": "trader", "header": "Trader"}, {"column": "pnl", "header": "P&L", "format": "#,##0;(#,##0)"}],
     "sorts": [{"column": "day", "direction": "Ascending"}]}},
   {"id": "byTrader", "title": "By trader", "pivot": {"data": "trades", "layout": {"version": 1,
     "rows": [{"field": "trader"}], "values": [{"field": "pnl", "aggregation": "sum", "numberFormat": "#,##0"}]}}},
   {"id": "daily", "title": "P&L by day", "chart": {"version": 1, "axis": {"values": "dates"},
     "series": [{"id": "pnl", "name": "P&L", "kind": "observation", "data": "trades", "x": "day", "y": "pnl"}]}}],
 "layout": [{"views": ["blotter", "byTrader"], "widths": [2, 1], "height": 260}, {"views": ["daily"], "height": 300}],
 "links": [{"from": "blotter", "to": ["byTrader", "daily"], "on": ["desk"]},
           {"from": "byTrader", "to": ["daily"], "on": ["trader"]}],
 "outputs": {"review": {"from": "blotter", "on": ["day", "trader"], "confirm": "Send to review"}}}`;

/** The JSON a tool returned for the Board's input this run. */
export const deskBoardData = `[{"desk": "Rates", "trader": "Aoki", "day": "2026-10-14", "pnl": 120500},
 {"desk": "FX", "trader": "Brandt", "day": "2026-10-15", "pnl": -42000},
 {"desk": "Rates", "trader": "Chen", "day": "2026-10-16", "pnl": 88250},
 {"desk": "Credit", "trader": "Dubois", "day": "2026-10-17", "pnl": 15400},
 {"desk": "FX", "trader": "Aoki", "day": "2026-10-20", "pnl": 61000}]`;

/** The Board read, and its one input made a Snapshot by its declaration; or the refusal. */
export type DeskBoardReading =
    | { readonly board: BoardReading; readonly data: Readonly<Record<string, Snapshot>>; readonly refusal: undefined }
    | { readonly board: undefined; readonly data: undefined; readonly refusal: string };

export function readDeskBoard(json: string): DeskBoardReading {
    try {
        const board = readBoard(json);
        const trades = boardInput(board, 'trades');
        if (trades === undefined) throw new BoardRefusedError('inputs', 'This page binds input \'trades\'.');
        return { board, data: { trades: trades.fromJson(deskBoardData) }, refusal: undefined };
    } catch (error) {
        if (!(error instanceof BoardRefusedError)) throw error;
        return { board: undefined, data: undefined, refusal: error.path === '' ? error.message : `${error.path}: ${error.message}` };
    }
}

/** The user's changes read back, as the host does next time. What no longer fits the Board is dropped as it binds,
 * and the Board says so above its views; a Diff this reader cannot read is refused whole. */
export function layOver(diff: string): { readonly diff: BoardDiff; readonly refusal: undefined } | { readonly diff: undefined; readonly refusal: string } {
    try {
        return { diff: BoardDiff.read(diff), refusal: undefined };
    } catch (error) {
        if (!(error instanceof BoardRefusedError)) throw error;
        return { diff: undefined, refusal: `The changes kept: ${error.path}: ${error.message}` };
    }
}
