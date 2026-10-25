// How the engine's work shares the thread (PV-40): a question's pass, the answer assembled after it, the cube
// made from the answer and the report laid out from the cube run in slices sized by time, yielding between
// them so that a browser keeps painting, and cancelled work stops at the next slice. The C# engine's
// PivotSlicing and Slicer.

import { elementAt } from '../must';

/** How the engine's work paces itself (PV-40). */
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
    readonly #slicing: CheckedSlicing;
    readonly #signal: AbortSignal | undefined;
    #sliceStart = performance.now();

    constructor(slicing: CheckedSlicing, signal: AbortSignal | undefined) {
        this.#slicing = slicing;
        this.#signal = signal;
        signal?.throwIfAborted();
    }

    /** A point between two steps: throws once the question is cancelled, and yields when the slice is spent. */
    async step(): Promise<void> {
        this.#signal?.throwIfAborted();
        if (performance.now() < this.#sliceStart + this.#slicing.budget) return;
        await this.#slicing.yield();
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

/** The units of work a piece of the work after a pass is cut to — a leaf copied, a cell merged, a node of a
 * report entered — so that a step falls about every thousand of them. */
export const pieceUnits = 1024;

/** Runs `step` over [0, `count`) in pieces of about {@link pieceUnits} units, each element worth `weight` of
 * them, yielding between two pieces. */
export function* inPieces(count: number, weight: number, step: (from: number, to: number) => void): Generator<void, void> {
    const size = Math.max(1, Math.floor(pieceUnits / weight));
    for (let from = 0; from < count; from += size) {
        if (from > 0) yield;
        step(from, Math.min(count, from + size));
    }
}

/** Runs a piece of work that yields between its steps through to its result, unsliced. */
export function runWhole<T>(steps: Generator<void, T>): T {
    for (;;) {
        const next = steps.next();
        if (next.done === true) return next.value;
    }
}

/** `list` sorted as `toSorted` sorts it, stably, a piece at a time: a short list at once, a long one merged
 * bottom up, yielding once a piece's worth of elements is placed. Two runs already in order are copied with one
 * comparison, so a list that comes sorted — as a source's Items often do — costs a comparison an element. */
export function* sortedSteps<T extends object | number>(list: readonly T[], compare: (a: T, b: T) => number): Generator<void, readonly T[]> {
    if (list.length <= pieceUnits / 8) return list.toSorted(compare);
    let from = [...list];
    let to = [...list];
    let compared = 0;
    for (let width = 1; width < from.length; width *= 2) {
        for (let start = 0; start < from.length; start += 2 * width) {
            const middle = Math.min(start + width, from.length);
            const end = Math.min(start + (2 * width), from.length);
            if (middle >= end || compare(elementAt(from, middle - 1), elementAt(from, middle)) <= 0) {
                for (let at = start; at < end; at++) to[at] = elementAt(from, at);
                compared += end - start;
                if (compared >= pieceUnits) {
                    compared = 0;
                    yield;
                }
                continue;
            }
            let left = start;
            let right = middle;
            for (let at = start; at < end; at++) {
                if (left < middle && (right >= end || compare(elementAt(from, left), elementAt(from, right)) <= 0)) {
                    to[at] = elementAt(from, left++);
                } else {
                    to[at] = elementAt(from, right++);
                }
                if (++compared >= pieceUnits) {
                    compared = 0;
                    yield;
                }
            }
        }
        [from, to] = [to, from];
    }
    return from;
}

/** A Consumer's slicing, checked and resolved: how long a slice works, and what the work awaits between two. */
export interface CheckedSlicing {
    readonly budget: number;
    readonly yield: () => Promise<void>;
}

/** Checks a Consumer's slicing where it is given — when a source is made, and when a cube or a report is asked
 * for in slices — so a wrong budget fails there, before any work. */
export function checkedSlicing(slicing: PivotSlicing | undefined): CheckedSlicing {
    const chosen = slicing === undefined ? {} : slicing;
    const budget = chosen.sliceBudgetMs === undefined ? 30 : chosen.sliceBudgetMs;
    if (typeof budget !== 'number' || Number.isNaN(budget) || budget < 0) {
        throw new RangeError(`A slice budget is a number of milliseconds, not negative; ${String(budget)} is not.`);
    }
    const given = chosen.yield;
    if (given !== undefined && typeof given !== 'function') throw new TypeError('A slicing\'s yield is a function that returns a promise.');
    return Object.freeze({ budget, yield: given === undefined ? macrotask : () => given() });
}
