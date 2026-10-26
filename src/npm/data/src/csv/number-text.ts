// Numbers read from a CSV's text by hand (ADR-0064), as ExGrid.Data's NumberText reads them: a sign, digits —
// with the thousands separator only between groups of the column's sizes — and the decimal point followed by
// digits. Nothing else is a number: no currency, no percent, no exponent but a Double's. Trailing zeros after
// the point are not places, so `1.50` is 15 at one place.

import { numberCulture } from './culture';
import { pointOf, thousandsOf, type PlannedColumn } from './schema';

/** How one column reads a number: its decimal point, its thousands separator and the sizes of the groups
 * it separates, and the culture's signs besides the hyphen-minus and the plus, which are always read. */
export interface NumberReading {
    readonly point: string;
    /** The thousands separator, or `''` for none. */
    readonly thousands: string;
    readonly groupSizes: readonly number[];
    /** The culture's negative sign when it is not a hyphen-minus, or `''`. */
    readonly minus: string;
    /** The culture's positive sign when it is not a plus, or `''`. */
    readonly plus: string;
    /** Whether a number is written as .NET's invariant culture writes one, without a thousands separator, so
     * that a Double is read from the field's own text. */
    readonly plain: boolean;
}

export function numberReading(column: PlannedColumn): NumberReading {
    const culture = column.culture === undefined ? undefined : numberCulture(column.culture);
    const point = pointOf(column);
    const thousands = thousandsOf(column);
    const minus = culture === undefined || culture.negativeSign === '-' ? '' : culture.negativeSign;
    const plus = culture === undefined || culture.positiveSign === '+' ? '' : culture.positiveSign;
    return Object.freeze({
        point,
        thousands,
        groupSizes: culture === undefined ? [3] : culture.groupSizes,
        minus,
        plus,
        plain: point === '.' && thousands === '' && minus === '' && plus === '',
    });
}

/** A number read: ± its digits, `scale` of them after the point, the trailing zeros there not counted. */
export interface ParsedNumber {
    readonly status: 'ok';
    readonly negative: boolean;
    /** The digits, without leading zeros; `''` for zero. */
    readonly digits: string;
    readonly scale: number;
    readonly hasPoint: boolean;
}

/** A number past the 128 bits ExGrid.Data reads one into, where it gives up: digits C# holds no kind of. */
export interface TooLong {
    readonly status: 'tooLong';
    /** Whether the decimal point had been read when the digits ran out of room. */
    readonly hasPoint: boolean;
}

const notANumber = Object.freeze({ status: 'notANumber' } as const);

/** The largest magnitude C#'s reader pushes another digit onto: (2^128 − 1 − 9) / 10. */
const pushLimit = ((1n << 128n) - 1n - 9n) / 10n;

/** The digits accumulated so far, which give up once one more would pass 128 bits, as C#'s Push does. */
class Digits {
    text = '';

    push(digit: string): boolean {
        if (this.text === '' && digit === '0') return true;
        // Thirty-eight digits are the first that can pass the limit; fewer never reach it.
        if (this.text.length >= 38 && BigInt(this.text) > pushLimit) return false;
        this.text += digit;
        return true;
    }
}

/** Where the digits begin after a sign — a hyphen-minus or a plus, or the culture's own — and whether it is negative. */
function sign(s: string, reading: NumberReading): { readonly at: number; readonly negative: boolean } {
    if (s.startsWith('-')) return { at: 1, negative: true };
    if (s.startsWith('+')) return { at: 1, negative: false };
    if (reading.minus !== '' && s.startsWith(reading.minus)) return { at: reading.minus.length, negative: true };
    if (reading.plus !== '' && s.startsWith(reading.plus)) return { at: reading.plus.length, negative: false };
    return { at: 0, negative: false };
}

function isDigit(s: string, at: number): boolean {
    const code = s.charCodeAt(at);
    return code >= 0x30 && code <= 0x39;
}

