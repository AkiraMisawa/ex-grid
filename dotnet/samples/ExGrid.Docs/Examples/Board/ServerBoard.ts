import type { ChartData } from '@ex-grid/chart';
import { thinChartData } from '@ex-grid/chart';
import { GridSource } from '@ex-grid/core';
import {
    boardView, narrowedPositions, narrowedSnapshot, readBoard, shapedColumns, shapedRows, valuesUnder, type BoardReading, type BoardSource,
    type LinkCondition,
} from '@ex-grid/board';
import { createSnapshot, ticksOf, type DataShape, type Snapshot } from '@ex-grid/data';
import { SnapshotPivotSource } from '@ex-grid/pivot-engine';

/** A Board whose input is held on a server. */
export const serverBoard: BoardReading = readBoard(`{"version": 1, "title": "All trades",
 "inputs": {"trades": {"columns": [{"name": "id", "type": "text"}, {"name": "desk", "type": "text"},
                                   {"name": "day", "type": "date"}, {"name": "seq", "type": "integer"}, {"name": "pnl", "type": "decimal"}]}},
 "views": [
   {"id": "blotter", "title": "Trades", "grid": {"data": "trades", "columns": [{"column": "id", "header": "Trade"},
     {"column": "desk", "header": "Desk"}, {"column": "pnl", "header": "P&L", "format": "#,##0;(#,##0)"}]}},
   {"id": "byDesk", "title": "By desk", "pivot": {"data": "trades", "layout": {"version": 1,
     "rows": [{"field": "desk"}], "values": [{"field": "pnl", "aggregation": "sum", "numberFormat": "#,##0"}]}}},
   {"id": "curve", "title": "P&L by trade", "chart": {"version": 1, "axis": {"values": "numbers"},
     "series": [{"id": "pnl", "name": "P&L", "kind": "level", "data": "trades", "x": "seq", "y": "pnl", "fromZero": false}]}}],
 "layout": [{"views": ["blotter", "byDesk"], "widths": [2, 1], "height": 280}, {"views": ["curve"], "height": 240}],
 "links": [{"from": "blotter", "to": ["byDesk"], "on": ["desk"]}, {"from": "byDesk", "to": ["curve"], "on": ["desk"]}]}`);

// Stands in for the server: 20,000 trades it never sends whole.
const held: Snapshot = make(20_000);

/**
 * The input's source. Each member is the host's own: here a few lines over the rows in memory; in an application, the
 * query and each condition's values sent over its own protocol, and the page read back as Arrow (readArrowSnapshot).
 */
export function serverSources(): Readonly<Record<string, BoardSource>> {
    const shape: DataShape = { columns: held.columns.map(({ name, kind }) => ({ name, kind })) };
    return {
        trades: {
            shape: () => Promise.resolve(shape),
            fetch: async (query, signal) => {
                // The server executes the narrowing the Board hands it, then the grid's own filter and sort with the
                // grid's own semantics, which a server's are written to match (ADR-0001).
                const view = boardView(serverBoard, query.view);
                if (view?.kind !== 'grid') throw new Error(`View '${query.view}' is not a grid of this Board.`);
                const kept = through(query.narrowing);
                const rows = GridSource.from(shapedRows(kept));
                rows.setColumns(shapedColumns(kept, view.grid));
                rows.setFilter(query.grid.filter);
                rows.setSorts(query.grid.sorts);
                const page = await rows.rows(query.grid.range, signal);
                const { totalCount } = rows.snapshot();
                if (totalCount === undefined) throw new Error('Rows held in memory are counted.');
                return { rows: narrowedSnapshot(kept, page.map(row => row.at)), start: Math.min(query.grid.range.start, totalCount), totalCount };
            },
            pivot: narrowing => new SnapshotPivotSource(through(narrowing)),
            // A chart's points are thinned where the trades are (ADR-0344): each pixel's first, last, lowest and
            // highest row; only those cross to the chart.
            thin: query => Promise.resolve(thinChartData(tableOf(through(query.narrowing)), query.chart, held.version)),
            // The desks under a pivot's selected cells, a subtotal's and the grand total's included (ADR-0345).
            under: query => Promise.resolve(valuesUnder(held, query)),
        },
    };
}

// The rows every condition lets through: those whose values in its columns are a tuple it holds.
function through(narrowing: readonly LinkCondition[]): Snapshot {
    return narrowedSnapshot(held, narrowedPositions(held, narrowing));
}

// The rows as a chart reads them, column by column.
function tableOf(rows: Snapshot): ChartData {
    return {
        columns: rows.columns.map(column => column.name),
        rowCount: rows.rowCount,
        value: (name, row) => rows.column(name).valueAt(row),
    };
}

function make(count: number): Snapshot {
    const desks = ['Rates', 'FX', 'Credit', 'Equities'];
    const ticksPerDay = 864_000_000_000n;
    const firstDay = ticksOf(2026, 1, 1);
    const at = Array.from({ length: count }, (_, i) => i);
    return createSnapshot({
        version: 1,
        columns: [
            { name: 'id', kind: 'text', values: at.map(i => `T-${String(i + 1).padStart(5, '0')}`) },
            { name: 'desk', kind: 'text', values: at.map(i => deskOf(desks, i)) },
            { name: 'day', kind: 'date', values: at.map(i => firstDay + BigInt(Math.floor(i / 100)) * ticksPerDay) },
            { name: 'seq', kind: 'integer', values: at.map(i => i + 1) },
            { name: 'pnl', kind: 'decimal', values: at.map(i => ({ mantissa: BigInt(((i * 7919) % 20_001) - 10_000), scale: 0 })) },
        ],
    });
}

function deskOf(desks: readonly string[], i: number): string {
    const desk = desks[i % desks.length];
    if (desk === undefined) throw new Error('A remainder of the desks\' count is one of them.');
    return desk;
}
