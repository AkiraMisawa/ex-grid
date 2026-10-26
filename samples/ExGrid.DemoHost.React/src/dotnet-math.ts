// .NET's `Math.Round(value, digits)`: scaled by the power of ten, rounded half to even, and scaled back, so a
// page here writes the values the Blazor hosts write from the same arithmetic (ADR-0350 section 4).

/** `value` rounded to `digits` decimal places as .NET's `Math.Round(value, digits)` rounds it. */
export function dotNetRound(value: number, digits: number): number {
    const power = 10 ** digits;
    const scaled = value * power;
    const floor = Math.floor(scaled);
    const rest = scaled - floor;
    const even = rest > 0.5 || (rest === 0.5 && floor % 2 !== 0) ? floor + 1 : floor;
    return even / power;
}
