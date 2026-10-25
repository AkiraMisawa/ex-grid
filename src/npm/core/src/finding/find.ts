// The reference Find (ADR-0055), the counterpart of C#'s GridFind: what a match is, and the order a step
// walks. `GridSource.from` answers with it and a remote source is held to it, as the query engine is for
// filtering and sorting (ADR-0023).
//
// What is matched is the displayed text: the column's format applied, the text a user reads and so the
// only form they can type. It is compared as .NET's OrdinalIgnoreCase compares unless Match case, and by
// containment unless Whole cell. The order is Excel's default, by rows: row by row, and across a row in
// the request's column order, starting after the cell the step starts from and wrapping, so that cell is
// considered last and a lone match finds itself.

import { itemAt } from '../must';
import { foldCase } from '../query/text-casing';
import type { CellPosition } from '../selection/cell';
import { rangeContains, type SelectionRange } from '../selection/range';

/**
 * What the find panel holds (ADR-0055, ADR-0332 §4): the text to look for and the two options, without
 * anything that belongs to one step. A Match Mark judges each painted cell against it by
 * `findMatches`.
 */
export interface GridFindPattern {
    /** What to look for. The grid never judges a cell against an empty one: an empty field marks nothing. */
    readonly text: string;
    /** Off: compared as OrdinalIgnoreCase compares, ADR-0023's rule. On: code unit by code unit. */
    readonly matchCase: boolean;
    /** Off: the displayed text contains `text`. On: it equals it. */
    readonly wholeCell: boolean;
}

/**
 * One step of a Find (ADR-0055): what to look for, where to start, which way, and in what. The grid asks
 * and the Consumer answers, as it answers for sort and filter; the grid only moves the Focus.
 */
export interface GridFindRequest extends GridFindPattern {
    /** Whether this step runs backward: "previous", Shift+Enter. */
    readonly backward: boolean;
    /** The Focus the step starts from: the search begins at the cell after it (before it, backward) and
     * considers it last, so a lone match finds itself. Absent: from the start (the end, backward). */
    readonly from?: CellPosition | undefined;
    /** The selected ranges, when more than one cell is selected: Excel's "search the selection". Absent:
     * every row. A cell covered by two ranges counts once. */
    readonly scope?: readonly SelectionRange[] | undefined;
    /** The visible columns' names in the grid's current order: position `i` here is column `i` of every
     * position in this request. */
    readonly columns: readonly string[];
    /** The order the positions above were read in (ADR-0011). An answer the grid receives under another
     * version is discarded. */
    readonly rowSequenceVersion: number;
}

/** A Find step's answer (ADR-0055): a row in the order the request was read in and a column name, or
 * nothing found. */
export type GridFindResult =
    | { readonly found: true; readonly row: number; readonly column: string }
    | { readonly found: false };

/** Nothing matches anywhere the step looked. */
export const notFound: GridFindResult = Object.freeze({ found: false });

/** The next match: `row` in the order the request was read in, `column` one of the request's names. */
export function foundAt(row: number, column: string): GridFindResult {
    if (!Number.isInteger(row) || row < 0) throw new RangeError(`A match is in a row at or after 0, not ${row}.`);
    if (typeof column !== 'string' || column === '') throw new TypeError('A match names its column.');
    return Object.freeze({ found: true, row, column });
}

/** The pattern of a request: its text and its two options, as a Match Mark judges a cell. */
export function patternOf(request: GridFindPattern): GridFindPattern {
    return Object.freeze({ text: request.text, matchCase: request.matchCase, wholeCell: request.wholeCell });
}

/**
 * The reference rule for one cell (ADR-0055, ADR-0332 §4): whether `displayedText`, the column's format
 * applied, matches `pattern`. The step finds by it, and the grid marks the painted matches by it. An empty
 * pattern matches nothing, as an empty field asks nothing.
 */
export function findMatches(displayedText: string, pattern: GridFindPattern): boolean {
    if (typeof displayedText !== 'string') throw new TypeError('A cell is matched by its displayed text, which is text.');
    return matcherOf(pattern)(displayedText);
}

/**
 * One step: the next match in `rows`, the result in its current order. `textOf` answers a column's
 * displayed text by name, or `undefined` for a column that has no text to search (an Action Column) or
 * that the source does not know, which is skipped.
 */
export function findStep<Row>(
    rows: readonly Row[], request: GridFindRequest, textOf: (column: string) => ((row: Row) => string) | undefined,
): GridFindResult {
    const columnCount = request.columns.length;
    if (request.text === '' || rows.length === 0 || columnCount === 0) return notFound;
    const texts = request.columns.map(textOf);
    const total = rows.length * columnCount;
    // The ordinal of the cell `from` names, in row-major order; the walk starts one step past it and ends
    // on it. No `from`: the walk starts at the first cell (the last, backward), which is the same as
    // starting past a cell just before it.
    const { from } = request;
    if (from !== undefined && (!Number.isInteger(from.row) || !Number.isInteger(from.column))) {
        throw new RangeError(`A Find step starts from a cell at whole positions, not [${from.row}, ${from.column}].`);
    }
    const matches = matcherOf(request);
    const start = from !== undefined && from.row >= 0 && from.row < rows.length && from.column >= 0 && from.column < columnCount
        ? from.row * columnCount + from.column
        : request.backward ? 0 : total - 1;
    const step = request.backward ? -1 : 1;
    for (let walked = 1; walked <= total; walked++) {
        const ordinal = (((start + step * walked) % total) + total) % total;
        const row = Math.floor(ordinal / columnCount);
        const column = ordinal % columnCount;
        const text = texts[column];
        if (text === undefined) continue;
        if (request.scope !== undefined && !inScope(request.scope, row, column)) continue;
        if (matches(text(itemAt(rows, row)))) {
            return foundAt(row, itemAt(request.columns, column));
        }
    }
    return notFound;
}

/** Whether a cell lies in any of the ranges. */
export function inScope(scope: readonly SelectionRange[], row: number, column: number): boolean {
    return scope.some(range => rangeContains(range, { row, column }));
}

/**
 * The rule itself, written once: the step, the mark and Replace all judge by it. Prepared once for a
 * pattern, so the pattern's text is folded once rather than once for every cell judged. An empty pattern
 * matches nothing.
 */
export function matcherOf(pattern: GridFindPattern): (shown: string) => boolean {
    const { text, matchCase, wholeCell } = pattern;
    if (text === '') return () => false;
    if (matchCase) return wholeCell ? shown => shown === text : shown => shown.includes(text);
    const folded = foldCase(text);
    return wholeCell ? shown => foldCase(shown) === folded : shown => foldCase(shown).includes(folded);
}
