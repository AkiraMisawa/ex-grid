// A bound Board's links and outputs (ADR-0340, Q5 and Q6), as ExBoard.Tests' BoardLinkTests and BoardOutputTests
// hold the C# BoundBoard to them.
import { describe, expect, it } from 'vitest';
import { BoardRefusedError, createSnapshot, ticksFromText, type Snapshot } from '@ex-grid/data';
import { SnapshotPivotSource, textItemKey } from '@ex-grid/pivot-engine';
import {
    bindBoard, boardInput, LinkCondition, readBoard, type BoardReading, type BoundBoard, type BoundView, type SelectedRows,
} from '../src/index';

const inputs = `"inputs": {
  "trades": {"columns": [{"name": "desk", "type": "text"}, {"name": "month", "type": "text"},
                         {"name": "pnl", "type": "decimal"}, {"name": "day", "type": "date"}]},
  "limits": {"columns": [{"name": "desk", "type": "text"}, {"name": "limit", "type": "decimal"}]}}`;

const blotter = '{"id": "blotter", "grid": {"data": "trades", "columns": [{"column": "desk"}, {"column": "month"}, {"column": "pnl", "format": "#,##0.00"}]}}';
const other = '{"id": "other", "grid": {"data": "trades", "columns": [{"column": "desk"}, {"column": "month"}]}}';
const limits = '{"id": "limits", "grid": {"data": "limits", "columns": [{"column": "desk"}, {"column": "limit", "format": "#,##0"}]}}';
const pivot = '{"id": "byDesk", "pivot": {"data": "trades", "layout": {"version": 1, "rows": [{"field": "desk"}], "values": [{"field": "pnl", "aggregation": "sum"}]}}}';
const chart = '{"id": "curve", "chart": {"version": 1, "axis": {"values": "dates"}, "series": [{"id": "pnl", "name": "P&L", "kind": "level", "data": "trades", "x": "day", "y": "pnl"}]}}';

function board(links: string, outputs = ''): BoardReading {
    return readBoard(`{"version": 1, ${inputs}, "views": [${blotter}, ${other}, ${limits}, ${pivot}, ${chart}],
      "layout": [{"views": ["blotter", "other", "limits", "byDesk", "curve"]}], "links": [${links}]${outputs === '' ? '' : `, "outputs": {${outputs}}`}}`);
}

function data(reading: BoardReading): Record<string, Snapshot> {
    const declared = (name: string) => {
        const input = boardInput(reading, name);
        if (input === undefined) throw new Error(`No input '${name}'.`);
        return input;
    };
    return {
        trades: declared('trades').fromJson(`[{"desk": "Rates", "month": "Jan", "pnl": 100, "day": "2026-01-05"},
            {"desk": "Credit", "month": "Feb", "pnl": 200, "day": "2026-01-06"},
            {"desk": "Rates", "month": "Feb", "pnl": 300, "day": "2026-01-07"},
            {"desk": "Credit", "month": "Jan", "pnl": 400, "day": "2026-01-08"},
            {"desk": "FX", "month": "Jan", "pnl": 500, "day": "2026-01-09"}]`),
        limits: declared('limits').fromJson('[{"desk": "rates", "limit": 1000}, {"desk": "Credit", "limit": 2000}, {"desk": "FX", "limit": 3000}]'),
    };
}

/** Whole rows selected, in the order the grid shows them. */
function rows(...shown: number[]): SelectedRows {
    return { ranges: shown.map(topRow => ({ topRow, leftColumn: 0, rowCount: 1, columnCount: 1 })) };
}

const none: SelectedRows = { ranges: [] };

function grid(bound: BoundBoard, id: string) {
    const view: BoundView = bound.view(id);
    if (view.kind !== 'grid') throw new Error(`'${id}' is not a grid.`);
    return view;
}

function desks(bound: BoundBoard, id: string): (string | null)[] {
    return grid(bound, id).source.snapshot().window.map(row => {
        const desk = row.snapshot.column('desk');
        return desk.kind === 'text' ? desk.valueAt(row.at) : null;
    });
}

function link(reading: BoardReading, at = 0) {
    const found = reading.links[at];
    if (found === undefined) throw new Error(`No link at ${at}.`);
    return found;
}

