// One declared date format (ADR-0064), read as ExGrid.Data's DateFormat reads one: as .NET's
// `DateTime.TryParseExact` reads a value under a custom format string, and as `DateTimeOffset.TryParseExact`
// reads one with an offset, which is then held as the clock it shows. The specifiers are .NET's: `y` to
// `yyyyy`, `M` to `MMMM`, `d` to `dddd`, `h`, `H`, `m`, `s`, `f` and `F` to seven, `t` and `tt`, `z` to `zzz`,
// `K`, and in a read `Z` for UTC; `/` and `:` are the culture's separators, or themselves; text in quotes, after
// a `\` or alone stands as written, and a `.` before `F` may be left out with it. A format of one character is
// one of .NET's standard formats, kept here in the invariant culture.

import { daysIn, maxTicks, ticksOf } from '../dates';
import { elementAt } from '../must';
import { dateCulture, type DateCulture } from './culture';

type Token =
    | { readonly kind: 'field'; readonly letter: string; readonly count: number }
    | { readonly kind: 'literal'; readonly text: string; readonly plain: boolean }
    | { readonly kind: 'dateSeparator' }
    | { readonly kind: 'timeSeparator' }
    | { readonly kind: 'utc' };

const fieldLetters = 'dfFghHKmMstyz';

/** The standard formats of .NET's invariant culture, as the custom formats they stand for. */
const invariantStandard: Readonly<Record<string, string>> = Object.freeze({
    d: 'MM/dd/yyyy',
    D: 'dddd, dd MMMM yyyy',
    f: 'dddd, dd MMMM yyyy HH:mm',
    F: 'dddd, dd MMMM yyyy HH:mm:ss',
    g: 'MM/dd/yyyy HH:mm',
    G: 'MM/dd/yyyy HH:mm:ss',
    m: 'MMMM dd',
    M: 'MMMM dd',
    o: "yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fffffffK",
    O: "yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fffffffK",
    r: "ddd, dd MMM yyyy HH':'mm':'ss 'GMT'",
    R: "ddd, dd MMM yyyy HH':'mm':'ss 'GMT'",
    s: "yyyy'-'MM'-'dd'T'HH':'mm':'ss",
    t: 'HH:mm',
    T: 'HH:mm:ss',
    u: "yyyy'-'MM'-'dd HH':'mm':'ss'Z'",
    y: 'yyyy MMMM',
    Y: 'yyyy MMMM',
});

/** The tokens of a custom format, or why it cannot be read. */
function tokenize(format: string): readonly Token[] | string {
    const tokens: Token[] = [];
    for (let i = 0; i < format.length;) {
        const c = format.charAt(i);
        if (fieldLetters.includes(c)) {
            let count = 1;
            while (format.charAt(i + count) === c) count++;
            if ((c === 'f' || c === 'F') && count > 7) return `it asks for ${count} places of a second, and a date holds seven`;
            if (c === 'g') return 'it names the era, which is not read here';
            if (c === 'K' && count > 1) return 'it writes K more than once in a row';
            tokens.push({ kind: 'field', letter: c, count });
            i += count;
            continue;
        }
        switch (c) {
            case 'Z':
                tokens.push({ kind: 'utc' });
                i++;
                continue;
            case '/':
                tokens.push({ kind: 'dateSeparator' });
                i++;
                continue;
            case ':':
                tokens.push({ kind: 'timeSeparator' });
                i++;
                continue;
            case "'":
            case '"': {
                let text = '';
                let at = i + 1;
                for (; at < format.length && format.charAt(at) !== c; at++) {
                    if (format.charAt(at) === '\\') {
                        at++;
                        if (at >= format.length) return 'a quote is not closed';
                    }
                    text += format.charAt(at);
                }
                if (at >= format.length) return 'a quote is not closed';
                tokens.push({ kind: 'literal', text, plain: false });
                i = at + 1;
                continue;
            }
            case '%': {
                const next = format.charAt(i + 1);
                if (next === '' || next === '%') return "a '%' stands where no specifier follows it";
                // `%` makes the one character after it a custom format of its own: `%d` is the day.
                const one = tokenize(next);
                if (typeof one === 'string') return one;
                tokens.push(...one);
                i += 2;
                continue;
            }
            case '\\':
                if (i + 1 >= format.length) return "a '\\' ends it, with nothing to write as it stands";
                tokens.push({ kind: 'literal', text: format.charAt(i + 1), plain: false });
                i += 2;
                continue;
            default:
                tokens.push({ kind: 'literal', text: c, plain: true });
                i++;
        }
    }
    return tokens;
}

