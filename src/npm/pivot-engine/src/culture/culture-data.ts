// A culture's number and date conventions, as .NET's NumberFormatInfo and DateTimeFormatInfo hold them
// (ADR-0370, section 10). The engine formats labels and values from this table, never from `Intl`, so a
// figure does not change with the runtime's ICU: the table is .NET's on Linux, which a .NET server pivots
// in, and the shared cases hold the two engines to it.

import { cultureTable } from './culture-table';

/** NumberFormatInfo's members the formats read. Patterns are .NET's numbers for them. */
export interface NumberCulture {
    readonly decimal: string;
    readonly group: string;
    readonly groupSizes: readonly number[];
    readonly digits: number;
    readonly negativePattern: number;
    readonly negativeSign: string;
    readonly positiveSign: string;
    readonly currencyDecimal: string;
    readonly currencyGroup: string;
    readonly currencyGroupSizes: readonly number[];
    readonly currencyDigits: number;
    readonly currencySymbol: string;
    readonly currencyPositivePattern: number;
    readonly currencyNegativePattern: number;
    readonly percentDecimal: string;
    readonly percentGroup: string;
    readonly percentGroupSizes: readonly number[];
    readonly percentDigits: number;
    readonly percentSymbol: string;
    readonly perMilleSymbol: string;
    readonly percentPositivePattern: number;
    readonly percentNegativePattern: number;
    readonly nan: string;
    readonly positiveInfinity: string;
    readonly negativeInfinity: string;
}

/** DateTimeFormatInfo's members the formats read, for the Gregorian calendar. */
export interface DateCulture {
    readonly shortDate: string;
    readonly longDate: string;
    readonly shortTime: string;
    readonly longTime: string;
    readonly fullDateTime: string;
    readonly monthDay: string;
    readonly yearMonth: string;
    readonly am: string;
    readonly pm: string;
    readonly dateSeparator: string;
    readonly timeSeparator: string;
    readonly era: string;
    /** Whether a month's genitive names differ from its own, so `MMMM` beside a day takes them. */
    readonly useGenitive: boolean;
    readonly days: readonly string[];
    readonly abbreviatedDays: readonly string[];
    readonly months: readonly string[];
    readonly abbreviatedMonths: readonly string[];
    readonly genitiveMonths: readonly string[];
    readonly abbreviatedGenitiveMonths: readonly string[];
}

/** One culture's conventions. */
export interface CultureData {
    readonly number: NumberCulture;
    readonly date: DateCulture;
}

/** A culture the engine formats in: its .NET name, `invariant` for the InvariantCulture, and its
 * conventions. */
export interface Culture {
    readonly name: string;
    readonly data: CultureData;
}

const byLowerName = new Map<string, string>();

/** The culture of a locale, a BCP 47 tag or `invariant`, matched as .NET matches a culture name, ignoring
 * case. A locale the table does not hold is refused with a `RangeError` naming it: formatting in some other
 * culture's conventions would show figures the reader cannot trust. */
export function cultureOf(locale: string): Culture {
    if (byLowerName.size === 0) {
        for (const name of Object.keys(cultureTable)) byLowerName.set(name.toLowerCase(), name);
    }
    const name = byLowerName.get(locale.toLowerCase());
    const data = name === undefined ? undefined : cultureTable[name];
    if (name === undefined || data === undefined) {
        throw new RangeError(
            `The locale '${locale}' is not one the pivot engine formats in; it holds ${[...byLowerName.values()].join(', ')}.`);
    }
    return Object.freeze({ name, data });
}

/** The locales the engine formats in, by their .NET names. */
export function pivotLocales(): readonly string[] {
    return Object.freeze(Object.keys(cultureTable));
}
