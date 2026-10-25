// A sort is a priority-ordered list of columns and directions (ADR-0023). The grid builds it and hands it
// over; the Consumer sorts.

/** Which way a column sorts. Blanks sort last either way (ADR-0023). */
export type SortDirection = 'ascending' | 'descending';

/** One level of a sort: a column, by name, and its direction. The first level of a list is the primary. */
export interface SortSpec {
    readonly column: string;
    readonly direction: SortDirection;
}

/**
 * The sort a click on `column`'s header asks for, from the sort in force: ascending, then descending, then
 * none. Only a single-column sort on this same column advances the cycle; any other state, unsorted,
 * another column or a several-column sort a Consumer set up, starts over at ascending on the clicked
 * column: the click means "sort by this", not "amend what was there".
 */
export function nextSort(current: readonly SortSpec[], column: string): readonly SortSpec[] {
    if (column === '') throw new TypeError('A sort names a column.');
    const [only] = current;
    if (current.length === 1 && only?.column === column) {
        return only.direction === 'ascending' ? Object.freeze([Object.freeze({ column, direction: 'descending' as const })]) : Object.freeze([]);
    }
    return Object.freeze([Object.freeze({ column, direction: 'ascending' as const })]);
}
