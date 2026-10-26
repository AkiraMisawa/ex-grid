// The arithmetic the C# engine does in .NET's way, where JavaScript's would land elsewhere.

/** The nearest whole number, a tie going to the even one: C#'s `Math.Round`. */
export function roundHalfToEven(value: number): number {
    const floor = Math.floor(value);
    if (value - floor !== 0.5) {
        return Math.round(value);
    }
    return floor % 2 === 0 ? floor : floor + 1;
}

/**
 * C#'s `Math.Round(value, digits)`: scaled by a power of ten, rounded to even, scaled back; a number of
 * sixteen digits or more is returned as it is, as .NET returns it.
 */
export function roundToDigits(value: number, digits: number): number {
    if (!Number.isInteger(digits) || digits < 0 || digits > 15) {
        throw new RangeError(`${String(digits)} digits is not between 0 and 15.`);
    }
    if (!(Math.abs(value) < 1e16)) {
        return value;
    }
    const power = 10 ** digits;
    return roundHalfToEven(value * power) / power;
}

const scratch = new DataView(new ArrayBuffer(8));

/** C#'s `Math.BitDecrement`: the largest number below `value`. */
export function bitDecrement(value: number): number {
    if (Number.isNaN(value) || value === -Infinity) {
        return value;
    }
    if (value === 0) {
        return -Number.MIN_VALUE;
    }
    scratch.setFloat64(0, value);
    const bits = scratch.getBigInt64(0);
    scratch.setBigInt64(0, value > 0 ? bits - 1n : bits + 1n);
    return scratch.getFloat64(0);
}

/** The least of `values`, read in one pass so a long series never meets the argument limit of `Math.min`; refused when there are none. */
export function least(values: Iterable<number>): number {
    let result = Infinity;
    let any = false;
    for (const v of values) {
        result = Math.min(result, v);
        any = true;
    }
    if (!any) {
        throw new RangeError('There is no least of no values.');
    }
    return result;
}

/**
 * The first of `items` whose value `before` puts ahead of every other's, as C#'s MinBy and MaxBy take it, so
 * a tie keeps the earlier item; refused when there are none.
 */
export function firstBy<T extends object | number | bigint>(items: readonly T[], value: (item: T) => number, before: (a: number, b: number) => boolean): T {
    const [head, ...rest] = items;
    if (head === undefined) {
        throw new RangeError('There is no first of no items.');
    }
    let best = head;
    for (const item of rest) {
        if (before(value(item), value(best))) {
            best = item;
        }
    }
    return best;
}

/** The greatest of `values`, as `least` reads them; refused when there are none. */
export function greatest(values: Iterable<number>): number {
    let result = -Infinity;
    let any = false;
    for (const v of values) {
        result = Math.max(result, v);
        any = true;
    }
    if (!any) {
        throw new RangeError('There is no greatest of no values.');
    }
    return result;
}
