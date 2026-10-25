import { CellTextMetrics } from './columns/cell-text-metrics';
import { defaultGlyphWidthsAt } from './columns/default-glyph-widths';
import type { GridPresentationDefaults } from './grid-presentation-defaults';
import { unreachable } from './must';
import { ViewportGeometry } from './viewport/viewport-geometry';

/**
 * A named preset resolving into a complete {@link GridMetrics}, so its numbers are consistent with each
 * other (ADR-0028). `compact` is the default, the numbers an untouched grid always had; `excel` names what
 * it reproduces, Excel's default row. No design system's density word is the grid's; a Wrapper maps its
 * own onto these (ADR-0030).
 */
export type GridDensity = 'compact' | 'comfortable' | 'standard' | 'excel';

/** What {@link GridMetrics.resolve} takes: a preset, and the values that beat it. */
export interface GridMetricsOptions {
    /** The preset every value not given comes from. */
    readonly density: GridDensity;
    /** The row height, beating the preset's; the header follows it unless given itself. */
    readonly rowHeightPx?: number;
    /** The header height, beating the preset's and the row height's. */
    readonly headerHeightPx?: number;
    /** The Cell Metrics, beating a Wrapper's defaults and the preset's. */
    readonly cellMetrics?: CellTextMetrics;
    /** A Wrapper's cascaded widths (ADR-0030), beating the preset's. */
    readonly defaults?: GridPresentationDefaults;
}

/** What {@link GridMetrics.headerRequiredPx} takes: a label, and what stands beside it in the header. */
export interface HeaderOptions {
    /** The header's text. */
    readonly label: string;
    /** Whether the header shows the column-menu button. */
    readonly menuButton: boolean;
    /** Whether the column can be sorted, sorted now or not. */
    readonly sortable: boolean;
}

interface Preset {
    readonly row: number;
    readonly header: number;
    readonly font: 14 | 12;
    readonly wide: number;
    readonly digit: number;
    readonly narrow: number;
    readonly other: number;
    readonly padding: number;
    readonly actionPad: number;
    readonly actionBorder: number;
    readonly actionGap: number;
    readonly menuWidth: number;
    readonly menuInset: number;
    readonly sortMark: number;
    readonly boldWide: number;
    readonly boldDigit: number;
    readonly boldNarrow: number;
    readonly boldOther: number;
}

// The preset table (ADR-0028), with the character-class widths of ADR-0016: each the widest glyph of its
// class measured on any supported platform at the stylesheet's system-ui, at every weight the grid paints
// regular text in (400 to 600), declared a shade over; the bold ones the same at 700. Measured on
// 2026-10-01 (ticket 83, tests/GlyphWidths); Excel's 12px set is measured at 12px, not scaled, because SF
// is optically sized. The C# core's GridMetrics.Resolve holds the same table and says each number's origin.
const fourteen = { font: 14, wide: 14.04, digit: 9.75, narrow: 6.41, other: 15.46, boldWide: 14.36, boldDigit: 9.75, boldNarrow: 6.41, boldOther: 15.46 } as const;
const chrome = { actionPad: 6, actionBorder: 1, actionGap: 4, menuWidth: 16, menuInset: 6, sortMark: 20 } as const;

function presetOf(density: GridDensity): Preset {
    switch (density) {
        case 'comfortable': return { row: 40, header: 40, padding: 12, ...fourteen, ...chrome };
        case 'standard': return { row: 32, header: 32, padding: 8, ...fourteen, ...chrome };
        case 'compact': return { row: 28, header: 28, padding: 8, ...fourteen, ...chrome };
        case 'excel': return {
            row: 20, header: 20, font: 12, wide: 12.04, digit: 8.36, narrow: 5.49, other: 13.26, padding: 4,
            actionPad: 4, actionBorder: 1, actionGap: 2, menuWidth: 14, menuInset: 4, sortMark: 16,
            boldWide: 12.44, boldDigit: 8.36, boldNarrow: 5.49, boldOther: 13.26,
        };
        default: return unreachable(density);
    }
}

