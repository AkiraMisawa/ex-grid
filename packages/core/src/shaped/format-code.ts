// An Excel number format code in the invariant spelling (ADR-0120), as a grid built from a source's shape
// reads it (ADR-0342): C#'s FormatCode, which ExBoard's port reads a Board's columns with. Each section is
// translated once into a .NET custom format that agrees with Excel's on the subset, and written as .NET
// writes it (`custom-number-format.ts`, `custom-date-format.ts`), so the two cores show one value alike.

import type { SnapshotDecimal } from '@ex-grid/data';
import { decimalPartsOf } from '../query/numbers';
import { customDate } from './custom-date-format';
import { customDecimal, customDouble, type NumberCulture } from './custom-number-format';
import { formatCulture } from './format-culture';

/** What reading a format code answers: the format, or the reason it cannot be read, a phrase that completes
 * "the format code …" (`is empty`, `holds '_' spacing, which the reader does not take`). */
export type FormatCodeReading = { readonly format: FormatCode } | { readonly reason: string };

/** A value a format code writes, as a Snapshot holds it: a Decimal, a double as a `number`, an Integer as a
 * `bigint`, or under a date code a Date's ticks as a `bigint` (C#'s DateTime ticks, 100 ns since 0001-01-01). */
export type FormatCodeValue = SnapshotDecimal | number | bigint;

/**
 * An Excel number format code in the invariant spelling (ADR-0120), as a grid built from a source's shape
 * reads it (ADR-0342): `#,##0.00`, `0.00%`, `0.00E+00`, `yyyy-mm-dd`.
 *
 * The subset read: for numbers, one to three sections (positive; negative; zero) of the digit placeholders
 * `0` and `#`, the decimal point, grouping and scaling commas, `%`, `E+`/`E-` exponents, quoted text, `\`
 * escapes and the symbols Excel shows unquoted; for dates, one section of `y m d h s` and `AM/PM`. A section
 * is chosen by the value's sign before rounding, as Excel chooses it, and a negative value that rounds to
 * zero in the first section shows no minus sign (ExSheet's FMT-063). Anything else (colours and conditions in
 * brackets, `@`, `_`, `*`, `?`, fractions, unquoted letters, fractional seconds) is refused rather than shown
 * some other way. A double is written through C#'s decimal, which keeps its 15 significant digits as Excel
 * does, so 1.005 in `0.00` is `1.01`.
 */
export class FormatCode {
    readonly #code: string;
    readonly #isDate: boolean;
    readonly #sections: readonly string[];

    private constructor(code: string, isDate: boolean, sections: readonly string[]) {
        this.#code = code;
        this.#isDate = isDate;
        this.#sections = sections;
    }

    /** The code as written. */
    get code(): string {
        return this.#code;
    }

    /** Whether the code formats dates; otherwise it formats numbers. */
    get isDate(): boolean {
        return this.#isDate;
    }

    /** Reads `code`, or says why it cannot be read, as a phrase that completes "the format code …". A code
     * that is not text, which only an untyped caller can pass, is refused with a `TypeError`. */
    static tryParse(code: string): FormatCodeReading {
        const given: unknown = code;
        if (typeof given !== 'string') throw new TypeError('A format code is text.');
        if (code.length === 0) return refused('is empty');
        if (code.length > 64) return refused('is longer than 64 characters');
        const split = splitSections(code);
        if (typeof split === 'string') return refused(split);
        const isDate = split.some(hasDateCode);
        if (isDate && split.length > 1) return refused('has more than one section for a date');
        if (split.length > 3) return refused('has more than three sections');
        const translated: string[] = [];
        for (const part of split) {
            const section = isDate ? translateDate(part) : translateNumber(part);
            if (section.reason !== undefined) return refused(section.reason);
            translated.push(section.format);
        }
        return Object.freeze({ format: new FormatCode(code, isDate, Object.freeze(translated)) });
    }

