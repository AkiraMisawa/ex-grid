// A Date column's values (ADR-0023, ADR-0350 section 16). Each date type is one Temporal type, and is
// compared as C# compares its counterpart: a day by its day (`Temporal.PlainDate`, C#'s DateOnly), a
// wall-clock date and time by its fields, whatever zone it was taken in (`Temporal.PlainDateTime`,
// DateTime), and an instant by the instant it names, whatever offset it is written in
// (`Temporal.ZonedDateTime`, `Temporal.Instant` or a `Date`, DateTimeOffset). A calendar other than
// ISO's names the same day, so it compares as that day, as Temporal's own `compare` does.
//
// The core never reads the global `Temporal`: a browser without it takes a polyfill the Consumer loads, so
// a value is recognised by its own tag, which a polyfill's values carry as the browser's do, and read
// through its own members. A value whose tag claims a type its members do not hold is refused.

import type { DateType } from './columns';

/** A date as it is compared: a day as a number that orders as the day does, a date and time or an
 * instant as a bigint that orders as it does. */
export type DateKey = number | bigint;

/** Said of a value whose tag claims a date type its members do not hold. */
const malformed = 'does not hold the date its type names';

/** The value as it is compared, or a reason it is not a value of the declared date type; the caller
 * names the column. */
export function dateKey(type: DateType, value: unknown): DateKey | string {
    if (typeof value !== 'object' || value === null) return 'contradicts the declared type';
    const tag = Object.prototype.toString.call(value);
    switch (tag) {
        case '[object Temporal.PlainDate]': return type === 'dateOnly' ? dayKey(value) : notDeclared(type);
        case '[object Temporal.PlainDateTime]': return type === 'dateTime' ? dateTimeKey(value) : notDeclared(type);
        case '[object Temporal.ZonedDateTime]':
        case '[object Temporal.Instant]': return type === 'dateTimeOffset' ? instantKey(value) : notDeclared(type);
        case '[object Date]': return type === 'dateTimeOffset' ? dateObjectKey(value) : notDeclared(type);
        default: return 'contradicts the declared type';
    }
}

function notDeclared(type: DateType): string {
    return `is not the column's declared ${type}`;
}

/** A day as year × 10000 + month × 100 + day, in ISO's calendar: monotone in the day for every year
 * Temporal holds, negative ones included. A day in another calendar is the ISO day it names, as
 * Temporal's own `compare` takes it. */
function dayKey(date: object): number | string {
    const iso = Reflect.get(date, 'calendarId') === 'iso8601' ? date : inIsoCalendar(date);
    if (iso === undefined) return malformed;
    const year = whole(iso, 'year');
    const month = whole(iso, 'month');
    const day = whole(iso, 'day');
    if (year === undefined || month === undefined || day === undefined) return malformed;
    return year * 10000 + month * 100 + day;
}

/** A date and time as its day × 10^15 plus its nanosecond of the day, which is under 10^14. */
function dateTimeKey(dateTime: object): bigint | string {
    const day = dayKey(dateTime);
    const parts = ['hour', 'minute', 'second', 'millisecond', 'microsecond', 'nanosecond'].map(name => whole(dateTime, name));
    const [hour, minute, second, millisecond, microsecond, nanosecond] = parts;
    if (typeof day === 'string') return day;
    if (hour === undefined || minute === undefined || second === undefined || millisecond === undefined
        || microsecond === undefined || nanosecond === undefined) return malformed;
    const seconds = (hour * 60 + minute) * 60 + second;
    const fraction = (millisecond * 1000 + microsecond) * 1000 + nanosecond;
    return BigInt(day) * 10n ** 15n + BigInt(seconds) * 1_000_000_000n + BigInt(fraction);
}

/** A ZonedDateTime or an Instant: either names its instant in nanoseconds since the epoch. */
function instantKey(instant: object): bigint | string {
    const nanoseconds: unknown = Reflect.get(instant, 'epochNanoseconds');
    return typeof nanoseconds === 'bigint' ? nanoseconds : malformed;
}

function dateObjectKey(date: object): bigint | string {
    const getTime: unknown = Reflect.get(date, 'getTime');
    if (typeof getTime !== 'function') return malformed;
    const milliseconds: unknown = Reflect.apply(getTime, date, []);
    return typeof milliseconds === 'number' && Number.isFinite(milliseconds) ? BigInt(milliseconds) * 1_000_000n : 'is not a valid date';
}

/** The same day in ISO's calendar, through the value's own `withCalendar`, never the global Temporal. */
function inIsoCalendar(date: object): object | undefined {
    const withCalendar: unknown = Reflect.get(date, 'withCalendar');
    if (typeof withCalendar !== 'function') return undefined;
    const iso: unknown = Reflect.apply(withCalendar, date, ['iso8601']);
    return typeof iso === 'object' && iso !== null ? iso : undefined;
}

/** A member that is a whole number, or `undefined` when it is missing or is not one. */
function whole(value: object, name: string): number | undefined {
    const read: unknown = Reflect.get(value, name);
    return typeof read === 'number' && Number.isSafeInteger(read) ? read : undefined;
}
