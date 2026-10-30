// Numbers written as text, as Excel writes them.
import type { Culture } from './culture';
import { formatExponential, formatGeneral, parseInvariantDouble } from './dotnet';

/**
 * The most characters General shows, whatever the width, not counting a minus sign: a wide column shows
 * `=1/3` as `0.333333333` and `123456789012` as `1.23457E+11` (GW-004, GW-009, GW-010; ADR-0120).
 */
export const generalLimit = 11;

/** The most characters, not counting a minus sign, that a number written into text takes in full (`written`). */
export const writtenLongest = 20;

/** The most characters a number constant in a Formula takes in full (`written`). */
export const formulaConstantLongest = 21;

/** A number's fifteen significant digits and the power of ten of the first: `x.ToString("E14")` read apart. */
export function fifteenDigits(magnitude: number): { digits: string; exponent: number } {
    const e14 = formatExponential(magnitude, 14);
    return { digits: e14.charAt(0) + e14.slice(2, 16), exponent: Number(e14.slice(17)) };
}

/**
 * Excel's General form in full: at most 15 significant digits (ADR-0047), the culture's decimal separator,
 * and scientific notation for very large and very small magnitudes (`number.ToString("G15", culture)`).
 */
export function generalText(number: number, culture: Culture): string {
    return formatGeneral(number, 15, culture.data.numberDecimalSeparator, culture.data.negativeSign);
}

/**
 * A number as Excel writes it into text — `=A1&""`, and the Cell Editor's text of a typed number: at most
 * 15 significant digits (ADR-0047) and the culture's decimal separator, written out in full where that
 * takes at most `longest` characters (a minus sign not counted), and in General's scientific spelling
 * (`1E+20`, `1.23456789012345E-05`) where it would take more (ADR-0047, second run).
 */
export function writtenText(number: number, culture: Culture, longest = writtenLongest): string {
    if (number === 0) return '0';
    const { digits: all, exponent } = fifteenDigits(Math.abs(number));
    const digits = all.replace(/0+$/, '');
    const fullLength = exponent < 0
        ? 2 + (-exponent - 1) + digits.length
        : Math.max(digits.length, exponent + 1) + (digits.length > exponent + 1 ? 1 : 0);
    if (fullLength > longest) return generalText(number, culture);
    const separator = culture.data.numberDecimalSeparator;
    const sign = number < 0 ? '-' : '';
    if (exponent < 0) return `${sign}0${separator}${'0'.repeat(-exponent - 1)}${digits}`;
    if (digits.length <= exponent + 1) return `${sign}${digits}${'0'.repeat(exponent + 1 - digits.length)}`;
    return `${sign}${digits.slice(0, exponent + 1)}${separator}${digits.slice(exponent + 1)}`;
}

/**
 * Excel's General form fitted to a column `characters` wide, each character charged one digit width
 * (ADR-0047): decimals are rounded to what fits, and the form is scientific where the integer part does
 * not fit, where it has twelve or more digits, or where the scientific form that fits is closer to the
 * number than the decimal form that fits. A negative number rounded to a bare 0 keeps its minus sign, and a
 * column with no room for the sign shows `####` (GW-026, GW-027). `undefined` when neither form fits.
 */
export function generalFitted(number: number, characters: number, culture: Culture): string | undefined {
    return generalForm(number, characters, culture.data.numberDecimalSeparator)?.text;
}

/** Whether General, fitted to `characters`, writes the number in scientific notation; `undefined` when it cannot be shown at that width. */
export function isGeneralScientific(number: number, characters: number): boolean | undefined {
    return generalForm(number, characters, '.')?.scientific;
}

