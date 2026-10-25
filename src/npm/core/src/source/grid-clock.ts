// A source's clock (ADR-0141): when its live changes reach the grid is the source's to decide, never the
// grid's, whose core has no clock for data. A test hands in its own.

/** The time a source gathers live changes by. */
export interface GridClock {
    /** Now, in milliseconds since the epoch, on a clock that only moves forward: a wall clock set back
     * would hold the gathered changes until it caught up again. Since the epoch, so a change time read
     * from it is an instant the Change Highlight can compare with (ADR-0068). */
    now(): number;
    /** Calls `callback` once, `milliseconds` from now; the answer cancels the call if it has not run. */
    setTimer(callback: () => void, milliseconds: number): () => void;
}

/** The browser's own clock: `performance.now` from the page's time origin, which the wall clock's
 * corrections do not move, and `setTimeout`. */
export const systemClock: GridClock = Object.freeze({
    now: () => performance.timeOrigin + performance.now(),
    setTimer: (callback: () => void, milliseconds: number) => {
        const handle = setTimeout(callback, milliseconds);
        return () => { clearTimeout(handle); };
    },
});
