import { GridMetrics, type GridDensity } from './grid-metrics';

/**
 * The font size a {@link GridDensity} paints its text at, and the width a text takes at it, estimated from
 * the preset's glyph widths and never measured (ADR-0016, ADR-0021). A product drawn beside a grid lays its
 * own labels out with it, as ExChart lays out its axes (ADR-0028), so the two read alike at one density.
 */
export interface DensityText {
    /** The font size in CSS pixels. */
    readonly fontSizePx: number;
    /** The width `text` takes at that size, in CSS pixels, without any cell's padding. */
    textWidthPx(text: string): number;
}

/** The text metrics of a density's preset, as the grid metrics resolve them for a grid with no overrides. */
export function densityText(density: GridDensity): DensityText {
    const metrics = GridMetrics.resolve({ density });
    const cells = metrics.cellMetrics;
    return Object.freeze({
        fontSizePx: metrics.fontSizePx,
        textWidthPx: (text: string) => cells.textWidthPx(text),
    });
}
