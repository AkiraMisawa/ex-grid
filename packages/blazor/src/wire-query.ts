// A Sort and a Filter as they cross to .NET (ADR-0001, ADR-0002): written as the C# core's GridViewStateJson
// writes a Query, so .NET's source reads them with the reader it already has. The grid holds a filter's operands
// as the values it compares a column with; the View State's JSON carries each with its type (ADR-0207), so each is
// written as the type .NET reads that value as: text, a Boolean, a number as the decimal the C# core's filter panel
// writes, a whole number past a double as a long or a decimal, or a date by its ticks.

import { writeGridQueryJson, type FilterClause, type GridFilter, type RowRange, type SortSpec } from '@ex-grid/core';
import { ticksOfTemporal } from '@ex-grid/data';

/** The range every query the bridge writes carries, which .NET's source reads only for its Sort and Filter. */
const anyRange: RowRange = Object.freeze({ start: 0, count: 1 });

/** A Sort, as the C# core's query text. */
export function sortsText(sorts: readonly SortSpec[]): string {
    return writeGridQueryJson({ sorts, filter: undefined, range: anyRange });
}

/** A Filter, or none, as the C# core's query text. */
export function filterText(filter: GridFilter | undefined): string {
    return writeGridQueryJson({ sorts: [], filter: filter === undefined ? undefined : typedFilter(filter), range: anyRange });
}

function typedFilter(filter: GridFilter): GridFilter {
    const columns = Object.fromEntries(Object.entries(filter.columns).map(([name, spec]) => [name, {
        ...spec,
        clauses: spec.clauses.map((clause): FilterClause => ({
            ...clause,
            ...(clause.value === undefined ? {} : { value: operandOf(clause.value) }),
            ...(clause.values === undefined ? {} : { values: clause.values.map(value => (value === null ? null : operandOf(value))) }),
        })),
    }]));
    return filter.opaque === undefined ? { columns } : { columns, opaque: filter.opaque };
}

const ticksPerSecond = 10_000_000n;
// 1970-01-01T00:00:00 in .NET's ticks, from which an instant's nanoseconds count.
const unixEpochTicks = 621_355_968_000_000_000n;

/** A value the grid compares a column with, as the View State's typed operand. */
export function operandOf(value: unknown): unknown {
    switch (typeof value) {
        case 'string': return { type: 'string', value };
        case 'boolean': return { type: 'boolean', value };
        case 'number': return { type: 'decimal', value: decimalOf(value) };
        case 'bigint': return wholeOperand(value);
        case 'object': {
            if (value === null) break;
            const day = ticksOfTemporal(value);
            const tag = Object.prototype.toString.call(value);
            if (day !== undefined) {
                return tag === '[object Temporal.PlainDate]' ? { type: 'dateOnly', ticks: day } : { type: 'dateTime', ticks: day, kind: 'unspecified' };
            }
            if (tag === '[object Temporal.ZonedDateTime]') return zonedOperand(value);
            if (tag === '[object Temporal.Instant]') return { type: 'dateTimeOffset', ticks: ticksOfInstant(nanosecondsOf(value)), offsetMinutes: 0 };
            break;
        }
        case 'undefined':
        case 'symbol':
        case 'function': break;
        default: throw new Error(`Internal: a value of the type ${typeof value}, which JavaScript does not have.`);
    }
    throw new TypeError(`A filter's operand of the type ${Object.prototype.toString.call(value)} has no form .NET reads (ADR-0207).`);
}

const longRange = 1n << 63n;
const decimalRange = 1n << 96n;

/** A number as the decimal it is written as: the C# core reads every Number column's operand as a decimal, and a
 * double's shortest text is the decimal a value of fifteen digits or fewer crossed as (ADR-0350 section 16). One a
 * decimal cannot hold is refused, as the C# core refuses it, rather than written as the nearest (ADR-0023). */
function decimalOf(value: number): { readonly mantissa: bigint; readonly scale: number } {
    if (!Number.isFinite(value)) throw new TypeError(`A filter's operand ${value} is not a finite number, which a Number column compares with (ADR-0023).`);
    const match = /^(-?)(\d+)(?:\.(\d+))?(?:e([+-]\d+))?$/u.exec(String(value));
    if (match === null) throw new Error(`Internal: the number ${value} was written as ${String(value)}.`);
    const [, sign = '', whole = '', fraction = '', exponent = '0'] = match;
    let mantissa = BigInt(`${whole}${fraction}`);
    let scale = fraction.length - Number(exponent);
    if (scale < 0) {
        mantissa *= 10n ** BigInt(-scale);
        scale = 0;
    }
    if (mantissa !== 0n && scale > 28) {
        throw new RangeError(`A filter's operand ${value} is too small in magnitude for a Number to represent (ADR-0023).`);
    }
    if (mantissa >= decimalRange) throw new RangeError(`A filter's operand ${value} is outside the range a Number can represent (ADR-0023).`);
    return { mantissa: sign === '-' ? -mantissa : mantissa, scale: mantissa === 0n ? 0 : scale };
}

/** A whole number past a double: a long where one holds it, as a long column's value, and otherwise the whole
 * decimal it crossed from. */
function wholeOperand(value: bigint): unknown {
    if (value >= -longRange && value < longRange) return { type: 'int64', value };
    if (value <= -decimalRange || value >= decimalRange) throw new RangeError(`A filter's operand ${value} is outside the range a Number can represent (ADR-0023).`);
    return { type: 'decimal', value: { mantissa: value, scale: 0 } };
}

function zonedOperand(value: object): unknown {
    const offset: unknown = Reflect.get(value, 'offsetNanoseconds');
    if (typeof offset !== 'number' || !Number.isInteger(offset / 60_000_000_000)) {
        throw new TypeError('A ZonedDateTime operand\'s offset is not a whole number of minutes, which a DateTimeOffset holds (ADR-0023).');
    }
    const offsetMinutes = offset / 60_000_000_000;
    const ticks = ticksOfInstant(nanosecondsOf(value)) + BigInt(offsetMinutes) * 60n * ticksPerSecond;
    return { type: 'dateTimeOffset', ticks, offsetMinutes };
}

function nanosecondsOf(value: object): bigint {
    const nanoseconds: unknown = Reflect.get(value, 'epochNanoseconds');
    if (typeof nanoseconds !== 'bigint') throw new TypeError('An instant operand does not name its instant.');
    return nanoseconds;
}

function ticksOfInstant(epochNanoseconds: bigint): bigint {
    if (epochNanoseconds % 100n !== 0n) throw new TypeError('An instant operand is finer than .NET\'s tick of 100 ns, which a DateTimeOffset cannot hold.');
    return epochNanoseconds / 100n + unixEpochTicks;
}
