// The text geometry handed to a Consumer is the one the grid charges with (ADR-0016): a Pivot Report's label
// widths, estimated by its report source under it, fit the cells the grid paints them in.
import { describe, expect, it } from 'vitest';
import { CellTextMetrics } from '../src/columns/cell-text-metrics';
import { GridMetrics } from '../src/grid-metrics';
import { GridPresentationDefaults } from '../src/grid-presentation-defaults';
import { gridCellMetrics, gridHeaderRequiredPx, gridTextGeometry } from '../src/text-geometry';

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

    it('ADR-0016, ADR-0028, ADR-0030: charges by the Cell Metrics given, then a Wrapper\'s widths and Density, as createGrid resolves them', () => {
        const cellMetrics = new CellTextMetrics({ digitWidthPx: 11, cellHorizontalPaddingPx: 5 });
        const defaults = new GridPresentationDefaults({ wideWidthPx: 13, digitWidthPx: 9, narrowWidthPx: 4, fontSizePx: 16, density: 'standard' });

        const given = gridTextGeometry({ cellMetrics });
        expect(given.digitWidthPx).toBe(GridMetrics.resolve({ density: 'compact', cellMetrics }).cellMetrics.digitWidthPx);
        expect(given.cellHorizontalPaddingPx).toBe(5);
        const wrapped = gridTextGeometry({ presentationDefaults: defaults });
        const resolved = GridMetrics.resolve({ density: 'standard', defaults }).cellMetrics;
        expect(wrapped.wideWidthPx).toBe(resolved.wideWidthPx);
        expect(wrapped.cellHorizontalPaddingPx).toBe(resolved.cellHorizontalPaddingPx);
        // The Cell Metrics beat the Wrapper's widths; the Density given beats the Wrapper's.
        expect(gridTextGeometry({ cellMetrics, presentationDefaults: defaults }).digitWidthPx).toBe(11);
        expect(gridHeaderRequiredPx('Region', { presentationDefaults: defaults, density: 'compact', menuButton: false, sortable: false }))
            .toBe(GridMetrics.resolve({ density: 'compact', defaults }).headerRequiredPx({ label: 'Region', menuButton: false, sortable: false }));
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

describe('gridCellMetrics', () => {
    it('ADR-0028/ADR-0030: resolves as the grid does, a Wrapper\'s Density and widths under the grid\'s own, the same options the same object', () => {
        const defaults = new GridPresentationDefaults({ wideWidthPx: 11, digitWidthPx: 8, narrowWidthPx: 5, fontSizePx: 14, density: 'standard' });

        expect(gridCellMetrics().equals(GridMetrics.resolve({ density: 'compact' }).cellMetrics)).toBe(true);
        expect(gridCellMetrics({ presentationDefaults: defaults }).equals(GridMetrics.resolve({ density: 'standard', defaults }).cellMetrics)).toBe(true);
        expect(gridCellMetrics({ density: 'excel', presentationDefaults: defaults }).equals(GridMetrics.resolve({ density: 'excel', defaults }).cellMetrics)).toBe(true);
        expect(gridCellMetrics({ presentationDefaults: defaults })).toBe(gridCellMetrics({ presentationDefaults: defaults }));
    });
});
