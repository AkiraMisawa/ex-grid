// A filter operand's text in the View State's JSON (ADR-0207), as the C# core's GridViewStateJson writes and
// reads it in the invariant culture: a Decimal's exact digits with its places, a Double's and a Single's
// round-trip text, a whole number's digits, a DateTime as yyyy-MM-ddTHH:mm:ss.fffffff with its kind beside it,
// a DateOnly as yyyy-MM-dd and a DateTimeOffset with its offset, as zzz writes it.

import { ticksOf, type SnapshotDecimal } from '@ex-grid/data';
import { parseFloatText, roundTripText } from './float-text';
import type { ViewStateDateTimeKind, ViewStateIntegerType, ViewStateOperand } from './view-state';

/** The operand types by the tag the JSON writes, and back. */
export const operandTags: Readonly<Record<ViewStateOperand['type'], string>> = Object.freeze({
    string: 'String', boolean: 'Boolean', decimal: 'Decimal', double: 'Double', single: 'Single',
    int64: 'Int64', int32: 'Int32', int16: 'Int16', sByte: 'SByte', uInt64: 'UInt64', uInt32: 'UInt32', uInt16: 'UInt16', byte: 'Byte',
    dateTime: 'DateTime', dateOnly: 'DateOnly', dateTimeOffset: 'DateTimeOffset',
});

/** The DateTime kinds by the name the JSON writes. */
export const kindNames: Readonly<Record<ViewStateDateTimeKind, string>> = Object.freeze({ unspecified: 'Unspecified', utc: 'Utc', local: 'Local' });

/** Each whole-number type's range, as .NET's type of that name holds it. */
export const integerRanges: Readonly<Record<ViewStateIntegerType, readonly [bigint, bigint]>> = Object.freeze({
    int64: [-(2n ** 63n), 2n ** 63n - 1n],
    int32: [-(2n ** 31n), 2n ** 31n - 1n],
    int16: [-(2n ** 15n), 2n ** 15n - 1n],
    sByte: [-128n, 127n],
    uInt64: [0n, 2n ** 64n - 1n],
    uInt32: [0n, 2n ** 32n - 1n],
    uInt16: [0n, 65_535n],
    byte: [0n, 255n],
});

const decimalLimit = 1n << 96n;
const ticksPerSecond = 10_000_000n;
const ticksPerMinute = 600_000_000n;
/** 9999-12-31T23:59:59.9999999, the last tick a .NET date holds. */
export const maxTicks = 3_155_378_975_999_999_999n;

// ---- Writing ----------------------------------------------------------------------------------------

/** A Decimal's text as .NET's `decimal.ToString(CultureInfo.InvariantCulture)` writes it: its digits, with
 * the places it holds, trailing zeros and all. */
export function decimalOperandText(value: SnapshotDecimal): string {
    const negative = value.mantissa < 0n;
    let digits = (negative ? -value.mantissa : value.mantissa).toString();
    if (value.scale > 0) {
        digits = digits.padStart(value.scale + 1, '0');
        digits = `${digits.slice(0, -value.scale)}.${digits.slice(-value.scale)}`;
    }
    return negative ? `-${digits}` : digits;
}

/** A Double's or a Single's text: its round trip, as `ToString("R", CultureInfo.InvariantCulture)`. */
export function floatOperandText(value: number, type: 'double' | 'single'): string {
    return roundTripText(value, type);
}

/** A date's text from its ticks: `yyyy-MM-dd`, and with `time`, `THH:mm:ss.fffffff` after it. */
export function ticksOperandText(ticks: bigint, time: boolean): string {
    const { year, month, day } = civilDate(Number(ticks / (86_400n * ticksPerSecond)));
    const date = `${pad(year, 4)}-${pad(month, 2)}-${pad(day, 2)}`;
    if (!time) return date;
    const ofDay = ticks % (86_400n * ticksPerSecond);
    const seconds = Number(ofDay / ticksPerSecond);
    const fraction = Number(ofDay % ticksPerSecond);
    return `${date}T${pad(Math.floor(seconds / 3600), 2)}:${pad(Math.floor(seconds / 60) % 60, 2)}:${pad(seconds % 60, 2)}.${pad(fraction, 7)}`;
}

/** An offset as .NET's `zzz` writes it: `+09:00`, `-09:30`, `+00:00`. */
export function offsetText(offsetMinutes: number): string {
    const magnitude = Math.abs(offsetMinutes);
    return `${offsetMinutes < 0 ? '-' : '+'}${pad(Math.floor(magnitude / 60), 2)}:${pad(magnitude % 60, 2)}`;
}

function pad(value: number, width: number): string {
    return String(value).padStart(width, '0');
}

/** The civil date `days` after 0001-01-01, in the proleptic Gregorian calendar .NET's dates use. */
function civilDate(days: number): { readonly year: number; readonly month: number; readonly day: number } {
    let rest = days;
    const cycles = Math.floor(rest / 146_097);
    rest -= cycles * 146_097;
    const centuries = Math.min(Math.floor(rest / 36_524), 3);
    rest -= centuries * 36_524;
    const quads = Math.floor(rest / 1461);
    rest -= quads * 1461;
    const years = Math.min(Math.floor(rest / 365), 3);
    rest -= years * 365;
    const year = cycles * 400 + centuries * 100 + quads * 4 + years + 1;
    let month = 1;
    for (; rest >= daysIn(year, month); month++) rest -= daysIn(year, month);
    return { year, month, day: rest + 1 };
}

function daysIn(year: number, month: number): number {
    if (month === 2) return (year % 4 === 0 && year % 100 !== 0) || year % 400 === 0 ? 29 : 28;
    return [4, 6, 9, 11].includes(month) ? 30 : 31;
}

