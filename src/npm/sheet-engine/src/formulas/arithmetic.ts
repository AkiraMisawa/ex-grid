// The places where Excel's arithmetic is not plain IEEE arithmetic, as it was observed (ADR-0047, "Observed in
// Excel" and its second and third runs). Excel computes in doubles, and then adjusts: a final addition or
// subtraction that nearly cancels is 0, and so is SUM's last addition; a comparison of two numbers is made at
// Excel's precision; and a power is e^(b ln a), not the correctly rounded one, except a square root.
import { formatExponential } from '../dotnet';

/** 2^-49: a final addition whose operands are closer than this, relative to each, is 0. */
export const cancelTolerance = 1 / 562949953421312;

/**
 * Whether two numbers are equal at Excel's precision: identical, or the same when each is rounded to 15
 * significant digits. `=0.1+0.2=0.3` and `=1+4.8E-15=1` are TRUE in Excel; `=1+5E-15=1` and `=9+3E-14=9` are FALSE.
 */
export function approximatelyEqual(a: number, b: number): boolean {
    return a === b || atFifteenDigits(a) === atFifteenDigits(b);
}

/** A number rounded to 15 significant digits: .NET formats the exact decimal value of the double and rounds a midpoint away from zero. */
export function atFifteenDigits(x: number): number {
    return Number.isFinite(x) ? Number(formatExponential(x, 14)) : x;
}

function near(a: number, b: number, tolerance: number): boolean {
    if (a === b) return true;
    if (a === 0 || b === 0) return false;
    const difference = Math.abs(a - b);
    return difference < Math.abs(a) * tolerance && difference < Math.abs(b) * tolerance;
}

/**
 * A Formula's last operation, when it is an addition (a subtraction is the addition of the negated operand),
 * and SUM's last addition: 0 when the operands have opposite signs and are closer than 2^-49 of each, the
 * plain sum otherwise. Excel does this to the last operation only.
 */
export function finalAdd(a: number, b: number): number {
    const opposite = (a > 0 && b < 0) || (a < 0 && b > 0);
    return opposite && near(a, -b, cancelTolerance) ? 0 : a + b;
}

/**
 * `a^b`, or `undefined` where it has no real answer (`#NUM!`). An integer exponent is `Math.Pow`, and an
 * exponent of 0.5 is the square root, correctly rounded (ARITH-133). Any other exponent of a positive base is
 * e^(b ln a), as Excel's is: `=8^(1/3)` is 1.9999999999999998. A negative base with an exponent that is the
 * reciprocal of an odd integer has a real answer, −e^(ln(−a) b); other fractional exponents of a negative base
 * are `#NUM!`.
 */
export function power(a: number, b: number): number | undefined {
    if (a === 0 || b === Math.floor(b)) return Math.pow(a, b);
    if (a > 0) return b === 0.5 ? Math.sqrt(a) : Math.exp(Math.log(a) * b);
    const root = 1 / b;
    if (Number.isFinite(root) && root === Math.floor(root) && Math.abs(root % 2) === 1) return -Math.exp(Math.log(-a) * b);
    return undefined;
}
