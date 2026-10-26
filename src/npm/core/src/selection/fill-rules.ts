// Where a fill-handle drag reaches, and whether it may be raised (ADR-0050, item 5): the C# core's FillRules.
// The gesture is the core's, the meaning is the Consumer's. Pure and stateless: the grid holds the drag, and
// this answers what it has reached.
//
// The only shape is one rectangle extended along one axis. A disjoint Selection has no handle, as in Excel.
// The axis is the one the pointer has travelled further along past the source, counted in cells; a tie goes
// down or up, Excel's usual fill. A pointer back inside the source reaches nothing, and a release there raises
// nothing.

import type { CellPosition, GridDirection, GridExtent } from './cell';
import { bottomRow, rightColumn, selectionRange, spansEveryColumn, spansEveryRow, type SelectionRange } from './range';
import type { GridSelection } from './selection';

/**
 * The corner of its range the fill handle stands at (ADR-0050, item 5): the bottom-right, but for a range of
 * whole columns (`topRight`, beside the column's Heading) or whole rows (`bottomLeft`, beside the row's
 * Heading), whose bottom-right lies where nobody can reach it.
 */
export type FillHandleCorner = 'bottomRight' | 'topRight' | 'bottomLeft';

/** What a fill-handle drag has reached: the cells a release would fill, which never include the source, and
 * the direction they lie in from it. */
export interface FillExtension {
    /** The cells to fill, adjacent to the source along one axis and as wide (or as tall) as it. */
    readonly target: SelectionRange;
    /** Which way from the source the target lies. */
    readonly direction: GridDirection;
}

/** A fill-handle release approved with its extension, or refused whole over the first target column that is
 * not Editable (ADR-0035), the column the words name (ADR-0203). */
export type FillDecision =
    | { readonly refused: false; readonly extension: FillExtension }
    | { readonly refused: true; readonly reason: 'targetNotEditable'; readonly column: number };

/** The range the handle stands on, or `undefined` when there is none: the Selection's one range. A disjoint
 * Selection shows no handle (ADR-0050, item 5). */
export function fillHandleRange(selection: GridSelection): SelectionRange | undefined {
    const { ranges } = selection;
    return ranges.length === 1 ? ranges[0] : undefined;
}

/**
 * Which corner of `range` the handle stands at, or `undefined` where there is none (ADR-0050, item 5;
 * 2026-10-12, decided with the user: the handle on a whole axis follows Excel). Whole columns put it at the
 * first row's top-right, beside the column's Heading; whole rows at the first column's bottom-left, beside
 * the row's Heading. A range spanning both, every cell, has none: there is nowhere left to fill to. Every
 * other range keeps its bottom-right corner.
 */
export function fillHandleCorner(range: SelectionRange, extent: GridExtent): FillHandleCorner | undefined {
    const everyRow = spansEveryRow(range, extent);
    const everyColumn = spansEveryColumn(range, extent);
    if (everyRow && everyColumn) return undefined;
    if (everyRow) return 'topRight';
    return everyColumn ? 'bottomLeft' : 'bottomRight';
}

/** What a drag from `source`'s handle has reached with the pointer over `pointer`: the cells a release would
 * fill, never the source itself, and the direction; `undefined` while the pointer is inside the source. */
export function fillExtensionFor(source: SelectionRange, pointer: CellPosition): FillExtension | undefined {
    const rows = beyond(pointer.row, source.topRow, bottomRow(source));
    const columns = beyond(pointer.column, source.leftColumn, rightColumn(source));
    if (rows === 0 && columns === 0) return undefined;
    // One axis only, the one the pointer went further along (ADR-0050); a tie is the vertical fill, the one
    // Excel users make most.
    if (Math.abs(rows) >= Math.abs(columns)) {
        return rows > 0
            ? Object.freeze({ target: selectionRange(bottomRow(source) + 1, source.leftColumn, rows, source.columnCount), direction: 'down' })
            : Object.freeze({ target: selectionRange(pointer.row, source.leftColumn, -rows, source.columnCount), direction: 'up' });
    }
    return columns > 0
        ? Object.freeze({ target: selectionRange(source.topRow, rightColumn(source) + 1, source.rowCount, columns), direction: 'right' })
        : Object.freeze({ target: selectionRange(source.topRow, pointer.column, source.rowCount, -columns), direction: 'left' });
}

/** The release (ADR-0050, item 5): `undefined` when the pointer is inside the source and nothing is filled;
 * otherwise approved with the extension, or refused whole when the target covers a column that is not
 * Editable. Paste and fill go through one gate, judged before anything is raised (ADR-0035). */
export function planHandleFill(source: SelectionRange, pointer: CellPosition, columnIsEditable: (column: number) => boolean): FillDecision | undefined {
    const extension = fillExtensionFor(source, pointer);
    if (extension === undefined) return undefined;
    const { target } = extension;
    for (let column = target.leftColumn; column <= rightColumn(target); column++) {
        if (!columnIsEditable(column)) return { refused: true, reason: 'targetNotEditable', column };
    }
    return { refused: false, extension };
}

// How far past first..last a position lies: positive after, negative before, zero inside.
function beyond(position: number, first: number, last: number): number {
    if (position > last) return position - last;
    return position < first ? position - first : 0;
}
