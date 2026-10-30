// What the shared suggestion cases (csv-suggest-cases.test.ts) cannot say, because it is the Script Core's own:
// the forms a file arrives in, how much of a Blob is read, cancellation, and a suggestion handed back to the
// reader (ADR-0064, Q33).
import { describe, expect, it } from 'vitest';
import { readCsvSnapshot, suggestCsvSchema } from '../src/index';

const encoder = new TextEncoder();

/** A Blob that counts the bytes read from it. */
function countingBlob(bytes: Uint8Array<ArrayBuffer>): { readonly blob: Blob; readonly read: () => number } {
    let read = 0;
    const blob = new Blob([bytes]);
    const slice = blob.slice.bind(blob);
    blob.slice = (start?: number, end?: number) => {
        const piece = slice(start, end);
        read += piece.size;
        return piece;
    };
    return { blob, read: () => read };
}

describe('suggestCsvSchema', () => {
    it('ADR-0064: a file is suggested from alike as a Uint8Array, an ArrayBuffer and a Blob', async () => {
        const bytes = encoder.encode('Desk,Qty,When\nFX,5,2026-09-30\nRates,"1,234",2026-10-01\n');
        const expected = await suggestCsvSchema(bytes);
        expect(await suggestCsvSchema(bytes.slice().buffer)).toEqual(expected);
        expect(await suggestCsvSchema(new Blob([bytes]))).toEqual(expected);
    });

    it('ADR-0064 (Q33): a Blob is read only as far as the rows sampled', async () => {
        const bytes = encoder.encode(`Id,Desk\n${'12345,D2\n'.repeat(200_000)}`);
        const { blob, read } = countingBlob(bytes);
        const suggestion = await suggestCsvSchema(blob);
        expect(suggestion.sampledRows).toBe(1000);
        expect(read()).toBe(1 << 16);
    });

    it('ADR-0064: a file in no form a CSV is read from is refused as an argument', async () => {
        await expect(suggestCsvSchema('A\n1\n' as never)).rejects.toThrow(TypeError);
    });

    it('ADR-0064: a culture that is not a language tag is refused as an argument', async () => {
        await expect(suggestCsvSchema(encoder.encode('A\n1\n'), { culture: 'not a tag' })).rejects.toThrow("The suggestion reads in 'not a tag', which is not a BCP 47 language tag.");
    });

    it('ADR-0064: a suggestion asked for with a cancelled signal reads nothing', async () => {
        const controller = new AbortController();
        const reason = new Error('stopped');
        controller.abort(reason);
        const { blob, read } = countingBlob(encoder.encode('A\n1\n'));
        await expect(suggestCsvSchema(blob, { signal: controller.signal })).rejects.toBe(reason);
        expect(read()).toBe(0);
    });

    it('ADR-0064: a suggestion cancelled while its Blob is read rejects with the reason', async () => {
        const controller = new AbortController();
        const reason = new Error('stopped');
        const blob = new Blob([encoder.encode('A\n1\n')]);
        const slice = blob.slice.bind(blob);
        blob.slice = (start?: number, end?: number) => {
            controller.abort(reason);
            return slice(start, end);
        };
        await expect(suggestCsvSchema(blob, { signal: controller.signal })).rejects.toBe(reason);
    });

    it('ADR-0064 (Q33, DA-9): a suggestion handed back reads the file, as strictly as any Schema', async () => {
        const rows = Array.from({ length: 1200 }, (_, i) => `${i + 1},D${(i + 1) % 3},${i + 1 === 1100 ? 'lots' : '5'}\n`).join('');
        const file = encoder.encode(`Id,Desk,Qty\n${rows}`);
        const suggestion = await suggestCsvSchema(file);
        expect(suggestion.schema.columns.map(column => column.kind)).toEqual(['integer', 'text', 'integer']);
        await expect(readCsvSnapshot(suggestion.schema, file)).rejects.toThrow("Row 1,100, column 'Qty': 'lots' is not an integer (line 1,101).");
        const changed = { ...suggestion.schema, columns: suggestion.schema.columns.map(column => (column.name === 'Qty' ? { ...column, kind: 'text' as const } : column)) };
        expect((await readCsvSnapshot(changed, file)).rowCount).toBe(1200);
    });
});
