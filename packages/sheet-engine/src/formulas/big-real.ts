// A binary number of any size, m × 2^e with a BigInt m, carried to a chosen precision, and the elementary
// functions on it (C#'s BigReal.cs). `CorrectlyRounded` computes the functions the platform's math library would
// otherwise answer — each library its own last bit — in it, and rounds the result once.
import { bitLength, decompose, roundToDouble } from './correctly-rounded';

/** m × 2^e. Addition and subtraction are exact; every other operation keeps `precision` bits, truncated toward zero. */
export class BigReal {
    readonly m: bigint;
    readonly e: number;

    constructor(m: bigint, e: number) {
        this.m = m;
        this.e = e;
    }

    static readonly zero = new BigReal(0n, 0);
    static readonly one = new BigReal(1n, 0);

    /** A finite double, exactly. */
    static of(x: number): BigReal {
        if (!Number.isFinite(x)) throw new RangeError(`${String(x)} is no finite number.`);
        if (x === 0) return BigReal.zero;
        const { significand, exponent } = decompose(x);
        return new BigReal(x < 0 ? -significand : significand, exponent);
    }

    /** A whole number, exactly. */
    static whole(n: number | bigint): BigReal {
        return new BigReal(BigInt(n), 0);
    }

    get sign(): number {
        return this.m > 0n ? 1 : this.m < 0n ? -1 : 0;
    }

    /** The power of two of the leading bit: the number lies in [2^top, 2^(top+1)) in magnitude. */
    get top(): number {
        return this.m === 0n ? -Infinity : this.e + bitLength(this.m < 0n ? -this.m : this.m) - 1;
    }

    /** The double nearest, ties to even. */
    toDouble(): number {
        return roundToDouble(this.m, this.e);
    }

    negated(): BigReal {
        return new BigReal(-this.m, this.e);
    }

    abs(): BigReal {
        return this.m < 0n ? this.negated() : this;
    }

    /** This times 2^`n`, exactly. */
    scaled(n: number): BigReal {
        return new BigReal(this.m, this.e + n);
    }

    plus(other: BigReal): BigReal {
        if (this.m === 0n) return other;
        if (other.m === 0n) return this;
        const e = Math.min(this.e, other.e);
        return new BigReal((this.m << BigInt(this.e - e)) + (other.m << BigInt(other.e - e)), e);
    }

    minus(other: BigReal): BigReal {
        return this.plus(other.negated());
    }

    /** Kept to `precision` bits, truncated toward zero. */
    kept(precision: number): BigReal {
        const magnitude = this.m < 0n ? -this.m : this.m;
        const excess = bitLength(magnitude) - precision;
        if (excess <= 0) return this;
        const shifted = magnitude >> BigInt(excess);
        return new BigReal(this.m < 0n ? -shifted : shifted, this.e + excess);
    }

    times(other: BigReal, precision: number): BigReal {
        return new BigReal(this.m * other.m, this.e + other.e).kept(precision);
    }

    dividedBy(other: BigReal, precision: number): BigReal {
        if (other.m === 0n) throw new RangeError('Internal: a BigReal is divided by 0.');
        if (this.m === 0n) return BigReal.zero;
        const n = this.m < 0n ? -this.m : this.m;
        const d = other.m < 0n ? -other.m : other.m;
        const shift = Math.max(0, precision + bitLength(d) - bitLength(n) + 1);
        const q = (n << BigInt(shift)) / d;
        const negative = (this.m < 0n) !== (other.m < 0n);
        return new BigReal(negative ? -q : q, this.e - other.e - shift).kept(precision);
    }

    /** The square root of a number not below 0. */
    sqrt(precision: number): BigReal {
        if (this.m < 0n) throw new RangeError('Internal: the square root of a negative BigReal is asked.');
        if (this.m === 0n) return BigReal.zero;
        let shift = Math.max(0, 2 * precision + 2 - bitLength(this.m));
        if ((this.e - shift) % 2 !== 0) shift++;
        return new BigReal(isqrt(this.m << BigInt(shift)), (this.e - shift) / 2).kept(precision);
    }

    compare(other: BigReal): number {
        const difference = this.minus(other);
        return difference.sign;
    }
}

/** The whole square root, rounded down, of a whole number above 0 (Newton's method from above). */
function isqrt(n: bigint): bigint {
    let x = 1n << BigInt(Math.ceil(bitLength(n) / 2));
    for (;;) {
        const y = (x + n / x) >> 1n;
        if (y >= x) return x;
        x = y;
    }
}

// ---- The functions, each to `p` bits with an error of a few of its last bits ----------------------------------

const ln2Cache = new Map<number, BigReal>();
const piCache = new Map<number, BigReal>();
const ln10Cache = new Map<number, BigReal>();

/** s + s^3/3 + s^5/5 + …, atanh s, for |s| at most 1/3. */
function atanhSeries(s: BigReal, p: number): BigReal {
    const s2 = s.times(s, p);
    let term = s;
    let sum = BigReal.zero;
    for (let k = 1n; term.m !== 0n && term.top > s.top - p - 4; k += 2n) {
        sum = sum.plus(term.dividedBy(BigReal.whole(k), p)).kept(p + 8);
        term = term.times(s2, p);
    }
    return sum;
}

