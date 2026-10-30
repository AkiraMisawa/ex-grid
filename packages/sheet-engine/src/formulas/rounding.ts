// Excel's ROUND, ROUNDUP and ROUNDDOWN, and the rounding the number formats share with them.
import { fifteenDigits } from '../number-text';

/** How ROUND, ROUNDUP and ROUNDDOWN treat the digits they drop. */
export type Rounding = 'halfAwayFromZero' | 'awayFromZero' | 'towardZero';

/** Excel's ROUND: half away from zero (`roundAt`). */
export function roundHalfAwayFromZeroAt(number: number, digits: number): number {
    return roundAt(number, digits, 'halfAwayFromZero');
}

/**
 * Excel's ROUND, ROUNDUP and ROUNDDOWN, applied to the number as Excel holds it to 15 significant digits —
 * so 2.675, which is 2.67499999999999982236431605997495353221893310546875 as a double, rounds to 2.68 as its
 * decimal spelling says, and 0.1+0.2 rounds up to 0.3 at one digit, not 0.4. `digits` is already a whole
 * number.
 */
export function roundAt(number: number, digits: number, rounding: Rounding): number {
    if (number === 0) return 0;
    const { digits: mantissa, exponent } = fifteenDigits(Math.abs(number));
    // How many of the 15 digits sit at or above the 10^-digits place.
    const kept = exponent + 1 + digits;
    if (kept >= 15) return number;
    if (kept < 0) {
        // Every digit is dropped: a number below half the place is 0, and away from zero it is the place itself.
        if (rounding !== 'awayFromZero') return 0;
        const place = powerOfTen(-digits);
        return number < 0 ? -place : place;
    }
    const retained = mantissa.slice(0, kept);
    let roundUp: boolean;
    switch (rounding) {
        case 'halfAwayFromZero': roundUp = mantissa.charAt(kept) >= '5'; break;
        case 'awayFromZero': roundUp = /[1-9]/.test(mantissa.slice(kept)); break;
        case 'towardZero': roundUp = false; break;
        default: return unreachableRounding(rounding);
    }
    // An integer of at most 16 digits, exact in a BigInt as it is in C#'s decimal.
    const magnitude = (kept === 0 ? 0n : BigInt(retained)) + (roundUp ? 1n : 0n);
    if (magnitude === 0n) return 0;
    // Placed back at 10^(exponent + 1 - kept), and read as .NET reads its spelling: correctly rounded.
    const result = Number(`${magnitude.toString()}E${String(exponent + 1 - kept)}`);
    return number < 0 ? -result : result;
}

/**
 * `Math.Pow(10, n)` for a whole `n`, correctly rounded, as .NET's `Math.Pow` gives it on every platform the
 * engine is held to: the double nearest 10^n, read from its spelling.
 */
export function powerOfTen(n: number): number {
    if (!Number.isInteger(n)) throw new Error(`Internal: 10^${String(n)} is asked for a whole power only.`);
    // Past the doubles' range, as Math.Pow overflows to infinity and underflows to zero; String(n) would be spelt with an exponent.
    if (n > 400) return Infinity;
    if (n < -400) return 0;
    return Number(`1e${String(n)}`);
}

function unreachableRounding(rounding: never): never {
    throw new Error(`Internal: ${String(rounding)} is no Rounding.`);
}
