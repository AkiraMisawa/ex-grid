// The Change Highlight's one rule (ADR-0068): a cell is marked while the current time is before the time
// its shown value changed plus the duration. The grid asks the Consumer for the change time and its clock
// for the current time; this answers whether the mark shows and when it ends, which is all the grid needs
// to paint the mark and to know when to take it away. Times are epoch milliseconds, as `Date.now()` reads.

/** How long a mark lasts unless the Consumer says otherwise: one second (ADR-0068). */
export const defaultChangeHighlightMs = 1000;

/** When the mark of a value that changed at `changedAtMs` ends: that time plus `durationMs`, the first
 * instant at which the cell is no longer marked. A negative duration is refused: a mark cannot end before
 * the change it marks. */
export function changeHighlightEnd(changedAtMs: number, durationMs: number): number {
    if (!Number.isFinite(changedAtMs)) {
        throw new RangeError(`A change time is an instant. (changedAtMs: ${changedAtMs})`);
    }
    if (Number.isNaN(durationMs) || durationMs < 0) {
        throw new RangeError(`A Change Highlight lasts zero or more milliseconds. (durationMs: ${durationMs})`);
    }
    return changedAtMs + durationMs;
}

/**
 * The end of the mark a cell shows at `nowMs`, or `undefined` while it shows none: when nobody said the
 * cell changed, and from the mark's end on, the end itself already unmarked. A change time later than now,
 * such as a server's clock running ahead of the grid's, is taken as given: the cell is marked until that
 * time plus the duration, as the rule reads. Moving it to now would need the grid to remember which cells
 * it had already seen, and a mark is keyed by row and column, never held.
 */
export function changeHighlightShowingUntil(changedAtMs: number | undefined, durationMs: number, nowMs: number): number | undefined {
    if (changedAtMs === undefined) return undefined;
    const end = changeHighlightEnd(changedAtMs, durationMs);
    return nowMs < end ? end : undefined;
}