/** What a read has found so far: each field once, a field read twice reading the same both times. */
interface Found {
    year?: number;
    month?: number;
    day?: number;
    hour?: number;
    minute?: number;
    second?: number;
    fraction?: number;
    dayOfWeek?: number;
    pm?: boolean;
    offset?: number;
}

/** Sets a field, as .NET's CheckNewValue: a field read twice must read alike. */
function put<K extends keyof Found>(found: Found, key: K, value: NonNullable<Found[K]>): boolean {
    const was = found[key];
    if (was !== undefined && was !== value) return false;
    found[key] = value;
    return true;
}

/** A read in progress over a value's text. */
class Cursor {
    readonly text: string;
    at = 0;

    constructor(text: string) {
        this.text = text;
    }

    /** `min` to `max` digits, as many as stand there; `undefined` when fewer than `min` do. */
    digits(min: number, max: number): number | undefined {
        let value = 0;
        let count = 0;
        while (count < max) {
            const code = this.text.charCodeAt(this.at + count) - 0x30;
            if (!(code >= 0 && code <= 9)) break;
            value = value * 10 + code;
            count++;
        }
        if (count < min) return undefined;
        this.at += count;
        return value;
    }

    /** The digits a field of `count` letters reads: one or two for one letter, exactly `count` otherwise. */
    field(count: number): number | undefined {
        return count === 1 ? this.digits(1, 2) : this.digits(count, count);
    }

    match(literal: string): boolean {
        if (!this.text.startsWith(literal, this.at)) return false;
        this.at += literal.length;
        return true;
    }

    /** The longest of `names` that stands here, ignoring case in `locale`, as its index; -1 for none. */
    name(names: readonly (readonly string[])[], locale: string | undefined): number {
        let best = -1;
        let length = 0;
        names.forEach((spellings, index) => {
            for (const spelling of spellings) {
                if (spelling.length <= length || spelling === '') continue;
                const here = this.text.slice(this.at, this.at + spelling.length);
                if (here.toLocaleLowerCase(locale) === spelling.toLocaleLowerCase(locale)) {
                    best = index;
                    length = spelling.length;
                }
            }
        });
        this.at += length;
        return best;
    }

    /** A sign and an offset of `count` letters of `z`, in minutes; `undefined` when none stands here. */
    offset(count: number): number | undefined {
        const sign = this.text.charAt(this.at);
        if (sign !== '+' && sign !== '-') return undefined;
        this.at++;
        const hours = count === 2 ? this.digits(2, 2) : this.digits(1, 2);
        if (hours === undefined) return undefined;
        let minutes = 0;
        if (count >= 3) {
            this.match(':');
            const read = this.digits(2, 2);
            if (read === undefined || read >= 60) return undefined;
            minutes = read;
        }
        const total = hours * 60 + minutes;
        return sign === '-' ? -total : total;
    }
}

/** One declared date format, checked, read in its culture. */
export class DateFormat {
    /** The format as declared, as a refusal names it. */
    readonly format: string;
    readonly #tokens: readonly Token[];
    readonly #culture: DateCulture;
    readonly #hasOffset: boolean;
    readonly #twelveHour: boolean;
    /** The culture's day names and AM and PM, each a list of its spellings, as `Cursor.name` takes names. */
    readonly #days: readonly (readonly string[])[];
    readonly #abbreviatedDays: readonly (readonly string[])[];
    readonly #periods: readonly (readonly string[])[];

