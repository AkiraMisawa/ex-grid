// A grid built from a source's shape over a Snapshot (ADR-0342), as C#'s ShapedGrid.Of builds one.
import { describe, expect, it } from 'vitest';
import { BoardInput, readJsonText } from '@ex-grid/data';
import { readGridViewSpec, shapedGrid } from '../src/index';

const trades = BoardInput.read('trades', readJsonText(
    '{"columns": [{"name": "desk", "type": "text"}, {"name": "notional", "type": "decimal"}, {"name": "month", "type": "text"}]}'), 'inputs.trades');
const snapshot = trades.fromJson(`[{"desk": "Rates", "notional": 300, "month": "Jan"}, {"desk": "FX", "notional": 100, "month": "Feb"},
    {"desk": "Credit", "notional": 200, "month": "Jan"}]`);
const read = (spec: string) => readGridViewSpec(readJsonText(spec), name => name === trades.name ? trades.shape : undefined, '');

describe('shapedGrid', () => {
    it('ADR-0342: opens its source under the spec\'s sort and filter, its pinned columns counted', () => {
        const grid = shapedGrid(snapshot, read(`{"data": "trades", "columns": [{"column": "desk", "pinned": true},
            {"column": "notional", "format": "#,##0"}, {"column": "month"}], "sorts": [{"column": "notional", "direction": "Descending"}],
            "filter": {"columns": {"month": {"combinator": "And", "clauses": [{"operator": "Equals", "value": {"type": "String", "value": "Jan"}}]}}}}`));
        const desk = snapshot.column('desk');
        expect(grid.source.snapshot().window.map(row => desk.valueAt(row.at))).toEqual(['Rates', 'Credit']);
        expect(grid.pinnedColumnCount).toBe(1);
        expect(grid.columns.map(column => column.name)).toEqual(['desk', 'notional', 'month']);
    });

    it('ADR-0342: refuses a sort by a column it does not show, at the path it is given', () => {
        const spec = read('{"data": "trades", "columns": [{"column": "desk"}], "sorts": [{"column": "month", "direction": "Ascending"}]}');
        expect(() => shapedGrid(snapshot, spec)).toThrow(expect.objectContaining({ path: 'sorts[0]' }));
        expect(() => shapedGrid(snapshot, spec, 'invariant', 'views[0].grid')).toThrow(expect.objectContaining({ path: 'views[0].grid.sorts[0]' }));
    });
});
