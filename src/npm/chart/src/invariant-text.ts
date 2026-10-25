// Numbers written as the C# engine writes them with the invariant culture, so a label, a Note or a
// refusal reads alike in both cores (ADR-0370, section 2). Each function names the .NET format it stands
// for; JavaScript's own `String(n)` and `toFixed` round and switch to exponents elsewhere.

/** A decimal digit string and the place of its point: `digits` × 10^(`point` − digits.length). */
interface Digits {
    readonly negative: boolean;
    readonly digits: string;
    readonly point: number;
}

// The digits of a finite number as `toExponential` or `toPrecision` writes them in exponent form.
function digitsOf(value: number, written: string): Digits {
    const match = /^-?(\d)(?:\.(\d+))?e([+-]\d+)$/.exec(written);
    if (match === null) {
        throw new Error(`Internal: ${written} is not a number in exponent form.`);
    }
    const [, first, rest, exponent] = match;
    if (first === undefined || exponent === undefined) {
        throw new Error(`Internal: ${written} has no leading digit or no exponent.`);
    }
    return {
        negative: value < 0 || Object.is(value, -0),
        // A number of one significant digit has no fraction digits.
        digits: rest === undefined ? first : first + rest,
        point: Number(exponent) + 1,
    };
}

function finite(value: number, format: string): void {
    if (!Number.isFinite(value)) {
        throw new RangeError(`${String(value)} is not a finite number, which the format ${format} writes.`);
    }
}

/**
 * C#'s `ToString("R")`, and a double written into a string as C# writes it: the shortest digits that
 * read back as the same number, in exponent form (`1E-05`, `1.2345678901234568E+17`) where the point
 * stands more than seventeen places left or three places right of the digits. A number that is no
 * number is written by the invariant culture's names, `NaN`, `Infinity` and `-Infinity`, as a position
 * the definition refuses is named in its sentence.
 */
export function roundTrip(value: number): string {
    if (Number.isNaN(value)) {
        return 'NaN';
    }
    if (!Number.isFinite(value)) {
        return value > 0 ? 'Infinity' : '-Infinity';
    }
    if (value === 0) {
        return Object.is(value, -0) ? '-0' : '0';
    }
    const d = digitsOf(value, Math.abs(value).toExponential());
    const sign = d.negative ? '-' : '';
    if (d.point > 17 || d.point < -3) {
        const exponent = d.point - 1;
        const mantissa = d.digits.length > 1 ? `${d.digits.slice(0, 1)}.${d.digits.slice(1)}` : d.digits;
        return `${sign}${mantissa}E${exponent < 0 ? '-' : '+'}${String(Math.abs(exponent)).padStart(2, '0')}`;
    }
    return sign + placed(d.digits, d.point);
}

// Digits written with their point, zeros added on either side as the point needs.
function placed(digits: string, point: number): string {
    if (point <= 0) {
        return `0.${'0'.repeat(-point)}${digits}`;
    }
    if (point >= digits.length) {
        return digits + '0'.repeat(point - digits.length);
    }
    return `${digits.slice(0, point)}.${digits.slice(point)}`;
}

/**
 * C#'s custom format `0.###…` with `places` hashes (`0.####`, `0.##`): the number taken to fifteen
 * significant digits, as .NET takes a double for a custom format, then rounded to `places` decimals with
 * a half going away from zero, its trailing zeros dropped. A negative number that rounds to nothing is
 * written `-0`, as .NET writes it.
 */
export function upToPlaces(value: number, places: number): string {
    finite(value, `0.${'#'.repeat(places)}`);
    const negative = value < 0 || Object.is(value, -0);
    if (value === 0) {
        return negative ? '-0' : '0';
    }
    const d = fifteenDigits(value);
    // Keep the digits up to `places` decimals; the next one rounds the kept ones half away from zero.
    const keep = d.point + places;
    let digits = d.digits;
    let point = d.point;
    if (keep < digits.length) {
        const up = keep >= 0 && Number(digits.charAt(keep)) >= 5;
        digits = keep > 0 ? digits.slice(0, keep) : '';
        if (up) {
            const carried = carry(digits);
            if (carried.length > digits.length) {
                point += 1;
            }
            digits = carried;
        }
    }
    digits = digits.replace(/0+$/, '');
    const text = digits === '' ? '0' : placed(digits, point);
    return (negative ? '-' : '') + text;
}

// The number to fifteen significant digits as .NET takes it for a custom format: a tie goes to the even
// digit, where `toExponential(14)` would send it up. A hundred digits hold the expansion as far as a tie
// can be told.
function fifteenDigits(value: number): Digits {
    // oxlint-disable-next-line oxc/number-arg-out-of-range -- ES2018 raised the limit to 100 digits, which an exact tie needs
    const exact = digitsOf(value, Math.abs(value).toExponential(99));
    const kept = exact.digits.slice(0, 15);
    const next = Number(exact.digits.charAt(15));
    const tie = next === 5 && /^0*$/.test(exact.digits.slice(16));
    const up = next > 5 || (next === 5 && !tie) || (tie && Number(kept.charAt(14)) % 2 === 1);
    if (!up) {
        return { ...exact, digits: kept };
    }
    const carried = carry(kept);
    return carried.length > kept.length
        ? { ...exact, digits: carried.slice(0, 15), point: exact.point + 1 }
        : { ...exact, digits: carried };
}

// One added to the last digit, carried left.
function carry(digits: string): string {
    const out = digits.split('');
    for (let i = out.length - 1; i >= 0; i--) {
        if (out[i] === '9') {
            out[i] = '0';
            continue;
        }
        out[i] = String(Number(out[i]) + 1);
        return out.join('');
    }
    return `1${out.join('')}`;
}

/**
 * C#'s `ToString("G3")`: three significant digits, trailing zeros dropped, in exponent form (`1.23E-05`)
 * where the point stands more than three places right or left of the first digit.
 */
export function threeSignificant(value: number): string {
    finite(value, 'G3');
    if (value === 0) {
        return Object.is(value, -0) ? '-0' : '0';
    }
    const d = digitsOf(value, Math.abs(value).toExponential(2));
    const sign = d.negative ? '-' : '';
    const digits = d.digits.replace(/0+$/, '');
    if (d.point > 3 || d.point < -3) {
        const exponent = d.point - 1;
        const mantissa = digits.length > 1 ? `${digits.slice(0, 1)}.${digits.slice(1)}` : digits;
        return `${sign}${mantissa}E${exponent < 0 ? '-' : '+'}${String(Math.abs(exponent)).padStart(2, '0')}`;
    }
    return sign + placed(digits, d.point);
}

/** C#'s `ToString("N0")` of a whole number: its digits in groups of three, split by commas. */
export function grouped(count: number): string {
    if (!Number.isSafeInteger(count)) {
        throw new RangeError(`${String(count)} is not a whole number, which N0 is written for here.`);
    }
    const digits = String(Math.abs(count));
    const groups: string[] = [];
    for (let end = digits.length; end > 0; end -= 3) {
        groups.unshift(digits.slice(Math.max(0, end - 3), end));
    }
    return (count < 0 ? '-' : '') + groups.join(',');
}

/** A number written as a surface writes a tick it has no format for (C#'s `SurfaceMesh.Plain`): `G3` near zero, `0.####` elsewhere. */
export function tickText(value: number): string {
    return value !== 0 && Math.abs(value) < 1e-4 ? threeSignificant(value) : upToPlaces(value, 4);
}
