// What the selection layers hold (ADR-0008, ADR-0206), as the C# core's markup composes them: the pinned
// shade layer, emitted only while it holds a pinned band or tint — the hover band, the Focus band, then each
// tint; the pinned selection layer, each range's box, then the Focus, then the Copied Range's dashes; and the
// scrollable layer, the hover band, the Focus band, every tint, then every box, then the Focus, then the
// dashes, and last the fill handle and a fill drag's target outline (ADR-0050, item 5). No row learns the Selection: it is painted over them, so every row still skips its
// paint when the Selection moves (ADR-0003).

import type { ColumnGeometry } from '../columns/column-geometry';
import {
    box, copied as copiedStyle, focusStyle, handle as handleStyle, pinned as pinnedPart, reaches, scrollable as scrollablePart, tint, tinted,
    type OutlineCover, type ReadableEdge,
} from '../paint/selection-styles';
import type { FillHandleCorner } from '../selection/fill-rules';
import type { SelectionRange } from '../selection/range';
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

/** The row bands painted beneath the Selection (ADR-0008, ADR-0029): the row under the pointer, and whether
 * the Focus's row is banded. */
export interface RowBands {
    readonly hoverRow: number | undefined;
    readonly focusRow: boolean;
}

/** The fill handle where it is painted, and a running fill drag's outline, the source and what it has reached
 * as one rectangle (ADR-0050, item 5). */
export interface FillPaint {
    readonly outline: SelectionRange | undefined;
    readonly handle: {
        readonly range: SelectionRange;
        readonly corner: FillHandleCorner;
        readonly sizePx: number;
        readonly readable: ReadableEdge;
    } | undefined;
}

const noFill: FillPaint = Object.freeze({ outline: undefined, handle: undefined });

/** The rectangles the layers hold over the painted rows `painted`, the Copied Range's `copied` among them. */
export function selectionLayers(
    selection: Selection, copied: readonly SelectionRange[], columns: ColumnGeometry, rowHeightPx: number, painted: RowRange,
    cover: OutlineCover, bands: RowBands = { hoverRow: undefined, focusRow: false }, fill: FillPaint = noFill,
): SelectionLayers {
    // The Copied Range's dashes (ADR-0170) come after the Focus, so they lie over the Selection's outline
    // that a copy usually shares.
    const dashes = (pinnedLayer: boolean): readonly LayerPiece[] =>
        copied.flatMap(range => pieceOf('ex-copied-range', copiedStyle(range, columns, rowHeightPx, painted, pinnedLayer, cover)));
    // A band is a row across every column, one rectangle per layer, beneath every tint so each range stays
    // readable over it; no row learns of it (ADR-0008, ADR-0029).
    const bandOf = (row: number): SelectionRange => ({ topRow: row, leftColumn: 0, rowCount: 1, columnCount: columns.count });
    // The fill handle and its target outline, painted last so they stand over the ranges and the dashes; the
    // handle sits in whichever layer its corner does.
    const fillPieces = (pinnedLayer: boolean): readonly LayerPiece[] => [
        ...(fill.outline === undefined ? []
            : pieceOf('ex-fill-target', pinnedLayer ? pinnedPart(fill.outline, columns, rowHeightPx, painted) : scrollablePart(fill.outline, columns, rowHeightPx, painted))),
        ...(fill.handle === undefined ? []
            : pieceOf('ex-fill-handle', handleStyle(fill.handle.range, fill.handle.corner, columns, rowHeightPx, painted, fill.handle.sizePx, pinnedLayer, fill.handle.readable))),
    ];
    const focus = selection.focus;
    // With no column there is no row to band.
    const rowBands = columns.count === 0 ? [] : [
        ...(bands.hoverRow === undefined ? [] : [{ className: 'ex-hover-row', range: bandOf(bands.hoverRow) }]),
        ...(bands.focusRow && focus !== undefined ? [{ className: 'ex-focus-row', range: bandOf(focus.row) }] : []),
    ];
    const pinnedBands = rowBands.flatMap(({ className, range }) => pieceOf(className, pinnedPart(range, columns, rowHeightPx, painted)));
    const scrollableBands = rowBands.flatMap(({ className, range }) => pieceOf(className, scrollablePart(range, columns, rowHeightPx, painted)));
    const bandShade = columns.pinnedCount > 0 && rowBands.some(({ range }) => reaches(range, columns, painted, true));
    if (focus === undefined) {
        if (rowBands.length === 0 && copied.length === 0) return nothing;
        return {
            shade: bandShade ? pinnedBands : undefined,
            pinned: dashes(true),
            scrollable: [...scrollableBands, ...dashes(false)],
        };
    }
    const { ranges } = selection;
    // A single range is outlined; among several the Focus is marked instead, where no range carries an
    // outline (ADR-0008, 2026-09-29 and 2026-10-12). Decided by the Selection, not the layer.
    const rangeClass = ranges.length === 1 ? 'ex-range ex-range-single' : 'ex-range';
    const focusClass = ranges.length > 1 ? 'ex-focus ex-focus-among-ranges' : 'ex-focus';
    const shadeHolds = bandShade || (columns.pinnedCount > 0 && ranges.some(range => tinted(range, focus, columns, painted, true)));
    const shade = shadeHolds
        ? [...pinnedBands, ...ranges.flatMap(range => pieceOf('ex-range-tint', tint(range, focus, columns, rowHeightPx, painted, true)))]
        : undefined;
    const pinned = [
        ...ranges.flatMap(range => pieceOf(rangeClass, box(range, focus, columns, rowHeightPx, painted, true, cover))),
        ...pieceOf(focusClass, focusStyle(focus, columns, rowHeightPx, painted, true, cover)),
        ...dashes(true),
        ...fillPieces(true),
    ];
    const scrollable = [
        ...scrollableBands,
        ...ranges.flatMap(range => pieceOf('ex-range-tint', tint(range, focus, columns, rowHeightPx, painted, false))),
        ...ranges.flatMap(range => pieceOf(rangeClass, box(range, focus, columns, rowHeightPx, painted, false, cover))),
        ...pieceOf(focusClass, focusStyle(focus, columns, rowHeightPx, painted, false, cover)),
        ...dashes(false),
        ...fillPieces(false),
    ];
    return { shade, pinned, scrollable };
}
