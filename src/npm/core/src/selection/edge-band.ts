/**
 * The edge band's rate curve (ADR-0008): a drag whose pointer is in the Viewport's inner 20px scrolls by
 * one row a tick at the band's inner lip, rising to eight at the outer edge. The ceiling is one row
 * short of ADR-0004's fling threshold, so the rows being selected across never turn into Placeholders.
 * The three numbers are provisional and OBSERVATIONAL (ADR-0008); the shape is not.
 */
export const edgeBand = Object.freeze({
    /** How far the band reaches into the Viewport from each edge. */
    depthPx: 20,
    /** The rate at the band's inner lip, in rows a tick. */
    minRowsPerTick: 1,
    /** The rate at the Viewport's outer edge, in rows a tick; lower when a Viewport holds fewer than nine
     * rows, to stay under the fling threshold. */
    maxRowsPerTick: 8,
});

/**
 * Signed rows a tick for a pointer at `positionPx` inside a visible band `lengthPx` long: negative toward
 * the leading edge, positive toward the trailing one, zero outside both bands. The rate never reaches
 * `rowsPerViewport`, the fling threshold's guard.
 */
export function edgeBandRowsPerTick(positionPx: number, lengthPx: number, rowsPerViewport: number): number {
    if (!Number.isFinite(positionPx) || !Number.isFinite(lengthPx) || lengthPx <= 0) {
        return 0;
    }
    // A band so short the two strips overlap scrolls nothing: every position would be at the edge of
    // both ends at once.
    if (lengthPx < edgeBand.depthPx * 2) {
        return 0;
    }
    const ceiling = Math.max(edgeBand.minRowsPerTick, Math.min(edgeBand.maxRowsPerTick, rowsPerViewport - 1));
    if (positionPx < edgeBand.depthPx && positionPx >= 0) {
        return -rateAt(edgeBand.depthPx - positionPx, ceiling);
    }
    const fromTrailing = lengthPx - positionPx;
    if (fromTrailing < edgeBand.depthPx && fromTrailing >= 0) {
        return rateAt(edgeBand.depthPx - fromTrailing, ceiling);
    }
    return 0;
}

/** One row where the band is entered, the ceiling at the very edge, and a rising integer staircase
 * between (ADR-0008: faster the deeper in). */
function rateAt(depthPx: number, ceiling: number): number {
    const rate = edgeBand.minRowsPerTick
        + Math.floor(depthPx / edgeBand.depthPx * (edgeBand.maxRowsPerTick - edgeBand.minRowsPerTick));
    return Math.min(rate, ceiling);
}