describe('a link (ADR-0340 Q5)', () => {
    it('lets through only the tuples selected, never a cross product of lists', () => {
        const condition = new LinkCondition(['desk', 'month'], [['Rates', 'Jan'], ['Credit', 'Feb'], ['rates', 'jan']]);

        expect(condition.values).toHaveLength(2);
        expect(condition.matches(['Rates', 'Jan'])).toBe(true);
        expect(condition.matches(['CREDIT', 'feb'])).toBe(true);
        expect(condition.matches(['Rates', 'Feb'])).toBe(false);
        expect(condition.matches(['Credit', 'Jan'])).toBe(false);
        expect(condition.matches([null, 'Jan'])).toBe(false);
        expect(new LinkCondition(['desk'], [[null]]).matches([null])).toBe(true);
        expect(new LinkCondition(['pnl'], [[{ mantissa: 1n, scale: 0 }]]).matches([1])).toBe(false);
    });

    it('narrows a grid, a pivot and a chart by a grid\'s selected rows, and a drop restores them (ADR-0011)', () => {
        const reading = board('{"from": "blotter", "to": ["other", "byDesk", "curve", "limits"], "on": ["desk"]}');
        const bound = bindBoard(reading, data(reading));
        const narrowing = link(reading);
        let told = 0;
        bound.subscribe(() => { told++; });

        const condition = bound.gridCondition(narrowing, rows(0, 2));
        expect(condition?.values).toEqual([['Rates']]);
        bound.narrow(narrowing, condition);

        expect(bound.conditionOf(narrowing)).toBe(condition);
        expect(desks(bound, 'other')).toEqual(['Rates', 'Rates']);
        // Across inputs that share the column, and as the grid's value list compares text.
        expect(desks(bound, 'limits')).toEqual(['rates']);
        const byDesk = bound.view('byDesk');
        expect(byDesk.kind === 'pivot' && byDesk.source instanceof SnapshotPivotSource && byDesk.source.snapshot.rowCount).toBe(2);
        const curve = bound.view('curve');
        expect(curve.kind === 'chart' && curve.reading.definition.series[0]?.points.map(p => Number(p.y))).toEqual([100, 300]);
        expect(told).toBe(1);

        bound.narrow(narrowing, bound.gridCondition(narrowing, none));

        expect(bound.conditionOf(narrowing)).toBeUndefined();
        expect(desks(bound, 'other')).toHaveLength(5);
        expect(byDesk.kind === 'pivot' && byDesk.source instanceof SnapshotPivotSource && byDesk.source.snapshot).toBe(bound.data.get('trades'));
        expect(curve.kind === 'chart' && curve.reading.definition.series[0]?.points).toHaveLength(5);
        expect(told).toBe(2);
    });

    it('narrows on two columns to its own rows: (Rates, Jan) and (Credit, Feb) let through no (Rates, Feb)', () => {
        const reading = board('{"from": "blotter", "to": ["other"], "on": ["desk", "month"]}');
        const bound = bindBoard(reading, data(reading));

        bound.narrow(link(reading), bound.gridCondition(link(reading), rows(0, 1)));

        expect(grid(bound, 'other').source.snapshot().window.map(row => row.at)).toEqual([0, 1]);
    });

    it('reads a selection against the rows as the grid shows them, and refuses one past them', () => {
        const reading = board('{"from": "blotter", "to": ["other"], "on": ["desk"]}');
        const bound = bindBoard(reading, data(reading));
        grid(bound, 'blotter').source.setSorts([{ column: 'pnl', direction: 'descending' }]);

        expect(bound.gridCondition(link(reading), rows(0))?.values).toEqual([['FX']]);
        expect(bound.gridCondition(link(reading), rows(9))).toBeUndefined();
    });

    it('narrows a view two links reach to what both let through', () => {
        const reading = board('{"from": "blotter", "to": ["other"], "on": ["desk"]}, {"from": "limits", "to": ["other"], "on": ["desk"]}');
        const bound = bindBoard(reading, data(reading));

        bound.select('blotter', rows(0, 1));
        expect(desks(bound, 'other')).toEqual(['Rates', 'Credit', 'Rates', 'Credit']);
        bound.select('limits', rows(1));
        expect(desks(bound, 'other')).toEqual(['Credit', 'Credit']);
    });

    it('draws a chart narrowed to no rows with none, never the rows it drew before', () => {
        const reading = board('{"from": "limits", "to": ["curve"], "on": ["desk"]}');
        const bound = bindBoard(reading, data(reading));
        bound.narrow(link(reading), new LinkCondition(['desk'], [['Nobody']]));

        const curve = bound.view('curve');
        expect(curve.kind === 'chart' && curve.reading.definition.series[0]?.points).toHaveLength(0);
    });

    it('narrows nothing again for a selection that moves within the same values', () => {
        const reading = board('{"from": "blotter", "to": ["other"], "on": ["desk"]}');
        const bound = bindBoard(reading, data(reading));

        expect(bound.select('blotter', rows(0))).toBe(true);
        expect(bound.select('blotter', rows(2))).toBe(false);
        expect(bound.select('blotter', rows(1))).toBe(true);
    });

    it('narrows by the rows under a pivot\'s cells: a subtotal stands for every Item below it, a grand total for all', () => {
        const reading = board('{"from": "byDesk", "to": ["other"], "on": ["desk"]}');
        const bound = bindBoard(reading, data(reading));
        const narrowing = link(reading);
        const rates = { row: [{ field: 'desk', item: textItemKey('RATES') }], column: [] };

        expect(bound.cellCondition(narrowing, [rates])?.values).toEqual([['Rates']]);
        expect(bound.cellCondition(narrowing, [{ row: [], column: [] }])?.values).toEqual([['Rates'], ['Credit'], ['FX']]);
        expect(bound.cellCondition(narrowing, [])).toBeUndefined();

        expect(bound.selectCells('byDesk', [rates])).toBe(true);
        expect(desks(bound, 'other')).toEqual(['Rates', 'Rates']);
        expect(bound.selectCells('byDesk', undefined)).toBe(true);
        expect(desks(bound, 'other')).toHaveLength(5);
    });

    it('reads a selection by the kind of view it starts at', () => {
        const reading = board('{"from": "blotter", "to": ["other"], "on": ["desk"]}, {"from": "byDesk", "to": ["other"], "on": ["desk"]}');
        const bound = bindBoard(reading, data(reading));

        expect(() => bound.cellCondition(link(reading, 0), [])).toThrow(RangeError);
        expect(() => bound.gridCondition(link(reading, 1), none)).toThrow(RangeError);
    });

    it('lets go of what a pivot\'s cells narrowed when the pivot is narrowed again', () => {
        const reading = board('{"from": "blotter", "to": ["byDesk"], "on": ["desk"]}, {"from": "byDesk", "to": ["other"], "on": ["desk"]}');
        const bound = bindBoard(reading, data(reading));
        bound.selectCells('byDesk', [{ row: [{ field: 'desk', item: textItemKey('FX') }], column: [] }]);
        expect(desks(bound, 'other')).toEqual(['FX']);

        bound.select('blotter', rows(0));

        expect(bound.conditionOf(link(reading, 1))).toBeUndefined();
        expect(desks(bound, 'other')).toHaveLength(5);
    });
});

