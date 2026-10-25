// .NET's date formatting, as `DateTime.ToString(format, culture)` writes a date Item's label (ADR-0060): a
// standard format's culture pattern, or a custom format's specifiers, transcribed from .NET's
// DateTimeFormat for the Gregorian calendar. A date is a clock value without a zone, a DateTime of
// unspecified kind, so `K` writes nothing and `z` writes the offset of UTC, which a .NET server pivots in.
// tools/pivot-oracle holds it to .NET.

import type { DateCulture } from '../culture/culture-data';
import { PivotFormatError } from '../errors';

const ticksPerDay = 864_000_000_000n;
const ticksPerSecond = 10_000_000n;

/** A clock value's parts, from its ticks. */
interface DateParts {
    readonly year: number;
    readonly month: number;
    readonly day: number;
    readonly dayOfWeek: number;
    readonly hour: number;
    readonly minute: number;
    readonly second: number;
    /** The ticks within the second, 0 to 9,999,999. */
    readonly fraction: number;
}

/** The parts of a DateTime's ticks: the proleptic Gregorian calendar from 0001-01-01. */
export function dateParts(ticks: bigint): DateParts {
    const days = Number(ticks / ticksPerDay);
    const within = ticks % ticksPerDay;
    const seconds = Number(within / ticksPerSecond);
    // Days since 0000-03-01, the civil calendar's own start, then back to January's year.
    const shifted = days + 306;
    const era = Math.floor(shifted / 146097);
    const dayOfEra = shifted - era * 146097;
    const yearOfEra = Math.floor((dayOfEra - Math.floor(dayOfEra / 1460) + Math.floor(dayOfEra / 36524) - Math.floor(dayOfEra / 146096)) / 365);
    const dayOfYear = dayOfEra - (365 * yearOfEra + Math.floor(yearOfEra / 4) - Math.floor(yearOfEra / 100));
    const monthFromMarch = Math.floor((5 * dayOfYear + 2) / 153);
    const day = dayOfYear - Math.floor((153 * monthFromMarch + 2) / 5) + 1;
    const month = monthFromMarch < 10 ? monthFromMarch + 3 : monthFromMarch - 9;
    const year = yearOfEra + era * 400 + (month <= 2 ? 1 : 0);
    return {
        year, month, day,
        dayOfWeek: (days + 1) % 7,
        hour: Math.floor(seconds / 3600),
        minute: Math.floor(seconds / 60) % 60,
        second: seconds % 60,
        fraction: Number(within % ticksPerSecond),
    };
}

const badFormat = 'Input string was not in a correct format.';

/** `new DateTime(ticks).ToString(format, culture)`. A format .NET refuses throws a PivotFormatError, as
 * C# throws its FormatException. */
export function formatDate(ticks: bigint, format: string | undefined = '', culture: DateCulture): string {
    const text = format;
    const parts = dateParts(ticks);
    if (text.length === 0) return custom(parts, `${culture.shortDate} ${culture.longTime}`, culture);
    if (text.length === 1) return custom(parts, ...standard(text, culture));
    return custom(parts, text, culture);
}

const invariantDate: DateCulture = {
    shortDate: 'MM/dd/yyyy', longDate: 'dddd, dd MMMM yyyy', shortTime: 'HH:mm', longTime: 'HH:mm:ss',
    fullDateTime: 'dddd, dd MMMM yyyy HH:mm:ss', monthDay: 'MMMM dd', yearMonth: 'yyyy MMMM', am: 'AM', pm: 'PM',
    dateSeparator: '/', timeSeparator: ':', era: 'A.D.', useGenitive: false,
    days: ['Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday'],
    abbreviatedDays: ['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat'],
    months: ['January', 'February', 'March', 'April', 'May', 'June', 'July', 'August', 'September', 'October', 'November', 'December'],
    abbreviatedMonths: ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'],
    genitiveMonths: ['January', 'February', 'March', 'April', 'May', 'June', 'July', 'August', 'September', 'October', 'November', 'December'],
    abbreviatedGenitiveMonths: ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'],
};

/** A standard format's pattern, and the culture it is written in: the invariant one for o, r, s and u. */
function standard(letter: string, culture: DateCulture): [string, DateCulture] {
    switch (letter) {
        case 'd': return [culture.shortDate, culture];
        case 'D': return [culture.longDate, culture];
        case 'f': return [`${culture.longDate} ${culture.shortTime}`, culture];
        case 'F':
        case 'U': return [culture.fullDateTime, culture];
        case 'g': return [`${culture.shortDate} ${culture.shortTime}`, culture];
        case 'G': return [`${culture.shortDate} ${culture.longTime}`, culture];
        case 'm':
        case 'M': return [culture.monthDay, culture];
        case 'o':
        case 'O': return ["yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fffffffK", invariantDate];
        case 'r':
        case 'R': return ["ddd, dd MMM yyyy HH':'mm':'ss 'GMT'", invariantDate];
        case 's': return ["yyyy'-'MM'-'dd'T'HH':'mm':'ss", invariantDate];
        case 't': return [culture.shortTime, culture];
        case 'T': return [culture.longTime, culture];
        case 'u': return ["yyyy'-'MM'-'dd HH':'mm':'ss'Z'", invariantDate];
        case 'y':
        case 'Y': return [culture.yearMonth, culture];
        default: throw new PivotFormatError(badFormat);
    }
}

