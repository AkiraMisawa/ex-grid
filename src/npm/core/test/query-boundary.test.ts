// Layer 1: what the Query semantics say beyond the shared cases (tests/cases/query): the columns a Query is
// run over, what only Temporal has (calendars, Instant, a value whose tag lies), and the operator matrix's
// completeness.
// The full polyfill, for its calendars other than ISO's; the shared cases' runner loads the basic one, so
// one run holds two Temporal implementations, which the core must tell apart only by their values' tags.
import { Temporal } from 'temporal-polyfill/full';
import { describe, expect, it } from 'vitest';
import type { QueryColumn } from '../src/query/columns';
import { filterOperatorsFor } from '../src/query/filter';
import { applyQuery, filterPredicate } from '../src/query/query-engine';

type Row = Readonly<Record<string, unknown>>;
const book: QueryColumn<Row> = { name: 'Book', type: 'text', value: row => row['book'] };

describe('the columns a Query runs over', () => {
    it('ADR-0023: two columns of one name are refused', () => {
        expect(() => applyQuery([], [book, book], undefined, undefined)).toThrow("Two columns are named 'Book'");
    });

    it('ADR-0023: a date type on a column that holds no dates is refused, naming it', () => {
        expect(() => applyQuery([], [{ ...book, dateType: 'dateOnly' }], undefined, undefined)).toThrow(TypeError);
    });

    it('ADR-0020: a column with no value is neither sorted nor filtered', () => {
        const actions: QueryColumn<Row> = { name: 'Actions', type: 'text', value: () => undefined, queryable: false };
        expect(() => applyQuery([{}], [actions], undefined, [{ column: 'Actions', direction: 'ascending' }])).toThrow('Actions');
    });

    it('ADR-0023: rows are handed back themselves, in the order the Query keeps', () => {
        const rows: Row[] = [{ book: 'b' }, { book: 'a' }];
        const sorted = applyQuery(rows, [book], undefined, [{ column: 'Book', direction: 'ascending' }]);
        expect(sorted[0]).toBe(rows[1]);
        expect(sorted[1]).toBe(rows[0]);
    });
});

describe('a Date column', () => {
    const settles: QueryColumn<Row> = { name: 'Settles', type: 'date', dateType: 'dateOnly', value: row => row['settles'] };
    const stamped: QueryColumn<Row> = { name: 'Stamped', type: 'date', dateType: 'dateTimeOffset', value: row => row['stamped'] };

    it('ADR-0023: a day in another calendar is the same day, as Temporal compares it', () => {
        const day = Temporal.PlainDate.from('2026-08-30');
        const rows: Row[] = [{ settles: day.withCalendar('japanese') }, { settles: day.add({ days: 1 }).withCalendar('hebrew') }];
        const kept = applyQuery(rows, [settles], { columns: { Settles: { clauses: [{ operator: 'equals', value: day }] } } }, undefined);
        expect(kept).toEqual([rows[0]]);
    });

    it('ADR-0023: a day in another calendar sorts by its day, not by that calendar\'s year', () => {
        const rows: Row[] = [{ settles: Temporal.PlainDate.from('2026-08-30').withCalendar('japanese') }, { settles: Temporal.PlainDate.from('1988-01-01').withCalendar('japanese') }];
        expect(applyQuery(rows, [settles], undefined, [{ column: 'Settles', direction: 'ascending' }])).toEqual([rows[1], rows[0]]);
    });

    it('ADR-0023: a Date that holds no time is refused, never compared as NaN', () => {
        expect(() => applyQuery([{ stamped: new Date(Number.NaN) }], [stamped], undefined, [{ column: 'Stamped', direction: 'ascending' }])).toThrow('not a valid date');
    });

    it('ADR-0350: a Temporal.Instant is the instant it names, as a ZonedDateTime and a Date are', () => {
        const rows: Row[] = [{ stamped: Temporal.Instant.from('2026-08-30T00:00:00Z') }, { stamped: new Date('2026-08-29T23:00:00Z') }];
        const filter = { columns: { Stamped: { clauses: [{ operator: 'equals' as const, value: Temporal.ZonedDateTime.from('2026-08-30T09:00:00+09:00[Asia/Tokyo]') }] } } };
        expect(applyQuery(rows, [stamped], filter, undefined)).toEqual([rows[0]]);
    });

    it('ADR-0023: a value of another date type is refused naming its type, as C# names it', () => {
        expect(() => applyQuery([{ settles: Temporal.PlainDateTime.from('2026-08-30T00:00') }], [settles], undefined, [{ column: 'Settles', direction: 'ascending' }]))
            .toThrow("Column 'Settles' (date): the value accessor returned (Temporal.PlainDateTime) is not the column's declared dateOnly.");
    });

    it('ADR-0023: a value whose tag claims a date it does not hold is refused, naming the column', () => {
        const lying = { [Symbol.toStringTag]: 'Temporal.Instant' };
        expect(() => applyQuery([{ stamped: lying }, { stamped: lying }], [stamped], undefined, [{ column: 'Stamped', direction: 'ascending' }]))
            .toThrow("Column 'Stamped' (date): the value accessor returned (Temporal.Instant) does not hold the date its type names.");
    });

    it('ADR-0023: a Temporal value of a type no date type takes is refused', () => {
        expect(() => applyQuery([{ settles: Temporal.PlainYearMonth.from('2026-08') }], [settles], undefined, [{ column: 'Settles', direction: 'ascending' }])).toThrow('contradicts the declared type');
    });
});

describe('a Filter as a predicate', () => {
    it('ADR-0023: the Filter is checked when the predicate is made, before any row is tested', () => {
        expect(() => filterPredicate([book], { columns: { Book: { clauses: [] } } })).toThrow('Book');
    });

    it('ADR-0023: the predicate tests one row at a time as the whole Query would', () => {
        const passes = filterPredicate([book], { columns: { Book: { clauses: [{ operator: 'startsWith', value: 'RA' }] } } });
        expect([{ book: 'Rates' }, { book: 'FX' }, {}].map(passes)).toEqual([true, false, false]);
    });
});

describe('the operators a column offers', () => {
    it('ADR-0009: the list the engine checks against cannot be changed by a caller', () => {
        for (const type of ['text', 'number', 'date', 'boolean'] as const) expect(Object.isFrozen(filterOperatorsFor(type))).toBe(true);
    });
});
