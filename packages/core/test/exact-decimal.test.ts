// Layer 1: C#'s decimal as the Script Core computes a Selection Summary in it (ADR-0130, ADR-0350 §16). Each
// vector is what .NET 10 answered for the same operands: a sum at the edge of a decimal's range, one that gives
// up places, a quotient, a rounding to ten places, the double a decimal converts to, and a double's "G10"
// text. They were picked from 200,000 fuzzed operations, every one of which the Script Core matched.
import { describe, expect, it } from 'vitest';
import { generalText } from '../src/summary/grid-summary';
import { decimalAdd, decimalCanonical, decimalDivide, decimalOf, decimalRound, decimalText, decimalToNumber, type ExactDecimal } from '../src/summary/exact-decimal';

type Written = readonly [bigint, number];

function decimal([mantissa, scale]: Written): ExactDecimal {
    return { mantissa, scale };
}

function bitsOf(value: number): bigint {
    const view = new DataView(new ArrayBuffer(8));
    view.setFloat64(0, value);
    return view.getBigInt64(0);
}

describe('the exact decimal agrees with .NET 10', () => {
    it('ADR-0350 §16: a sum keeps every place 96 bits hold, rounds half to even past them, and overflows past the whole part', () => {
        const vectors: readonly (readonly [Written, Written, string])[] = [
        [[79228162514264337593543950335n, 0], [1n, 0], "overflow"],
        [[79228162514264337593543950335n, 0], [4n, 1], "79228162514264337593543950335"],
        [[79228162514264337593543950335n, 0], [5n, 1], "overflow"],
        [[79228162514264337593543950334n, 0], [5n, 1], "79228162514264337593543950334"],
        [[79228162514264337593543950333n, 0], [5n, 1], "79228162514264337593543950334"],
        [[79228162514264337593543950334n, 1], [5n, 2], "7922816251426433759354395033.4"],
        [[79228162514264337593543950335n, 1], [5n, 2], "7922816251426433759354395034"],
        [[-79228162514264337593543950335n, 0], [-1n, 0], "overflow"],
        [[-16975089918059008507956416754n, 2], [1121688784n, 22], "-169750899180590085079564167.54"],
        [[1055060810331278517064n, 4], [-33093508128900372600927022640n, 17], "105505750098046562.70267399073"],
        [[-3175314353696375052056951982n, 2], [1882997415n, 14], "-31753143536963750520569519.82"],
        [[-31436514854478499997766471102n, 16], [9163731814861295056780663748n, 14], "88493666663165.10056802999037"],
        [[1737778726n, 12], [-14998181333372517979619397318n, 6], "-14998181333372517979619.39558"],
        ];
        for (const [a, b, sum] of vectors) {
            const got = decimalAdd(decimal(a), decimal(b));
            expect(got === undefined ? 'overflow' : decimalText(decimalCanonical(got)), `${a.join('e-')} + ${b.join('e-')}`).toBe(sum);
        }
    });
    it('ADR-0130: an Average divides to as many places as a decimal holds, the last rounded half to even', () => {
        const vectors: readonly (readonly [Written, number, string])[] = [
        [[4166517994394872682n, 2], 1821601, "22872835458.450410830911928573"],
        [[-1497907209n, 2], 1296089, "-11.557132334276427004626997066"],
        [[12125185466652756326860n, 2], 183188, "661898457685697.55261589187065"],
        ];
        for (const [a, count, quotient] of vectors) expect(decimalText(decimalDivide(decimal(a), count)), `${a.join('e-')} / ${count}`).toBe(quotient);
    });
    it('ADR-0130: a figure is shown to ten places, rounded half to even', () => {
        const vectors: readonly (readonly [Written, string])[] = [
        [[-33535822165736227359214112450n, 12], "-33535822165736227.3592141124"],
        [[1601037925n, 11], "0.0160103792"],
        [[-8510209570671916416043222805n, 11], "-85102095706719164.160432228"],
        [[-33210614235924477470317666885n, 11], "-332106142359244774.7031766688"],
        [[-6852513447545907719155228105n, 11], "-68525134475459077.191552281"],
        ];
        for (const [a, rounded] of vectors) expect(decimalText(decimalRound(decimal(a), 10)), a.join('e-')).toBe(rounded);
    });
    it('ADR-0130: a decimal converts to the double C# converts it to, which is not always the nearest', () => {
        const vectors: readonly (readonly [Written, bigint])[] = [
        [[-11161483670314071604600485224n, 2], -4226886811160537788n],
        [[13988541274824822302159300957n, 27], 4624064466713853135n],
        [[-1379414260572691688n, 2], -4375105366128629149n],
        [[30593948n, 23], 4374696828396669319n],
        [[20748085122697046022483408054n, 23], 4686369000602473982n],
        ];
        for (const [a, bits] of vectors) expect(bitsOf(decimalToNumber(decimal(a))), a.join('e-')).toBe(bits);
    });
    it('ADR-0350 §16: a number is read as .NET converts a double to a decimal', () => {
        const vectors: readonly (readonly [number, string])[] = [
        [76378300123092.9, "76378300123092.9"],
        [1.5, "1.5"],
        [666.07, "666.07"],
        ];
        for (const [value, read] of vectors) {
            const got = decimalOf(value);
            expect(got === undefined ? 'none' : decimalText(got), String(value)).toBe(read);
        }
    });
    it('ADR-0130: a double figure reads as .NET\'s "G10" writes it, ties to even and exponents signed', () => {
        const vectors: readonly (readonly [number, string])[] = [
        [6859541279.5, "6859541280"],
        [1065692281.5, "1065692282"],
        [4104405596.5, "4104405596"],
        [6164935055.5, "6164935056"],
        [7.5641326269, "7.564132627"],
        [5.5179E-07, "5.5179E-07"],
        [69938786732.93571, "6.993878673E+10"],
        [37535842140.5, "3.753584214E+10"],
        [1.78871514825E-08, "1.788715148E-08"],
        [1.80111817100829E+197, "1.801118171E+197"],
        ];
        for (const [value, text] of vectors) expect(generalText(value, 10), String(value)).toBe(text);
    });
});
