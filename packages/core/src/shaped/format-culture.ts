// The culture a FormatCode writes its text in (ADR-0342, ADR-0370 section 10). C# hands FormatCode.Format a
// CultureInfo; the Script Core is told a locale, a BCP 47 tag or `invariant`. The invariant culture's
// conventions come from this module's own table, as .NET's InvariantCulture holds them, never from `Intl`,
// which has no culture of that name. Another locale's separators come from `Intl.NumberFormat`'s parts, as
// the core reads a typed operand's (`chrome/grid-culture.ts`). The names a date code shows (months, days,
// AM and PM) come from the table below, as .NET holds them on Linux (ICU's data, the platform the shared
// cases run C# on, and the one @ex-grid/pivot-engine's table was generated on): `Intl` writes a name in a
// sentence's form, not as a list of names (ja-JP's month comes apart from its 月), so it is not read for them.
// A locale the table does not hold formats numbers and numeric dates, and refuses a code that names a month
// or a day, or AM and PM, rather than write a name some other way.

import { gridCulture, invariantLocale } from '../chrome/grid-culture';
import { defined } from '../must';
import type { NumberCulture } from './custom-number-format';

/** The names a .NET custom date format writes, as its DateTimeFormatInfo holds them (Gregorian calendar). */
export interface DateNames {
    readonly am: string;
    readonly pm: string;
    readonly days: readonly string[];
    readonly abbreviatedDays: readonly string[];
    readonly months: readonly string[];
    readonly abbreviatedMonths: readonly string[];
    readonly genitiveMonths: readonly string[];
    readonly abbreviatedGenitiveMonths: readonly string[];
}

/** What a FormatCode reads of a locale. */
export interface FormatCulture {
    /** The locale as given. */
    readonly name: string;
    /** Its number conventions. */
    readonly number: NumberCulture;
    /** The texts of a double that is not finite: NaN, ∞ and −∞. */
    readonly nan: string;
    readonly positiveInfinity: string;
    readonly negativeInfinity: string;
    /** Its names, or `undefined` where the table holds none for the locale. */
    readonly names: DateNames | undefined;
    /** The calendar a date is written in; only `gregory` writes the year a Date holds. */
    readonly calendar: string;
}

const englishDays = Object.freeze(['Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday']);
const englishAbbreviatedDays = Object.freeze(['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat']);
const englishMonths = Object.freeze(['January', 'February', 'March', 'April', 'May', 'June', 'July', 'August', 'September',
    'October', 'November', 'December']);
const englishAbbreviatedMonths = Object.freeze(['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec']);
const britishAbbreviatedMonths = Object.freeze(['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sept', 'Oct', 'Nov', 'Dec']);
const japaneseMonths = Object.freeze(['1月', '2月', '3月', '4月', '5月', '6月', '7月', '8月', '9月', '10月', '11月', '12月']);