/** Whether the groups, left to right, have the sizes a culture writes: the rightmost of the first size, each
 * further left of the next, the last size repeating, and the leftmost of at most its size. */
function grouped(groups: readonly number[], sizes: readonly number[]): boolean {
    let next = 0;
    for (let g = groups.length - 1; g >= 0; g--) {
        const size = sizes[Math.min(next, sizes.length - 1)];
        const group = groups[g];
        if (size === undefined || group === undefined || size <= 0) return false;
        if (g === 0) return group >= 1 && group <= size;
        if (group !== size) return false;
        next++;
    }
    return true;
}

/** A number of at most fifteen significant digits, which a double holds exactly: ± `mantissa` at `scale` places. */
export interface ShortNumber {
    readonly negative: boolean;
    readonly mantissa: number;
    readonly scale: number;
    readonly hasPoint: boolean;
}

/**
 * Reads `s`, already trimmed, as {@link parseNumber} reads it, when it is a sign, digits, and the decimal point
 * followed by digits, with at most fifteen significant digits: most numbers a file holds, read without a
 * `bigint`. `undefined` for any other text, which `parseNumber` then reads, or refuses.
 */
export function parseShort(s: string, reading: NumberReading): ShortNumber | undefined {
    const point = reading.point.length === 1 ? reading.point.charCodeAt(0) : -1;
    let i = 0;
    let negative = false;
    const first = s.charCodeAt(0);
    if (first === 0x2d || first === 0x2b) {
        negative = first === 0x2d;
        i = 1;
    }
    let mantissa = 0;
    let significant = 0;
    let anyDigit = false;
    for (; i < s.length; i++) {
        const digit = s.charCodeAt(i) - 0x30;
        if (digit < 0 || digit > 9) break;
        anyDigit = true;
        if (mantissa === 0 && digit === 0) continue;
        mantissa = mantissa * 10 + digit;
        significant++;
    }
    let scale = 0;
    let hasPoint = false;
    if (i < s.length && s.charCodeAt(i) === point) {
        hasPoint = true;
        let zeros = 0;
        for (i++; i < s.length; i++) {
            const digit = s.charCodeAt(i) - 0x30;
            if (digit < 0 || digit > 9) break;
            anyDigit = true;
            if (digit === 0) {
                zeros++;
                continue;
            }
            // The zeros before a digit after the point are places; those at the end are not.
            for (; zeros > 0; zeros--) {
                mantissa *= 10;
                scale++;
                if (significant > 0) significant++;
            }
            mantissa = mantissa * 10 + digit;
            scale++;
            significant++;
        }
    }
    // Past twenty-eight places no kind holds the number, which `parseNumber` then says.
    if (i !== s.length || !anyDigit || significant > 15 || scale > 28) return undefined;
    return { negative, mantissa, scale, hasPoint };
}

/** Reads `s`, already trimmed, as a fixed-point number under `reading`. */
export function parseNumber(s: string, reading: NumberReading): ParsedNumber | TooLong | typeof notANumber {
    const { at, negative } = sign(s, reading);
    let i = at;
    const digits = new Digits();
    const groups: number[] = [];
    let inGroup = 0;
    let integerDigits = 0;
    const { thousands, point } = reading;
    while (i < s.length) {
        if (isDigit(s, i)) {
            if (!digits.push(s.charAt(i))) return { status: 'tooLong', hasPoint: false };
            inGroup++;
            integerDigits++;
            i++;
            continue;
        }
        if (thousands !== '' && s.startsWith(thousands, i)) {
            if (inGroup === 0 || groups.length === 31) return notANumber;
            groups.push(inGroup);
            inGroup = 0;
            i += thousands.length;
            continue;
        }
        break;
    }
    if (groups.length > 0) {
        if (inGroup === 0) return notANumber;
        groups.push(inGroup);
        if (!grouped(groups, reading.groupSizes)) return notANumber;
    }
    let places = 0;
    let hasPoint = false;
    if (s.startsWith(point, i)) {
        hasPoint = true;
        i += point.length;
        let zeros = 0;
        for (; i < s.length && isDigit(s, i); i++) {
            const digit = s.charAt(i);
            if (digit === '0') {
                zeros++;
                continue;
            }
            for (; zeros > 0; zeros--) {
                if (!digits.push('0')) return { status: 'tooLong', hasPoint: true };
                places++;
            }
            if (!digits.push(digit)) return { status: 'tooLong', hasPoint: true };
            places++;
        }
        // ".00" is zero, written with digits.
        if (zeros > 0 && integerDigits + places === 0) integerDigits = 1;
    }
    if (i !== s.length || integerDigits + places === 0) return notANumber;
    return { status: 'ok', negative, digits: digits.text, scale: places, hasPoint };
}

