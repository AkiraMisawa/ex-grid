// Where a selected rectangle is painted, written as an inline style (ADR-0008): the C# core's
// SelectionStyles. A tint and a box per range in each layer it reaches (ADR-0206), so what this costs
// depends on how many rectangles there are and never on how many cells they cover — which is the whole
// reason selection is an overlay and not a class on every selected cell.
//
// Coordinates are relative to the painted slice rather than to the content, because the overlay lives
// inside the row Viewport: that element carries the scroll translation, and being inside it is what lets
// a rectangle pass under the Pinned Columns instead of over them.
//
// A rectangle is clipped to the painted rows, one row beyond each end (ADR-0053): a whole-column
// selection is a million rows tall, and a browser clamps a layout length under its Layout Ceiling. The
// extra row on each side keeps a clipped edge out of the readable area.
//
// A range spanning the pinned boundary is painted in two layers (ADR-0004): its pinned part travels with
// the Pinned Columns while the rest pans underneath. The fill handle stands in whichever layer its corner
// does (ADR-0050, item 5).

import type { ColumnGeometry } from '../columns/column-geometry';
import { itemAt, unreachable } from '../must';
import type { CellPosition } from '../selection/cell';
import type { FillHandleCorner } from '../selection/fill-rules';
import { bottomRow, rangeCellCount, rangeContains, rightColumn, selectionRange, type SelectionRange } from '../selection/range';
import type { RowRange } from '../viewport/row-range';

/**
 * What lies above the selection layer at its edges at the moment (ADR-0008, 2026-10-01): `firstOpenRow`
 * is the first row whose top edge lies below the top of the readable area, `scrollLeftPx` the horizontal
 * scroll offset, which takes a scrollable column's left edge under the Row Headings or the pinned block,
 * and `hasHeader` whether a Heading Mark can replace the covered top side.
 */
export interface OutlineCover {
    readonly firstOpenRow: number;
    readonly scrollLeftPx: number;
    readonly hasHeader: boolean;
}

/**
 * Where the readable area begins, for a fill handle held at its edge (ADR-0050, item 5): `topPx` is its top in
 * the selection layers' coordinates, measured from the painted slice's top, so the part of the first painted
 * row under the header; and `leftPx` its left in the scrollable layer's, the content's, which is the horizontal
 * scroll offset plus the Row Headings and the pinned block that cover the edge.
 */
export interface ReadableEdge {
    readonly topPx: number;
    readonly leftPx: number;
}

// The sides of the Selection's outline that stay inside the range, as the stylesheet reads them
// (ex-grid.css): written once, and none when every side lies outside, as Excel's does.
const outlineSideStyles: readonly string[] = Object.freeze(Array.from({ length: 16 }, (_, flags) =>
    ((flags & 1) === 0 ? '' : '; --ex-outline-in-t: 1')
    + ((flags & 2) === 0 ? '' : '; --ex-outline-in-l: 1')
    + ((flags & 4) === 0 ? '' : '; --ex-outline-heading-t: 1')
    + ((flags & 8) === 0 ? '' : '; --ex-outline-heading-l: 1')));

// A pixel's hundredth: a row's top edge or a column's left edge this close to what covers it is flush
// with it.
const flushPx = 0.01;

/** The columns of a rectangle on one side of the pinned boundary. */
interface Side {
    readonly first: number;
    readonly last: number;
}

/** The rows of a rectangle that are painted. */
interface ClippedRows {
    readonly top: number;
    readonly count: number;
}

/** The Focus as a rectangle, so one cell and a block are the same arithmetic. */
export function cellRange(cell: CellPosition): SelectionRange {
    return selectionRange(cell.row, cell.column, 1, 1);
}

/**
 * A selected range's box as one layer paints it (ADR-0008; ADR-0206): the element that keeps the class
 * `ex-range` and draws the outline, or `undefined` when the layer holds no part of the range, and for a
 * range that is the Focus's cell alone, which the Focus outline marks. A range that spans the pinned
 * boundary has its box drawn whole in both layers, each clipped to its own side, so the two parts meet
 * with no seam. The clip lets a row's height past the range's edges on every side but the boundary, more
 * than any outline is wide.
 */
export function box(
    range: SelectionRange, focus: CellPosition, columns: ColumnGeometry, rowHeightPx: number, painted: RowRange,
    pinnedLayer: boolean, cover: OutlineCover,
): string | undefined {
    if (rangeCellCount(range) === 1 && rangeContains(range, focus)) return undefined;
    const style = whole(range, columns, rowHeightPx, painted, pinnedLayer, rowHeightPx);
    const side = sideOf(range, columns, pinnedLayer);
    if (style === undefined || side === undefined) return undefined;
    return style + outlineSides(range.topRow, side.first, columns, pinnedLayer, cover, true);
}