function withGrid(spec: string): BoardReading {
    return readBoard(`{"version": 1, ${inputs}, "views": [{"id": "blotter", "grid": ${spec}}], "layout": [{"views": ["blotter"]}]}`);
}

describe('a grid view (ADR-0342)', () => {
    it('opens under its spec\'s filter, a Blank among In\'s operands included', () => {
        const reading = withGrid('{"data": "trades", "columns": [{"column": "desk"}], "filter": {"columns": {"desk": {"combinator": "Or", '
            + '"clauses": [{"operator": "In", "values": [null, {"type": "String", "value": "rates"}]}]}}}}');
        const bound = bindBoard(reading, data(reading));

        expect(desks(bound, 'blotter')).toEqual(['Rates', 'Rates']);
    });

    it('refuses a filter on a column it does not show, and one comparing a decimal with text, naming the column\'s filter', () => {
        const hidden = withGrid('{"data": "trades", "columns": [{"column": "desk"}], "filter": {"columns": {"month": {"combinator": "And", '
            + '"clauses": [{"operator": "Equals", "value": {"type": "String", "value": "Oct"}}]}}}}');
        expect(() => bindBoard(hidden, data(hidden))).toThrow(expect.objectContaining({
            path: "views[0].grid.filter.columns['month']", message: "The filter is on column 'month', which the grid does not show.",
        }));
        const pnl = withGrid('{"data": "trades", "columns": [{"column": "pnl", "format": "#,##0"}], "filter": {"columns": {"pnl": {"combinator": "And", '
            + '"clauses": [{"operator": "Equals", "value": {"type": "String", "value": "1"}}]}}}}');
        expect(() => bindBoard(pnl, data(pnl))).toThrow("Column 'pnl' is number, and its filter compares it with a string.");
    });

    it('refuses a sort by a column it does not show, and a filter compared with another type, naming the path', () => {
        const sorted = withGrid('{"data": "trades", "columns": [{"column": "desk"}], "sorts": [{"column": "day", "direction": "Ascending"}]}');
        expect(() => bindBoard(sorted, data(sorted))).toThrow(expect.objectContaining({ path: 'views[0].grid.sorts[0]' }));
        const filtered = withGrid('{"data": "trades", "columns": [{"column": "desk"}], "filter": {"columns": {"desk": {"combinator": "And", '
            + '"clauses": [{"operator": "Equals", "value": {"type": "Int32", "value": "1"}}]}}}}');
        expect(() => bindBoard(filtered, data(filtered))).toThrow(expect.objectContaining({
            path: "views[0].grid.filter.columns['desk']", message: "Column 'desk' is text, and its filter compares it with a int32.",
        }));
    });
});

