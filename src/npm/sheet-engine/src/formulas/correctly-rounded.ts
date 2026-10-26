// e^x and ln x, each the exact value rounded once to the nearest double, ties to even: the same double on every
// platform (C#'s CorrectlyRounded.cs). PV, FV and XNPV raise 1+rate to a number that is not whole by these.
// Each is computed in double-double arithmetic (`Wide`), and rounded when no rounding boundary lies within its
// error bound (2^-90); otherwise it is computed again in 256-bit fixed point.
import { bitDecrement, bitIncrement, bitsDouble, doubleBits, roundHalfToEven, scaleB } from '../dotnet';
import { Wide } from './wide';

/** The bits of a whole number above 0 (`BigInteger.GetBitLength`); 0 for 0. */
export function bitLength(value: bigint): number {
    if (value < 0n) throw new Error('Internal: the bit length of a negative number is asked.');
    if (value === 0n) return 0;
    const hex = value.toString(16);
    return (hex.length - 1) * 4 + Number.parseInt(hex.charAt(0), 16).toString(2).length;
}

/** The relative error the double-double path is trusted to. */
const bound = 2 ** -90;
const inverseLn2 = Math.LOG2E;
const minNormal = 2.2250738585072014E-308;
const sqrt2 = Math.sqrt(2);

// 256-bit fixed point: a BigInt n stands for n * 2^-256.
const scale = 256;
const fixedOne = 1n << BigInt(scale);

interface Constants {
    readonly fixedLn2: bigint;
    /** ln 2 as three doubles: the first 42 bits, so that a whole number below 2^11 times it is exact, then the next 53, then the next. */
    readonly ln2High: number;
    readonly ln2Low: number;
    readonly ln2Lowest: number;
    /** 2/(2j+1), j = 0 to 24. */
    readonly atanhCoefficients: readonly Wide[];
}

let constants: Constants | undefined;

/** The constants, computed when first asked for, as C#'s static constructor computes them. */
function constantsOf(): Constants {
    if (constants !== undefined) return constants;
    const fixedLn2 = atanh2(1n, 3n);
    const high = fixedLn2 >> BigInt(scale - 42);
    const ln2High = scaleB(Number(high), -42);
    let rest = fixedLn2 - (high << BigInt(scale - 42));
    const ln2Low = roundToDouble(rest, -scale);
    rest -= exactly(ln2Low, scale);
    const ln2Lowest = roundToDouble(rest, -scale);
    const atanhCoefficients: Wide[] = [];
    for (let j = 0; j < 25; j++) atanhCoefficients.push(new Wide(2).dividedBy(2 * j + 1));
    constants = { fixedLn2, ln2High, ln2Low, ln2Lowest, atanhCoefficients };
    return constants;
}

/** e^`x`, correctly rounded. */
export function exp(x: number): number {
    if (Number.isNaN(x)) return x;
    if (x === 0) return 1;
    if (Math.abs(x) > 708) return expExact(x); // overflow, and results near or below the smallest normal double
    const { ln2High, ln2Low, ln2Lowest } = constantsOf();
    const k = roundHalfToEven(x * inverseLn2);
    // x - k*ln2 as a double-double: k*ln2High is exact, and so is the subtraction.
    const reduced = new Wide(x - k * ln2High).minus(Wide.product(k, ln2Low)).minus(new Wide(k * ln2Lowest));
    // e^r = (e^(r/16))^16, e^(r/16) by its Taylor series to 2^-113.
    const s = reduced.times(1 / 16);
    let power = new Wide(1);
    for (let j = 14; j >= 1; j--) power = new Wide(1).plus(s.times(power).dividedBy(j));
    for (let i = 0; i < 4; i++) power = power.times(power);
    const rounded = roundedOf(power);
    return rounded === undefined ? expExact(x) : scaleB(rounded, k);
}

/** ln `x`, correctly rounded. */
export function log(x: number): number {
    if (Number.isNaN(x) || x < 0) return NaN;
    if (x === 0) return -Infinity;
    if (x === Infinity) return x;
    if (x === 1) return 0;
    if (x < minNormal) return logExact(x);
    const { ln2High, ln2Low, ln2Lowest, atanhCoefficients } = constantsOf();
    // x = m * 2^e with m in [sqrt(1/2), sqrt(2)]; ln m = 2 atanh(s), s = (m-1)/(m+1), |s| < 0.172.
    const bits = doubleBits(x);
    let e = Number((bits >> 52n) & 0x7FFn) - 1023;
    let m = bitsDouble((bits & 0xFFFFFFFFFFFFFn) | 0x3FF0000000000000n);
    if (m > sqrt2) {
        m /= 2;
        e++;
    }
    const s = new Wide(m - 1).dividedBy(Wide.sum(m, 1)); // m - 1 is exact (Sterbenz)
    const t = s.times(s);
    let series = atanhCoefficients[atanhCoefficients.length - 1] as Wide; // oxlint-disable-line typescript/no-unsafe-type-assertion -- 25 coefficients
    for (let j = atanhCoefficients.length - 2; j >= 0; j--) series = (atanhCoefficients[j] as Wide).plus(t.times(series)); // oxlint-disable-line typescript/no-unsafe-type-assertion -- j is within them
    const logarithm = s.times(series).plus(new Wide(e * ln2High)).plus(Wide.product(e, ln2Low)).plus(new Wide(e * ln2Lowest));
    const rounded = roundedOf(logarithm);
    return rounded === undefined ? logExact(x) : rounded;
}

