// Layer 1: a View State's filter made the filter the engine executes (ADR-0207, ADR-0023), each typed operand
// handed over exactly or refused. The C# core has no counterpart: its operands are already the values its engine
// compares.
import { decimalFromText, ticksFromText } from '@ex-grid/data';
import { describe, expect, it } from 'vitest';
import type { GridFilter } from '../src/query/filter';
import { engineFilterOf, gridNumberOf, instantOf, plainDateOf, plainDateTimeOf, significantDigits, type FilteredColumn } from '../src/view-state/engine-values';
import { readGridViewStateJson } from '../src/view-state/view-state-json';

const types: Readonly<Record<string, FilteredColumn>> = {
    desk: { type: 'text' }, pnl: { type: 'number' }, day: { type: 'date' }, live: { type: 'boolean' },
    settled: { type: 'date', dateType: 'dateOnly' }, at: { type: 'date', dateType: 'dateTimeOffset' },
};

function filterOf(column: string, clauses: string): GridFilter | undefined {
    return readGridViewStateJson(`{"version": 1, "columns": [], "sorts": [], "filter": {"columns": {"${column}": {"combinator": "And", "clauses": ${clauses}}}}}`,
        Object.keys(types)).filter;
}

function engine(column: string, clauses: string): GridFilter | undefined {
    return engineFilterOf(filterOf(column, clauses), name => types[name]);
}

describe('a View State filter made the engine\'s (ADR-0207)', () => {
    it('hands each operand over as the value of its type, a Blank among In\'s kept', () => {
        const desk = engine('desk', '[{"operator": "In", "values": [null, {"type": "String", "value": "Rates"}]}]');
        expect(desk?.columns['desk']?.clauses[0]?.values).toEqual([null, 'Rates']);
        const pnl = engine('pnl', '[{"operator": "GreaterThan", "value": {"type": "Decimal", "value": "12.50"}}]');
        expect(pnl?.columns['pnl']?.clauses[0]?.value).toBe(12.5);
        const count = engine('pnl', '[{"operator": "Equals", "value": {"type": "Int64", "value": "3"}}]');
        expect(count?.columns['pnl']?.clauses[0]?.value).toBe(3n);
        const day = engine('day', '[{"operator": "Equals", "value": {"type": "DateTime", "value": "2026-10-19T00:00:00.0000000", "kind": "Unspecified"}}]');
        expect(String(day?.columns['day']?.clauses[0]?.value)).toBe('2026-10-19T00:00:00');
        const settled = engine('settled', '[{"operator": "Equals", "value": {"type": "DateOnly", "value": "2026-10-19"}}]');
        expect(String(settled?.columns['settled']?.clauses[0]?.value)).toBe('2026-10-19');
        const at = engine('at', '[{"operator": "Equals", "value": {"type": "DateTimeOffset", "value": "1970-01-01T09:00:00.0000000+09:00"}}]');
        expect(at?.columns['at']?.clauses[0]?.value).toMatchObject({ epochNanoseconds: 0n });
    });

    it('refuses an operand of another type or date type, a Decimal it would round, and an unknown column, at the column\'s filter', () => {
        expect(() => engine('desk', '[{"operator": "Equals", "value": {"type": "Int32", "value": "1"}}]')).toThrow(expect.objectContaining({
            path: "$.filter.columns['desk']", message: "Column 'desk' is text, and its filter compares it with a int32.",
        }));
        expect(() => engine('pnl', '[{"operator": "Equals", "value": {"type": "Decimal", "value": "1.0000000000000001"}}]')).toThrow(
            "Column 'pnl': the filter's 17 significant digits are more than a grid compares a number at (15).");
        expect(() => engine('day', '[{"operator": "Equals", "value": {"type": "DateOnly", "value": "2026-10-19"}}]')).toThrow(
            "Column 'day' is dateTime, and its filter compares it with a dateOnly.");
        expect(() => engine('settled', '[{"operator": "Equals", "value": {"type": "DateTime", "value": "2026-10-19T00:00:00.0000000", "kind": "Unspecified"}}]'))
            .toThrow("Column 'settled' is dateOnly, and its filter compares it with a dateTime.");
        expect(() => engineFilterOf(filterOf('live', '[{"operator": "Equals", "value": {"type": "String", "value": "yes"}}]'), () => undefined))
            .toThrow(expect.objectContaining({ path: "$.filter.columns['live']" }));
    });
});

describe('the values a grid reads (ADR-0023)', () => {
    it('counts significant digits without trailing zeros, and reads a Decimal as its number', () => {
        expect(significantDigits(decimalFromText('-120.500'))).toBe(4);
        expect(significantDigits(decimalFromText('0'))).toBe(0);
        expect(gridNumberOf(decimalFromText('-120.500'))).toBe(-120.5);
    });

    it('makes a PlainDate and an Instant from ticks', () => {
        expect(plainDateOf(ticksFromText('2026-10-19'))).toMatchObject({ year: 2026, month: 10, day: 19 });
        expect(Object.prototype.toString.call(plainDateOf(0n))).toBe('[object Temporal.PlainDate]');
        expect(instantOf(ticksFromText('1970-01-01T00:00:01'), 0)).toMatchObject({ epochNanoseconds: 1_000_000_000n });
    });

    it('makes a PlainDateTime from ticks, read through its members', () => {
        const value = plainDateTimeOf(ticksFromText('2026-10-19T13:45:30.1234567'));
        expect(Object.prototype.toString.call(value)).toBe('[object Temporal.PlainDateTime]');
        expect(value).toMatchObject({ year: 2026, month: 10, day: 19, hour: 13, minute: 45, second: 30, millisecond: 123, microsecond: 456, nanosecond: 700 });
    });
});
