// Exact dyadic numbers and interval arithmetic over them (C#'s Enclosure.cs), in which PMT computes its exact
// payment and refuses where the enclosure does not decide the double nearest it (`exact-payment.ts`).
import { doubleBits, roundHalfToEven } from '../dotnet';
import { bitLength, roundToDouble } from './correctly-rounded';

function abs(value: bigint): bigint {
    return value < 0n ? -value : value;
}

function signOf(value: bigint): number {
    return value < 0n ? -1 : value > 0n ? 1 : 0;
}

/**
 * A dyadic number, `mantissa` times 2^`exponent`, held exactly: an end of an `Enclosure`. Its sums and products
 * are exact, its exponent has no double's limits, and `down` and `up` round it to a number of bits toward -∞ and
 * +∞.
 */
export class Dyadic {
    readonly mantissa: bigint;
    readonly exponent: number;

    constructor(mantissa: bigint, exponent: number) {
        this.mantissa = mantissa;
        this.exponent = exponent;
    }

    static readonly zero = new Dyadic(0n, 0);
    static readonly one = new Dyadic(1n, 0);

    /** A finite double, exactly. */
    static of(value: number): Dyadic {
        if (!Number.isFinite(value)) throw new RangeError(`Only a finite double has a dyadic value, not ${String(value)}.`);
        if (value === 0) return Dyadic.zero;
        const bits = doubleBits(value);
        const biased = Number((bits >> 52n) & 0x7FFn);
        const fraction = bits & 0xFFFFFFFFFFFFFn;
        const mantissa = biased === 0 ? fraction : fraction | (1n << 52n);
        return new Dyadic(value < 0 ? -mantissa : mantissa, biased === 0 ? -1074 : biased - 1075);
    }

    /** A whole number, exactly. */
    static whole(value: number): Dyadic {
        return new Dyadic(BigInt(value), 0);
    }

    get sign(): number {
        return signOf(this.mantissa);
    }

    get isZero(): boolean {
        return this.mantissa === 0n;
    }

    /** The exponent of the leading bit: a number other than 0 lies in [2^top, 2^(top+1)) in magnitude. */
    get top(): number {
        return this.exponent + bitLength(abs(this.mantissa)) - 1;
    }

    negate(): Dyadic {
        return new Dyadic(-this.mantissa, this.exponent);
    }

    abs(): Dyadic {
        return new Dyadic(abs(this.mantissa), this.exponent);
    }

    /** This number times 2^`power`, exactly. */
    scale(power: number): Dyadic {
        return new Dyadic(this.mantissa, this.exponent + power);
    }

    plus(b: Dyadic): Dyadic {
        if (this.isZero) return b;
        if (b.isZero) return this;
        const exponent = Math.min(this.exponent, b.exponent);
        return new Dyadic((this.mantissa << BigInt(this.exponent - exponent)) + (b.mantissa << BigInt(b.exponent - exponent)), exponent);
    }

    minus(b: Dyadic): Dyadic {
        return this.plus(b.negate());
    }

    times(b: Dyadic): Dyadic {
        return new Dyadic(this.mantissa * b.mantissa, this.exponent + b.exponent);
    }

    static compare(a: Dyadic, b: Dyadic): number {
        return a.minus(b).sign;
    }

    /** The largest number of `bits` significant bits not above this one. */
    down(bits: number): Dyadic {
        if (this.isZero) return this;
        const shift = bitLength(abs(this.mantissa)) - bits;
        // BigInt's right shift is arithmetic: it rounds a negative mantissa toward -∞ as well.
        return shift <= 0 ? this : new Dyadic(this.mantissa >> BigInt(shift), this.exponent + shift);
    }

    /** The smallest number of `bits` significant bits not below this one. */
    up(bits: number): Dyadic {
        return this.negate().down(bits).negate();
    }

