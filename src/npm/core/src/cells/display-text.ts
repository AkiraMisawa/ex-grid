// The text a value cell displays (ADR-0006, note of 2026-10-01; ADR-0350 section 16): empty for a Blank,
// the column's `format` where it has one, and otherwise the value's own text, which for a date is one ISO
// form by its type, so the same value reads the same for every reader. The one place the rule is written:
// the cell paints by it, and a source compares by it, so a change the format hides is not marked (ADR-0067)
// and a Find matches what the user reads (ADR-0055).
//
// A value's own text is C#'s where the two can agree: a boolean reads `True` or `False`, as C#'s does, so
// a column reads alike on either core. A number reads as Excel's General does: rounded to 15 significant
// digits, then JavaScript's shortest text for that, so 0.1 + 0.2 shows 0.3 and not the 0.30000000000000004
// a binary fraction leaves (decided with the user, 2026-10-26; ADR-0350 section 16). It holds no trailing
// zeros, since a `number` keeps none; a column that wants a fixed number of places says so with a `format`.
// A `bigint` is exact and shows every digit. The clipboard's raw form keeps the number whole (rawText). A
// date is read through its own members, never the global Temporal, and shows the ISO day it names in whatever
// calendar it is held, the day its filter and sort read (see dates.ts).

import type { QueryColumn } from '../query/columns';
import { isoDay, whole as wholeMember } from '../query/dates';

/** The text a value shows when its column declares no format: empty for a Blank, `yyyy-MM-dd` for a
 * `Temporal.PlainDate`, `yyyy-MM-dd HH:mm:ss` for a `Temporal.PlainDateTime`, `yyyy-MM-dd HH:mm:ss ±hh:mm`
 * for a `Temporal.ZonedDateTime` in its own offset and for a `Temporal.Instant` or a `Date` in UTC's,
 * `HH:mm:ss` for a `Temporal.PlainTime`, `True` or `False` for a boolean, a `number` in at most 15 significant
 * digits as Excel's General shows it, and the value's own text for anything else but a function, which has none
 * and is refused. */
export function displayText(value: unknown): string {
    if (value === null || value === undefined) return '';
    switch (typeof value) {
        case 'string': return value;
        case 'boolean': return value ? 'True' : 'False';
        case 'number': return generalText(value);
        case 'bigint':
        case 'symbol': return value.toString();
        case 'function': throw new TypeError('A cell\'s value is a function, which has no text to show; a column reads a value.');
        // A Blank returned above: what is left is an object.
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

/** The entry a cell's editor opens on where the Consumer gives none, which the Formula Bar shows and Replace
 * rewrites: the shown text, but a `number` in a column with no format whole, so an edit committed unchanged, or a
 * Replace of other digits, never writes the 15 digits it shows back over the value (ADR-0006, note of 2026-10-26). */
export function entryFor<Row>(column: Pick<QueryColumn<Row>, 'name' | 'format'>, value: unknown): string {
    return column.format === undefined && typeof value === 'number' ? value.toString() : textFor(column, value);
}

/**
 * The raw, locale-free text of a value, for the clipboard's `text/html` flavour (ADR-0005): what a spreadsheet
 * reads back as the same value, never the displayed format, whose separators are a locale trap. Empty for a
 * Blank, `TRUE` or `FALSE` for a boolean, a number's own shortest text in full precision, and a date in ISO
 * form with a `T` between the day and the time, as the C# core writes them: `yyyy-MM-ddTHH:mm:ss` for a
 * `Temporal.PlainDateTime`, with `±hh:mm` after it for a `Temporal.ZonedDateTime`, and `+00:00` for a
 * `Temporal.Instant` or a `Date`, which hold no offset of their own. Anything else is its own text.
 */
export function rawText(value: unknown): string {
    if (value === null || value === undefined) return '';
    if (typeof value === 'boolean') return value ? 'TRUE' : 'FALSE';
    // Whole: a spreadsheet pasted into reads back the value the source holds, not the 15 digits it shows.
    if (typeof value === 'number') return value.toString();
    if (typeof value !== 'object') return displayText(value);
    switch (Object.prototype.toString.call(value)) {
        case '[object Temporal.PlainDateTime]': return `${day(value)}T${time(value)}`;
        case '[object Temporal.ZonedDateTime]': return `${day(value)}T${time(value)}${text(value, 'offset')}`;
        case '[object Temporal.Instant]': return rawText(called(value, 'toZonedDateTimeISO', 'UTC'));
        case '[object Date]': return displayText(value).replace(' ', 'T').replace(' ', '');
        default: return displayText(value);
    }
}

/** Excel's General for a number: at most 15 significant digits, the precision Excel holds and shows, written
 * as the shortest text of the number they round to. NaN and the infinities have no digits to round. */
function generalText(value: number): string {
    return Number.isFinite(value) ? Number(value.toPrecision(15)).toString() : value.toString();
}

function day(value: object): string {
    const iso = isoDay(value);
    if (iso === undefined) throw new TypeError(`A value tagged ${tagOf(value)} does not hold the day its type names.`);
    return dayText(iso.year, iso.month, iso.day);
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
    // An invalid Date names no instant, and the query refuses it too: it is refused, never shown as a text
    // that compares equal to every other invalid one.
    if (typeof milliseconds !== 'number' || !Number.isFinite(milliseconds)) throw new TypeError('A cell\'s Date is not a valid date.');
    const utc = new Date(milliseconds);
    return `${dayText(utc.getUTCFullYear(), utc.getUTCMonth() + 1, utc.getUTCDate())} `
        + `${timeText(utc.getUTCHours(), utc.getUTCMinutes(), utc.getUTCSeconds())} +00:00`;
}

/** A member that is a whole number; a value whose tag names a type whose members it does not hold is
 * refused. */
function whole(value: object, name: string): number {
    const read = wholeMember(value, name);
    if (read === undefined) throw new TypeError(`A value tagged ${tagOf(value)} has no whole ${name}.`);
    return read;
}

function tagOf(value: object): string {
    return Object.prototype.toString.call(value);
}

function text(value: object, name: string): string {
    const read: unknown = Reflect.get(value, name);
    if (typeof read !== 'string') {
        throw new TypeError(`A value tagged ${tagOf(value)} has no ${name} text.`);
    }
    return read;
}

/** The answer of the value's own method, which a value whose tag names a type it is not lacks. */
function called(value: object, name: string, ...args: unknown[]): unknown {
    const method: unknown = Reflect.get(value, name);
    if (typeof method !== 'function') {
        throw new TypeError(`A value tagged ${tagOf(value)} has no ${name}.`);
    }
    return Reflect.apply(method, value, args);
}

function padded(n: number, digits: number): string {
    return n.toString().padStart(digits, '0');
}
