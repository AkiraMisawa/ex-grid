// What a case cannot hold (ADR-0330 §6, §7, §9): the culture and the options an application passes beside the file, a save's
// snapshot taken when the call is made, its pacing, progress and cancellation, a Linked Table of as many rows as a worksheet
// holds, every width to the hundredth, and the writer's list of newer functions against every function the engine declares.
// What a workbook carries, and what is refused, is held by the shared cases (sheet-xlsx-cases.test.ts).
import { describe, expect, it } from 'vitest';
import { CellAddress, Culture, DeclaredFunction, Entry, SheetEdit, Value, Workbook } from '@ex-grid/sheet-engine';
import { createXlsxWriter, openXlsx, type XlsxProgress } from '@ex-grid/data-xlsx';
import { textsOf } from '../../data-xlsx/test/xlsx-case-io';
import { maxLinkedTableRows, openXlsxWorkbook, saveXlsxWorkbook, XlsxSaveError, type XlsxSaveProgress } from '../src/index';
import { widthFromFile, widthToFile } from '../src/column-width-unit';
import { isNewerFunction } from '../src/written-formula';

const enUs = Culture.of('en-US');

/** A file of one worksheet holding the numbers 0 to `rows` - 1 down column A. */
async function numbersFile(rows: number): Promise<Blob> {
    const writer = createXlsxWriter();
    const sheet = writer.addSheet('Sheet1');
    for (let r = 0; r < rows; r++) sheet.write({ row: r, column: 0, type: 'number', value: String(r) });
    return writer.complete();
}

/** A Workbook of `rows` numbers in column A, and a Formula over them in B1. */
function numbers(rows: number): Workbook {
    const workbook = new Workbook(enUs);
    const entries: (readonly [CellAddress, Entry | undefined])[] = [];
    for (let r = 0; r < rows; r++) entries.push([new CellAddress(r, 0), Entry.fromValue(Value.fromNumber(r + 1))]);
    entries.push([new CellAddress(0, 1), Entry.parse(`=SUM(A1:A${String(rows)})`, enUs)]);
    workbook.do('Sheet1', SheetEdit.setEntries(entries));
    return workbook;
}

async function reopened(file: Blob): Promise<Workbook> {
    return Workbook.open((await openXlsxWorkbook(file, enUs)).document);
}

describe('opening', () => {
    it('ADR-0330 §7: a locale the engine holds no data for is refused before the file is read', async () => {
        await expect(openXlsxWorkbook(new Uint8Array([1, 2, 3]), 'zz-ZZ')).rejects.toThrow("The locale 'zz-ZZ' is not one the engine holds data for");
    });

    it('ADR-0330 §9: the file layer\'s options pass through: progress is told, and a cancelled read opens nothing', async () => {
        const file = await numbersFile(5000);
        const told: XlsxProgress[] = [];
        await openXlsxWorkbook(file, enUs, { onProgress: p => told.push(p), sliceBudgetMs: 0 });
        expect(told.some(p => p.cells === 5000)).toBe(true);

        const cancel = new AbortController();
        cancel.abort(new Error('cancelled by the test'));
        await expect(openXlsxWorkbook(file, enUs, { signal: cancel.signal })).rejects.toThrow('cancelled by the test');
    });
});