    /**
     * The text of `value` in `locale`, a BCP 47 tag or `invariant` (ADR-0370 section 10): a number for a number
     * code, a date for a date code, whose value is a Date's ticks. A date code given a number, which a checked
     * column never hands it, is refused with a `TypeError` rather than shown as some other text, and ticks
     * outside 0001 to 9999 with a `RangeError`, as is a locale whose calendar is not the Gregorian (a year
     * written in it would not be the year the Date holds), and one with no names in the core's table under a
     * code that names a month, a day, or AM and PM.
     */
    format(value: FormatCodeValue, locale: string): string {
        const culture = formatCulture(locale);
        if (this.#isDate) {
            if (typeof value !== 'bigint') throw new TypeError(`The date code "${this.#code}" writes a Date's ticks, and was given a number.`);
            if (culture.calendar !== 'gregory') {
                throw new RangeError(`The locale '${locale}' writes dates in the ${culture.calendar} calendar; a date code writes the Gregorian `
                    + 'year a Date holds.');
            }
            return customDate(value, this.#section(0), culture);
        }
        if (typeof value === 'bigint') return this.#decimal({ mantissa: value, scale: 0 }, culture.number);
        if (typeof value === 'number') {
            // A double is written through C#'s decimal where one holds it; outside, as the double it is.
            if (value === 0 || (Math.abs(value) >= 1e-13 && Math.abs(value) < 7.9e27)) return this.#decimal(decimalOfDouble(value), culture.number);
            if (Number.isFinite(value)) {
                return value < 0 && this.#sections.length >= 2
                    ? this.#apply(-value, this.#section(1), culture.number)
                    : this.#apply(value, this.#section(0), culture.number);
            }
            if (Number.isNaN(value)) return culture.nan;
            return value > 0 ? culture.positiveInfinity : culture.negativeInfinity;
        }
        return this.#decimal(value, culture.number);
    }

    #section(index: number): string {
        const section = this.#sections[index];
        if (section === undefined) throw new Error(`Internal: section ${index} of "${this.#code}" is missing.`);
        return section;
    }

    #decimal(value: SnapshotDecimal, culture: NumberCulture): string {
        if (value.mantissa < 0n && this.#sections.length >= 2) {
            return this.#apply({ mantissa: -value.mantissa, scale: value.scale }, this.#section(1), culture);
        }
        if (value.mantissa === 0n && this.#sections.length >= 3) return this.#apply({ mantissa: 0n, scale: 0 }, this.#section(2), culture);
        // One section takes the sign itself, and a decimal that rounds to zero drops it, as Excel does.
        return this.#apply(value, this.#section(0), culture);
    }

    // An empty section shows nothing, as Excel shows it; .NET would read "" as "G".
    #apply(value: SnapshotDecimal | number, section: string, culture: NumberCulture): string {
        if (section.length === 0) return '';
        return typeof value === 'number' ? customDouble(value, section, culture) : customDecimal(value, section, culture);
    }
}

function refused(reason: string): FormatCodeReading {
    return Object.freeze({ reason });
}

/** C#'s `(decimal)value`: fifteen significant digits, as .NET's VarDecFromR8 reads a double. */
function decimalOfDouble(value: number): SnapshotDecimal {
    const parts = decimalPartsOf(value);
    if (parts === undefined) throw new Error(`Internal: ${value} lies where a decimal holds it, and none was read.`);
    const digits = BigInt(parts.digits);
    const magnitude = parts.power >= 0 ? { mantissa: digits, scale: parts.power } : { mantissa: digits * 10n ** BigInt(-parts.power), scale: 0 };
    return parts.negative ? { mantissa: -magnitude.mantissa, scale: magnitude.scale } : magnitude;
}

// ---- Reading the code -------------------------------------------------------------------------------

