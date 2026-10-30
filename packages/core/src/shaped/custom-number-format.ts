// .NET's custom numeric format, as `decimal.ToString(format, culture)` and `double.ToString(format, culture)`
// write a number under a custom format string (`#,##0.00`, `0.00E+00`): the only formats a FormatCode hands
// .NET, which translates an Excel code into one (ADR-0342). It is .NET's NumberToStringFormat, transcribed as
// @ex-grid/pivot-engine transcribes it (`text/number-text.ts`, held to .NET by dotnet/tools/pivot-oracle): the digits
// of the number in a buffer, rounded where the format puts its last placeholder, then laid out by the
// section's placeholders. A decimal's digits are its own, rounded half away from zero; a double's are its
// exact binary value rounded to fifteen significant digits, a tie to the even digit, and rounded again half
// up where the format puts its last, as .NET does. The packages share no code, so the core carries its own.

import type { SnapshotDecimal } from '@ex-grid/data';

/** What a custom format reads of a culture: the members of .NET's NumberFormatInfo it writes with. */
export interface NumberCulture {
    /** The decimal separator. */
    readonly decimal: string;
    /** The group separator. */
    readonly group: string;
    /** The sizes of the groups, from the decimal point leftwards, the last repeating; 0 ends the grouping. */
    readonly groupSizes: readonly number[];
    /** The negative sign. */
    readonly negativeSign: string;
    /** The positive sign, which an exponent written `E+` takes. */
    readonly positiveSign: string;
    /** The percent symbol `%` writes. */
    readonly percentSymbol: string;
    /** The per-mille symbol `‰` writes. */
    readonly perMilleSymbol: string;
}

/** The digits of a number: `0.d1d2d3… × 10^scale`, as .NET's NumberBuffer holds them. */
interface NumberBuffer {
    digits: string;
    scale: number;
    negative: boolean;
    readonly floating: boolean;
}

/** `value.ToString(format, culture)` for a C# decimal, `mantissa` × 10^-`scale`, under a custom format. */
export function customDecimal(value: SnapshotDecimal, format: string, culture: NumberCulture): string {
    const negative = value.mantissa < 0n;
    const magnitude = negative ? -value.mantissa : value.mantissa;
    const digits = magnitude === 0n ? '' : magnitude.toString();
    return customFormat({ digits, scale: digits.length - value.scale, negative, floating: false }, format, culture);
}

/** `value.ToString(format, culture)` for a finite C# double under a custom format: fifteen significant
 * digits, rounded again where the format puts its last. */
export function customDouble(value: number, format: string, culture: NumberCulture): string {
    if (!Number.isFinite(value)) throw new Error(`Internal: ${value} is not a finite double a custom format lays out.`);
    const negative = value < 0 || Object.is(value, -0);
    const exact = exactDigits(Math.abs(value));
    const rounded = roundExact(exact.digits, exact.scale, 15);
    return customFormat({ digits: rounded.digits, scale: rounded.scale, negative, floating: true }, format, culture);
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
    return { digits: digits.replace(/0+$/u, ''), scale };
}

/** Exact digits cut to `keep`, rounded to the nearest and a tie to the even digit, without trailing zeros.
 * Rounded to nothing, the digits are empty at scale 0. */
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
        const trimmed = kept.replace(/0+$/u, '');
        return trimmed.length === 0 ? { digits: '', scale: 0 } : { digits: trimmed, scale };
    }
    let i = kept.length;
    while (i > 0 && kept.charCodeAt(i - 1) === 57) i--;
    if (i === 0) return { digits: '1', scale: scale + 1 };
    return { digits: kept.slice(0, i - 1) + String.fromCharCode(kept.charCodeAt(i - 1) + 1), scale };
}

/** .NET's RoundNumber: keeps `pos` digits, rounding half up; trailing zeros come off, and a number rounded to
 * nothing is 0 at scale 0, a decimal's sign dropped with it. */
function roundNumber(buffer: NumberBuffer, pos: number): void {
    const digits = buffer.digits;
    let i = 0;
    while (i < pos && i < digits.length) i++;
    let result: string;
    if (i === pos && i < digits.length && digits.charCodeAt(i) >= 53) {
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

/** .NET's FormatExponent. */
function exponentText(value: number, letter: string, minDigits: number, positiveSign: boolean, culture: NumberCulture): string {
    let text = letter;
    if (value < 0) text += culture.negativeSign;
    else if (positiveSign) text += culture.positiveSign;
    return text + Math.abs(value).toString().padStart(minDigits, '0');
}

/** .NET's FindSection: where section `section` (0 positive, 1 negative, 2 zero) starts, or 0 when the format
 * has none. */
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

/** The separator positions a grouped number puts in, counted in digits from the decimal point. */
function separatorsOf(culture: NumberCulture, numDigits: number): number[] {
    const separators: number[] = [];
    const sizes = culture.groupSizes;
    const [first] = sizes;
    if (first === undefined) return separators;
    let index = 0;
    let total = first;
    let size = total;
    while (numDigits > total) {
        if (size === 0) break;
        separators.push(total);
        if (index < sizes.length - 1) {
            index++;
            const next = sizes[index];
            if (next === undefined) throw new Error('Internal: a group size is missing.');
            size = next;
        }
        total += size;
    }
    return separators;
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
            roundNumber(buffer, pos);
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

    let separators: number[] = [];
    if (thousandSeps && culture.group.length > 0) {
        const totalDigits = digPos + (adjust < 0 ? adjust : 0);
        separators = separatorsOf(culture, firstDigit > totalDigits ? firstDigit : totalDigits);
    }
    let separator = separators.length - 1;

    const digits = buffer.digits;
    let cur = 0;
    let text = '';
    if (buffer.negative && section === 0 && buffer.scale !== 0) text += culture.negativeSign;
    let decimalWritten = false;
    const groupAfter = (): void => {
        if (thousandSeps && digPos > 1 && separator >= 0 && digPos === separatorAt(separators, separator) + 1) {
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

function separatorAt(separators: readonly number[], index: number): number {
    const at = separators[index];
    if (at === undefined) throw new Error(`Internal: separator ${index} is outside ${separators.length}.`);
    return at;
}
