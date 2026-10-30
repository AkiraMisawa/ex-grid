// .NET's number formatting, as `double.ToString(format, culture)` and `decimal.ToString(format, culture)`
// write a number (ADR-0060: a Value Field's number format and a field's format are .NET format strings,
// and a report written in TypeScript must show what the C# engine shows). It is .NET's own algorithm,
// transcribed: the digits of the number in a buffer, rounded where the format says, then laid out by the
// standard format's pattern or the custom format's sections. dotnet/tools/pivot-oracle holds it to .NET.
//
// A double's digits are its exact binary value's: correctly rounded where a standard format names its
// precision, the shortest that reads back where it names none, and fifteen significant digits, rounded
// again half up, under a custom format, as .NET does. A decimal's are its own, rounded half away from zero.

import type { SnapshotDecimal } from '@ex-grid/data';
import type { PivotNumber } from '../cube/answer';
import { cultureOf, type NumberCulture } from '../culture/culture-data';
import { PivotFormatError } from '../errors';

/** The digits of a number: `0.d1d2d3… × 10^scale`, as .NET's NumberBuffer holds them. */
interface NumberBuffer {
    digits: string;
    scale: number;
    negative: boolean;
    readonly floating: boolean;
}

const badFormat = 'Format specifier was invalid.';

/** `value.ToString(format, culture)` for a C# double. */
export function formatDouble(value: number, format: string | undefined = '', culture: NumberCulture): string {
    const text = format;
    if (!Number.isFinite(value)) {
        if (Number.isNaN(value)) return culture.nan;
        return value < 0 ? culture.negativeInfinity : culture.positiveInfinity;
    }
    const specifier = parseSpecifier(text);
    const negative = value < 0 || Object.is(value, -0);
    if (specifier.letter === undefined) {
        // A custom format: fifteen significant digits, rounded again where the format puts its last.
        const buffer = significantDigits(Math.abs(value), 15, negative);
        return customFormat(buffer, text, culture);
    }
    let precision = specifier.precision;
    let significant: boolean;
    const letter = specifier.letter.toUpperCase();
    switch (letter) {
        case 'C':
            if (precision === -1) precision = culture.currencyDigits;
            significant = false;
            break;
        case 'E':
            if (precision === -1) precision = 6;
            precision++;
            significant = true;
            break;
        case 'F':
        case 'N':
            if (precision === -1) precision = culture.digits;
            significant = false;
            break;
        case 'G':
            if (precision === 0) precision = -1;
            significant = true;
            break;
        case 'P':
            if (precision === -1) precision = culture.percentDigits;
            precision += 2;
            significant = false;
            break;
        case 'R':
            precision = -1;
            significant = true;
            break;
        default:
            throw new PivotFormatError(badFormat);
    }
    let maxDigits = specifier.precision;
    const magnitude = Math.abs(value);
    let buffer: NumberBuffer;
    if (precision === -1) {
        buffer = shortestDigits(magnitude, negative);
        maxDigits = Math.max(buffer.digits.length, 17);
    } else if (significant) {
        buffer = significantDigits(magnitude, precision, negative);
    } else {
        buffer = fixedDigits(magnitude, precision, negative);
    }
    return standardFormat(buffer, specifier.letter, maxDigits, culture);
}

/** `value.ToString(format, culture)` for a C# decimal, `mantissa` × 10^-`scale`, trailing zeros and all. */
export function formatDecimal(value: SnapshotDecimal, format: string | undefined = '', culture: NumberCulture): string {
    const text = format;
    const negative = value.mantissa < 0n;
    const magnitude = negative ? -value.mantissa : value.mantissa;
    const digits = magnitude === 0n ? '' : magnitude.toString();
    const buffer: NumberBuffer = { digits, scale: digits.length - value.scale, negative, floating: false };
    const specifier = parseSpecifier(text);
    if (specifier.letter === undefined) return customFormat(buffer, text, culture);
    return standardFormat(buffer, specifier.letter, specifier.precision, culture);
}

// ---- The specifier ----------------------------------------------------------------------------------

interface Specifier {
    /** The standard format's letter, or undefined for a custom format. */
    readonly letter: string | undefined;
    /** Its precision, or -1 where it names none. */
    readonly precision: number;
}

