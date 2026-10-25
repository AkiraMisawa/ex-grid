// What the C# engine reads from .NET, spelt as .NET spells it, so that the TypeScript engine answers as
// the C# one does (ADR-0370, section 2): .NET's classes of characters, its case-insensitive ordinal
// comparison, its rounding of a midpoint to even, and its number formats "R", "E" and "G". JavaScript's
// own (`/\s/`, `toUpperCase`, `Math.round`, `toString`) differ from each in places a test rarely reaches,
// which is where an engine is quietly wrong.

/** Whether the UTF-16 code unit is an ASCII digit, `0` to `9` (`char.IsAsciiDigit`). */
export function isAsciiDigit(c: number): boolean {
    return c >= 0x30 && c <= 0x39;
}

/** Whether the code unit is an ASCII letter (`char.IsAsciiLetter`). */
export function isAsciiLetter(c: number): boolean {
    return (c >= 0x41 && c <= 0x5a) || (c >= 0x61 && c <= 0x7a);
}

/** Whether the code unit is an ASCII letter or digit (`char.IsAsciiLetterOrDigit`). */
export function isAsciiLetterOrDigit(c: number): boolean {
    return isAsciiLetter(c) || isAsciiDigit(c);
}

/** Whether the code unit is ASCII (`char.IsAscii`). */
export function isAscii(c: number): boolean {
    return c <= 0x7f;
}

/**
 * `char.IsWhiteSpace`: the C0 controls U+0009 to U+000D, U+0085, and the Unicode separators (Zs, Zl, Zp).
 * Not JavaScript's `\s`, which takes U+FEFF and leaves out U+0085.
 */
export function isWhiteSpace(c: number): boolean {
    if (c === 0x20 || (c >= 0x09 && c <= 0x0d) || c === 0x85 || c === 0xa0) return true;
    if (c < 0x1680) return false;
    return c === 0x1680 || (c >= 0x2000 && c <= 0x200a) || c === 0x2028 || c === 0x2029 || c === 0x202f || c === 0x205f
        || c === 0x3000;
}

const letter = /^\p{L}$/u;
const decimalDigit = /^\p{Nd}$/u;

/** `char.IsLetter`: a code unit of the categories Lu, Ll, Lt, Lm or Lo. A surrogate is never one, as in .NET. */
export function isLetter(c: number): boolean {
    if (c < 0x80) return isAsciiLetter(c);
    return letter.test(String.fromCharCode(c));
}

/** `char.IsDigit`: a code unit of the category Nd, any script's decimal digit. */
export function isDigit(c: number): boolean {
    if (c < 0x80) return isAsciiDigit(c);
    return decimalDigit.test(String.fromCharCode(c));
}

/** `string.Trim()`: the string without the white space (`isWhiteSpace`) at either end. */
export function trimWhiteSpace(text: string): string {
    let start = 0;
    let end = text.length;
    while (start < end && isWhiteSpace(text.charCodeAt(start))) start++;
    while (end > start && isWhiteSpace(text.charCodeAt(end - 1))) end--;
    return text.slice(start, end);
}

/** `string.Trim(char)`: the string without the given character at either end. */
export function trimChar(text: string, c: string): string {
    let start = 0;
    let end = text.length;
    while (start < end && text[start] === c) start++;
    while (end > start && text[end - 1] === c) end--;
    return text.slice(start, end);
}

/** `string.TrimEnd(char)`. */
export function trimEndChar(text: string, c: string): string {
    let end = text.length;
    while (end > 0 && text[end - 1] === c) end--;
    return text.slice(0, end);
}

/**
 * The invariant upper case of one code unit, by its simple mapping: a letter whose upper case is more than
 * one character (`ß`, `ŉ`) stays as it is, as `char.ToUpperInvariant` leaves it.
 */
export function upperInvariant(c: number): number {
    if (c < 0x80) return c >= 0x61 && c <= 0x7a ? c - 0x20 : c;
    const upper = String.fromCharCode(c).toUpperCase();
    return upper.length === 1 ? upper.charCodeAt(0) : c;
}

/** The invariant lower case of one code unit, by its simple mapping (`char.ToLowerInvariant`). */
export function lowerInvariant(c: number): number {
    if (c < 0x80) return c >= 0x41 && c <= 0x5a ? c + 0x20 : c;
    const lower = String.fromCharCode(c).toLowerCase();
    return lower.length === 1 ? lower.charCodeAt(0) : c;
}

