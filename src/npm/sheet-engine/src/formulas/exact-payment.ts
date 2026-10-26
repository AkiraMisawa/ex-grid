// PMT's payment at a rate above -1 other than 0, or `undefined` where PMT refuses it (C#'s ExactPayment.cs,
// ADR-0047's exception for PMT). The payment is the exact payment over its arguments' doubles,
// -rate (pv g + fv) / (g - 1), g = (1+rate)^nper (over (1+rate) more at the start), rounded once: computed in
// `Enclosure`s, and given only where both bounds round to the same double, the enclosures computed again with
// twice the bits while they do not decide it. The refusals are the financial area's rules (2026-10-12), as the
// C# remarks set them out.
import { log } from './portable-math';
import { Dyadic, Enclosure } from './enclosure';

const firstBits = 96;
const mostBits = 1536;
const leastFit = Dyadic.one.scale(-1021);
const mostFit = Dyadic.one.scale(1022);
const half = Dyadic.one.scale(-1);

/** The bound of `conditioned`, as a power of 2. */
const mostMagnification = 6;

/**
 * The exact payment at the end, or at the start, rounded once, or `undefined` where PMT refuses. The caller has
 * answered or refused a rate of 0, a rate of -1 or below, an `periods` of 0 and one past 2^53.
 */
export function exactPayment(rate: number, periods: number, present: number, future: number, atStart: boolean): number | undefined {
    const r = Dyadic.of(rate);
    const onePlusRate = Dyadic.one.plus(r);
    const pv = Dyadic.of(present);
    const fv = Dyadic.of(future);
    const sum = pv.plus(fv);
    if (!fits(r) || !fits(pv) || !fits(fv) || !fits(sum)) return undefined;
    // The growth's logarithm, estimated in doubles: past 2^1100 either way the enclosures would be vast. ln is the
    // correctly rounded one, not the platform's, so a growth near the line is refused alike everywhere (ADR-0047).
    if (!(Math.abs(periods * log(1 + rate)) <= 1100 * Math.LN2)) return undefined;
    for (let bits = firstBits; bits <= mostBits; bits *= 2) {
        const { growth, lessOne } = growthOf(r, onePlusRate, periods, bits);
        if (bits === firstBits && !quantitiesFit(r, pv, fv, sum, growth, lessOne, onePlusRate, atStart)) return undefined;
        // pv g + fv, and pv (g-1) + (pv+fv), which is narrower where g is near 1: each holds it.
        const owed = Enclosure.both(
            Enclosure.add(Enclosure.multiply(Enclosure.exactly(pv), growth, bits), Enclosure.exactly(fv), bits),
            Enclosure.add(Enclosure.multiply(Enclosure.exactly(pv), lessOne, bits), Enclosure.exactly(sum), bits));
        if (bits === firstBits && !conditioned(r, onePlusRate, periods, pv, fv, sum, growth, lessOne, owed, atStart, bits)) return undefined;
        const atEnd = Enclosure.divide(Enclosure.multiply(Enclosure.exactly(r.negate()), owed, bits), lessOne, bits);
        const payment = atStart ? Enclosure.divide(atEnd, Enclosure.exactly(onePlusRate), bits) : atEnd;
        const low = payment.low.toDouble();
        if (low !== payment.high.toDouble()) continue;
        return low === 0 ? 0 : low;
    }
    return undefined;
}

/**
 * g = (1+rate)^periods and g - 1. A whole number of periods raises g - 1 from the rate by
 * (1+a)(1+b) - 1 = a + b + ab and (1+a)^2 - 1 = a (a + 2), and g by squaring 1+rate where g is below 1/2; a
 * negative one is 1 over the power to its absolute value. Any other is e^(periods ln(1+rate)), and g - 1 e^x - 1
 * by its own series.
 */
