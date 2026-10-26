import { useState, type ReactElement } from 'react';
import { Temporal } from 'temporal-polyfill';
import { BoardDiff, boardInput, readBoard, type BoardReading } from '@ex-grid/board';
import { ExBoard } from '@ex-grid/board-react';
import type { Snapshot } from '@ex-grid/data';
import { DemoNav } from '../demo-nav';

// The Board of the Blazor hosts' /board, read by ExBoard's Script Core.
const reading: BoardReading = readBoard(`{"version": 1, "title": "Desk P&L",
 "inputs": {
   "trades": {"columns": [{"name": "desk", "type": "text"}, {"name": "trader", "type": "text"},
                          {"name": "day", "type": "date"}, {"name": "pnl", "type": "decimal"}]},
   "limits": {"columns": [{"name": "desk", "type": "text"}, {"name": "limit", "type": "decimal"}]}},
 "views": [
   {"id": "blotter", "title": "Trades", "grid": {"data": "trades",
     "columns": [{"column": "day", "header": "Day"}, {"column": "desk", "header": "Desk"},
                 {"column": "trader", "header": "Trader"}, {"column": "pnl", "header": "P&L", "format": "#,##0;(#,##0)"}]}},
   {"id": "byTrader", "title": "By trader", "pivot": {"data": "trades", "layout": {"version": 1,
     "rows": [{"field": "trader"}], "values": [{"field": "pnl", "aggregation": "sum", "numberFormat": "#,##0"}]}}},
   {"id": "limits", "title": "Limits", "grid": {"data": "limits",
     "columns": [{"column": "desk", "header": "Desk"}, {"column": "limit", "header": "Limit", "format": "#,##0"}]}},
   {"id": "daily", "title": "P&L by day", "chart": {"version": 1, "axis": {"values": "dates"},
     "series": [{"id": "pnl", "name": "P&L", "kind": "observation", "data": "trades", "x": "day", "y": "pnl"}]}}],
 "layout": [{"views": ["blotter", "byTrader", "limits"], "widths": [2, 1, 1], "height": 240}, {"views": ["daily"], "height": 260}],
 "links": [{"from": "blotter", "to": ["byTrader", "limits", "daily"], "on": ["desk"]},
           {"from": "byTrader", "to": ["daily"], "on": ["trader"]}],
 "outputs": {"review": {"from": "blotter", "on": ["day", "trader"], "confirm": "Send to review"}}}`);

function input(name: string) {
    const declared = boardInput(reading, name);
    if (declared === undefined) throw new Error(`The Board declares no input '${name}'.`);
    return declared;
}

function boardData(): Record<string, Snapshot> {
    return {
        trades: input('trades').fromJson(`[{"desk": "Rates", "trader": "Aoki", "day": "2026-10-14", "pnl": 120500},
            {"desk": "FX", "trader": "Brandt", "day": "2026-10-15", "pnl": -42000},
            {"desk": "Rates", "trader": "Chen", "day": "2026-10-16", "pnl": 88250},
            {"desk": "Credit", "trader": "Dubois", "day": "2026-10-17", "pnl": 15400},
            {"desk": "FX", "trader": "Aoki", "day": "2026-10-20", "pnl": 61000}]`),
        limits: input('limits').fromJson('[{"desk": "Rates", "limit": 500000}, {"desk": "FX", "limit": 250000}, {"desk": "Credit", "limit": 100000}]'),
    };
}

/** The page /board of the Blazor hosts, as ExBoard's Script Core draws it: the blotter's selected rows narrow the
 * pivot, the chart and the limits grid by desk, its confirm hands on the chosen rows, and what the user changes is
 * written as its Board Diff, which "Draw again" lays over the Board bound afresh (ADR-0340). */
export function Board(): ReactElement {
    const [data, setData] = useState(boardData);
    const [raised, setRaised] = useState<string | undefined>(undefined);
    const [diff, setDiff] = useState<string | undefined>(undefined);
    const [laid, setLaid] = useState<BoardDiff | undefined>(undefined);
    return (
        <>
            <DemoNav route="board" />
            <h1>A Board</h1>
            <ExBoard board={reading} data={data} width={1000} locale="en-US" temporal={Temporal}
                     onOutput={output => { setRaised(output.toJson()); }} onDiff={changed => { setDiff(changed.toJson()); }}
                     {...(laid === undefined ? {} : { diff: laid })} />
            <pre id="raised" role="log">{raised}</pre>
            <button type="button" id="again" onClick={() => {
                // As a host does next time: the Diff read back from its JSON, and the Board bound afresh.
                setLaid(diff === undefined ? undefined : BoardDiff.read(diff));
                setData(boardData());
            }}>Draw again with these changes</button>
            <pre id="diff" role="log">{diff}</pre>
        </>
    );
}
