import { CellTextMetrics } from './columns/cell-text-metrics';
import type { GlyphWidthTable } from './columns/glyph-width-table';
import type { GridDensity } from './grid-metrics';

/** The widths a Wrapper states, in the forms {@link GridPresentationDefaults} takes. */
export interface GridPresentationDefaultsOptions {
    /** What `%` costs: the widest glyph of the wide class (ADR-0016). */
    readonly wideWidthPx: number;
    /** A tabular digit. */
    readonly digitWidthPx: number;
    /** The widest separator. */
    readonly narrowWidthPx: number;
    /** The font size the widths were measured at. */
    readonly fontSizePx: number;
    /** The three classes at the bold weight (ADR-0050, item 15), given together or not at all. */
    readonly boldWideWidthPx?: number;
    /** See {@link GridPresentationDefaultsOptions.boldWideWidthPx}. */
    readonly boldDigitWidthPx?: number;
    /** See {@link GridPresentationDefaultsOptions.boldWideWidthPx}. */
    readonly boldNarrowWidthPx?: number;
    /** The other class at each weight (ticket 83), given together and only with the bold widths. */
    readonly otherWidthPx?: number;
    /** See {@link GridPresentationDefaultsOptions.otherWidthPx}. */
    readonly boldOtherWidthPx?: number;
    /** The font's letters and currency signs at their own widths (ticket 83). */
    readonly glyphWidths?: GlyphWidthTable;
    /** The preset the Wrapper's density word maps onto. */
    readonly density?: GridDensity;
    /** The hover band's switch, where the design system names it on the Wrapper (ADR-0029). */
    readonly highlightHoverRow?: boolean;
    /** Row Stripes, where the design system names them on the Wrapper (ADR-0038). */
    readonly stripeRows?: boolean;
}

/**
 * What a Wrapper hands down to every grid inside it (ADR-0030): the glyph widths of the font its Theme
 * sets, per class (ADR-0016), stated at the size they were measured, and optionally the Density, hover
 * band and Row Stripes its own words map onto. Whoever sets the font owes the widths (ADR-0027), and a
 * Wrapper sets the font outside the grid, so this object keeps font and widths in one hand.
 *
 * Precedence is ADR-0028's, one rung lower: an explicit value on the grid beats this object, per value,
 * and this object beats the preset. The widths are scaled to the resolved font size, so one measurement
 * serves every preset; the padding is the preset's own, because it is layout, not type. Without bold
 * widths a bold cell is charged {@link CellTextMetrics.boldWidthAllowance} over these; without other
 * widths, {@link CellTextMetrics.otherWidthAllowance} over the digit.
 */
export class GridPresentationDefaults {
    /** What `%` costs. */
    readonly wideWidthPx: number;
    /** A tabular digit. */
    readonly digitWidthPx: number;
    /** The widest separator. */
    readonly narrowWidthPx: number;
    /** The wide class at the bold weight. */
    readonly boldWideWidthPx: number;
    /** A tabular digit at the bold weight. */
    readonly boldDigitWidthPx: number;
    /** The widest separator at the bold weight. */
    readonly boldNarrowWidthPx: number;
    /** The other class: the widest glyph outside the measured classes. */
    readonly otherWidthPx: number;
    /** The other class at the bold weight. */
    readonly boldOtherWidthPx: number;
    /** The font's letters and currency signs at their own widths, or `undefined` to charge them the other
     * class. */
    readonly glyphWidths: GlyphWidthTable | undefined;
    /** The font size the widths are true at. */
    readonly fontSizePx: number;
    /** The preset the Wrapper's density word maps onto, or `undefined` to leave the grid's own. */
    readonly density: GridDensity | undefined;
    /** The hover band's switch, or `undefined` to leave the grid's default (off). */
    readonly highlightHoverRow: boolean | undefined;
    /** Row Stripes, or `undefined` to leave the grid's default (off). */
    readonly stripeRows: boolean | undefined;

