// C#'s decimal, as far as a Selection Summary reads one (ADR-0130, ADR-0350 §16): a whole number of at most
// 96 bits and a scale of 0 to 28 decimal places. A Summary's Sum is computed in it, exactly, by counting
// decimal places, as C#'s decimal sums, never by adding doubles, which would show a total a money grid
// cannot stand behind. Each operation is .NET's: a result too wide for 96 bits gives up decimal places,
// rounded half to even, and one whose whole part is too wide overflows.

import { decimalPartsOf, tenTo } from '../query/numbers';

/** A decimal value, `mantissa` × 10^-`scale`. A sum keeps the larger scale of its two terms, trailing zeros
 * and all, as C#'s does, since the double C# converts it to depends on the scale; everything else this
 * module makes is canonical: no trailing zero after the point, and 0 is `0n` at scale 0. */
export interface ExactDecimal {
    readonly mantissa: bigint;
    readonly scale: number;
}

const limit = 1n << 96n;
const maxScale = 28;

/** How a tie is rounded: to the even digit, as `Math.Round` does, or away from zero, as .NET's format
 * strings write a decimal. */
export type Midpoint = 'toEven' | 'awayFromZero';

/** The decimal C# reads a value as, or `undefined` where no decimal holds it: a `number` as .NET converts a
 * double (fifteen significant digits), a `bigint` exactly while a decimal can hold it. */
export function decimalOf(value: number | bigint): ExactDecimal | undefined {
    if (typeof value === 'bigint') return value >= limit || value <= -limit ? undefined : canonical(value, 0);
    const parts = decimalPartsOf(value);
    if (parts === undefined) return undefined;
    // Trailing zeros come off while the digits are still a safe integer, as .NET's VarDecFromR8 takes them
    // off, which spares a bigint division per zero on a path a whole column of numbers takes.
    let { digits, power } = parts;
    while (power > 0 && digits % 10 === 0) {
        digits /= 10;
        power -= 1;
    }
    const magnitude = power >= 0 ? BigInt(digits) : BigInt(digits) * tenToThe(-power);
    return Object.freeze({ mantissa: parts.negative ? -magnitude : magnitude, scale: digits === 0 ? 0 : Math.max(power, 0) });
}

/** A whole count as a decimal. */
export function decimalOfCount(count: number): ExactDecimal {
    if (!Number.isSafeInteger(count) || count < 0) throw new RangeError(`A count is a whole number from 0, not ${count}.`);
    return canonical(BigInt(count), 0);
}

/** `a` + `b` as C#'s decimal adds them, at the larger of their scales, or `undefined` where C# overflows. */
export function decimalAdd(a: ExactDecimal, b: ExactDecimal): ExactDecimal | undefined {
    const scale = Math.max(a.scale, b.scale);
    return fitted(scaledTo(a, scale) + scaledTo(b, scale), scale);
}

/** `a` ÷ `count` as C#'s decimal divides by a whole count: as many decimal places as 96 bits hold, to 28,
 * the last rounded half to even. */
export function decimalDivide(a: ExactDecimal, count: number): ExactDecimal {
    if (!Number.isSafeInteger(count) || count <= 0) throw new RangeError(`A decimal is divided by a count from 1, not ${count}.`);
    const divisor = BigInt(count);
    for (let scale = maxScale; scale >= a.scale; scale--) {
        const quotient = roundedQuotient(scaledTo(a, scale), divisor);
        if (abs(quotient) < limit) return canonical(quotient, scale);
    }
    // The quotient at `a`'s own scale is no larger than `a`, which fits.
    throw new Error(`Internal: ${decimalText(a)} ÷ ${count} fits at no scale.`);
}

/** -1, 0 or 1 as `a` is below, equal to or above `b`. */
export function decimalCompare(a: ExactDecimal, b: ExactDecimal): number {
    const scale = Math.max(a.scale, b.scale);
    const left = scaledTo(a, scale);
    const right = scaledTo(b, scale);
    if (left < right) return -1;
    return left > right ? 1 : 0;
}