/** .NET's ParseFormatSpecifier: a letter and up to nine digits is a standard format; an empty one is G. */
function parseSpecifier(format: string): Specifier {
    if (format.length === 0) return { letter: 'G', precision: -1 };
    const first = format.charCodeAt(0);
    if (first === 0) return { letter: 'G', precision: -1 };
    if (isAsciiLetter(first)) {
        if (format.length === 1) return { letter: format.charAt(0), precision: -1 };
        let n = 0;
        let i = 1;
        while (i < format.length && isAsciiDigit(format.charCodeAt(i))) {
            if (n >= 100_000_000) throw new PivotFormatError(badFormat);
            n = n * 10 + format.charCodeAt(i) - 48;
            i++;
        }
        if (i >= format.length || format.charCodeAt(i) === 0) return { letter: format.charAt(0), precision: n };
    }
    return { letter: undefined, precision: -1 };
}

function isAsciiLetter(code: number): boolean {
    return (code >= 65 && code <= 90) || (code >= 97 && code <= 122);
}

function isAsciiDigit(code: number): boolean {
    return code >= 48 && code <= 57;
}

// ---- A double's digits ------------------------------------------------------------------------------

let powersOfFive: bigint[] | undefined;

function fiveTo(power: number): bigint {
    powersOfFive ??= [1n];
    while (powersOfFive.length <= power) {
        const last = powersOfFive[powersOfFive.length - 1];
        if (last === undefined) throw new Error('Internal: the powers of five are empty.');
        powersOfFive.push(last * 5n);
    }
    const five = powersOfFive[power];
    if (five === undefined) throw new Error(`Internal: 5^${power} is missing.`);
    return five;
}

/** The exact decimal digits of a finite, non-negative double, without trailing zeros, and its scale. */
function exactDigits(magnitude: number): { digits: string; scale: number } {
    if (magnitude === 0) return { digits: '', scale: 0 };
    const bits = new DataView(new ArrayBuffer(8));
    bits.setFloat64(0, magnitude);
    const word = bits.getBigUint64(0);
    const biased = Number(word >> 52n);
    const fraction = word & ((1n << 52n) - 1n);
    const mantissa = biased === 0 ? fraction : fraction | (1n << 52n);
    const exponent = (biased === 0 ? 1 : biased) - 1075;
    let digits: string;
    let scale: number;
    if (exponent >= 0) {
        digits = (mantissa << BigInt(exponent)).toString();
        scale = digits.length;
    } else {
        digits = (mantissa * fiveTo(-exponent)).toString();
        scale = digits.length + exponent;
    }
    return { digits: digits.replace(/0+$/, ''), scale };
}

/** The shortest digits that read back as the double, as its round-trip text writes them. */
function shortestDigits(magnitude: number, negative: boolean): NumberBuffer {
    if (magnitude === 0) return { digits: '', scale: 0, negative, floating: true };
    const [mantissa = '', exponent = ''] = magnitude.toExponential().split('e');
    const digits = mantissa.replace('.', '').replace(/0+$/, '');
    return { digits, scale: Number(exponent) + 1, negative, floating: true };
}

/** The double's exact value rounded to `count` significant digits, a tie to the even digit. */
function significantDigits(magnitude: number, count: number, negative: boolean): NumberBuffer {
    const exact = exactDigits(magnitude);
    const rounded = roundExact(exact.digits, exact.scale, count);
    return { digits: rounded.digits, scale: rounded.scale, negative, floating: true };
}

/** The double's exact value rounded to `places` decimal places, a tie to the even digit. */
function fixedDigits(magnitude: number, places: number, negative: boolean): NumberBuffer {
    const exact = exactDigits(magnitude);
    const rounded = roundExact(exact.digits, exact.scale, exact.scale + places);
    return { digits: rounded.digits, scale: rounded.scale, negative, floating: true };
}

/** Exact digits cut to `keep`, rounded to the nearest and a tie to the even digit, without trailing
 * zeros. Rounded to nothing, the digits are empty at scale 0. */
