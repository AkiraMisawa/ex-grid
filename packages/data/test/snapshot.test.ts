// The TypeScript edges the shared cases do not reach: a Snapshot made from a Consumer's values, the text of
// a Decimal and a Date, and how a read paces itself, reports its progress and is cancelled (ADR-0064,
// ADR-0370 section 3).
import { describe, expect, it } from 'vitest';
import {
    SnapshotError, createSnapshot, decimalFromText, decimalText, readArrowSnapshot, ticksFromText, ticksOf, ticksOfTemporal, ticksText,
    writeArrowSnapshot, type Snapshot, type SnapshotProgress,
} from '../src/index';
import { Temporal } from 'temporal-polyfill';

describe('createSnapshot', () => {
    it('ADR-0064: a Snapshot holds each kind, a Blank apart from every value, and Text in the order values first appear', () => {
        const snapshot = createSnapshot({
            version: 3,
            recordKey: 'Id',
            columns: [
                { name: 'Id', kind: 'integer', values: [1, 2n, 3] },
                { name: 'Desk', caption: 'Trading desk', kind: 'text', values: ['FX', null, 'fx'] },
                { name: 'Live', kind: 'boolean', values: [true, null, false] },
            ],
        });
        expect(snapshot.version).toBe(3n);
        expect(snapshot.recordKey).toBe('Id');
        expect(snapshot.rowCount).toBe(3);
        const desk = snapshot.column('Desk');
        expect(desk.caption).toBe('Trading desk');
        expect(desk.kind === 'text' ? desk.dictionary : []).toEqual(['FX', 'fx']);
        expect(desk.isBlank(1)).toBe(true);
        expect(snapshot.column('Id').caption).toBe('Id');
        expect(Object.isFrozen(snapshot)).toBe(true);
    });

    it('ADR-0064: a value of the wrong kind, columns of different lengths and a name empty or twice are refused as TypeErrors', () => {
        expect(() => createSnapshot({ columns: [{ name: 'A', kind: 'double', values: ['1' as never] }] })).toThrow(TypeError);
        expect(() => createSnapshot({ columns: [{ name: 'A', kind: 'integer', values: [1.5] }] })).toThrow(TypeError);
        expect(() => createSnapshot({ columns: [{ name: 'A', kind: 'text', values: ['a'] }, { name: 'B', kind: 'text', values: [] }] })).toThrow(TypeError);
        expect(() => createSnapshot({ columns: [{ name: '', kind: 'text', values: [] }] })).toThrow(TypeError);
        expect(() => createSnapshot({ columns: [{ name: 'A', kind: 'text', values: [] }, { name: 'A', kind: 'text', values: [] }] })).toThrow(TypeError);
        expect(() => createSnapshot({ recordKey: 'B', columns: [{ name: 'A', kind: 'text', values: [] }] })).toThrow(TypeError);
        expect(() => createSnapshot({ recordKey: 'A', columns: [{ name: 'A', kind: 'double', values: [] }] })).toThrow(TypeError);
    });

    it('ADR-0064: a value outside its kind\'s range, and a version below 0, are refused as RangeErrors', () => {
        expect(() => createSnapshot({ columns: [{ name: 'A', kind: 'integer', values: [1n << 63n] }] })).toThrow(RangeError);
        expect(() => createSnapshot({ columns: [{ name: 'A', kind: 'date', values: [-1n] }] })).toThrow(RangeError);
        expect(() => createSnapshot({ version: -1, columns: [] })).toThrow(RangeError);
        expect(() => createSnapshot({ columns: [{ name: 'A', kind: 'decimal', values: [{ mantissa: 1n << 96n, scale: 0 }] }] })).toThrow(TypeError);
    });

    it('ADR-0064: a Record Key Blank or carried twice is refused by its row, as a read refuses it', () => {
        expect(() => createSnapshot({ recordKey: 'Id', columns: [{ name: 'Id', kind: 'text', values: ['a', null] }] }))
            .toThrow(new SnapshotError('the Record Key is Blank.', { row: 2, column: 'Id' }));
        expect(() => createSnapshot({ recordKey: 'Id', columns: [{ name: 'Id', kind: 'integer', values: [5, 6, 5] }] }))
            .toThrow('Row 3, column \'Id\': the Record Key 5 is already carried by row 1.');
    });

    it('ADR-0064: a Date is taken from ticks, or from a Temporal PlainDate or PlainDateTime by its own members', () => {
        const snapshot = createSnapshot({
            columns: [{ name: 'When', kind: 'date', values: [Temporal.PlainDate.from('2026-09-30'), Temporal.PlainDateTime.from('2026-09-30T12:00:00.0000001'), 0n] }],
        });
        const when = snapshot.column('When');
        expect(when.valueAt(0)).toBe(ticksFromText('2026-09-30'));
        expect(when.valueAt(1)).toBe(ticksFromText('2026-09-30T12:00:00.0000001'));
        expect(() => ticksOfTemporal(Temporal.PlainDateTime.from('2026-09-30T12:00:00.000000001'))).toThrow(RangeError);
        expect(ticksOfTemporal(new Date())).toBeUndefined();
    });
});

