// The Fill Intent (ADR-0050, item 5): a fill-handle drag, released. The C# core's GridFillIntent.

import type { GridDirection } from './cell';
import type { SelectionRange } from './range';

/**
 * A fill-handle drag, released (ADR-0050, item 5): the source range, the target range beside it and the
 * direction. The grid writes nothing: what a fill means, copies, a series or a refusal, is the Consumer's,
 * which resolves the intent into its own data and hands new rows back, as with every edit (ADR-0007). One drag
 * is one intent.
 *
 * Positional, like a paste: the Consumer resolves the ranges under the order they were written in, and a
 * `rowSequenceVersion` that no longer matches its own means the order moved under the intent, which must then
 * be discarded (ADR-0011).
 *
 * After a fill the Consumer accepts, the Selection is the source and the target together, as in Excel; a
 * Consumer that does not implement the pattern calls {@link GridFillIntent.refuse} before its `onFill` returns,
 * or before the promise it returned settles, and the Selection stays on the source (ADR-0380 section 2).
 * @public
 */
export class GridFillIntent {
    /** The range the handle was dragged from: the Selection when the drag began. */
    readonly source: SelectionRange;
    /** The cells to fill, adjacent to the source along one axis; never the source itself. Every column it covers
     * was Editable when the intent was raised (ADR-0035). */
    readonly target: SelectionRange;
    /** Which way from the source the target lies. */
    readonly direction: GridDirection;
    /** The order these positions are written in (ADR-0011): the source's own Row Sequence Version. */
    readonly rowSequenceVersion: number;
    /** Aborted when the user cancels the fill with Escape while the Consumer's answer is out (ADR-0380 section 3):
     * nothing is filled, and a handler that writes after it is aborted writes a fill the user took back. */
    readonly signal: AbortSignal;
    #refused = false;
    #sealed = false;

    /** @internal */
    constructor(source: SelectionRange, target: SelectionRange, direction: GridDirection, rowSequenceVersion: number, signal: AbortSignal) {
        this.source = source;
        this.target = target;
        this.direction = direction;
        this.rowSequenceVersion = rowSequenceVersion;
        this.signal = signal;
    }

    /** Whether the Consumer refused the fill. */
    get refused(): boolean {
        return this.#refused;
    }

    /** The Consumer's answer that it does not fill this: a pattern it has not implemented is refused, never filled
     * with copies (ADR-0050, item 5). Called before `onFill` returns, or before the promise it returned settles, it
     * leaves the Selection on the source; telling the user why is the Consumer's. Called after, it throws: the grid
     * has already taken the fill as made, and placed the Selection over it. */
    refuse(): void {
        if (this.#sealed) {
            throw new Error('This fill was answered when onFill returned or its promise settled: refuse it before then (ADR-0050, item 5).');
        }
        this.#refused = true;
    }

    /** @internal Ends the time the Consumer may refuse it in. */
    seal(): void {
        this.#sealed = true;
    }
}