function roundExact(digits: string, scale: number, keep: number): { digits: string; scale: number } {
    if (digits.length === 0) return { digits: '', scale: 0 };
    if (keep >= digits.length) return { digits, scale };
    if (keep < 0) return { digits: '', scale: 0 };
    const kept = digits.slice(0, keep);
    const next = digits.charCodeAt(keep) - 48;
    const rest = digits.length > keep + 1;
    const lastKept = keep === 0 ? 0 : kept.charCodeAt(keep - 1) - 48;
    const up = next > 5 || (next === 5 && (rest || lastKept % 2 === 1));
    if (!up) {
        const trimmed = kept.replace(/0+$/, '');
        return trimmed.length === 0 ? { digits: '', scale: 0 } : { digits: trimmed, scale };
    }
    return incremented(kept, scale);
}

/** `kept`, a run of digits at `scale`, plus one in its last place. */
function incremented(kept: string, scale: number): { digits: string; scale: number } {
    let i = kept.length;
    while (i > 0 && kept.charCodeAt(i - 1) === 57) i--;
    if (i === 0) return { digits: '1', scale: scale + 1 };
    return { digits: kept.slice(0, i - 1) + String.fromCharCode(kept.charCodeAt(i - 1) + 1), scale };
}

// ---- Rounding the buffer ------------------------------------------------------------------------------

/** .NET's RoundNumber: keeps `pos` digits, rounding half up unless the digits were already rounded
 * correctly; trailing zeros come off, and a number rounded to nothing is 0 at scale 0, a decimal's
 * sign dropped with it. */
function roundNumber(buffer: NumberBuffer, pos: number, correctlyRounded: boolean): void {
    const digits = buffer.digits;
    let i = 0;
    while (i < pos && i < digits.length) i++;
    let result: string;
    if (i === pos && i < digits.length && !correctlyRounded && digits.charCodeAt(i) >= 53) {
        while (i > 0 && digits.charCodeAt(i - 1) === 57) i--;
        if (i > 0) {
            result = digits.slice(0, i - 1) + String.fromCharCode(digits.charCodeAt(i - 1) + 1);
        } else {
            buffer.scale++;
            result = '1';
        }
    } else {
        while (i > 0 && digits.charCodeAt(i - 1) === 48) i--;
        result = digits.slice(0, i);
    }
    if (result.length === 0) {
        if (!buffer.floating) buffer.negative = false;
        buffer.scale = 0;
    }
    buffer.digits = result;
}

// ---- Standard formats ---------------------------------------------------------------------------------

const negativeNumberPatterns = ['(#)', '-#', '- #', '#-', '# -'];
const positiveCurrencyPatterns = ['$#', '#$', '$ #', '# $'];
const negativeCurrencyPatterns = [
    '($#)', '-$#', '$-#', '$#-', '(#$)', '-#$', '#-$', '#$-', '-# $', '-$ #', '# $-', '$ #-', '$ -#', '#- $',
    '($ #)', '(# $)', '$- #',
];
const positivePercentPatterns = ['# %', '#%', '%#', '% #'];
const negativePercentPatterns = ['-# %', '-#%', '-%#', '%-#', '%#-', '#-%', '#%-', '-% #', '# %-', '% #-', '% -#', '#- %'];

function pattern(list: readonly string[], index: number): string {
    const chosen = list[index];
    if (chosen === undefined) throw new Error(`Internal: the culture names pattern ${index}, which .NET does not have.`);
    return chosen;
}

