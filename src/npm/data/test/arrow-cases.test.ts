// Runs the shared Arrow cases of tests/cases/arrow/ (ADR-0370, section 3), which xUnit runs against
// ExGrid.Data.Arrow as well: arrowRead reads a stream and holds the Snapshot, or the refusal, to what the
// case says; arrowShape reads a stream's shape; arrowWrite writes a Snapshot, reads it back, and keeps the
// stream it wrote under written/, which the C# runner reads too. tests/cases/README.md says what a file holds.
//
// A written stream is compared byte for byte with the one committed; EXGRID_WRITE_ARROW_CASES=1 writes them
// instead, and the diff is read like any other.
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { describe, expect, it } from 'vitest';
import {
    SnapshotError, decimalText, readArrowShape, readArrowSnapshot, ticksFromText, writeArrowSnapshot, type Snapshot,
} from '../src/index';
import { streamMessages } from '../src/arrow/ipc';
import { fieldTypeName, readSchema } from '../src/arrow/schema';
import { build, doubleBits, isObject, key, kindOf, list, text, textValue, whole, type Json, type JsonObject } from './case-json';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../../../../tests/cases/arrow');
const writeCases = process.env['EXGRID_WRITE_ARROW_CASES'] === '1';

interface CaseFile {
    readonly file: string;
    readonly cases: readonly JsonObject[];
}

function caseFiles(kind: string): CaseFile[] {
    return fs.readdirSync(root)
        .filter(name => name.endsWith('.json'))
        .toSorted()
        .map(name => ({ name, json: JSON.parse(fs.readFileSync(path.join(root, name), 'utf8')) as Json }))
        .filter(({ json }) => key(json, 'kind') === kind)
        .map(({ name, json }) => ({ file: name, cases: list(key(json, 'cases')).filter(isObject) }));
}

/** Holds a Snapshot to what a case expects of it: its version, Record Key, and each column whole. */
function same(expected: Json, snapshot: Snapshot, dictionaries: boolean): void {
    expect(snapshot.version).toBe(whole(key(expected, 'version')));
    const recordKey = key(expected, 'recordKey');
    expect(snapshot.recordKey).toBe(recordKey === null ? undefined : text(recordKey));
    const columns = list(key(expected, 'columns'));
    expect(snapshot.columns.map(column => column.name)).toEqual(columns.map(column => text(key(column, 'name'))));
    columns.forEach((column, c) => {
        const actual = snapshot.columns[c];
        if (actual === undefined) throw new Error(`The Snapshot has no column ${c}.`);
        expect(actual.caption).toBe(text(key(column, 'caption')));
        expect(actual.kind).toBe(kindOf(key(column, 'kind')));
        const values = list(key(column, 'values'));
        expect(snapshot.rowCount).toBe(values.length);
        values.forEach((value, row) => {
            const where = `column '${actual.name}', row ${row}`;
            if (value === null) {
                expect(actual.isBlank(row), where).toBe(true);
                return;
            }
            expect(actual.isBlank(row), where).toBe(false);
            switch (actual.kind) {
                case 'text': expect(actual.valueAt(row), where).toBe(textValue(value)); break;
                case 'decimal': {
                    const decimal = actual.valueAt(row);
                    expect(decimal === null ? null : decimalText(decimal), where).toBe(text(value));
                    break;
                }
                case 'double': expect(actual.bitsAt(row).toString(16), where).toBe(doubleBits(value).toString(16)); break;
                case 'integer': expect(actual.valueAt(row), where).toBe(whole(value)); break;
                case 'date': expect(actual.valueAt(row), where).toBe(ticksFromText(text(value))); break;
                case 'boolean': expect(actual.valueAt(row), where).toBe(value); break;
                default: throw new Error(`'${String(actual satisfies never)}' is not a column.`);
            }
        });
        if (actual.kind === 'text') {
            const dictionary = dictionaries
                ? list(key(column, 'dictionary')).map(textValue)
                : [...new Set(values.filter(value => value !== null).map(textValue))];
            expect(actual.dictionary).toEqual(dictionary);
        }
    });
}

/** The bytes a case names, under tests/cases/arrow. */
function bytes(name: string): Uint8Array {
    return new Uint8Array(fs.readFileSync(path.join(root, name)));
}

/** Every value of a Snapshot, a double by its bits, for two reads to be compared whole. */
function picture(snapshot: Snapshot): unknown[] {
    return snapshot.columns.map(column => {
        const rows = Array.from({ length: snapshot.rowCount }, (_, row) => (column.kind === 'double' ? column.bitsAt(row) : column.valueAt(row)));
        return { name: column.name, caption: column.caption, kind: column.kind, rows, dictionary: column.kind === 'text' ? column.dictionary : null };
    });
}

/** Reads every way a Consumer may hand the bytes over: as they are, as an ArrayBuffer, at an odd offset into
 * a larger buffer, and a chunk at a time with a slice budget of 0. All must agree. */