/** The double nearest `value`, when every number within `bound` of it rounds to that same double; `undefined` where a boundary lies so close that the error could cross it. */
function roundedOf(value: Wide): number | undefined {
    const neighbour = value.low >= 0 ? bitIncrement(value.high) : bitDecrement(value.high);
    const halfGap = Math.abs(neighbour - value.high) / 2;
    const error = bound * Math.abs(value.high);
    return Math.abs(value.low) + error < halfGap && error < halfGap / 2 ? value.high : undefined;
}

/** e^`x` computed in 256-bit fixed point and rounded once. */
export function expExact(x: number): number {
    if (Number.isNaN(x)) return x;
    if (x === Infinity || x > 710) return Infinity;
    if (x === -Infinity || x < -746) return 0;
    if (Math.abs(x) < 2 ** -60) return 1; // e^x lies within 2^-60 of 1, which is the nearest double
    const { fixedLn2 } = constantsOf();
    const f = exactly(x, scale);
    const k = roundHalfToEven(x / Math.LN2);
    const r = f - BigInt(k) * fixedLn2;
    let sum = 0n;
    let term = fixedOne;
    for (let j = 1n; term !== 0n; j++) {
        sum += term;
        term = (term * r) / (fixedOne * j);
    }
    return roundToDouble(sum, k - scale);
}

/** ln `x` computed in 256-bit fixed point and rounded once. */
export function logExact(x: number): number {
    if (Number.isNaN(x) || x < 0) return NaN;
    if (x === 0) return -Infinity;
    if (x === Infinity) return x;
    const { fixedLn2 } = constantsOf();
    const { significand, exponent } = decompose(x);
    const length = bitLength(significand);
    const k = exponent + length - 1; // x = y * 2^k, y in [1, 2)
    const top = 1n << BigInt(length - 1);
    return roundToDouble(BigInt(k) * fixedLn2 + atanh2(significand - top, significand + top), -scale);
}

/** 2 atanh(n/d) = ln((d+n)/(d-n)) in fixed point, for 0 <= n/d <= 1/3. */
function atanh2(n: bigint, d: bigint): bigint {
    const z = (n << BigInt(scale)) / d;
    const z2 = (z * z) / fixedOne;
    let term = z;
    let sum = 0n;
    for (let k = 1n; term !== 0n; k += 2n) {
        sum += term / k;
        term = (term * z2) / fixedOne;
    }
    return 2n * sum;
}

/** A finite double as significand * 2^exponent, the significand a whole number. */
export function decompose(x: number): { significand: bigint; exponent: number } {
    const bits = doubleBits(Math.abs(x));
    const biased = Number((bits >> 52n) & 0x7FFn);
    const fraction = bits & 0xFFFFFFFFFFFFFn;
    return biased === 0 ? { significand: fraction, exponent: -1074 } : { significand: fraction | (1n << 52n), exponent: biased - 1075 };
}

/** `x` times 2^`shift`, truncated toward zero to a whole number. */
function exactly(x: number, shift: number): bigint {
    const { significand, exponent } = decompose(x);
    const scaled = exponent + shift >= 0 ? significand << BigInt(exponent + shift) : significand >> BigInt(-(exponent + shift));
    return x < 0 ? -scaled : scaled;
}

/** The double nearest `m` * 2^`e`, ties to even, subnormal and infinite results included. */
export function roundToDouble(m: bigint, e: number): number {
    if (m === 0n) return 0;
    const negative = m < 0n;
    const a = negative ? -m : m;
    const length = bitLength(a);
    const top = e + length - 1; // a * 2^e lies in [2^top, 2^(top+1))
    if (top > 1023) return negative ? -Infinity : Infinity;
    const precision = top >= -1022 ? 53 : 53 - (-1022 - top);
    const shift = length - precision;
    let kept: bigint;
    if (shift <= 0) kept = a;
    else {
        kept = a >> BigInt(shift);
        const rest = a - (kept << BigInt(shift));
        const half = 1n << BigInt(shift - 1);
        if (rest > half || (rest === half && (kept & 1n) === 1n)) kept += 1n;
    }
    const result = scaleB(Number(kept), e + Math.max(shift, 0));
    return negative ? -result : result;
}