function generalForm(number: number, characters: number, separator: string): { text: string; scientific: boolean } | undefined {
    const minus = number < 0;
    const limit = Math.min(characters - (minus ? 1 : 0), generalLimit);
    if (limit < 1) return undefined;
    if (number === 0) return { text: '0', scientific: false };

    // Fifteen significant digits (ADR-0047), rounded again from there.
    const magnitude = Math.abs(number);
    const { digits, exponent } = fifteenDigits(magnitude);
    const fixedForm = decimalForm(digits, exponent, limit);
    const scientificForm = scientific(digits, exponent, limit);

    let chosen: string;
    let isScientific: boolean;
    if (fixedForm !== undefined && (scientificForm === undefined || error(fixedForm, magnitude) <= error(scientificForm, magnitude))) {
        chosen = fixedForm;
        isScientific = false;
    } else if (scientificForm !== undefined) {
        chosen = scientificForm;
        isScientific = true;
    } else {
        return undefined;
    }
    if (separator !== '.') chosen = chosen.replaceAll('.', separator);
    return { text: (minus ? '-' : '') + chosen, scientific: isScientific };
}

function error(invariant: string, magnitude: number): number {
    return Math.abs(parseInvariantDouble(invariant) - magnitude);
}

/** The decimal form, invariant, in at most `limit` characters. */
function decimalForm(digits: string, exponent: number, limit: number): string | undefined {
    const integerDigits = exponent >= 0 ? exponent + 1 : 1;
    if (integerDigits > limit) return undefined;
    const fraction = Math.max(0, limit - integerDigits - 1);
    const { digits: rounded, at } = round(digits, exponent, exponent + 1 + fraction);
    if (rounded.length === 0) return '0';

    // rounded holds the significant digits, the first at 10^at.
    const digitAt = (power: number): string => {
        const i = at - power;
        return i >= 0 && i < rounded.length ? rounded.charAt(i) : '0';
    };

    let text = at < 0 ? '0' : '';
    for (let p = at; p >= 0; p--) text += digitAt(p);
    let fractionText = '';
    for (let p = -1; p >= -fraction; p--) fractionText += digitAt(p);
    const trimmed = fractionText.replace(/0+$/, '');
    if (trimmed.length > 0) text += `.${trimmed}`;
    return text.length <= limit ? text : undefined;
}

/** The scientific form, invariant, in at most `limit` characters. */
function scientific(digits: string, exponent: number, limit: number): string | undefined {
    for (let mantissaFraction = Math.max(0, limit - 6); mantissaFraction >= 0; mantissaFraction--) {
        const { digits: rounded, at } = round(digits, exponent, 1 + mantissaFraction);
        const mantissa = rounded.replace(/0+$/, '');
        let text = mantissa.charAt(0);
        if (mantissa.length > 1) text += `.${mantissa.slice(1)}`;
        const power = Math.abs(at);
        text += (at < 0 ? 'E-' : 'E+') + String(power).padStart(power >= 100 ? 3 : 2, '0');
        if (text.length <= limit) return text;
    }
    return undefined;
}

/**
 * `digits` (the first at 10^`exponent`) rounded half away from zero to `keep` significant digits: the digits
 * kept, and the power of ten of the first. Empty when the number rounds to zero.
 */
export function roundDigits(digits: string, exponent: number, keep: number): { digits: string; at: number } {
    return round(digits, exponent, keep);
}

function round(digits: string, exponent: number, keep: number): { digits: string; at: number } {
    if (keep >= digits.length) return { digits, at: exponent };
    if (keep < 0) return { digits: '', at: exponent };
    if (keep === 0) return digits.charAt(0) >= '5' ? { digits: '1', at: exponent + 1 } : { digits: '', at: exponent };
    let kept = digits.slice(0, keep);
    if (digits.charAt(keep) >= '5') {
        let i = keep - 1;
        while (i >= 0 && kept.charAt(i) === '9') i--;
        if (i < 0) return { digits: `1${'0'.repeat(keep)}`, at: exponent + 1 };
        kept = kept.slice(0, i) + String.fromCharCode(kept.charCodeAt(i) + 1) + '0'.repeat(keep - 1 - i);
    }
    return { digits: kept, at: exponent };
}
