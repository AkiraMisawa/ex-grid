// Runs the shared CSV read cases of tests/cases/data/ (ADR-0064, ADR-0370 section 3), which xUnit runs against
// ExGrid.Data as well: each case's file is read under its Schema, and the Snapshot it made, or how it was
// refused, is held to the answer ExGrid.Data wrote. tests/cases/README.md says what a file holds.
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { describe, expect, it } from 'vitest';
import { SnapshotError, readCsvSnapshot, type CsvColumn, type CsvSchema, type Snapshot } from '../src/index';
import { fileOf, isObject, key, kindOf, list, text, valueOf, type Json, type JsonObject } from './case-json';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../../../tests/cases/data');

const files = fs.readdirSync(root)
    .filter(name => name.endsWith('.json'))
    .toSorted()
    .map(name => ({ name, json: JSON.parse(fs.readFileSync(path.join(root, name), 'utf8')) as Json }))
    .filter(({ json }) => key(json, 'kind') === 'csvRead');

function texts(value: Json): readonly string[] {
    return list(value).map(text);
}

/** A key of the case's Schema, which the case may leave out, as the TypeScript `CsvSchema` leaves it out. */
function optional<T>(object: JsonObject, name: string, read: (value: Json) => T): { readonly [key: string]: T } {
    const value = object[name];
    return value === undefined ? {} : { [name]: read(value) };
}

function wholeNumber(value: Json): number {
    if (typeof value !== 'number') throw new Error(`Expected a number, not ${JSON.stringify(value)}.`);
    return value;
}

function flag(value: Json): boolean {
    if (typeof value !== 'boolean') throw new Error(`Expected a boolean, not ${JSON.stringify(value)}.`);
    return value;
}

function columnOf(value: Json): CsvColumn {
    if (!isObject(value)) throw new Error('A column is an object.');
    return {
        name: text(key(value, 'name')),
        kind: kindOf(key(value, 'kind')),
        ...optional(value, 'header', text),
        ...optional(value, 'position', wholeNumber),
        ...optional(value, 'caption', text),
        ...optional(value, 'culture', text),
        ...optional(value, 'decimalPoint', text),
        ...optional(value, 'thousandsSeparator', text),
        ...optional(value, 'dateFormats', texts),
        ...optional(value, 'trueText', texts),
        ...optional(value, 'falseText', texts),
        ...optional(value, 'blankText', texts),
    };
}

function separatorOf(value: Json): 'comma' | 'tab' | 'semicolon' {
    if (value === 'comma' || value === 'tab' || value === 'semicolon') return value;
    throw new Error(`${JSON.stringify(value)} is not a separator the cases name.`);
}

function encodingOf(value: Json): 'utf-8' | 'shift-jis' {
    if (value === 'utf-8' || value === 'shift-jis') return value;
    throw new Error(`${JSON.stringify(value)} is not an encoding the cases name.`);
}

function schemaOf(value: Json): CsvSchema {
    if (!isObject(value)) throw new Error('A Schema is an object.');
    return {
        columns: list(key(value, 'columns')).map(columnOf),
        ...optional(value, 'separator', separatorOf),
        ...optional(value, 'hasHeader', flag),
        ...optional(value, 'encoding', encodingOf),
        ...optional(value, 'blankText', texts),
        ...optional(value, 'recordKey', text),
    };
}

/** The Snapshot a read made, as ExGrid.Data's runner writes it. */
function answer(snapshot: Snapshot): Json {
    return {
        rows: snapshot.rowCount,
        recordKey: snapshot.recordKey === undefined ? null : snapshot.recordKey,
        columns: snapshot.columns.map(column => ({
            name: column.name,
            caption: column.caption,
            kind: column.kind,
            values: Array.from({ length: snapshot.rowCount }, (_, row) => valueOf(column, row)),
            ...(column.kind === 'text' ? { dictionary: [...column.dictionary] } : {}),
        })),
    };
}

function refusal(error: unknown): Json {
    if (error instanceof SnapshotError) {
        return { refused: { message: error.message, row: error.row === undefined ? null : error.row, column: error.column === undefined ? null : error.column } };
    }
    // A Schema refused says so in csvPlan's words; a TypeError in other words is a defect of the reader, which
    // must fail the case rather than pass one that expects a refusal.
    if (error instanceof TypeError && /^(A |The |Column |Columns |Only )/.test(error.message)) return { refused: 'argument' };
    throw error;
}

async function attempt(entry: JsonObject): Promise<Json> {
    const schema = schemaOf(key(entry, 'schema'));
    const file = fileOf(key(entry, 'file'));
    try {
        return answer(await readCsvSnapshot(schema, file, { sliceBudgetMs: 1_000_000 }));
    } catch (error) { // oxlint-disable-line ex-grid/no-catch -- a refusal is the answer the case holds; refusal() rethrows anything else
        return refusal(error);
    }
}

describe('csvRead', () => {
    for (const { name: file, json } of files) {
        describe(file, () => {
            for (const entry of list(key(json, 'cases')).filter(isObject)) {
                it(text(key(entry, 'name')), async () => {
                    expect.hasAssertions();
                    expect(await attempt(entry)).toEqual(key(entry, 'expect'));
                });
            }
        });
    }
});