/** `string.ToUpperInvariant()`: each code unit by its simple mapping, so the length never changes. */
export function toUpperInvariant(text: string): string {
    let result = '';
    for (let i = 0; i < text.length; i++) result += String.fromCharCode(upperInvariant(text.charCodeAt(i)));
    return result;
}

/** `string.ToLowerInvariant()`: each code unit by its simple mapping. */
export function toLowerInvariant(text: string): string {
    let result = '';
    for (let i = 0; i < text.length; i++) result += String.fromCharCode(lowerInvariant(text.charCodeAt(i)));
    return result;
}

/** `StringComparison.OrdinalIgnoreCase` equality: code unit by code unit, each upper-cased by its simple mapping. */
export function equalsIgnoreCase(a: string, b: string): boolean {
    if (a.length !== b.length) return false;
    for (let i = 0; i < a.length; i++) {
        const x = a.charCodeAt(i);
        const y = b.charCodeAt(i);
        if (x !== y && upperInvariant(x) !== upperInvariant(y)) return false;
    }
    return true;
}

/** `StringComparison.OrdinalIgnoreCase` order: negative, 0 or positive, as `string.Compare` answers. */
export function compareIgnoreCase(a: string, b: string): number {
    const length = Math.min(a.length, b.length);
    for (let i = 0; i < length; i++) {
        const x = upperInvariant(a.charCodeAt(i));
        const y = upperInvariant(b.charCodeAt(i));
        if (x !== y) return x - y;
    }
    return a.length - b.length;
}

/** `string.CompareOrdinal`: code unit by code unit. */
export function compareOrdinal(a: string, b: string): number {
    const length = Math.min(a.length, b.length);
    for (let i = 0; i < length; i++) {
        const x = a.charCodeAt(i);
        const y = b.charCodeAt(i);
        if (x !== y) return x - y;
    }
    return a.length - b.length;
}

/** Whether `text` holds `part` at `at`, compared ordinally: `string.CompareOrdinal(text, at, part, 0, part.length) == 0`. */
export function startsWithAt(text: string, at: number, part: string): boolean {
    return text.startsWith(part, at);
}

/** Whether `text` holds `part` at `at`, compared ignoring case as `OrdinalIgnoreCase` compares. */
export function startsWithAtIgnoreCase(text: string, at: number, part: string): boolean {
    if (at + part.length > text.length) return false;
    return equalsIgnoreCase(text.slice(at, at + part.length), part);
}

/** A key that compares as `StringComparer.OrdinalIgnoreCase` does: the text upper-cased by simple mapping. */
export function ignoreCaseKey(text: string): string {
    return toUpperInvariant(text);
}

// ---- Numbers ---------------------------------------------------------------------------------------------

/** `Math.Round(double)`: to the nearest whole number, a midpoint to the even one. */
export function roundHalfToEven(x: number): number {
    if (!Number.isFinite(x)) return x;
    const floor = Math.floor(x);
    const difference = x - floor;
    const rounded = difference < 0.5 ? floor : difference > 0.5 ? floor + 1 : floor % 2 === 0 ? floor : floor + 1;
    return rounded === 0 && x < 0 ? -0 : rounded;
}

/** `Math.Round(x, MidpointRounding.AwayFromZero)`. */
export function roundHalfAwayFromZero(x: number): number {
    if (!Number.isFinite(x)) return x;
    const magnitude = Math.floor(Math.abs(x) + 0.5);
    // Math.floor(|x| + 0.5) rounds |x| = 0.49999999999999994 up; the sum is inexact there.
    const corrected = magnitude - Math.abs(x) > 0.5 ? magnitude - 1 : magnitude;
    return x < 0 ? -corrected : corrected;
}

const scratch = new DataView(new ArrayBuffer(8));

/** The IEEE bits of a double, as `BitConverter.DoubleToInt64Bits`. */
export function doubleBits(x: number): bigint {
    scratch.setFloat64(0, x);
    return scratch.getBigInt64(0);
}

/** The double of IEEE bits, as `BitConverter.Int64BitsToDouble`. */
export function bitsDouble(bits: bigint): number {
    scratch.setBigInt64(0, BigInt.asIntN(64, bits));
    return scratch.getFloat64(0);
}