// The names .NET holds on Linux for each culture, by its canonical tag (dotnet/tools/pivot-oracle's culture table).
const namesTable: Readonly<Record<string, DateNames>> = Object.freeze({
    [invariantLocale]: Object.freeze({
        am: 'AM', pm: 'PM', days: englishDays, abbreviatedDays: englishAbbreviatedDays, months: englishMonths,
        abbreviatedMonths: englishAbbreviatedMonths, genitiveMonths: englishMonths, abbreviatedGenitiveMonths: englishAbbreviatedMonths,
    }),
    'en-US': Object.freeze({
        am: 'AM', pm: 'PM', days: englishDays, abbreviatedDays: englishAbbreviatedDays, months: englishMonths,
        abbreviatedMonths: englishAbbreviatedMonths, genitiveMonths: englishMonths, abbreviatedGenitiveMonths: englishAbbreviatedMonths,
    }),
    'en-GB': Object.freeze({
        am: 'am', pm: 'pm', days: englishDays, abbreviatedDays: englishAbbreviatedDays, months: englishMonths,
        abbreviatedMonths: britishAbbreviatedMonths, genitiveMonths: englishMonths, abbreviatedGenitiveMonths: britishAbbreviatedMonths,
    }),
    'de-DE': Object.freeze({
        am: 'AM', pm: 'PM',
        days: Object.freeze(['Sonntag', 'Montag', 'Dienstag', 'Mittwoch', 'Donnerstag', 'Freitag', 'Samstag']),
        abbreviatedDays: Object.freeze(['So', 'Mo', 'Di', 'Mi', 'Do', 'Fr', 'Sa']),
        months: Object.freeze(['Januar', 'Februar', 'März', 'April', 'Mai', 'Juni', 'Juli', 'August', 'September', 'Oktober',
            'November', 'Dezember']),
        abbreviatedMonths: Object.freeze(['Jan', 'Feb', 'Mär', 'Apr', 'Mai', 'Jun', 'Jul', 'Aug', 'Sep', 'Okt', 'Nov', 'Dez']),
        genitiveMonths: Object.freeze(['Januar', 'Februar', 'März', 'April', 'Mai', 'Juni', 'Juli', 'August', 'September', 'Oktober',
            'November', 'Dezember']),
        abbreviatedGenitiveMonths: Object.freeze(['Jan.', 'Feb.', 'März', 'Apr.', 'Mai', 'Juni', 'Juli', 'Aug.', 'Sept.', 'Okt.',
            'Nov.', 'Dez.']),
    }),
    'ja-JP': Object.freeze({
        am: '午前', pm: '午後',
        days: Object.freeze(['日曜日', '月曜日', '火曜日', '水曜日', '木曜日', '金曜日', '土曜日']),
        abbreviatedDays: Object.freeze(['日', '月', '火', '水', '木', '金', '土']),
        months: japaneseMonths, abbreviatedMonths: japaneseMonths, genitiveMonths: japaneseMonths, abbreviatedGenitiveMonths: japaneseMonths,
    }),
});

const invariant: FormatCulture = Object.freeze({
    name: invariantLocale,
    number: Object.freeze({
        decimal: '.', group: ',', groupSizes: Object.freeze([3]), negativeSign: '-', positiveSign: '+', percentSymbol: '%', perMilleSymbol: '‰',
    }),
    nan: 'NaN',
    positiveInfinity: 'Infinity',
    negativeInfinity: '-Infinity',
    names: namesTable[invariantLocale],
    calendar: 'gregory',
});

const made = new Map<string, FormatCulture>();

/** The culture of `locale`, a BCP 47 tag or `invariant`; a tag `Intl` does not take is refused with a
 * `RangeError` naming it, as a grid's locale is. */
export function formatCulture(locale: string): FormatCulture {
    if (locale.toLowerCase() === invariantLocale) return invariant;
    const known = made.get(locale);
    if (known !== undefined) return known;
    const separators = gridCulture(locale);
    const tag = defined(Intl.getCanonicalLocales(locale)[0], `${locale}'s canonical tag`);
    const numbers = new Intl.NumberFormat(tag, { useGrouping: 'always', maximumFractionDigits: 0, numberingSystem: 'latn' });
    // The sizes of the groups, read from the right of a long integer: the first, then the one that repeats.
    const integers = numbers.formatToParts(123456789012345678n).filter(part => part.type === 'integer').map(part => part.value.length);
    const fromRight = integers.toReversed();
    const [first, second] = fromRight;
    const groupSizes = first === undefined || fromRight.length === 1 ? [0]
        : second === undefined || second === first ? [first] : [first, second];
    const part = (format: Intl.NumberFormat, value: number, type: Intl.NumberFormatPartTypes): string =>
        defined(format.formatToParts(value).find(found => found.type === type), `${tag}'s ${type} in Intl's parts`).value;
    const percent = new Intl.NumberFormat(tag, { style: 'percent', numberingSystem: 'latn' });
    const infinity = part(numbers, Infinity, 'infinity');
    const culture: FormatCulture = Object.freeze({
        name: locale,
        number: Object.freeze({
            decimal: separators.decimal,
            group: separators.group,
            groupSizes: Object.freeze(groupSizes),
            negativeSign: separators.negativeSign,
            positiveSign: separators.positiveSign,
            percentSymbol: part(percent, 0.5, 'percentSign'),
            perMilleSymbol: '‰',
        }),
        nan: part(numbers, NaN, 'nan'),
        positiveInfinity: infinity,
        negativeInfinity: separators.negativeSign + infinity,
        names: namesTable[tag],
        calendar: new Intl.DateTimeFormat(tag).resolvedOptions().calendar,
    });
    made.set(locale, culture);
    return culture;
}
