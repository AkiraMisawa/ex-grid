// What the shared cases cannot hold, as ExGrid.Data.Xlsx's StreamingTests, RoundTripTests and TableTests hold it in C#
// (ADR-0330 §1, §6, §9): a large worksheet read a batch at a time, through text that runs past what the reader holds;
// the read paced, reported and cancelled between slices; a file given as a Blob; the writer refusing past its size limit
// as it writes; and a table's columns taken when it is added.
import { describe, expect, it } from 'vitest';
import { XlsxError, batchSize, createXlsxWriter, openXlsx, type XlsxCellInput, type XlsxProgress, type XlsxReadOptions } from '../src/index';

const rows = 25_000;

function* cells(): Generator<XlsxCellInput> {
    for (let r = 0; r < rows; r++) {
        const row = String(r + 1);
        yield { row: r, column: 0, type: 'number', value: row };
        yield { row: r, column: 1, type: 'number', value: String(r * 1.25) };
        yield { row: r, column: 2, type: 'number', value: String(-r) };
        yield { row: r, column: 3, type: 'number', value: String(r * 2.25 + 1), formula: { text: `A${row}+B${row}` } };
    }
}

/** A sheet of `rows` rows of three numbers and a Formula: some 3 MiB of XML, more than the reader holds at once. */
async function large(): Promise<Blob> {
    const writer = createXlsxWriter();
    await writer.addSheet('Large').writeAll(cells());
    return writer.complete();
}

/** A sheet of 20,000 cells, each its own text: a shared strings part of 20,000 items. */
async function manyTexts(): Promise<Blob> {
    const writer = createXlsxWriter();
    await writer.addSheet('Texts').writeAll(Array.from({ length: 20_000 }, (_, i) => ({ row: i, column: 0, type: 'sharedString', text: `Text ${String(i)}` }) as const));
    return writer.complete();
}

function counting(): { readonly options: XlsxReadOptions; readonly reports: XlsxProgress[]; readonly yields: () => number } {
    const reports: XlsxProgress[] = [];
    let yields = 0;
    return {
        options: { sliceBudgetMs: 0, onProgress: progress => reports.push(progress), yield: () => { yields++; return Promise.resolve(); } },
        reports,
        yields: () => yields,
    };
}

describe('ADR-0330 §9: a worksheet read as a stream', () => {
    it('ADR-0330 §9: the cells come a batch at a time, in the file\'s order, every one of them', async () => {
        const reader = await openXlsx(await large());
        const sheet = await reader.openSheet(0);
        let batches = 0;
        let count = 0;
        let last = [-1, 3];
        while (await sheet.read()) {
            batches++;
            expect(sheet.cells.length).toBeGreaterThanOrEqual(1);
            expect(sheet.cells.length).toBeLessThanOrEqual(batchSize);
            for (const cell of sheet.cells) {
                const [row = -1, column = -1] = last;
                expect(cell.row > row || (cell.row === row && cell.column > column)).toBe(true);
                last = [cell.row, cell.column];
                count++;
            }
        }
        expect(count).toBe(rows * 4);
        expect(reader.cellsRead).toBe(rows * 4);
        expect(batches).toBeGreaterThanOrEqual(rows * 4 / batchSize);
        expect(await sheet.read()).toBe(false);
        expect(last).toStrictEqual([rows - 1, 3]);
    });

    it('ADR-0330 §9: a cell whose text runs past what the reader holds at once is read whole', async () => {
        // Some 3 MiB of one text, past the mebibyte the reader first takes, and past the room it keeps ahead of a cell.
        const long = 'x'.repeat(3 << 20);
        const writer = createXlsxWriter();
        const sheet = writer.addSheet('Long');
        sheet.write({ row: 0, column: 0, type: 'number', value: '1' });
        sheet.write({ row: 0, column: 1, type: 'inlineString', text: long });
        sheet.write({ row: 1, column: 0, type: 'number', value: '2' });
        const reader = await openXlsx(await writer.complete());

        const read = await reader.readSheet(0);

        expect(read.cells.map(c => (c.text === null ? null : c.text.length))).toStrictEqual([null, long.length, null]);
        expect(read.cells[1]?.text === long).toBe(true);
    });

    it('ADR-0330 §9: the read reports its progress and yields between slices, and its last report is the whole sheet', async () => {
        const { options, reports, yields } = counting();
        const reader = await openXlsx(await large(), options);

        await reader.readSheet(0);

        expect(yields()).toBeGreaterThanOrEqual(rows * 4 / batchSize);
        expect(reports.length).toBeGreaterThan(yields());
        for (const report of reports) expect([report.sheet, report.sheets]).toStrictEqual([0, 1]);
        expect(reports.map(r => r.cells)).toStrictEqual(reports.map(r => r.cells).toSorted((a, b) => a - b));
        expect(reports.at(-1)?.cells).toBe(rows * 4);
        expect(reports.at(-1)?.bytes).toBe(reports.at(-1)?.totalBytes);
    });

    it('ADR-0330 §9: a read is cancelled between slices', async () => {
        const cancel = new AbortController();
        const reader = await openXlsx(await large(), { sliceBudgetMs: 0, signal: cancel.signal, yield: () => { cancel.abort(); return Promise.resolve(); } });

        await expect(reader.readSheet(0)).rejects.toThrow(DOMException);
        expect(reader.cellsRead).toBeLessThan(rows * 4);
    });

    it('ADR-0330 §9: the shared strings, read whole as the workbook opens, are read in slices too', async () => {
        const { options, reports, yields } = counting();

        const reader = await openXlsx(await manyTexts(), options);

        expect(reader.sharedStrings.count).toBe(20_000);
        expect(reader.sharedStrings.texts.at(-1)).toBe('Text 19999');
        expect(yields()).toBeGreaterThanOrEqual(Math.floor(20_000 / 256));
        for (const report of reports) expect([report.sheet, report.sheets, report.cells]).toStrictEqual([-1, 1, 0]);
        expect(reports.map(r => r.bytes)).toStrictEqual(reports.map(r => r.bytes).toSorted((a, b) => a - b));
        expect(reports.at(-1)?.totalBytes).toBeGreaterThan(0);
        expect(reports.at(-1)?.bytes).toBe(reports.at(-1)?.totalBytes);
    });

    it('ADR-0330 §9: opening is cancelled between the shared strings\' slices', async () => {
        const cancel = new AbortController();
        const file = await manyTexts();

        await expect(openXlsx(file, { sliceBudgetMs: 0, signal: cancel.signal, yield: () => { cancel.abort(); return Promise.resolve(); } }))
            .rejects.toThrow(DOMException);
    });

    it('ADR-0330 §1: a file is read alike as a Blob and as its bytes', async () => {
        const blob = await large();
        const fromBlob = await (await openXlsx(blob)).readSheet(0);
        const fromBytes = await (await openXlsx(new Uint8Array(await blob.arrayBuffer()))).readSheet(0);

        expect(fromBlob.cells).toStrictEqual(fromBytes.cells);
        expect(fromBlob.cells.length).toBe(rows * 4);
    });
});

