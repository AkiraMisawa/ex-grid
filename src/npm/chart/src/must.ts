// What the core knows is there. `noUncheckedIndexedAccess` makes every read of a list or a map say what
// happens on a miss; where the answer is "a miss is a defect in the core", these helpers say it by
// throwing, where a `!` would read on with `undefined` (CONVENTIONS.md, "No quiet fallback").

/** The element at `index`, which the caller knows is there. A miss throws: it is a defect, never an answer. */
export function elementAt<T extends object | string | number | boolean | bigint | symbol | null>(list: readonly T[], index: number): T {
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

/**
 * The end of a switch that has taken every member of a union. The type checker proves nothing reaches it;
 * a value from outside the union, which only an untyped caller can pass, throws here rather than falling
 * out of the switch as `undefined`.
 */
export function unreachable(value: never): never {
    throw new Error(`Internal: ${String(value)} is not a value this switch takes.`);
}
