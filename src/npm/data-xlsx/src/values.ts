// An attribute's value read as ExGrid.Data.Xlsx's reader reads it, a value that is not one refused as malformed,
// naming where it was met.

import { parseDouble, parseInt32 } from './invariant';
import { XlsxError, type XlsxPlace } from './xlsx-error';

/** An `xsd:boolean` that is true: `1` or `true`. Anything else, an absent attribute included, is false. */
export function bool(value: string | undefined): boolean {
    return value === '1' || value === 'true';
}

/** A whole number, as `int.TryParse` with a leading sign reads it. */
export function int(value: string, where: XlsxPlace): number {
    const n = parseInt32(value);
    if (n === undefined) throw new XlsxError('malformed', `'${value}' is not a whole number where the part needs one.`, where);
    return n;
}

/** A number, as `double.TryParse` with `NumberStyles.Float` reads it. */
export function double(value: string, part: string): number {
    const n = parseDouble(value);
    if (n === undefined) throw new XlsxError('malformed', `'${value}' is not a number where the part needs one.`, { part });
    return n;
}
