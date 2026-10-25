// C#'s Math.Round sends a tie to the even number; the Script Core rounds what the C# core rounds the same
// way (CONVENTIONS.md), or the two draw a size a pixel apart on every half.
import { expect, it } from 'vitest';
import { roundHalfToEven } from '../src/rounding';

it.each([
    [10.5, 10], [11.5, 12], [16.5, 16], [0.5, 0], [1.5, 2], [-2.5, -2], [-3.5, -4],
    [10.4, 10], [10.6, 11], [-10.6, -11], [7, 7],
])('ADR-0350: %s rounds to %s, as Math.Round does', (value, rounded) => {
    expect(roundHalfToEven(value)).toBe(rounded);
});

it('ADR-0350: a value that is not finite is left as it is, as Math.Round leaves it', () => {
    expect(roundHalfToEven(Number.NaN)).toBeNaN();
    expect(roundHalfToEven(Number.POSITIVE_INFINITY)).toBe(Number.POSITIVE_INFINITY);
});
