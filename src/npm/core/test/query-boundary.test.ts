// Layer 1: what the Query semantics refuse beyond the shared cases (tests/cases/query): the columns a Query
// is run over, and the Date column the Script Core does not compare yet.
import { describe, expect, it } from 'vitest';
import type { QueryColumn } from '../src/query/columns';
import { filterOperatorsFor } from '../src/query/filter';
import { applyQuery } from '../src/query/query-engine';

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

    it('ADR-0023: a Date column is refused until the Script Core compares dates, never compared by a guess', () => {
        const traded: QueryColumn<Row> = { name: 'TradedOn', type: 'date', value: row => row['tradedOn'] };
        expect(() => applyQuery([{}], [traded], undefined, [{ column: 'TradedOn', direction: 'ascending' }])).toThrow('TradedOn');
    });

    it('ADR-0023: rows are handed back themselves, in the order the Query keeps', () => {
        const rows: Row[] = [{ book: 'b' }, { book: 'a' }];
        const sorted = applyQuery(rows, [book], undefined, [{ column: 'Book', direction: 'ascending' }]);
        expect(sorted[0]).toBe(rows[1]);
        expect(sorted[1]).toBe(rows[0]);
    });
});

describe('the operators a column offers', () => {
    it('ADR-0009: the list the engine checks against cannot be changed by a caller', () => {
        for (const type of ['text', 'number', 'date', 'boolean'] as const) expect(Object.isFrozen(filterOperatorsFor(type))).toBe(true);
    });
});
