import type { XlsxSaveOptions, XlsxSaveProgress } from './save-error';

// Paces a write in slices sized by time (ADR-0330 §9, as a read is paced), as ExSheet.Xlsx's SavePacer paces it: a slice
// works until its budget is spent, then the write reports its progress and yields, so a browser — which has one thread —
// keeps painting while a large Workbook is written. Cancellation is looked at between batches of elements.

/** The elements written between two looks at the clock and the signal. */
const batch = 256;

function macrotask(): Promise<void> {
    return new Promise(resolve => {
        setTimeout(resolve, 0);
    });
}

export class SavePacer {
    readonly #budget: number;
    readonly #yield: () => Promise<void>;
    readonly #onProgress: ((progress: XlsxSaveProgress) => void) | undefined;
    readonly #signal: AbortSignal | undefined;
    #sliceStart = performance.now();
    #sinceLook = 0;

    /** The worksheets the file holds. */
    sheets = 0;
    /** The elements the file writes; 0 until the snapshot is planned. */
    total = 0;
    /** The worksheet being written; -1 while the snapshot is planned. */
    sheet = -1;
    /** The elements written so far. */
    written = 0;

    constructor(options: XlsxSaveOptions) {
        const budget = options.sliceBudgetMs === undefined ? 30 : options.sliceBudgetMs;
        if (typeof budget !== 'number' || Number.isNaN(budget)) throw new TypeError(`The slice budget ${String(budget)} is not a number of milliseconds.`);
        this.#budget = Math.max(0, budget);
        this.#yield = options.yield === undefined ? macrotask : options.yield;
        this.#onProgress = options.onProgress;
        this.#signal = options.signal;
    }

    /** Throws the signal's reason when the write is cancelled. */
    cancelled(): void {
        this.#signal?.throwIfAborted();
    }

    /**
     * One step of work, which wrote `elements` (none while the snapshot is planned); every batch of steps answers the
     * checkpoint to await, and every other step nothing, so a step costs no turn of the event loop.
     */
    step(elements = 1): Promise<void> | undefined {
        this.written += elements;
        if (++this.#sinceLook < batch) return undefined;
        this.#sinceLook = 0;
        return this.checkpoint();
    }

    /** Looks at the signal and, past the budget, reports the progress and yields. */
    async checkpoint(): Promise<void> {
        this.cancelled();
        if (performance.now() < this.#sliceStart + this.#budget) return;
        this.report();
        await this.#yield();
        this.cancelled();
        this.#sliceStart = performance.now();
    }

    /** Tells the progress as it stands. */
    report(): void {
        this.#onProgress?.({
            sheet: this.sheet,
            sheets: this.sheets,
            elements: this.sheet < 0 ? 0 : this.written,
            totalElements: this.sheet < 0 ? 0 : this.total,
        });
    }
}
