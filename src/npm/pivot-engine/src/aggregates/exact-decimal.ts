// C#'s decimal, as far as the engine computes in one (ADR-0060, ADR-0370 §3): a whole number of at most
// 96 bits and a scale of 0 to 28 decimal places, the Snapshot's Decimal. Money is summed in it exactly, by
// counting decimal places, never by adding doubles. Each operation is .NET's: a result too wide for 96
// bits gives up decimal places, rounded half to even, and one whose whole part is too wide overflows, which
// the caller turns into Excel's double. The Script Core's exact-decimal.ts says the same; the packages
// share no code, so each carries its own.

import type { SnapshotDecimal } from '@ex-grid/data';
import { elementAt } from '../must';

const limit = 1n << 96n;
const maxScale = 28;

/** Zero, canonical. */
export const decimalZero: SnapshotDecimal = Object.freeze({ mantissa: 0n, scale: 0 });

// 10^k for k = 0 to 28 as doubles, the table .NET's VarR8FromDec divides by; every one to 1e22 is exact.
const doublePowers: readonly number[] = Object.freeze([
    1e0, 1e1, 1e2, 1e3, 1e4, 1e5, 1e6, 1e7, 1e8, 1e9, 1e10, 1e11, 1e12, 1e13, 1e14,
    1e15, 1e16, 1e17, 1e18, 1e19, 1e20, 1e21, 1e22, 1e23, 1e24, 1e25, 1e26, 1e27, 1e28,
]);

/** 10^`power` as a double, for a power from 0 to 28. */
export function doubleTenTo(power: number): number {
    return elementAt(doublePowers, power);
}

let bigPowers: readonly bigint[] | undefined;

/** 10^`power` as a bigint, for a power from 0 to 28, from a table: every scale a decimal has, and every
 * difference of two. */
function tenToThe(power: number): bigint {
    bigPowers ??= Object.freeze(Array.from({ length: maxScale + 1 }, (_, n) => 10n ** BigInt(n)));
    return elementAt(bigPowers, power);
}

/** A whole number as a decimal, which the caller knows is narrower than 96 bits. */
export function decimalOfWhole(value: bigint): SnapshotDecimal {
    if (value >= limit || value <= -limit) throw new Error(`Internal: ${value} is wider than a decimal.`);
    return Object.freeze({ mantissa: value, scale: 0 });
}

/** `a` + `b` as C#'s decimal adds them, at the larger of their scales, or `undefined` where C# overflows. */
export function decimalAdd(a: SnapshotDecimal, b: SnapshotDecimal): SnapshotDecimal | undefined {
    const scale = Math.max(a.scale, b.scale);
    return fitted(scaledTo(a, scale) + scaledTo(b, scale), scale);
}

/** `a` ÷ `count` as C#'s decimal divides by a whole count: as many decimal places as 96 bits hold, to 28,
 * the last rounded half to even. */
export function decimalDivide(a: SnapshotDecimal, count: number): SnapshotDecimal {
    if (!Number.isSafeInteger(count) || count <= 0) throw new RangeError(`A decimal is divided by a count from 1, not ${count}.`);
    const divisor = BigInt(count);
    for (let scale = maxScale; scale >= a.scale; scale--) {
        const quotient = roundedQuotient(scaledTo(a, scale), divisor);
        if (abs(quotient) < limit) return decimalCanonical({ mantissa: quotient, scale });
    }
    // The quotient at `a`'s own scale is no larger than `a`, which fits.
    throw new Error(`Internal: a decimal ÷ ${count} fits at no scale.`);
}

/** -1, 0 or 1 as `a` is below, equal to or above `b`. */
export function decimalCompare(a: SnapshotDecimal, b: SnapshotDecimal): number {
    const scale = Math.max(a.scale, b.scale);
    const left = scaledTo(a, scale);
    const right = scaledTo(b, scale);
    if (left < right) return -1;
    return left > right ? 1 : 0;
}

