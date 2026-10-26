// A value of a record behind a cell as Show Details writes it (ADR-0059), as the Razor component's
// PivotDetailsSheet.TextOf writes it: text as it is; a number or a date in its field's format under the report's
// culture, or at most 15 significant digits, or the culture's short date (with its long time where the date has a
// time of day), as an Item is labelled (ADR-0060); a Boolean as TRUE or FALSE; and a double that is not finite as
// #NUM!. A format .NET cannot write the value in writes it in General, as `value.ToString(null, culture)` does.

import { cultureOf } from '../culture/culture-data';
import { PivotFormatError } from '../errors';
import type { PivotDetailValue } from '../source/pivot-source';
import { formatDate } from './date-text';
import { formatDecimal, formatDouble } from './number-text';

const ticksPerDay = 864_000_000_000n;

// Written in `format`, or in General when .NET cannot write the value in it.
function formatted(write: (format: string) => string, format: string): string {
    try {
        return write(format);
    } catch (error) { // oxlint-disable-line ex-grid/no-catch -- a format .NET refuses for a value writes it in General, as the Razor component's TextOf does; anything else is thrown on
        if (error instanceof PivotFormatError) return write('');
        throw error;
    }
}

/**
 * A value of a record behind a cell, `value`, written as Show Details shows it in its field's column: in `format`,
 * the field's own format, where it is a number or a date, under `locale`, a BCP 47 tag or `invariant`. A Blank is
 * written as nothing.
 */
export function formatPivotDetailValue(value: PivotDetailValue, format: string | undefined, locale: string): string {
    if (value === null) return '';
    if (typeof value === 'string') return value;
    if (typeof value === 'boolean') return value ? 'TRUE' : 'FALSE';
    const { data } = cultureOf(locale);
    if (typeof value === 'bigint') {
        const fallback = value % ticksPerDay === 0n ? 'd' : 'G';
        // A field with no format of its own writes its dates as the culture's short date, or date and time, as the
        // Razor component's does.
        return formatted(f => formatDate(value, f, data.date), format ?? fallback);
    }
    if (typeof value === 'object') return formatted(f => formatDecimal(value, f, data.number), format ?? 'G15'); // oxlint-disable-line ex-grid/no-stand-in-default -- ADR-0060: a number with no format of its own is written to 15 significant digits
    if (!Number.isFinite(value)) return '#NUM!';
    return formatted(f => formatDouble(value, f, data.number), format ?? 'G15'); // oxlint-disable-line ex-grid/no-stand-in-default -- ADR-0060: a number with no format of its own is written to 15 significant digits
}
