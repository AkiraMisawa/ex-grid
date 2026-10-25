import { dateCode, type Culture } from './culture';
import { dateFromSerial, maximumSerial, serialDayOfWeek } from './date-serial';
import { equalsIgnoreCase, formatExponential, isAsciiLetter, lowerInvariant, startsWithAtIgnoreCase } from './dotnet';
import { powerOfTen, roundHalfAwayFromZeroAt } from './formulas/rounding';
import { elementAt } from './must';
import { generalFitted, generalText } from './number-text';
import type { Value } from './value';

/**
 * A colour a Number Format names at the start of a section — `[Red]` and the seven others, in any case
 * (ADR-0047). The text of a Value that section shows is painted in it, over the cell's Font colour (ADR-0071).
 * A numbered colour (`[Color10]`) is not one of these: it is refused, as Excel refused it.
 */
export type NumberFormatColour = 'black' | 'blue' | 'cyan' | 'green' | 'magenta' | 'red' | 'white' | 'yellow';

const colours: readonly NumberFormatColour[] = ['black', 'blue', 'cyan', 'green', 'magenta', 'red', 'white', 'yellow'];

/** The RGB Excel paints a Number Format's colour in, as `0xRRGGBB`: Excel's legacy palette (ADR-0071). */
export function numberFormatColourRgb(colour: NumberFormatColour): number {
    switch (colour) {
        case 'black': return 0x000000;
        case 'blue': return 0x0000ff;
        case 'cyan': return 0x00ffff;
        case 'green': return 0x00ff00;
        case 'magenta': return 0xff00ff;
        case 'red': return 0xff0000;
        case 'white': return 0xffffff;
        case 'yellow': return 0xffff00;
        default: throw new RangeError(`A Number Format names one of eight colours, not ${String(colour)}.`);
    }
}

/** The colour `name` names, in any case, or `undefined` when it names none of the eight. */
function namedColour(name: string): NumberFormatColour | undefined {
    return colours.find(colour => equalsIgnoreCase(colour, name));
}

/** A format code read, or why it could not be. */
export type NumberFormatReading =
    | { readonly format: NumberFormat; readonly reason?: undefined }
    | { readonly format?: undefined; readonly reason: string };

/** A Value as a format shows it: the text, whether it cannot be shown (`####`), and the section's colour. */
export interface FormattedValue {
    readonly text: string;
    readonly cannotShow: boolean;
    readonly colour: NumberFormatColour | undefined;
}

type SectionKind = 'number' | 'date' | 'text' | 'literal';

type PartKind = 'literal' | 'digit' | 'decimalPoint' | 'comma' | 'percent' | 'exponent' | 'dateCode' | 'amPm' | 'textValue';

interface Part {
    readonly kind: PartKind;
    readonly text: string;
    readonly code: string;
    readonly count: number;
}

function part(kind: PartKind, text: string, code = '', count = 0): Part {
    return { kind, text, code, count };
}

/** What `Section.parse` and `classify` give: the section, or why the code cannot be read. */
type SectionReading = { readonly section: Section; readonly reason?: undefined } | { readonly section?: undefined; readonly reason: string };

/** The digits of a number section: how many placeholders each part has, and what its commas mean. */
interface NumberShape {
    scale: number;
    thousands: boolean;
    integerPlaceholders: number;
    fractionPlaceholders: number;
    exponentPlaceholders: number;
    percent: number;
}

class Section {
    readonly #parts: readonly Part[];
    readonly kind: SectionKind;
    readonly percent: number;
    readonly colour: NumberFormatColour | undefined;
    readonly #scale: number;
    readonly #thousands: boolean;
    readonly #integerPlaceholders: number;
    readonly #fractionPlaceholders: number;
    readonly #exponentPlaceholders: number;
    readonly #twelveHour: boolean;

    constructor(parts: readonly Part[], kind: SectionKind, shape: NumberShape, twelveHour: boolean, colour: NumberFormatColour | undefined) {
        this.#parts = parts;
        this.kind = kind;
        this.percent = shape.percent;
        this.colour = colour;
        this.#scale = shape.scale;
        this.#thousands = shape.thousands;
        this.#integerPlaceholders = shape.integerPlaceholders;
        this.#fractionPlaceholders = shape.fractionPlaceholders;
        this.#exponentPlaceholders = shape.exponentPlaceholders;
        this.#twelveHour = twelveHour;
    }

    /** Whether a date section shows a time of day. */
    get hasTime(): boolean {
        return this.#parts.some(p => p.kind === 'amPm' || (p.kind === 'dateCode' && (p.code === 'h' || p.code === 's')));
    }