/** .NET's NumberToString for a double's or a decimal's buffer. */
function standardFormat(buffer: NumberBuffer, letter: string, precision: number, culture: NumberCulture): string {
    const correctlyRounded = buffer.floating;
    let maxDigits = precision;
    switch (letter) {
        case 'C':
        case 'c': {
            if (maxDigits < 0) maxDigits = culture.currencyDigits;
            roundNumber(buffer, buffer.scale + maxDigits, correctlyRounded);
            const layout = buffer.negative
                ? pattern(negativeCurrencyPatterns, culture.currencyNegativePattern)
                : pattern(positiveCurrencyPatterns, culture.currencyPositivePattern);
            return laidOut(layout, buffer, maxDigits, culture.currencyGroupSizes, culture.currencyDecimal,
                culture.currencyGroup, culture);
        }
        case 'F':
        case 'f':
            if (maxDigits < 0) maxDigits = culture.digits;
            roundNumber(buffer, buffer.scale + maxDigits, correctlyRounded);
            return (buffer.negative ? culture.negativeSign : '')
                + fixed(buffer, maxDigits, undefined, culture.decimal, '');
        case 'N':
        case 'n': {
            if (maxDigits < 0) maxDigits = culture.digits;
            roundNumber(buffer, buffer.scale + maxDigits, correctlyRounded);
            const layout = buffer.negative ? pattern(negativeNumberPatterns, culture.negativePattern) : '#';
            return laidOut(layout, buffer, maxDigits, culture.groupSizes, culture.decimal, culture.group, culture);
        }
        case 'E':
        case 'e':
            if (maxDigits < 0) maxDigits = 6;
            maxDigits++;
            roundNumber(buffer, maxDigits, correctlyRounded);
            return (buffer.negative ? culture.negativeSign : '') + scientific(buffer, maxDigits, culture, letter);
        case 'G':
        case 'g':
        case 'R':
        case 'r': {
            const exponent = letter === 'G' || letter === 'R' ? 'E' : 'e';
            if (maxDigits < 1) {
                if (!buffer.floating && maxDigits === -1) {
                    // A decimal in G keeps its trailing zeros, as significant, and its own digits.
                    const sign = buffer.negative && buffer.digits.length > 0 ? culture.negativeSign : '';
                    return sign + general(buffer, maxDigits, culture, exponent, true);
                }
                maxDigits = buffer.digits.length;
            }
            roundNumber(buffer, maxDigits, correctlyRounded);
            return (buffer.negative ? culture.negativeSign : '') + general(buffer, maxDigits, culture, exponent, false);
        }
        case 'P':
        case 'p': {
            if (maxDigits < 0) maxDigits = culture.percentDigits;
            buffer.scale += 2;
            roundNumber(buffer, buffer.scale + maxDigits, correctlyRounded);
            const layout = buffer.negative
                ? pattern(negativePercentPatterns, culture.percentNegativePattern)
                : pattern(positivePercentPatterns, culture.percentPositivePattern);
            return laidOut(layout, buffer, maxDigits, culture.percentGroupSizes, culture.percentDecimal,
                culture.percentGroup, culture);
        }
        default:
            throw new PivotFormatError(badFormat);
    }
}

/** A standard pattern: `#` the grouped number, `-` the culture's negative sign, `$` its currency symbol and
 * `%` its percent symbol; anything else as it stands. */
function laidOut(layout: string, buffer: NumberBuffer, maxDigits: number, groupSizes: readonly number[], decimal: string,
    group: string, culture: NumberCulture): string {
    let text = '';
    for (const ch of layout) {
        switch (ch) {
            case '#': text += fixed(buffer, maxDigits, groupSizes, decimal, group); break;
            case '-': text += culture.negativeSign; break;
            case '$': text += culture.currencySymbol; break;
            case '%': text += culture.percentSymbol; break;
            default: text += ch; break;
        }
    }
    return text;
}

/** .NET's FormatFixed: the integer digits, grouped from the right when sizes are given, then `maxDigits`
 * places. */
function fixed(buffer: NumberBuffer, maxDigits: number, groupSizes: readonly number[] | undefined, decimal: string,
    group: string): string {
    const digits = buffer.digits;
    let digPos = buffer.scale;
    let next = 0;
    let text = '';
    if (digPos > 0) {
        let whole = '';
        for (let i = 0; i < digPos; i++) whole += i < digits.length ? digits.charAt(i) : '0';
        next = Math.min(digPos, digits.length);
        text = groupSizes === undefined ? whole : grouped(whole, groupSizes, group);
    } else {
        text = '0';
    }
    if (maxDigits > 0) {
        text += decimal;
        let places = maxDigits;
        if (digPos < 0) {
            const zeroes = Math.min(-digPos, places);
            text += '0'.repeat(zeroes);
            digPos += zeroes;
            places -= zeroes;
        }
        for (; places > 0; places--) {
            text += next < digits.length ? digits.charAt(next) : '0';
            next++;
        }
    }
    return text;
}

/** Whole digits with the group separator put in from the right: the first size, then each next, the last
 * repeating; a size of 0 ends the grouping. */
function grouped(whole: string, sizes: readonly number[], separator: string): string {
    const first = sizes[0];
    if (first === undefined || first === 0) return whole;
    const parts: string[] = [];
    let end = whole.length;
    let index = 0;
    let size = first;
    while (size > 0 && end > size) {
        parts.unshift(whole.slice(end - size, end));
        end -= size;
        if (index < sizes.length - 1) {
            index++;
            size = sizes[index] ?? 0; // oxlint-disable-line ex-grid/no-stand-in-default -- the index was checked against the length above
        }
    }
    parts.unshift(whole.slice(0, end));
    return parts.join(separator);
}

