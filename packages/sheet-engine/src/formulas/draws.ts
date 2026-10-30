// A draw of RAND or RANDBETWEEN (ADR-0180), in 64-bit unsigned arithmetic held in BigInts.
import { toUpperInvariant } from '../dotnet';
import type { CellAddress } from '../cell-address';

const mask = (1n << 64n) - 1n;

/** SplitMix64's increment, the golden ratio's fractional part in 64 bits. */
const golden = 0x9E3779B97F4A7C15n;

/**
 * The draw's 64 bits: a function of the Workbook's seed, the name of the Sheet and the cell whose Formula calls
 * the function, which call in that Formula it is, and the count of changes since the Workbook was opened — and
 * of nothing else. The inputs are mixed by SplitMix64's finaliser, one after another; the name is read as Excel
 * compares Sheet names, ignoring case.
 */
export function drawOf(seed: bigint, sheet: string, cell: CellAddress, place: number, changes: bigint): bigint {
    let h = mix(seed);
    const upper = toUpperInvariant(sheet);
    for (let i = 0; i < upper.length; i++) h = mix(h ^ BigInt(upper.charCodeAt(i)));
    h = mix(h ^ BigInt(sheet.length));
    h = mix(h ^ BigInt.asUintN(32, BigInt(cell.row)));
    h = mix(h ^ BigInt.asUintN(32, BigInt(cell.column)));
    h = mix(h ^ BigInt.asUintN(32, BigInt(place)));
    return mix(h ^ BigInt.asUintN(64, changes));
}

/** A number in [0,1), evenly spread: the draw's top 53 bits, every double of that spacing equally likely. */
export function unitOf(draw: bigint): number {
    return Number(draw >> 11n) * (1 / 2 ** 53);
}

/**
 * A whole number from `bottom` to `top`, both included, every one equally likely: the draw scaled to the count
 * of them, a draw that would favour some numbers over others mixed again until one does not (Lemire's method,
 * with the rejection made a function of the draw). Both bounds are at most 2^53 in magnitude.
 */
export function between(draw: bigint, bottom: number, top: number): number {
    const count = BigInt.asUintN(64, BigInt(top) - BigInt(bottom)) + 1n;
    let current = draw;
    let scaled = current * count;
    if ((scaled & mask) < count) {
        const threshold = ((-count) & mask) % count;
        while ((scaled & mask) < threshold) {
            current = mix(current);
            scaled = current * count;
        }
    }
    return Number(BigInt(bottom) + (scaled >> 64n));
}

/** SplitMix64: the increment, then its finaliser. */
function mix(value: bigint): bigint {
    let z = (value + golden) & mask;
    z = ((z ^ (z >> 30n)) * 0xBF58476D1CE4E5B9n) & mask;
    z = ((z ^ (z >> 27n)) * 0x94D049BB133111EBn) & mask;
    return z ^ (z >> 31n);
}
