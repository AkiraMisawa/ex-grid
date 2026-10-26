// What the user changes in a bound Board, raised as one Board Diff and laid over the Board next time (ADR-0340, Q7), as
// ExBoard.Tests' BoardDiffTests hold the C# BoundBoard to it. What a Diff reads, writes and drops is the shared cases'.
import { describe, expect, it } from 'vitest';
import type { Snapshot } from '@ex-grid/data';
import { readPivotLayoutJson } from '@ex-grid/pivot-engine';
import { bindBoard, boardInput, BoardDiff, readBoard, type BoardReading, type BoundBoard, type BoundView } from '../src/index';

const grid = '{"id": "blotter", "grid": {"data": "trades", "columns": [{"column": "id", "pinned": true}, {"column": "desk"}, {"column": "pnl", "format": "#,##0", "width": 90}]}}';
const pivot = '{"id": "byDesk", "pivot": {"data": "trades", "layout": {"version": 1, "rows": [{"field": "desk"}], "values": [{"field": "pnl", "aggregation": "sum"}]}}}';
const chart = '{"id": "curve", "chart": {"version": 1, "axis": {"values": "dates"}, "series": [{"id": "pnl", "name": "P&L", "kind": "level", "data": "trades", "x": "day", "y": "pnl"}]}}';

const board: BoardReading = readBoard(`{"version": 1,
  "inputs": {"trades": {"columns": [{"name": "id", "type": "text"}, {"name": "desk", "type": "text"},
                                    {"name": "pnl", "type": "decimal"}, {"name": "day", "type": "date"}]}},
  "views": [${grid}, ${pivot}, ${chart}], "layout": [{"views": ["blotter", "byDesk"]}, {"views": ["curve"]}]}`);

function data(): Record<string, Snapshot> {
    const trades = boardInput(board, 'trades');
    if (trades === undefined) throw new Error('No input trades.');
    return {
        trades: trades.fromJson(`[{"id": "T-1", "desk": "Rates", "pnl": 100, "day": "2026-10-19"},
            {"id": "T-2", "desk": "FX", "pnl": -50, "day": "2026-10-20"}, {"id": "T-3", "desk": "Rates", "pnl": 75, "day": "2026-10-21"}]`),
    };
}

const byDay = readPivotLayoutJson('{"version": 1, "rows": [{"field": "day"}], "values": [{"field": "pnl", "aggregation": "sum"}]}');

function view<K extends BoundView['kind']>(bound: BoundBoard, id: string, kind: K): Extract<BoundView, { kind: K }> {
    const found = bound.view(id);
    if (found.kind !== kind) throw new Error(`'${id}' is not a ${kind}.`);
    return found as Extract<BoundView, { kind: K }>;
}

// Every kind of change a user makes, once: what ExBoard.Tests' Changed makes.
function changed(): BoundBoard {
    const bound = bindBoard(board, data());
    const blotter = view(bound, 'blotter', 'grid');
    blotter.resize('desk', 140);
    blotter.reorder(['id', 'pnl', 'desk']);
    expect(blotter.hide('desk')).toBe(true);
    blotter.pin(2);
    blotter.source.setSorts([{ column: 'pnl', direction: 'descending' }]);
    view(bound, 'byDesk', 'pivot').changeLayout(byDay);
    const curve = view(bound, 'curve', 'chart');
    curve.changeVisibility('pnl', true);
    curve.changeRange({ from: 739_908 });
    curve.changeScale('categories');
    return bound;
}

// What ExBoard writes for the same changes.
const changedJson = '{"version":1,"views":{"blotter":{"grid":{"viewState":{"version":1,"columns":[{"column":"id","width":"Auto","pinned":true},'
    + '{"column":"pnl","width":90,"pinned":true}],"sorts":[{"column":"pnl","direction":"Descending"}]},"hidden":["desk"],"kinds":{}}},'
    + '"byDesk":{"pivot":{"layout":{"version":1,"form":"compact","valuesAxis":"columns","subtotalsAtTop":true,"grandTotalRow":true,'
    + '"grandTotalColumn":true,"repeatItemLabels":false,"filters":[],"rows":[{"field":"day"}],"columns":[],"values":[{"field":"pnl","aggregation":"sum"}]},'
    + '"kinds":{"day":"date","pnl":"decimal"}}},"curve":{"chart":{"hidden":["pnl"],"range":{"from":739908},"scale":"categories"}}}}';

