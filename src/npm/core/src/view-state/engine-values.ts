// A View State's filter, whose operands carry their type (ADR-0207), made the filter a grid's engine executes: each
// operand handed over as the value of that type the engine compares, exactly or not at all (spine rule 1). A Date is
// a Temporal PlainDateTime, which the engine recognises by its tag, and a Decimal the number a grid holds it as.

import { ticksText, type SnapshotDecimal } from '@ex-grid/data';
import type { ColumnType } from '../query/columns';
import type { FilterClause, GridFilter } from '../query/filter';
import type { ViewStateOperand } from './view-state';
import { GridViewStateJsonError } from './view-state-json';

/** The most significant digits a Decimal is shown and compared at exactly in a grid, whose numbers are C#'s decimal
 * read from a double, fifteen digits at most (ADR-0023). */
export const exactDigits = 15;

/** A Decimal as the number a grid reads it as, which holds it exactly while it has no more than
 * {@link exactDigits} significant digits. */
export function gridNumberOf(value: SnapshotDecimal): number {
    return Number(`${value.mantissa}e-${value.scale}`);
}

/** How many significant digits a Decimal holds, trailing zeros after the point left out. */
export function significantDigits(value: SnapshotDecimal): number {
    let mantissa = value.mantissa < 0n ? -value.mantissa : value.mantissa;
    if (mantissa === 0n) return 0;
    while (mantissa % 10n === 0n) mantissa /= 10n;
    return mantissa.toString().length;
}

/** A Date's ticks as a Temporal `PlainDateTime` in ISO's calendar, read through its members as the engine reads
 * the browser's or a polyfill's; the global `Temporal` is not needed. */
export function plainDateTimeOf(ticks: bigint): object {
    const text = ticksText(ticks);
    const fraction = Number(ticks % 10_000_000n);
    return Object.freeze({
        [Symbol.toStringTag]: 'Temporal.PlainDateTime',
        calendarId: 'iso8601',
        year: Number(text.slice(0, 4)),
        month: Number(text.slice(5, 7)),
        day: Number(text.slice(8, 10)),
        hour: Number(text.slice(11, 13)),
        minute: Number(text.slice(14, 16)),
        second: Number(text.slice(17, 19)),
        millisecond: Math.floor(fraction / 10_000),
        microsecond: Math.floor(fraction / 10) % 1000,
        nanosecond: (fraction % 10) * 100,
        toString: () => text,
    });
}

/**
 * `filter`, as {@link readGridViewStateJson} reads it, its operands made the values the engine compares a column of
 * each type with: a string for text, a number or a `bigint` for a number, a `PlainDateTime` for a wall-clock date,
 * a boolean for a boolean; a Blank among In's operands stays `null`. `typeOf` answers each filtered column's type,
 * or undefined for a column the grid does not have. Refused, with a {@link GridViewStateJsonError} at the column's
 * filter (`$.filter.columns['pnl']`): a column the grid does not have, an operand of another type than its
 * column's, a Decimal of more significant digits than a grid compares exactly, and a DateOnly or a DateTimeOffset,
 * which a wall-clock date column does not hold.
 */
export function engineFilterOf(filter: GridFilter | undefined, typeOf: (column: string) => ColumnType | undefined): GridFilter | undefined {
    if (filter === undefined) return undefined;
    const specs = Object.entries(filter.columns).map(([name, spec]) => {
        const path = `$.filter.columns['${name}']`;
        const type = typeOf(name);
        if (type === undefined) throw new GridViewStateJsonError(`The filter is on column '${name}', which the grid does not show.`, path);
        const clauses = spec.clauses.map((clause): FilterClause => {
            const { value, values } = clause;
            return {
                ...clause,
                ...(value === undefined ? {} : { value: operand(value, type, name, path) }),
                // A Blank among In's operands, as the View State's reader keeps it.
                ...(values === undefined ? {} : { values: values.map(one => one === null ? null : operand(one, type, name, path)) }),
            };
        });
        return [name, { ...spec, clauses }] as const;
    });
    return { ...filter, columns: Object.fromEntries(specs) };
}

function operand(written: unknown, type: ColumnType, column: string, path: string): unknown {
    if (!isOperand(written)) throw new Error(`Internal: the filter on column '${column}' holds an operand without its type.`);
    const tagged = written;
    const refuse = (): never => {
        throw new GridViewStateJsonError(`Column '${column}' is ${type}, and its filter compares it with a ${tagged.type}.`, path);
    };
    switch (tagged.type) {
        case 'string': return type === 'text' ? tagged.value : refuse();
        case 'boolean': return type === 'boolean' ? tagged.value : refuse();
        case 'double':
        case 'single': return type === 'number' ? tagged.value : refuse();
        case 'decimal': {
            if (type !== 'number') return refuse();
            if (significantDigits(tagged.value) > exactDigits) {
                throw new GridViewStateJsonError(`Column '${column}': the filter's ${significantDigits(tagged.value)} significant digits are more than a grid `
                    + `compares a number at (${exactDigits}).`, path);
            }
            return gridNumberOf(tagged.value);
        }
        case 'int64':
        case 'int32':
        case 'int16':
        case 'sByte':
        case 'uInt64':
        case 'uInt32':
        case 'uInt16':
        case 'byte': return type === 'number' ? tagged.value : refuse();
        case 'dateTime': return type === 'date' ? plainDateTimeOf(tagged.ticks) : refuse();
        case 'dateOnly':
        case 'dateTimeOffset': return refuse();
        default: throw new Error(`Internal: ${String(tagged satisfies never)} is not an operand.`);
    }
}

// The View State's reader puts only tagged operands in a filter it reads.
function isOperand(value: unknown): value is ViewStateOperand {
    return typeof value === 'object' && value !== null && typeof Reflect.get(value, 'type') === 'string';
}
