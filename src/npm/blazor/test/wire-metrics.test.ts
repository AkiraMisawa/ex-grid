// The widths a Blazor Consumer hands a grid, as they arrive from .NET (ADR-0016, ADR-0030): the numbers .NET wrote are
// the metrics the core charges by, a glyph table included at the size it is scaled to, and the same numbers handed
// again on another render are the same objects, so the grid resolves nothing anew.
import { describe, expect, it } from 'vitest';
import { CellTextMetrics } from '@ex-grid/core';
import { WireMetrics, type WireCellMetrics, type WirePresentationDefaults } from '../src/wire-metrics';

// As ScriptMetricsJson writes them: each glyph's charge at the metrics' size, in a table measured at one pixel and
// scaled to one.
const roboto: WireCellMetrics = {
    wideWidthPx: 9.5, digitWidthPx: 7.5, narrowWidthPx: 3.5, fullWidthPx: 13, cellHorizontalPaddingPx: 8,
    boldWideWidthPx: 10, boldDigitWidthPx: 8, boldNarrowWidthPx: 4, otherWidthPx: 12, boldOtherWidthPx: 12.5,
    glyphWidths: { measuredAtPx: 1, glyphs: [['W', 11.375, 12.1875]] }, fontSizePx: 1,
};

describe('the widths .NET hands a grid', () => {
    it('ADR-0016: the Cell Metrics .NET wrote charge every class and each glyph as .NET charges them', () => {
        const made = new WireMetrics().cellMetrics(roboto);

        expect(made).toBeInstanceOf(CellTextMetrics);
        expect(made?.digitWidthPx).toBe(7.5);
        expect(made?.boldOtherWidthPx).toBe(12.5);
        expect(made?.textWidthPx('W')).toBe(11.375);
        expect(made?.bold.textWidthPx('W')).toBe(12.1875);
    });

    it('ADR-0003: the same numbers handed again are the same objects, and other numbers are new ones', () => {
        const metrics = new WireMetrics();
        const first = metrics.cellMetrics(roboto);

        expect(metrics.cellMetrics({ ...roboto })).toBe(first);
        expect(metrics.cellMetrics({ ...roboto, digitWidthPx: 8 })).not.toBe(first);
        expect(metrics.cellMetrics(undefined)).toBeUndefined();
    });

    it('ADR-0030: a Wrapper\'s defaults carry its font\'s widths, its glyph table and its words', () => {
        const wire: WirePresentationDefaults = {
            wideWidthPx: 9.5, digitWidthPx: 7.5, narrowWidthPx: 3.5, fontSizePx: 14, boldWideWidthPx: 10, boldDigitWidthPx: 8,
            boldNarrowWidthPx: 4, otherWidthPx: 12, boldOtherWidthPx: 12.5, glyphWidths: { measuredAtPx: 16, glyphs: [['W', 14, 15]] },
            density: 'standard', stripeRows: true,
        };
        const metrics = new WireMetrics();
        const made = metrics.presentationDefaults(wire);

        expect(made?.density).toBe('standard');
        expect(made?.stripeRows).toBe(true);
        expect(made?.highlightHoverRow).toBeUndefined();
        expect(made?.glyphWidths?.widthPxOf(0x57, false)).toBe(14);
        expect(metrics.presentationDefaults({ ...wire })).toBe(made);
    });
});