    static parse(text: string): SectionReading {
        const parts: Part[] = [];
        let colour: NumberFormatColour | undefined;
        for (let i = 0; i < text.length; i++) {
            const c = text.charAt(i);
            switch (c) {
                case '"': {
                    const close = closing(text, '"', i + 1);
                    parts.push(part('literal', text.slice(i + 1, close)));
                    i = close;
                    continue;
                }
                case '\\':
                    if (i + 1 >= text.length) return { reason: '\\ escapes nothing.' };
                    parts.push(part('literal', text.charAt(i + 1)));
                    i++;
                    continue;
                case '_':
                    // Excel pads by the width of the next character; a space stands in for it.
                    if (i + 1 >= text.length) return { reason: '_ pads by nothing.' };
                    parts.push(part('literal', ' '));
                    i++;
                    continue;
                case '*':
                    return { reason: '* repeats a character to fill the column, which depends on its width.' };
                case '[': {
                    const bracketEnd = text.indexOf(']', i + 1);
                    if (bracketEnd > i && startsWithAtIgnoreCase(text.slice(i + 1, bracketEnd), 0, 'Color')) {
                        return { reason: 'a numbered colour ([Color n]) is refused, as Excel refuses it; name one of the eight colours instead.' };
                    }
                    const named = bracketEnd > i ? namedColour(text.slice(i + 1, bracketEnd)) : undefined;
                    if (named !== undefined) {
                        // The section's colour (ADR-0071). Excel reads one at the start of a section; anywhere else it is refused.
                        if (parts.length > 0 || colour !== undefined) return { reason: 'a colour is read only once, at the start of its section.' };
                        colour = named;
                        i = bracketEnd;
                        continue;
                    }
                    return { reason: 'conditions, locales and elapsed time in brackets are not supported.' };
                }
                case '@':
                    parts.push(part('textValue', '@'));
                    continue;
                case '0':
                case '#':
                case '?':
                    parts.push(part('digit', c, c));
                    continue;
                case '.':
                    parts.push(part('decimalPoint', '.'));
                    continue;
                case ',':
                    parts.push(part('comma', ','));
                    continue;
                case '%':
                    parts.push(part('percent', '%'));
                    continue;
                default:
                    break;
            }

            if ((c === 'E' || c === 'e') && i + 1 < text.length && (text.charAt(i + 1) === '+' || text.charAt(i + 1) === '-')) {
                parts.push(part('exponent', text.slice(i, i + 2), text.charAt(i + 1)));
                i++;
                continue;
            }
            if (startsWithAtIgnoreCase(text, i, 'AM/PM')) {
                parts.push(part('amPm', text.slice(i, i + 5)));
                i += 4;
                continue;
            }
            if (startsWithAtIgnoreCase(text, i, 'A/P')) {
                parts.push(part('amPm', text.slice(i, i + 3)));
                i += 2;
                continue;
            }
            const lower = String.fromCharCode(lowerInvariant(text.charCodeAt(i)));
            if (lower === 'y' || lower === 'm' || lower === 'd' || lower === 'h' || lower === 's') {
                let run = 1;
                while (i + run < text.length && String.fromCharCode(lowerInvariant(text.charCodeAt(i + run))) === lower) run++;
                parts.push(part('dateCode', text.slice(i, i + run), lower, run));
                i += run - 1;
                continue;
            }
            if (isAsciiLetter(text.charCodeAt(i))) return { reason: `'${c}' is not a format code; quote text that should show as it is.` };
            parts.push(part('literal', c));
        }
        return classify(parts, colour);
    }

