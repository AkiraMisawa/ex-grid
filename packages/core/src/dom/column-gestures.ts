// The header's two drags (ADR-0011, ADR-0016, ADR-0032): a resize from a column's grip, applied on release and
// never live, and a reorder of the unit grabbed, a leaf or a Header Group's rectangle, dropped only at a
// boundary that keeps every group whole and the pinned block where it is. What a pixel means is here; the
// listeners and the elements are the grid's.

import type { ColumnGeometry } from '../columns/column-geometry';
import type { HeaderGroupLayout, ResolvedHeaderGroup } from '../columns/header-groups';
import { allowedBoundaries } from '../columns/header-reorder';
import { itemAt } from '../must';

/** What `onColumnWidthChanged` hears (ADR-0016): one column's new width, as the user asked for it. The grid
 * changes nothing; the width moves when the Consumer hands back the column declared at it, Fixed and with the
 * bounds it had: `{ px: widthPx, minPx, maxPx }`. */
export interface GridColumnWidthChange {
    /** The column's `name`. */
    readonly column: string;
    /** The width, in CSS pixels: the declared width moved by the drag, bounded below by the column's
     * MinWidth and never above, since a MaxWidth bounds only what the grid computes. */
    readonly widthPx: number;
}

/** How far a header press travels before it is a drag, a resize or a reorder, rather than a click
 * (ADR-0011, ADR-0016). */
export const dragThresholdPx = 4;

/** A drag on a column's grip: the column by name, so columns handed over meanwhile cannot move it onto another. */
export interface ResizeGesture {
    readonly kind: 'resize';
    readonly name: string;
    readonly startClientX: number;
    readonly startWidthPx: number;
}

/** A press on the header while a reorder is heard: the unit it lifted, by position against the order it was
 * taken in. It becomes a reorder only past the threshold. */
export interface ReorderGesture {
    readonly kind: 'reorder';
    readonly names: readonly string[];
    readonly unitFirst: number;
    readonly unitCount: number;
    readonly startClientX: number;
    readonly startContentX: number;
    readonly reordering: boolean;
    readonly boundary: number | undefined;
}

export type ColumnGesture = ResizeGesture | ReorderGesture;

/** Where the resize guide stands for `column` at `widthPx`, in content space: a pinned column's edge rides
 * the scroll, as the column does. */
export function guideLeftPx(geometry: ColumnGeometry, column: number, widthPx: number, scrollLeftPx: number): number {
    return (column < geometry.pinnedCount ? scrollLeftPx : 0) + geometry.offsetPxOf(column) + widthPx;
}

/** Where the drop indicator stands for `boundary`, in content space. */
export function boundaryLeftPx(geometry: ColumnGeometry, boundary: number, unitFirst: number, scrollLeftPx: number): number {
    const pinned = geometry.pinnedCount;
    const rides = boundary <= pinned && pinned > 0 && unitFirst < pinned;
    return (rides ? scrollLeftPx : 0) + (boundary >= geometry.count ? geometry.totalWidthPx : geometry.offsetPxOf(boundary));
}

/** The boundaries the unit may drop at. The pinned boundary is the View State's, suspended or not: a drop
 * across it would change what is pinned once the box widens (ADR-0011, ADR-0045). */
export function dropBoundaries(geometry: ColumnGeometry, layout: HeaderGroupLayout, unitFirst: number, unitCount: number): readonly number[] {
    return allowedBoundaries({ layout, columnCount: geometry.count, pinnedCount: geometry.requestedPinnedCount, unitFirst, unitCount });
}

/** The boundary of `boundaries` nearest `contentX`. */
export function nearestBoundary(geometry: ColumnGeometry, boundaries: readonly number[], unitFirst: number, contentX: number,
    scrollLeftPx: number): number {
    let best = itemAt(boundaries, 0);
    let bestDistance = Number.POSITIVE_INFINITY;
    for (const boundary of boundaries) {
        const distance = Math.abs(boundaryLeftPx(geometry, boundary, unitFirst, scrollLeftPx) - contentX);
        if (distance < bestDistance) {
            bestDistance = distance;
            best = boundary;
        }
    }
    return best;
}

/** What a press at `offsetY` above `column` grabs (ADR-0032): `'leaf'` on the column's own header, the Header
 * Group standing there in a tier above it, or nothing over an empty tier above a leaf that does not stretch
 * there. */
export function grabbedAt(layout: HeaderGroupLayout, column: number, offsetY: number, bandHeightPx: number,
    tierPx: number): 'leaf' | ResolvedHeaderGroup | undefined {
    if (layout.tierCount === 0 || offsetY >= bandHeightPx - (layout.leafTierSpanOf(column) * tierPx)) return 'leaf';
    const tier = layout.tierCount - Math.floor(offsetY / tierPx);
    return layout.groups.find(group => column >= group.firstColumn && column < group.firstColumn + group.memberCount
        && tier >= group.bottomTier && tier <= group.topTier);
}

/** The columns a gesture on `grabbed`'s grip acts on: every column a whole-column range covers when the
 * grabbed one is among them, and the grabbed one alone otherwise (ADR-0016, FN-12c). */
export function columnsResizedWith(wholeColumns: readonly number[], grabbed: number): readonly number[] {
    return wholeColumns.includes(grabbed) ? wholeColumns : [grabbed];
}