/** s - s^3/3 + s^5/5 - …, atan s, for |s| at most 1/8. */
function atanSeries(s: BigReal, p: number): BigReal {
    const s2 = s.times(s, p).negated();
    let term = s;
    let sum = BigReal.zero;
    for (let k = 1n; term.m !== 0n && term.top > s.top - p - 4; k += 2n) {
        sum = sum.plus(term.dividedBy(BigReal.whole(k), p)).kept(p + 8);
        term = term.times(s2, p);
    }
    return sum;
}

/** ln 2 to `p` bits: 2 atanh(1/3). */
export function ln2(p: number): BigReal {
    const known = ln2Cache.get(p);
    if (known !== undefined) return known;
    const value = atanhSeries(BigReal.one.dividedBy(BigReal.whole(3), p + 8), p + 8).scaled(1).kept(p);
    ln2Cache.set(p, value);
    return value;
}

/** pi to `p` bits: Machin's 16 atan(1/5) - 4 atan(1/239). */
export function pi(p: number): BigReal {
    const known = piCache.get(p);
    if (known !== undefined) return known;
    const q = p + 8;
    const fifth = atanAnySmall(BigReal.one.dividedBy(BigReal.whole(5), q), q);
    const rest = atanSeries(BigReal.one.dividedBy(BigReal.whole(239), q), q);
    const value = fifth.scaled(4).minus(rest.scaled(2)).kept(p);
    piCache.set(p, value);
    return value;
}

/** atan s for |s| at most 1/4, by one halving and the series. */
function atanAnySmall(s: BigReal, p: number): BigReal {
    return atanSeries(halved(s, p), p).scaled(1);
}

/** tan(a/2) from tan a = s: s / (1 + sqrt(1 + s^2)). */
function halved(s: BigReal, p: number): BigReal {
    return s.dividedBy(BigReal.one.plus(BigReal.one.plus(s.times(s, p)).sqrt(p)), p);
}

/** ln x for x above 0. */
export function ln(x: BigReal, p: number): BigReal {
    if (x.m <= 0n) throw new RangeError('Internal: the logarithm of a BigReal not above 0 is asked.');
    // x = y * 2^k with y in [1/sqrt 2, sqrt 2): the top 54 bits tell which side of sqrt 2 y lies on well enough.
    let k = x.top;
    let y = x.scaled(-k);
    if (y.kept(54).toDouble() > Math.SQRT2) {
        k++;
        y = y.scaled(-1);
    }
    const s = y.minus(BigReal.one).dividedBy(y.plus(BigReal.one), p + 4);
    const logarithm = atanhSeries(s, p + 4).scaled(1);
    return k === 0 ? logarithm.kept(p) : logarithm.plus(ln2(p + 16).times(BigReal.whole(k), p + 16)).kept(p);
}

/** ln(1 + u) for u above -1, as exact relative to u as to 1. */
export function ln1p(u: BigReal, p: number): BigReal {
    if (u.m === 0n) return BigReal.zero;
    if (u.top < -1) return atanhSeries(u.dividedBy(u.plus(BigReal.whole(2)), p + 4), p + 4).scaled(1).kept(p);
    return ln(BigReal.one.plus(u), p);
}

/** ln 10 to `p` bits. */
function ln10(p: number): BigReal {
    const known = ln10Cache.get(p);
    if (known !== undefined) return known;
    const value = ln(BigReal.whole(10), p);
    ln10Cache.set(p, value);
    return value;
}

/** log10 x for x above 0. */
export function log10(x: BigReal, p: number): BigReal {
    return ln(x, p + 4).dividedBy(ln10(p + 4), p);
}

/** e^x for |x| below 2^20. */
export function exp(x: BigReal, p: number): BigReal {
    if (x.m === 0n) return BigReal.one;
    if (x.top > 20) throw new RangeError('Internal: e^x is asked of a BigReal too large.');
    const q = p + 32;
    const k = Math.round(x.kept(54).toDouble() / Math.LN2);
    const r = k === 0 ? x : x.minus(ln2(q + 24).times(BigReal.whole(k), q + 24)).kept(q);
    // e^r = (e^(r/256))^256, e^(r/256) by its Taylor series.
    const s = r.scaled(-8);
    let term = BigReal.one;
    let sum = BigReal.zero;
    for (let j = 1n; term.m !== 0n && term.top > -q - 4; j++) {
        sum = sum.plus(term).kept(q + 8);
        term = term.times(s, q).dividedBy(BigReal.whole(j), q);
    }
    let power = sum;
    for (let i = 0; i < 8; i++) power = power.times(power, q);
    return power.scaled(k).kept(p);
}

/** e^x - 1 for |x| below 1, as exact relative to the result as e^x is. */
export function expm1(x: BigReal, p: number): BigReal {
    if (x.m === 0n) return BigReal.zero;
    if (x.top >= 0) return exp(x, p + 4).minus(BigReal.one).kept(p);
    let term = x;
    let sum = BigReal.zero;
    for (let j = 2n; term.m !== 0n && term.top > x.top - p - 6; j++) {
        sum = sum.plus(term).kept(p + 8);
        term = term.times(x, p + 4).dividedBy(BigReal.whole(j), p + 4);
    }
    return sum.kept(p);
}