async function readEveryWay(payload: Uint8Array): Promise<Snapshot> {
    const first = await readArrowSnapshot(payload);
    const larger = new Uint8Array(payload.byteLength + 3);
    larger.set(payload, 3);
    const others = [
        await readArrowSnapshot(payload.slice().buffer),
        await readArrowSnapshot(larger.subarray(3)),
        await readArrowSnapshot(payload, { sliceBudgetMs: 0, yield: () => Promise.resolve() }),
    ];
    for (const other of others) expect(picture(other)).toEqual(picture(first));
    return first;
}

/** What a refusal a case says must hold: its message, whole or in part, and the row, column and key it names. */
function refusalOf(refused: Json): Record<string, unknown> {
    const row = key(refused, 'row');
    const column = key(refused, 'column');
    const saying = isObject(refused) ? refused['saying'] : undefined;
    const found: Record<string, unknown> = {
        message: saying === undefined ? text(key(refused, 'message')) : expect.stringContaining(text(saying)),
        row: row === null ? undefined : row,
        column: column === null ? undefined : column,
    };
    const named = isObject(refused) ? refused['key'] : undefined;
    if (named !== undefined) found['key'] = typeof named === 'number' ? BigInt(named) : named;
    return found;
}

/** Holds what `act` throws, or rejects with, to the refusal a case says. */
async function holdsRefusal(act: () => unknown, refusal: Json): Promise<void> {
    await expect((async () => act())()).rejects.toBeInstanceOf(SnapshotError);
    await expect((async () => act())()).rejects.toMatchObject(refusalOf(refusal));
}

describe('arrowRead', () => {
    for (const { file, cases } of caseFiles('arrowRead')) {
        describe(file, () => {
            for (const entry of cases) {
                it(text(key(entry, 'name')), async () => {
                    expect.hasAssertions();
                    const payload = bytes(text(key(entry, 'stream')));
                    if (entry['refused'] !== undefined) {
                        await holdsRefusal(() => readArrowSnapshot(payload), entry['refused']);
                        await holdsRefusal(() => readArrowSnapshot(payload, { sliceBudgetMs: 0, yield: () => Promise.resolve() }), entry['refused']);
                        return;
                    }
                    same(key(entry, 'expect'), await readEveryWay(payload), true);
                });
            }
        });
    }
});

describe('arrowShape', () => {
    for (const { file, cases } of caseFiles('arrowShape')) {
        describe(file, () => {
            for (const entry of cases) {
                it(text(key(entry, 'name')), async () => {
                    expect.hasAssertions();
                    const payload = bytes(text(key(entry, 'stream')));
                    if (entry['refused'] !== undefined) {
                        await holdsRefusal(() => readArrowShape(payload), entry['refused']);
                        return;
                    }
                    const shape = list(key(entry, 'shape')).map(column => ({ name: text(key(column, 'name')), kind: kindOf(key(column, 'kind')) }));
                    expect(readArrowShape(payload).columns).toEqual(shape);
                });
            }
        });
    }
});

describe('arrowWrite', () => {
    for (const { file, cases } of caseFiles('arrowWrite')) {
        describe(file, () => {
            for (const entry of cases) {
                it(text(key(entry, 'name')), async () => {
                    expect.hasAssertions();
                    const written = key(entry, 'snapshot');
                    const snapshot = build(written);
                    if (entry['refused'] !== undefined) {
                        await holdsRefusal(() => writeArrowSnapshot(snapshot), entry['refused']);
                        return;
                    }
                    const payload = writeArrowSnapshot(snapshot);
                    schema(key(entry, 'expect'), payload);
                    same(written, await readEveryWay(payload), false);
                    const kept = path.join(root, text(key(entry, 'written')));
                    if (writeCases) {
                        fs.mkdirSync(path.dirname(kept), { recursive: true });
                        fs.writeFileSync(kept, payload);
                    }
                    expect(Buffer.from(payload).equals(fs.readFileSync(kept)), `${kept} is what the writer writes`).toBe(true);
                });
            }
        });
    }
});

/** Holds a written stream's schema to what a case says: each field's name, type, nullability and caption,
 * and the schema's metadata, whole. */
function schema(expected: Json, payload: Uint8Array): void {
    const [first] = streamMessages(payload);
    const header = first?.message.table(2);
    if (header === undefined) throw new Error('The written stream holds no schema.');
    const read = readSchema(header);
    const fields = list(key(expected, 'fields'));
    expect(read.fields.map(field => ({
        name: field.name,
        type: fieldTypeName(field),
        caption: field.metadata.has('exgrid.caption') ? field.metadata.get('exgrid.caption') : null,
        nullable: field.nullable,
    }))).toEqual(fields.map(field => ({
        name: text(key(field, 'name')),
        type: text(key(field, 'type')),
        caption: key(field, 'caption'),
        nullable: true,
    })));
    const metadata = key(expected, 'metadata');
    expect(Object.fromEntries(read.metadata)).toEqual(metadata);
}
