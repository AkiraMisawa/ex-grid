// The locale a Framework Adapter hands the pivot when the Consumer gives none (ADR-0370 section 10): the
// browser's own, as the browser ranks its languages. The pivot writes numbers only in the locales its engine
// holds, so a language it does not hold is passed over for the next the browser lists. A language is never
// stood in for by another, its region dropped: `es-419` written as `es` would group thousands with a point
// for readers who read it as a decimal. A browser with none it holds is refused, naming them.

import { pivotLocales } from '@ex-grid/pivot-engine';

/**
 * The first of `languages`, in the browser's order, that the pivot writes in, matched whole and without
 * regard to case. A list with none it holds throws a `RangeError` that says to pass `locale`.
 */
export function browserPivotLocale(languages: readonly string[]): string {
    const held = new Map(pivotLocales().map(name => [name.toLowerCase(), name]));
    for (const tag of languages) {
        const exact = held.get(tag.toLowerCase());
        if (exact !== undefined) return exact;
    }
    throw new RangeError(`None of the browser's languages (${languages.join(', ')}) is one the pivot writes numbers in: pass locale, a BCP 47 tag or 'invariant' (ADR-0370 section 10).`);
}