/**
 * A selected range's tint as one layer paints it (ADR-0206): its part on the layer's side of the pinned
 * boundary, cut at the boundary, or `undefined` when the layer holds no part of it, or when that part is
 * the Focus's cell alone. A range holding the Focus carries a hole where the Focus is, in the layer the
 * Focus's cell belongs to, as the polygon the stylesheet clips the tint with; it is left out while the
 * Focus's row is not among the painted rows.
 */
export function tint(
    range: SelectionRange, focus: CellPosition, columns: ColumnGeometry, rowHeightPx: number, painted: RowRange,
    pinnedLayer: boolean,
): string | undefined {
    const side = sideOf(range, columns, pinnedLayer);
    if (side === undefined || focusAlone(range, side, focus)) return undefined;
    const rows = clip(range, painted);
    if (rows === undefined) return undefined;
    const leftPx = columns.offsetPxOf(side.first);
    let style = rect(leftPx, columns.offsetPxOf(side.last + 1) - leftPx, rows, rowHeightPx, painted.start);
    if (rangeContains(range, focus) && sideOf(cellRange(focus), columns, pinnedLayer) !== undefined
        && focus.row >= rows.top && focus.row < rows.top + rows.count) {
        const holeLeftPx = columns.offsetPxOf(focus.column) - leftPx;
        const holeRightPx = columns.offsetPxOf(focus.column + 1) - leftPx;
        const holeTopPx = (focus.row - rows.top) * rowHeightPx;
        const holeBottomPx = holeTopPx + rowHeightPx;
        // evenodd: the box, then the Focus's cell inside it, is the box less the cell.
        style += `; --ex-range-hole: polygon(evenodd, 0 0, 100% 0, 100% 100%, 0 100%, 0 0, ${holeLeftPx}px ${holeTopPx}px, `
            + `${holeRightPx}px ${holeTopPx}px, ${holeRightPx}px ${holeBottomPx}px, ${holeLeftPx}px ${holeBottomPx}px, ${holeLeftPx}px ${holeTopPx}px)`;
    }
    return style;
}

/** Whether one layer paints a tint for a selected range, exactly when {@link tint} answers one: what
 * decides whether the pinned shade layer is emitted at all (ADR-0206). */
export function tinted(range: SelectionRange, focus: CellPosition, columns: ColumnGeometry, painted: RowRange, pinnedLayer: boolean): boolean {
    const side = sideOf(range, columns, pinnedLayer);
    return side !== undefined && !focusAlone(range, side, focus) && clip(range, painted) !== undefined;
}

/** Whether a rectangle has a part in one layer among the painted rows. */
export function reaches(range: SelectionRange, columns: ColumnGeometry, painted: RowRange, pinnedLayer: boolean): boolean {
    return sideOf(range, columns, pinnedLayer) !== undefined && clip(range, painted) !== undefined;
}

/**
 * A rectangle painted whole in one layer and clipped to that layer's side of the pinned boundary, or
 * `undefined` when the layer holds no part of it: how a selected range's box is painted, and how a
 * Reference Outline is (ADR-0057). The clip lets what is drawn past the edges show for `outsidePx` on
 * every side but the boundary: zero for what is drawn inside the edge.
 */
export function whole(
    range: SelectionRange, columns: ColumnGeometry, rowHeightPx: number, painted: RowRange, pinnedLayer: boolean,
    outsidePx = 0,
): string | undefined {
    const rows = clip(range, painted);
    if (sideOf(range, columns, pinnedLayer) === undefined || rows === undefined) return undefined;
    const leftPx = columns.offsetPxOf(range.leftColumn);
    const rightPx = columns.offsetPxOf(rightColumn(range) + 1);
    let style = rect(leftPx, rightPx - leftPx, rows, rowHeightPx, painted.start);
    if (sideOf(range, columns, !pinnedLayer) !== undefined) {
        const boundaryPx = columns.offsetPxOf(columns.pinnedCount);
        const o = outsidePx > 0 ? `${-outsidePx}px` : '0';
        style += pinnedLayer
            ? `; clip-path: inset(${o} ${rightPx - boundaryPx}px ${o} ${o})`
            : `; clip-path: inset(${o} ${o} ${o} ${boundaryPx - leftPx}px)`;
    }
    return style;
}

