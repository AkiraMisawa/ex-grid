// The functions a platform's math library answers, each its own last bit — V8's, glibc's and Apple's differ —
// answered here as the exact value rounded once to the nearest double, ties to even, so that every browser and
// the server give one double (C#'s PortableMath.cs). Each is computed in `BigReal` to 128 bits and rounded when
// every number within its error rounds alike; otherwise to twice the bits, and so on (Ziv's strategy). e^x and
// ln x, which are asked far more often, keep `CorrectlyRounded`'s double-double path.
import * as big from './big-real';
import { BigReal } from './big-real';
import { bitLength, decompose, exp as correctlyRoundedExp, log as correctlyRoundedLog, roundToDouble, tryLog10 } from './correctly-rounded';

/** The bits at the end of a result its computation may have got wrong: far more than any function here loses. */
const slackBits = 24;

/** The double nearest the value `compute` approximates to the bits it is asked for. */
function rounded(compute: (precision: number) => BigReal): number {
    for (let precision = 128; precision <= 4096; precision *= 2) {
        const value = compute(precision);
        if (value.m === 0n) return 0;
        const slack = ((value.m < 0n ? -value.m : value.m) >> BigInt(precision - slackBits)) + 1n;
        const low = roundToDouble(value.m - slack, value.e);
        const high = roundToDouble(value.m + slack, value.e);
        if (low === high) return low;
    }
    throw new Error('Internal: a function lies too close to a rounding boundary to round at 4096 bits.');
}

export const exp = correctlyRoundedExp;
export const log = correctlyRoundedLog;

/** log10 x for x above 0; 10^k is k exactly. */
export function log10(x: number): number {
    if (!(x > 0) || !Number.isFinite(x)) throw new RangeError(`Internal: log10 is asked of ${String(x)}.`);
    const fast = tryLog10(x);
    return fast === undefined ? rounded(p => big.log10(BigReal.of(x), p)) : fast;
}

export function atan(x: number): number {
    return x === 0 ? x : rounded(p => big.atan(BigReal.of(x), p));
}

/** atan2(y, x) as `Math.atan2` answers it at a 0: (0, x > 0) is 0, (0, x < 0) is pi, (y, 0) is pi/2 with y's sign. */
export function atan2(y: number, x: number): number {
    if (y === 0) return x >= 0 && !Object.is(x, -0) ? y : Object.is(y, -0) ? -rounded(p => big.pi(p)) : rounded(p => big.pi(p));
    if (x === 0) return rounded(p => big.pi(p).scaled(-1)) * Math.sign(y);
    return rounded(p => big.atan2(BigReal.of(y), BigReal.of(x), p));
}

/** asin x for |x| at most 1. */
export function asin(x: number): number {
    if (Math.abs(x) === 1) return rounded(p => big.pi(p).scaled(-1)) * x;
    return x === 0 ? x : rounded(p => big.asin(BigReal.of(x), p));
}

/** acos x for |x| at most 1. */
export function acos(x: number): number {
    if (x === 1) return 0;
    if (x === -1) return rounded(p => big.pi(p));
    return rounded(p => big.acos(BigReal.of(x), p));
}

/** The magnitude past which sinh and cosh overflow every double. */
const hyperbolicOverflow = 711;

export function sinh(x: number): number {
    if (x === 0) return x;
    if (Math.abs(x) > hyperbolicOverflow) return x > 0 ? Infinity : -Infinity;
    return rounded(p => big.sinh(BigReal.of(x), p));
}

export function cosh(x: number): number {
    if (Math.abs(x) > hyperbolicOverflow) return Infinity;
    return rounded(p => big.cosh(BigReal.of(x), p));
}

/** tanh x; past 22 in magnitude it lies within 2^-63 of 1 and rounds to it. */
export function tanh(x: number): number {
    if (x === 0) return x;
    if (Math.abs(x) >= 22) return Math.sign(x);
    return rounded(p => big.tanh(BigReal.of(x), p));
}

export function asinh(x: number): number {
    return x === 0 ? x : rounded(p => big.asinh(BigReal.of(x), p));
}

/** acosh x for x at least 1. */
export function acosh(x: number): number {
    return rounded(p => big.acosh(BigReal.of(x), p));
}

/** atanh x for |x| below 1. */
export function atanh(x: number): number {
    return x === 0 ? x : rounded(p => big.atanh(BigReal.of(x), p));
}

/**
 * `a`^`b` for a whole number `b`, as `Math.pow` answers it at 0, 1 and past the doubles' range, and otherwise
 * the exact power rounded once: counted exactly where |b| is at most 64, and as e^(b ln |a|) beyond.
 */
export function powWhole(a: number, b: number): number {
    if (!Number.isInteger(b)) throw new RangeError(`Internal: ${String(b)} is no whole power.`);
    if (b === 0) return 1;
    // One operation each, which IEEE arithmetic rounds correctly: a square, a reciprocal, and a power of ten whose 10^|b| is exact.
    if (b === 1) return a;
    if (b === 2) return a * a;
    if (b === -1) return 1 / a;
    if (Math.abs(a) === 10 && Math.abs(b) <= 22) {
        const exact = Number(`1e${String(Math.abs(b))}`);
        const tenth = b > 0 ? exact : 1 / exact;
        return a < 0 && Math.abs(b % 2) === 1 ? -tenth : tenth;
    }
    const odd = Math.abs(b % 2) === 1;
    const negative = a < 0 && odd;
    if (a === 0) return b > 0 ? (Object.is(a, -0) && odd ? -0 : 0) : (Object.is(a, -0) && odd ? -Infinity : Infinity);
    const magnitude = Math.abs(a);
    if (magnitude === 1) return negative ? -1 : 1;
    // Past the doubles' range at once: 2^1100 overflows and 2^-1180 underflows every double. The platform's log2 only
    // places the number on one side of a line far from the range's ends, which its last bit cannot move.
    const log2 = Math.log2(magnitude) * b;
    if (log2 > 1100) return negative ? -Infinity : Infinity;
    if (log2 < -1180) return negative ? -0 : 0;
    const { significand, exponent } = decompose(magnitude);
    const twos = trailingZeros(significand);
    const oddPart = significand >> BigInt(twos);
    const power = exponent + twos;
    let result: number;
    if (oddPart === 1n) {
        result = roundToDouble(1n, power * b);
    } else if (Math.abs(b) <= 64) {
        const n = oddPart ** BigInt(Math.abs(b));
        if (b > 0) {
            result = roundToDouble(n, power * b);
        } else {
            // 2^(power b) / n with 64 bits more than a double holds, and a sticky bit for any remainder.
            const shift = bitLength(n) + 64;
            const numerator = 1n << BigInt(shift);
            const quotient = numerator / n;
            const sticky = numerator % n === 0n ? 0n : 1n;
            result = roundToDouble((quotient << 1n) | sticky, power * b - shift - 1);
        }
    } else {
        result = rounded(p => big.exp(big.ln(BigReal.of(magnitude), p + 16).times(BigReal.whole(b), p + 16), p));
    }
    return negative ? -result : result;
}

function trailingZeros(n: bigint): number {
    let count = 0;
    let rest = n;
    while ((rest & 1n) === 0n) {
        rest >>= 1n;
        count++;
    }
    return count;
}
