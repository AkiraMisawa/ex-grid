// What the shared CSV cases (csv-read-cases.test.ts) cannot say, because it is the Script Core's own: the forms a
// file arrives in, the pacing, and the readings ExGrid.Data takes from .NET that the Script Core refuses
// rather than reads differently (ADR-0064).
import { describe, expect, it } from 'vitest';
import { readCsvSnapshot, type CsvSchema, type SnapshotProgress } from '../src/index';

const schema: CsvSchema = { columns: [{ name: 'A', kind: 'text' }, { name: 'B', kind: 'integer' }] };
const text = 'A,B\nx,1\ny,2\n';

function values(snapshot: Awaited<ReturnType<typeof readCsvSnapshot>>): unknown[] {
    return Array.from({ length: snapshot.rowCount }, (_, row) => snapshot.columns.map(column => column.valueAt(row)));
}

describe('readCsvSnapshot', () => {
    it('ADR-0064: a file is read alike from a Uint8Array, an ArrayBuffer and a Blob', async () => {
        const bytes = new TextEncoder().encode(text);
        const expected = [['x', 1n], ['y', 2n]];
        expect(values(await readCsvSnapshot(schema, bytes))).toEqual(expected);
        expect(values(await readCsvSnapshot(schema, bytes.slice().buffer))).toEqual(expected);
        expect(values(await readCsvSnapshot(schema, new Blob([bytes])))).toEqual(expected);
    });

    it('ADR-0064: a file in no form a CSV is read from is refused as an argument', async () => {
        await expect(readCsvSnapshot(schema, text as never)).rejects.toThrow(TypeError);
    });

    it('ADR-0064: a read reports its rows and bytes after every slice, and once more at the end with every row and byte', async () => {
        const reports: SnapshotProgress[] = [];
        const file = new TextEncoder().encode(`A,B\n${'x,1\n'.repeat(3000)}`);
        let yields = 0;
        const snapshot = await readCsvSnapshot(schema, file, {
            sliceBudgetMs: 0,
            onProgress: progress => reports.push(progress),
            yield: () => {
                yields++;
                return Promise.resolve();
            },
        });
        expect(snapshot.rowCount).toBe(3000);
        expect(yields).toBeGreaterThan(1);
        expect(reports.at(-1)).toEqual({ rows: 3000, totalRows: 3000, bytes: file.length, totalBytes: file.length });
        expect(reports.slice(0, -1).every(report => report.totalRows === undefined && report.totalBytes === file.length)).toBe(true);
        expect(reports.map(report => report.rows)).toEqual(reports.map(report => report.rows).toSorted((a, b) => a - b));
    });

    it('ADR-0064: a cancelled read rejects with the signal\'s reason and yields nothing', async () => {
        const controller = new AbortController();
        const reason = new Error('stopped');
        const file = new TextEncoder().encode(`A,B\n${'x,1\n'.repeat(3000)}`);
        const read = readCsvSnapshot(schema, file, {
            sliceBudgetMs: 0,
            yield: () => {
                controller.abort(reason);
                return Promise.resolve();
            },
            signal: controller.signal,
        });
        await expect(read).rejects.toBe(reason);
    });

    it('ADR-0064: a date in a calendar the Script Core does not keep is refused before a byte is read', async () => {
        const persian: CsvSchema = { columns: [{ name: 'V', kind: 'date', culture: 'fa-IR', dateFormats: ['yyyy/MM/dd'] }] };
        await expect(readCsvSnapshot(persian, new Uint8Array(0))).rejects.toThrow(/fa-IR', which writes its dates in the persian calendar/);
    });

    it('ADR-0064: a standard date format is read only in the invariant culture, and the era not at all', async () => {
        const standard: CsvSchema = { columns: [{ name: 'V', kind: 'date', culture: 'de-DE', dateFormats: ['d'] }] };
        await expect(readCsvSnapshot(standard, new Uint8Array(0))).rejects.toThrow(TypeError);
        const era: CsvSchema = { columns: [{ name: 'V', kind: 'date', dateFormats: ['yyyy-MM-dd g'] }] };
        await expect(readCsvSnapshot(era, new Uint8Array(0))).rejects.toThrow(TypeError);
    });

    it('ADR-0064: a culture that is not a language tag is refused before a byte is read', async () => {
        const unknown: CsvSchema = { columns: [{ name: 'V', kind: 'decimal', culture: 'not a tag' }] };
        await expect(readCsvSnapshot(unknown, new Uint8Array(0))).rejects.toThrow("Column 'V' reads in 'not a tag', which is not a BCP 47 language tag.");
    });
});
