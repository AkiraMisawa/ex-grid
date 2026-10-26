// ADR-0028: a product drawn beside a grid lays its labels out at the grid's density, from the same widths.
import { describe, expect, it } from 'vitest';
import { densityText } from '../src/density-text';
import { GridMetrics } from '../src/grid-metrics';

describe('densityText', () => {
    it('ADR-0028: answers the preset\'s font size and the grid\'s own estimate of a text\'s width', () => {
        expect.hasAssertions();
        for (const density of ['compact', 'comfortable', 'standard', 'excel'] as const) {
            const grid = GridMetrics.resolve({ density });
            const text = densityText(density);
            expect(text.fontSizePx).toBe(grid.fontSizePx);
            expect(text.textWidthPx('-0,000.00 Exposure 日本')).toBe(grid.cellMetrics.textWidthPx('-0,000.00 Exposure 日本'));
        }
        expect(densityText('excel').fontSizePx).toBe(12);
    });

    it('ADR-0028: refuses a density that is not a preset', () => {
        expect.hasAssertions();
        // @ts-expect-error -- an untyped caller's unknown density
        expect(() => densityText('dense')).toThrow(RangeError);
    });
});