describe('ADR-0330 §1, §6, §9: what the writer refuses as it writes', () => {
    // The writer counts a sheet's text as it hands it to the zip, every 64 KiB, where ExGrid.Data.Xlsx counts it as its XmlWriter
    // flushes: where the refusal surfaces differs, so it is held here and not in the shared cases.
    it('ADR-0330 §9: a workbook whose parts would hold more uncompressed bytes than the limit is refused by name, and the writer is then done', async () => {
        const writer = createXlsxWriter({ maxUncompressedBytes: 2_000 });
        const sheet = writer.addSheet('One');
        let refused: unknown;
        try {
            for (let row = 0; row < 10_000; row++) sheet.write({ row, column: 0, type: 'number', value: '1' });
        } catch (error) { // oxlint-disable-line ex-grid/no-catch -- the refusal is what the test reads
            refused = error;
        }

        expect(refused).toBeInstanceOf(XlsxError);
        const error = refused as XlsxError;
        expect([error.reason, error.part, error.sheet]).toStrictEqual(['tooLarge', 'xl/worksheets/sheet1.xml', 'One']);
        expect(error.message).toBe("Sheet 'One': The workbook would hold more than the 2,000 uncompressed bytes a file is written with (XlsxWriteOptions.MaxUncompressedBytes).");
        expect(() => sheet.write({ row: 50_000, column: 0, type: 'number', value: '1' })).toThrow(Error);
        await expect(writer.complete()).rejects.toThrow(Error);
    });

    it('ADR-0330 §6: a table\'s columns are taken when it is added, so a list changed afterwards changes nothing', async () => {
        const columns = ['Desk', 'PV'];
        const writer = createXlsxWriter();
        const sheet = writer.addSheet('Sheet1');
        sheet.addTable({ name: 'T', headerRow: 0, firstColumn: 0, columns, dataRows: 1 });
        columns[1] = 'Changed';
        sheet.write({ row: 0, column: 0, type: 'sharedString', text: 'Desk' });
        expect(() => sheet.write({ row: 0, column: 1, type: 'sharedString', text: 'Changed' })).toThrow(TypeError);
        sheet.write({ row: 0, column: 1, type: 'sharedString', text: 'PV' });

        const read = await (await openXlsx(await writer.complete())).readSheet(0);

        expect(read.cells.map(c => c.text)).toStrictEqual(['Desk', 'PV']);
    });
});
