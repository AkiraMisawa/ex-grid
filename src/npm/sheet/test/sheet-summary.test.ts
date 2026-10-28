// A Sheet's Selection Summary cells beside Excel's status bar, as the seventeenth Windows run read them
// (verification/2026-10-11-windows-17/report.md, Part B's b5 to b7; ADR-0130, SM-7), ported from the cell facts of
// tests/ExSheet.Components/SheetSummaryTests.cs: which cells are read, how a refused one declines, and the figures
// the Sheet Source answers, folded by the family's one definition and written in the Focus cell's Number Format.
import { describe, expect, it } from 'vitest';
import type { GridSummaryRequest, GridSummaryResult } from '@ex-grid/core';
import { CellAddress, Culture, NumberFormat, SheetEdit, Workbook, type Sheet } from '@ex-grid/sheet-engine';
import { sheetSummaryCells, sheetSummaryDeclined, sheetWords, SheetWords } from '../src/index';
import { SheetSource } from '../src/sheet-source';

// A Formula ExSheet refuses because it cannot answer it as Excel does (MROUND-001): its cell is #VALUE!.
const refusing = '=MROUND(6.05,0.1)';

function sheetOf(...cells: readonly (readonly [string, string])[]): Sheet {
    const workbook = new Workbook(Culture.of('en-US'));
    for (const [address, typed] of cells) workbook.do('Sheet1', SheetEdit.enter(CellAddress.parse(address), typed));
    return workbook.shown;
}

/** A request over columns A and B, rows `top` to `bottom` from 1, its Focus at the first cell. */
function request(range: string, columns: readonly string[] = ['A', 'B'], focusAt?: string): GridSummaryRequest {
    const [first = '', last = first] = range.split(':');
    const a = CellAddress.parse(first);
    const b = CellAddress.parse(last);
    const focus = focusAt === undefined ? a : CellAddress.parse(focusAt);
    return {
        ranges: [{ topRow: Math.min(a.row, b.row), leftColumn: a.column, rowCount: Math.abs(b.row - a.row) + 1, columnCount: b.column - a.column + 1 }],
        columns,
        rowSequenceVersion: 0,
        figures: ['average', 'count', 'numericalCount', 'min', 'max', 'sum'],
        focus: { row: focus.row, column: focus.column },
    };
}

// Each figure as the status line shows it: the Sheet's text where it wrote one, else the count's whole number.
function shown(result: GridSummaryResult): readonly string[] {
    if (result.kind !== 'answered') return [`declined: ${result.reason}`];
    return Object.entries(result.figures).flatMap(([figure, answer]) => {
        if (answer.kind !== 'number') return [];
        const text = (result.texts as Readonly<Record<string, string | undefined>>)[figure];
        return [`${figure}: ${text ?? String(answer.number)}`];
    });
}

async function summarised(sheet: Sheet, asked: GridSummaryRequest): Promise<readonly string[]> {
    const source = new SheetSource(sheet, () => SheetWords.english);
    return shown(await source.summarize(asked, new AbortController().signal));
}

describe('SheetSource.summarize (ADR-0130), beside Excel\'s status bar in the seventeenth Windows run', () => {
    it('ADR-0130 / SM-7: an Error Value among the cells leaves Count and Numerical Count, and no other figure (run 17, b5)', async () => {
        const sheet = sheetOf(['A1', '1'], ['A2', '2'], ['A3', '=1/0'], ['A4', 'x']);
        // Excel: "Count: 4    Numerical Count: 2", and nothing else.
        expect(await summarised(sheet, request('A1:A5'))).toEqual(['count: 4', 'numericalCount: 2']);
    });

    it('ADR-0130 / SM-7: the error cleared, every figure is back, in General (run 17, b6)', async () => {
        const sheet = sheetOf(['A1', '1'], ['A2', '2'], ['A4', 'x']);
        // Excel: "Average: 1.5    Count: 3    Numerical Count: 2    Min.: 1    Max.: 2    Sum: 3".
        expect(await summarised(sheet, request('A1:A5'))).toEqual(['average: 1.5', 'count: 3', 'numericalCount: 2', 'min: 1', 'max: 2', 'sum: 3']);
    });

    it('ADR-0130: a Boolean is counted and not numerically; a date is a number (run 17, b7)', async () => {
        const sheet = sheetOf(['A1', '1'], ['A2', 'TRUE'], ['A3', '2026-10-11'], ['A4', 'x']);
        // Excel: "Average: 23153.5    Count: 4    Numerical Count: 2    Min.: 1    Max.: 46306    Sum: 46307".
        expect(await summarised(sheet, request('A1:A4'))).toEqual(['average: 23153.5', 'count: 4', 'numericalCount: 2', 'min: 1', 'max: 46306', 'sum: 46307']);
    });

    it('ADR-0130 / SM-9: every figure but the counts wears the Focus cell\'s format, whatever the other\'s (run 17, b8)', async () => {
        const sheet = sheetOf();
        sheet.workbook.do('Sheet1', SheetEdit.setNumberFormatOf([CellAddress.parse('A1')], NumberFormat.parse('#,##0.00')));
        sheet.workbook.do('Sheet1', SheetEdit.setNumberFormatOf([CellAddress.parse('A2')], NumberFormat.parse('0%')));
        sheet.workbook.do('Sheet1', SheetEdit.enter(CellAddress.parse('A1'), '1000'));
        sheet.workbook.do('Sheet1', SheetEdit.enter(CellAddress.parse('A2'), '2000'));
        // Excel: "Average: 510.00    Count: 2    Numerical Count: 2    Min.: 20.00    Max.: 1,000.00    Sum: 1,020.00".
        expect(await summarised(sheet, request('A1:A2', ['A', 'B'], 'A1')))
            .toEqual(['average: 510.00', 'count: 2', 'numericalCount: 2', 'min: 20.00', 'max: 1,000.00', 'sum: 1,020.00']);
        // Excel: "Average: 51000%    Count: 2    Numerical Count: 2    Min.: 2000%    Max.: 100000%    Sum: 102000%".
        expect(await summarised(sheet, request('A1:A2', ['A', 'B'], 'A2')))
            .toEqual(['average: 51000%', 'count: 2', 'numericalCount: 2', 'min: 2000%', 'max: 100000%', 'sum: 102000%']);
    });

    it('ADR-0130 (2026-10-13, decided with the user): a refused cell declines the whole summary in the Sheet\'s words', async () => {
        const sheet = sheetOf(['A1', '1'], ['A2', refusing]);
        expect(await summarised(sheet, request('A1:A2'))).toEqual([
            'declined: No figures: ExSheet refused the Formula in A2 with #VALUE! because it cannot answer it as Excel does, so a figure over it would not be Excel\'s.',
        ]);
    });

    it('ADR-0130: a summary asked of a Sheet shown before the last one is given up, never answered over cells nobody read', async () => {
        const sheet = sheetOf(['A1', '1'], ['A2', '2']);
        const source = new SheetSource(sheet, () => SheetWords.english);
        source.show(sheetOf());
        source.show(sheet);
        await expect(source.summarize(request('A1:A2'), new AbortController().signal)).rejects.toMatchObject({ name: 'AbortError' });
        expect(shown(await source.summarize({ ...request('A1:A2'), rowSequenceVersion: 2 }, new AbortController().signal))).toEqual([
            'average: 1.5', 'count: 2', 'numericalCount: 2', 'min: 1', 'max: 2', 'sum: 3',
        ]);
    });
});

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
