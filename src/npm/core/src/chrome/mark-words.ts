import type { RowMarkCounts } from '../rows/row-marks';

/** The ids of the words the Mark Column shows (ADR-0043, ADR-0370 section 10), resolved through the grid's
 * `commandLabel`, as the menus' words are. */
export const markLabelIds = Object.freeze({
    /** A row's checkbox's accessible name. */
    markRow: 'mark-row',
    /** The header's checkbox's accessible name. */
    markAllRows: 'mark-all-rows',
    /** The count while every mark lies inside the current filter, at `{count}`. */
    marked: 'mark-count',
    /** The count while some lie outside it: every mark at `{count}`, the ones outside at `{outside}`. */
    markedOutside: 'mark-count-outside-filter',
    /** Under a pager, the button that marks every row of the result, past the page, at `{count}` (ADR-0015). */
    markAllResultRows: 'mark-all-result-rows',
} as const);

/** The built-in English for the Mark Column's ids, as the C# core says them. */
export const markWords: readonly (readonly [string, string])[] = Object.freeze([
    [markLabelIds.markRow, 'Mark row'],
    [markLabelIds.markAllRows, 'Mark all rows'],
    [markLabelIds.marked, '{count} marked'],
    [markLabelIds.markedOutside, '{count} marked ({outside} outside the current filter)'],
    [markLabelIds.markAllResultRows, 'Mark all {count} rows'],
]);

/**
 * What the count beside the grid says (ADR-0043): every mark, with the clause that names the marks outside the
 * current filter whenever there are any, so an action never runs over rows the user cannot see without being
 * told. Nothing while nothing is marked, and nothing while the counts are not known: the grid claims nothing it
 * was not told.
 */
export function markCountText(counts: RowMarkCounts | undefined, words: (id: string) => string): string | undefined {
    if (counts === undefined || counts.marked === 0) return undefined;
    if (counts.markedOutsideResult === 0) return words(markLabelIds.marked).replaceAll('{count}', String(counts.marked));
    return words(markLabelIds.markedOutside)
        .replaceAll('{count}', String(counts.marked))
        .replaceAll('{outside}', String(counts.markedOutsideResult));
}
