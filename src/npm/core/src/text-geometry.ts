// The text geometry a grid charges text with (ADR-0016), handed out as plain data, so a Consumer that lays text
// out beside the grid's, or has it estimated elsewhere, charges it alike: ExPivot tells its report source the
// geometry its label widths are estimated under, as the Razor component tells it the grid's Cell Metrics
// (ADR-0151). Nothing here reads a browser's layout (ADR-0021).

import { GridMetrics, type GridDensity } from './grid-metrics';

/** The widths a grid charges each character class, its cells' padding, and single glyphs, in pixels. */
export interface GridTextGeometry {
    /** `%`, `€`, `−`, `+`, `#`. */
    readonly wideWidthPx: number;
    /** Digits, currency signs and the characters numbers are written with. */
    readonly digitWidthPx: number;
    /** `.`, `,`, `(`, `)`, `/`, `:` and the space. */
    readonly narrowWidthPx: number;
    /** A full-width character, the em of the grid's font. */
    readonly fullWidthPx: number;
    /** Any other character. */
    readonly otherWidthPx: number;
    /** The padding on one side of a cell. */
    readonly cellHorizontalPaddingPx: number;
    /** Single glyphs charged at their own width, by code point. */
    readonly glyphWidths: Readonly<Record<number, number>>;
}

/** The text geometry of a grid drawn at `density`, as `createGrid` resolves it: plain data, which crosses to a
 * Worker as it is. A row height does not move it. */
export function gridTextGeometry(options: { readonly density?: GridDensity } = {}): GridTextGeometry {
    const { density = 'compact' } = options;
    const metrics = GridMetrics.resolve({ density }).cellMetrics;
    return Object.freeze({
        wideWidthPx: metrics.wideWidthPx,
        digitWidthPx: metrics.digitWidthPx,
        narrowWidthPx: metrics.narrowWidthPx,
        fullWidthPx: metrics.fullWidthPx,
        otherWidthPx: metrics.otherWidthPx,
        cellHorizontalPaddingPx: metrics.cellHorizontalPaddingPx,
        glyphWidths: metrics.exportGlyphWidths(),
    });
}

/** The cell width a header needs in a grid drawn at `density`, as an Auto width charges it (ADR-0016): its label,
 * an em of slack, the column-menu button's band when `menuButton`, and the sort mark's box when `sortable`. A
 * Consumer that fixes a column's width beside the grid's own charges its header alike. */
export function gridHeaderRequiredPx(label: string, options: { readonly density?: GridDensity; readonly menuButton: boolean; readonly sortable: boolean }): number {
    const { density = 'compact', menuButton, sortable } = options;
    return GridMetrics.resolve({ density }).headerRequiredPx({ label, menuButton, sortable });
}
