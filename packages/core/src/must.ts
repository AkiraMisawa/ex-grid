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

/** The item at `index` of a list whose items may be any type, a Consumer's rows among them, where
 * `undefined` could be an item: the index, not the value, is checked. A miss throws, as `elementAt`'s does. */
export function itemAt<T>(list: readonly T[], index: number): T {
    if (!Number.isInteger(index) || index < 0 || index >= list.length) {
        throw new Error(`Internal: index ${index} is outside a list of ${list.length}.`);
    }
    return list[index] as T; // oxlint-disable-line typescript/no-unsafe-type-assertion -- the index was checked against the length above
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
