// Layer 1 (ADR-0350 section 13): ExSheet's Find step and Replace plan (ADR-0055, ADR-0332 §2), the counterpart of C#'s
// SheetFind. A step finds by the text a cell is shown with; a Replace matches the Entry, a Formula's text.
import { describe, expect, it } from 'vitest';
import type { GridFindRequest } from '@ex-grid/core';
import { CellAddress, Culture, SheetEdit, Workbook, type Sheet } from '@ex-grid/sheet-engine';
import { planSheetReplace, positionsOf, sheetFindStep } from '../src/sheet-find';
import { SheetSource } from '../src/sheet-source';

const columns = ['A', 'B', 'C'];
const accept = (): undefined => undefined;

function sheetOf(entries: Readonly<Record<string, string>>): Sheet {
    const workbook = new Workbook(Culture.of('en-US'));
    for (const [address, typed] of Object.entries(entries)) workbook.do('Sheet1', SheetEdit.enter(CellAddress.parse(address), typed));
    const sheet = workbook.find('Sheet1');
    if (sheet === undefined) throw new Error('The Workbook holds Sheet1.');
    return sheet;
}

function request(text: string, init: Partial<GridFindRequest> = {}): GridFindRequest {
    return { text, matchCase: false, wholeCell: false, backward: false, columns, rowSequenceVersion: 0, ...init };
}

describe('sheetFindStep (ADR-0055)', () => {
    it('ADR-0055: a step finds the text a cell is shown with, a Formula\'s Value, never its Entry', () => {
        const sheet = sheetOf({ A1: 'apple', B2: '=1+1', C3: 'Pear' });
        expect(sheetFindStep(sheet, request('2'))).toEqual({ found: true, row: 1, column: 'B' });
        expect(sheetFindStep(sheet, request('1+1'))).toEqual({ found: false });
        expect(sheetFindStep(sheet, request('pear'))).toEqual({ found: true, row: 2, column: 'C' });
        expect(sheetFindStep(sheet, request('pear', { matchCase: true }))).toEqual({ found: false });
        expect(sheetFindStep(sheet, request('ear', { wholeCell: true }))).toEqual({ found: false });
    });

    it('ADR-0055: from the cell after the Focus by rows, wrapping, the Focus\'s own cell last; backward the other way; inside the scope', () => {
        const sheet = sheetOf({ A1: 'x', C1: 'x', B3: 'x' });
        expect(sheetFindStep(sheet, request('x', { from: { row: 0, column: 0 } }))).toEqual({ found: true, row: 0, column: 'C' });
        expect(sheetFindStep(sheet, request('x', { from: { row: 2, column: 1 } }))).toEqual({ found: true, row: 0, column: 'A' });
        expect(sheetFindStep(sheet, request('x', { from: { row: 0, column: 0 }, backward: true }))).toEqual({ found: true, row: 2, column: 'B' });
        const alone = sheetOf({ B2: 'only' });
        expect(sheetFindStep(alone, request('only', { from: { row: 1, column: 1 } }))).toEqual({ found: true, row: 1, column: 'B' });
        const scope = [{ topRow: 1, leftColumn: 0, rowCount: 3, columnCount: 3 }];
        expect(sheetFindStep(sheet, request('x', { from: { row: 0, column: 0 }, scope }))).toEqual({ found: true, row: 2, column: 'B' });
    });
});

describe('planSheetReplace (ADR-0332 §2)', () => {
    it('ADR-0332 §2: a Replace matches the Entry, a Formula\'s text, by rows; a replaced Formula that cannot be read refuses the plan, naming the cell', () => {
        const sheet = sheetOf({ A1: '=SUM(B1:B2)', B1: 'SUMMER', A2: '=1+2' });
        const plan = planSheetReplace(sheet, { text: 'sum', matchCase: false, wholeCell: false }, 'MAX', positionsOf(columns), undefined, accept);
        expect(plan.writes.map(write => [write.address.toString(), write.text])).toEqual([['A1', '=MAX(B1:B2)'], ['B1', 'MAXMER']]);
        const refusing = planSheetReplace(sheet, { text: '+', matchCase: false, wholeCell: false }, '+)', positionsOf(columns), undefined,
            (address, text) => (text.startsWith('=') ? `unreadable at ${address.toString()}` : undefined));
        expect(refusing.refused.map(cell => [cell.address.toString(), cell.position, cell.reason])).toEqual([['A2', 0, 'unreadable at A2']]);
    });
});

describe('SheetSource.find (ADR-0055)', () => {
    it('ADR-0055: a step asked under the version before is answered from the Sheet shown then, which the grid still paints; an older one is given up, never "not found"', async () => {
        const first = sheetOf({ A1: 'here' });
        const second = sheetOf({ B2: 'there' });
        const source = new SheetSource(first);
        const signal = new AbortController().signal;
        source.show(second);
        await expect(source.find(request('here', { rowSequenceVersion: 0 }), signal)).resolves.toEqual({ found: true, row: 0, column: 'A' });
        await expect(source.find(request('there', { rowSequenceVersion: 1 }), signal)).resolves.toEqual({ found: true, row: 1, column: 'B' });
        source.show(first);
        await expect(source.find(request('here', { rowSequenceVersion: 0 }), signal)).rejects.toMatchObject({ name: 'AbortError' });
    });
});
