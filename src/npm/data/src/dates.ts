// The Snapshot's Date (ADR-0064): a clock value, held as C#'s DateTime holds it, in ticks of 100
// nanoseconds since 0001-01-01T00:00, with no zone. A tick count is what ExGrid.Data holds and what Arrow
// carries, so it travels exactly; a date's text, or a Temporal value the Consumer holds, is turned into one
// at the edge. The package never reads the global `Temporal`: a value is recognised by its own tag, as the
// Script Core recognises one, and read through its own members.

import { elementAt } from './must';

/** The ticks in a day. */
export const ticksPerDay = 864_000_000_000n;

/** 1970-01-01T00:00 in ticks: where Arrow counts its dates and timestamps from. */
export const epochTicks = 621_355_968_000_000_000n;

/** 9999-12-31T23:59:59.9999999 in ticks, the last a Date holds. */
export const maxTicks = 3_155_378_975_999_999_999n;

const daysBefore = [0, 31, 59, 90, 120, 151, 181, 212, 243, 273, 304, 334];

function isLeap(year: number): boolean {
    return year % 4 === 0 && (year % 100 !== 0 || year % 400 === 0);
}

const monthDays = [31, 28, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31];

/** The days of a month the caller has checked is one. */
function daysIn(year: number, month: number): number {
    return month === 2 && isLeap(year) ? 29 : elementAt(monthDays, month - 1);
}

/** The days from 0001-01-01 to the day, which the caller has checked is one. */
function dayNumber(year: number, month: number, day: number): number {
    const before = year - 1;
    const leap = month > 2 && isLeap(year) ? 1 : 0;
    return before * 365 + Math.floor(before / 4) - Math.floor(before / 100) + Math.floor(before / 400)
        + elementAt(daysBefore, month - 1) + leap + day - 1;
}

/** The ticks of a date and time, refused with a `RangeError` when it is not one a Date holds. */
export function ticksOf(year: number, month: number, day: number, hour = 0, minute = 0, second = 0, fraction = 0): bigint {
    if (!Number.isInteger(year) || year < 1 || year > 9999) throw new RangeError(`The year ${year} lies outside a date's, 0001 to 9999.`);
    if (!Number.isInteger(month) || month < 1 || month > 12) throw new RangeError(`The month ${month} is not one of a year's.`);
    if (!Number.isInteger(day) || day < 1 || day > daysIn(year, month)) throw new RangeError(`The day ${day} is not one of ${year}-${month}'s.`);
    if (!Number.isInteger(hour) || hour < 0 || hour > 23 || !Number.isInteger(minute) || minute < 0 || minute > 59
        || !Number.isInteger(second) || second < 0 || second > 59) {
        throw new RangeError(`${hour}:${minute}:${second} is not a time of day.`);
    }
    if (!Number.isInteger(fraction) || fraction < 0 || fraction > 9_999_999) throw new RangeError(`${fraction} is not a number of ticks within a second.`);
    const seconds = BigInt(dayNumber(year, month, day)) * 86_400n + BigInt((hour * 60 + minute) * 60 + second);
    return seconds * 10_000_000n + BigInt(fraction);
}

/**
 * The ticks a date's text writes, a Date's clock value: `2026-09-30`, or `2026-09-30T12:00:00` with up to
 * seven places after the second, `2026-09-30T12:00:00.0000001`. Refused with a `TypeError` when the text is
 * not one, and with a `RangeError` when it names no date a Date holds.
 */
export function ticksFromText(text: string): bigint {
    const match = /^(\d{4})-(\d{2})-(\d{2})(?:T(\d{2}):(\d{2}):(\d{2})(?:\.(\d{1,7}))?)?$/.exec(text);
    if (match === null) throw new TypeError(`'${text}' is not a date: a date is written as 2026-09-30, or 2026-09-30T12:00:00.0000001.`);
    // The time and the places after the second are optional: a date alone is its midnight.
    const [, year = '', month = '', day = '', hour = '0', minute = '0', second = '0', fraction = ''] = match;
    return ticksOf(Number(year), Number(month), Number(day), Number(hour), Number(minute), Number(second), Number(fraction.padEnd(7, '0')));
}

function two(value: number): string {
    return value.toString().padStart(2, '0');
}

