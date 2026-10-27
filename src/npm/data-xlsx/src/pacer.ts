// How a read paces itself (ADR-0330 §9, ADR-0064), as ExGrid.Data.Xlsx's Pacer paces it: in slices sized by time,
// yielding between them so that a browser keeps painting while a large workbook opens, reporting its progress after
// each, and seeing a cancellation between them.

/** How far a read has come. */
export interface XlsxProgress {
    /** The index of the worksheet being read, or -1 while the shared strings are. */
    readonly sheet: number;
    /** The sheets the workbook lists. */
    readonly sheets: number;
    /** The cells read so far, every worksheet counted. */
    readonly cells: number;
    /** The worksheets' bytes inflated so far; while the shared strings are read, theirs. */
    readonly bytes: number;
    /** The worksheets' uncompressed bytes, as the zip declares them; while the shared strings are read, theirs. */
    readonly totalBytes: number;
}

/** The limits, progress and pacing of a read. A key left out takes the default its comment names. */
export interface XlsxReadOptions {
    /**
     * The uncompressed bytes the package may hold, every part counted, as the zip declares them, and that a part may
     * inflate to: past it the file is refused as `tooLarge`. 1 GiB when left out.
     */
    readonly maxUncompressedBytes?: number;
    /** The cells a read may hold, every worksheet counted: past it the file is refused as `tooManyCells`. 10,000,000
     * when left out. */
    readonly maxCells?: number;
    /** Told how far the read has come after every slice, and once more at the end of the shared strings and of each
     * worksheet. */
    readonly onProgress?: (progress: XlsxProgress) => void;
    /** How long one slice works, in milliseconds, before the read yields; 30 when left out. A slice always does some work,
     * so 0 yields after every batch. */
    readonly sliceBudgetMs?: number;
    /** What the read awaits between slices; a macrotask (`setTimeout(…, 0)`) when left out, which gives a browser a turn
     * of its event loop to paint in. */
    readonly yield?: () => Promise<void>;
    /** Cancels the read: it rejects with the signal's reason, looked at between slices. */
    readonly signal?: AbortSignal;
}

/** The uncompressed bytes a read allows when the options name none: 1 GiB. */
export const defaultMaxUncompressedBytes = 2 ** 30;

/** The cells a read allows when the options name none. */
export const defaultMaxCells = 10_000_000;

function macrotask(): Promise<void> {
    return new Promise(resolve => {
        setTimeout(resolve, 0);
    });
}

/** Paces a read in slices sized by time; cancellation is looked at between them. */
export class Pacer {
    readonly #budget: number;
    readonly #yield: () => Promise<void>;
    readonly #onProgress: ((progress: XlsxProgress) => void) | undefined;
    readonly #signal: AbortSignal | undefined;
    #sliceStart = performance.now();

    constructor(options: XlsxReadOptions) {
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

    /** A point between batches: when the slice has spent its budget, reports `progress` and yields. */
    async checkpoint(progress: () => XlsxProgress): Promise<void> {
        this.cancelled();
        if (performance.now() < this.#sliceStart + this.#budget) return;
        this.#onProgress?.(progress());
        await this.#yield();
        this.cancelled();
        this.#sliceStart = performance.now();
    }

    report(progress: XlsxProgress): void {
        this.#onProgress?.(progress);
    }
}
