// What C# says with its types and the Script Core must say at run time: a count or a position is a whole
// number, and a count past what a number holds exactly is refused rather than rounded
// (CONVENTIONS.md, "Check at the boundary"). The shared cases cannot pass C# a half row.
import { describe, expect, it } from 'vitest';
import { cell } from '../src/selection/cell';
import { edgeBandRowsPerTick } from '../src/selection/edge-band';
import { selectionRange } from '../src/selection/range';
import { Selection } from '../src/selection/selection';

const grid = { rowCount: 10, columnCount: 5 };

describe('a whole number where C# takes an int', () => {
    it.each([2.5, Number.NaN, Number.POSITIVE_INFINITY])('ADR-0050: a column click refuses a Focus row of %s', focusRow => {
        expect(() => Selection.empty.selectColumn(1, grid, focusRow)).toThrow(RangeError);
        expect(() => Selection.empty.toggleColumn(1, grid, focusRow)).toThrow(RangeError);
        expect(() => Selection.empty.extendToColumn(1, grid, focusRow)).toThrow(RangeError);
    });

    it('ADR-0050: a row click refuses a Focus column that is not whole', () => {
        expect(() => Selection.empty.selectRow(1, grid, 0.5)).toThrow(RangeError);
        expect(() => Selection.empty.toggleRow(1, grid, 0.5)).toThrow(RangeError);
        expect(() => Selection.empty.extendToRow(1, grid, 0.5)).toThrow(RangeError);
    });

    it('ADR-0050 / ADR-0055: a placed or found Focus is a whole cell', () => {
        expect(() => Selection.empty.place(selectionRange(0, 0, 3, 3), cell(1.5, 1), grid)).toThrow(RangeError);
        const selected = Selection.empty.click(cell(0, 0), grid).extendTo(cell(3, 3), grid);
        expect(() => selected.focusOn(cell(1, Number.NaN))).toThrow(RangeError);
    });

    it('ADR-0011 / ADR-0012: an extent, a page and a Viewport of rows are whole', () => {
        expect(() => Selection.empty.click(cell(0, 0), { rowCount: Number.NaN, columnCount: 5 })).toThrow(RangeError);
        expect(() => Selection.empty.selectAll(grid, { firstRow: 1.5, rowCount: 3 })).toThrow(RangeError);
        const one = Selection.empty.click(cell(0, 0), grid);
        expect(() => one.moveByViewport(2.5, grid)).toThrow(RangeError);
        expect(() => one.extendByViewport(Number.NaN, grid)).toThrow(RangeError);
    });

    it('ADR-0008: the edge band refuses a Viewport of rows that is not whole', () => {
        expect(() => edgeBandRowsPerTick(0, 400, 5.5)).toThrow(RangeError);
        expect(() => edgeBandRowsPerTick(0, 400, Number.NaN)).toThrow(RangeError);
    });
});

describe('a count a number holds exactly', () => {
    it('ADR-0011 / SL-2: a selection past 2^53 cells is refused, not rounded (C# counts it in a long)', () => {
        const largest = { rowCount: 2_147_483_647, columnCount: 2_147_483_647 };
        const all = Selection.empty.selectAll(largest);

        expect(all.ranges).toEqual([selectionRange(0, 0, largest.rowCount, largest.columnCount)]);
        expect(() => all.cellCount).toThrow(RangeError);
    });

    it('ADR-0011: the largest row count across Excel\'s 16,384 columns is counted exactly', () => {
        expect(Selection.empty.selectAll({ rowCount: 2_147_483_647, columnCount: 16_384 }).cellCount).toBe(35_184_372_072_448);
    });
});