    private constructor(format: string, tokens: readonly Token[], culture: DateCulture) {
        this.format = format;
        this.#tokens = tokens;
        this.#culture = culture;
        this.#hasOffset = tokens.some(token => token.kind === 'field' && (token.letter === 'z' || token.letter === 'K'));
        this.#twelveHour = tokens.some(token => token.kind === 'field' && token.letter === 'h');
        this.#days = culture.days.map(name => [name]);
        this.#abbreviatedDays = culture.abbreviatedDays.map(name => [name]);
        this.#periods = [[culture.am], [culture.pm]];
        Object.freeze(this);
    }

    /** The format read in `locale`, or why it cannot be. */
    static of(format: string, locale: string | undefined): DateFormat | string {
        let custom = format;
        if (format.length === 1) {
            const standard = invariantStandard[format];
            if (standard === undefined) return `'${format}' is not one of .NET's standard formats read here`;
            if (locale !== undefined) return 'a standard format is read here only in the invariant culture; write it as a custom format';
            custom = standard;
        }
        const tokens = tokenize(custom);
        if (typeof tokens === 'string') return tokens;
        return new DateFormat(format, tokens, dateCulture(locale));
    }

    /** Why a date read under the format would take part of its value from the day it is read — a month or a
     * day without a year takes the current year, an offset without a date the current date — as ExGrid.Data
     * tells it; `undefined` when nothing is taken from it. */
    takenFromToday(): string | undefined {
        // A month, a day of the month or a day's name without a year: .NET takes the year from the day it reads.
        const letters = this.#tokens.flatMap(token => (token.kind === 'field' ? [token.letter] : []));
        if (letters.includes('y')) return undefined;
        if (letters.includes('M') || letters.includes('d')) return 'it has a month or a day but no year, so a date read under it would take the year it is read in';
        return this.#hasOffset ? 'it has an offset but no date, so a date read under it would take the day it is read on' : undefined;
    }