/** A rectangle of the Copied Range as one layer paints it (ADR-0170): whole and clipped to the layer's
 * side, as a selected range is, with covered sides kept inside so the dashes stay visible. */
export function copied(
    range: SelectionRange, columns: ColumnGeometry, rowHeightPx: number, painted: RowRange, pinnedLayer: boolean, cover: OutlineCover,
): string | undefined {
    const style = whole(range, columns, rowHeightPx, painted, pinnedLayer, rowHeightPx);
    const side = sideOf(range, columns, pinnedLayer);
    if (style === undefined || side === undefined) return undefined;
    return style + outlineSides(range.topRow, side.first, columns, pinnedLayer, cover, false);
}

/** The Focus as one layer paints it: its cell, cut to the layer's side, with the sides of its outline that
 * stay inside the cell; `undefined` when the layer holds no part of it. */
export function focusStyle(
    focus: CellPosition, columns: ColumnGeometry, rowHeightPx: number, painted: RowRange, pinnedLayer: boolean, cover: OutlineCover,
): string | undefined {
    const style = part(cellRange(focus), columns, rowHeightPx, painted, pinnedLayer);
    if (style === undefined) return undefined;
    return style + outlineSides(focus.row, focus.column, columns, pinnedLayer, cover, true);
}

/** The part of a range that pans with the content, or `undefined` when it lies under the Pinned Columns. */
export function scrollable(range: SelectionRange, columns: ColumnGeometry, rowHeightPx: number, painted: RowRange): string | undefined {
    return part(range, columns, rowHeightPx, painted, false);
}

/** The part of a range held against the Viewport's left edge, or `undefined` when it reaches no Pinned Column. */
export function pinned(range: SelectionRange, columns: ColumnGeometry, rowHeightPx: number, painted: RowRange): string | undefined {
    return part(range, columns, rowHeightPx, painted, true);
}

/**
 * The fill handle (ADR-0050, item 5): a square of `sizePx` at the `corner` of the range `fillHandleCorner`
 * chose, in the layer that corner's cell belongs to, the pinned one when its column is pinned, the scrollable
 * one otherwise. `undefined` for the other layer, so the handle is one element and never two, and while that
 * cell is not among the painted rows (ADR-0053), where it could not be seen or grabbed; but a whole column's
 * first row scrolled above them leaves its handle held at the readable area's top, as Excel's stayed in view
 * with row 1 scrolled away. Where it lies about the corner is `handleCentrePx`'s.
 */
export function handle(
    range: SelectionRange, corner: FillHandleCorner, columns: ColumnGeometry, rowHeightPx: number, painted: RowRange,
    sizePx: number, pinnedLayer: boolean, readable: ReadableEdge,
): string | undefined {
    const cell = handleCell(range, corner);
    if (sideOf(cellRange(cell), columns, pinnedLayer) === undefined) return undefined;
    const heldAtTheTop = corner === 'topRight';
    if ((!heldAtTheTop && cell.row < painted.start - 1) || cell.row > painted.start + painted.count) return undefined;
    const [leftPx, topPx] = handleCentrePx(range, corner, columns, rowHeightPx, painted.start, sizePx, readable);
    return `left: ${leftPx - (sizePx / 2)}px; top: ${topPx - (sizePx / 2)}px; width: ${sizePx}px; height: ${sizePx}px`;
}

/** The cell whose corner the handle stands at: the range's last row and last column, its first row and last
 * column for whole columns, its last row and first column for whole rows. Its column decides the layer, and its
 * row whether the handle is painted. */
export function handleCell(range: SelectionRange, corner: FillHandleCorner): CellPosition {
    switch (corner) {
        case 'topRight': return Object.freeze({ row: range.topRow, column: rightColumn(range) });
        case 'bottomLeft': return Object.freeze({ row: bottomRow(range), column: range.leftColumn });
        case 'bottomRight': return Object.freeze({ row: bottomRow(range), column: rightColumn(range) });
        default: return unreachable(corner);
    }
}

/**
 * The handle's centre, `[left, top]`, in the coordinates its layer paints in: the painted slice's top, and the
 * content's (or the pinned layer's) left. At the bottom-right it is centred on the range's corner, the
 * outline's outer corner (ADR-0008). At the top-right of whole columns and the bottom-left of whole rows it is
 * centred on the edge that runs along the range and lies wholly inside the range across the other, so the
 * header and the Row Headings, which stand above the selection layer, cover none of it. Held at the readable
 * area's edge: a whole column's top never lies above the readable area's top, and a whole row's left in the
 * scrollable layer never lies left of the Row Headings' and the pinned block's edge.
 */