/** .NET's FormatScientific: one digit, the point unless only one is asked for, the rest, and an exponent
 * of at least three digits with its sign. */
function scientific(buffer: NumberBuffer, maxDigits: number, culture: NumberCulture, letter: string): string {
    const digits = buffer.digits;
    let text = digits.length > 0 ? digits.charAt(0) : '0';
    if (maxDigits !== 1) text += culture.decimal;
    for (let i = 1; i < maxDigits; i++) text += i < digits.length ? digits.charAt(i) : '0';
    const exponent = digits.length === 0 ? 0 : buffer.scale - 1;
    return text + exponentText(exponent, letter, 3, true, culture);
}

/** .NET's FormatGeneral: fixed while the scale lies within the digits asked for, and from -3, else
 * scientific with an exponent of at least two digits. */
function general(buffer: NumberBuffer, maxDigits: number, culture: NumberCulture, letter: string,
    suppressScientific: boolean): string {
    const digits = buffer.digits;
    let digPos = buffer.scale;
    let isScientific = false;
    if (!suppressScientific && (digPos > maxDigits || digPos < -3)) {
        digPos = 1;
        isScientific = true;
    }
    let next = 0;
    let text = '';
    if (digPos > 0) {
        for (; digPos > 0; digPos--) {
            text += next < digits.length ? digits.charAt(next) : '0';
            next++;
        }
    } else {
        text = '0';
    }
    if (next < digits.length || digPos < 0) {
        text += culture.decimal;
        for (; digPos < 0; digPos++) text += '0';
        text += digits.slice(next);
    }
    if (isScientific) text += exponentText(buffer.scale - 1, letter, 2, true, culture);
    return text;
}

/** .NET's FormatExponent. */
function exponentText(value: number, letter: string, minDigits: number, positiveSign: boolean, culture: NumberCulture): string {
    let text = letter;
    if (value < 0) text += culture.negativeSign;
    else if (positiveSign) text += culture.positiveSign;
    return text + Math.abs(value).toString().padStart(minDigits, '0');
}

// ---- Custom formats -------------------------------------------------------------------------------------

/** .NET's FindSection: where section `section` (0 positive, 1 negative, 2 zero) starts, or 0 when the
 * format has none. */
function findSection(format: string, section: number): number {
    if (section === 0) return 0;
    let remaining = section;
    let src = 0;
    for (;;) {
        if (src >= format.length) return 0;
        const ch = format.charAt(src++);
        switch (ch) {
            case '\'':
            case '"':
                while (src < format.length && format.charCodeAt(src) !== 0 && format.charAt(src++) !== ch) { /* the quoted text is skipped */ }
                break;
            case '\\':
                if (src < format.length && format.charCodeAt(src) !== 0) src++;
                break;
            case ';':
                if (--remaining !== 0) break;
                if (src < format.length && format.charCodeAt(src) !== 0 && format.charAt(src) !== ';') return src;
                return 0;
            case '\0':
                return 0;
            default:
                break;
        }
    }
}