/** The code's sections, split at each `;` outside quotes and escapes, or why it cannot be split. */
function splitSections(code: string): string[] | string {
    const parts: string[] = [];
    let current = '';
    for (let i = 0; i < code.length; i++) {
        const c = code.charAt(i);
        if (c === '"') {
            const end = code.indexOf('"', i + 1);
            if (end < 0) return 'opens a quotation that it does not close';
            current += code.slice(i, end + 1);
            i = end;
        } else if (c === '\\') {
            if (i + 1 >= code.length) return 'ends in an escape with nothing after it';
            current += code.slice(i, i + 2);
            i++;
        } else if (c === ';') {
            parts.push(current);
            current = '';
        } else {
            current += c;
        }
    }
    parts.push(current);
    return parts;
}

/** Walks a section, handing each unquoted character's index to `plain` and each quoted or escaped one to
 * `literal`; false as soon as `plain` refuses. */
function walk(section: string, plain: (at: number) => boolean, literal: (c: string) => void): boolean {
    for (let i = 0; i < section.length; i++) {
        switch (section.charAt(i)) {
            case '"': {
                // A character at a time, as C# walks a string: a character outside the BMP is two.
                const end = section.indexOf('"', i + 1);
                for (let q = i + 1; q < end; q++) literal(section.charAt(q));
                i = end;
                break;
            }
            case '\\':
                i++;
                literal(section.charAt(i));
                break;
            default:
                if (!plain(i)) return false;
                break;
        }
    }
    return true;
}

/** C#'s char.ToLowerInvariant, for the letters a code is read by. */
function lower(c: string): string {
    return c >= 'A' && c <= 'Z' ? c.toLowerCase() : c;
}

function isDateLetter(c: string): boolean {
    return c === 'y' || c === 'm' || c === 'd' || c === 'h' || c === 's';
}

function hasDateCode(section: string): boolean {
    let found = false;
    walk(section, i => {
        found ||= isDateLetter(lower(section.charAt(i)));
        return true;
    }, ignoreLiteral);
    return found;
}

/** A quoted or escaped character, which tells nothing of whether the code formats dates. */
function ignoreLiteral(): void {
    // Text in quotes is shown as it stands; only the unquoted characters say what the code formats.
}

const letter = /^\p{L}$/u;

function refusalOf(c: string): string | undefined {
    switch (c) {
        case '[': return 'holds a part in brackets (a colour, a condition or a locale), which the reader does not take';
        case '@': return 'holds a text placeholder \'@\', which the reader does not take';
        case '_': return 'holds \'_\' spacing, which the reader does not take';
        case '*': return 'holds \'*\' fill, which the reader does not take';
        case '?': return 'holds the placeholder \'?\', which the reader does not take';
        case '/': return 'holds a fraction, which the reader does not take';
        default: return letter.test(c) ? `holds the letter '${c}' unquoted; quote text as "text"` : undefined;
    }
}

/** A section translated, or why it cannot be. */
type Translated = { readonly format: string; readonly reason?: undefined } | { readonly format?: undefined; readonly reason: string };

function translateNumber(section: string): Translated {
    let output = '';
    let reason: string | undefined;
    let points = 0;
    const ok = walk(section, i => {
        const c = section.charAt(i);
        if (c === '.') points++;
        switch (c) {
            case '0':
            case '#':
            case '.':
            case ',':
            case '%':
                output += c;
                return true;
            case 'E':
            case 'e': {
                const sign = section.charAt(i + 1);
                const digit = section.charAt(i + 2);
                if (i + 2 < section.length && (sign === '+' || sign === '-') && (digit === '0' || digit === '#')) {
                    output += c;
                    return true;
                }
                reason = 'holds an \'E\' that is no exponent such as E+00';
                return false;
            }
            default: {
                if ((c === '+' || c === '-') && i > 0 && (section.charAt(i - 1) === 'E' || section.charAt(i - 1) === 'e')) {
                    output += c;
                    return true;
                }
                const why = refusalOf(c);
                if (why !== undefined) {
                    reason = why;
                    return false;
                }
                output += `\\${c}`;
                return true;
            }
        }
    }, c => {
        output += `\\${c}`;
    });
    if (!ok) return { reason: mustSay(reason) };
    if (points > 1) return { reason: 'holds two decimal points in a section' };
    return { format: output };
}

