// .NET's `Math.Round(value, digits)`: scaled by the power of ten, rounded half to even, and scaled back, so a
// page here writes the values the Blazor hosts write from the same arithmetic (ADR-0350 section 4). It is
// @ex-grid/chart's own `roundToDigits`, which the package does not export.

/** `value` rounded to `digits` decimal places as .NET's `Math.Round(value, digits)` rounds it. */
export function dotNetRound(value: number, digits: number): number {
    if (!Number.isInteger(digits) || digits < 0 || digits > 15) {
        throw new RangeError(`${String(digits)} digits is not between 0 and 15.`);
    }
    // A number of sixteen digits or more is returned as it is, as .NET returns it.
    if (!(Math.abs(value) < 1e16)) {
        return value;
    }
    const power = 10 ** digits;
    const scaled = value * power;
    const floor = Math.floor(scaled);
    // Math.round keeps the sign of a value that rounds to zero, as .NET does: -8.7e-17 is -0, which writes "-0".
    const even = scaled - floor !== 0.5 ? Math.round(scaled) : floor % 2 === 0 ? floor : floor + 1;
    return even / power;
}