/** `Math.BitIncrement`: the next double up. */
export function bitIncrement(x: number): number {
    if (Number.isNaN(x) || x === Infinity) return x;
    if (x === 0) return Number.MIN_VALUE;
    const bits = doubleBits(x);
    return bitsDouble(x > 0 ? bits + 1n : bits - 1n);
}

/** `Math.BitDecrement`: the next double down. */
export function bitDecrement(x: number): number {
    if (Number.isNaN(x) || x === -Infinity) return x;
    if (x === 0) return -Number.MIN_VALUE;
    const bits = doubleBits(x);
    return bitsDouble(x > 0 ? bits - 1n : bits + 1n);
}

/** Whether a double's lowest significand bit is 0 (`(DoubleToInt64Bits(x) & 1) == 0`). */
export function lowestBitClear(x: number): boolean {
    return (doubleBits(x) & 1n) === 0n;
}

/**
 * `Math.ScaleB`: `x` × 2^`n`, rounded once, as musl's `scalbn` computes it, stepping so that a subnormal
 * result is not rounded twice.
 */
export function scaleB(x: number, n: number): number {
    let y = x;
    let e = n;
    if (e > 1023) {
        y *= 2 ** 1023;
        e -= 1023;
        if (e > 1023) {
            y *= 2 ** 1023;
            e -= 1023;
            if (e > 1023) e = 1023;
        }
    } else if (e < -1022) {
        // 2^-1022 * 2^53: the steps stay normal, so only the last multiplication rounds.
        y *= 2 ** -1022 * 2 ** 53;
        e += 1022 - 53;
        if (e < -1022) {
            y *= 2 ** -1022 * 2 ** 53;
            e += 1022 - 53;
            if (e < -1022) e = -1022;
        }
    }
    return y * 2 ** e;
}

/**
 * The exact error of the product `a` × `b` rounded to a double: `Math.FusedMultiplyAdd(a, b, -(a * b))`,
 * the one use the C# engine makes of a fused multiply-add. Dekker's product, exact where neither factor
 * nor the product overflows and the error does not underflow, which holds for every argument the engine
 * passes.
 */
export function productError(a: number, b: number): number {
    const p = a * b;
    const [aHigh, aLow] = split(a);
    const [bHigh, bLow] = split(b);
    return ((aHigh * bHigh - p) + aHigh * bLow + aLow * bHigh) + aLow * bLow;
}

const splitter = 134217729; // 2^27 + 1

function split(a: number): [number, number] {
    const c = splitter * a;
    const high = c - (c - a);
    return [high, a - high];
}

/** `Math.FusedMultiplyAdd(a, b, c)` where `c` is `-(a * b)`'s negation of the rounded product: see `productError`. */
export function fusedProductError(a: number, b: number, minusProduct: number): number {
    if (minusProduct !== -(a * b)) throw new Error('Internal: a fused multiply-add is only ever the error of a product.');
    return productError(a, b);
}

// ---- Formatting ------------------------------------------------------------------------------------------

/** The significant digits and decimal exponent of a finite, non-zero double rounded to `digits` digits, as .NET rounds them: the exact value, a midpoint away from zero. */
function significant(x: number, digits: number): { digits: string; exponent: number } {
    const spelled = Math.abs(x).toExponential(digits - 1);
    const e = spelled.indexOf('e');
    return { digits: spelled.slice(0, e).replace('.', ''), exponent: Number(spelled.slice(e + 1)) };
}

/** The shortest digits that read back as the double, and its decimal exponent. */
function shortest(x: number): { digits: string; exponent: number } {
    const spelled = Math.abs(x).toExponential();
    const e = spelled.indexOf('e');
    return { digits: spelled.slice(0, e).replace('.', ''), exponent: Number(spelled.slice(e + 1)) };
}

/** An exponent as .NET writes it: `E`, its sign always, and at least `minimum` digits. */
function exponentText(exponent: number, minimum: number): string {
    return `E${exponent < 0 ? '-' : '+'}${String(Math.abs(exponent)).padStart(minimum, '0')}`;
}

/**
 * `x.ToString("E" + digits, InvariantCulture)`: one digit, the point, `digits` more, and an exponent of at
 * least three digits, as `1.23450000000000E+003`.
 */
