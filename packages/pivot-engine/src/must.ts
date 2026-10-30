// What the engine knows is there, as core/src/must.ts says it for the Script Core: a miss is a defect in
// the engine, said by throwing where a `!` would read on with `undefined` (CONVENTIONS.md, "No quiet
// fallback"). The packages share no code, so each carries its own.

/** The element at `index`, which the caller knows is there. A miss throws: it is a defect, never an answer. */
export function elementAt<T extends object | string | number | boolean | bigint | null>(list: ArrayLike<T>, index: number): T {
    const value = list[index];
    if (value === undefined) {
        throw new Error(`Internal: index ${index} is outside a list of ${list.length}.`);
    }
    return value;
}

/** The value, which the caller knows is there. `undefined` throws, naming what was missing. */
export function defined<T>(value: T | undefined, what: string): T {
    if (value === undefined) {
        throw new Error(`Internal: ${what} is missing.`);
    }
    return value;
}

/** The smallest 64-bit integer, as C#'s long holds it. */
export const int64Min = -(1n << 63n);

/** The largest 64-bit integer, as C#'s long holds it. */
export const int64Max = (1n << 63n) - 1n;

/** The end of a switch that has taken every member of a union; a value from outside it throws here. */
export function unreachable(value: never): never {
    throw new Error(`Internal: ${String(value)} is not a value this switch takes.`);
}
