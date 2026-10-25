// A Number column's values on one number line (ADR-0023). C#'s reference implementation holds every number
// as a decimal, so a double is read as .NET's conversion to decimal reads it: scaled by a power of ten to
// fifteen significant digits, at most 28 decimal places, the scaling done in double arithmetic and the last
// digit rounded half to even. The Script Core reads a `number` by the same steps, so 0.1 + 0.2 equals 0.3
// here as it does there, and a value lands on the digit C# lands it on. A `bigint` is kept exact, as C#
// keeps a long.

/** The largest magnitude a decimal holds, which C# refuses beyond. */
const decimalMax = 79228162514264337593543950335n;

/** A Number value as it is compared: a `number` holding a decimal of at most fifteen significant digits
 * below 2^53, or a `bigint` for a whole value beyond, where a double no longer holds the decimal exactly
 * (the key of 68554523619150056 is 68554523619150000, which no double is). */
export type NumberKey = number | bigint;

/** The value as it is compared, or `undefined` when it cannot be a Number; `numberRefusal` says why. */
export function numberKey(value: number | bigint): NumberKey | undefined {
    if (typeof value === 'bigint') return value > decimalMax || value < -decimalMax ? undefined : value;
    const parts = decimalPartsOf(value);
    if (parts === undefined) return undefined;
    if (parts.digits === 0) return 0;
    const key = decimalOf(parts.digits, parts.power);
    return parts.negative ? negated(key) : key;
}

/**
 * The decimal C# reads a `number` as, `digits` × 10^-`power` with the sign apart: .NET's VarDecFromR8,
 * fifteen significant digits at most and at most 28 decimal places. `undefined` where no decimal holds the
 * value: not finite, beyond a decimal's range, or so small in magnitude that it would read as 0 (a non-zero
 * value that read as 0 would pass equals 0 and tie with true zeros: quietly flattened, on a grid that
 * displays risk numbers).
 */
export function decimalPartsOf(value: number): { readonly digits: number; readonly power: number; readonly negative: boolean } | undefined {
    if (!Number.isFinite(value)) return undefined;
    const exponent = binaryExponent(value);
    if (exponent > 96) return undefined;
    if (value === 0) return { digits: 0, power: 0, negative: false };
    if (exponent < -94) return undefined;
    // The steps of .NET's VarDecFromR8. The power of ten that brings the value to fifteen digits is
    // estimated from the binary exponent (19728 / 2^16 is log10 2), and corrected by one if it fell short.
    let power = 14 - ((exponent * 19728) >> 16);
    let scaled = Math.abs(value);
    if (power >= 0) {
        power = Math.min(power, 28);
        scaled *= tenTo(power);
    } else if (power !== -1 || scaled >= 1e15) {
        scaled /= tenTo(-power);
    } else {
        power = 0;
    }
    if (scaled < 1e14 && power < 28) {
        scaled *= 10;
        power += 1;
    }
    const digits = roundHalfToEven(scaled);
    if (digits === 0) return undefined;
    return { digits, power, negative: Math.sign(value) === -1 };
}

/** Why a value cannot be a Number, or `undefined` when it can; the caller names the column. */
export function numberRefusal(value: number | bigint): string | undefined {
    if (numberKey(value) !== undefined) return undefined;
    if (typeof value === 'number' && !Number.isFinite(value)) return 'is not a finite number';
    if (typeof value === 'number' && binaryExponent(value) <= 96) return 'is too small in magnitude for a Number to represent';
    return 'is outside the range a Number can represent';
}

/** Orders two Number keys on one line, a bigint against a number exactly. */
export function compareNumbers(a: NumberKey, b: NumberKey): number {
    if (a < b) return -1;
    return a > b ? 1 : 0;
}

/** A text that is the same for two keys exactly when they are equal, for a set of them. */
export function numberIdentity(key: NumberKey): string {
    return typeof key === 'number' && Number.isInteger(key) ? BigInt(key).toString() : key.toString();
}

/** fifteen digits at a decimal scale, as the double that holds them, or as a bigint from 2^53. */
function decimalOf(digits: number, power: number): NumberKey {
    // Dividing by an exact power of ten (to 10^22) is the one correctly rounded double of the quotient, as
    // parsing the decimal would be; a scale beyond it is parsed.
    if (power > 22) return Number(`${digits.toString()}e-${power.toString()}`);
    if (power >= 0) return digits / tenTo(power);
    const whole = digits * tenTo(-power);
    return whole < 2 ** 53 ? whole : BigInt(digits) * 10n ** BigInt(-power);
}

function negated(key: NumberKey): NumberKey {
    return -key;
}

let bitsView: DataView | undefined;

/** The binary exponent .NET reads a double's magnitude by: one more than the power of two below it. */
function binaryExponent(value: number): number {
    bitsView ??= new DataView(new ArrayBuffer(8));
    bitsView.setFloat64(0, value);
    return ((bitsView.getUint16(0) >> 4) & 0x7ff) - 1022;
}

let powersOfTen: readonly number[] | undefined;

/** 10^n as the double the literal 1En is, for n to 28, which `10 ** n` does not promise beyond 10^22. */
export function tenTo(power: number): number {
    powersOfTen ??= Object.freeze(Array.from({ length: 29 }, (_, n) => Number(`1e${n.toString()}`)));
    const ten = powersOfTen[power];
    if (ten === undefined) throw new Error(`Internal: 10^${power.toString()} is outside the table.`);
    return ten;
}

function roundHalfToEven(value: number): number {
    const whole = Math.floor(value);
    const fraction = value - whole;
    return fraction > 0.5 || (fraction === 0.5 && whole % 2 === 1) ? whole + 1 : whole;
}