/** .NET's NumberToStringFormat: a custom format's sections, placeholders, grouping and scaling. */
function customFormat(buffer: NumberBuffer, format: string, culture: NumberCulture): string {
    let section = findSection(format, buffer.digits.length === 0 ? 2 : buffer.negative ? 1 : 0);
    let digitCount: number;
    let decimalPos: number;
    let firstDigit: number;
    let lastDigit: number;
    let isScientific: boolean;
    let thousandPos: number;
    let thousandCount = 0;
    let thousandSeps: boolean;
    let scaleAdjust: number;
    let src: number;

    for (;;) {
        digitCount = 0;
        decimalPos = -1;
        firstDigit = 0x7FFFFFFF;
        lastDigit = 0;
        isScientific = false;
        thousandPos = -1;
        thousandSeps = false;
        scaleAdjust = 0;
        src = section;
        while (src < format.length) {
            const ch = format.charAt(src++);
            if (ch === '\0' || ch === ';') break;
            switch (ch) {
                case '#':
                    digitCount++;
                    break;
                case '0':
                    if (firstDigit === 0x7FFFFFFF) firstDigit = digitCount;
                    digitCount++;
                    lastDigit = digitCount;
                    break;
                case '.':
                    if (decimalPos < 0) decimalPos = digitCount;
                    break;
                case ',':
                    if (digitCount > 0 && decimalPos < 0) {
                        if (thousandPos >= 0) {
                            if (thousandPos === digitCount) {
                                thousandCount++;
                                break;
                            }
                            thousandSeps = true;
                        }
                        thousandPos = digitCount;
                        thousandCount = 1;
                    }
                    break;
                case '%':
                    scaleAdjust += 2;
                    break;
                case '‰':
                    scaleAdjust += 3;
                    break;
                case '\'':
                case '"':
                    while (src < format.length && format.charCodeAt(src) !== 0 && format.charAt(src++) !== ch) { /* the quoted text is skipped */ }
                    break;
                case '\\':
                    if (src < format.length && format.charCodeAt(src) !== 0) src++;
                    break;
                case 'E':
                case 'e':
                    if ((src < format.length && format.charAt(src) === '0')
                        || (src + 1 < format.length && (format.charAt(src) === '+' || format.charAt(src) === '-')
                            && format.charAt(src + 1) === '0')) {
                        while (++src < format.length && format.charAt(src) === '0') { /* the exponent's zeros */ }
                        isScientific = true;
                    }
                    break;
                default:
                    break;
            }
        }
        if (decimalPos < 0) decimalPos = digitCount;
        if (thousandPos >= 0) {
            if (thousandPos === decimalPos) scaleAdjust -= thousandCount * 3;
            else thousandSeps = true;
        }
        if (buffer.digits.length > 0) {
            buffer.scale += scaleAdjust;
            const pos = isScientific ? digitCount : buffer.scale + digitCount - decimalPos;
            roundNumber(buffer, pos, false);
            if (buffer.digits.length === 0) {
                const zero = findSection(format, 2);
                if (zero !== section) {
                    section = zero;
                    continue;
                }
            }
        } else {
            if (!buffer.floating) buffer.negative = false;
            buffer.scale = 0;
        }
        break;
    }

    firstDigit = firstDigit < decimalPos ? decimalPos - firstDigit : 0;
    lastDigit = lastDigit > decimalPos ? decimalPos - lastDigit : 0;
    let digPos: number;
    let adjust: number;
    if (isScientific) {
        digPos = decimalPos;
        adjust = 0;
    } else {
        digPos = buffer.scale > decimalPos ? buffer.scale : decimalPos;
        adjust = buffer.scale - decimalPos;
    }
    src = section;

    const separators: number[] = [];
    if (thousandSeps && culture.group.length > 0) {
        const sizes = culture.groupSizes;
        let index = 0;
        let total = sizes.length === 0 ? 0 : sizes[0] ?? 0; // oxlint-disable-line ex-grid/no-stand-in-default -- the length was checked on the same line
        let size = total;
        const totalDigits = digPos + (adjust < 0 ? adjust : 0);
        const numDigits = firstDigit > totalDigits ? firstDigit : totalDigits;
        while (numDigits > total) {
            if (size === 0) break;
            separators.push(total);
            if (index < sizes.length - 1) {
                index++;
                size = sizes[index] ?? 0; // oxlint-disable-line ex-grid/no-stand-in-default -- the index was checked against the length above
            }
            total += size;
        }
    }
    let separator = separators.length - 1;

    const digits = buffer.digits;
    let cur = 0;
    let text = '';
    if (buffer.negative && section === 0 && buffer.scale !== 0) text += culture.negativeSign;
    let decimalWritten = false;
    const groupAfter = (): void => {
        if (thousandSeps && digPos > 1 && separator >= 0 && digPos === (separators[separator] ?? -1) + 1) { // oxlint-disable-line ex-grid/no-stand-in-default -- the index was checked on the same line
            text += culture.group;
            separator--;
        }
    };

    while (src < format.length) {
        let ch = format.charAt(src++);
        if (ch === '\0' || ch === ';') break;
        if (adjust > 0 && (ch === '#' || ch === '0' || ch === '.')) {
            while (adjust > 0) {
                text += cur < digits.length ? digits.charAt(cur++) : '0';
                groupAfter();
                digPos--;
                adjust--;
            }
        }
        switch (ch) {
            case '#':
            case '0': {
                if (adjust < 0) {
                    adjust++;
                    ch = digPos <= firstDigit ? '0' : '';
                } else {
                    ch = cur < digits.length ? digits.charAt(cur++) : digPos > lastDigit ? '0' : '';
                }
                if (ch !== '') {
                    text += ch;
                    groupAfter();
                }
                digPos--;
                break;
            }
            case '.':
                if (digPos !== 0 || decimalWritten) break;
                if (lastDigit < 0 || (decimalPos < digitCount && cur < digits.length)) {
                    text += culture.decimal;
                    decimalWritten = true;
                }
                break;
            case '‰':
                text += culture.perMilleSymbol;
                break;
            case '%':
                text += culture.percentSymbol;
                break;
            case ',':
                break;
            case '\'':
            case '"':
                while (src < format.length && format.charCodeAt(src) !== 0 && format.charAt(src) !== ch) text += format.charAt(src++);
                if (src < format.length && format.charCodeAt(src) !== 0) src++;
                break;
            case '\\':
                if (src < format.length && format.charCodeAt(src) !== 0) text += format.charAt(src++);
                break;
            case 'E':
            case 'e': {
                let positiveSign = false;
                let zeros = 0;
                if (isScientific) {
                    if (src < format.length && format.charAt(src) === '0') {
                        zeros++;
                    } else if (src + 1 < format.length && format.charAt(src) === '+' && format.charAt(src + 1) === '0') {
                        positiveSign = true;
                    } else if (src + 1 < format.length && format.charAt(src) === '-' && format.charAt(src + 1) === '0') {
                        // E-0 writes the sign only when the exponent is negative.
                    } else {
                        text += ch;
                        break;
                    }
                    while (++src < format.length && format.charAt(src) === '0') zeros++;
                    if (zeros > 10) zeros = 10;
                    const exponent = digits.length === 0 ? 0 : buffer.scale - decimalPos;
                    text += exponentText(exponent, ch, zeros, positiveSign, culture);
                    isScientific = false;
                } else {
                    text += ch;
                    if (src < format.length) {
                        if (format.charAt(src) === '+' || format.charAt(src) === '-') text += format.charAt(src++);
                        while (src < format.length && format.charAt(src) === '0') text += format.charAt(src++);
                    }
                }
                break;
            }
            default:
                text += ch;
                break;
        }
    }
    if (buffer.negative && section === 0 && buffer.scale === 0 && text.length > 0) text = culture.negativeSign + text;
    return text;
}