/** Whether two decimals are one value, whatever their scales. */
export function decimalEquals(a: SnapshotDecimal, b: SnapshotDecimal): boolean {
    return decimalCompare(a, b) === 0;
}

/** The double C# converts a decimal to: .NET's VarR8FromDec, which adds the two halves of the mantissa and
 * divides by the power of ten in double arithmetic, so the last bit can differ from the nearest double. */
export function decimalToNumber(value: SnapshotDecimal): number {
    const magnitude = abs(value.mantissa);
    const low = Number(magnitude & ((1n << 64n) - 1n));
    const high = Number(magnitude >> 64n);
    const result = (low + high * 18446744073709551616) / doubleTenTo(value.scale);
    return value.mantissa < 0n ? -result : result;
}

/** The value without trailing zeros, as C#'s canonical form writes it. */
export function decimalCanonical(value: SnapshotDecimal): SnapshotDecimal {
    let digits = value.mantissa;
    let places = value.scale;
    while (places > 0 && digits % 10n === 0n) {
        digits /= 10n;
        places -= 1;
    }
    if (digits === value.mantissa && places === value.scale && Object.isFrozen(value)) return value;
    return Object.freeze({ mantissa: digits, scale: digits === 0n ? 0 : places });
}

/**
 * An exact sum, a whole number of any width at a power of ten, as the decimal C# finishes it into: the one
 * form of the value, without trailing zeros, or `undefined` where no decimal holds it — more than 96 bits
 * once the trailing zeros are gone, or more than 28 places.
 */
export function decimalOfSum(value: bigint, scale: number): SnapshotDecimal | undefined {
    const canonical = canonicalWide(value, scale);
    if (canonical.scale > maxScale || abs(canonical.mantissa) >= limit) return undefined;
    return Object.freeze(canonical);
}

/** The double of an exact sum no decimal holds: its one form's whole number divided by its power of ten,
 * as C# divides an Int128 by `Math.Pow(10, scale)`. */
export function doubleOfSum(value: bigint, scale: number): number {
    const canonical = canonicalWide(value, scale);
    return Number(canonical.mantissa) / doubleTenTo(canonical.scale);
}

function canonicalWide(value: bigint, scale: number): { mantissa: bigint; scale: number } {
    if (value === 0n) return { mantissa: 0n, scale: 0 };
    let digits = value;
    let places = scale;
    while (places > 0 && digits % 10n === 0n) {
        digits /= 10n;
        places -= 1;
    }
    return { mantissa: digits, scale: places };
}

/** `value`'s whole number at `scale`, which is at least its own. */
export function scaledTo(value: SnapshotDecimal, scale: number): bigint {
    return scale === value.scale ? value.mantissa : value.mantissa * tenToThe(scale - value.scale);
}

/** An exact result held in 96 bits: decimal places given up, rounded once, half to even, until it fits,
 * or `undefined` where even the whole part does not. Trailing zeros are kept, as C# keeps them. */
function fitted(mantissa: bigint, scale: number): SnapshotDecimal | undefined {
    for (let dropped = 0; dropped <= scale; dropped++) {
        const kept = dropped === 0 ? mantissa : roundedQuotient(mantissa, tenToThe(dropped));
        if (abs(kept) < limit) return Object.freeze({ mantissa: kept, scale: scale - dropped });
    }
    return undefined;
}

/** `numerator` ÷ `divisor` (positive), a tie rounded to the even quotient. */
function roundedQuotient(numerator: bigint, divisor: bigint): bigint {
    const quotient = numerator / divisor;
    const twice = abs(numerator % divisor) * 2n;
    if (twice < divisor || (twice === divisor && quotient % 2n === 0n)) return quotient;
    return numerator < 0n ? quotient - 1n : quotient + 1n;
}

function abs(value: bigint): bigint {
    return value < 0n ? -value : value;
}
