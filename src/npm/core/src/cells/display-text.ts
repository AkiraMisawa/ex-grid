// The text a value cell displays (ADR-0006, note of 2026-10-01; ADR-0350 section 16): empty for a Blank,
// the column's `format` where it has one, and otherwise the value's own text, which for a date is one ISO
// form by its type, so the same value reads the same for every reader. The one place the rule is written:
// the cell paints by it, and a source compares by it, so a change the format hides is not marked (ADR-0067)
// and a Find matches what the user reads (ADR-0055).
//
// A value's own text is C#'s where the two can agree: a boolean reads `True` or `False`, as C#'s does, so
// a column reads alike on either core. A number is JavaScript's own shortest text for it, which holds no
// trailing zeros, since a `number` keeps none; a column that wants a fixed number of places says so with a
// `format`. A date is read through its own members, never the global Temporal (see dates.ts).

import type { QueryColumn } from '../query/columns';

/** The text a value shows when its column declares no format: empty for a Blank, `yyyy-MM-dd` for a
 * `Temporal.PlainDate`, `yyyy-MM-dd HH:mm:ss` for a `Temporal.PlainDateTime`, `yyyy-MM-dd HH:mm:ss ±hh:mm`
 * for a `Temporal.ZonedDateTime` in its own offset and for a `Temporal.Instant` or a `Date` in UTC's,
 * `HH:mm:ss` for a `Temporal.PlainTime`, `True` or `False` for a boolean, and the value's own text for
 * anything else but a function, which has none and is refused. */
export function displayText(value: unknown): string {
    if (value === null || value === undefined) return '';
    switch (typeof value) {
        case 'string': return value;
        case 'boolean': return value ? 'True' : 'False';
        case 'number':
        case 'bigint': return value.toString();
        case 'symbol': return value.toString();
        case 'function': throw new TypeError('A cell\'s value is a function, which has no text to show; a column reads a value.');
        case 'object':
        case 'undefined': break;
        default: throw new Error(`Internal: a value of the type ${typeof value}, which JavaScript does not have.`);
    }
    switch (Object.prototype.toString.call(value)) {
        case '[object Temporal.PlainDate]': return day(value);
        case '[object Temporal.PlainDateTime]': return `${day(value)} ${time(value)}`;
        case '[object Temporal.ZonedDateTime]': return `${day(value)} ${time(value)} ${text(value, 'offset')}`;
        case '[object Temporal.Instant]': return displayText(called(value, 'toZonedDateTimeISO', 'UTC'));
        case '[object Temporal.PlainTime]': return time(value);
        case '[object Date]': return dateObject(value);
        // An object of the Consumer's own, such as a decimal library's value, shows its own toString, as C#'s
        // shows its own ToString.
        default: return String(value); // oxlint-disable-line typescript/no-base-to-string -- the value's own text is the rule, whatever object it is
    }
}

/** The text a cell of `column` displays for `row`: {@link displayText} of its value, or the column's
 * `format` of it. A Blank is empty whatever the format, which is never asked of one. A format that answers
 * anything but text is refused, naming the column: the grid would otherwise paint, and compare, a text it
 * made up. */
export function textOf<Row>(column: Pick<QueryColumn<Row>, 'name' | 'value' | 'format'>, row: Row): string {
    return textFor(column, column.value(row));
}

/** The same text from a value already read. */
export function textFor<Row>(column: Pick<QueryColumn<Row>, 'name' | 'format'>, value: unknown): string {
    if (value === null || value === undefined) return '';
    if (column.format === undefined) return displayText(value);
    const formatted: unknown = column.format(value);
    if (typeof formatted !== 'string') {
        throw new TypeError(`Column '${column.name}''s format answered ${typeof formatted}, not text.`);
    }
    return formatted;
}

function day(value: object): string {
    return dayText(whole(value, 'year'), whole(value, 'month'), whole(value, 'day'));
}

function time(value: object): string {
    return timeText(whole(value, 'hour'), whole(value, 'minute'), whole(value, 'second'));
}

/** A year outside 0 to 9999 is written as ISO's extended year, with its sign and six digits. */
function dayText(year: number, month: number, dayOfMonth: number): string {
    const written = year >= 0 && year <= 9999 ? padded(year, 4) : `${year < 0 ? '-' : '+'}${padded(Math.abs(year), 6)}`;
    return `${written}-${padded(month, 2)}-${padded(dayOfMonth, 2)}`;
}

function timeText(hour: number, minute: number, second: number): string {
    return `${padded(hour, 2)}:${padded(minute, 2)}:${padded(second, 2)}`;
}

/** A `Date` holds an instant and no offset, so it is written in UTC's, the same for every reader. */
function dateObject(date: object): string {
    const milliseconds: unknown = called(date, 'getTime');
    if (typeof milliseconds !== 'number' || !Number.isFinite(milliseconds)) return 'Invalid Date';
    const utc = new Date(milliseconds);
    return `${dayText(utc.getUTCFullYear(), utc.getUTCMonth() + 1, utc.getUTCDate())} `
        + `${timeText(utc.getUTCHours(), utc.getUTCMinutes(), utc.getUTCSeconds())} +00:00`;
}

/** A member that is a whole number; a value whose tag names a type whose members it does not hold is
 * refused. */
function whole(value: object, name: string): number {
    const read: unknown = Reflect.get(value, name);
    if (typeof read !== 'number' || !Number.isSafeInteger(read)) {
        throw new TypeError(`A value tagged ${Object.prototype.toString.call(value)} has no whole ${name}.`);
    }
    return read;
}

function text(value: object, name: string): string {
    const read: unknown = Reflect.get(value, name);
    if (typeof read !== 'string') {
        throw new TypeError(`A value tagged ${Object.prototype.toString.call(value)} has no ${name} text.`);
    }
    return read;
}

/** The answer of the value's own method, which a value whose tag names a type it is not lacks. */
function called(value: object, name: string, ...args: unknown[]): unknown {
    const method: unknown = Reflect.get(value, name);
    if (typeof method !== 'function') {
        throw new TypeError(`A value tagged ${Object.prototype.toString.call(value)} has no ${name}.`);
    }
    return Reflect.apply(method, value, args);
}

function padded(n: number, digits: number): string {
    return n.toString().padStart(digits, '0');
}
