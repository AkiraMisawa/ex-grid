// What the selection layers hold (ADR-0008, ADR-0206), as the C# core's markup composes them: the pinned
// shade layer, emitted only while it holds a pinned tint; the pinned selection layer, each range's box and
// then the Focus; and the scrollable layer, every tint, then every box, then the Focus. No row learns the
// Selection: it is painted over them, so every row still skips its paint when the Selection moves
// (ADR-0003).

import type { ColumnGeometry } from '../columns/column-geometry';
import { box, focusStyle, tint, tinted, type OutlineCover } from '../paint/selection-styles';
import type { Selection } from '../selection/selection';
import type { RowRange } from '../viewport/row-range';

/** One rectangle of a layer: its classes and its inline style. */
export interface LayerPiece {
    readonly className: string;
    readonly style: string;
}

/** The three layers' rectangles; `shade` is undefined when the pinned shade layer holds nothing. */
export interface SelectionLayers {
    readonly shade: readonly LayerPiece[] | undefined;
    readonly pinned: readonly LayerPiece[];
    readonly scrollable: readonly LayerPiece[];
}

const pieceOf = (className: string, style: string | undefined): readonly LayerPiece[] =>
    (style === undefined ? [] : [{ className, style }]);

const nothing: SelectionLayers = Object.freeze({ shade: undefined, pinned: Object.freeze([]), scrollable: Object.freeze([]) });

/** The rectangles the layers hold over the painted rows `painted`. */
export function selectionLayers(
    selection: Selection, columns: ColumnGeometry, rowHeightPx: number, painted: RowRange, cover: OutlineCover,
): SelectionLayers {
    const focus = selection.focus;
    if (focus === undefined) return nothing;
    const { ranges } = selection;
    // A single range is outlined; among several the Focus is marked instead, where no range carries an
    // outline (ADR-0008, 2026-09-29 and 2026-10-12). Decided by the Selection, not the layer.
    const rangeClass = ranges.length === 1 ? 'ex-range ex-range-single' : 'ex-range';
    const focusClass = ranges.length > 1 ? 'ex-focus ex-focus-among-ranges' : 'ex-focus';
    const shadeHolds = columns.pinnedCount > 0 && ranges.some(range => tinted(range, focus, columns, painted, true));
    const shade = shadeHolds
        ? ranges.flatMap(range => pieceOf('ex-range-tint', tint(range, focus, columns, rowHeightPx, painted, true)))
        : undefined;
    const pinned = [
        ...ranges.flatMap(range => pieceOf(rangeClass, box(range, focus, columns, rowHeightPx, painted, true, cover))),
        ...pieceOf(focusClass, focusStyle(focus, columns, rowHeightPx, painted, true, cover)),
    ];
    const scrollable = [
        ...ranges.flatMap(range => pieceOf('ex-range-tint', tint(range, focus, columns, rowHeightPx, painted, false))),
        ...ranges.flatMap(range => pieceOf(rangeClass, box(range, focus, columns, rowHeightPx, painted, false, cover))),
        ...pieceOf(focusClass, focusStyle(focus, columns, rowHeightPx, painted, false, cover)),
    ];
    return { shade, pinned, scrollable };
}
