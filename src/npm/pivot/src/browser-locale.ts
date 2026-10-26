// The locale a Framework Adapter hands the pivot when the Consumer gives none (ADR-0370 section 10): the
// browser's own, as the browser ranks its languages. The pivot writes numbers only in the locales its engine
// holds, so a language it does not hold is passed over for the next, or for its own language without the
// region; one with none it holds is refused, naming them, rather than written in a locale the user never chose.

import { pivotLocales } from '@ex-grid/pivot-engine';

/**
 * The first of `languages`, in the browser's order, that the pivot writes in: the tag itself, or else its
 * language alone (`es-419` is written as `es`). A list with none it holds throws a `RangeError` that says to
 * pass `locale`.
 */
export function browserPivotLocale(languages: readonly string[]): string {
    const held = new Map(pivotLocales().map(name => [name.toLowerCase(), name]));
    for (const tag of languages) {
        const exact = held.get(tag.toLowerCase());
        if (exact !== undefined) return exact;
        const [subtag] = tag.split('-');
        const language = subtag === undefined ? undefined : held.get(subtag.toLowerCase());
        if (language !== undefined) return language;
    }
    throw new RangeError(`None of the browser's languages (${languages.join(', ')}) is one the pivot writes numbers in: pass locale, a BCP 47 tag or 'invariant' (ADR-0370 section 10).`);
}
