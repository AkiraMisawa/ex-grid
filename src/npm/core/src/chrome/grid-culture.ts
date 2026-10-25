// The culture a grid reads typed text in (ADR-0370 section 10): a filter's operand, and in time a pasted
// cell's displayed text. C# reads `CultureInfo`; the Script Core is told its locale once, as an option, and
// never reads the browser's on its own, so one instance never reads two cultures. A culture's separators come
// from `Intl.NumberFormat`'s parts for the locale, and the invariant culture's from this module's own table,
// as .NET's InvariantCulture holds them, since `Intl` has no culture of that name.

import { defined } from '../must';

/** What a grid reads of a culture: the counterpart of the members of C#'s `NumberFormatInfo` and
 * `DateTimeFormatInfo` that a typed operand is read and written by. */
export interface GridCulture {
    /** The locale as given: a BCP 47 tag, or `invariant`. */
    readonly name: string;
    /** The culture's name in English, as a refusal names it: `German (Germany)`, as C#'s DisplayName. */
    readonly displayName: string;
    /** The decimal separator. */
    readonly decimal: string;
    /** The group separator. */
    readonly group: string;
    /** The negative sign. */
    readonly negativeSign: string;
    /** The positive sign. */
    readonly positiveSign: string;
    /** Whether a numeric date is written with its year first (ja-JP's `yyyy/MM/dd`). */
    readonly yearFirst: boolean;
    /** Whether a numeric date is written with its day before its month (en-GB's `dd/MM/yyyy`). */
    readonly dayFirst: boolean;
    /** The integer's text as the culture groups it, as C#'s `N0` writes it. */
    readonly grouped: (integer: bigint) => string;
}

/** The locale that reads as .NET's InvariantCulture. */
export const invariantLocale = 'invariant';

const invariant: GridCulture = Object.freeze({
    name: invariantLocale,
    displayName: 'Invariant Language (Invariant Country)',
    decimal: '.',
    group: ',',
    negativeSign: '-',
    positiveSign: '+',
    // MM/dd/yyyy.
    yearFirst: false,
    dayFirst: false,
    grouped: (integer: bigint) => groupedInThrees(integer.toString(), ','),
});

const made = new Map<string, GridCulture>();

/**
 * The culture of `locale`, a BCP 47 tag or `invariant` (ADR-0370 section 10). A tag `Intl` does not take is
 * refused with a `RangeError` naming it: reading in some other culture would read a number as one the user
 * did not type.
 */
export function gridCulture(locale: string): GridCulture {
    if (locale.toLowerCase() === invariantLocale) return invariant;
    const known = made.get(locale);
    if (known !== undefined) return known;
    let tags: readonly string[];
    try {
        tags = Intl.getCanonicalLocales(locale);
    } catch {
        throw new RangeError(`The locale '${locale}' is not a BCP 47 tag; a grid's locale is one, or '${invariantLocale}'.`);
    }
    const [tag] = tags;
    if (tag === undefined || locale.length === 0) {
        throw new RangeError(`The locale is empty; a grid's locale is a BCP 47 tag, or '${invariantLocale}'.`);
    }
    // Grouped always, as C#'s N0 groups 1234 in every culture: ICU leaves a four-digit number ungrouped in
    // some (es-ES), where Windows does not, and a text typed with the group would then read as refused.
    const numbers = new Intl.NumberFormat(tag, { useGrouping: 'always', maximumFractionDigits: 1, numberingSystem: 'latn' });
    const parts = [
        ...numbers.formatToParts(-1234.5),
        ...new Intl.NumberFormat(tag, { signDisplay: 'always', numberingSystem: 'latn' }).formatToParts(1),
    ];
    const part = (type: Intl.NumberFormatPartTypes): string =>
        defined(parts.find(found => found.type === type), `${tag}'s ${type} in Intl's parts`).value;
    const dateParts = new Intl.DateTimeFormat(tag, { year: 'numeric', month: '2-digit', day: '2-digit', calendar: 'gregory', numberingSystem: 'latn' })
        .formatToParts(new Date(Date.UTC(2026, 0, 5, 12)))
        .map(found => found.type);
    const at = (type: Intl.DateTimeFormatPartTypes): number => dateParts.indexOf(type);
    const whole = new Intl.NumberFormat(tag, { useGrouping: 'always', maximumFractionDigits: 0, numberingSystem: 'latn' });
    const culture: GridCulture = Object.freeze({
        name: locale,
        displayName: defined(new Intl.DisplayNames('en', { type: 'language', languageDisplay: 'standard' }).of(tag), `${tag}'s name`),
        decimal: part('decimal'),
        group: part('group'),
        negativeSign: part('minusSign'),
        positiveSign: part('plusSign'),
        yearFirst: at('year') < at('month'),
        dayFirst: at('day') < at('month'),
        grouped: (integer: bigint) => whole.format(integer < 0n ? -integer : integer),
    });
    made.set(locale, culture);
    return culture;
}

function groupedInThrees(digits: string, group: string): string {
    const runs: string[] = [];
    for (let end = digits.length; end > 0; end -= 3) runs.unshift(digits.slice(Math.max(0, end - 3), end));
    return runs.join(group);
}