describe('saving', () => {
    it('ADR-0330 §9: the file is the Workbook as it stood at the call, though the Workbook changes while the write is under way', async () => {
        const workbook = numbers(2000);
        const releases: (() => void)[] = [];
        const held = new Promise<void>(resolve => {
            releases.push(resolve);
        });
        let yields = 0;
        const writing = saveXlsxWorkbook(workbook, {
            sliceBudgetMs: 0,
            yield: async () => {
                if (++yields === 1) await held;
            },
        });
        workbook.do('Sheet1', SheetEdit.setEntries([[new CellAddress(0, 0), Entry.fromValue(Value.fromNumber(1_000_000))]]));
        workbook.do('Sheet1', SheetEdit.setEntries([[new CellAddress(5000, 0), Entry.fromValue(Value.fromNumber(7))]]));
        for (const release of releases) release();
        const sheet = (await reopened(await writing)).sheets[0];

        expect(sheet?.getValue(new CellAddress(0, 0))?.number).toBe(1);
        expect(sheet?.getValue(new CellAddress(5000, 0))).toBeUndefined();
        expect(sheet?.getValue(new CellAddress(0, 1))?.number).toBe(2_001_000);
    });

    it('ADR-0330 §9: the write yields between slices and tells its progress, ending with every element written', async () => {
        const told: XlsxSaveProgress[] = [];
        let yields = 0;
        await saveXlsxWorkbook(numbers(5000), {
            sliceBudgetMs: 0,
            onProgress: p => told.push(p),
            yield: () => {
                yields++;
                return Promise.resolve();
            },
        });

        expect(yields).toBeGreaterThan(10);
        expect(told).toContainEqual({ sheet: -1, sheets: 1, elements: 0, totalElements: 0 });
        const writing = told.filter(p => p.sheet >= 0);
        expect(writing.length).toBeGreaterThan(10);
        expect(writing.every(p => p.sheets === 1 && p.totalElements === 5001)).toBe(true);
        expect(writing.map(p => p.elements)).toStrictEqual(writing.map(p => p.elements).toSorted((a, b) => a - b));
        expect(writing.at(-1)).toStrictEqual({ sheet: 0, sheets: 1, elements: 5001, totalElements: 5001 });
    });

    it('ADR-0330 §9: cancelled while the file is built, the write stops between slices with the signal\'s reason, and returns nothing', async () => {
        const cancel = new AbortController();
        const told: XlsxSaveProgress[] = [];
        const writing = saveXlsxWorkbook(numbers(5000), {
            sliceBudgetMs: 0,
            onProgress: p => told.push(p),
            signal: cancel.signal,
            yield: () => {
                if (told.some(p => p.elements > 1000)) cancel.abort(new Error('cancelled by the test'));
                return Promise.resolve();
            },
        });

        await expect(writing).rejects.toThrow('cancelled by the test');
        expect(Math.max(...told.map(p => p.elements))).toBeLessThan(5001);
    });
});

/** A Workbook whose one Linked Table holds `rows` rows of one column. */
function ids(rows: number): Workbook {
    const workbook = new Workbook(enUs);
    workbook.declareLinkedTable('Ids', ['Id']);
    const one = [Value.fromNumber(1)];
    workbook.pushLinkedTable('Ids', Array.from({ length: rows }, () => one));
    return workbook;
}

/** A Workbook with two Linked Tables, both answered, and Formulas on two Sheets that read them. */
function booked(): Workbook {
    const workbook = new Workbook(enUs, ['Trades', 'Summary']);
    workbook.declareLinkedTable('Positions', ['Desk', 'PV', 'Live', 'Total#'], 'Desk');
    workbook.declareLinkedTable('Rates', ['Ccy', 'Rate']);
    workbook.pushLinkedTable('Positions', [
        [Value.fromText('EMEA'), Value.fromNumber(1_000_000.25), Value.fromBoolean(true), Value.fromNumber(1)],
        [Value.fromText('APAC'), Value.fromNumber(-250), undefined, Value.fromError('#N/A')],
        [Value.fromText('0042'), undefined, Value.fromBoolean(false), Value.fromNumber(2)],
    ]);
    workbook.pushLinkedTable('Rates', [[Value.fromText('EUR'), Value.fromNumber(1.08)]]);
    for (const [at, typed] of [['A1', '=SUM(Positions[PV])'], ['A2', '=XLOOKUP("APAC",positions[desk],Positions[[PV]])'], ['A3', '=SUM(Positions[[Total\'#]])'], ['A4', '=Rates[Rate]*2']] as const) {
        workbook.do('Trades', SheetEdit.enter(CellAddress.parse(at), typed));
    }
    workbook.do('Summary', SheetEdit.enter(CellAddress.parse('B2'), '=COUNTA(Positions[Desk])&" desks"'));
    return workbook;
}

describe('Linked Tables', () => {
    it('ADR-0330 §6: the rows written are the snapshot as it stands when the call is made, not one pushed while the file is built', async () => {
        const workbook = booked();
        const writing = saveXlsxWorkbook(workbook);
        workbook.pushLinkedTable('Rates', [[Value.fromText('GBP'), Value.fromNumber(1.27)], [Value.fromText('JPY'), Value.fromNumber(0.0067)]]);
        const file = await writing;

        const parts = new Map(await textsOf(file));
        expect(parts.get('xl/tables/table2.xml')).toMatch(/ ref="A1:B2"/);
        const reader = await openXlsx(file);
        const rates = await reader.readSheet(3);
        expect(rates.cells.find(c => c.row === 1 && c.column === 0)).toMatchObject({ type: 'sharedString', text: 'EUR' });
        const trades = await reader.readSheet(0);
        expect(trades.cells.find(c => c.row === 3 && c.column === 0)?.value).toBe('2.16');
    });

    it('ADR-0330 §6: a Linked Table of more rows than a worksheet holds under its header is refused by name; one of exactly as many is written', { timeout: 120_000 }, async () => {
        const refused = saveXlsxWorkbook(ids(maxLinkedTableRows + 1), { maxCells: Number.MAX_SAFE_INTEGER });
        await expect(refused).rejects.toThrow(XlsxSaveError);
        await expect(refused).rejects.toMatchObject({ reason: 'linkedTable', message: expect.stringContaining('1,048,576') as unknown });

        const file = await saveXlsxWorkbook(ids(maxLinkedTableRows), { maxCells: Number.MAX_SAFE_INTEGER });
        const parts = new Map(await textsOf(file));
        expect(parts.get('xl/tables/table1.xml')).toMatch(/ ref="A1:A1048576"/);
    });
});

