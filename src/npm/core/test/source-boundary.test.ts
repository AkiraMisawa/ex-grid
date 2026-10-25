// Layer 1: what a Grid Source does beyond the shared cases (tests/cases/source): the cap of a value list,
// cancellation, that what a Consumer hands it, or reads from it, cannot move it, and the Row Keys and the
// clock that JSON cannot write.
import { afterEach, describe, expect, it, vi } from 'vitest';
import type { QueryColumn } from '../src/query/columns';
import type { FilterSpec, GridFilter } from '../src/query/filter';
import type { SortSpec } from '../src/query/sort';
import { elementAt } from '../src/must';
import { applyQuery } from '../src/query/query-engine';
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

interface Keyed {
    readonly id: unknown;
    readonly amount: number;
}

const keyedColumns: readonly QueryColumn<Keyed>[] = [{ name: 'Amount', type: 'number', value: row => row.amount }];

describe('GridSource.from with a Row Key', () => {
    afterEach(() => {
        vi.useRealTimers();
    });

    it.each([
        ['NaN', Number.NaN, 'NaN'],
        ['an object', { id: 1 }, 'a object'],
        ['a boolean', true, 'a boolean'],
    ])('ADR-0140: a Row Key that is %s names no row, and is refused by name', (_, id, saying) => {
        expect(() => GridSource.from<Keyed>([{ id: 1, amount: 1 }, { id, amount: 2 }], { rowKey: row => row.id as never }))
            .toThrow(expect.objectContaining({ name: 'TypeError', message: expect.stringContaining(`a Row Key that is ${saying}`) as unknown }));
    });

    it('ADR-0140: 1 and 1n are two keys, as a Map holds them, and so are 1 and \'1\'', () => {
        const source = GridSource.from<Keyed>([{ id: 1, amount: 1 }, { id: 1n, amount: 2 }, { id: '1', amount: 3 }], { rowKey: row => row.id as never });
        source.setColumns(keyedColumns);
        source.gatherInterval = 0;
        source.apply({ removedKeys: [1n] });
        expect(source.snapshot().window.map(row => row.amount)).toEqual([1, 3]);
    });

    it('ADR-0141 / LV-6: without a clock given, changes are gathered on the system\'s clock, 250 ms by default', () => {
        vi.useFakeTimers();
        const source = GridSource.from<Keyed>([{ id: 'A', amount: 1 }], { rowKey: row => row.id as never });
        source.setColumns(keyedColumns);
        expect(source.gatherInterval).toBe(250);
        source.apply({ added: [{ id: 'B', amount: 2 }] });
        source.apply({ added: [{ id: 'C', amount: 3 }] });
        expect(source.snapshot().window.map(row => row.id)).toEqual(['A', 'B']);
        vi.advanceTimersByTime(249);
        expect(source.snapshot().window).toHaveLength(2);
        vi.advanceTimersByTime(1);
        expect(source.snapshot().window.map(row => row.id)).toEqual(['A', 'B', 'C']);
    });

    it('ADR-0141 / LV-6: a gathering interval that is not a finite number of milliseconds from 0 is refused', () => {
        const source = GridSource.from<Keyed>([], { rowKey: row => row.id as never });
        for (const interval of [-1, Number.NaN, Number.POSITIVE_INFINITY]) {
            expect(() => { source.gatherInterval = interval; }).toThrow(RangeError);
        }
        expect(source.gatherInterval).toBe(250);
    });

    it('ADR-0003 / LV-7: a row changed in place, then changed out of the result, moves the Row Sequence Version', () => {
        // Changed in place, which ADR-0003 forbids, so the search for its old version misses: the whole
        // requery drops it all the same, and the grid must hear that the sequence moved.
        const rows = [{ id: 'A', amount: 3 }, { id: 'B', amount: 5 }, { id: 'C', amount: 7 }];
        const source = GridSource.from<Keyed>(rows, { rowKey: row => row.id as never });
        source.setColumns(keyedColumns);
        source.setFilter({ columns: { Amount: { clauses: [{ operator: 'greaterThanOrEqual', value: 2 }] } } });
        source.setSorts([{ column: 'Amount', direction: 'ascending' }]);
        source.gatherInterval = 0;
        const before = source.snapshot().rowSequenceVersion;
        elementAt(rows, 0).amount = 6;
        source.apply({ changed: [{ id: 'A', amount: 0 }] });
        expect(source.snapshot().window.map(row => row.id)).toEqual(['B', 'C']);
        expect(source.snapshot().rowSequenceVersion).toBe(before + 1);
    });

    it('ADR-0141 / LV-6: a wait longer than a timer can hold is armed a day at a time, never run at once', () => {
        vi.useFakeTimers();
        const source = GridSource.from<Keyed>([{ id: 'A', amount: 1 }], { rowKey: row => row.id as never });
        source.setColumns(keyedColumns);
        source.gatherInterval = 30 * 86_400_000;
        source.apply({ added: [{ id: 'B', amount: 2 }] });
        source.apply({ added: [{ id: 'C', amount: 3 }] });
        vi.advanceTimersByTime(29 * 86_400_000);
        expect(source.snapshot().window).toHaveLength(2);
        vi.advanceTimersByTime(86_400_000);
        expect(source.snapshot().window).toHaveLength(3);
    });

    it('ADR-0141: a publication the query refuses leaves what was gathered waiting, to be published once it can be', () => {
        // A row changed in place to a value its column refuses: the gathered change cannot be published,
        // and is not let go either.
        const rows = [{ id: 'A', amount: 3 as unknown }, { id: 'B', amount: 5 }];
        const looseColumns: readonly QueryColumn<{ readonly id: string; readonly amount: unknown }>[] = [{ name: 'Amount', type: 'number', value: row => row.amount }];
        const source = GridSource.from(rows, { rowKey: row => row.id });
        source.setColumns(looseColumns);
        source.setSorts([{ column: 'Amount', direction: 'ascending' }]);
        source.gatherInterval = 0;
        elementAt(rows, 1).amount = 'five';
        expect(() => { source.apply({ added: [{ id: 'C', amount: 4 }] }); }).toThrow('contradicts the declared type');
        elementAt(rows, 1).amount = 5;
        source.publishGathered();
        expect(source.snapshot().window.map(row => row.id)).toEqual(['A', 'C', 'B']);
    });

    it('ADR-0141 / LV-9: one Change Highlight function for the source\'s life, so only a row with a new object asks again', () => {
        const source = GridSource.from<Keyed>([{ id: 'A', amount: 1 }], { rowKey: row => row.id as never });
        source.setColumns(keyedColumns);
        source.gatherInterval = 0;
        const first = source.cellChangedAt;
        source.apply({ changed: [{ id: 'A', amount: 2 }] });
        expect(source.cellChangedAt).toBe(first);
        expect(first === undefined ? undefined : Object.isFrozen(first)).toBe(true);
    });

    it('ADR-0141 / LV-9: a format that throws while a change is compared leaves the times as they were', () => {
        let throwing = false;
        const formatted: readonly QueryColumn<Keyed>[] = [{
            name: 'Amount', type: 'number', value: row => row.amount,
            format: value => { if (throwing) throw new Error('format'); return String(value); },
        }];
        const source = GridSource.from<Keyed>([{ id: 'A', amount: 1 }], { rowKey: row => row.id as never });
        source.setColumns(formatted);
        source.gatherInterval = 0;
        const changedAt = source.cellChangedAt;
        throwing = true;
        expect(() => { source.apply({ changed: [{ id: 'A', amount: 2 }] }); }).toThrow('format');
        throwing = false;
        source.publishGathered();
        const [row] = source.snapshot().window;
        expect(row === undefined || changedAt === undefined ? 'missing' : changedAt(row, 'Amount')).toBeTypeOf('number');
    });

    it('ADR-0141 / LV-5: most of the rows removed a few at a time, and the rest reordered, still match applyQuery', () => {
        const rows: Keyed[] = Array.from({ length: 300 }, (_, i) => ({ id: i, amount: (i * 7919) % 101 }));
        const source = GridSource.from(rows, { rowKey: row => row.id as never });
        source.setColumns(keyedColumns);
        source.setSorts([{ column: 'Amount', direction: 'descending' }]);
        source.gatherInterval = 0;
        let held = rows;
        for (let at = 0; at < 260; at += 4) {
            const removed = new Set([at, at + 1, at + 2]);
            source.apply({ removedKeys: [...removed] });
            held = held.filter(row => !removed.has(row.id as number));
            expect(source.snapshot().window).toEqual(applyQuery(held, keyedColumns, undefined, [{ column: 'Amount', direction: 'descending' }]));
        }
        held = held.toReversed();
        source.replaceAll(held);
        source.apply({ changed: [{ id: 299, amount: 50 }] });
        held = held.map(row => row.id === 299 ? { id: 299, amount: 50 } : row);
        const window = source.snapshot().window;
        expect(window.map(row => [row.id, row.amount])).toEqual(
            applyQuery(held, keyedColumns, undefined, [{ column: 'Amount', direction: 'descending' }]).map(row => [row.id, row.amount]));
    });
});