/**
 * A count written as the report writes a whole number in `locale`, a BCP 47 tag or `invariant`, with its
 * group separators: `1,020,000` in en-US, `1.020.000` in de-DE, as C#'s `count.ToString("N0", culture)`
 * writes it. The Field List writes a field's count of Items with it, and the leaf cap's refusal its figures
 * (ADR-0370 section 3). A count that is not a whole number of at least 0 is refused.
 */
export function formatPivotCount(count: number, locale: string): string {
    if (!Number.isSafeInteger(count) || count < 0) throw new RangeError(`A count is a whole number of at least 0, not ${count}.`);
    return formatDouble(count, 'N0', cultureOf(locale).data.number);
}

/**
 * A number written in a number format in `locale`, as C#'s `value.ToString(format, culture)` writes it: what a
 * Value Field's number format makes of a figure, which Value Field Settings… shows as its sample (ADR-0061).
 * A format .NET cannot format a number with throws a `PivotFormatError`; `pivotNumberFormatProblem` says so
 * first.
 */
export function formatPivotNumber(value: number, format: string, locale: string): string {
    return formatDouble(value, format, cultureOf(locale).data.number);
}

/**
 * A figure written in a number format in `locale`, as C#'s `ToString(format, culture)` writes the decimal or the
 * double it is: a Selection Summary's figure in the Focus value's format, as the report shows that value (ADR-0130).
 * A format .NET cannot format a number with throws a `PivotFormatError`.
 */
export function formatPivotFigure(value: PivotNumber, format: string, locale: string): string {
    const culture = cultureOf(locale).data.number;
    return value.kind === 'exact' ? formatDecimal(value.value, format, culture) : formatDouble(value.value, format, culture);
}