    /** The ticks of `text`, already trimmed, read under the format: the clock it shows, its offset dropped;
     * `undefined` when it is not a date in the format. */
    read(text: string): bigint | undefined {
        const culture = this.#culture;
        const cursor = new Cursor(text);
        const found: Found = {};
        const tokens = this.#tokens;
        for (let t = 0; t < tokens.length; t++) {
            const token = elementAt(tokens, t);
            switch (token.kind) {
                case 'literal':
                    if (cursor.match(token.text)) break;
                    // A '.' before F's is left out with them, as .NET reads "ss.FFFFFFF" against "14:05:06".
                    if (token.plain && token.text === '.') {
                        const next = tokens[t + 1];
                        if (next?.kind === 'field' && next.letter === 'F') {
                            t++;
                            break;
                        }
                    }
                    return undefined;
                case 'dateSeparator':
                    if (!cursor.match('/') && !cursor.match(culture.dateSeparator)) return undefined;
                    break;
                case 'timeSeparator':
                    if (!cursor.match(':') && !cursor.match(culture.timeSeparator)) return undefined;
                    break;
                case 'utc':
                    if (!cursor.match('Z') && !cursor.match('GMT')) return undefined;
                    if (!put(found, 'offset', 0)) return undefined;
                    break;
                case 'field':
                    if (!this.#field(cursor, token.letter, token.count, found)) return undefined;
                    break;
                default:
                    return token satisfies never;
            }
        }
        if (cursor.at !== text.length) return undefined;
        return this.#ticks(found);
    }

    #field(cursor: Cursor, letter: string, count: number, found: Found): boolean {
        const culture = this.#culture;
        switch (letter) {
            case 'y': {
                const year = count <= 2 ? cursor.field(count) : cursor.digits(count, count);
                if (year === undefined) return false;
                if (count > 2) return put(found, 'year', year);
                // Two digits stand for the century the culture's calendar puts them in: 2049 at most in the
                // Gregorian, so 49 is 2049 and 50 is 1950.
                const max = culture.twoDigitYearMax;
                return put(found, 'year', ((Math.floor(max / 100) - (year > max % 100 ? 1 : 0)) * 100) + year);
            }
            case 'M': {
                if (count <= 2) {
                    const month = cursor.field(count);
                    return month !== undefined && put(found, 'month', month);
                }
                const index = cursor.name(count === 3 ? culture.abbreviatedMonths : culture.months, culture.locale);
                return index >= 0 && put(found, 'month', index + 1);
            }
            case 'd': {
                if (count <= 2) {
                    const day = cursor.field(count);
                    return day !== undefined && put(found, 'day', day);
                }
                const index = cursor.name(count === 3 ? this.#abbreviatedDays : this.#days, culture.locale);
                return index >= 0 && put(found, 'dayOfWeek', index);
            }
            case 'h':
            case 'H': {
                const hour = cursor.field(count > 2 ? 2 : count);
                if (hour === undefined) return false;
                if (letter === 'h' && hour > 12) return false;
                return put(found, 'hour', hour);
            }
            case 'm': {
                const minute = cursor.field(count > 2 ? 2 : count);
                return minute !== undefined && put(found, 'minute', minute);
            }
            case 's': {
                const second = cursor.field(count > 2 ? 2 : count);
                return second !== undefined && put(found, 'second', second);
            }
            case 'f':
            case 'F': {
                // f's read exactly as many digits as they are; F's as many as stand there, up to as many, or none.
                const start = cursor.at;
                const digits = cursor.digits(letter === 'f' ? count : 0, count);
                if (digits === undefined) return false;
                const read = cursor.at - start;
                if (read === 0) return true;
                return put(found, 'fraction', digits * 10 ** (7 - read));
            }
            case 't': {
                const am = culture.am;
                const pm = culture.pm;
                let isPm: boolean;
                if (count === 1) {
                    const c = cursor.text.charAt(cursor.at);
                    if (am !== '' && c === am.charAt(0)) isPm = false;
                    else if (pm !== '' && c === pm.charAt(0)) isPm = true;
                    else return false;
                    cursor.at++;
                } else {
                    const index = cursor.name(this.#periods, culture.locale);
                    if (index < 0) return false;
                    isPm = index === 1;
                }
                // Read twice, the designator must read alike, as .NET's CheckNewValue holds it.
                return put(found, 'pm', isPm);
            }
            case 'z': {
                const offset = cursor.offset(count);
                return offset !== undefined && put(found, 'offset', offset);
            }
            case 'K': {
                if (cursor.match('Z')) return put(found, 'offset', 0);
                const sign = cursor.text.charAt(cursor.at);
                if (sign !== '+' && sign !== '-') return true;
                const offset = cursor.offset(3);
                return offset !== undefined && put(found, 'offset', offset);
            }
            default:
                throw new Error(`Internal: '${letter}' is not a specifier a format is tokenized into.`);
        }
    }

    #ticks(found: Found): bigint | undefined {
        const culture = this.#culture;
        let hour = found.hour === undefined ? 0 : found.hour;
        if (this.#twelveHour) {
            if (hour > 12) return undefined;
            if (found.pm === true) hour = hour === 12 ? 12 : hour + 12;
            else if (hour === 12) hour = 0;
        } else if (found.pm !== undefined && (found.pm ? hour < 12 : hour >= 12)) {
            return undefined;
        }
        const calendarYear = found.year === undefined ? 1 + culture.yearOffset : found.year;
        const year = calendarYear - culture.yearOffset;
        const month = found.month === undefined ? 1 : found.month;
        const day = found.day === undefined ? 1 : found.day;
        const minute = found.minute === undefined ? 0 : found.minute;
        const second = found.second === undefined ? 0 : found.second;
        if (year < 1 || year > 9999 || month < 1 || month > 12 || day < 1 || day > daysIn(year, month)
            || hour > 23 || minute > 59 || second > 59) {
            return undefined;
        }
        const ticks = ticksOf(year, month, day, hour, minute, second, found.fraction === undefined ? 0 : found.fraction);
        if (found.dayOfWeek !== undefined && Number((ticks / 864_000_000_000n + 1n) % 7n) !== found.dayOfWeek) return undefined;
        if (found.offset !== undefined) {
            // An offset beyond fourteen hours is no offset .NET reads, and the instant it names must be one a
            // date holds, though the clock it shows is what is kept.
            if (Math.abs(found.offset) > 14 * 60) return undefined;
            const utc = ticks - BigInt(found.offset) * 600_000_000n;
            if (utc < 0n || utc > maxTicks) return undefined;
        }
        return ticks;
    }
}