/** A Date's text, as Temporal's `PlainDateTime` writes one: `2026-09-30T12:00:00`, with the places after
 * the second that are not trailing zeros, `2026-09-30T12:00:00.0000001`. */
export function ticksText(ticks: bigint): string {
    if (ticks < 0n || ticks > maxTicks) throw new RangeError(`${ticks} ticks lie outside a date's, 0001-01-01 to 9999-12-31.`);
    const fraction = Number(ticks % 10_000_000n);
    const seconds = ticks / 10_000_000n;
    const days = Number(seconds / 86_400n);
    const ofDay = Number(seconds % 86_400n);
    // A civil date from the days since 0001-01-01: 400-year cycles of 146,097 days, then centuries,
    // four-year cycles and years.
    let rest = days;
    const cycles = Math.floor(rest / 146_097);
    rest -= cycles * 146_097;
    const centuries = Math.min(3, Math.floor(rest / 36_524));
    rest -= centuries * 36_524;
    const fours = Math.floor(rest / 1_461);
    rest -= fours * 1_461;
    const years = Math.min(3, Math.floor(rest / 365));
    rest -= years * 365;
    const year = cycles * 400 + centuries * 100 + fours * 4 + years + 1;
    let month = 1;
    while (month < 12 && rest >= daysIn(year, month)) {
        rest -= daysIn(year, month);
        month += 1;
    }
    const time = `${two(Math.floor(ofDay / 3600))}:${two(Math.floor(ofDay / 60) % 60)}:${two(ofDay % 60)}`;
    const places = fraction === 0 ? '' : `.${fraction.toString().padStart(7, '0').replace(/0+$/, '')}`;
    return `${year.toString().padStart(4, '0')}-${two(month)}-${two(rest + 1)}T${time}${places}`;
}

/**
 * The ticks of a Temporal `PlainDate`, its midnight, or of a Temporal `PlainDateTime`, its clock value, in
 * whatever calendar it is held; `undefined` for a value that is neither. A value whose tag claims one of
 * them and whose members do not hold it, or a time finer than the 100 nanoseconds a Date holds, is refused
 * with a `RangeError`.
 */
export function ticksOfTemporal(value: object): bigint | undefined {
    const tag = Object.prototype.toString.call(value);
    if (tag !== '[object Temporal.PlainDate]' && tag !== '[object Temporal.PlainDateTime]') return undefined;
    const iso = Reflect.get(value, 'calendarId') === 'iso8601' ? value : inIsoCalendar(value);
    const [year, month, day] = ['year', 'month', 'day'].map(name => whole(iso, name));
    if (year === undefined || month === undefined || day === undefined) throw new RangeError(`The ${tag.slice(8, -1)} does not hold the day it names.`);
    if (tag === '[object Temporal.PlainDate]') return ticksOf(year, month, day);
    const [hour, minute, second, millisecond, microsecond, nanosecond] = ['hour', 'minute', 'second', 'millisecond', 'microsecond', 'nanosecond']
        .map(name => whole(value, name));
    if (hour === undefined || minute === undefined || second === undefined || millisecond === undefined
        || microsecond === undefined || nanosecond === undefined) {
        throw new RangeError('The Temporal.PlainDateTime does not hold the time it names.');
    }
    if (nanosecond % 100 !== 0) throw new RangeError(`The Temporal.PlainDateTime's ${nanosecond} nanoseconds are finer than the 100 a date holds.`);
    return ticksOf(year, month, day, hour, minute, second, millisecond * 10_000 + microsecond * 10 + nanosecond / 100);
}

/** The same day in ISO's calendar, through the value's own `withCalendar`, never the global Temporal. */
function inIsoCalendar(date: object): object {
    const withCalendar: unknown = Reflect.get(date, 'withCalendar');
    const iso: unknown = typeof withCalendar === 'function' ? Reflect.apply(withCalendar, date, ['iso8601']) : undefined;
    if (typeof iso !== 'object' || iso === null) throw new RangeError('The Temporal value cannot say its day in ISO\'s calendar.');
    return iso;
}

/** A member that is a whole number, or `undefined` when it is missing or is not one. */
function whole(value: object, name: string): number | undefined {
    const read: unknown = Reflect.get(value, name);
    return typeof read === 'number' && Number.isSafeInteger(read) ? read : undefined;
}
