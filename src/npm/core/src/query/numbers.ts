// A Number column's values on one number line (ADR-0023). C#'s reference implementation holds every number
// as a decimal, and so reads a double at fifteen significant digits and at most 28 decimal places; Excel
// compares at fifteen digits too. The Script Core reads a `number` the same way, so 0.1 + 0.2 equals 0.3
// here as it does there, and keeps a `bigint` exact, as C# keeps a long.

/** The largest magnitude a decimal holds, which C# refuses beyond. */
const decimalMax = 79228162514264337593543950335n;

/** A Number value as it is compared: a `number` at fifteen significant digits and 28 decimal places, or a
 * `bigint`. */
export type NumberKey = number | bigint;

/** Why a value cannot be a Number, or `undefined` when it can; the caller names the column. */
export function numberRefusal(value: number | bigint): string | undefined {
    if (typeof value === 'bigint') {
        return value > decimalMax || value < -decimalMax ? 'is outside the range a Number can represent' : undefined;
    }
    if (!Number.isFinite(value)) return 'is not a finite number';
    if (Math.abs(value) > Number(decimalMax)) return 'is outside the range a Number can represent';
    // A non-zero value that reads as 0 would pass equals 0 and tie with true zeros: quietly flattened, on a
    // grid that displays risk numbers.
    if (value !== 0 && numberKey(value) === 0) return 'is too small in magnitude for a Number to represent';
    return undefined;
}

/** The value as it is compared. Call only on a value `numberRefusal` accepts. */
export function numberKey(value: number | bigint): NumberKey {
    if (typeof value === 'bigint') return value;
    if (value === 0) return 0;
    // Fifteen significant digits, and no more of them than reach the 28th decimal place: a decimal's scale.
    const exponent = Number(value.toExponential().split('e')[1]);
    const digits = Math.min(15, 28 + exponent + 1);
    if (digits < 1) return Math.abs(value) >= 0.5e-28 ? Math.sign(value) * 1e-28 : 0;
    const key = Number(value.toPrecision(digits));
    // -0 is 0, as in a decimal.
    return key === 0 ? 0 : key;
}

/** Orders two Number keys on one line, a bigint against a number exactly. */
export function compareNumbers(a: NumberKey, b: NumberKey): number {
    if (a < b) return -1;
    return a > b ? 1 : 0;
}

/** A text that is the same for two keys exactly when they are equal, for a set of them. */
export function numberIdentity(key: NumberKey): string {
    if (typeof key === 'bigint') return key.toString();
    return Number.isInteger(key) ? BigInt(key).toString() : String(key);
}
