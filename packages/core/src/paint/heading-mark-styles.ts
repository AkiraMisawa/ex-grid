// Where the Heading Mark is painted, written as an inline style (ADR-0008, 2026-10-13): one rectangle per
// run of marked Headings (selection/heading-marks.ts), the Headings' own box, on whose edge toward the
// cells the stylesheet draws the line. Geometry only: the line's width and colour are the stylesheet's, so
// nothing here reads either (ADR-0027/0028). The C# core's HeadingMarkStyles.
//
// A column's mark stands in the header band, at the band's last tier, where the leaves stand beneath any
// Header Group (ADR-0032); a Pinned Column's part rides a sticky layer of its own at the band's left, and
// the rest passes beneath the pinned header cells as the columns do (ADR-0004). A row's mark stands over
// the Row Headings in the painted slice's coordinates, as the selection layers do, and is cut to the
// painted rows, one row beyond each end (ADR-0053).

import type { ColumnGeometry, ColumnRange } from '../columns/column-geometry';
import type { RowRange } from '../viewport/row-range';

/**
 * A run of marked columns in one layer of the header band: its part on the layer's side of the pinned
 * boundary, cut at the boundary, or `undefined` when the layer holds none of it. `topPx` and `heightPx`
 * are the band's last tier.
 */
export function columnMark(run: ColumnRange, columns: ColumnGeometry, pinnedLayer: boolean, topPx: number, heightPx: number): string | undefined {
    let last = run.start + run.count - 1;
    if (last >= columns.count) return undefined;
    const first = pinnedLayer ? run.start : Math.max(run.start, columns.pinnedCount);
    last = pinnedLayer ? Math.min(last, columns.pinnedCount - 1) : last;
    if (first > last) return undefined;
    const leftPx = columns.offsetPxOf(first);
    return `left: ${leftPx}px; top: ${topPx}px; width: ${columns.offsetPxOf(last + 1) - leftPx}px; height: ${heightPx}px`;
}

/**
 * A run of marked rows over the Row Headings, `leadWidthPx` wide, in the coordinates of the painted slice
 * `painted`: cut to the painted rows, one row beyond each end, or `undefined` when it shares none of them
 * (ADR-0053).
 */
export function rowMark(run: RowRange, painted: RowRange, rowHeightPx: number, leadWidthPx: number): string | undefined {
    const top = Math.max(run.start, painted.start - 1);
    const bottom = Math.min(run.start + run.count - 1, painted.start + painted.count);
    if (bottom < top) return undefined;
    return `left: 0; top: ${(top - painted.start) * rowHeightPx}px; width: ${leadWidthPx}px; height: ${(bottom - top + 1) * rowHeightPx}px`;
}
