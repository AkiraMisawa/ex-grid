// The text geometry handed to a Consumer is the one the grid charges with (ADR-0016): a Pivot Report's label
// widths, estimated by its report source under it, fit the cells the grid paints them in.
import { describe, expect, it } from 'vitest';
import { GridMetrics } from '../src/grid-metrics';
import { gridHeaderRequiredPx, gridTextGeometry } from '../src/text-geometry';

describe('gridTextGeometry', () => {
    it.each(['compact', 'comfortable', 'standard', 'excel'] as const)('ADR-0016: charges %s text as the grid does', density => {
        const geometry = gridTextGeometry({ density });
        const metrics = GridMetrics.resolve({ density }).cellMetrics;

        expect(geometry.cellHorizontalPaddingPx).toBe(metrics.cellHorizontalPaddingPx);
        expect(geometry.fullWidthPx).toBe(metrics.fullWidthPx);
        // A glyph outside the classes is charged its own width, as the grid charges it.
        const glyphs = Object.entries(geometry.glyphWidths);
        expect(glyphs.length).toBeGreaterThan(0);
        for (const [point, width] of glyphs) expect(metrics.textWidthPx(String.fromCodePoint(Number(point)))).toBe(width);
    });

    it('ADR-0016: is plain data, frozen, that a Worker is handed as it is', () => {
        const geometry = gridTextGeometry();

        expect(Object.isFrozen(geometry)).toBe(true);
        expect(structuredClone(geometry)).toEqual(geometry);
        expect(geometry).toEqual(gridTextGeometry({ density: 'compact' }));
    });
});

describe('gridHeaderRequiredPx', () => {
    it('ADR-0016: charges a header as an Auto width does, with the menu band and the sort mark only when asked', () => {
        const metrics = GridMetrics.resolve({ density: 'standard' });
        const bare = gridHeaderRequiredPx('Region', { density: 'standard', menuButton: false, sortable: false });

        expect(bare).toBe(metrics.headerRequiredPx({ label: 'Region', menuButton: false, sortable: false }));
        expect(gridHeaderRequiredPx('Region', { density: 'standard', menuButton: true, sortable: true }))
            .toBe(bare + metrics.menuButtonBandPx + metrics.sortMarkWidthPx);
    });
});