function growthOf(rate: Dyadic, onePlusRate: Dyadic, periods: number, bits: number): { growth: Enclosure; lessOne: Enclosure } {
    const one = Enclosure.exactly(Dyadic.one);
    const magnitude = Math.abs(periods);
    if (magnitude !== Math.floor(magnitude)) {
        const exponent = Enclosure.multiply(Enclosure.log1p(rate, bits), Enclosure.exactly(periods), bits);
        const lessOne = Enclosure.expm1(exponent, bits);
        return { growth: Dyadic.compare(lessOne.low, half.negate()) >= 0 ? Enclosure.add(one, lessOne, bits) : Enclosure.exp(exponent, bits), lessOne };
    }
    let raised: Enclosure | undefined;
    let square = Enclosure.exactly(rate);
    const two = Enclosure.exactly(Dyadic.whole(2));
    for (let k = magnitude; ;) {
        if (k % 2 === 1) raised = raised === undefined ? square : Enclosure.add(Enclosure.add(raised, square, bits), Enclosure.multiply(raised, square, bits), bits);
        k = Math.floor(k / 2);
        if (k === 0) break;
        square = Enclosure.multiply(square, Enclosure.add(square, two, bits), bits);
    }
    if (raised === undefined) throw new Error('Internal: PMT raises to a whole number of periods other than 0.');
    const less = raised;
    const power = Dyadic.compare(less.low, half.negate()) >= 0 ? Enclosure.add(one, less, bits) : raise(onePlusRate, magnitude, bits);
    if (periods > 0) return { growth: power, lessOne: less };
    // (1+rate)^-n = 1 / (1+rate)^n, and less 1, -((1+rate)^n - 1) / (1+rate)^n.
    return { growth: Enclosure.divide(one, power, bits), lessOne: Enclosure.divide(less.negate(), power, bits) };
}

function raise(value: Dyadic, exponent: number, bits: number): Enclosure {
    let result = Enclosure.exactly(Dyadic.one);
    let square = Enclosure.exactly(value);
    let k = exponent;
    for (;;) {
        if (k % 2 === 1) result = Enclosure.multiply(result, square, bits);
        k = Math.floor(k / 2);
        if (k === 0) return result;
        square = Enclosure.multiply(square, square, bits);
    }
}

/**
 * The quantities the readings of PMT compute in doubles, each a product of powers of r (the rate), pv, fv, s
 * (pv+fv), g, e (g-1), b (1+rate) and c (1+rate*type: 1, or b at the start), numerator before the slash.
 */
const quantities = [
    'r', 'pv', 'fv', 's', 'g', '/ g', 'e', 'b', '/ b', 'c', '/ c',
    'e / r', 'r / e', 'e / g', 'e / g r', 'c e', 'c e / r', 'c e / g', 'c e / g r', 'c / r', 'r / c',
    'b g', 'b e', 'r g',
    'pv g', 'fv / g', 'pv r', 'fv r', 'pv r g', 'fv r / g',
    'pv g / e', 'fv / e', 'r g / e', 'pv r g / e', 'fv r / e',
    's r', 's / e', 's r / e',
    'pv r g / c e', 'fv r / c e',
];

/** At the start, also the ways from the payment at the end to it: the payment at the end times the rate, and over 1+rate. */
const atStartQuantities = ['pv r r g / e', 'fv r r / e', 'pv r r g / b e', 'fv r r / b e'];

/** The quantities Excel was seen to carry above a double's range while it gave the exact payment rounded once (PMT-038 to PMT-041). */
const mayPassAbove = new Set(['g', 'e', 'e / r', 'c e', 'c e / r', 'b g', 'b e', 'r g', 'pv g', 'pv r g']);

/** The quantities Excel was seen to carry below a double's normal numbers while it gave the exact payment rounded once (PMT-038, PMT-044). */
const mayFallBelow = new Set(['/ g', 'r / e', 's / e', 's r / e', 'pv r', 'pv r g', 's r']);

const factors = ['r', 'pv', 'fv', 's', 'g', 'e', 'b', 'c'];

/** A quantity as the power of each factor, and which sides of the range it must lie within. */
interface Bound {
    readonly powers: readonly number[];
    readonly above: boolean;
    readonly below: boolean;
}

function boundOf(quantity: string): Bound {
    return { powers: powersOf(quantity), above: !mayPassAbove.has(quantity), below: !mayFallBelow.has(quantity) };
}

/** A quantity as the power of each factor, in the order of `factors`. */
function powersOf(quantity: string): number[] {
    const powers = factors.map(() => 0);
    let sign = 1;
    for (const name of quantity.split(' ').filter(part => part.length > 0)) {
        if (name === '/') sign = -1;
        else {
            const at = factors.indexOf(name);
            if (at < 0) throw new Error(`Internal: ${name} is no factor of a PMT quantity.`);
            powers[at] = (powers[at] as number) + sign; // oxlint-disable-line typescript/no-unsafe-type-assertion -- at is a factor's index
        }
    }
    return powers;
}

let bounds: { readonly atEnd: readonly Bound[]; readonly atStart: readonly Bound[] } | undefined;

function boundsOf(atStart: boolean): readonly Bound[] {
    bounds ??= { atEnd: quantities.map(boundOf), atStart: [...quantities, ...atStartQuantities].map(boundOf) };
    return atStart ? bounds.atStart : bounds.atEnd;
}

function fits(value: Dyadic): boolean {
    return value.isZero || (Dyadic.compare(value.abs(), leastFit) >= 0 && Dyadic.compare(value.abs(), mostFit) <= 0);
}

