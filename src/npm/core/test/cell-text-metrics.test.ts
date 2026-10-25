// What the shared cases cannot say, because C# says it with overloads: the options forms the Script Core
// takes, and the forms it refuses (tests/cases/columns/text-metrics.json holds the rest).
import { describe, expect, it } from 'vitest';
import { CellTextMetrics, type CellTextMetricsOptions } from '../src/columns/cell-text-metrics';
import { GlyphWidthTable } from '../src/columns/glyph-width-table';

describe('CellTextMetrics options', () => {
    it.each([
        ['a wide width without its narrow one', { digitWidthPx: 9, cellHorizontalPaddingPx: 4, wideWidthPx: 14 }],
        ['a full width without the three classes', { digitWidthPx: 9, cellHorizontalPaddingPx: 4, fullWidthPx: 14 }],
        ['two bold widths of three', {
            wideWidthPx: 14, digitWidthPx: 9, narrowWidthPx: 5, fullWidthPx: 14, cellHorizontalPaddingPx: 4,
            boldWideWidthPx: 15, boldDigitWidthPx: 10,
        }],
        ['the other widths without the bold ones', {
            wideWidthPx: 14, digitWidthPx: 9, narrowWidthPx: 5, fullWidthPx: 14, cellHorizontalPaddingPx: 4,
            otherWidthPx: 13, boldOtherWidthPx: 14,
        }],
        ['one other width of two', {
            wideWidthPx: 14, digitWidthPx: 9, narrowWidthPx: 5, fullWidthPx: 14, cellHorizontalPaddingPx: 4,
            boldWideWidthPx: 15, boldDigitWidthPx: 10, boldNarrowWidthPx: 6, otherWidthPx: 13,
        }],
    ])('ADR-0016: %s matches no form and is refused', (_, options) => {
        expect(() => new CellTextMetrics(options as unknown as CellTextMetricsOptions)).toThrow(TypeError);
    });

    it('ADR-0016: the three-class form derives the bold other class from the derived bold digit', () => {
        const metrics = new CellTextMetrics({ wideWidthPx: 14, digitWidthPx: 9, narrowWidthPx: 5, cellHorizontalPaddingPx: 4 });

        expect(metrics.boldOtherWidthPx).toBe(9 * CellTextMetrics.boldWidthAllowance * CellTextMetrics.otherWidthAllowance);
    });
});

describe('GlyphWidthTable', () => {
    it('ADR-0016 / ticket 83: a table answers the width of a glyph it holds at either weight, and nothing for one it lacks', () => {
        const table = new GlyphWidthTable(14, [['M', 13, 14]]);

        expect(table.widthPxOf(0x4D, false)).toBe(13);
        expect(table.widthPxOf(0x4D, true)).toBe(14);
        expect(table.widthPxOf(0x57, false)).toBeUndefined();
        expect([...table.codePoints]).toEqual([0x4D]);
    });

    it('ADR-0016: a lone surrogate is not one character', () => {
        expect(() => new GlyphWidthTable(14, [['\uD835', 13, 14]])).toThrow(TypeError);
    });
});

describe('CellTextMetrics as the paint path reads it', () => {
    const metrics = new CellTextMetrics({
        wideWidthPx: 14, digitWidthPx: 9, narrowWidthPx: 5, fullWidthPx: 16, cellHorizontalPaddingPx: 0,
        boldWideWidthPx: 15, boldDigitWidthPx: 10, boldNarrowWidthPx: 6, otherWidthPx: 13, boldOtherWidthPx: 14,
    });

    it('ADR-0027 P5: the bold and Text metrics are made once, not per cell', () => {
        expect(metrics.bold).toBe(metrics.bold);
        expect(metrics.for('text')).toBe(metrics.for('text'));
        expect(metrics.for('number')).toBe(metrics);
    });

    it('ADR-0016: a lone surrogate is charged as U+FFFD, as C# reads it', () => {
        const table = new GlyphWidthTable(14, [['�', 6, 7]]);

        expect(metrics.withGlyphWidths(table, 14).textWidthPx('1\uD835')).toBe(9 + 6);
        expect(metrics.textWidthPx('1\uD835')).toBe(9 + 13);
    });
});