/**
 * `s`, already trimmed, as .NET's invariant culture writes a floating-point number — a hyphen-minus, digits
 * without separators, a full stop and an exponent — or `undefined` when it is not a number as the column reads
 * one.
 */
export function normalizeDouble(s: string, reading: NumberReading): string | undefined {
    const { at, negative } = sign(s, reading);
    let i = at;
    let out = negative ? '-' : '';
    const groups: number[] = [];
    let inGroup = 0;
    let digits = 0;
    const { thousands, point } = reading;
    while (i < s.length) {
        if (isDigit(s, i)) {
            out += s.charAt(i);
            inGroup++;
            digits++;
            i++;
            continue;
        }
        if (thousands !== '' && s.startsWith(thousands, i)) {
            if (inGroup === 0 || groups.length === 31) return undefined;
            groups.push(inGroup);
            inGroup = 0;
            i += thousands.length;
            continue;
        }
        break;
    }
    if (groups.length > 0) {
        if (inGroup === 0) return undefined;
        groups.push(inGroup);
        if (!grouped(groups, reading.groupSizes)) return undefined;
    }
    if (s.startsWith(point, i)) {
        out += '.';
        i += point.length;
        for (; i < s.length && isDigit(s, i); i++) {
            out += s.charAt(i);
            digits++;
        }
    }
    if (digits === 0) return undefined;
    if (i < s.length && (s.charAt(i) === 'e' || s.charAt(i) === 'E')) {
        out += 'e';
        i++;
        if (s.charAt(i) === '+' || s.charAt(i) === '-') out += s.charAt(i++);
        let exponent = 0;
        for (; i < s.length && isDigit(s, i); i++) {
            out += s.charAt(i);
            exponent++;
        }
        if (exponent === 0) return undefined;
    }
    return i === s.length ? out : undefined;
}

/** A Double as .NET's invariant culture writes one, which `Number` reads as .NET does. */
const plainDouble = /^[+-]?(?:[0-9]+\.?[0-9]*|\.[0-9]+)(?:[eE][+-]?[0-9]+)?$/;

/** `s`, already trimmed, as a double, when the column reads numbers as .NET's invariant culture writes them and
 * `s` is one; `undefined` otherwise, for {@link normalizeDouble} to read, or refuse. */
export function plainNumber(s: string, reading: NumberReading): number | undefined {
    return reading.plain && plainDouble.test(s) ? Number(s) : undefined;
}

/** NaN or an infinity, as .NET's invariant culture reads one past any column's separators, ignoring case. */
export function nonFinite(s: string): number | undefined {
    const match = /^([+-]?)(nan|infinity|∞)$/i.exec(s);
    if (match === null) return undefined;
    const [, signText, word] = match;
    if (word?.toLowerCase() === 'nan') return Number.NaN;
    return signText === '-' ? Number.NEGATIVE_INFINITY : Number.POSITIVE_INFINITY;
}

/** Whether `s` holds a digit, which an infinity read from it then does not stand for. */
export function hasDigit(s: string): boolean {
    return /[0-9]/.test(s);
}
