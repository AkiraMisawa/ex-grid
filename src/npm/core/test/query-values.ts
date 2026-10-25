// How the query and source cases write values and Filters (tests/cases/README.md), read as the Script Core
// takes them.
import { Temporal } from 'temporal-polyfill';
import type { GridFilter } from '../src/query/filter';

/** A value as a case writes it: JSON's own, `{"bigint": text}` a bigint, `{"double": text}` a number JSON
 * cannot hold, `{"dateTime": text}`, `{"dateOnly": text}` and `{"dateTimeOffset": text}` the Temporal value
 * of that date type, `{"jsDate": text}` a `Date`. Anything else is a defect in the case. */
export function value(written: unknown): unknown {
    if (written === null || typeof written !== 'object') return written;
    if (Array.isArray(written)) return written.map(value);
    const entries: [string, unknown][] = Object.entries(written);
    const [only] = entries;
    if (entries.length === 1 && only !== undefined) {
        const [kind, text] = only;
        if (kind === 'bigint' && typeof text === 'string') return BigInt(text);
        if (kind === 'double' && typeof text === 'string') return Number(text);
        if (kind === 'dateTime' && typeof text === 'string') return Temporal.PlainDateTime.from(text);
        if (kind === 'dateOnly' && typeof text === 'string') return Temporal.PlainDate.from(text);
        if (kind === 'dateTimeOffset' && typeof text === 'string') return zoned(text);
        if (kind === 'jsDate' && typeof text === 'string') return new Date(text);
    }
    throw new Error(`${JSON.stringify(written)} is not a value a query case writes.`);
}

/** A date and time with an offset, as a ZonedDateTime in the fixed zone of that offset. */
function zoned(text: string): Temporal.ZonedDateTime {
    const offset = /[+-]\d{2}:\d{2}$/u.exec(text);
    if (offset === null) throw new Error(`${text} is not a date and time with an offset.`);
    return Temporal.ZonedDateTime.from(`${text}[${offset[0]}]`);
}

/** Whether a list is there, where a case may write it as null as a Query that came through JSON does. */
function isWritten<T>(list: readonly T[] | null | undefined): list is readonly T[] {
    return Array.isArray(list);
}

/** The Filter as the Script Core takes it, its operands read as values. */
export function filterOf(filter: GridFilter): GridFilter {
    const columns = Object.fromEntries(Object.entries(filter.columns).map(([name, spec]) => [name, {
        ...spec,
        clauses: isWritten(spec.clauses) ? spec.clauses.map(clause => ({
            ...clause,
            ...('value' in clause ? { value: value(clause.value) } : {}),
            ...(isWritten(clause.values) ? { values: clause.values.map(value) } : {}),
        })) : spec.clauses,
    }]));
    return { ...filter, columns };
}
