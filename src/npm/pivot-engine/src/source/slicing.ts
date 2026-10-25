// How the bundled sources share the thread (PV-40): a question's pass and the answer assembled after it run
// in slices sized by time, yielding between them so that a browser keeps painting, and a cancelled question
// stops at the next slice. The C# engine's PivotSlicing and Slicer.

/** How a source's work paces itself (PV-40). */
export interface PivotSlicing {
    /** How long one slice works, in milliseconds, before it yields; 30 when left out, within the 50 a page
     * may be blocked for. A slice always does some work, so 0 yields after every step. */
    readonly sliceBudgetMs?: number;
    /** What the work awaits between slices; a macrotask (`setTimeout(…, 0)`) when left out, which gives a
     * browser a turn of its event loop to paint in. */
    readonly yield?: () => Promise<void>;
}

function macrotask(): Promise<void> {
    return new Promise(resolve => {
        setTimeout(resolve, 0);
    });
}

/** Paces one question's work: at each step it looks at the signal, and yields once the slice is spent. */
export class Slicer {
    readonly #budget: number;
    readonly #yield: () => Promise<void>;
    readonly #signal: AbortSignal | undefined;
    #sliceStart = performance.now();

    constructor(slicing: PivotSlicing, signal: AbortSignal | undefined) {
        const budget = slicing.sliceBudgetMs === undefined ? 30 : slicing.sliceBudgetMs;
        if (typeof budget !== 'number' || Number.isNaN(budget) || budget < 0) {
            throw new RangeError(`A slice budget is a number of milliseconds, not negative; ${String(budget)} is not.`);
        }
        this.#budget = budget;
        this.#yield = slicing.yield === undefined ? macrotask : slicing.yield;
        this.#signal = signal;
        signal?.throwIfAborted();
    }

    /** A point between two steps: throws once the question is cancelled, and yields when the slice is spent. */
    async step(): Promise<void> {
        this.#signal?.throwIfAborted();
        if (performance.now() < this.#sliceStart + this.#budget) return;
        await this.#yield();
        this.#signal?.throwIfAborted();
        this.#sliceStart = performance.now();
    }

    /** Runs a piece of work that yields between its steps, a step at a time. */
    async run<T>(steps: Generator<void, T>): Promise<T> {
        for (;;) {
            const next = steps.next();
            if (next.done === true) return next.value;
            await this.step();
        }
    }
}

/** Checks a Consumer's slicing when a source is made, so a wrong budget fails there and not at a question. */
export function checkedSlicing(slicing: PivotSlicing | undefined): PivotSlicing {
    const chosen = slicing === undefined ? {} : slicing;
    const budget = chosen.sliceBudgetMs;
    if (budget !== undefined && (typeof budget !== 'number' || Number.isNaN(budget) || budget < 0)) {
        throw new RangeError(`A slice budget is a number of milliseconds, not negative; ${String(budget)} is not.`);
    }
    if (chosen.yield !== undefined && typeof chosen.yield !== 'function') throw new TypeError('A slicing\'s yield is a function that returns a promise.');
    return Object.freeze({ ...chosen });
}
