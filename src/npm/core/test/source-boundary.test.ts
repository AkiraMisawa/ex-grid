// Layer 1: what a Grid Source does beyond the shared cases (tests/cases/source): the cap of a value list,
// cancellation, and that what a Consumer hands it, or reads from it, cannot move it.
import { describe, expect, it } from 'vitest';
import type { QueryColumn } from '../src/query/columns';
import type { FilterSpec, GridFilter } from '../src/query/filter';
import type { SortSpec } from '../src/query/sort';
import { elementAt } from '../src/must';
import { GridSource } from '../src/source/grid-source';

interface Trade {
    readonly book: string;
    readonly amount: number;
}

const columns: readonly QueryColumn<Trade>[] = [
    { name: 'Book', type: 'text', value: row => row.book },
    { name: 'Amount', type: 'number', value: row => row.amount },
];
const trades: readonly Trade[] = [{ book: 'Rates', amount: 2 }, { book: 'Credit', amount: 3 }, { book: 'Rates', amount: 1 }];

function bound(rows: readonly Trade[] = trades) {
    const source = GridSource.from(rows);
    source.setColumns(columns);
    return source;
}

describe('GridSource.from', () => {
    it('ADR-0009: past a thousand distinct values the answer is too many, never a list cut short', async () => {
        const rows = Array.from({ length: 1001 }, (_, i) => ({ book: `B${i.toString()}`, amount: i }));
        await expect(bound(rows).distinctValues('Book', new AbortController().signal)).resolves.toEqual({ kind: 'tooMany' });
        await expect(bound(rows.slice(1)).distinctValues('Book', new AbortController().signal)).resolves.toMatchObject({ kind: 'values' });
    });

    it('ADR-0350: a value list the grid gave up on is not answered', async () => {
        const abandoned = new AbortController();
        abandoned.abort();
        await expect(bound().distinctValues('Book', abandoned.signal)).rejects.toThrow('aborted');
    });

    it('ADR-0023: the rows are copied, so a change to the Consumer\'s list does not reach the Window', () => {
        const rows = [...trades];
        const source = GridSource.from(rows);
        rows.push({ book: 'FX', amount: 9 });
        expect(source.snapshot().window).toHaveLength(3);
        expect(source.snapshot().totalCount).toBe(3);
    });

    it('ADR-0011: the Sort is copied, so changing the Consumer\'s list does not replay it', () => {
        const source = bound();
        const sorts: SortSpec[] = [{ column: 'Amount', direction: 'ascending' }];
        source.setSorts(sorts);
        sorts[0] = { column: 'Amount', direction: 'descending' };
        source.setFilter(undefined);
        expect(source.snapshot().window.map(row => row.amount)).toEqual([1, 2, 3]);
        expect(source.snapshot().sorts).toEqual([{ column: 'Amount', direction: 'ascending' }]);
    });

    it('ADR-0011: the Filter is copied, so changing the Consumer\'s object does not replay it', () => {
        const source = bound();
        const spec: { clauses: FilterSpec['clauses'] } = { clauses: [{ operator: 'equals', value: 'Rates' }] };
        const filter: GridFilter = { columns: { Book: spec } };
        source.setFilter(filter);
        spec.clauses = [{ operator: 'equals', value: 'Credit' }];
        source.setSorts([]);
        expect(source.snapshot().window.map(row => row.book)).toEqual(['Rates', 'Rates']);
    });

    it('ADR-0350: what a snapshot hands out is frozen, and the same object until something changes', () => {
        const source = bound();
        const before = source.snapshot();
        expect(Object.isFrozen(before)).toBe(true);
        expect(Object.isFrozen(before.window)).toBe(true);
        source.setSorts([]);
        expect(source.snapshot()).toBe(before);
        source.setSorts([{ column: 'Amount', direction: 'ascending' }]);
        expect(source.snapshot()).not.toBe(before);
    });

    it('ADR-0018: two sources over the same rows do not reach each other', () => {
        const first = bound();
        const second = bound();
        first.setSorts([{ column: 'Amount', direction: 'descending' }]);
        expect(second.snapshot().window).toEqual(trades);
        expect(second.snapshot().rowSequenceVersion).toBe(0);
    });

    it('ADR-0350: a listener that stops hearing is not heard from again', () => {
        const source = bound();
        let heard = 0;
        const stop = source.subscribe(() => { heard += 1; });
        source.setSorts([{ column: 'Amount', direction: 'ascending' }]);
        stop();
        source.setSorts([{ column: 'Amount', direction: 'descending' }]);
        expect(heard).toBe(1);
    });

    it('ADR-0350: new columns that change nothing keep the snapshot the same object, since nobody is told', () => {
        const source = bound();
        source.setSorts([{ column: 'Amount', direction: 'ascending' }]);
        const before = source.snapshot();
        source.setColumns(columns.map(column => ({ ...column, value: (row: Trade) => column.value(row) })));
        expect(source.snapshot()).toBe(before);
    });

    it('ADR-0023: an Opaque Filter built afresh with the same data is no change, and other data is one', () => {
        const source = bound();
        let heard = 0;
        source.subscribe(() => { heard += 1; });
        source.setFilter({ columns: {}, opaque: { search: 'abc', books: ['Rates'] } });
        source.setFilter({ columns: {}, opaque: { books: ['Rates'], search: 'abc' } });
        expect(heard).toBe(1);
        source.setFilter({ columns: {}, opaque: { search: 'abc', books: ['Rates', 'FX'] } });
        expect(heard).toBe(2);
    });

    it('ADR-0001: a range that names no rows is refused as the call is made, not in a promise nobody awaits', () => {
        const source = bound();
        expect(() => source.needRange({ start: -1, count: 5 })).toThrow(RangeError);
        expect(() => source.needRange({ start: 0, count: 0 })).toThrow(RangeError);
    });

    it('ADR-0007: an edit keeps the Filter and the Sort in force as the same objects, rather than copying them again', () => {
        const rows = [...trades];
        const source = bound(rows);
        source.setFilter({ columns: { Amount: { clauses: [{ operator: 'in', values: [1, 2, 3] }] } } });
        source.setSorts([{ column: 'Amount', direction: 'ascending' }]);
        const { filter, sorts } = source.snapshot();
        source.replaceRow(elementAt(rows, 0), { book: 'Rates', amount: 3 });
        expect(source.snapshot().filter).toBe(filter);
        expect(source.snapshot().sorts).toBe(sorts);
    });
});
