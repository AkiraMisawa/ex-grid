// The order a Find step walks once the headers are searched (ADR-0333 §3), the counterpart of C#'s
// HeaderFindWalk: the header rows first, top to bottom and each left to right, then the data rows by rows,
// wrapping; backward the reverse. The grid searches the headers itself and asks the Consumer only about
// rows, with the request ADR-0055 gave it, unchanged. This is the arithmetic of the two together.

import { itemAt } from '../must';
import type { CellPosition } from '../selection/cell';

/**
 * The header a step lands on without asking the Consumer, or `undefined` when the Consumer is asked.
 * `matches` are the matching headers' indices in walk order, ascending. From a header (`fromHeader`), the
 * next matching header after it (before it, backward) is reached before any row. From nothing at all, no
 * Focus and no header, a forward walk starts at the top, where the header rows are, so the first matching
 * header is the answer; backward it starts at the end of the rows, and the Consumer is asked.
 */
export function headerBeforeAsking(
    matches: readonly number[], fromHeader: number | undefined, fromCell: boolean, backward: boolean,
): number | undefined {
    if (matches.length === 0) return undefined;
    if (fromHeader !== undefined) {
        return backward ? matches.findLast(match => match < fromHeader) : matches.find(match => match > fromHeader);
    }
    return !fromCell && !backward ? itemAt(matches, 0) : undefined;
}

/**
 * The header a step lands on once the Consumer has answered, or `undefined` when its answer stands. The
 * Consumer was asked from the Focus (`from`), or from the start of the rows (the end, backward) when the
 * step began at a header or nowhere. Its answer passed the header rows when it wrapped, at or before
 * `from` forward and at or after it backward, or when nothing matched among the rows (`answer`
 * `undefined`); then the first matching header (the last, backward) is reached first. Asked from the
 * start, the answer wraps past nothing and stands whenever it was found.
 */
export function headerAfterAnswer(
    matches: readonly number[], from: CellPosition | undefined, answer: CellPosition | undefined, backward: boolean,
): number | undefined {
    if (matches.length === 0) return undefined;
    const wrapped = answer === undefined
        || (from !== undefined && (backward ? compare(answer, from) >= 0 : compare(answer, from) <= 0));
    if (!wrapped) return undefined;
    return itemAt(matches, backward ? matches.length - 1 : 0);
}

// Row-major order: by row, then across the row in the request's column order.
function compare(a: CellPosition, b: CellPosition): number {
    return a.row !== b.row ? a.row - b.row : a.column - b.column;
}