/** `value` rounded to `places` decimal places, half to even, as C#'s `Math.Round(decimal, places)`, or away
 * from zero, as a .NET format string rounds one. A value already within `places` is returned as it is. */
export function decimalRound(value: ExactDecimal, places: number, midpoint: Midpoint = 'toEven'): ExactDecimal {
    if (!Number.isInteger(places) || places < 0 || places > maxScale) throw new RangeError(`A decimal rounds to 0 to 28 places, not ${places}.`);
    if (value.scale <= places) return value;
    return canonical(roundedQuotient(value.mantissa, tenToThe(value.scale - places), midpoint), places);
}

/** The double C# converts a decimal to: .NET's VarR8FromDec, which adds the two halves of the mantissa and
 * divides by the power of ten in double arithmetic, so the last bit can differ from the nearest double. */
export function decimalToNumber(value: ExactDecimal): number {
    const magnitude = abs(value.mantissa);
    const low = Number(magnitude & ((1n << 64n) - 1n));
    const high = Number(magnitude >> 64n);
    const result = (low + high * 18446744073709551616) / tenTo(value.scale);
    return value.mantissa < 0n ? -result : result;
}

/** The decimal's text as C# writes it in the invariant culture: its digits, a point and its places. */
export function decimalText(value: ExactDecimal): string {
    const digits = abs(value.mantissa).toString().padStart(value.scale + 1, '0');
    const sign = value.mantissa < 0n ? '-' : '';
    if (value.scale === 0) return sign + digits;
    return `${sign}${digits.slice(0, -value.scale)}.${digits.slice(-value.scale)}`;
}

/** The value without trailing zeros, as C#'s canonical form writes it. */
export function decimalCanonical(value: ExactDecimal): ExactDecimal {
    return canonical(value.mantissa, value.scale);
}

function canonical(mantissa: bigint, scale: number): ExactDecimal {
    let digits = mantissa;
    let places = scale;
    while (places > 0 && digits % 10n === 0n) {
        digits /= 10n;
        places -= 1;
    }
    return Object.freeze({ mantissa: digits, scale: digits === 0n ? 0 : places });
}

/** An exact result held in 96 bits: decimal places given up, rounded once, half to even, until it fits,
 * or `undefined` where even the whole part does not. Trailing zeros are kept, as C# keeps them. */
function fitted(mantissa: bigint, scale: number): ExactDecimal | undefined {
    for (let dropped = 0; dropped <= scale; dropped++) {
        const kept = dropped === 0 ? mantissa : roundedQuotient(mantissa, tenToThe(dropped));
        if (abs(kept) < limit) return Object.freeze({ mantissa: kept, scale: scale - dropped });
    }
    return undefined;
}

function scaledTo(value: ExactDecimal, scale: number): bigint {
    return scale === value.scale ? value.mantissa : value.mantissa * tenToThe(scale - value.scale);
}

let bigPowersOfTen: readonly bigint[] | undefined;

/** 10^n as a bigint, for n to 28, from a table: every scale a decimal has, and every difference of two. */
function tenToThe(power: number): bigint {
    bigPowersOfTen ??= Object.freeze(Array.from({ length: maxScale + 1 }, (_, n) => 10n ** BigInt(n)));
    const ten = bigPowersOfTen[power];
    if (ten === undefined) throw new Error(`Internal: 10^${power.toString()} is outside the table.`);
    return ten;
}

/** `numerator` ÷ `divisor` (positive), a tie rounded to the even quotient or away from zero. */
export function roundedQuotient(numerator: bigint, divisor: bigint, midpoint: Midpoint = 'toEven'): bigint {
    const quotient = numerator / divisor;
    const twice = abs(numerator % divisor) * 2n;
    if (twice < divisor || (twice === divisor && midpoint === 'toEven' && quotient % 2n === 0n)) return quotient;
    return numerator < 0n ? quotient - 1n : quotient + 1n;
}

function abs(value: bigint): bigint {
    return value < 0n ? -value : value;
}
