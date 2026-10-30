// How a read paces itself (ADR-0064), whichever format it reads: in slices sized by time, yielding between
// them so that a browser keeps painting while a million rows load, reporting its progress after each, and
// seeing a cancellation between them. The Arrow read and the CSV read both pace themselves this way.

/** How far a read has come. */
export interface SnapshotProgress {
    /** The rows read so far. */
    readonly rows: number;
    /** The rows there are to read, when that is known. */
    readonly totalRows: number | undefined;
    /** The bytes read so far, when the read can say. */
    readonly bytes: number | undefined;
    /** The bytes there are to read, when that is known. */
    readonly totalBytes: number | undefined;
}

/** How a read paces itself (ADR-0064): in slices sized by time, yielding between them so that a browser
 * keeps painting while a million rows load, and reporting its progress after each. */
export interface SnapshotReadOptions {
    /** Cancels the read: it rejects with the signal's reason and yields nothing. */
    readonly signal?: AbortSignal;
    /** Told how far the read has come after every slice, and once more at the end. */
    readonly onProgress?: (progress: SnapshotProgress) => void;
    /** How long one slice works, in milliseconds, before the read yields; 30 when left out. A slice always
     * does some work, so 0 yields after every chunk of rows. */
    readonly sliceBudgetMs?: number;
    /** What the read awaits between slices; a macrotask (`setTimeout(…, 0)`) when left out, which gives a
     * browser a turn of its event loop to paint in. */
    readonly yield?: () => Promise<void>;
}

function macrotask(): Promise<void> {
    return new Promise(resolve => {
        setTimeout(resolve, 0);
    });
}

/** Paces a read in slices sized by time; cancellation is looked at between chunks. */
export class Pacer {
    readonly #budget: number;
    readonly #yield: () => Promise<void>;
    readonly #onProgress: ((progress: SnapshotProgress) => void) | undefined;
    readonly #signal: AbortSignal | undefined;
    #sliceStart = performance.now();

    constructor(options: SnapshotReadOptions) {
        const budget = options.sliceBudgetMs === undefined ? 30 : options.sliceBudgetMs;
        if (typeof budget !== 'number' || Number.isNaN(budget)) throw new TypeError(`The slice budget ${String(budget)} is not a number of milliseconds.`);
        this.#budget = Math.max(0, budget);
        this.#yield = options.yield === undefined ? macrotask : options.yield;
        this.#onProgress = options.onProgress;
        this.#signal = options.signal;
    }

    cancelled(): void {
        this.#signal?.throwIfAborted();
    }

    /** A point between chunks: when the slice has spent its budget, reports `progress` and yields. */
    async checkpoint(progress: SnapshotProgress): Promise<void> {
        this.cancelled();
        if (performance.now() < this.#sliceStart + this.#budget) return;
        this.#onProgress?.(progress);
        await this.#yield();
        this.cancelled();
        this.#sliceStart = performance.now();
    }

    report(progress: SnapshotProgress): void {
        this.#onProgress?.(progress);
    }
}

