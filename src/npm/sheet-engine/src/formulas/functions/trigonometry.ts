// LOG and the trigonometric and hyperbolic functions (C#'s Functions.Trigonometry.cs), each to Microsoft's
// documentation of it (ticket 07) and to what the Windows runs observed of Excel. Angles are in radians.
import { fusedProductError, roundHalfToEven } from '../../dotnet';
import type { FunctionCall } from '../function-definition';
import { Operand } from '../operand';
import { numberArgument, numberResult } from './shared';

/**
 * The size of argument, 2^27, at and above which SIN, COS, TAN, COT, CSC and SEC are `#NUM!`. Microsoft
 * documents it for COT, CSC and SEC; the seventeenth Windows run observed it for all six (SIN-005, COS-005,
 * TAN-005, COT-004, CSC-002, SEC-003). COTH, CSCH and SECH, for which Microsoft documents it too, answer past it
 * in Excel (COTH-002, CSCH-002, SECH-003), so they do here.
 */
const trigonometricLimit = 134_217_728;

// ---- LOG ---------------------------------------------------------------------------------------------------

/**
 * LOG: to `base`, 10 when left out. A number or base not above 0 is `#NUM!`; base 1, whose logarithm is 0, is
 * `#DIV/0!`. Base 10 goes through LOG10's own logarithm, so `LOG(1000)` is 3 as LOG10's is.
 */
export function log(call: FunctionCall): Operand {
    const number = numberArgument(call, 0);
    if (number instanceof Operand) return number;
    let base = 10;
    if (call.has(1)) {
        const given = numberArgument(call, 1);
        if (given instanceof Operand) return given;
        base = given;
    }
    if (number <= 0 || base <= 0) return Operand.ofError('#NUM!');
    if (base === 1) return Operand.ofError('#DIV/0!');
    return numberResult(base === 10 ? Math.log10(number) : Math.log(number) / Math.log(base));
}

// ---- SIN, COS, TAN, COT, CSC, SEC ----------------------------------------------------------------------------

/** SIN: the sine of the angle reduced as Excel reduces it (`reduced`). */
export function sin(call: FunctionCall): Operand {
    return angle(call, n => reduced(n).sine.value, false);
}

/** COS: the cosine of the angle reduced as Excel reduces it (`reduced`). */
export function cos(call: FunctionCall): Operand {
    return angle(call, n => reduced(n).cosine.value, false);
}

/** TAN: the sine over the cosine of the reduced angle, rounded once (`reduced`). */
export function tan(call: FunctionCall): Operand {
    return angle(call, tangent, false);
}

/** COT: 1/TAN; `COT(0)` is `#DIV/0!`, as documented. */
export function cot(call: FunctionCall): Operand {
    return angle(call, n => 1 / tangent(n), true);
}

/** CSC: 1/SIN, divided as doubles (CSC-001); at 0, where SIN is 0, `#DIV/0!`. */
export function csc(call: FunctionCall): Operand {
    return angle(call, n => 1 / reduced(n).sine.value, true);
}

/** SEC: 1/COS, divided as doubles. */
export function sec(call: FunctionCall): Operand {
    return angle(call, n => 1 / reduced(n).cosine.value, false);
}

/** A function of an angle whose absolute value must be below 2^27 (`trigonometricLimit`); at or past it, `#NUM!`. Where the function has a pole at 0, 0 is `#DIV/0!`. */
function angle(call: FunctionCall, fn: (n: number) => number, zeroIsDiv0: boolean): Operand {
    const number = numberArgument(call, 0);
    if (number instanceof Operand) return number;
    if (Math.abs(number) >= trigonometricLimit) return Operand.ofError('#NUM!');
    if (zeroIsDiv0 && number === 0) return Operand.ofError('#DIV/0!');
    return numberResult(fn(number));
}