/**
 * The single resolved geometry of an instance (ADR-0028): every number the virtualisation arithmetic, the
 * overlays, the editor placement or the width estimates read, resolved once, and the Geometry Tokens the
 * DOM lays out with are emitted from the same object, so arithmetic and paint cannot disagree.
 *
 * Precedence is per value: an explicit value beats the {@link GridDensity} preset, which beats the core
 * default. The preset numbers are declared, not measured.
 */
export class GridMetrics {
    /** The fixed height of every row (ADR-0013). */
    readonly rowHeightPx: number;
    /** One header tier's height, no longer tied to the row height (ADR-0028, ADR-0032). */
    readonly headerHeightPx: number;
    /** The size {@link GridMetrics.cellMetrics}' widths are true at. */
    readonly fontSizePx: number;
    /** The widths the width and overflow estimates read (ADR-0016). */
    readonly cellMetrics: CellTextMetrics;
    /** One action button's own horizontal padding, per side (ADR-0020). */
    readonly actionPaddingXPx: number;
    /** One action button's border, per side. */
    readonly actionBorderPx: number;
    /** The gap between neighbouring action buttons. */
    readonly actionGapPx: number;
    /** The column-menu button's own width in the header cell (ADR-0010). */
    readonly menuButtonWidthPx: number;
    /** The gap between the menu button and the header cell's right edge. */
    readonly menuButtonInsetPx: number;
    /** The box the sort mark stands in at the end of a sorted header: an em and a gap (ADR-0016/0029). */
    readonly sortMarkWidthPx: number;
    /** The resize grip's hit width in a header cell's right edge (ADR-0016; UX-1). */
    readonly resizeGripWidthPx: number;
    /** The painted width of the resize line and the reorder drop indicator (ADR-0016, ADR-0011; UX-1). */
    readonly columnGuideWidthPx: number;

    private constructor(preset: Preset, rowHeightPx: number, headerHeightPx: number, cellMetrics: CellTextMetrics) {
        this.rowHeightPx = rowHeightPx;
        this.headerHeightPx = headerHeightPx;
        this.fontSizePx = preset.font;
        this.cellMetrics = cellMetrics;
        this.actionPaddingXPx = preset.actionPad;
        this.actionBorderPx = preset.actionBorder;
        this.actionGapPx = preset.actionGap;
        this.menuButtonWidthPx = preset.menuWidth;
        this.menuButtonInsetPx = preset.menuInset;
        this.sortMarkWidthPx = preset.sortMark;
        // Neither is read by any arithmetic, but no pixel is only a stylesheet value (ADR-0027).
        this.resizeGripWidthPx = 5;
        this.columnGuideWidthPx = 2;
    }

    /**
     * Resolution, once (ADR-0028): a value not given leaves the preset's in place, and a value given wins.
     * An explicit row height moves the header with it unless the header is given too: an untouched grid's
     * header always matched its rows. A Wrapper's defaults sit between the two for the widths (ADR-0030).
     */
    static resolve(options: GridMetricsOptions): GridMetrics {
        const preset = presetOf(options.density);
        const rowHeightPx = options.rowHeightPx ?? preset.row;
        const headerHeightPx = options.headerHeightPx ?? options.rowHeightPx ?? preset.header;
        if (!Number.isFinite(rowHeightPx) || rowHeightPx <= 0) {
            throw new RangeError(`RowHeight is a finite, positive number of pixels (ADR-0013). (rowHeightPx: ${rowHeightPx})`);
        }
        if (!Number.isFinite(headerHeightPx) || headerHeightPx <= 0) {
            throw new RangeError(`HeaderHeight is a finite, positive number of pixels (ADR-0028). (headerHeightPx: ${headerHeightPx})`);
        }
        const metrics = options.cellMetrics
            ?? options.defaults?.cellMetricsAt(preset.font, preset.padding)
            ?? new CellTextMetrics({
                wideWidthPx: preset.wide, digitWidthPx: preset.digit, narrowWidthPx: preset.narrow, fullWidthPx: preset.font,
                cellHorizontalPaddingPx: preset.padding,
                boldWideWidthPx: preset.boldWide, boldDigitWidthPx: preset.boldDigit, boldNarrowWidthPx: preset.boldNarrow,
                otherWidthPx: preset.other, boldOtherWidthPx: preset.boldOther,
            }).withGlyphWidths(defaultGlyphWidthsAt(preset.font), preset.font);
        // A full-width character is an em whatever the family, and the em is the font size this grid
        // emits, so explicit metrics too are charged at least that for it (ADR-0016).
        return new GridMetrics(preset, rowHeightPx, headerHeightPx, metrics.withFullWidthAtLeast(preset.font));
    }