// ---- Reading ----------------------------------------------------------------------------------------

/**
 * The Decimal a text writes, as the C# core reads one: only the text `decimal.ToString` would write for the
 * value it parses to, so a text with more places or digits than a decimal holds, which the parse would round
 * or flatten to zero, is `undefined`, and so are a sign before zero, a leading zero, a `+`, an exponent and a
 * point without digits on both sides (principle 1).
 */
export function readDecimalText(text: string): SnapshotDecimal | undefined {
    const match = /^(-?)(0|[1-9]\d*)(?:\.(\d+))?$/u.exec(text);
    if (match === null) return undefined;
    const [, sign = '', whole = '', fraction = ''] = match;
    const magnitude = BigInt(whole + fraction);
    if (fraction.length > 28 || magnitude >= decimalLimit || (sign === '-' && magnitude === 0n)) return undefined;
    return Object.freeze({ mantissa: sign === '-' ? -magnitude : magnitude, scale: fraction.length });
}

/** A Double's or a Single's value, as the C# core reads one: finite, and not zero unless the text writes
 * zero, since a figure too small for the type reads as zero and would be flattened, not refused. */
export function readFloatText(text: string, type: 'double' | 'single'): number | undefined {
    const value = parseFloatText(text, type);
    if (value === undefined || !Number.isFinite(value)) return undefined;
    return value !== 0 || writtenAsZero(text) ? value : undefined;
}

/** Whether a floating-point text writes zero: no digit but 0 before its exponent. */
function writtenAsZero(text: string): boolean {
    const [mantissa = text] = text.split(/[eE]/u);
    return !/[1-9]/u.test(mantissa);
}

/** A whole number of the type, as `TryParse` reads it in the invariant culture: a sign allowed only where the
 * type is signed, digits, and within the type's range. */
export function readIntegerText(text: string, type: ViewStateIntegerType): bigint | undefined {
    const [least, most] = integerRanges[type];
    if (!(least < 0n ? /^[+-]?\d+$/u : /^\d+$/u).test(text)) return undefined;
    const value = BigInt(text.startsWith('+') ? text.slice(1) : text);
    return value >= least && value <= most ? value : undefined;
}

const dateTimePattern = /^(\d{4})-(\d{2})-(\d{2})T(\d{2}):(\d{2}):(\d{2})\.(\d{7})/u;

/** A DateTime's ticks from `yyyy-MM-ddTHH:mm:ss.fffffff`, exactly, or `undefined`. */
export function readDateTimeText(text: string): bigint | undefined {
    const match = dateTimePattern.exec(text);
    return match !== null && match[0].length === text.length ? ticksOfMatch(match) : undefined;
}

/** A DateOnly's ticks, its midnight, from `yyyy-MM-dd`, exactly, or `undefined`. */
export function readDateOnlyText(text: string): bigint | undefined {
    const match = /^(\d{4})-(\d{2})-(\d{2})$/u.exec(text);
    return match === null ? undefined : ticksOfMatch(match);
}

/**
 * A DateTimeOffset's clock ticks and offset from `yyyy-MM-ddTHH:mm:ss.fffffffzzz`, as .NET's exact parse
 * reads it: the offset is a sign, one or two digits of hours taken as many as there are, an optional colon
 * and two digits of minutes; it lies within ±14:00, and the instant it names within a date's range.
 */
export function readDateTimeOffsetText(text: string): { readonly ticks: bigint; readonly offsetMinutes: number } | undefined {
    const match = dateTimePattern.exec(text);
    if (match === null) return undefined;
    const ticks = ticksOfMatch(match);
    const offset = offsetOf(text.slice(match[0].length));
    if (ticks === undefined || offset === undefined) return undefined;
    const utc = ticks - BigInt(offset) * ticksPerMinute;
    return utc >= 0n && utc <= maxTicks ? { ticks, offsetMinutes: offset } : undefined;
}

/** The minutes `zzz` reads from the text after the time, or `undefined`. The hours are taken greedily and
 * never given back, as .NET's ParseDigits takes them, so `+930` is refused rather than read as 9:30. */
function offsetOf(text: string): number | undefined {
    const sign = text.charAt(0);
    if (sign !== '+' && sign !== '-') return undefined;
    let at = 1;
    let hours = '';
    while (hours.length < 2 && /\d/u.test(text.charAt(at))) hours += text.charAt(at++);
    if (hours === '') return undefined;
    if (text.charAt(at) === ':') at++;
    const minutes = text.slice(at);
    if (!/^\d{2}$/u.test(minutes) || Number(minutes) >= 60) return undefined;
    const total = Number(hours) * 60 + Number(minutes);
    if (total > 14 * 60) return undefined;
    return sign === '-' ? -total : total;
}

function ticksOfMatch(match: RegExpExecArray): bigint | undefined {
    const [year, month, day, hour, minute, second, fraction] = match.slice(1).map(part => Number(part));
    if (year === undefined || month === undefined || day === undefined) throw new Error('Internal: a date matched without its fields.');
    if (year < 1 || month < 1 || month > 12 || day < 1 || day > daysIn(year, month)) return undefined;
    const [h, m, s, f] = [hour, minute, second, fraction].map(part => (part === undefined || Number.isNaN(part) ? 0 : part));
    if (h === undefined || m === undefined || s === undefined || f === undefined) throw new Error('Internal: a time read without its fields.');
    if (h > 23 || m > 59 || s > 59) return undefined;
    return ticksOf(year, month, day, h, m, s, f);
}