/** TAN, and COT's divisor: the reduced sine over the reduced cosine, rounded once. */
function tangent(angleGiven: number): number {
    const { sine, cosine } = reduced(angleGiven);
    return sine.dividedBy(cosine).value;
}

/**
 * The double nearest pi/2, the high part of the pi/2 Excel reduces an angle by: pi rounded to a 64-bit
 * significand, the x87's `FLDPI` (`0xC90FDAA22168C235` × 2^-62), halved, is this plus `halfPiLow` exactly.
 * `SIN(PI())` is the difference between that pi and the double `PI()` gives, 1.22514845490862E-16 (SIN-004),
 * where the true sine of that double is 1.2246467991473532E-16; the seventeenth Windows run observed it, with
 * `COS(PI()/2)` and `TAN(PI()/2)` (ADR-0047; `spikes/exsheet-trig-reduction/` holds the experiment).
 */
const halfPiHigh = Math.PI / 2;

/** The low part of Excel's pi/2, 565 × 2^-63 (6.1257422745431E-17). */
const halfPiLow = 565 * 2 ** -63;

/**
 * The sine and cosine of `angle` (below 2^27 in absolute value), as Excel answers them: the angle less the
 * nearest whole number `k` of Excel's pi/2 (`halfPiHigh` + `halfPiLow`), exactly, leaves `r` within about pi/4
 * of 0; the sine and cosine of `r`, to about 100 bits, give those of the angle by the quadrant `k` names. Each
 * is rounded to a double once, by `DoubleDouble.value`. Nothing here is the platform's `Math.sin`, so every
 * runtime gives the same doubles.
 */
function reduced(angleGiven: number): { sine: DoubleDouble; cosine: DoubleDouble } {
    const k = roundHalfToEven(angleGiven / halfPiHigh);
    // k·halfPiHigh = p + pRest exactly. angle − p is exact (the two are within a factor of 2 of each
    // other whenever k is not 0), and so is its difference with pRest: both are multiples of 2^-53
    // and the result is below 1. k·halfPiLow is exact (k is below 2^27, 565 below 2^10), so r is
    // angle − k·(halfPiHigh + halfPiLow) to the last bit.
    const p = k * halfPiHigh;
    const pRest = fusedProductError(k, halfPiHigh, -p);
    const r = DoubleDouble.sum(angleGiven - p - pRest, -(k * halfPiLow));
    const r2 = r.times(r);
    const sine = r.times(series(r2, true));
    const cosine = series(r2, false);
    // k is below 2^26 in absolute value, so its low two bits, in two's complement, name the quadrant.
    switch (k & 3) {
        case 0: return { sine, cosine };
        case 1: return { sine: cosine, cosine: sine.negate() };
        case 2: return { sine: sine.negate(), cosine: cosine.negate() };
        default: return { sine: cosine.negate(), cosine: sine };
    }
}

/** The terms of the series `reduced` sums: enough for |r| up to pi/4, to 2^-110. */
const seriesTerms = 14;

/**
 * sin(r)/r (`odd`) or cos(r) from r², by Horner's rule: 1 − r²/(2·3)·(1 − r²/(4·5)·(…)) or
 * 1 − r²/(1·2)·(1 − r²/(3·4)·(…)).
 */
function series(r2: DoubleDouble, odd: boolean): DoubleDouble {
    let inner = DoubleDouble.one;
    for (let n = seriesTerms; n >= 1; n--) {
        const denominator = odd ? (2 * n) * (2 * n + 1) : (2 * n - 1) * (2 * n);
        inner = DoubleDouble.one.minus(inner.times(r2).dividedByNumber(denominator));
    }
    return inner;
}

/**
 * A number held as the unevaluated sum of two doubles, `high` the double nearest it: about 106 bits, enough that
 * rounding it to a double gives the double nearest the true value. The operations are Hida, Li and Bailey's
 * (the QD library's "accurate" ones), on the exact error of a product (`fusedProductError`).
 */
class DoubleDouble {
    readonly high: number;
    readonly low: number;

