// A Sheet's Selection Summary cells beside Excel's status bar, as the seventeenth Windows run read them
// (verification/2026-10-11-windows-17/report.md, Part B's b5 to b7; ADR-0130, SM-7), ported from the cell facts of
// tests/ExSheet.Components/SheetSummaryTests.cs. The figures themselves are folded by the family's one definition,
// which @ex-grid/core does not export yet, so these hold which cells are read and how a refused one declines.
import { describe, expect, it } from 'vitest';
import type { GridSummaryRequest } from '@ex-grid/core';
import { CellAddress, Culture, SheetEdit, Workbook, type Sheet } from '@ex-grid/sheet-engine';
import { sheetSummaryCells, sheetSummaryDeclined, sheetWords, SheetWords } from '../src/index';

// A Formula ExSheet refuses because it cannot answer it as Excel does (MROUND-001): its cell is #VALUE!.
const refusing = '=MROUND(6.05,0.1)';

function sheetOf(...cells: readonly (readonly [string, string])[]): Sheet {
    const workbook = new Workbook(Culture.of('en-US'));
    for (const [address, typed] of cells) workbook.do('Sheet1', SheetEdit.enter(CellAddress.parse(address), typed));
    return workbook.shown;
}

/** A request over columns A and B, rows `top` to `bottom` from 1, its Focus at the first cell. */
function request(range: string, columns: readonly string[] = ['A', 'B']): GridSummaryRequest {
    const [first = '', last = first] = range.split(':');
    const a = CellAddress.parse(first);
    const b = CellAddress.parse(last);
    return {
        ranges: [{ topRow: a.row, leftColumn: a.column, rowCount: b.row - a.row + 1, columnCount: b.column - a.column + 1 }],
        columns,
        rowSequenceVersion: 0,
        figures: ['average', 'count', 'numericalCount', 'min', 'max', 'sum'],
        focus: { row: a.row, column: a.column },
    };
}

describe('sheetSummaryCells', () => {
    it('ADR-0130 / SM-7: an Error Value among the cells is counted apart from the numbers (run 17, b5)', () => {
        expect.hasAssertions();
        const cells = sheetSummaryCells(sheetOf(['A1', '1'], ['A2', '2'], ['A3', '=1/0'], ['A4', 'x']), request('A1:A5'));
        expect(cells).toMatchObject({ kind: 'cells', numbers: [1, 2], errors: 1, others: 1 });
    });

    it('ADR-0130: a Boolean is counted and not numerically; a date is a number (run 17, b7)', () => {
        expect.hasAssertions();
        const cells = sheetSummaryCells(sheetOf(['A1', '1'], ['A2', 'TRUE'], ['A3', '2026-10-11'], ['A4', 'x']), request('A1:A4'));
        expect(cells).toMatchObject({ kind: 'cells', numbers: [1, 46306], errors: 0, others: 2 });
        expect(cells.kind === 'cells' ? cells.focus?.toString() : undefined).toBe('A1');
    });

    it('ADR-0130 (2026-10-13, decided with the user): a refused cell among the cells declines, naming it and what it shows', () => {
        expect.hasAssertions();
        const cells = sheetSummaryCells(sheetOf(['A1', '1'], ['A2', '2'], ['A3', refusing], ['A4', 'x']), request('A1:A4'));
        expect(cells.kind).toBe('refused');
        if (cells.kind === 'refused') {
            expect(sheetSummaryDeclined(cells, SheetWords.english)).toEqual({
                kind: 'declined',
                reason: 'No figures: ExSheet refused the Formula in A3 with #VALUE! because it cannot answer it as Excel does, so a figure over it would not be Excel\'s.',
            });
        }
    });

    it('ADR-0130, ADR-0047 (2026-10-13): a refused #NAME? declines, and the line names what the cell shows', () => {
        expect.hasAssertions();
        const cells = sheetSummaryCells(sheetOf(['A1', '1'], ['A2', '=NPV(0.1,1,2)']), request('A1:A2'));
        expect(cells).toMatchObject({ kind: 'refused', shown: '#NAME?' });
    });

    it('ADR-0130 (2026-10-13): the first refused cell in reading order is named, and the cells come back once none is left', () => {
        expect.hasAssertions();
        const sheet = sheetOf(['B2', refusing], ['A3', '=B2+1'], ['A1', '4']);
        const named = (range: string): string | undefined => {
            const cells = sheetSummaryCells(sheet, request(range));
            return cells.kind === 'refused' ? cells.at.toString() : undefined;
        };
        expect(named('A1:B3')).toBe('B2');
        expect(named('A1:A3')).toBe('A3');
        sheet.workbook.do('Sheet1', SheetEdit.enter(CellAddress.parse('B2'), '6'));
        const cells = sheetSummaryCells(sheet, request('A1:A3'));
        expect(cells.kind).toBe('cells');
        if (cells.kind === 'cells') expect([...cells.numbers].toSorted((a, b) => a - b)).toEqual([4, 7]);
    });

    it('ADR-0130 (2026-10-13): the reason is worded through the Consumer\'s label, so a Consumer words it in its own language', () => {
        expect.hasAssertions();
        const cells = sheetSummaryCells(sheetOf(['A1', '1'], ['A2', refusing]), request('A1:A2'));
        const words = sheetWords({
            label: id => (id === 'summaryRefused' ? '集計なし: {0} の数式は Excel と同じ答えを出せないため、ExSheet が {1} で拒否しました。' : undefined),
        });
        if (cells.kind === 'refused') {
            expect(sheetSummaryDeclined(cells, words)).toEqual({
                kind: 'declined', reason: '集計なし: A2 の数式は Excel と同じ答えを出せないため、ExSheet が #VALUE! で拒否しました。',
            });
        }
        expect(cells.kind).toBe('refused');
    });

    it('ADR-0130: a column the request does not name is not read', () => {
        expect.hasAssertions();
        const cells = sheetSummaryCells(sheetOf(['A1', '1'], ['B1', '2']), request('A1:B1', ['A']));
        expect(cells).toMatchObject({ kind: 'cells', numbers: [1] });
    });
});