describe('an output (ADR-0340 Q6)', () => {
    const chosen = '"chosen": {"from": "blotter", "on": ["desk", "month"], "confirm": "Use these"}';

    it('is raised by no selection; a confirm hands on its values with the version', () => {
        const reading = board('', chosen);
        const bound = bindBoard(reading, data(reading));
        expect(bound.canConfirm('chosen')).toBe(false);
        expect(bound.confirm('chosen')).toBeUndefined();

        bound.select('blotter', rows(0, 4));
        const output = bound.confirm('chosen');

        expect(output?.condition.values).toEqual([['Rates', 'Jan'], ['FX', 'Jan']]);
        expect(output?.sourceVersion).toBe(0n);
        expect(output?.toJson()).toBe('{"output":"chosen","input":"trades","version":0,"on":["desk","month"],"values":[["Rates","Jan"],["FX","Jan"]]}');
        const trades = bound.data.get('trades');
        if (trades === undefined) throw new Error('No trades.');
        expect(output?.take(trades).rowCount).toBe(2);
        expect(() => bound.confirm('nothing')).toThrow(RangeError);
    });

    it('takes its rows only from the version it was taken against', () => {
        const reading = board('', chosen);
        const trades = createSnapshot({
            version: 3,
            columns: [
                { name: 'desk', kind: 'text', values: ['Rates', 'FX'] }, { name: 'month', kind: 'text', values: ['Jan', 'Jan'] },
                { name: 'pnl', kind: 'decimal', values: [null, null] }, { name: 'day', kind: 'date', values: [ticksFromText('2026-01-05'), ticksFromText('2026-01-06')] },
            ],
        });
        const bound = bindBoard(reading, { ...data(reading), trades });
        bound.select('blotter', rows(1));
        const output = bound.confirm('chosen');
        if (output === undefined) throw new Error('Nothing to confirm.');
        const later = createSnapshot({ version: 4, columns: trades.columns.map(column => ({ name: column.name, kind: 'text', values: [] })) });

        expect(() => output.take(later)).toThrow(BoardRefusedError);
        expect(() => output.take(later)).toThrow("Output 'chosen' was taken against version 3 of input 'trades', and the data is version 4; the values the user chose may no longer be these.");
        const again = createSnapshot({ version: 3, columns: [{ name: 'desk', kind: 'text', values: ['FX'] }, { name: 'month', kind: 'integer', values: [1] }] });
        expect(() => output.take(again)).toThrow("Output 'chosen', column 'month': chosen as a text, and the data holds a integer.");
    });

    it('is let go at once when a link narrows its grid', () => {
        const reading = board('{"from": "limits", "to": ["blotter"], "on": ["desk"]}', chosen);
        const bound = bindBoard(reading, data(reading));
        bound.select('blotter', rows(0));
        expect(bound.canConfirm('chosen')).toBe(true);

        bound.select('limits', rows(2));

        expect(bound.canConfirm('chosen')).toBe(false);
    });
});
