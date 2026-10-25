import { wholeAtLeast } from '../viewport/checks';

// What the gestures on a Mark Column mean (ADR-0043). The core owns the meaning; the marks themselves are
// the Consumer's, which is why these rules take counts rather than rows: a Consumer resolving a gesture
// over rows the grid has never seen applies the same rule to what it holds.

/** What the Mark Column's header checkbox shows (ADR-0043): no row of the current result marked, some
 * but not all, or every Detail row. */
export type RowMarkHeaderState = 'none' | 'some' | 'all';

/**
 * Why a press on a mark marked nothing (ADR-0043's note of 2026-10-08; MK-9). A press is judged against
 * the paint it was made on, and what it can no longer name exactly marks nothing and is refused once per
 * press, for the Consumer to word:
 * - `orderMoved`: the press named rows by position, and the order it named them in has moved since
 *   (ADR-0011);
 * - `sourceChanged`: the press was made on what the grid painted from a Grid Source it has since been
 *   unbound from (ADR-0142);
 * - `rowLeftTheWindow`: the waiting row command lost its identity from the current Window, even if it
 *   later returned (ADR-0160).
 */
export type MarkRefusalReason = 'orderMoved' | 'sourceChanged' | 'rowLeftTheWindow';

/**
 * The state a gesture over several rows brings them all to: marked if any of them was unmarked, unmarked
 * only when every one already was. Rows are brought into line, never flipped one by one: flipping would
 * leave a mixed block scrambled, and the result could not be predicted from the gesture (ADR-0043).
 * `marked` is how many of the `rows` are marked now; the answer is true to mark them all, false to unmark
 * them all.
 */
export function lineUpMarks(marked: number, rows: number): boolean {
    wholeAtLeast(marked, 0, 'marked');
    if (!Number.isSafeInteger(rows) || marked > rows) {
        throw new RangeError(`marked is at most rows, a whole number: ${marked} of ${rows}.`);
    }
    return marked < rows;
}

/**
 * What the Consumer answers about its Row Marks for the header and the count display (ADR-0043). The grid
 * cannot count them itself: it holds a Window, and a Window whose rows are all marked says nothing about
 * the million rows beyond it.
 */
export class RowMarkCounts {
    readonly #markedInResult: number;
    readonly #rowsInResult: number;
    readonly #markedOutsideResult: number;

    /** Counts as the Consumer holds them. Refused when they contradict each other, more marked rows in the
     * result than the result has, because a header painted from them would claim something nobody knows. */
    constructor(counts: { readonly markedInResult: number; readonly rowsInResult: number; readonly markedOutsideResult: number }) {
        this.#markedInResult = wholeAtLeast(counts.markedInResult, 0, 'markedInResult');
        this.#rowsInResult = wholeAtLeast(counts.rowsInResult, 0, 'rowsInResult');
        this.#markedOutsideResult = wholeAtLeast(counts.markedOutsideResult, 0, 'markedOutsideResult');
        if (this.#markedInResult > this.#rowsInResult) {
            throw new RangeError(
                `markedInResult is at most rowsInResult: ${this.#markedInResult} marked of ${this.#rowsInResult} rows.`);
        }
    }

    /** Marked Detail rows of the current result, after filtering. */
    get markedInResult(): number { return this.#markedInResult; }

    /** Detail rows of the current result. Group and Total rows are never marked, so they are never counted
     * (ADR-0024/0043). */
    get rowsInResult(): number { return this.#rowsInResult; }

    /** Marked rows the current filter leaves out, said aloud beside the count, so an action never lands on
     * rows the user cannot see without being told (ADR-0043). */
    get markedOutsideResult(): number { return this.#markedOutsideResult; }

    /** Every mark, inside the result and outside it. */
    get marked(): number { return this.#markedInResult + this.#markedOutsideResult; }

    /** Whether `other` holds the same three counts. */
    equals(other: RowMarkCounts): boolean {
        return this.#markedInResult === other.#markedInResult
            && this.#rowsInResult === other.#rowsInResult
            && this.#markedOutsideResult === other.#markedOutsideResult;
    }
}

/**
 * The header's state, from the counts the Consumer answers: none, some or all of the current result's
 * Detail rows. Never from the Window: a fully marked Window inside a larger result is "some" (ADR-0043).
 * Marks outside the current result do not count towards it; they are said aloud beside the count instead.
 * An empty result is "none".
 */
export function rowMarkHeaderState(counts: RowMarkCounts): RowMarkHeaderState {
    if (counts.markedInResult === 0) return 'none';
    return counts.markedInResult < counts.rowsInResult ? 'some' : 'all';
}

const pressMarks: Readonly<Record<RowMarkHeaderState, boolean>> = { none: true, some: true, all: false };

/** What pressing the header does from a given state: mark all, unless all are already marked. Pressing
 * "some" completes the marking, and only "all" clears it (ADR-0043). True to mark all, false to unmark. */
export function rowMarkHeaderPress(state: RowMarkHeaderState): boolean {
    // A state nobody declared, which only an untyped caller can pass, is refused: read as "some", it would
    // mark every row of the result.
    if (typeof state !== 'string' || !Object.hasOwn(pressMarks, state)) {
        throw new RangeError(`A header's state is none, some or all, not ${JSON.stringify(state)}.`);
    }
    return pressMarks[state];
}
