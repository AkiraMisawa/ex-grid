/**
 * The nearest whole number, a tie going to the even one: C#'s `Math.Round`, where JavaScript's
 * `Math.round` sends a tie up. A size the C# core rounds is rounded here, or the two cores draw it a
 * pixel apart whenever it lands on a half (ADR-0350).
 */
export function roundHalfToEven(value: number): number {
    const floor = Math.floor(value);
    if (value - floor !== 0.5) {
        return Math.round(value);
    }
    return floor % 2 === 0 ? floor : floor + 1;
}