    /** `a` / `b` rounded to `bits` bits, up (toward +∞) or down. */
    static quotient(a: Dyadic, b: Dyadic, bits: number, up: boolean): Dyadic {
        if (b.isZero) throw new RangeError('Division by zero.');
        if (a.isZero) return Dyadic.zero;
        const { quotient, inexact, exponent } = divide(a, b, bits + 2);
        const positive = a.sign === b.sign;
        // The magnitude is quotient plus a fraction below 1: away from 0 is up for a positive quotient and down for a negative one.
        const magnitude = inexact && positive === up ? quotient + 1n : quotient;
        const result = new Dyadic(positive ? magnitude : -magnitude, exponent);
        return up ? result.up(bits) : result.down(bits);
    }

    /** The double nearest this number, ties to even, subnormal and infinite results included. */
    toDouble(): number {
        return roundToDouble(this.mantissa, this.exponent);
    }
}

/** |a/b| truncated to a whole number of at least `bits` bits, whether a remainder was dropped, and its exponent. */
function divide(a: Dyadic, b: Dyadic, bits: number): { quotient: bigint; inexact: boolean; exponent: number } {
    const numerator = abs(a.mantissa);
    const denominator = abs(b.mantissa);
    const shift = Math.max(0, bits + bitLength(denominator) - bitLength(numerator));
    const shifted = numerator << BigInt(shift);
    const quotient = shifted / denominator;
    return { quotient, inexact: shifted - quotient * denominator !== 0n, exponent: a.exponent - b.exponent - shift };
}

const two = new Dyadic(2n, 0);

/**
 * A real number known to lie between two dyadic numbers, `low` and `high`: interval arithmetic, each operation
 * rounding the low end of its result down and the high end up. The functions, `log1p`, `expm1` and `exp`, are
 * series whose truncation is bounded and added to the enclosure.
 */
export class Enclosure {
    readonly low: Dyadic;
    readonly high: Dyadic;

    constructor(low: Dyadic, high: Dyadic) {
        this.low = low;
        this.high = high;
    }

    static exactly(value: Dyadic | number): Enclosure {
        const dyadic = typeof value === 'number' ? Dyadic.of(value) : value;
        return new Enclosure(dyadic, dyadic);
    }

    get containsZero(): boolean {
        return this.low.sign <= 0 && this.high.sign >= 0;
    }

    negate(): Enclosure {
        return new Enclosure(this.high.negate(), this.low.negate());
    }

    /** This enclosure times 2^`power`, exactly. */
    scale(power: number): Enclosure {
        return new Enclosure(this.low.scale(power), this.high.scale(power));
    }

    /** The least and the greatest magnitude within it; not for an enclosure of 0. */
    magnitudes(): { least: Dyadic; most: Dyadic } {
        if (this.containsZero) throw new Error('Internal: an enclosure of 0 has no least magnitude.');
        return this.low.sign > 0 ? { least: this.low, most: this.high } : { least: this.high.abs(), most: this.low.abs() };
    }

    /** The narrower enclosure both say: each encloses the same number. */
    static both(a: Enclosure, b: Enclosure): Enclosure {
        return new Enclosure(Dyadic.compare(a.low, b.low) >= 0 ? a.low : b.low, Dyadic.compare(a.high, b.high) <= 0 ? a.high : b.high);
    }

    static add(a: Enclosure, b: Enclosure, bits: number): Enclosure {
        return new Enclosure(a.low.plus(b.low).down(bits), a.high.plus(b.high).up(bits));
    }

    static subtract(a: Enclosure, b: Enclosure, bits: number): Enclosure {
        return Enclosure.add(a, b.negate(), bits);
    }

    static multiply(a: Enclosure, b: Enclosure, bits: number): Enclosure {
        if (b.high.sign <= 0 && b.low.sign < 0) return Enclosure.multiply(a, b.negate(), bits).negate();
        if (b.low.sign < 0) {
            // b holds 0 inside it: take the one that does not, if either does not.
            if (a.low.sign >= 0 || a.high.sign <= 0) return Enclosure.multiply(b, a, bits);
            const low = least(a.low.times(b.high), a.high.times(b.low));
            const high = greatest(a.low.times(b.low), a.high.times(b.high));
            return new Enclosure(low.down(bits), high.up(bits));
        }
        // b is 0 or above.
        const lowest = a.low.sign >= 0 ? a.low.times(b.low) : a.low.times(b.high);
        const most = a.high.sign >= 0 ? a.high.times(b.high) : a.high.times(b.low);
        return new Enclosure(lowest.down(bits), most.up(bits));
    }

