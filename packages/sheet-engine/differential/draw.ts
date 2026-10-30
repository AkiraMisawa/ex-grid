// What every differential run draws from: a seeded generator, the cultures the Script Core holds data for,
// and numbers written as a Formula writes them, crowded near the places a last bit or a rounding goes wrong.

import { random, seedOfRun } from './oracle';

/** The cultures whose data the Script Core is held to. */
export const cultures = ['invariant', 'en-US', 'en-GB', 'de-DE', 'fr-FR', 'ja-JP'] as const;

/** A seeded draw, so a failing run is repeated by the seed in its test's name. */
export interface Draw {
    readonly seed: number;
    /** A number in [0, 1). */
    next(): number;
    /** A whole number in [0, below). */
    int(below: number): number;
    pick<T>(list: readonly T[]): T;
    /** True with probability `p`. */
    chance(p: number): boolean;
}

export function draw(): Draw {
    const seed = seedOfRun();
    const next = random(seed);
    const int = (below: number): number => Math.floor(next() * below);
    return {
        seed,
        next,
        int,
        pick<T>(list: readonly T[]): T {
            const chosen = list[int(list.length)];
            if (chosen === undefined) throw new Error('Internal: picked from an empty list.');
            return chosen;
        },
        chance: (p: number): boolean => next() < p,
    };
}

function digits(d: Draw, count: number): string {
    return Array.from({ length: count }, () => String(d.int(10))).join('');
}

/**
 * A number literal as a Formula writes it, at most fifteen significant digits: small whole numbers, decimals
 * ending in 5 at the digit a ROUND cuts, the edges of a double's range, and plain random ones.
 */
export function literal(d: Draw): string {
    switch (d.int(8)) {
        case 0: return String(d.int(20));
        case 1: return `${String(d.int(1000))}.${digits(d, d.int(4))}5`;
        case 2: return d.pick(['0.1', '0.2', '0.3', '0.5', '1.5', '2.5', '0.125', '1E+15', '1E+16', '9.99999999999999E+307', '1E-307', '2.2250738585072E-308', '4.94065645841247E-324', '123456789012345', '0.000001']);
        case 3: return digits(d, 1 + d.int(15)).replace(/^0+(?=\d)/u, '');
        case 4: {
            const whole = digits(d, 1 + d.int(7)).replace(/^0+(?=\d)/u, '');
            return `${whole}.${digits(d, 1 + d.int(15 - whole.length))}`;
        }
        case 5: return `${String(1 + d.int(9))}.${digits(d, d.int(14))}E${d.pick(['+', '-'])}${String(d.int(310))}`;
        case 6: return `0.${'0'.repeat(d.int(6))}${digits(d, 1 + d.int(9))}`;
        default: return `${String(d.int(100))}.${d.pick(['45', '55', '05', '15', '25', '35', '65', '75', '85', '95', '005', '0049999999999'])}`;
    }
}

/**
 * A number written as an expression whose value is a double no literal of fifteen digits reaches: a literal
 * nudged by a few ulps, π and its fractions, powers of two.
 */
export function exactish(d: Draw): string {
    switch (d.int(6)) {
        case 0: return `(${literal(d)}*(1+${d.pick(['', '-'])}2^-${String(40 + d.int(13))}))`;
        case 1: return `(PI()${d.pick(['', '/2', '/4', '/3', '/6', '*2', '*1000000', '*1E+15', '/180'])})`;
        case 2: return `(2^${d.pick(['', '-'])}${String(d.int(1075))})`;
        case 3: return `(${literal(d)}/${literal(d)})`;
        case 4: return `(1${d.pick(['+', '-'])}2^-${String(1 + d.int(53))})`;
        default: return `(${String(1 + d.int(99))}/${String(1 + d.int(99))})`;
    }
}
