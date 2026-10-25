// A source's clock (ADR-0141): when its live changes reach the grid is the source's to decide, never the
// grid's, whose core has no clock for data. A test hands in its own.

/** The time a source gathers live changes by. */
export interface GridClock {
    /** Now, in milliseconds since the epoch. */
    now(): number;
    /** Calls `callback` once, `milliseconds` from now; the answer cancels the call if it has not run. */
    setTimer(callback: () => void, milliseconds: number): () => void;
}

/** The browser's own clock: `Date.now` and `setTimeout`. */
export const systemClock: GridClock = Object.freeze({
    now: () => Date.now(),
    setTimer: (callback: () => void, milliseconds: number) => {
        const handle = setTimeout(callback, milliseconds);
        return () => { clearTimeout(handle); };
    },
});