    /** `a` / `b`, where `b` does not hold 0. */
    static divide(a: Enclosure, b: Enclosure, bits: number): Enclosure {
        if (b.containsZero) throw new RangeError('Division by an enclosure of 0.');
        if (b.high.sign < 0) return Enclosure.divide(a, b.negate(), bits).negate();
        // b is above 0: a / b grows with a, and falls with b where a is above 0 and rises where it is below.
        const low = Dyadic.quotient(a.low, a.low.sign >= 0 ? b.high : b.low, bits, false);
        const high = Dyadic.quotient(a.high, a.high.sign >= 0 ? b.low : b.high, bits, true);
        return new Enclosure(low, high);
    }

    static readonly #ln2s = new Map<number, Enclosure>();

    /**
     * ln 2 to `bits` bits: 2 atanh(1/3), the sum of 2 / ((2j+1) 3^(2j+1)), in fixed point with every term
     * truncated, so that the truncations and the terms left out bound it from above.
     */
    static ln2(bits: number): Enclosure {
        const known = Enclosure.#ln2s.get(bits);
        if (known !== undefined) return known;
        const scale = bits + 8;
        const twoScaled = 1n << BigInt(scale + 1);
        let sum = 0n;
        let terms = 0n;
        let power = 3n;
        for (let j = 0n; ; j++) {
            const term = twoScaled / ((2n * j + 1n) * power);
            if (term === 0n) break; // this term and the rest, each a ninth of the one before, sum to below 2 units
            sum += term;
            terms++;
            power *= 9n;
        }
        const ln2 = new Enclosure(new Dyadic(sum, -scale).down(bits), new Dyadic(sum + terms + 2n, -scale).up(bits));
        Enclosure.#ln2s.set(bits, ln2);
        return ln2;
    }

    /**
     * ln(1 + `d`) for an exact `d` above -1: 2 atanh(d / (2 + d)) where |d| is below 1/2, which keeps the
     * logarithm's relative precision however small d is; otherwise k ln 2 plus that of the rest, 1 + d being
     * m 2^k with m within a factor of √2 of 1.
     */
    static log1p(d: Dyadic, bits: number): Enclosure {
        if (d.isZero) return Enclosure.exactly(Dyadic.zero);
        if (Dyadic.compare(d, Dyadic.one.negate()) <= 0) throw new RangeError('ln(1+d) needs d above -1.');
        const working = bits + 16;
        if (d.top < -1) {
            const z = Enclosure.divide(Enclosure.exactly(d), Enclosure.exactly(d.plus(two)), working);
            return Enclosure.multiply(z, atanhSeries(z, working), working).scale(1);
        }
        const onePlus = Dyadic.one.plus(d);
        let k = onePlus.top;
        const leading = onePlus.scale(-k); // in [1, 2)
        if (Dyadic.compare(leading.times(leading), two) >= 0) k++;
        const rest = onePlus.scale(-k).minus(Dyadic.one); // in [1/√2 - 1, √2 - 1)
        return Enclosure.add(
            Enclosure.multiply(Enclosure.exactly(Dyadic.whole(k)), Enclosure.ln2(working + 16), working),
            Enclosure.log1p(rest, working),
            working);
    }

    /** e^y - 1 over an enclosure: it rises with y, so it lies between its values at the two ends. */
    static expm1(y: Enclosure, bits: number): Enclosure {
        const low = expm1At(y.low, bits);
        return Dyadic.compare(y.low, y.high) === 0 ? low : new Enclosure(low.low, expm1At(y.high, bits).high);
    }

    /** e^y over an enclosure, as `expm1`. */
    static exp(y: Enclosure, bits: number): Enclosure {
        const low = expAt(y.low, bits);
        return Dyadic.compare(y.low, y.high) === 0 ? low : new Enclosure(low.low, expAt(y.high, bits).high);
    }
}

