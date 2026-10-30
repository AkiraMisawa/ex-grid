// A double's and a float's text as .NET writes and reads it in the invariant culture, for the View State's
// JSON (ADR-0207): `ToString("R")`, the shortest text that reads back as the same value, laid out as .NET's
// general format lays it out, and `TryParse` under AllowLeadingSign, AllowDecimalPoint and AllowExponent,
// rounded correctly to the type. JavaScript's own text agrees with .NET's only between 10^-4 and 10^15, and
// it has no float at all, so both are made here.

/** The most digits .NET's round-trip format writes without an exponent, past the digits the value needs:
 * `TNumber.MaxRoundTripDigits`, 17 for a double and 9 for a float. */
const roundTripDigits = Object.freeze({ double: 17, single: 9 });

/** Which of the two floating-point types a text is for. */
export type FloatType = keyof typeof roundTripDigits;

/** `value.ToString("R", CultureInfo.InvariantCulture)` for a finite double, or for a float held as the
 * double it widens to. */
export function roundTripText(value: number, type: FloatType): string {
    if (!Number.isFinite(value)) throw new Error(`Internal: ${value} has no round-trip text; it is refused before it is written.`);
    const negative = value < 0 || Object.is(value, -0);
    if (value === 0) return negative ? '-0' : '0';
    const { digits, scale } = type === 'double' ? shortestDouble(Math.abs(value)) : shortestSingle(Math.abs(value));
    return (negative ? '-' : '') + generalLayout(digits, scale, Math.max(digits.length, roundTripDigits[type]));
}

/** The shortest digits that read back as the double, and the power of ten they stand under: the value is
 * 0.`digits` × 10^`scale`. JavaScript's `toExponential()` with no argument writes exactly those. */
function shortestDouble(magnitude: number): { readonly digits: string; readonly scale: number } {
    return digitsOf(magnitude.toExponential());
}

/** The shortest digits that read back as the float: the fewest significant digits, rounded from the
 * float's exact value, that a float reading rounds to it. */
function shortestSingle(magnitude: number): { readonly digits: string; readonly scale: number } {
    for (let precision = 1; precision < 9; precision++) {
        const text = magnitude.toExponential(precision - 1);
        if (parseFloatText(text, 'single') === magnitude) return digitsOf(text);
    }
    return digitsOf(magnitude.toExponential(8));
}

function digitsOf(exponential: string): { readonly digits: string; readonly scale: number } {
    const [mantissa = '', exponent = ''] = exponential.split('e');
    return { digits: mantissa.replace('.', '').replace(/0+$/u, ''), scale: Number(exponent) + 1 };
}

/** .NET's FormatGeneral: the digits with a point where the scale puts it, or one digit before the point and
 * an exponent of at least two digits where the scale lies past `maxDigits` or below -3. */
function generalLayout(digits: string, scale: number, maxDigits: number): string {
    const scientific = scale > maxDigits || scale < -3;
    const point = scientific ? 1 : scale;
    let text = point > 0 ? digits.slice(0, point).padEnd(point, '0') : '0';
    const rest = point > 0 ? digits.slice(point) : digits;
    if (rest.length > 0 || point < 0) text += `.${'0'.repeat(Math.max(0, -point))}${rest}`;
    if (scientific) {
        const exponent = scale - 1;
        text += `E${exponent < 0 ? '-' : '+'}${String(Math.abs(exponent)).padStart(2, '0')}`;
    }
    return text;
}

// .NET's float styles in the invariant culture: a sign, digits with at most one point among or around them
// (at least one digit), and an exponent. No space, no group separator, no other sign.
const floatPattern = /^([+-]?)(\d*)(?:\.(\d*))?(?:[eE]([+-]?\d+))?$/u;

/**
 * The value `text` reads as under `double.TryParse` or `float.TryParse` with the float styles in the invariant
 * culture: rounded correctly to the type, and infinite past its range. `undefined` where the text is not a
 * number in that form. A text that names NaN or an infinity by its symbol is `undefined` too: no reader of the
 * View State takes one, so the difference never shows.
 */
export function parseFloatText(text: string, type: FloatType): number | undefined {
    const match = floatPattern.exec(text);
    if (match === null) return undefined;
    const [, sign = '', whole = '', fraction = '', exponent = '0'] = match;
    if (whole.length + fraction.length === 0) return undefined;
    if (type === 'double') {
        const value = Number(`${whole === '' ? '0' : whole}.${fraction === '' ? '0' : fraction}e${exponent}`);
        return sign === '-' ? -value : value;
    }
    const value = singleOf(whole + fraction, BigInt(exponent) - BigInt(fraction.length));
    return sign === '-' ? -value : value;
}

const floatSignificand = 1n << 24n;

/** The float nearest to `digits` × 10^`power`, a tie to the even one, as the double it widens to. Made with
 * whole numbers, because a double between would round twice. */
function singleOf(digits: string, power: bigint): number {
    const trimmed = digits.replace(/^0+/u, '');
    if (trimmed === '') return 0;
    // Past these the value is beyond a float's range, or below half its smallest step, whatever the digits.
    const magnitude = BigInt(trimmed.length) + power;
    if (magnitude > 40n) return Infinity;
    if (magnitude < -46n) return 0;
    const mantissa = BigInt(trimmed);
    const numerator = power >= 0n ? mantissa * 10n ** power : mantissa;
    const denominator = power >= 0n ? 1n : 10n ** -power;
    // The binary exponent that leaves 24 bits before the point, or the subnormals' fixed one.
    let exponent = Math.max(bitLength(numerator) - bitLength(denominator) - 24, -149);
    let quotient = 0n;
    let remainder = 0n;
    let divisor = 1n;
    for (;;) {
        const [n, d] = exponent >= 0 ? [numerator, denominator << BigInt(exponent)] : [numerator << BigInt(-exponent), denominator];
        quotient = n / d;
        remainder = n - quotient * d;
        divisor = d;
        if (quotient >= floatSignificand) exponent++;
        else if (quotient < floatSignificand >> 1n && exponent > -149) exponent--;
        else break;
    }
    if (2n * remainder > divisor || (2n * remainder === divisor && quotient % 2n === 1n)) quotient++;
    if (quotient === floatSignificand) {
        quotient >>= 1n;
        exponent++;
    }
    // The largest float is (2^24 - 1) × 2^104.
    if (exponent > 104) return Infinity;
    return Number(quotient) * 2 ** exponent;
}

function bitLength(value: bigint): number {
    return value.toString(2).length;
}