export function formatExponential(x: number, digits: number): string {
    if (!Number.isFinite(x)) throw new RangeError(`${String(x)} has no exponential form.`);
    const sign = x < 0 || Object.is(x, -0) ? '-' : '';
    if (x === 0) return `${sign}0${digits > 0 ? `.${'0'.repeat(digits)}` : ''}E+000`;
    const { digits: d, exponent } = significant(x, digits + 1);
    return `${sign}${d.charAt(0)}${digits > 0 ? `.${d.slice(1)}` : ''}${exponentText(exponent, 3)}`;
}

/** .NET's general form of a number's digits: plain where the exponent is −4 to `precision` − 1, scientific otherwise, trailing zeros dropped. */
function general(negative: boolean, digits: string, exponent: number, precision: number, decimalSeparator: string, negativeSign: string): string {
    const trimmed = digits.replace(/0+$/, '');
    const kept = trimmed === '' ? '0' : trimmed;
    const sign = negative ? negativeSign : '';
    if (exponent >= precision || exponent < -4) {
        const mantissa = kept.length > 1 ? `${kept.charAt(0)}${decimalSeparator}${kept.slice(1)}` : kept;
        return `${sign}${mantissa}${exponentText(exponent, 2)}`;
    }
    if (exponent < 0) return `${sign}0${decimalSeparator}${'0'.repeat(-exponent - 1)}${kept}`;
    if (kept.length <= exponent + 1) return `${sign}${kept}${'0'.repeat(exponent + 1 - kept.length)}`;
    return `${sign}${kept.slice(0, exponent + 1)}${decimalSeparator}${kept.slice(exponent + 1)}`;
}

/** `x.ToString("R", InvariantCulture)`: the shortest text that reads back as the double, `1E+15`, `0.0001`, `1E-05`. */
export function formatRoundTrip(x: number): string {
    if (Number.isNaN(x)) return 'NaN';
    if (x === Infinity) return 'Infinity';
    if (x === -Infinity) return '-Infinity';
    if (x === 0) return Object.is(x, -0) ? '-0' : '0';
    const { digits, exponent } = shortest(x);
    return general(x < 0, digits, exponent, 15, '.', '-');
}

/** `x.ToString("G" + precision, culture)` for a finite double, under the separator and sign given. */
export function formatGeneral(x: number, precision: number, decimalSeparator = '.', negativeSign = '-'): string {
    if (!Number.isFinite(x)) throw new RangeError(`${String(x)} has no general form here.`);
    if (x === 0) return Object.is(x, -0) ? `${negativeSign}0` : '0';
    const { digits, exponent } = significant(x, precision);
    return general(x < 0, digits, exponent, precision, decimalSeparator, negativeSign);
}

/** `n.ToString("X" + width)`: upper-case hexadecimal, padded with zeros to `width`. */
export function hex(n: number, width: number): string {
    return n.toString(16).toUpperCase().padStart(width, '0');
}

/** `n.ToString("0" × width)`: a whole number padded with zeros to `width`. */
export function padded(n: number, width: number): string {
    const sign = n < 0 ? '-' : '';
    return sign + String(Math.abs(n)).padStart(width, '0');
}

/**
 * `double.Parse(text, NumberStyles.Float, InvariantCulture)` for the texts the engine writes itself: an
 * optional sign, digits with an optional point, an optional exponent. Rounded correctly, as .NET Core
 * rounds it. Anything else throws, as a text the engine wrote that does not read back is a defect.
 */
export function parseInvariantDouble(text: string): number {
    const value = tryParseInvariantDouble(text);
    if (value === undefined) throw new Error(`Internal: '${text}' does not read as a number.`);
    return value;
}

const floatPattern = /^[\t-\r ]*[+-]?(?:\d+\.?\d*|\.\d+)(?:[eE][+-]?\d+)?[\t-\r ]*$/;

/**
 * `double.TryParse(text, NumberStyles.Float, InvariantCulture, …)`: white space at either end, a sign, digits
 * with a point, an exponent; `undefined` where it does not read. .NET reads a value past a double's range as
 * an infinity, as `Number` does.
 */
export function tryParseInvariantDouble(text: string): number | undefined {
    if (floatPattern.test(text)) return Number(text.trim());
    // .NET also reads the invariant culture's symbols, in any case: Infinity, -Infinity and NaN.
    const symbol = /^[\t-\r ]*([+-]?)(infinity|nan)[\t-\r ]*$/i.exec(text);
    if (symbol === null) return undefined;
    if (symbol[2]?.toLowerCase() === 'nan') return Number.NaN;
    return symbol[1] === '-' ? -Infinity : Infinity;
}