/** The sum of z^(2j) / (2j+1) for |z| at most 1/3, so that 2z times it is 2 atanh z = ln((1+z)/(1-z)). */
function atanhSeries(z: Enclosure, bits: number): Enclosure {
    const square = Enclosure.multiply(z, z, bits);
    const top = square.high.top + 1; // square < 2^top, top at most -3
    const count = Math.max(0, Math.ceil((bits + 2) / -top) - 1); // square^(count+1) < 2^-(bits+2)
    let sum = reciprocal(2 * count + 1, bits);
    for (let j = count - 1; j >= 0; j--) sum = Enclosure.add(reciprocal(2 * j + 1, bits), Enclosure.multiply(square, sum, bits), bits);
    // The terms left out sum to below square^(count+1) / (1 - square) < 2^(top (count+1) + 1).
    return Enclosure.add(sum, new Enclosure(Dyadic.zero, Dyadic.one.scale(top * (count + 1) + 1)), bits);
}

function reciprocal(k: number, bits: number): Enclosure {
    const whole = Dyadic.whole(k);
    return new Enclosure(Dyadic.quotient(Dyadic.one, whole, bits, false), Dyadic.quotient(Dyadic.one, whole, bits, true));
}

/**
 * e^y - 1 for an exact y. Below 1/2 in magnitude, its Taylor series at y / 2^s, s halvings bringing it below
 * 2^-8, keeps its relative precision however small y is, and each halving is undone by
 * e^(2u) - 1 = (e^u - 1)(e^u - 1 + 2); otherwise e^y less 1.
 */
function expm1At(y: Dyadic, bits: number): Enclosure {
    if (y.isZero) return Enclosure.exactly(Dyadic.zero);
    const working = bits + 24;
    if (y.top >= -1) return Enclosure.subtract(expAt(y, bits), Enclosure.exactly(Dyadic.one), working);
    const halvings = Math.max(0, y.top + 9);
    const u = y.scale(-halvings);
    const top = u.top + 1; // |u| < 2^top, top at most -8
    const degree = Math.max(1, Math.ceil((working + 3) / -top)); // |u|^degree < 2^-(working+3)
    const exactlyU = Enclosure.exactly(u);
    const one = Enclosure.exactly(Dyadic.one);
    let inner = one;
    for (let k = degree; k >= 2; k--) {
        inner = Enclosure.add(one, Enclosure.divide(Enclosure.multiply(exactlyU, inner, working), Enclosure.exactly(Dyadic.whole(k)), working), working);
    }
    const rest = u.abs().scale(-(working + 2));
    let result = Enclosure.add(Enclosure.multiply(exactlyU, inner, working), new Enclosure(rest.negate(), rest), working);
    for (let i = 0; i < halvings; i++) result = Enclosure.multiply(result, Enclosure.add(result, Enclosure.exactly(two), working), working);
    return result;
}

/** e^y for an exact y: 2^k (1 + (e^t - 1)), t = y - k ln 2 below 1/2 in magnitude. */
function expAt(y: Dyadic, bits: number): Enclosure {
    const working = bits + 24;
    if (y.top < -1) return Enclosure.add(Enclosure.exactly(Dyadic.one), expm1At(y, bits), working);
    // Any whole k leaves |t| below 1/2 here; toDouble and the division are IEEE's, so k is the same on every platform.
    const k = roundHalfToEven(y.toDouble() / Math.LN2);
    const t = Enclosure.subtract(
        Enclosure.exactly(y),
        Enclosure.multiply(Enclosure.exactly(Dyadic.whole(k)), Enclosure.ln2(working + 16), working + 16),
        working + 16);
    const fraction = new Enclosure(expm1At(t.low, working).low, expm1At(t.high, working).high);
    return Enclosure.add(Enclosure.exactly(Dyadic.one), fraction, working).scale(k);
}

function least(a: Dyadic, b: Dyadic): Dyadic {
    return Dyadic.compare(a, b) <= 0 ? a : b;
}

function greatest(a: Dyadic, b: Dyadic): Dyadic {
    return Dyadic.compare(a, b) >= 0 ? a : b;
}
