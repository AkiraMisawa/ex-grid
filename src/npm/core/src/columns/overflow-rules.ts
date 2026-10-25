import { unreachable } from '../must';
import type { CellTextMetrics } from './cell-text-metrics';

/**
 * The declared type of a Column. Declared by the Consumer, never inferred from values (ADR-0002): it
 * decides which filter operators exist (ADR-0023) and how a value too wide for its column is painted
 * (ADR-0016).
 */
export type ColumnType = 'text' | 'number' | 'date' | 'boolean';

/** What the cell paints (ADR-0016). Presentation only: selection, copy, editing and screen readers all
 * deal with the real value. */
export interface OverflowDecision {
    /** Whether the value does not fit and the cell paints `####` instead: Number and Date only. */
    readonly isHashed: boolean;
    /** What the cell paints: the formatted value, or, when {@link OverflowDecision.isHashed}, a run of
     * `#` longer than the cell, which the cell cuts at the last whole `#` that fits (ADR-0016, 2026-10-02). */
    readonly displayText: string;
}

/**
 * The one place the Number/Date-versus-Text/Boolean split lives (ADR-0016). It answers both "does this
 * type become `####` when it does not fit" and "is this a numeric presentation" (right-aligned, tabular
 * digits): the two are the same classification by construction, and callers ask this rather than derive
 * it again.
 */
export function hashesWhenOverflowing(type: ColumnType): boolean {
    switch (type) {
        case 'text':
        case 'boolean':
            return false;
        case 'number':
        case 'date':
            return true;
        default:
            return unreachable(type);
    }
}

/**
 * What one cell paints at its resolved width (ADR-0016). Text and Boolean always show the value: a
 * truncated "fal…" is visibly truncated, so the danger `####` answers is absent. A Number or Date shows
 * it when its estimate fits, exactly fitting included, and otherwise becomes a run of `#` longer than
 * the content width: counted at half a digit's width, which no supported face draws `#` under, and at
 * least one. The cell paints the run in a box that may break between any two `#`, one line tall, so
 * what it shows is exactly the `#` that fit in the face it is painted in (ADR-0016's note of 2026-10-02).
 * The text is already formatted: formats and culture are not this rule's business.
 */
export function decideOverflow(
    type: ColumnType, formattedText: string, resolvedWidthPx: number, metrics: CellTextMetrics,
): OverflowDecision {
    if (!Number.isFinite(resolvedWidthPx) || resolvedWidthPx < 0) {
        throw new RangeError(`A resolved width is a finite, non-negative number of pixels. (resolvedWidthPx: ${resolvedWidthPx})`);
    }
    if (!hashesWhenOverflowing(type) || metrics.estimatePx(formattedText) <= resolvedWidthPx) {
        return Object.freeze({ isHashed: false, displayText: formattedText });
    }
    const hashCount = Math.max(1, Math.ceil(metrics.contentWidthPx(resolvedWidthPx) / (metrics.digitWidthPx / 2)));
    return Object.freeze({ isHashed: true, displayText: hashRun(hashCount) });
}

// Runs of hashes by length, made once (ADR-0027 P5). The run is the width's string, not the value's, so
// the same few lengths are every hashed cell any grid paints.
const hashRuns = new Map<number, string>();

function hashRun(count: number): string {
    const known = hashRuns.get(count);
    if (known !== undefined) {
        return known;
    }
    const run = '#'.repeat(count);
    hashRuns.set(count, run);
    return run;
}
