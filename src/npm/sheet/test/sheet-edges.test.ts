// Where Ctrl+arrow stops on a Sheet (ADR-0050, item 2), as Excel answers it from the Sheet's blanks, ported from
// tests/ExSheet.Components/SheetEdgesTests.cs. Each row of the table is one Excel observation: the filled cells,
// where the Focus stands, the direction, and the cell Excel's Ctrl+arrow lands on.
import { describe, expect, it } from 'vitest';
import type { GridDirection } from '@ex-grid/core';
import { CellAddress, CellRange, Culture, NumberFormat, SheetEdit, Workbook, type Sheet } from '@ex-grid/sheet-engine';
import { SheetEdges } from '../src/index';

function sheetWith(...filled: string[]): Sheet {
    const workbook = new Workbook(Culture.of('en-US'));
    for (const address of filled) workbook.do('Sheet1', SheetEdit.enter(CellAddress.parse(address), '1'));
    return workbook.shown;
}

function enter(sheet: Sheet, address: string, typed: string): void {
    const step = sheet.workbook.do(sheet.name, SheetEdit.enter(CellAddress.parse(address), typed));
    return void step;
}

const excelTable: readonly (readonly [readonly string[], string, GridDirection, string])[] = [
    // An empty column: to the Sheet's edge, and nowhere from the edge itself.
    [[], 'A1', 'down', 'A1048576'],
    [[], 'A1', 'up', 'A1'],
    [[], 'A500', 'up', 'A1'],
    [[], 'A1', 'left', 'A1'],
    [[], 'A1', 'right', 'XFD1'],
    [[], 'XFD1048576', 'down', 'XFD1048576'],
    [[], 'XFD1048576', 'right', 'XFD1048576'],
    // Inside a block: to the block's last cell.
    [['A1', 'A2', 'A3', 'A4', 'A5'], 'A1', 'down', 'A5'],
    [['A1', 'A2', 'A3', 'A4', 'A5'], 'A3', 'down', 'A5'],
    [['A1', 'A2', 'A3', 'A4', 'A5'], 'A5', 'up', 'A1'],
    // At a block's last cell: to the next block's first cell, or to the edge.
    [['A1', 'A2', 'A3', 'A4', 'A5'], 'A5', 'down', 'A1048576'],
    [['A1', 'A2', 'A3', 'A4', 'A5', 'A10', 'A11', 'A12'], 'A5', 'down', 'A10'],
    [['A1', 'A2', 'A3', 'A4', 'A5', 'A10', 'A11', 'A12'], 'A10', 'down', 'A12'],
    [['A1', 'A2', 'A3', 'A4', 'A5', 'A10', 'A11', 'A12'], 'A10', 'up', 'A5'],
    // From a blank cell: to the next filled cell along the line.
    [['A1', 'A2', 'A3', 'A4', 'A5', 'A10', 'A11', 'A12'], 'A7', 'down', 'A10'],
    [['A1', 'A2', 'A3', 'A4', 'A5', 'A10', 'A11', 'A12'], 'A7', 'up', 'A5'],
    [['A10'], 'A1', 'down', 'A10'],
    // A filled cell with a gap after it: across the gap, not to the edge.
    [['A1', 'A3'], 'A1', 'down', 'A3'],
    [['A1', 'A3'], 'A3', 'up', 'A1'],
    // A lone filled cell at the top: up goes nowhere.
    [['A1'], 'A1', 'up', 'A1'],
    // Along a row, the same rules.
    [['A1', 'B1', 'C1', 'E1'], 'A1', 'right', 'C1'],
    [['A1', 'B1', 'C1', 'E1'], 'C1', 'right', 'E1'],
    [['A1', 'B1', 'C1', 'E1'], 'E1', 'right', 'XFD1'],
    [['A1', 'B1', 'C1', 'E1'], 'E1', 'left', 'C1'],
    [['A1', 'B1', 'C1', 'E1'], 'D1', 'left', 'C1'],
    [['A1', 'B1', 'C1', 'E1'], 'XFD1', 'left', 'E1'],
    // Another column's data does not stop this one.
    [['B3'], 'A1', 'down', 'A1048576'],
    // The last row: a block running to the edge ends there.
    [['A1048575', 'A1048576'], 'A1048575', 'down', 'A1048576'],
    [['A1048576'], 'A1', 'down', 'A1048576'],
];

describe('SheetEdges', () => {
    it.each(excelTable)('ADR-0050 item 2: over %j, Ctrl+arrow from %s %s stops at %s, where Excel stops', (filled, from, direction, expected) => {
        expect.hasAssertions();
        expect(SheetEdges.of(sheetWith(...filled)).find(CellAddress.parse(from), direction).toString()).toBe(expected);
    });

    it('ADR-0050 item 2: formatting alone does not fill a cell; a Formula showing empty text does', () => {
        expect.hasAssertions();
        const sheet = sheetWith('A1', 'A2');
        sheet.workbook.do(sheet.name, SheetEdit.setNumberFormat(CellRange.parse('A3'), NumberFormat.parse('0.00')));
        enter(sheet, 'A5', '=""');
        const edges = SheetEdges.of(sheet);
        expect(edges.find(CellAddress.parse('A1'), 'down').toString()).toBe('A2');
        expect(edges.find(CellAddress.parse('A2'), 'down').toString()).toBe('A5');
    });

    it('ADR-0050 item 2: the answer follows every change the Sheet reports — an entry, a clearing, an insertion', () => {
        expect.hasAssertions();
        const sheet = sheetWith('A1', 'A2');
        const edges = SheetEdges.of(sheet);
        const apply = (edit: SheetEdit): void => edges.update(sheet, sheet.workbook.do(sheet.name, edit).change.for(sheet));

        apply(SheetEdit.enter(CellAddress.parse('A3'), 'x'));
        expect(edges.find(CellAddress.parse('A1'), 'down').toString()).toBe('A3');

        apply(SheetEdit.enter(CellAddress.parse('A2'), ''));
        expect(edges.find(CellAddress.parse('A1'), 'down').toString()).toBe('A3');
        expect(edges.find(CellAddress.parse('A3'), 'up').toString()).toBe('A1');

        apply(SheetEdit.insertRows(0));
        expect(edges.find(CellAddress.parse('A1'), 'down').toString()).toBe('A2');
        expect(edges.find(CellAddress.parse('A2'), 'down').toString()).toBe('A4');
    });
});