    /** The widths, refused here by the rules the Cell Metrics enforce, so a bad measurement fails where it
     * was written rather than in a grid far away. */
    constructor(options: GridPresentationDefaultsOptions) {
        const { fontSizePx } = options;
        if (!Number.isFinite(fontSizePx) || fontSizePx <= 0) {
            throw new RangeError(`The size the widths were measured at is a finite, positive number of pixels. (fontSizePx: ${fontSizePx})`);
        }
        // The Cell Metrics take the same forms, refuse by the same rules and derive what a form leaves out
        // by the same allowances, so the widths are read back from them.
        const metrics = metricsOf(options);
        this.wideWidthPx = metrics.wideWidthPx;
        this.digitWidthPx = metrics.digitWidthPx;
        this.narrowWidthPx = metrics.narrowWidthPx;
        this.boldWideWidthPx = metrics.boldWideWidthPx;
        this.boldDigitWidthPx = metrics.boldDigitWidthPx;
        this.boldNarrowWidthPx = metrics.boldNarrowWidthPx;
        this.otherWidthPx = metrics.otherWidthPx;
        this.boldOtherWidthPx = metrics.boldOtherWidthPx;
        this.glyphWidths = options.glyphWidths;
        this.fontSizePx = fontSizePx;
        this.density = options.density;
        this.highlightHoverRow = options.highlightHoverRow;
        this.stripeRows = options.stripeRows;
    }

    /**
     * The metrics at the size the grid resolved: the widths scaled linearly from
     * {@link GridPresentationDefaults.fontSizePx}, with the preset's own padding. The full-width class is
     * the resolved font size itself (ADR-0016).
     *
     * @internal
     */
    cellMetricsAt(fontSizePx: number, cellHorizontalPaddingPx: number): CellTextMetrics {
        if (!Number.isFinite(fontSizePx) || fontSizePx <= 0) {
            throw new RangeError(`A font size is a finite, positive number of pixels. (fontSizePx: ${fontSizePx})`);
        }
        const scale = fontSizePx / this.fontSizePx;
        return new CellTextMetrics({
            wideWidthPx: this.wideWidthPx * scale, digitWidthPx: this.digitWidthPx * scale, narrowWidthPx: this.narrowWidthPx * scale,
            // Full-width is the em, not a measurement to scale: it is the resolved size.
            fullWidthPx: Math.max(fontSizePx, this.digitWidthPx * scale), cellHorizontalPaddingPx,
            boldWideWidthPx: this.boldWideWidthPx * scale, boldDigitWidthPx: this.boldDigitWidthPx * scale,
            boldNarrowWidthPx: this.boldNarrowWidthPx * scale,
            otherWidthPx: this.otherWidthPx * scale, boldOtherWidthPx: this.boldOtherWidthPx * scale,
        }).withGlyphWidths(this.glyphWidths, fontSizePx);
    }
}

// The defaults' widths as Cell Metrics of no padding, form by form: the three classes; with the bold ones;
// with the other ones too. A group given in part matches no form.
function metricsOf(options: GridPresentationDefaultsOptions): CellTextMetrics {
    const { wideWidthPx, digitWidthPx, narrowWidthPx } = options;
    const { boldWideWidthPx, boldDigitWidthPx, boldNarrowWidthPx, otherWidthPx, boldOtherWidthPx } = options;
    const classes = { wideWidthPx, digitWidthPx, narrowWidthPx, cellHorizontalPaddingPx: 0 };
    if (boldWideWidthPx === undefined && boldDigitWidthPx === undefined && boldNarrowWidthPx === undefined
        && otherWidthPx === undefined && boldOtherWidthPx === undefined) {
        return new CellTextMetrics(classes);
    }
    if (boldWideWidthPx === undefined || boldDigitWidthPx === undefined || boldNarrowWidthPx === undefined) {
        throw new TypeError('The bold widths are given together, and the other widths only with them (ADR-0030).');
    }
    const bold = { ...classes, fullWidthPx: 2 * digitWidthPx, boldWideWidthPx, boldDigitWidthPx, boldNarrowWidthPx };
    if (otherWidthPx === undefined && boldOtherWidthPx === undefined) {
        return new CellTextMetrics(bold);
    }
    if (otherWidthPx === undefined || boldOtherWidthPx === undefined) {
        throw new TypeError('The other widths are given together (ADR-0030).');
    }
    return new CellTextMetrics({ ...bold, otherWidthPx, boldOtherWidthPx });
}