    constructor(high: number, low: number) {
        this.high = high;
        this.low = low;
    }

    static readonly one = new DoubleDouble(1, 0);

    /** The double nearest the number. */
    get value(): number {
        return this.high + this.low;
    }

    /** a + b exactly (Knuth's TwoSum). */
    static sum(a: number, b: number): DoubleDouble {
        const s = a + b;
        const bb = s - a;
        return new DoubleDouble(s, (a - (s - bb)) + (b - bb));
    }

    /** a + b exactly, where |a| ≥ |b| (Dekker's FastTwoSum). */
    static #quickSum(a: number, b: number): DoubleDouble {
        const s = a + b;
        return new DoubleDouble(s, b - (s - a));
    }

    negate(): DoubleDouble {
        return new DoubleDouble(-this.high, -this.low);
    }

    plus(b: DoubleDouble): DoubleDouble {
        const s = DoubleDouble.sum(this.high, b.high);
        const t = DoubleDouble.sum(this.low, b.low);
        const u = DoubleDouble.#quickSum(s.high, s.low + t.high);
        return DoubleDouble.#quickSum(u.high, u.low + t.low);
    }

    minus(b: DoubleDouble): DoubleDouble {
        return this.plus(b.negate());
    }

    times(b: DoubleDouble): DoubleDouble {
        const p1 = this.high * b.high;
        let p2 = fusedProductError(this.high, b.high, -p1);
        p2 += this.high * b.low + this.low * b.high;
        return DoubleDouble.#quickSum(p1, p2);
    }

    timesNumber(b: number): DoubleDouble {
        const p1 = this.high * b;
        let p2 = fusedProductError(this.high, b, -p1);
        p2 += this.low * b;
        return DoubleDouble.#quickSum(p1, p2);
    }

    dividedByNumber(b: number): DoubleDouble {
        const q1 = this.high / b;
        const p1 = q1 * b;
        const p2 = fusedProductError(q1, b, -p1);
        const se = DoubleDouble.sum(this.high, -p1);
        let e = se.low;
        e -= p2;
        e += this.low;
        const q2 = (se.high + e) / b;
        return DoubleDouble.#quickSum(q1, q2);
    }

    dividedBy(b: DoubleDouble): DoubleDouble {
        const q1 = this.high / b.high;
        let r = this.minus(b.timesNumber(q1));
        const q2 = r.high / b.high;
        r = r.minus(b.timesNumber(q2));
        const q3 = r.high / b.high;
        return DoubleDouble.#quickSum(q1, q2).plus(new DoubleDouble(q3, 0));
    }
}

// ---- COTH, CSCH, SECH ----------------------------------------------------------------------------------------

/** COTH: the hyperbolic cotangent; at 0 `#DIV/0!`. Microsoft documents a limit of 2^27 on the argument, but Excel answers past it: `COTH(2^27)` is 1 (COTH-002). */
export function coth(call: FunctionCall): Operand {
    return hyperbolic(call, n => 1 / Math.tanh(n), true);
}

/** CSCH: 1/SINH; at 0 `#DIV/0!`; past what SINH holds, 0, as Excel's (CSCH-002, CSCH-004). */
export function csch(call: FunctionCall): Operand {
    return hyperbolic(call, n => 1 / Math.sinh(n), true);
}

/** SECH: 1/COSH; past what COSH holds, 0, as Excel's (SECH-003). */
export function sech(call: FunctionCall): Operand {
    return hyperbolic(call, n => 1 / Math.cosh(n), false);
}

/** A hyperbolic function of any number; where it has a pole at 0, 0 is `#DIV/0!`. */
function hyperbolic(call: FunctionCall, fn: (n: number) => number, zeroIsDiv0: boolean): Operand {
    const number = numberArgument(call, 0);
    if (number instanceof Operand) return number;
    if (zeroIsDiv0 && number === 0) return Operand.ofError('#DIV/0!');
    return numberResult(fn(number));
}