describe('the unit and the lists', () => {
    it('ADR-0330 §4: every width to the hundredth, from 0.01 to 255, reads back as it was written', () => {
        const wrong: number[] = [];
        for (let hundredths = 1; hundredths <= 25_500; hundredths++) {
            const characters = hundredths / 100;
            if (widthFromFile(widthToFile(characters)) !== characters) wrong.push(characters);
        }
        expect(wrong).toStrictEqual([]);
    });

    it('ADR-0330 §4: every function the engine declares that Excel stores with a prefix is in the writer\'s list', () => {
        expect(DeclaredFunction.all.map(f => f.name).filter(isNewerFunction).toSorted()).toStrictEqual([
            'ACOT', 'ACOTH', 'AGGREGATE', 'CEILING.MATH', 'CONCAT', 'COT', 'COTH', 'CSC', 'CSCH', 'DAYS', 'FILTER', 'FLOOR.MATH', 'IFNA',
            'IFS', 'LET', 'MAXIFS', 'MINIFS', 'NUMBERVALUE', 'RANK.EQ', 'SEC', 'SECH', 'SEQUENCE', 'SORT', 'SORTBY', 'STDEV.P', 'STDEV.S',
            'SWITCH', 'TEXTJOIN', 'UNIQUE', 'VAR.P', 'VAR.S', 'XLOOKUP', 'XMATCH', 'XOR',
        ]);
    });

    /** Excel 2007's functions the engine declares, which a file stores with no prefix. */
    const excel2007 = new Set([
        'ABS', 'ACOS', 'ACOSH', 'AND', 'ASIN', 'ASINH', 'ATAN', 'ATAN2', 'ATANH', 'AVERAGE', 'AVERAGEIF', 'AVERAGEIFS', 'CHOOSE',
        'COLUMN', 'COLUMNS', 'CONCATENATE', 'COS', 'COSH', 'COUNT', 'COUNTA', 'COUNTBLANK', 'COUNTIF', 'COUNTIFS', 'DATE', 'DATEDIF',
        'DAY', 'DEGREES', 'EDATE', 'EOMONTH', 'ERROR.TYPE', 'EXACT', 'EXP', 'FIND', 'FV', 'HLOOKUP', 'HOUR', 'IF', 'IFERROR',
        'INDEX', 'INDIRECT', 'INT', 'ISBLANK', 'ISERROR', 'ISNA', 'ISNUMBER', 'ISTEXT', 'LARGE', 'LEFT', 'LEN', 'LN', 'LOG', 'LOG10',
        'LOWER', 'MATCH', 'MAX', 'MEDIAN', 'MID', 'MIN', 'MINUTE', 'MOD', 'MONTH', 'MROUND', 'NA', 'NETWORKDAYS', 'NOT', 'NOW',
        'OFFSET', 'OR', 'PI', 'PMT', 'POWER', 'PRODUCT', 'PROPER', 'PV', 'RADIANS', 'RAND', 'RANDBETWEEN', 'REPLACE', 'REPT',
        'RIGHT', 'ROUND', 'ROUNDDOWN', 'ROUNDUP', 'ROW', 'ROWS', 'SEARCH', 'SECOND', 'SIGN', 'SIN', 'SINH', 'SMALL', 'SQRT',
        'SUBSTITUTE', 'SUBTOTAL', 'SUM', 'SUMIF', 'SUMIFS', 'SUMPRODUCT', 'TAN', 'TANH', 'TEXT', 'TIME', 'TODAY', 'TRANSPOSE',
        'TRIM', 'TRUNC', 'UPPER', 'VALUE', 'VLOOKUP', 'WEEKDAY', 'WORKDAY', 'XNPV', 'YEAR', 'YEARFRAC',
    ]);

    it('ADR-0330 §4: every function the engine declares is either one of Excel 2007\'s, written bare, or a newer one, written with its prefix', () => {
        const misplaced = DeclaredFunction.all.map(f => f.name).filter(name => excel2007.has(name) === isNewerFunction(name));
        expect(misplaced).toStrictEqual([]);
    });
});
