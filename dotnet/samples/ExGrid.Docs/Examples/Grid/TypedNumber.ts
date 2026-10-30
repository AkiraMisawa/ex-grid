import { exactDecimalDigits } from '@ex-grid/core';

/** A number as a desk types it, as C#'s `NumberStyles.Number` reads it under the invariant culture: digits with a
 * sign before or after them, commas between digits and a point. Anything else answers `undefined`, never a number
 * made up for it: an empty text is not zero, `0x96` is not 150, and more significant digits than a number keeps
 * exactly are not rounded away. */
export function typedNumber(text: string): number | undefined {
    const parts = /^([+-]?)(\d+(?:,\d+)*)?(\.\d*)?([+-]?)$/u.exec(text.trim());
    if (parts === null) return undefined;
    const [, leading = '', whole = '', fraction = '', trailing = ''] = parts;
    if (leading !== '' && trailing !== '') return undefined;
    const digits = whole.replaceAll(',', '') + fraction.slice(1);
    if (digits === '' || digits.replace(/^0+/u, '').replace(/0+$/u, '').length > exactDecimalDigits) return undefined;
    const value = Number(`${leading}${trailing}${whole.replaceAll(',', '')}${fraction}`);
    // -0 is 0: C#'s decimal shows no sign on a zero.
    return value === 0 ? 0 : value;
}

/** A whole number as a desk types it, or `undefined` for anything else, a whole number past what a number holds
 * exactly included. */
export function typedWholeNumber(text: string): number | undefined {
    const value = typedNumber(text);
    return value !== undefined && Number.isSafeInteger(value) ? value : undefined;
}