/** atan x. */
export function atan(x: BigReal, p: number): BigReal {
    if (x.m === 0n) return BigReal.zero;
    const q = p + 12;
    const a = x.abs();
    let result: BigReal;
    if (a.compare(BigReal.one) > 0) {
        // pi/2 - atan(1/a), at least pi/4.
        result = pi(q).scaled(-1).minus(atanReduced(BigReal.one.dividedBy(a, q), q));
    } else {
        result = atanReduced(a, q);
    }
    return (x.m < 0n ? result.negated() : result).kept(p);
}

/** atan a for 0 <= a <= 1: three halvings bring it below tan(pi/32), where the series converges fast. */
function atanReduced(a: BigReal, q: number): BigReal {
    let s = a;
    for (let i = 0; i < 3; i++) s = halved(s, q);
    return atanSeries(s, q).scaled(3);
}

/** sinh x for |x| below 2^20. */
export function sinh(x: BigReal, p: number): BigReal {
    if (x.top < 0) {
        // x + x^3/3! + x^5/5! + …, with no cancellation.
        const x2 = x.times(x, p + 4);
        let term = x;
        let sum = BigReal.zero;
        for (let j = 2n; term.m !== 0n && term.top > x.top - p - 6; j += 2n) {
            sum = sum.plus(term).kept(p + 8);
            term = term.times(x2, p + 4).dividedBy(BigReal.whole(j * (j + 1n)), p + 4);
        }
        return sum.kept(p);
    }
    const ex = exp(x, p + 4);
    return ex.minus(BigReal.one.dividedBy(ex, p + 4)).scaled(-1).kept(p);
}

/** cosh x for |x| below 2^20. */
export function cosh(x: BigReal, p: number): BigReal {
    const ex = exp(x, p + 4);
    return ex.plus(BigReal.one.dividedBy(ex, p + 4)).scaled(-1).kept(p);
}

/** tanh x for |x| below 2^20. */
export function tanh(x: BigReal, p: number): BigReal {
    const a = x.abs();
    // (e^2a - 1) / (e^2a + 1), its numerator from e^2a - 1 as exact relative to itself as e^2a.
    const m1 = a.top < -1 ? expm1(a.scaled(1), p + 4) : exp(a.scaled(1), p + 8).minus(BigReal.one);
    const result = m1.dividedBy(m1.plus(BigReal.whole(2)), p);
    return x.m < 0n ? result.negated() : result;
}

/** asinh x. */
export function asinh(x: BigReal, p: number): BigReal {
    const a = x.abs();
    const q = p + 8;
    const a2 = a.times(a, q);
    const root = BigReal.one.plus(a2).sqrt(q);
    const result = a.top < 0
        ? ln1p(a.plus(a2.dividedBy(BigReal.one.plus(root), q)), p)
        : ln(a.plus(root), p);
    return x.m < 0n ? result.negated() : result;
}

/** acosh x for x at least 1. */
export function acosh(x: BigReal, p: number): BigReal {
    const t = x.minus(BigReal.one);
    if (t.m < 0n) throw new RangeError('Internal: acosh is asked of a BigReal below 1.');
    const q = p + 8;
    if (t.top < 0) return ln1p(t.plus(t.times(t.plus(BigReal.whole(2)), q).sqrt(q)), p);
    return ln(x.plus(x.times(x, 2 * q).minus(BigReal.one).sqrt(q)), p);
}

/** atanh x for |x| below 1: half ln(1 + 2|x|/(1 - |x|)). */
export function atanh(x: BigReal, p: number): BigReal {
    const a = x.abs();
    const result = ln1p(a.scaled(1).dividedBy(BigReal.one.minus(a), p + 8), p + 4).scaled(-1).kept(p);
    return x.m < 0n ? result.negated() : result;
}

/** asin x for |x| below 1: atan(x / sqrt((1 - x)(1 + x))). */
export function asin(x: BigReal, p: number): BigReal {
    const q = p + 8;
    const cosine = BigReal.one.minus(x).times(BigReal.one.plus(x), q).sqrt(q);
    return atan(x.dividedBy(cosine, q), p);
}

/** acos x for |x| below 1: 2 atan(sqrt((1 - x)/(1 + x))). */
export function acos(x: BigReal, p: number): BigReal {
    const q = p + 8;
    return atan(BigReal.one.minus(x).dividedBy(BigReal.one.plus(x), q).sqrt(q), q).scaled(1).kept(p);
}

/** The angle of (x, y), neither 0: atan(|y|/|x|), from pi when x is negative, with y's sign. */
export function atan2(y: BigReal, x: BigReal, p: number): BigReal {
    const q = p + 8;
    const angle = atan(y.abs().dividedBy(x.abs(), q), q);
    const result = x.m < 0n ? pi(q).minus(angle) : angle;
    return (y.m < 0n ? result.negated() : result).kept(p);
}
