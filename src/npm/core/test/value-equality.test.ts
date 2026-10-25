// What C# says with records and types, and the Script Core says at run time (CONVENTIONS.md): a value made
// again equals the one before, and what only an untyped caller can pass is refused at the boundary. The
// shared cases cannot ask C# for Equals, whose overloads take the same one argument.
import { describe, expect, it } from 'vitest';
import { AutoWidth } from '../src/columns/auto-width';
import { CellTextMetrics } from '../src/columns/cell-text-metrics';
import { ColumnWidth, ColumnWidthSpec } from '../src/columns/column-width';
import { GridMetrics, type GridDensity } from '../src/grid-metrics';

const spec = () => new ColumnWidthSpec({ width: ColumnWidth.auto, maxWidthPx: 300 });
const again = () => GridMetrics.resolve({ density: 'compact', rowHeightPx: 30 });
const metrics = () => new CellTextMetrics({ digitWidthPx: 9, cellHorizontalPaddingPx: 8 });

describe('a value made again is equal, as a C# record is', () => {
    it('ADR-0016: a width, its spec and its tracker', () => {
        expect(ColumnWidth.fixed(120).equals(ColumnWidth.fixed(120))).toBe(true);
        expect(ColumnWidth.fixed(120).equals(ColumnWidth.auto)).toBe(false);
        expect(spec().equals(spec())).toBe(true);
        expect(spec().equals(new ColumnWidthSpec({ width: ColumnWidth.auto }))).toBe(false);
        expect(AutoWidth.of(spec()).observe(90).equals(AutoWidth.of(spec()).observe(90))).toBe(true);
        expect(AutoWidth.of(spec()).observe(90).equals(AutoWidth.of(spec()).observe(91))).toBe(false);
    });

    it('ADR-0028 / ADR-0016: Grid Metrics resolved again from the same parameters are no change', () => {
        expect(again().equals(again())).toBe(true);
        expect(again().equals(GridMetrics.resolve({ density: 'compact', rowHeightPx: 31 }))).toBe(false);
        expect(again().equals(GridMetrics.resolve({ density: 'compact', rowHeightPx: 30, headerHeightPx: 40 }))).toBe(false);
    });

    it('ADR-0016: metrics compare their widths and their glyph table', () => {
        expect(metrics().equals(metrics())).toBe(true);
        expect(metrics().equals(new CellTextMetrics({ digitWidthPx: 9, cellHorizontalPaddingPx: 6 }))).toBe(false);
        const tabled = GridMetrics.resolve({ density: 'compact' }).cellMetrics;
        expect(tabled.equals(GridMetrics.resolve({ density: 'compact' }).cellMetrics)).toBe(true);
        expect(tabled.equals(tabled.withGlyphWidths(undefined, 14))).toBe(false);
        expect(tabled.bold.equals(tabled)).toBe(false);
    });
});

describe('what only an untyped caller can pass', () => {
    it('ADR-0028: an unknown density is the caller\'s argument refused, not a defect of the core', () => {
        expect(() => GridMetrics.resolve({ density: 'dense' as GridDensity })).toThrow(RangeError);
    });

    it('ADR-0016: a spec\'s width is a ColumnWidth, not a number of pixels', () => {
        expect(() => new ColumnWidthSpec({ width: 120 as unknown as ColumnWidth })).toThrow(TypeError);
    });
});
