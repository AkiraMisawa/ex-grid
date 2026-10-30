// .NET's custom date format, as `DateTime.ToString(format, culture)` writes a date under the formats a
// FormatCode translates an Excel date code into (ADR-0342): `yy`, `yyyy`, `d` to `dddd`, `M` to `MMMM`, `H`, `HH`,
// `h`, `hh`, `m`, `mm`, `s`, `ss`, `tt`, a `\` before a character written as it stands, and a leading `%`
// that marks a format of one character as custom. It is .NET's FormatCustomized for those tokens, over a
// Date's ticks (C#'s DateTime ticks, as @ex-grid/data holds a Date): a month's name beside a day number is
// its genitive form, as .NET's IsUseGenitiveForm decides it.

import type { DateNames, FormatCulture } from './format-culture';

const ticksPerDay = 864_000_000_000n;
const ticksPerSecond = 10_000_000n;
/** 9999-12-31T23:59:59.9999999 in ticks, the last a Date holds. */
const maxTicks = 3_155_378_975_999_999_999n;

/** A Date's fields, read from its ticks. */
interface DateFields {
    readonly year: number;
    readonly month: number;
    readonly day: number;
    readonly hour: number;
    readonly minute: number;
    readonly second: number;
    /** 0 for Sunday. */
    readonly dayOfWeek: number;
}

/** The fields of a Date's ticks, refused with a `RangeError` outside 0001-01-01 to 9999-12-31. */
function fieldsOf(ticks: bigint): DateFields {
    if (ticks < 0n || ticks > maxTicks) {
        throw new RangeError(`${ticks} ticks lie outside a date's, 0001-01-01 to 9999-12-31.`);
    }
    const days = Number(ticks / ticksPerDay);
    const seconds = Number((ticks % ticksPerDay) / ticksPerSecond);
    // The civil date of a day number from 0001-01-01, by 400-year cycles of 146097 days.
    const cycles = Math.floor(days / 146097);
    let rest = days % 146097;
    let centuries = Math.floor(rest / 36524);
    if (centuries === 4) centuries = 3;
    rest -= centuries * 36524;
    const quads = Math.floor(rest / 1461);
    rest -= quads * 1461;
    let years = Math.floor(rest / 365);
    if (years === 4) years = 3;
    rest -= years * 365;
    const year = cycles * 400 + centuries * 100 + quads * 4 + years + 1;
    const leap = year % 4 === 0 && (year % 100 !== 0 || year % 400 === 0);
    const lengths = [31, leap ? 29 : 28, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31];
    let month = 0;
    for (const length of lengths) {
        if (rest < length) break;
        rest -= length;
        month++;
    }
    return {
        year,
        month: month + 1,
        day: rest + 1,
        hour: Math.floor(seconds / 3600),
        minute: Math.floor(seconds / 60) % 60,
        second: seconds % 60,
        // 0001-01-01 was a Monday.
        dayOfWeek: (days + 1) % 7,
    };
}

/** `new DateTime(ticks).ToString(format, culture)` for the formats a FormatCode writes. */
export function customDate(ticks: bigint, format: string, culture: FormatCulture): string {
    const date = fieldsOf(ticks);
    let text = '';
    let i = 0;
    // A leading % marks a format of one character as custom; it is not written.
    const source = format.length === 2 && format.startsWith('%') ? format.slice(1) : format;
    while (i < source.length) {
        const ch = source.charAt(i);
        if (ch === '\\') {
            if (i + 1 >= source.length) throw new Error(`Internal: the date format "${format}" ends in an escape.`);
            text += source.charAt(i + 1);
            i += 2;
            continue;
        }
        let length = 1;
        while (i + length < source.length && source.charAt(i + length) === ch) length++;
        switch (ch) {
            case 'y':
                text += length <= 2 ? digits(date.year % 100, length) : digits(date.year, length);
                break;
            case 'M':
                text += length <= 2 ? digits(date.month, length) : monthName(date.month, length, isGenitive(source, i, length), culture);
                break;
            case 'd':
                text += length <= 2 ? digits(date.day, length) : dayName(date.dayOfWeek, length, culture);
                break;
            case 'H':
                text += digits(date.hour, Math.min(length, 2));
                break;
            case 'h':
                text += digits(date.hour % 12 === 0 ? 12 : date.hour % 12, Math.min(length, 2));
                break;
            case 'm':
                text += digits(date.minute, Math.min(length, 2));
                break;
            case 's':
                text += digits(date.second, Math.min(length, 2));
                break;
            case 't': {
                const names = namesIn(culture, 'AM or PM');
                const designator = date.hour < 12 ? names.am : names.pm;
                text += length === 1 ? designator.slice(0, 1) : designator;
                break;
            }
            default:
                throw new Error(`Internal: '${ch}' is not a token of the date formats a FormatCode writes ("${format}").`);
        }
        i += length;
    }
    return text;
}

function digits(value: number, length: number): string {
    return value.toString().padStart(length, '0');
}

function namesIn(culture: FormatCulture, what: string): DateNames {
    const { names } = culture;
    if (names === undefined) {
        throw new RangeError(`The locale '${culture.name}' has no names in the core's table, so a date code that shows ${what} `
            + 'cannot be written in it; its numbers and numeric dates can (ADR-0370 section 10).');
    }
    return names;
}

function monthName(month: number, length: number, genitive: boolean, culture: FormatCulture): string {
    const names = namesIn(culture, 'a month\'s name');
    const list = length === 3
        ? (genitive ? names.abbreviatedGenitiveMonths : names.abbreviatedMonths)
        : (genitive ? names.genitiveMonths : names.months);
    return nameAt(list, month - 1);
}

function dayName(dayOfWeek: number, length: number, culture: FormatCulture): string {
    const names = namesIn(culture, 'a day\'s name');
    return nameAt(length === 3 ? names.abbreviatedDays : names.days, dayOfWeek);
}

function nameAt(list: readonly string[], index: number): string {
    const name = list[index];
    if (name === undefined) throw new Error(`Internal: name ${index} is outside a list of ${list.length}.`);
    return name;
}

/** .NET's IsUseGenitiveForm: whether a day number, `d` or `dd`, stands before the month token at `index`
 * (the nearest run of `d` before it) or after it (the nearest run after), read over the format's raw
 * characters as .NET reads them. */
function isGenitive(format: string, index: number, length: number): boolean {
    let i = index - 1;
    while (i >= 0 && format.charAt(i) !== 'd') i--;
    if (i >= 0) {
        let repeat = 0;
        while (--i >= 0 && format.charAt(i) === 'd') repeat++;
        if (repeat <= 1) return true;
    }
    i = index + length;
    while (i < format.length && format.charAt(i) !== 'd') i++;
    if (i < format.length) {
        let repeat = 0;
        while (++i < format.length && format.charAt(i) === 'd') repeat++;
        if (repeat <= 1) return true;
    }
    return false;
}