/** How many times `format[at]` repeats from `at`. */
function repeat(format: string, at: number): number {
    const ch = format.charAt(at);
    let end = at + 1;
    while (end < format.length && format.charAt(end) === ch) end++;
    return end - at;
}

function digits(value: number, length: number): string {
    return value.toString().padStart(length, '0');
}

function name(list: readonly string[], index: number): string {
    const found = list[index];
    if (found === undefined) throw new Error(`Internal: a culture's names miss ${index}.`);
    return found;
}

/** .NET's IsUseGenitiveForm: whether a `MMMM` at `at` stands next to a `d` or `dd`, before or after it. */
function usesGenitive(format: string, at: number, length: number): boolean {
    let i = at - 1;
    while (i >= 0 && format.charAt(i) !== 'd') i--;
    if (i >= 0) {
        let count = 0;
        while (--i >= 0 && format.charAt(i) === 'd') count++;
        if (count <= 1) return true;
    }
    i = at + length;
    while (i < format.length && format.charAt(i) !== 'd') i++;
    if (i < format.length) {
        let count = 0;
        while (++i < format.length && format.charAt(i) === 'd') count++;
        if (count <= 1) return true;
    }
    return false;
}

/** A quoted run from `at`, its quote's own character ending it and a backslash taking the next: the text,
 * and how many characters it took. An unclosed quote is refused. */
function quoted(format: string, at: number): [string, number] {
    const quote = format.charAt(at);
    let i = at + 1;
    let text = '';
    let closed = false;
    while (i < format.length) {
        const ch = format.charAt(i++);
        if (ch === quote) {
            closed = true;
            break;
        }
        if (ch === '\\') {
            if (i >= format.length) throw new PivotFormatError(badFormat);
            text += format.charAt(i++);
        } else {
            text += ch;
        }
    }
    if (!closed) throw new PivotFormatError(badFormat);
    return [text, i - at];
}

/** .NET's FormatCustomized for the Gregorian calendar, appending to `written`, the text so far. */
function custom(parts: DateParts, format: string, culture: DateCulture, written = ''): string {
    let text = written;
    let i = 0;
    while (i < format.length) {
        const ch = format.charAt(i);
        let length: number;
        switch (ch) {
            case 'g':
                length = repeat(format, i);
                text += culture.era;
                break;
            case 'h': {
                length = repeat(format, i);
                const hour = parts.hour % 12;
                text += digits(hour === 0 ? 12 : hour, Math.min(length, 2));
                break;
            }
            case 'H':
                length = repeat(format, i);
                text += digits(parts.hour, Math.min(length, 2));
                break;
            case 'm':
                length = repeat(format, i);
                text += digits(parts.minute, Math.min(length, 2));
                break;
            case 's':
                length = repeat(format, i);
                text += digits(parts.second, Math.min(length, 2));
                break;
            case 'f':
            case 'F': {
                length = repeat(format, i);
                if (length > 7) throw new PivotFormatError(badFormat);
                let fraction = Math.floor(parts.fraction / 10 ** (7 - length));
                if (ch === 'f') {
                    text += digits(fraction, length);
                } else {
                    let effective = length;
                    while (effective > 0 && fraction % 10 === 0) {
                        fraction /= 10;
                        effective--;
                    }
                    if (effective > 0) text += digits(fraction, effective);
                    else if (text.endsWith('.')) text = text.slice(0, -1);
                }
                break;
            }
            case 't':
                length = repeat(format, i);
                if (length === 1) {
                    const designator = parts.hour < 12 ? culture.am : culture.pm;
                    if (designator.length >= 1) text += designator.charAt(0);
                } else {
                    text += parts.hour < 12 ? culture.am : culture.pm;
                }
                break;
            case 'd':
                length = repeat(format, i);
                if (length <= 2) text += digits(parts.day, length);
                else if (length === 3) text += name(culture.abbreviatedDays, parts.dayOfWeek);
                else text += name(culture.days, parts.dayOfWeek);
                break;
            case 'M':
                length = repeat(format, i);
                if (length <= 2) {
                    text += digits(parts.month, length);
                } else if (culture.useGenitive && usesGenitive(format, i, length)) {
                    text += name(length === 3 ? culture.abbreviatedGenitiveMonths : culture.genitiveMonths, parts.month - 1);
                } else {
                    text += name(length === 3 ? culture.abbreviatedMonths : culture.months, parts.month - 1);
                }
                break;
            case 'y':
                length = repeat(format, i);
                text += length <= 2 ? digits(parts.year % 100, length) : digits(parts.year, length);
                break;
            case 'z':
                // A clock value without a zone is written in the offset of UTC, where the C# engine runs.
                length = repeat(format, i);
                text += length === 1 ? '+0' : length === 2 ? '+00' : '+00:00';
                break;
            case 'K':
                length = 1;
                break;
            case ':':
                length = 1;
                text += culture.timeSeparator;
                break;
            case '/':
                length = 1;
                text += culture.dateSeparator;
                break;
            case '\'':
            case '"': {
                const [run, taken] = quoted(format, i);
                text += run;
                length = taken;
                break;
            }
            case '%': {
                const following = format.charAt(i + 1);
                if (i + 1 >= format.length || following === '%') throw new PivotFormatError(badFormat);
                text = custom(parts, following, culture, text);
                length = 2;
                break;
            }
            case '\\':
                if (i + 1 >= format.length) throw new PivotFormatError(badFormat);
                text += format.charAt(i + 1);
                length = 2;
                break;
            default:
                text += ch;
                length = 1;
                break;
        }
        i += length;
    }
    return text;
}