describe('a bound Board\'s Diff (ADR-0340 Q7)', () => {
    it('is empty for a Board the user has not changed', () => {
        expect(bindBoard(board, data()).diff.toJson()).toBe('{"version":1,"views":{}}');
    });

    it('raises what the user changes as one Diff, written as ExBoard writes it, and leaves the Board as it was', () => {
        expect(changed().diff.toJson()).toBe(changedJson);
        const blotter = board.views.find(one => one.id === 'blotter');
        expect(blotter?.kind === 'grid' ? blotter.grid.viewState.columns.map(c => c.column) : []).toEqual(['id', 'desk', 'pnl']);
        expect(bindBoard(board, data()).diff.isEmpty).toBe(true);
    });

    it('leaves out a change undone', () => {
        const bound = bindBoard(board, data());
        const byDesk = view(bound, 'byDesk', 'pivot');
        byDesk.changeLayout(byDay);
        byDesk.changeLayout(byDesk.view.layout);
        const curve = view(bound, 'curve', 'chart');
        curve.changeVisibility('pnl', true);
        curve.changeVisibility('pnl', false);

        expect(bound.diff.isEmpty).toBe(true);
    });

    it('lays over the same Board next time as it was left', () => {
        const read = BoardDiff.read(changed().diff.toJson());
        const again = bindBoard(board, data(), { diff: read });

        expect(again.dropped).toEqual([]);
        const blotter = view(again, 'blotter', 'grid');
        expect(blotter.columns.map(column => column.name)).toEqual(['id', 'pnl']);
        expect(blotter.pinnedColumnCount).toBe(2);
        expect(blotter.hidden).toEqual(['desk']);
        expect(blotter.source.snapshot().sorts).toEqual([{ column: 'pnl', direction: 'descending' }]);
        expect(view(again, 'byDesk', 'pivot').layout).toBe(read.pivots.get('byDesk')?.layout);
        expect(view(again, 'curve', 'chart').range).toEqual({ from: 739_908 });
        expect(again.diff.sameAs(read)).toBe(true);
    });

    it('writes a filter the user set with its operands typed as ExBoard types them, and its column\'s kind', () => {
        const bound = bindBoard(board, data());
        const blotter = view(bound, 'blotter', 'grid');
        blotter.source.setFilter({ columns: { desk: { clauses: [{ operator: 'in', values: [null, 'Rates'] }] }, pnl: { clauses: [{ operator: 'greaterThan', value: 12.5 }] } } });

        const json = bound.diff.toJson();
        expect(json).toContain('"desk":{"combinator":"And","clauses":[{"operator":"In","values":[null,{"type":"String","value":"Rates"}]}]}');
        expect(json).toContain('{"type":"Decimal","value":"12.5"}');
        expect(json).toContain('"kinds":{"desk":"text","pnl":"decimal"}');
        // Read back, it filters the rows as it did.
        const again = view(bindBoard(board, data(), { diff: BoardDiff.read(json) }), 'blotter', 'grid');
        expect(again.source.snapshot().window.map(row => row.at)).toEqual([0, 2]);
    });

    it('drops by name a filter whose operands the grid cannot compare, and applies the rest', () => {
        const json = '{"version":1,"views":{"blotter":{"grid":{"viewState":{"version":1,"columns":[{"column":"pnl","width":90,"pinned":false},'
            + '{"column":"id","width":"Auto","pinned":false},{"column":"desk","width":"Auto","pinned":false}],"sorts":[],'
            + '"filter":{"columns":{"desk":{"combinator":"And","clauses":[{"operator":"Equals","value":{"type":"Int32","value":"1"}}]}}}},"hidden":[]}}}}';
        const bound = bindBoard(board, data(), { diff: BoardDiff.read(json) });

        expect(bound.dropped).toEqual(["views.blotter.grid: Column 'desk' is text, and its filter compares it with a int32."]);
        expect(view(bound, 'blotter', 'grid').columns.map(column => column.name)).toEqual(['pnl', 'id', 'desk']);
        expect(view(bound, 'blotter', 'grid').source.snapshot().filter).toBeUndefined();
    });

    it('refuses a gesture naming a column no longer shown, rather than applying it to others (spine rule 6)', () => {
        const blotter = view(bindBoard(board, data()), 'blotter', 'grid');
        expect(blotter.hide('desk')).toBe(true);

        expect(blotter.resize('desk', 140)).toBe(false);
        expect(blotter.reorder(['desk', 'id', 'pnl'])).toBe(false);
        expect(blotter.hide('desk')).toBe(false);
        expect(blotter.pin(3)).toBe(false);
        expect(blotter.hide('id')).toBe(true);
        expect(blotter.hide('pnl')).toBe(false);
        expect(blotter.columns.map(column => column.name)).toEqual(['pnl']);
    });
});
