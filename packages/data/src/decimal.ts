// The Snapshot's Decimal (ADR-0064): C#'s decimal, a whole number of at most 96 bits and 0 to 28 decimal
// places, held as its value and not as the places it was written with, so 1.5 and 1.50 are one value. The
// shape is the Script Core's ExactDecimal's, so a value passes between the two packages as it is.

/** A Decimal value, `mantissa` × 10^-`scale`: a whole number below 2^96 in magnitude, and 0 to 28 places.
 * A value a Snapshot hands out is canonical: no trailing zero after the point, and 0 is `0n` at scale 0. */
export interface SnapshotDecimal {
    /** The digits, as a whole number. */
    readonly mantissa: bigint;
    /** How many of them stand after the point, 0 to 28. */
    readonly scale: number;
}

/** The magnitude a Decimal's digits stay below: 2^96. */
export const decimalLimit = 1n << 96n;

/** The most places a Decimal holds. */
export const maxDecimalScale = 28;

/** The canonical Decimal ± `magnitude` × 10^-`scale` is, its trailing zeros taken off, frozen. The caller
 * has checked it is a Decimal. */
export function canonicalDecimal(mantissa: bigint, scale: number): SnapshotDecimal {
    let digits = mantissa;
    let places = scale;
    while (places > 0 && digits % 10n === 0n) {
        digits /= 10n;
        places -= 1;
    }
    return Object.freeze({ mantissa: digits, scale: digits === 0n ? 0 : places });
}

/** Whether `value` is a Decimal a Snapshot holds: a bigint below 2^96 in magnitude and a whole scale from
 * 0 to 28. A value of any other shape, which only an untyped caller can pass, is not one. */
export function isSnapshotDecimal(value: unknown): value is SnapshotDecimal {
    if (typeof value !== 'object' || value === null) return false;
    const mantissa: unknown = Reflect.get(value, 'mantissa');
    const scale: unknown = Reflect.get(value, 'scale');
    return typeof mantissa === 'bigint' && mantissa < decimalLimit && mantissa > -decimalLimit
        && typeof scale === 'number' && Number.isInteger(scale) && scale >= 0 && scale <= maxDecimalScale;
}

/**
 * The Decimal a text writes: an optional `-`, digits, and optionally a point and more digits, as
 * `"-1234.5678"`. Refused with a `TypeError` when the text is not one, and with a `RangeError` when no
 * Decimal holds it exactly: more than 28 places that are not trailing zeros, or digits past 2^96.
 */
export function decimalFromText(text: string): SnapshotDecimal {
    const match = /^(-?)(\d+)(?:\.(\d+))?$/.exec(text);
    if (match === null) throw new TypeError(`'${text}' is not a decimal: a decimal is written as -1234.5678.`);
    // The point and the places after it are optional: a whole number has none.
    const [, sign = '', whole = '', fraction = ''] = match;
    const places = fraction.replace(/0+$/, '');
    if (places.length > maxDecimalScale) throw new RangeError(`'${text}' has more decimal places than the 28 a decimal holds.`);
    const magnitude = BigInt(whole + places);
    if (magnitude >= decimalLimit) throw new RangeError(`'${text}' lies beyond the range of a decimal.`);
    return canonicalDecimal(sign === '-' ? -magnitude : magnitude, places.length);
}

/** A Decimal's text, canonical: `-0.0001`, `1.5`, `79228162514264337593543950335`. */
export function decimalText(value: SnapshotDecimal): string {
    return unscaledText(value.mantissa.toString(), value.scale);
}

/** An unscaled whole number's digits with the point put in at `places`, which may be negative, as a
 * refusal shows a value read at its Arrow scale (`0.000…015`, `999…000`). */
export function unscaledText(unscaled: string, places: number): string {
    const negative = unscaled.startsWith('-');
    let digits = negative ? unscaled.slice(1) : unscaled;
    if (places > 0) {
        digits = digits.padStart(places + 1, '0');
        digits = `${digits.slice(0, -places)}.${digits.slice(-places)}`;
    } else if (places < 0) {
        digits += '0'.repeat(-places);
    }
    return negative ? `-${digits}` : digits;
}
