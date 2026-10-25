// Excel's General as a PivotTable's value cell shows it, and as it labels a number Item (ADR-0060; the
// readings of 2026-10-12, R3 (c)): at most eleven characters besides a minus sign. Decimals are rounded
// to what fits, and the form is scientific where the integer part has twelve or more digits, or where the
// scientific form is closer to the number than the decimal form that fits: 1.414213562, 1.23456789,
// 1.23457E+11, 1.23457E-05. The C# engine's PivotGeneral, transcribed; the minus sign is `-` in every
// culture, and only the decimal separator is the culture's.

import type { SnapshotDecimal } from '@ex-grid/data';
import type { NumberCulture } from '../culture/culture-data';
import { invariantNumbers } from './invariant';
import { formatDecimal, formatDouble } from './number-text';

/** The most characters General shows, not counting a minus sign. */
export const generalCharacters = 11;

/** A double in General, under the culture's decimal separator. */
export function generalOfDouble(number: number, culture: NumberCulture): string {
    if (!Number.isFinite(number)) return formatDouble(number, undefined, culture);
    const magnitude = Math.abs(number);
    return form(number < 0, formatDouble(magnitude, 'E14', invariantNumbers),
        (decimalForm, scientific) => Math.abs(Number(decimalForm) - magnitude) <= Math.abs(Number(scientific) - magnitude), culture);
}

/** An exact decimal in General: its own digits are rounded, never a double's, so a sum of money is shown
 * from the sum itself (ADR-0060, "Money stays exact"). The two forms are built from its fifteen
 * significant digits, and which is closer is judged against those same digits. */
export function generalOfDecimal(number: SnapshotDecimal, culture: NumberCulture): string {
    const magnitude = { mantissa: number.mantissa < 0n ? -number.mantissa : number.mantissa, scale: number.scale };
    const e14 = formatDecimal(magnitude, 'E14', invariantNumbers);
    const fifteen = exactOf(e14);
    return form(number.mantissa < 0n, e14,
        (decimalForm, scientific) => compareMagnitudes(distance(exactOf(decimalForm), fifteen), distance(exactOf(scientific), fifteen)) <= 0,
        culture);
}

/** An invariant number's text, `-1.5`, `1.23457E+11`, as an exact fraction: digits over a power of ten. */
interface Exact {
    readonly digits: bigint;
    readonly scale: number;
}

function exactOf(text: string): Exact {
    const match = /^(-?)(\d+)(?:\.(\d+))?(?:E([+-]\d+))?$/.exec(text);
    if (match === null) throw new Error(`Internal: '${text}' is not a number General wrote.`);
    const [, sign = '', whole = '', fraction = '', exponent = '0'] = match;
    const scale = fraction.length - Number(exponent);
    const digits = BigInt(sign + whole + fraction);
    return scale >= 0 ? { digits, scale } : { digits: digits * 10n ** BigInt(-scale), scale: 0 };
}

function distance(a: Exact, b: Exact): Exact {
    const scale = Math.max(a.scale, b.scale);
    const difference = a.digits * 10n ** BigInt(scale - a.scale) - b.digits * 10n ** BigInt(scale - b.scale);
    return { digits: difference < 0n ? -difference : difference, scale };
}

function compareMagnitudes(a: Exact, b: Exact): number {
    const scale = Math.max(a.scale, b.scale);
    const left = a.digits * 10n ** BigInt(scale - a.scale);
    const right = b.digits * 10n ** BigInt(scale - b.scale);
    return left < right ? -1 : left > right ? 1 : 0;
}

/** The form of a magnitude written `d.ddddddddddddddE±xxx`: fifteen significant digits, rounded again
 * from there. */
function form(minus: boolean, e14: string, decimalIsCloser: (decimalForm: string, scientific: string) => boolean,
    culture: NumberCulture): string {
    const digits = e14.charAt(0) + e14.slice(2, 16);
    if (/^0+$/.test(digits)) return '0';
    const exponent = Number(e14.slice(17));
    const decimalForm = decimalText(digits, exponent, generalCharacters);
    const scientific = scientificText(digits, exponent, generalCharacters);
    let chosen = decimalForm !== undefined && decimalIsCloser(decimalForm, scientific) ? decimalForm : scientific;
    const separator = culture.decimal;
    if (separator !== '.') chosen = chosen.replaceAll('.', separator);
    return (minus ? '-' : '') + chosen;
}

/** The decimal form, invariant, in at most `limit` characters, or `undefined` where none fits. */
function decimalText(digits: string, exponent: number, limit: number): string | undefined {
    const integerDigits = exponent >= 0 ? exponent + 1 : 1;
    if (integerDigits > limit) return undefined;
    const fraction = Math.max(0, limit - integerDigits - 1);
    const [rounded, at] = round(digits, exponent, exponent + 1 + fraction);
    if (rounded.length === 0) return '0';
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

/** The scientific form, invariant, in at most `limit` characters; eleven always hold one. */
function scientificText(digits: string, exponent: number, limit: number): string {
    for (let mantissaFraction = Math.max(0, limit - 6); mantissaFraction >= 0; mantissaFraction--) {
        const [rounded, at] = round(digits, exponent, 1 + mantissaFraction);
        const mantissa = rounded.replace(/0+$/, '');
        let text = mantissa.charAt(0);
        if (mantissa.length > 1) text += `.${mantissa.slice(1)}`;
        const power = Math.abs(at);
        text += (at < 0 ? 'E-' : 'E+') + power.toString().padStart(power >= 100 ? 3 : 2, '0');
        if (text.length <= limit) return text;
    }
    throw new Error(`Internal: no scientific form of ${digits}E${exponent} fits ${limit} characters.`);
}

/** `digits` (the first at 10^`exponent`) rounded half away from zero to `keep` significant digits: the
 * digits kept, and the power of ten of the first. Empty when the number rounds to zero. */
function round(digits: string, exponent: number, keep: number): [string, number] {
    if (keep >= digits.length) return [digits, exponent];
    if (keep < 0) return ['', exponent];
    if (keep === 0) return digits.charCodeAt(0) >= 53 ? ['1', exponent + 1] : ['', exponent];
    const kept = digits.slice(0, keep).split('');
    if (digits.charCodeAt(keep) >= 53) {
        let i = keep - 1;
        while (i >= 0 && kept[i] === '9') {
            kept[i] = '0';
            i--;
        }
        if (i < 0) return [`1${kept.join('')}`, exponent + 1];
        kept[i] = String.fromCharCode((kept[i] ?? '0').charCodeAt(0) + 1); // oxlint-disable-line ex-grid/no-stand-in-default -- i lies within kept, checked by the loop above
    }
    return [kept.join(''), exponent];
}
