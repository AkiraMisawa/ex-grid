// Where the Cell Editor and the Keyboard Field stand, and which keys open an edit (ADR-0010, ADR-0080): the
// C# core's arithmetic for both boxes, with nothing of the view's state. The boxes are the cell's own, from
// the same arithmetic the selection layers read (ADR-0028), so the editor stands exactly over its cell and the
// Keyboard Field where an IME's composition is drawn.

import type { ColumnGeometry } from '../columns/column-geometry';
import { cellRange, pinned, scrollable } from '../paint/selection-styles';
import type { CellPosition } from '../selection/cell';
import type { RowRange } from '../viewport/row-range';

/** A box over one cell: its inline style, and whether it rides the pinned layer's sticky anchor. */
export interface CellBox {
    readonly style: string;
    readonly pinned: boolean;
}

/** The style of the Keyboard Field while it waits, with the Focus not painted (ADR-0080). */
const waitingStyle = 'left: 0px; top: 0px; width: 0px; height: 0px';

/**
 * Whether a key pressed with no edit open would open one (ADR-0010): F2, or a character typed. Not a chord,
 * except AltGr, which reports Control and Alt together and is how several layouts type @ { [ €: either
 * modifier alone is a shortcut and stays the browser's. Meta is never typing.
 */
export function opensAnEdit(key: string, ctrl: boolean, alt: boolean, meta: boolean): boolean {
    return !meta && ctrl === alt && ((key === 'F2' && !ctrl) || key.length === 1);
}

/** The cell's box in the layer its column belongs to: the pinned one over a Pinned Column, the scrollable one
 * otherwise. `undefined` while the cell lies outside the painted rows and the row beyond each end. */
export function cellBox(at: CellPosition, columns: ColumnGeometry, rowHeightPx: number, painted: RowRange): CellBox | undefined {
    const range = cellRange(at);
    if (at.column < columns.pinnedCount) {
        const style = pinned(range, columns, rowHeightPx, painted);
        return style === undefined ? undefined : { style, pinned: true };
    }
    const style = scrollable(range, columns, rowHeightPx, painted);
    return style === undefined ? undefined : { style, pinned: false };
}

/**
 * Where the Keyboard Field stands (ADR-0080): over `at`, the Focus or, with none, the first painted cell, in
 * the Cell Editor's box; and, with that cell not painted, at the painted rows' top-left, held at the
 * Viewport's left edge, until a composition reveals the Focus.
 */
export function keyFieldBox(at: CellPosition | undefined, columns: ColumnGeometry, rowHeightPx: number, painted: RowRange | undefined): CellBox {
    const box = at === undefined || painted === undefined ? undefined : cellBox(at, columns, rowHeightPx, painted);
    return box ?? { style: waitingStyle, pinned: true };
}

/** Whether the row the editor stands at lies outside the painted rows and the row beyond each end: the grid
 * does not scroll to follow it, and the editor stays open there, unseen (ADR-0011's note of 2026-10-07). */
export function editorAway(at: CellPosition, painted: RowRange): boolean {
    return at.row < painted.start - 1 || at.row > painted.start + painted.count;
}