    /**
     * The tallest row a result of `totalRowCount` rows can carry under the browser's scroll ceiling
     * (ADR-0013/0028): what a Consumer offering a density menu greys choices out with.
     */
    static largestRowHeightFor(totalRowCount: number, headerHeightPx: number): number {
        if (!Number.isSafeInteger(totalRowCount) || totalRowCount < 1) {
            throw new RangeError(`A row count is a whole number of at least 1. (totalRowCount: ${totalRowCount})`);
        }
        if (!(headerHeightPx >= 0)) {
            throw new RangeError(`A header height is not negative. (headerHeightPx: ${headerHeightPx})`);
        }
        return (ViewportGeometry.maxScrollHeightPx - headerHeightPx) / totalRowCount;
    }

    /** The Formula Bar's height: the header's, so the bar reads as one more band of the frame (ADR-0051). */
    get formulaBarHeightPx(): number {
        return this.headerHeightPx;
    }

    /** The Name Box's width (ADR-0051): `XFD1048576`, the longest address a Sheet has, estimated as text. */
    get nameBoxWidthPx(): number {
        return this.cellMetrics.for('text').estimatePx('XFD1048576');
    }

    /** The fill handle's side (ADR-0050, item 5): a little under a third of a row, and at least 5px. */
    get fillHandleSizePx(): number {
        return Math.max(5, Math.round(this.rowHeightPx * 0.3));
    }

    /** A cell's horizontal padding on one side; a cell pays it twice. */
    get cellPaddingXPx(): number {
        return this.cellMetrics.cellHorizontalPaddingPx;
    }

    /** The width of one digit: the unit `####` is counted in (ADR-0016). */
    get digitWidthPx(): number {
        return this.cellMetrics.digitWidthPx;
    }

    /** One action button's box beside its text: padding and border both sides, and the gap to the next
     * (ADR-0016/0020). */
    get actionButtonChromePx(): number {
        return (this.actionPaddingXPx * 2) + (this.actionBorderPx * 2) + this.actionGapPx;
    }

    /** What the menu button takes off the header text's room: its width and inset (ADR-0016/0028). */
    get menuButtonBandPx(): number {
        return this.menuButtonWidthPx + this.menuButtonInsetPx;
    }

    /**
     * The full cell width a header needs (ADR-0016, 2026-09-25): its label charged as text, one full-width
     * em of slack (a label is proportional letters no per-class charge can bound), the menu button's band
     * when there is one, and the sort mark's box when the column can be sorted, sorted now or not, so a
     * header click never chops its label nor moves the columns to its right.
     */
    headerRequiredPx(header: HeaderOptions): number {
        return this.cellMetrics.for('text').estimatePx(header.label)
            + this.cellMetrics.fullWidthPx
            + (header.menuButton ? this.menuButtonBandPx : 0)
            + (header.sortable ? this.sortMarkWidthPx : 0);
    }
}
