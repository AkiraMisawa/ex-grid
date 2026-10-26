// Runs the shared Board Input cases of tests/cases/data/ (kind boardInput; ADR-0341, ADR-0370 section 3), which
// xUnit runs against ExGrid.Data as well: a declaration read, a source's shape checked against it, and JSON rows
// made a Snapshot, or the refusal, held to the case. tests/cases/README.md says what a file holds.
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { describe, expect, it } from 'vitest';
import {
    BoardInput, BoardRefusedError, createSnapshot, decimalText, readJsonText, ticksText, type DataShapeColumn, type Snapshot, type SnapshotColumn,
} from '../src/index';
import { isObject, key, kindOf, list, text, type Json, type JsonObject } from './case-json';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../../../../tests/cases/data');

const files = fs.readdirSync(root)
    .filter(name => name.endsWith('.json'))
    .toSorted()
    .map(name => ({ name, json: JSON.parse(fs.readFileSync(path.join(root, name), 'utf8')) as Json }))
    .filter(({ json }) => key(json, 'kind') === 'boardInput');

function columnsOf(value: Json): DataShapeColumn[] {
    return list(value).map(column => ({ name: text(key(column, 'name')), kind: kindOf(key(column, 'type')) }));
}

/** A value as tests/cases/README.md writes one under boardInput: a double that is no number by its name. */
function valueOf(column: SnapshotColumn, row: number): Json {
    switch (column.kind) {
        case 'text': return column.valueAt(row);
        case 'boolean': return column.valueAt(row);
        case 'decimal': {
            const value = column.valueAt(row);
            return value === null ? null : decimalText(value);
        }
        case 'double': {
            const value = column.valueAt(row);
            if (value === null) return null;
            return Number.isNaN(value) ? 'NaN' : value === Number.POSITIVE_INFINITY ? 'Infinity' : value === Number.NEGATIVE_INFINITY ? '-Infinity' : value;
        }
        case 'integer': {
            const value = column.valueAt(row);
            if (value === null) return null;
            return value >= -(2n ** 53n) && value <= 2n ** 53n ? Number(value) : value.toString();
        }
        case 'date': {
            const value = column.valueAt(row);
            if (value === null) return null;
            const written = ticksText(value);
            return written.endsWith('T00:00:00') ? written.slice(0, -'T00:00:00'.length) : written;
        }
        default: throw new Error(`'${String(column satisfies never)}' is not a column.`);
    }
}

function snapshotOf(snapshot: Snapshot): Json {
    return snapshot.columns.map(column => ({
        name: column.name,
        kind: column.kind,
        values: Array.from({ length: snapshot.rowCount }, (_, row) => valueOf(column, row)),
    }));
}

async function answer(entry: JsonObject): Promise<Json> {
    const read = entry['read'];
    if (read !== undefined) {
        return BoardInput.read('trades', readJsonText(text(read)), 'inputs.trades').columns.map(c => ({ name: c.name, type: c.kind }));
    }
    const input = new BoardInput('trades', columnsOf(key(entry, 'declared')));
    const check = entry['check'];
    if (check !== undefined) {
        input.check({ columns: columnsOf(check) });
        return true;
    }
    const bind = entry['bind'];
    if (bind !== undefined) {
        const snapshot = createSnapshot({ columns: columnsOf(bind).map(column => ({ name: column.name, kind: column.kind, values: [] })) });
        expect(input.bind(snapshot)).toBe(snapshot);
        return true;
    }
    const source = entry['checkSource'];
    if (source !== undefined) {
        const shape = source === null ? undefined : { columns: columnsOf(source) };
        let asked = 0;
        const answered = await input.checkSource({ shape: () => { asked++; return Promise.resolve(shape); } });
        expect(answered).toBe(shape);
        expect(asked).toBe(1);
        return true;
    }
    return snapshotOf(input.fromJson(text(key(entry, 'fromJson'))));
}

for (const { name: file, json } of files) {
    describe(`data/${file}`, () => {
        for (const entry of list(key(json, 'cases')).filter(isObject)) {
            it(text(key(entry, 'name')), async () => {
                expect.hasAssertions();
                const refused = entry['refused'];
                if (refused !== undefined) {
                    const saying = isObject(refused) ? refused['saying'] : undefined;
                    await expect(answer(entry)).rejects.toBeInstanceOf(BoardRefusedError);
                    await expect(answer(entry)).rejects.toMatchObject({ path: text(key(refused, 'path')) });
                    // A message quoting the platform's JSON parser is held to the words before it.
                    if (saying === undefined) await expect(answer(entry)).rejects.toMatchObject({ message: text(key(refused, 'message')) });
                    else await expect(answer(entry)).rejects.toThrow(text(saying));
                    return;
                }
                const want = entry['columns'] ?? entry['snapshot'] ?? key(entry, 'binds');
                expect(await answer(entry)).toEqual(want);
            });
        }
    });
}