/**
 * Whether no reading of PMT magnifies its own roundings more than 2^6 times (the financial area's bound,
 * 2026-10-12): the sum pv g + fv and its kin, the exponent's rounding, and at the start the payment at the end
 * less its rate's share and LibreOffice's divisor, as the C# remarks list them.
 */
function conditioned(r: Dyadic, onePlusRate: Dyadic, periods: number, pv: Dyadic, fv: Dyadic, sum: Dyadic, growth: Enclosure, lessOne: Enclosure,
    owed: Enclosure, atStart: boolean, bits: number): boolean {
    if (lessOne.containsZero || growth.containsZero) return false;
    // With no pv and no fv every reading adds only zeros, and its payment is 0; but LibreOffice's divisor at the
    // start, which can come out as 0 and give 0/0.
    const owesNothing = pv.isZero && fv.isZero;
    if (!owesNothing && owed.containsZero) return false;
    const most = growth.magnitudes().most;
    const lessOneLeast = lessOne.magnitudes().least;
    const logarithm = Enclosure.log1p(r, bits);
    const logMost = Dyadic.compare(logarithm.high.abs(), logarithm.low.abs()) >= 0 ? logarithm.high.abs() : logarithm.low.abs();
    const exponentMost = Dyadic.of(Math.abs(periods)).times(logMost);
    const limit = Dyadic.one.scale(mostMagnification);
    const within = (terms: Dyadic, result: Dyadic): boolean => Dyadic.compare(terms, limit.times(result)) <= 0;
    if (!owesNothing) {
        const owedLeast = owed.magnitudes().least;
        if (!within(pv.abs().times(Dyadic.one.plus(most)).plus(fv.abs()), owedLeast)) return false;
        if (!within(exponentMost.times(sum.abs()).times(most), lessOneLeast.times(owedLeast))) return false;
        if (atStart && !within(onePlusRate.abs().plus(r.abs()), Dyadic.one)) return false;
        if (atStart && !within(exponentMost.times(pv.abs()).times(most), owedLeast)) return false;
    }
    const periodsPlusOne = Dyadic.of(periods).plus(Dyadic.one).abs();
    return !atStart || within(periodsPlusOne.times(logMost).times(onePlusRate).times(most).plus(r.abs()), onePlusRate.times(lessOneLeast));
}

interface Magnitudes {
    readonly least: Dyadic;
    readonly most: Dyadic;
}

/** The magnitudes of a number known exactly; `undefined` for 0, which has none. */
function exact(value: Dyadic): Magnitudes | undefined {
    return value.isZero ? undefined : { least: value.abs(), most: value.abs() };
}

function quantitiesFit(r: Dyadic, pv: Dyadic, fv: Dyadic, sum: Dyadic, growth: Enclosure, lessOne: Enclosure, onePlusRate: Dyadic, atStart: boolean): boolean {
    if (growth.containsZero || lessOne.containsZero) return false;
    const one: Magnitudes = { least: Dyadic.one, most: Dyadic.one };
    const magnitudes: readonly (Magnitudes | undefined)[] = [
        exact(r), exact(pv), exact(fv), exact(sum), growth.magnitudes(), lessOne.magnitudes(), exact(onePlusRate),
        atStart ? exact(onePlusRate) : one,
    ];
    for (const { powers, above, below } of boundsOf(atStart)) {
        let numeratorLeast = Dyadic.one;
        let numeratorMost = Dyadic.one;
        let denominatorLeast = Dyadic.one;
        let denominatorMost = Dyadic.one;
        let zero = false;
        for (let f = 0; f < factors.length && !zero; f++) {
            const power = powers[f] as number; // oxlint-disable-line typescript/no-unsafe-type-assertion -- a power for each factor
            if (power === 0) continue;
            const m = magnitudes[f];
            if (m === undefined) {
                zero = true; // exactly 0 in every reading
                continue;
            }
            for (let i = 0; i < Math.abs(power); i++) {
                if (power > 0) {
                    numeratorLeast = numeratorLeast.times(m.least);
                    numeratorMost = numeratorMost.times(m.most);
                } else {
                    denominatorLeast = denominatorLeast.times(m.least);
                    denominatorMost = denominatorMost.times(m.most);
                }
            }
        }
        if (zero) continue;
        // The quantity lies within [numeratorLeast / denominatorMost, numeratorMost / denominatorLeast].
        if (below && Dyadic.compare(numeratorLeast, leastFit.times(denominatorMost)) < 0) return false;
        if (above && Dyadic.compare(numeratorMost, mostFit.times(denominatorLeast)) > 0) return false;
    }
    return true;
}
