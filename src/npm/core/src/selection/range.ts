import type { CellPosition, GridExtent } from './cell';

/**
 * One selected rectangle in position space, with normalised bounds (ADR-0011). Orientation lives on
 * the selection's Focus and Extent, not here: a rectangle with a direction would duplicate that state
 * and admit invalid values. A range covers at least one cell; "nothing selected" is an empty list of
 * ranges.
 * @public
 */
export interface SelectionRange {
    /** The first row, as a position in the current order. */
    readonly topRow: number;
    /** The first visible column's index. */
    readonly leftColumn: number;
    /** How many rows the rectangle covers; at least one. */
    readonly rowCount: number;
    /** How many columns the rectangle covers; at least one. */
    readonly columnCount: number;
}

/**
 * A rectangle from its top-left cell and its size. Positions are whole and non-negative, and each count
 * is at least one; anything else is refused with a RangeError rather than normalised away (ADR-0011).
 */
export function selectionRange(topRow: number, leftColumn: number, rowCount: number, columnCount: number): SelectionRange {
    requireWhole('topRow', topRow, 0);
    requireWhole('leftColumn', leftColumn, 0);
    requireWhole('rowCount', rowCount, 1);
    requireWhole('columnCount', columnCount, 1);
    return Object.freeze({ topRow, leftColumn, rowCount, columnCount });
}

function requireWhole(name: string, value: number, least: number): void {
    if (!Number.isSafeInteger(value) || value < least) {
        throw new RangeError(`${name} is ${value}; it must be a whole number of at least ${least} (ADR-0011).`);
    }
}

/** The last row, inclusive. */
export function bottomRow(range: SelectionRange): number {
    return range.topRow + range.rowCount - 1;
}

/** The last column, inclusive. */
export function rightColumn(range: SelectionRange): number {
    return range.leftColumn + range.columnCount - 1;
}

/** The area. It needs no data, which lets the status display count any selection (ADR-0014). */
export function rangeCellCount(range: SelectionRange): number {
    return range.rowCount * range.columnCount;
}

/** Whether two ranges are the same rectangle. */
export function sameRange(a: SelectionRange, b: SelectionRange): boolean {
    return a.topRow === b.topRow && a.leftColumn === b.leftColumn
        && a.rowCount === b.rowCount && a.columnCount === b.columnCount;
}

/** Whether the range is whole columns: it spans every row of the extent (ADR-0012, ADR-0016). */
export function spansEveryRow(range: SelectionRange, extent: GridExtent): boolean {
    return range.topRow === 0 && range.rowCount === extent.rowCount;
}

/** Whether the range is whole rows: it spans every column of the extent (ADR-0012). */
export function spansEveryColumn(range: SelectionRange, extent: GridExtent): boolean {
    return range.leftColumn === 0 && range.columnCount === extent.columnCount;
}

/**
 * The rectangle between any pair of opposite corners. Shrinking a range back through the Focus flips
 * it around the Focus for free (ADR-0052).
 */
export function fromCorners(a: CellPosition, b: CellPosition): SelectionRange {
    return selectionRange(
        Math.min(a.row, b.row),
        Math.min(a.column, b.column),
        Math.abs(a.row - b.row) + 1,
        Math.abs(a.column - b.column) + 1);
}

/** The smallest rectangle holding both ranges: a fill's source and its target together (ADR-0050, item 5). */
export function boundingRange(a: SelectionRange, b: SelectionRange): SelectionRange {
    return fromCorners(
        { row: Math.min(a.topRow, b.topRow), column: Math.min(a.leftColumn, b.leftColumn) },
        { row: Math.max(bottomRow(a), bottomRow(b)), column: Math.max(rightColumn(a), rightColumn(b)) });
}

/** Whether the cell lies inside the rectangle, every bound inclusive. */
export function rangeContains(range: SelectionRange, position: CellPosition): boolean {
    return position.row >= range.topRow && position.row <= bottomRow(range)
        && position.column >= range.leftColumn && position.column <= rightColumn(range);
}

/**
 * Removes one cell, yielding the up to four rectangles that remain: the Ctrl+click take-out
 * (ADR-0012). A cell outside the range leaves it as it was; the only cell of a 1×1 range leaves
 * nothing. The pieces come bottom to top, in the order Excel's `Selection.Address` lists them: the
 * full-width band below the cell's row, what remains of that row to its right, then to its left, then
 * the band above (ADR-0052, "What the third run settled").
 */
export function subtractCell(range: SelectionRange, position: CellPosition): readonly SelectionRange[] {
    return rangeContains(range, position)
        ? subtractArea(range, selectionRange(position.row, position.column, 1, 1))
        : [range];
}

/**
 * Removes the part of the range that the area covers, yielding the up to four rectangles that remain,
 * in the order a cell's do (subtractCell). A Ctrl+click on a wholly selected Heading takes out a whole
 * column or row this way (ADR-0050, item 1). An area that misses the range leaves it as it was; one
 * that covers it leaves nothing.
 */
export function subtractArea(range: SelectionRange, area: SelectionRange): readonly SelectionRange[] {
    const bottom = bottomRow(range);
    const right = rightColumn(range);
    const top = Math.max(range.topRow, area.topRow);
    const last = Math.min(bottom, bottomRow(area));
    const left = Math.max(range.leftColumn, area.leftColumn);
    const end = Math.min(right, rightColumn(area));
    if (top > last || left > end) {
        return [range];
    }

    const pieces: SelectionRange[] = [];
    if (last < bottom) {
        pieces.push(selectionRange(last + 1, range.leftColumn, bottom - last, range.columnCount));
    }
    if (end < right) {
        pieces.push(selectionRange(top, end + 1, last - top + 1, right - end));
    }
    if (left > range.leftColumn) {
        pieces.push(selectionRange(top, range.leftColumn, last - top + 1, left - range.leftColumn));
    }
    if (top > range.topRow) {
        pieces.push(selectionRange(range.topRow, range.leftColumn, top - range.topRow, range.columnCount));
    }
    return pieces;
}