    formatText(text: string): string {
        let output = '';
        for (const p of this.#parts) output += p.kind === 'textValue' ? text : p.text;
        return output;
    }

    formatNumber(magnitude: number, minus: boolean, culture: Culture): { text: string; cannotShow: boolean } {
        switch (this.kind) {
            case 'date': return this.#formatDate(magnitude, minus, culture);
            case 'literal': return { text: (minus ? '-' : '') + this.#parts.map(p => p.text).join(''), cannotShow: false };
            case 'number':
            case 'text':
                return { text: this.#formatDigits(magnitude, minus, culture), cannotShow: false };
            default: return unreachableSection(this.kind);
        }
    }

    #formatDigits(magnitude: number, minusGiven: boolean, culture: Culture): string {
        let minus = minusGiven;
        const value = magnitude * powerOfTen(2 * this.percent) / powerOfTen(3 * this.#scale);
        let integerDigits: string;
        let fractionDigits: string;
        let exponent = 0;
        if (this.#exponentPlaceholders > 0) {
            ({ integer: integerDigits, fraction: fractionDigits, exponent } = scientificDigits(value, this.#fractionPlaceholders));
        } else {
            const rounded = roundHalfAwayFromZeroAt(value, this.#fractionPlaceholders);
            ({ integer: integerDigits, fraction: fractionDigits } = digitsOf(rounded, this.#fractionPlaceholders));
            // A negative number that rounds to zero shows no minus sign: -0.001 in 0.00 is 0.00, as Excel was observed to show it (FMT-063).
            if (rounded === 0) minus = false;
        }

        const data = culture.data;
        let output = minus ? '-' : '';

        // The integer placeholders take the digits right-aligned; extra digits go with the first.
        const integerParts: Part[] = [];
        for (const p of this.#parts) {
            if (p.kind === 'decimalPoint' || p.kind === 'exponent') break;
            if (p.kind === 'digit') integerParts.push(p);
        }
        const integerOutput: string[] = Array.from({ length: this.#integerPlaceholders }, () => '');
        let digitIndex = integerDigits.length;
        for (let k = integerParts.length - 1; k >= 0; k--) {
            digitIndex--;
            let rendered: string;
            if (digitIndex >= 0) {
                rendered = k === 0 ? integerDigits.slice(0, digitIndex + 1) : integerDigits.charAt(digitIndex);
            } else {
                const code = elementAt(integerParts, k).code;
                rendered = code === '0' ? '0' : code === '?' ? ' ' : '';
            }
            integerOutput[k] = rendered;
        }
        if (this.#thousands) {
            const joined = group(integerOutput.join(''), data.numberGroupSeparator, data.numberGroupSizes);
            for (let k = 0; k < integerOutput.length; k++) integerOutput[k] = k === 0 ? joined : '';
        }

        // The fraction placeholders take the digits left-aligned; trailing zeros under # vanish, under ? become spaces.
        const fractionParts: Part[] = [];
        let afterPoint = false;
        for (const p of this.#parts) {
            if (p.kind === 'exponent') break;
            if (p.kind === 'decimalPoint') afterPoint = true;
            else if (afterPoint && p.kind === 'digit') fractionParts.push(p);
        }
        const fractionOutput: string[] = Array.from({ length: this.#fractionPlaceholders }, () => '');
        let significant = true;
        for (let k = fractionParts.length - 1; k >= 0; k--) {
            const digit = fractionDigits.charAt(k);
            const code = elementAt(fractionParts, k).code;
            if (significant && digit === '0' && code !== '0') {
                fractionOutput[k] = code === '?' ? ' ' : '';
                continue;
            }
            significant = false;
            fractionOutput[k] = digit;
        }

        let integerAt = 0;
        let fractionAt = 0;
        let exponentAt = 0;
        const exponentDigits = String(Math.abs(exponent)).padStart(this.#exponentPlaceholders, '0');
        let inExponent = false;
        afterPoint = false;
        for (const p of this.#parts) {
            switch (p.kind) {
                case 'digit':
                    if (inExponent) {
                        // All exponent digits come out at the first exponent placeholder.
                        if (exponentAt++ === 0) output += exponentDigits;
                    } else if (afterPoint) {
                        output += elementAt(fractionOutput, fractionAt++);
                    } else {
                        output += elementAt(integerOutput, integerAt++);
                    }
                    break;
                case 'decimalPoint':
                    afterPoint = true;
                    output += data.numberDecimalSeparator;
                    break;
                case 'exponent':
                    inExponent = true;
                    output += p.text.charAt(0);
                    if (exponent < 0) output += '-';
                    else if (p.code === '+') output += '+';
                    break;
                case 'literal':
                case 'comma':
                case 'percent':
                case 'dateCode':
                case 'amPm':
                case 'textValue':
                    output += p.text;
                    break;
                default:
                    return unreachablePart(p.kind);
            }
        }
        return output;
    }

    #formatDate(magnitude: number, minus: boolean, culture: Culture): { text: string; cannotShow: boolean } {
        if (minus || magnitude >= maximumSerial + 1) return { text: '', cannotShow: true };
        // To the second: a time is shown rounded to the nearest second.
        const totalSeconds = roundHalfAwayFromZero(magnitude * 86_400);
        const serial = Math.trunc(totalSeconds / 86_400);
        if (serial > maximumSerial) return { text: '', cannotShow: true };
        const secondOfDay = totalSeconds % 86_400;
        const { year, month, day } = dateFromSerial(serial);
        const hour = Math.trunc(secondOfDay / 3600);
        const minute = Math.trunc(secondOfDay / 60) % 60;
        const second = secondOfDay % 60;
        const names = culture.data;

        let output = '';
        for (let i = 0; i < this.#parts.length; i++) {
            const p = elementAt(this.#parts, i);
            switch (p.kind) {
                case 'dateCode':
                    switch (p.code) {
                        case 'y':
                            output += p.count <= 2 ? two(year % 100) : String(year).padStart(4, '0');
                            break;
                        case 'm':
                            if (this.#isMinute(i)) {
                                output += p.count === 1 ? String(minute) : two(minute);
                            } else if (p.count === 1) {
                                output += String(month);
                            } else if (p.count === 2) {
                                output += two(month);
                            } else if (p.count === 3) {
                                output += elementAt(abbreviatedMonthNamesOf(culture), month - 1);
                            } else if (p.count === 4) {
                                output += elementAt(names.monthNames, month - 1);
                            } else {
                                output += elementAt(names.monthNames, month - 1).charAt(0);
                            }
                            break;
                        case 'd':
                            if (p.count === 1) output += String(day);
                            else if (p.count === 2) output += two(day);
                            else if (p.count === 3) output += elementAt(names.abbreviatedDayNames, serialDayOfWeek(serial));
                            else output += elementAt(names.dayNames, serialDayOfWeek(serial));
                            break;
                        case 'h': {
                            const shown = this.#twelveHour ? (hour % 12 === 0 ? 12 : hour % 12) : hour;
                            output += p.count === 1 ? String(shown) : two(shown);
                            break;
                        }
                        default:
                            output += p.count === 1 ? String(second) : two(second);
                            break;
                    }
                    break;
                case 'amPm': {
                    const afternoon = hour >= 12;
                    output += p.text.length === 5
                        ? (afternoon ? p.text.slice(3, 5) : p.text.slice(0, 2))
                        : (afternoon ? p.text.slice(2, 3) : p.text.slice(0, 1));
                    break;
                }
                case 'literal':
                case 'digit':
                case 'decimalPoint':
                case 'comma':
                case 'percent':
                case 'exponent':
                case 'textValue':
                    output += p.text;
                    break;
                default:
                    return unreachablePart(p.kind);
            }
        }
        return { text: output, cannotShow: false };
    }

    /** An `m` is minutes right after an hour code or right before a seconds code, as in Excel. */
    #isMinute(index: number): boolean {
        for (let i = index - 1; i >= 0; i--) {
            const p = elementAt(this.#parts, i);
            if (p.kind !== 'dateCode') continue;
            if (p.code === 'h') return true;
            break;
        }
        for (let i = index + 1; i < this.#parts.length; i++) {
            const p = elementAt(this.#parts, i);
            if (p.kind !== 'dateCode') continue;
            return p.code === 's';
        }
        return false;
    }
}

function noShape(percent = 0): NumberShape {
    return { scale: 0, thousands: false, integerPlaceholders: 0, fractionPlaceholders: 0, exponentPlaceholders: 0, percent };
}

function classify(parts: readonly Part[], colour: NumberFormatColour | undefined): SectionReading {
    const hasDate = parts.some(p => p.kind === 'dateCode' || p.kind === 'amPm');
    const hasDigits = parts.some(p => p.kind === 'digit');
    const hasText = parts.some(p => p.kind === 'textValue');
    if (hasText && (hasDate || hasDigits)) return { reason: '@ cannot share a section with number or date codes.' };
    if (hasText) return { section: new Section(literalise(parts, ['comma', 'decimalPoint', 'percent']), 'text', noShape(), false, colour) };

    if (hasDate) {
        if (hasDigits || parts.some(p => p.kind === 'exponent' || p.kind === 'percent')) {
            return { reason: 'fractional seconds and numbers inside a date or time format are not supported.' };
        }
        return { section: new Section(literalise(parts, ['comma', 'decimalPoint']), 'date', noShape(), parts.some(p => p.kind === 'amPm'), colour) };
    }

    if (!hasDigits) {
        return {
            section: new Section(literalise(parts, ['comma', 'decimalPoint']), 'literal', noShape(parts.filter(p => p.kind === 'percent').length), false, colour),
        };
    }

    // A number section: decide what each comma and point means.
    const result: Part[] = [];
    const shape = noShape();
    let seenPoint = false;
    let seenExponent = false;
    const lastDigit = parts.findLastIndex(p => p.kind === 'digit');
    for (let i = 0; i < parts.length; i++) {
        const p = elementAt(parts, i);
        switch (p.kind) {
            case 'digit':
                if (seenExponent) shape.exponentPlaceholders++;
                else if (seenPoint) shape.fractionPlaceholders++;
                else shape.integerPlaceholders++;
                result.push(p);
                break;
            case 'decimalPoint':
                if (seenPoint || seenExponent) return { reason: 'a number format has one decimal point.' };
                seenPoint = true;
                result.push(p);
                break;
            case 'comma': {
                const previous = i > 0 ? elementAt(parts, i - 1).kind : undefined;
                const before = previous === 'digit' || previous === 'comma';
                let digitAfterInInteger = false;
                for (let j = i + 1; j < parts.length; j++) {
                    const later = elementAt(parts, j).kind;
                    if (later === 'decimalPoint' || later === 'exponent') break;
                    if (later === 'digit') digitAfterInInteger = true;
                }
                if (!seenPoint && !seenExponent && before && digitAfterInInteger) {
                    shape.thousands = true;
                } else if (!seenExponent && before && !digitAfterInInteger && (!seenPoint || i > lastDigit)) {
                    shape.scale++;
                } else {
                    result.push(part('literal', ','));
                }
                break;
            }
            case 'percent':
                shape.percent++;
                result.push(p);
                break;
            case 'exponent':
                if (seenExponent || shape.integerPlaceholders !== 1) {
                    return { reason: 'scientific notation is supported with one integer placeholder, as in 0.00E+00.' };
                }
                seenExponent = true;
                result.push(p);
                break;
            case 'literal':
                if (p.text === '/' && parts.slice(i + 1).some(later => later.kind === 'digit')) return { reason: 'fractions are not supported.' };
                result.push(p);
                break;
            case 'dateCode':
            case 'amPm':
            case 'textValue':
                result.push(p);
                break;
            default:
                return unreachablePart(p.kind);
        }
    }
    if (seenExponent && shape.exponentPlaceholders === 0) return { reason: 'the exponent needs a digit placeholder.' };
    return { section: new Section(result, 'number', shape, false, colour) };
}

function literalise(parts: readonly Part[], kinds: readonly PartKind[]): Part[] {
    return parts.map(p => (kinds.includes(p.kind) ? part('literal', p.text) : p));
}

function group(integer: string, separator: string, sizes: readonly number[]): string {
    const digits = integer.replace(/^ +/, '');
    const padding = integer.slice(0, integer.length - digits.length);
    if (digits.length === 0) return integer;
    const groups: string[] = [];
    let end = digits.length;
    let sizeIndex = 0;
    while (end > 0) {
        const size = sizes.length === 0 ? 3 : elementAt(sizes, Math.min(sizeIndex, sizes.length - 1));
        if (size <= 0) {
            groups.push(digits.slice(0, end));
            break;
        }
        const start = Math.max(0, end - size);
        groups.push(digits.slice(start, end));
        end = start;
        sizeIndex++;
    }
    return padding + groups.toReversed().join(separator);
}

/** The integer and fraction digits of a value already rounded, from its 15 significant digits; zero's integer part is empty. */
function digitsOf(value: number, fractionDigits: number): { integer: string; fraction: string } {
    if (value === 0) return { integer: '', fraction: '0'.repeat(fractionDigits) };
    const spelled = formatExponential(value, 14);
    const mantissa = spelled.charAt(0) + spelled.slice(2, 16);
    const exponent = Number(spelled.slice(17));
    let integer: string;
    let fraction: string;
    if (exponent >= 0) {
        integer = exponent + 1 >= 15 ? mantissa + '0'.repeat(exponent + 1 - 15) : mantissa.slice(0, exponent + 1);
        fraction = exponent + 1 >= 15 ? '' : mantissa.slice(exponent + 1);
    } else {
        integer = '';
        fraction = '0'.repeat(-exponent - 1) + mantissa;
    }
    fraction = fraction.length >= fractionDigits ? fraction.slice(0, fractionDigits) : fraction.padEnd(fractionDigits, '0');
    return { integer, fraction };
}

/** One integer digit, the fraction digits rounded half away from zero, and the exponent. */
function scientificDigits(value: number, fractionDigits: number): { integer: string; fraction: string; exponent: number } {
    if (value === 0) return { integer: '0', fraction: '0'.repeat(fractionDigits), exponent: 0 };
    const spelled = formatExponential(value, 14);
    const mantissa = spelled.charAt(0) + spelled.slice(2, 16);
    let exponent = Number(spelled.slice(17));
    const keep = Math.min(1 + fractionDigits, 15);
    let digits = mantissa.slice(0, keep).split('');
    if (keep < 15 && mantissa.charAt(keep) >= '5') {
        let i = keep - 1;
        while (i >= 0) {
            if (digits[i] === '9') {
                digits[i] = '0';
                i--;
                continue;
            }
            digits[i] = String(Number(elementAt(digits, i)) + 1);
            break;
        }
        if (i < 0) {
            digits = ['1', ...digits.slice(0, -1)];
            exponent++;
        }
    }
    const all = digits.join('').padEnd(1 + fractionDigits, '0');
    return { integer: all.slice(0, 1), fraction: all.slice(1), exponent };
}

/** `Math.Round(x, MidpointRounding.AwayFromZero)` as .NET computes it: `Truncate(x + CopySign(BitDecrement(0.5), x))`. */
function roundHalfAwayFromZero(x: number): number {
    return Math.trunc(x + (x < 0 ? -0.49999999999999994 : 0.49999999999999994));
}

function two(n: number): string {
    return String(n).padStart(2, '0');
}

function closing(text: string, quote: string, from: number): number {
    const close = text.indexOf(quote, from);
    if (close < 0) throw new Error('Internal: the sections were split with every quotation closed.');
    return close;
}

function unreachableSection(kind: never): never {
    throw new Error(`Internal: ${String(kind)} is no kind of section.`);
}

function unreachablePart(kind: never): never {
    throw new Error(`Internal: ${String(kind)} is no kind of part.`);
}

/**
 * The abbreviated month names Excel shows under a culture, which `mmm` shows: the culture's own, with the two
 * differences found between the ICU data .NET reads and Windows' regional settings, which Excel reads.
 * ICU abbreviates September `Sept` under en-GB, where Windows writes `Sep`; and it abbreviates a Japanese
 * month as its full name, `1月`, where Windows writes the month's number alone (ADR-0071).
 */
export function abbreviatedMonthNamesOf(culture: Culture): readonly string[] {
    const japanese = culture.data.twoLetterLanguage === 'ja';
    return culture.data.abbreviatedMonthNames.map((name, i) => {
        const number = String(i + 1);
        if (name === 'Sept') return 'Sep';
        if (japanese && name === `${number}月`) return number;
        return name;
    });
}

/**
 * The currency symbol Excel shows under a culture: the culture's own, with Windows' ja-JP yen sign U+00A5
 * where ICU on Linux gives the full-width U+FFE5 (ADR-0071).
 */
export function currencySymbolOf(culture: Culture): string {
    return culture.data.currencySymbol.replaceAll('￥', '¥');
}

/**
 * The built-in currency format as Excel spells it under a culture, built from the culture's currency symbol
 * and where it puts the symbol and the sign, as Windows' regional settings give Excel the same three.
 */
function localCurrencyCode(culture: Culture, decimals: boolean): string {
    const data = culture.data;
    const currencySymbol = currencySymbolOf(culture);
    const symbol = /^\p{Sc}*$/u.test(currencySymbol) && !/[\uD800-\uDFFF]/.test(currencySymbol)
        ? currencySymbol
        : `"${currencySymbol.replaceAll('"', '')}"`;
    const n = decimals ? '#,##0.00' : '#,##0';
    // Windows' regional default for en-US writes a negative amount in parentheses, and Excel follows it;
    // .NET's ICU data writes it with a minus. Elsewhere .NET's data is read, and it agreed with Excel.
    const negativePattern = data.name === 'en-US' ? 0 : data.currencyNegativePattern;
    const negatives: Readonly<Record<number, string>> = {
        0: `(${symbol}${n})`, 2: `${symbol}-${n}`, 3: `${symbol}${n}-`, 4: `(${n}${symbol})`, 5: `-${n}${symbol}`,
        6: `${n}-${symbol}`, 7: `${n}${symbol}-`, 8: `-${n} ${symbol}`, 9: `-${symbol} ${n}`, 10: `${n} ${symbol}-`,
        11: `${symbol} ${n}-`, 12: `${symbol} -${n}`, 13: `${n}- ${symbol}`, 14: `(${symbol} ${n})`, 15: `(${n} ${symbol})`,
        16: `${symbol}- ${n}`,
    };
    const negative = negatives[negativePattern] ?? `-${symbol}${n}`;
    const positive = data.currencyPositivePattern === 0 ? `${symbol}${n}`
        : data.currencyPositivePattern === 1 ? `${n}${symbol}`
            : data.currencyPositivePattern === 2 ? `${symbol} ${n}`
                : `${n} ${symbol}`;
    return `${negative.endsWith(')') ? `${positive}_)` : positive};[Red]${negative}`;
}

const shortDateCode = 'm/d/yyyy';
const shortDateTimeCode = 'm/d/yyyy h:mm';
/** Excel's built-in day and month name (`d-mmm`, format 16), which a date typed without its year records. */
const dayMonthCode = 'd-mmm';
/** Excel's built-in date with the month's name (`d-mmm-yy`, format 15). */
const dayMonthYearCode = 'd-mmm-yy';
/** Excel's built-in hour and minute (`h:mm`, format 20). */
const hourMinuteCode = 'h:mm';
const currencyCode = '$#,##0.00_);[Red]($#,##0.00)';
const wholeCurrencyCode = '$#,##0_);[Red]($#,##0)';

/**
 * An Excel number format code, such as `#,##0.00`, `0%` or `yyyy-mm-dd` (ADR-0046). The code is written in
 * Excel's stored, invariant form — `,` groups thousands and `.` marks decimals — and shown with the Sheet's
 * culture's separators and month and day names. A code outside the subset the engine shows as Excel does —
 * conditions, locales and elapsed time in brackets, fractions, fractional seconds, `*` fill, era codes,
 * unquoted letters, a numbered colour — is refused rather than shown some other way.
 */
export class NumberFormat {
    readonly #code: string;
    readonly #sections: readonly Section[];

    private constructor(code: string, sections: readonly Section[]) {
        this.#code = code;
        this.#sections = sections;
    }

    /** Excel's `General`: at most 15 significant digits, the culture's decimal separator. */
    static readonly general: NumberFormat = new NumberFormat('General', []);

    /** The code as given, such as `#,##0.00`. */
    get code(): string {
        return this.#code;
    }

    /** Whether this is `General`. */
    get isGeneral(): boolean {
        return this.#sections.length === 0;
    }

    /** Whether a number shows as a date or time: its first section holds date or time codes. */
    get isDate(): boolean {
        return this.#sections.length > 0 && elementAt(this.#sections, 0).kind === 'date';
    }

    /** Whether a number shows as a date with no time of day. @internal */
    get isDateOnly(): boolean {
        return this.isDate && !elementAt(this.#sections, 0).hasTime;
    }

    /** Whether a number shows as a percentage: its first section holds `%`. */
    get isPercent(): boolean {
        return this.#sections.length > 0 && elementAt(this.#sections, 0).percent > 0;
    }

    /** Reads a format code; one outside the subset is refused with a `TypeError` that says why. */
    static parse(code: string): NumberFormat {
        const reading = NumberFormat.tryParse(code);
        if (reading.format === undefined) throw new TypeError(`The number format '${code}' is not supported: ${reading.reason}`);
        return reading.format;
    }

    /** Reads a format code, saying why when it cannot. */
    static tryParse(given: string): NumberFormatReading {
        if (equalsIgnoreCase(given, 'General')) return { format: NumberFormat.general };
        const code = upperCaseAmPm(given);
        const parts = splitSections(code);
        if (typeof parts === 'string') return { reason: parts };
        if (parts.length > 4) return { reason: 'a format has at most four sections.' };
        const sections: Section[] = [];
        for (let i = 0; i < parts.length; i++) {
            const reading = Section.parse(elementAt(parts, i));
            if (reading.section === undefined) return { reason: reading.reason };
            const section = reading.section;
            if (section.kind === 'text' && i < parts.length - 1) return { reason: '@ belongs in the last section.' };
            if (i === 3 && section.kind !== 'text' && section.kind !== 'literal') return { reason: 'the fourth section is for text.' };
            sections.push(section);
        }
        return { format: new NumberFormat(code, sections) };
    }

    /** Whether a number shows in Excel's General form: the format is General, or holds only a text section. @internal */
    get showsNumbersAsGeneral(): boolean {
        return this.#numberSections === 0;
    }

    /** How many sections show numbers: a text section that ends a format of fewer than four shows none. */
    get #numberSections(): number {
        const count = this.#sections.length;
        return count > 0 && count < 4 && elementAt(this.#sections, count - 1).kind === 'text' ? count - 1 : count;
    }

    /** The section that shows text: the fourth, or a text section that ends a shorter format. */
    get #textSection(): Section | undefined {
        const count = this.#sections.length;
        if (count === 4) return elementAt(this.#sections, 3);
        if (count > 0 && elementAt(this.#sections, count - 1).kind === 'text') return elementAt(this.#sections, count - 1);
        return undefined;
    }

    /**
     * A Value as this format shows it in a column `characters` wide, each character charged one digit width
     * (ADR-0047): a number in General is fitted to the width as Excel's General fits it, and any other number
     * whose text is longer than the width cannot be shown (`####`, ADR-0016). Text, booleans and Error Values
     * are never fitted. @internal
     */
    formatIn(value: Value, culture: Culture, characters: number): FormattedValue {
        if (value.kind !== 'number') return this.format(value, culture);
        if (this.showsNumbersAsGeneral) {
            const fitted = generalFitted(value.number, characters, culture);
            return fitted === undefined ? { text: '', cannotShow: true, colour: undefined } : { text: fitted, cannotShow: false, colour: undefined };
        }
        const shown = this.format(value, culture);
        return shown.cannotShow || shown.text.length > characters ? { text: '', cannotShow: true, colour: shown.colour } : shown;
    }

    /**
     * A Value as this format shows it under a culture, and the colour of the section that showed it
     * (ADR-0071), `undefined` where it names none or no section showed the Value. @internal
     */
    format(value: Value, culture: Culture): FormattedValue {
        if (value.kind === 'number') {
            const local = this.#localIn(culture);
            if (local !== undefined) return local.format(value, culture);
        }
        switch (value.kind) {
            case 'boolean':
            case 'error':
                return { text: value.toString(), cannotShow: false, colour: undefined };
            case 'text': {
                const textSection = this.#textSection;
                return textSection === undefined
                    ? { text: value.text, cannotShow: false, colour: undefined }
                    : { text: textSection.formatText(value.text), cannotShow: false, colour: textSection.colour };
            }
            case 'number':
                break;
            default:
                throw new Error(`Internal: ${String(value.kind)} is no kind of Value.`);
        }

        const number = value.number;
        if (this.showsNumbersAsGeneral) return { text: generalText(number, culture), cannotShow: false, colour: undefined };
        let chosen: Section;
        let automaticMinus = false;
        if (number < 0 && this.#numberSections >= 2) {
            chosen = elementAt(this.#sections, 1);
        } else if (number === 0 && this.#numberSections >= 3) {
            chosen = elementAt(this.#sections, 2);
        } else {
            chosen = elementAt(this.#sections, 0);
            automaticMinus = number < 0;
        }
        const { text, cannotShow } = chosen.formatNumber(Math.abs(number), automaticMinus, culture);
        return { text, cannotShow, colour: chosen.colour };
    }

    /** Two formats are equal when their codes are (ordinal). */
    equals(other: NumberFormat): boolean {
        return this.#code === other.#code;
    }

    /** The code. */
    toString(): string {
        return this.#code;
    }

    /**
     * Excel's built-in short date, `m/d/yyyy` in the invariant codes: what a typed date records under every
     * culture. It shows a date in the Sheet culture's own short-date pattern, as Excel's built-in format 14
     * shows in the system's pattern. @internal
     */
    static readonly shortDate: NumberFormat = NumberFormat.parse(shortDateCode);

    /** Excel's built-in short date with a time (`m/d/yyyy h:mm`, format 22). @internal */
    static readonly shortDateTime: NumberFormat = NumberFormat.parse(shortDateTimeCode);

    static readonly #germanDayMonth = NumberFormat.parse('dd. mmm');
    static readonly #twoDigitDayMonth = NumberFormat.parse('dd-mmm');
    static readonly #currency = NumberFormat.parse(currencyCode);
    static readonly #wholeCurrency = NumberFormat.parse(wholeCurrencyCode);
    static readonly #shortDates = new Map<string, NumberFormat>();
    static readonly #datesAndTimes = new Map<string, NumberFormat>();
    static readonly #currencies = new Map<string, NumberFormat>();

    /**
     * The built-ins shown in the Sheet culture's own form: the short date and the short date with a time (14
     * and 22), the day and month (16), the day, month and year (15), the hour and minute (20), and the
     * currency (6 and 8).
     */
    static readonly #localisedBuiltIns: readonly NumberFormat[] = [
        NumberFormat.shortDate, NumberFormat.shortDateTime, NumberFormat.parse(dayMonthCode), NumberFormat.parse(dayMonthYearCode),
        NumberFormat.parse(hourMinuteCode), NumberFormat.#currency, NumberFormat.#wholeCurrency,
    ];

    /**
     * Excel's built-in currency format under a culture: what Ctrl+Shift+$ records (ADR-0071). It is format 8,
     * `$#,##0.00_);[Red]($#,##0.00)`, or format 6, `$#,##0_);[Red]($#,##0)`, where the culture's currency has
     * no decimals; recorded in those codes, it shows in the culture's own currency.
     */
    static builtInCurrency(culture: Culture): NumberFormat {
        return culture.data.currencyDecimalDigits === 0 ? NumberFormat.#wholeCurrency : NumberFormat.#currency;
    }

    /** The built-in `d-mmm` in the culture's own form (ADR-0047, third run), or `undefined`. */
    #dayMonthIn(culture: Culture): NumberFormat | undefined {
        if (!equalsIgnoreCase(this.#code, dayMonthCode)) return undefined;
        if (culture.data.name === 'de-DE') return NumberFormat.#germanDayMonth;
        return culture.data.shortDatePattern.includes('dd') ? NumberFormat.#twoDigitDayMonth : undefined;
    }

    /** The pattern the built-in short date (and short date with time, and `d-mmm`) shows in under the culture, or `undefined`. */
    #shortDateIn(culture: Culture): NumberFormat | undefined {
        const dayMonth = this.#dayMonthIn(culture);
        if (dayMonth !== undefined) return dayMonth;
        const time = equalsIgnoreCase(this.#code, shortDateTimeCode);
        if (!time && !equalsIgnoreCase(this.#code, shortDateCode)) return undefined;
        const key = `${culture.data.name}|${String(time)}`;
        let local = NumberFormat.#shortDates.get(key);
        if (local === undefined) {
            const code = dateCode(culture) + (time ? ' h:mm' : '');
            const parsed = code === this.#code ? undefined : NumberFormat.tryParse(code).format;
            local = parsed === undefined ? this : parsed;
            NumberFormat.#shortDates.set(key, local);
        }
        return local === this || local.#code === this.#code ? undefined : local;
    }

    /** The built-ins 15 and 20 in the culture's own form, as the twelfth Windows run read them (ADR-0071, case 19), or `undefined`. */
    #dateOrTimeIn(culture: Culture): NumberFormat | undefined {
        let code: string;
        if (equalsIgnoreCase(this.#code, dayMonthYearCode)) {
            code = `${culture.data.shortDatePattern.includes('dd') ? 'dd' : 'd'}-mmm-yy`;
        } else if (equalsIgnoreCase(this.#code, hourMinuteCode)) {
            const time = culture.data.shortTimePattern;
            code = time.includes('HH') || time.includes('hh') ? 'hh:mm' : hourMinuteCode;
        } else {
            return undefined;
        }
        if (code === this.#code) return undefined;
        const key = `${culture.data.name}|${code}`;
        let local = NumberFormat.#datesAndTimes.get(key);
        if (local === undefined) {
            local = NumberFormat.parse(code);
            NumberFormat.#datesAndTimes.set(key, local);
        }
        return local;
    }

    /** The built-in currency format in the culture's own form, or `undefined` (en-US). */
    #currencyIn(culture: Culture): NumberFormat | undefined {
        const decimals = equalsIgnoreCase(this.#code, currencyCode);
        if (!decimals && !equalsIgnoreCase(this.#code, wholeCurrencyCode)) return undefined;
        const key = `${culture.data.name}|${String(decimals)}`;
        let local = NumberFormat.#currencies.get(key);
        if (local === undefined) {
            const code = localCurrencyCode(culture, decimals);
            const parsed = code === this.#code ? undefined : NumberFormat.tryParse(code).format;
            local = parsed === undefined ? this : parsed;
            NumberFormat.#currencies.set(key, local);
        }
        return local === this || local.#code === this.#code ? undefined : local;
    }

    #localIn(culture: Culture): NumberFormat | undefined {
        return this.#shortDateIn(culture) ?? this.#dateOrTimeIn(culture) ?? this.#currencyIn(culture);
    }

    /**
     * The code as Format Cells' Custom box spells it under a culture, as Excel's local code does (ADR-0071).
     * A built-in shown in the culture's own form is spelled in that form; a code of its own that spells such
     * a built-in's code is spelled as it was typed, and any other code as it is.
     */
    localCode(culture: Culture): string {
        const local = this.#localIn(culture);
        if (local !== undefined) return local.#code;
        const spelled = builtInSpelled(this.#code, NumberFormat.#localisedBuiltIns);
        return spelled === undefined ? this.#code : spelled;
    }

    /**
     * Reads a code typed into Format Cells' Custom box under a culture (ADR-0071). A code is a built-in only
     * when it spells that built-in's code under the culture (`localCode`), in any case; a code of its own that
     * spells a built-in's invariant code is recorded with its separators escaped, `d\-mmm\-yy`.
     */
    static tryParseLocal(code: string, culture: Culture): NumberFormatReading {
        const reading = NumberFormat.tryParse(code);
        if (reading.format === undefined) return reading;
        const format = reading.format;
        for (const builtIn of NumberFormat.#localisedBuiltIns) {
            if (equalsIgnoreCase(format.#code, builtIn.localCode(culture))) return { format: builtIn };
        }
        // The engine would show it as the built-in it spells, in the culture's own form.
        if (format.#localIn(culture) !== undefined) return { format: NumberFormat.parse(ownSpelling(format.#code)) };
        return { format };
    }

    /**
     * This format as a Sheet Document opened under a culture holds it (ADR-0071, ticket 103): a code of its
     * own that spells a localised built-in's code as the culture spells that built-in is the built-in. @internal
     */
    openedIn(culture: Culture): NumberFormat {
        const unescaped = builtInSpelled(this.#code, NumberFormat.#localisedBuiltIns);
        if (unescaped === undefined) return this;
        const read = NumberFormat.tryParseLocal(unescaped, culture).format;
        return read !== undefined && NumberFormat.#localisedBuiltIns.some(builtIn => builtIn.equals(read)) ? read : this;
    }

    /**
     * Reads a code a file stores as a format of its own, as `TEXT` reads a code (ADR-0120, ADR-0330): the
     * invariant spelling, as written. A code that spells a built-in shown in the culture's own form is a code
     * of its own, recorded with its separators escaped.
     */
    static tryParseAsWritten(code: string): NumberFormatReading {
        const reading = NumberFormat.tryParse(code);
        if (reading.format === undefined) return reading;
        const written = reading.format.#code;
        if (NumberFormat.#localisedBuiltIns.some(builtIn => equalsIgnoreCase(builtIn.#code, written))) {
            return { format: NumberFormat.parse(ownSpelling(written)) };
        }
        return reading;
    }
}

/** A built-in's invariant code as a code of its own: each separator escaped, so that it shows as it is spelled and is no built-in's. */
function ownSpelling(builtIn: string): string {
    let spelled = '';
    for (const c of builtIn) {
        if (c === '-' || c === '/' || c === ':' || c === '$') spelled += '\\';
        spelled += c;
    }
    return spelled;
}

/** The built-in's code a code of its own spells (`ownSpelling`), or `undefined` when it is not one. */
function builtInSpelled(code: string, builtIns: readonly NumberFormat[]): string | undefined {
    if (!code.includes('\\')) return undefined;
    const unescaped = code.replaceAll('\\', '');
    return ownSpelling(unescaped) === code && builtIns.some(builtIn => equalsIgnoreCase(builtIn.code, unescaped)) ? unescaped : undefined;
}

/**
 * `AM/PM` written in any case is `AM/PM`: Excel rewrites `h:mm am/pm` to `h:mm AM/PM` and shows `6:00 PM`
 * (FMT-065). Quoted and escaped text is left alone.
 */
function upperCaseAmPm(code: string): string {
    let rewritten = code;
    for (let i = 0; i < code.length; i++) {
        switch (code.charAt(i)) {
            case '"': {
                const close = code.indexOf('"', i + 1);
                if (close < 0) return rewritten;
                i = close;
                continue;
            }
            case '[': {
                const end = code.indexOf(']', i + 1);
                if (end < 0) return rewritten;
                i = end;
                continue;
            }
            case '\\':
            case '_':
            case '*':
                i++;
                continue;
            default:
                break;
        }
        if (!startsWithAtIgnoreCase(code, i, 'AM/PM')) continue;
        if (code.slice(i, i + 5) !== 'AM/PM') rewritten = `${rewritten.slice(0, i)}AM/PM${rewritten.slice(i + 5)}`;
        i += 4;
    }
    return rewritten;
}

/** The code's sections, split at each `;` outside quotes, brackets and escapes; or why it cannot be split. */
function splitSections(code: string): string[] | string {
    const parts: string[] = [];
    let start = 0;
    for (let i = 0; i < code.length; i++) {
        switch (code.charAt(i)) {
            case '"': {
                const close = code.indexOf('"', i + 1);
                if (close < 0) return 'a quotation is not closed.';
                i = close;
                break;
            }
            case '[': {
                const end = code.indexOf(']', i + 1);
                if (end < 0) return 'a bracket is not closed.';
                i = end;
                break;
            }
            case '\\':
            case '_':
            case '*':
                i++;
                break;
            case ';':
                parts.push(code.slice(start, i));
                start = i + 1;
                break;
            default:
                break;
        }
    }
    parts.push(code.slice(start));
    return parts;
}