describe('the text of a Decimal and a Date', () => {
    it('ADR-0064: a Decimal is read from its text canonical, and refused past 28 places or 2^96', () => {
        expect(decimalText(decimalFromText('-1234.5600'))).toBe('-1234.56');
        expect(decimalText(decimalFromText('0.000'))).toBe('0');
        expect(decimalText(decimalFromText('79228162514264337593543950335'))).toBe('79228162514264337593543950335');
        expect(() => decimalFromText('79228162514264337593543950336')).toThrow(RangeError);
        expect(() => decimalFromText('0.00000000000000000000000000001')).toThrow(RangeError);
        expect(() => decimalFromText('1e5')).toThrow(TypeError);
    });

    it('ADR-0064: a Date\'s text is Temporal\'s, its places trimmed, from 0001-01-01 to 9999-12-31', () => {
        expect(ticksText(0n)).toBe('0001-01-01T00:00:00');
        expect(ticksText(ticksOf(9999, 12, 31, 23, 59, 59, 9_999_999))).toBe('9999-12-31T23:59:59.9999999');
        expect(ticksText(ticksFromText('2024-02-29T08:05:03.12'))).toBe('2024-02-29T08:05:03.12');
        expect(() => ticksFromText('2023-02-29')).toThrow(RangeError);
        expect(() => ticksFromText('2023-2-28')).toThrow(TypeError);
        expect(() => ticksText(-1n)).toThrow(RangeError);
    });
});

/** A Snapshot of `rows` rows, one Integer column, written as Arrow. */
function stream(rows: number): Uint8Array {
    return writeArrowSnapshot(createSnapshot({ columns: [{ name: 'N', kind: 'integer', values: Array.from({ length: rows }, (_, i) => i) }] }));
}

describe('readArrowSnapshot', () => {
    it('ADR-0064: a read reports its progress after each slice, and once more at the end with every row', async () => {
        const reports: SnapshotProgress[] = [];
        const payload = stream(70_000);
        const snapshot = await readArrowSnapshot(payload, { sliceBudgetMs: 0, yield: () => Promise.resolve(), onProgress: progress => reports.push(progress) });
        expect(snapshot.rowCount).toBe(70_000);
        expect(reports.length).toBeGreaterThan(2);
        expect(reports.at(-1)).toEqual({ rows: 70_000, totalRows: 70_000, bytes: payload.byteLength, totalBytes: payload.byteLength });
        expect(reports.every(report => report.totalRows === 70_000)).toBe(true);
        expect(reports.map(report => report.rows)).toEqual(reports.map(report => report.rows).toSorted((a, b) => a - b));
    });

    it('ADR-0064: a cancelled read rejects with the signal\'s reason and yields nothing', async () => {
        const controller = new AbortController();
        const reason = new Error('cancelled');
        let slices = 0;
        const read = readArrowSnapshot(stream(70_000), {
            signal: controller.signal,
            sliceBudgetMs: 0,
            yield: () => {
                slices++;
                controller.abort(reason);
                return Promise.resolve();
            },
        });
        await expect(read).rejects.toBe(reason);
        expect(slices).toBe(1);
    });

    it('ADR-0065: a read of something that is not bytes is refused as a TypeError', async () => {
        await expect(readArrowSnapshot('arrow' as never)).rejects.toThrow(TypeError);
    });

    it('ADR-0065: a write refuses a look-alike Snapshot, whose values nothing checked', () => {
        const lookAlike: Snapshot = { version: 0n, recordKey: undefined, rowCount: 0, columns: [], column: () => { throw new RangeError('none'); } };
        expect(() => writeArrowSnapshot(lookAlike)).toThrow(TypeError);
    });

    it('ADR-0065: more rows than a record batch holds are written in batches of 65,536 and read back whole', async () => {
        const snapshot = await readArrowSnapshot(stream(140_000));
        const column = snapshot.column('N');
        expect(snapshot.rowCount).toBe(140_000);
        expect(column.valueAt(65_535)).toBe(65_535n);
        expect(column.valueAt(65_536)).toBe(65_536n);
        expect(column.valueAt(139_999)).toBe(139_999n);
    });
});
