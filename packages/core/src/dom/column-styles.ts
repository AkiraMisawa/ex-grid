import type { ColumnGeometry } from '../columns/column-geometry';

/**
 * The inline styles a column's cells are placed with, made once per column geometry (ADR-0027 P5): every
 * row reads the same strings, so a scroll frame composes none. A new geometry is a new instance, and a
 * row painted under another instance is painted again (ADR-0003).
 */
export class ColumnStyles {
    /** The geometry the styles are made from. */
    readonly geometry: ColumnGeometry;
    readonly #cells: (string | undefined)[] = [];
    readonly #pinnedCells: (string | undefined)[] = [];
    readonly #gaps: (string | undefined)[] = [];

    constructor(geometry: ColumnGeometry) {
        this.geometry = geometry;
    }

    /** A scrollable cell's width. */
    cell(column: number): string {
        return this.#cells[column] ??= `width: ${this.geometry.widthPxOf(column)}px`;
    }

    /** A pinned cell's width and its own offset: a sticky cell still occupies its place in the flow, so it
     * sticks exactly where it would otherwise have been. */
    pinnedCell(column: number): string {
        return this.#pinnedCells[column] ??= `width: ${this.geometry.widthPxOf(column)}px; left: ${this.geometry.offsetPxOf(column)}px`;
    }

    /** The spacer standing for the scrollable columns virtualised away to the left (ADR-0004). The pinned
     * block is already in the flow ahead of it, so its width comes off. */
    gap(firstScrollable: number): string {
        return this.#gaps[firstScrollable] ??=
            `width: ${Math.max(0, this.geometry.offsetPxOf(firstScrollable) - this.geometry.pinnedWidthPx)}px`;
    }

    /** Whether any scrollable column lies to the left of `firstScrollable`, so a gap stands for it. */
    hasGap(firstScrollable: number): boolean {
        return this.geometry.offsetPxOf(firstScrollable) > this.geometry.pinnedWidthPx;
    }
}
