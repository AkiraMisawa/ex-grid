import { foldCase } from './text-casing';

// Numbers read and written as ExGrid.Data.Xlsx reads and writes them with the invariant culture, so a file,
// a refusal and a written part read alike in both packages (ADR-0370, section 2). Each function names the
// .NET parse or format it stands for: JavaScript's own `Number()` and `String(n)` take other text and
// write other text.

/** C#'s `int.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture)`: ASCII digits after an
 * optional sign, no space, within a 32-bit integer; `undefined` otherwise. */
export function parseInt32(text: string): number | undefined {
    if (!/^[+-]?[0-9]+$/.test(text)) return undefined;
    const value = Number(text);
    return value >= -2147483648 && value <= 2147483647 ? value : undefined;
}

/** C#'s `int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture)`: ASCII digits alone, within a 32-bit
 * integer; `undefined` otherwise. */
export function parseDigits(text: string): number | undefined {
    if (!/^[0-9]+$/.test(text)) return undefined;
    const value = Number(text);
    return value <= 2147483647 ? value : undefined;
}

/** C#'s `uint.TryParse(text, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture)`: hexadecimal digits
 * alone, no `0x` and no space, within 32 bits; `undefined` otherwise. */
export function parseHex32(text: string): number | undefined {
    if (!/^[0-9A-Fa-f]+$/.test(text)) return undefined;
    const value = Number.parseInt(text, 16);
    return value <= 0xFFFFFFFF ? value : undefined;
}

/**
 * C#'s `double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture)`: an optional sign, digits with
 * an optional point, an optional exponent, and white space (U+0009 to U+000D and U+0020) on either side; or the
 * invariant culture's `Infinity` and `NaN`, in any case, with a sign. A number past the doubles' range is
 * infinite, as .NET reads it. `undefined` for anything else.
 */
export function parseDouble(text: string): number | undefined {
    const trimmed = text.replace(/^[\t\n\v\f\r ]+|[\t\n\v\f\r ]+$/g, '');
    if (/^[+-]?(?:[0-9]+\.?[0-9]*|\.[0-9]+)(?:[eE][+-]?[0-9]+)?$/.test(trimmed)) return Number(trimmed);
    const lower = trimmed.toLowerCase();
    if (lower === 'infinity' || lower === '+infinity') return Number.POSITIVE_INFINITY;
    if (lower === '-infinity') return Number.NEGATIVE_INFINITY;
    if (lower === 'nan' || lower === '+nan' || lower === '-nan') return Number.NaN;
    return undefined;
}

/** C#'s `ToString("N0", CultureInfo.InvariantCulture)` of a whole number: its digits grouped by threes with commas. */
export function grouped(value: number): string {
    const digits = String(Math.abs(Math.trunc(value)));
    const groups: string[] = [];
    for (let end = digits.length; end > 0; end -= 3) groups.unshift(digits.slice(Math.max(0, end - 3), end));
    return (value < 0 ? '-' : '') + groups.join(',');
}

/** C#'s `ToString("X8")` of a 32-bit value: eight upper-case hexadecimal digits. */
export function hex8(value: number): string {
    return (value >>> 0).toString(16).toUpperCase().padStart(8, '0');
}

/**
 * C#'s `ToString("R", CultureInfo.InvariantCulture)` of a double: the shortest digits that read back as the same
 * number, in exponent form (`1E-05`, `1.2345678901234568E+17`) where the point stands more than seventeen places
 * right of the first digit or more than three places left of it; `NaN`, `Infinity` and `-Infinity` by the
 * invariant culture's names.
 */
export function roundTrip(value: number): string {
    if (Number.isNaN(value)) return 'NaN';
    if (!Number.isFinite(value)) return value > 0 ? 'Infinity' : '-Infinity';
    if (value === 0) return Object.is(value, -0) ? '-0' : '0';
    const match = /^(\d)(?:\.(\d+))?e([+-]\d+)$/.exec(Math.abs(value).toExponential());
    if (match === null) throw new Error(`Internal: ${String(value)} is not written in exponent form.`);
    const [, first = '', rest = '', exponent = ''] = match;
    const digits = first + rest;
    const point = Number(exponent) + 1;
    const sign = value < 0 ? '-' : '';
    if (point > 17 || point < -3) {
        const e = point - 1;
        const mantissa = digits.length > 1 ? `${digits.slice(0, 1)}.${digits.slice(1)}` : digits;
        return `${sign}${mantissa}E${e < 0 ? '-' : '+'}${String(Math.abs(e)).padStart(2, '0')}`;
    }
    if (point <= 0) return `${sign}0.${'0'.repeat(-point)}${digits}`;
    if (point >= digits.length) return sign + digits + '0'.repeat(point - digits.length);
    return `${sign}${digits.slice(0, point)}.${digits.slice(point)}`;
}

/** C#'s `string.Equals(a, b, StringComparison.OrdinalIgnoreCase)`. */
export function equalsIgnoreCase(a: string, b: string): boolean {
    return a.length === b.length && foldCase(a) === foldCase(b);
}