export function handleCentrePx(
    range: SelectionRange, corner: FillHandleCorner, columns: ColumnGeometry, rowHeightPx: number, firstPaintedRow: number,
    sizePx: number, readable: ReadableEdge,
): readonly [number, number] {
    const half = sizePx / 2;
    switch (corner) {
        case 'topRight':
            return [columns.offsetPxOf(rightColumn(range) + 1), Math.max((range.topRow - firstPaintedRow) * rowHeightPx, readable.topPx) + half];
        case 'bottomLeft':
            return [
                (range.leftColumn < columns.pinnedCount
                    ? columns.offsetPxOf(range.leftColumn)
                    : Math.max(columns.offsetPxOf(range.leftColumn), readable.leftPx)) + half,
                (bottomRow(range) + 1 - firstPaintedRow) * rowHeightPx,
            ];
        case 'bottomRight':
            return [columns.offsetPxOf(rightColumn(range) + 1), (bottomRow(range) + 1 - firstPaintedRow) * rowHeightPx];
        default:
            return unreachable(corner);
    }
}

// The sides that need their outline inside (ADR-0008, D2, 2026-10-14). A Heading Mark replaces the side
// hidden beneath a header or Row Headings; a pinned block's boundary and an unheaded grid edge have no
// replacing mark, so their outline stays inside. Copied Range dashes have no replacing mark.
function outlineSides(topRow: number, firstColumn: number, columns: ColumnGeometry, pinnedLayer: boolean, cover: OutlineCover, headingMark: boolean): string {
    const top = topRow < cover.firstOpenRow;
    const left = pinnedLayer
        ? columns.offsetPxOf(firstColumn) <= columns.leadWidthPx + flushPx
        : columns.offsetPxOf(firstColumn) - cover.scrollLeftPx <= columns.pinnedWidthPx + flushPx;
    const headingTop = top && headingMark && cover.hasHeader;
    const headingLeft = left && headingMark && columns.leadWidthPx > 0 && (pinnedLayer || columns.pinnedCount === 0);
    const flags = (top && !headingTop ? 1 : 0) | (left && !headingLeft ? 2 : 0) | (headingTop ? 4 : 0) | (headingLeft ? 8 : 0);
    return itemAt(outlineSideStyles, flags);
}

// Whether a range's part on one side of the boundary is the Focus's cell and nothing else.
function focusAlone(range: SelectionRange, side: Side, focus: CellPosition): boolean {
    return range.rowCount === 1 && side.first === side.last && focus.row === range.topRow && focus.column === side.first;
}

// A rectangle's part on one side of the pinned boundary, cut at it.
function part(range: SelectionRange, columns: ColumnGeometry, rowHeightPx: number, painted: RowRange, pinnedLayer: boolean): string | undefined {
    const side = sideOf(range, columns, pinnedLayer);
    const rows = clip(range, painted);
    if (side === undefined || rows === undefined) return undefined;
    return rect(columns.offsetPxOf(side.first), columns.offsetPxOf(side.last + 1) - columns.offsetPxOf(side.first), rows, rowHeightPx, painted.start);
}

// The columns of a rectangle on one side of the pinned boundary, or none there: the one place the split
// is decided.
function sideOf(range: SelectionRange, columns: ColumnGeometry, pinnedLayer: boolean): Side | undefined {
    const first = pinnedLayer ? range.leftColumn : Math.max(range.leftColumn, columns.pinnedCount);
    const last = pinnedLayer ? Math.min(rightColumn(range), columns.pinnedCount - 1) : rightColumn(range);
    return first > last ? undefined : { first, last };
}

// The rows of a range that are painted, one row beyond each end of the painted slice (ADR-0053).
function clip(range: SelectionRange, painted: RowRange): ClippedRows | undefined {
    const top = Math.max(range.topRow, painted.start - 1);
    const bottom = Math.min(bottomRow(range), painted.start + painted.count);
    return bottom < top ? undefined : { top, count: bottom - top + 1 };
}

function rect(leftPx: number, widthPx: number, rows: ClippedRows, rowHeightPx: number, firstPaintedRow: number): string {
    const topPx = (rows.top - firstPaintedRow) * rowHeightPx;
    return `left: ${leftPx}px; top: ${topPx}px; width: ${widthPx}px; height: ${rows.count * rowHeightPx}px`;
}