function mustSay(reason: string | undefined): string {
    if (reason === undefined) throw new Error('Internal: a section was refused with no reason.');
    return reason;
}

/** A token of a date section: a code letter and how many times it stands (`t` for AM/PM), or a literal. */
interface DateToken {
    readonly code: string;
    readonly count: number;
    readonly text: string;
}

function startsAmPm(section: string, at: number): boolean {
    return section.slice(at, at + 5).toUpperCase() === 'AM/PM';
}

function isInsideAmPm(section: string, i: number): boolean {
    for (let start = Math.max(0, i - 4); start < i; start++) {
        if (startsAmPm(section, start) && i < start + 5) return true;
    }
    return false;
}

function translateDate(section: string): Translated {
    // Tokens first, so that an 'm' can be read as minutes after an hour or before a second.
    const tokens: DateToken[] = [];
    let reason: string | undefined;
    const last = (): DateToken | undefined => tokens[tokens.length - 1];
    const ok = walk(section, i => {
        const c = section.charAt(i);
        const low = lower(c);
        // The rest of AM/PM, already taken.
        if (last()?.code === 't' && isInsideAmPm(section, i)) return true;
        if (low === 'a' && startsAmPm(section, i)) {
            tokens.push({ code: 't', count: 5, text: '' });
            return true;
        }
        if (isDateLetter(low)) {
            const previous = last();
            if (previous !== undefined && previous.code === low && i > 0 && lower(section.charAt(i - 1)) === low) {
                tokens[tokens.length - 1] = { code: low, count: previous.count + 1, text: '' };
            } else {
                tokens.push({ code: low, count: 1, text: '' });
            }
            return true;
        }
        if (c === '0' && i > 0 && (section.charAt(i - 1) === '.' || section.charAt(i - 1) === '0')) {
            reason = 'holds fractional seconds, which the reader does not take';
            return false;
        }
        const why = refusalOf(c);
        if (why !== undefined && c !== '/') {
            reason = why;
            return false;
        }
        tokens.push({ code: '', count: 0, text: c });
        return true;
    }, c => {
        tokens.push({ code: '', count: 0, text: c });
    });
    if (!ok) return { reason: mustSay(reason) };

    const twelveHour = tokens.some(token => token.code === 't');
    let output = '';
    for (const [k, { code, count, text }] of tokens.entries()) {
        switch (code) {
            case '':
                output += `\\${text}`;
                break;
            case 'y':
                output += count <= 2 ? 'yy' : 'yyyy';
                break;
            case 'd':
                output += 'd'.repeat(Math.min(count, 4));
                break;
            case 'h':
                output += (twelveHour ? 'h' : 'H').repeat(Math.min(count, 2));
                break;
            case 's':
                output += 's'.repeat(Math.min(count, 2));
                break;
            case 't':
                output += 'tt';
                break;
            case 'm':
                if (count > 4) return { reason: 'holds \'mmmmm\', which the reader does not take' };
                output += (isMinute(tokens, k) && count <= 2 ? 'm' : 'M').repeat(count);
                break;
            default:
                throw new Error(`Internal: '${code}' is not a date token.`);
        }
    }
    // A custom format of one character is read by .NET as a standard one ("d" the short date) unless it is
    // marked as custom.
    return { format: output.length === 1 ? `%${output}` : output };
}

/** An 'm' is minutes when the nearest code before it is an hour or the nearest after it a second. */
function isMinute(tokens: readonly DateToken[], at: number): boolean {
    for (let k = at - 1; k >= 0; k--) {
        const token = tokens[k];
        if (token !== undefined && token.code !== '') {
            if (token.code === 'h') return true;
            break;
        }
    }
    for (let k = at + 1; k < tokens.length; k++) {
        const token = tokens[k];
        if (token !== undefined && token.code !== '') return token.code === 's';
    }
    return false;
}