// ---- ASIN, ACOS, ATAN, ATAN2, ACOT ---------------------------------------------------------------------------

const anyNumber = (): boolean => true;

/** ASIN: a number outside -1 to 1 is `#NUM!`. */
export function asin(call: FunctionCall): Operand {
    return bounded(call, n => Math.abs(n) <= 1, Math.asin);
}

/** ACOS: a number outside -1 to 1 is `#NUM!`. */
export function acos(call: FunctionCall): Operand {
    return bounded(call, n => Math.abs(n) <= 1, Math.acos);
}

/** ATAN: the angle whose tangent is the number. */
export function atan(call: FunctionCall): Operand {
    return bounded(call, anyNumber, Math.atan);
}

/** ACOT: an angle from 0 to pi. */
export function acot(call: FunctionCall): Operand {
    return bounded(call, anyNumber, n => Math.PI / 2 - Math.atan(n));
}

/** ATAN2: the angle of the point (`x_num`, `y_num`), above -pi and up to pi. Excel takes x first, the reverse of `Math.atan2`. Both 0 is `#DIV/0!`, as documented. */
export function atan2(call: FunctionCall): Operand {
    const x = numberArgument(call, 0);
    if (x instanceof Operand) return x;
    const y = numberArgument(call, 1);
    if (y instanceof Operand) return y;
    if (x === 0 && y === 0) return Operand.ofError('#DIV/0!');
    return numberResult(Math.atan2(y, x));
}

// ---- SINH, COSH, TANH, ASINH, ACOSH, ATANH, ACOTH ------------------------------------------------------------

/** SINH: past what a double holds, `#NUM!`. */
export function sinh(call: FunctionCall): Operand {
    return bounded(call, anyNumber, Math.sinh);
}

/** COSH: past what a double holds, `#NUM!`. */
export function cosh(call: FunctionCall): Operand {
    return bounded(call, anyNumber, Math.cosh);
}

/** TANH: the hyperbolic tangent. */
export function tanh(call: FunctionCall): Operand {
    return bounded(call, anyNumber, Math.tanh);
}

/** ASINH: the inverse hyperbolic sine. */
export function asinh(call: FunctionCall): Operand {
    return bounded(call, anyNumber, Math.asinh);
}

/** ACOSH: a number below 1 is `#NUM!`. */
export function acosh(call: FunctionCall): Operand {
    return bounded(call, n => n >= 1, Math.acosh);
}

/** ATANH: a number not strictly between -1 and 1 is `#NUM!`. */
export function atanh(call: FunctionCall): Operand {
    return bounded(call, n => Math.abs(n) < 1, Math.atanh);
}

/**
 * ACOTH: ½·ln((x+1)/(x−1)), for an absolute value above 1. Microsoft's page names both `#NUM!` and `#VALUE!` for
 * a smaller one; Excel gives `#NUM!` (ACOTH-003, observed by the seventeenth Windows run).
 */
export function acoth(call: FunctionCall): Operand {
    return bounded(call, n => Math.abs(n) > 1, n => 0.5 * Math.log((n + 1) / (n - 1)));
}

// ---- DEGREES, RADIANS ----------------------------------------------------------------------------------------

/** DEGREES: radians to degrees. */
export function degrees(call: FunctionCall): Operand {
    return bounded(call, anyNumber, n => n * 180 / Math.PI);
}

/** RADIANS: degrees to radians. */
export function radians(call: FunctionCall): Operand {
    return bounded(call, anyNumber, n => n * Math.PI / 180);
}

/** A function of one number, `#NUM!` where `inDomain` refuses it or the result is not finite. */
function bounded(call: FunctionCall, inDomain: (n: number) => boolean, fn: (n: number) => number): Operand {
    const number = numberArgument(call, 0);
    if (number instanceof Operand) return number;
    return inDomain(number) ? numberResult(fn(number)) : Operand.ofError('#NUM!');
}
