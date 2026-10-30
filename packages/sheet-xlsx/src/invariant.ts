// Numbers written as ExSheet.Xlsx writes them with the invariant culture, so that a written part and a refusal's
// words read alike in both packages (ADR-0370, section 2). Each function names the .NET format it stands for.

/** C#'s `ToString("N0", CultureInfo.InvariantCulture)` of a whole number: its digits grouped by threes with commas. */
export function grouped(value: number): string {
    const digits = String(Math.abs(Math.trunc(value)));
    const groups: string[] = [];
    for (let end = digits.length; end > 0; end -= 3) groups.unshift(digits.slice(Math.max(0, end - 3), end));
    return (value < 0 ? '-' : '') + groups.join(',');
}

/**
 * C#'s `ToString("R", CultureInfo.InvariantCulture)` of a double: the shortest digits that read back as the same
 * number, in exponent form (`1E-05`, `1.2345678901234568E+17`) where the point stands more than seventeen places
 * right of the first digit or more than three places left of it.
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

/**
 * C#'s `double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture)`: an optional sign, digits with an optional
 * point, an optional exponent, and white space (U+0009 to U+000D and U+0020) on either side; or the invariant culture's
 * `Infinity` and `NaN`, in any case, with a sign. `undefined` for anything else.
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
