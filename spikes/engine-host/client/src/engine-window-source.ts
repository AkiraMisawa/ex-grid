// A Grid Source over a Window the Engine Host answers (ADR-0370 section 2): the grid says which rows it
// needs, this source asks the Worker for them, and holds the newest Window answered.
//
// - An answer carries what it was asked against. Every write moves the source's epoch as it is posted,
//   and a read answered for an older epoch is not drawn: it is asked again, for the newest version.
// - Writes are never refused for being late. Each is posted at once, the Worker does them in order, and
//   each answers the Window as it left it, which is drawn as it arrives: always newer than what is shown.
// - Reads coalesce: one is in flight at a time, and when it lands the next asks for what the grid needs
//   then, not for each range it asked for meanwhile.
// - Until the last write is answered the state says `loading`, which the page shows as calculating: an
//   answer older than the newest edit is never shown as the current one.

import type { DistinctValues, GridFilter, GridSource, GridSourceState, RowRange, SortSpec } from '../../../../packages/core/src/index';

/** A Window as the Worker answered it. */
export interface WindowAnswer<Row> {
    readonly rows: readonly Row[];
    readonly start: number;
    readonly totalCount: number;
}

export class EngineWindowSource<Row> implements GridSource<Row> {
    readonly #read: (range: RowRange) => Promise<WindowAnswer<Row>>;
    readonly #same: (painted: Row, next: Row) => boolean;
    readonly #readAhead: number;
    readonly #listeners = new Set<() => void>();
    #state: GridSourceState<Row>;
    #epoch = 0;
    #writesInFlight = 0;
    #needed: RowRange | undefined;
    #reading: Promise<void> | undefined;

    /** `read` answers a Window; `same` says whether a row of a new Window is the row already painted, which
     * then keeps its instance and is not painted again (ADR-0003). */
    constructor(read: (range: RowRange) => Promise<WindowAnswer<Row>>, same: (painted: Row, next: Row) => boolean, first: WindowAnswer<Row>, readAhead = 60) {
        this.#read = read;
        this.#same = same;
        this.#readAhead = readAhead;
        this.#state = Object.freeze({
            window: Object.freeze([...first.rows]), windowStart: first.start, totalCount: first.totalCount,
            loading: false, rowSequenceVersion: 0, sorts: Object.freeze([]), filter: undefined,
        });
    }

    subscribe(listener: () => void): () => void {
        this.#listeners.add(listener);
        return () => { this.#listeners.delete(listener); };
    }

    snapshot(): GridSourceState<Row> {
        return this.#state;
    }

    /** The rows a write answers with: what the grid needs, widened by the read-ahead. */
    get held(): RowRange {
        const needed = this.#needed ?? { start: this.#state.windowStart, count: Math.max(1, this.#state.window.length) };
        return this.#expand(needed);
    }

    setColumns(): void {
        // The columns are the page's; the Worker answers every column the page asks for.
    }

    setSorts(sorts: readonly SortSpec[]): void {
        if (sorts.length > 0) throw new Error('The Engine Host answers its own order; a Sort is not taken here.');
    }

    setFilter(filter: GridFilter | undefined): void {
        if (filter !== undefined) throw new Error('The Engine Host answers every row; a Filter is not taken here.');
    }

    needRange(range: RowRange): Promise<void> {
        this.#needed = range;
        if (this.#covers(range)) return Promise.resolve();
        return this.#readUntilCovered();
    }

    async rows(range: RowRange): Promise<readonly Row[]> {
        const epoch = this.#epoch;
        const answer = await this.#read(range);
        // A copy taken across a write would mix two versions: refused rather than guessed (spine rule 1).
        if (epoch !== this.#epoch) throw new Error('The document changed while its rows were read; copy again.');
        const skip = range.start - answer.start;
        return answer.rows.slice(skip, skip + range.count);
    }

    distinctValues(): Promise<DistinctValues> {
        return Promise.resolve(Object.freeze({ kind: 'tooMany' }));
    }

    /**
     * Posts a write at once, in order behind every write before it, and draws the Window it answers.
     * `moved` says the write moved the rows' order or membership (ADR-0011).
     */
    async write(perform: (held: RowRange) => Promise<WindowAnswer<Row>>, moved = false): Promise<void> {
        this.#epoch += 1;
        this.#writesInFlight += 1;
        this.#publish({ loading: true });
        try {
            const answer = await perform(this.held);
            this.#apply(answer, moved);
            // The grid scrolled on while the write was answered: what it needs now is read at the new version.
            const needed = this.#needed;
            if (needed !== undefined && !this.#covers(needed)) void this.#readUntilCovered();
        } finally {
            this.#writesInFlight -= 1;
            if (this.#writesInFlight === 0) this.#publish({ loading: false });
        }
    }

    #readUntilCovered(): Promise<void> {
        this.#reading ??= (async () => {
            try {
                for (let needed = this.#needed; needed !== undefined && !this.#covers(needed); needed = this.#needed) {
                    const epoch = this.#epoch;
                    const answer = await this.#read(this.#expand(needed));
                    if (epoch === this.#epoch) this.#apply(answer, false);
                }
            } finally {
                this.#reading = undefined;
            }
        })();
        return this.#reading;
    }

    #apply(answer: WindowAnswer<Row>, moved: boolean): void {
        const painted = this.#state;
        const rows = answer.rows.map((row, i) => {
            const at = answer.start + i - painted.windowStart;
            const old = moved || at < 0 || at >= painted.window.length ? undefined : painted.window[at];
            return old !== undefined && this.#same(old, row) ? old : row;
        });
        this.#publish({
            window: Object.freeze(rows),
            windowStart: answer.start,
            totalCount: answer.totalCount,
            rowSequenceVersion: moved ? painted.rowSequenceVersion + 1 : painted.rowSequenceVersion,
        });
    }

    #publish(change: Partial<GridSourceState<Row>>): void {
        this.#state = Object.freeze({ ...this.#state, ...change });
        for (const listener of Array.from(this.#listeners)) listener();
    }

    #covers(range: RowRange): boolean {
        const { windowStart, window, totalCount } = this.#state;
        const end = Math.min(range.start + range.count, totalCount ?? Number.MAX_SAFE_INTEGER);
        return range.start >= windowStart && end <= windowStart + window.length;
    }

    #expand(range: RowRange): RowRange {
        const start = Math.max(0, range.start - this.#readAhead);
        const total = this.#state.totalCount ?? Number.MAX_SAFE_INTEGER;
        const end = Math.max(start + 1, Math.min(total, range.start + range.count + this.#readAhead));
        return { start, count: end - start };
    }
}
