// The refusals the geometry shares. Each names the argument, as C#'s ArgumentOutOfRangeException does,
// so a Consumer told "is refused" knows which number to look at.

/** A whole number at or above `least`; anything else is a RangeError naming `name`. */
export function wholeAtLeast(value: number, least: number, name: string): number {
    if (!Number.isSafeInteger(value) || value < least) {
        throw new RangeError(`${name} is a whole number of at least ${least}, not ${value}.`);
    }
    return value;
}

/** A finite number; anything else is a RangeError naming `name` and saying `why`. */
export function finite(value: number, name: string, why: string): number {
    if (!Number.isFinite(value)) {
        throw new RangeError(`${why} (${name}: ${value})`);
    }
    return value;
}

/** A finite, positive number; anything else is a RangeError naming `name` and saying `why`. */
export function finitePositive(value: number, name: string, why: string): number {
    if (!Number.isFinite(value) || value <= 0) {
        throw new RangeError(`${why} (${name}: ${value})`);
    }
    return value;
}

/** Math.Clamp: `value` held between `min` and `max`, which must not cross. */
export function clamp(value: number, min: number, max: number): number {
    if (min > max) {
        throw new Error(`Internal: a clamp to [${min}, ${max}] has no room.`);
    }
    return Math.min(Math.max(value, min), max);
}
