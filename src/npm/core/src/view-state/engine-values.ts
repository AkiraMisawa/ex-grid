// A View State's filter, whose operands carry their type (ADR-0207), made the filter a grid's engine executes: each
// operand handed over as the value of that type the engine compares, exactly or not at all (spine rule 1). A date is
// the Temporal value of its column's date type, which the engine recognises by its tag, and a Decimal the number a
// grid holds it as.

import { ticksText } from '@ex-grid/data';
import type { SnapshotDecimal } from '@ex-grid/data';
import type { QueryColumn } from '../query/columns';
import type { FilterClause, GridFilter } from '../query/filter';
import type { ViewStateOperand } from './view-state';
import { GridViewStateJsonError } from './view-state-json';

/** The most significant digits a Decimal is shown and compared at exactly in a grid, whose numbers are C#'s decimal
 * read from a double, fifteen digits at most (ADR-0023). */
export const exactDecimalDigits = 15;

/** A Decimal as the number a grid reads it as, which holds it exactly while it has no more than
 * {@link exactDecimalDigits} significant digits. */
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

const ticksPerMinute = 600_000_000n;
// 1970-01-01T00:00:00 in .NET's ticks, from which an Instant counts.
const unixEpochTicks = 621_355_968_000_000_000n;

/** A day's ticks, its midnight, as a Temporal `PlainDate` in ISO's calendar, read through its members as the
 * engine reads the browser's or a polyfill's. */
export function plainDateOf(ticks: bigint): object {
    const text = ticksText(ticks).slice(0, 10);
    return Object.freeze({
        [Symbol.toStringTag]: 'Temporal.PlainDate',
        calendarId: 'iso8601',
        year: Number(text.slice(0, 4)),
        month: Number(text.slice(5, 7)),
        day: Number(text.slice(8, 10)),
        toString: () => text,
    });
}

/** A clock's ticks at an offset of `offsetMinutes` from UTC as the Temporal `Instant` they name, read through its
 * `epochNanoseconds` as the engine reads one; C#'s DateTimeOffset compares by that instant too. */
export function instantOf(ticks: bigint, offsetMinutes: number): object {
    const epochNanoseconds = (ticks - BigInt(offsetMinutes) * ticksPerMinute - unixEpochTicks) * 100n;
    return Object.freeze({ [Symbol.toStringTag]: 'Temporal.Instant', epochNanoseconds });
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

/** What {@link engineFilterOf} reads of a filtered column: its declared type and, for a Date column, its date type. */
export type FilteredColumn = Pick<QueryColumn<never>, 'type' | 'dateType'>;

/**
 * `filter`, as {@link readGridViewStateJson} reads it, its operands made the values the engine compares a column of
 * each type with: a string for text, a number or a `bigint` for a number, a boolean for a boolean, and for a date the
 * Temporal value of the column's date type — a DateTime a `PlainDateTime`, a DateOnly a `PlainDate`, a
 * DateTimeOffset the `Instant` it names. A Blank among In's operands stays `null`. `columnOf` answers each filtered
 * column (a {@link GridColumn} will do), or undefined for a column the grid does not have. Refused, with a
 * {@link GridViewStateJsonError} at the column's filter (`$.filter.columns['pnl']`): a column the grid does not
 * have, an operand of another type than its column's or, on a Date column, of another date type, and a Decimal of
 * more significant digits than a grid compares exactly.
 */
export function engineFilterOf(filter: GridFilter | undefined, columnOf: (column: string) => FilteredColumn | undefined): GridFilter | undefined {
    if (filter === undefined) return undefined;
    const specs = Object.entries(filter.columns).map(([name, spec]) => {
        const path = `$.filter.columns['${name}']`;
        const column = columnOf(name);
        if (column === undefined) throw new GridViewStateJsonError(`The filter is on column '${name}', which the grid does not show.`, path);
        const clauses = spec.clauses.map((clause): FilterClause => {
            const { value, values } = clause;
            return {
                ...clause,
                ...(value === undefined ? {} : { value: operand(value, column, name, path) }),
                // A Blank among In's operands, as the View State's reader keeps it.
                ...(values === undefined ? {} : { values: values.map(one => one === null ? null : operand(one, column, name, path)) }),
            };
        });
        return [name, { ...spec, clauses }] as const;
    });
    return { ...filter, columns: Object.fromEntries(specs) };
}

function operand(written: unknown, filtered: FilteredColumn, column: string, path: string): unknown {
    if (!isOperand(written)) throw new Error(`Internal: the filter on column '${column}' holds an operand without its type.`);
    const tagged = written;
    const { type } = filtered;
    const dateType = type === 'date' ? filtered.dateType ?? 'dateTime' : undefined; // oxlint-disable-line ex-grid/no-stand-in-default -- a Date column's dates are dateTime unless declared (QueryColumn.dateType)
    const refuse = (): never => {
        throw new GridViewStateJsonError(`Column '${column}' is ${dateType ?? type}, and its filter compares it with a ${tagged.type}.`, path);
    };
    switch (tagged.type) {
        case 'string': return type === 'text' ? tagged.value : refuse();
        case 'boolean': return type === 'boolean' ? tagged.value : refuse();
        case 'double':
        case 'single': return type === 'number' ? tagged.value : refuse();
        case 'decimal': {
            if (type !== 'number') return refuse();
            const digits = significantDigits(tagged.value);
            if (digits > exactDecimalDigits) {
                throw new GridViewStateJsonError(`Column '${column}': the filter's ${digits} significant digits are more than a grid `
                    + `compares a number at (${exactDecimalDigits}).`, path);
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
        case 'dateTime': return dateType === 'dateTime' ? plainDateTimeOf(tagged.ticks) : refuse();
        case 'dateOnly': return dateType === 'dateOnly' ? plainDateOf(tagged.ticks) : refuse();
        case 'dateTimeOffset': return dateType === 'dateTimeOffset' ? instantOf(tagged.ticks, tagged.offsetMinutes) : refuse();
        default: throw new Error(`Internal: ${String(tagged satisfies never)} is not an operand.`);
    }
}

// The View State's reader puts only tagged operands in a filter it reads.
function isOperand(value: unknown): value is ViewStateOperand {
    return typeof value === 'object' && value !== null && typeof Reflect.get(value, 'type') === 'string';
}
