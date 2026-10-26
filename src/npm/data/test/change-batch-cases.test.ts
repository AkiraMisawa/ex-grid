// Runs the shared Change Batch cases of tests/cases/data/ (ADR-0064, ADR-0067, ADR-0370 section 5), which
// xUnit runs against ExGrid.Data as well: each batch of a case is applied to the case's Snapshot, or to the
// version a step before made, and what it made, or how it was refused, is held to the answer ExGrid.Data
// wrote. Each step also holds the fold, and that the version a batch was applied to still reads as it did.
// tests/cases/README.md says what a file holds.
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { describe, expect, it } from 'vitest';
import {
    SnapshotError, applyChangeBatch, createChangeBatch, createSnapshot, type ChangeBatch, type Snapshot, type SnapshotChange,
} from '../src/index';
import { build, columnInput, integer, isObject, key, list, text, valueOf, type Json, type JsonObject } from './case-json';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../../../../tests/cases/data');

const files = fs.readdirSync(root)
    .filter(name => name.endsWith('.json'))
    .toSorted()
    .map(name => ({ name, json: JSON.parse(fs.readFileSync(path.join(root, name), 'utf8')) as Json }))
    .filter(({ json }) => key(json, 'kind') === 'changeBatch');

/** A batch's records: columns with no key, under their names. */
function records(step: JsonObject, role: string): Snapshot | undefined {
    const written = step[role];
    if (written === undefined) return undefined;
    return createSnapshot({ columns: list(key(written, 'columns')).map(column => columnInput(column, undefined)) });
}

/** A removed key as a case writes it: a string a Text key, a number an Integer key (or a number that is no
 * integer, which is refused), `{"integer": "…"}` an Integer key past a double's exact integers. */
function removedKey(value: Json): string | bigint | number {
    if (isObject(value)) return BigInt(text(key(value, 'integer')));
    if (typeof value === 'string' || typeof value === 'number') return value;
    // A Blank the case asks to be refused, which no caller the types admit would hand over.
    return value as never;
}

function batchOf(step: JsonObject): ChangeBatch {
    const added = records(step, 'added');
    const changed = records(step, 'changed');
    const removed = step['removedKeys'];
    return createChangeBatch({
        ...(added === undefined ? {} : { added }),
        ...(changed === undefined ? {} : { changed }),
        ...(removed === undefined ? {} : { removedKeys: list(removed).map(removedKey) }),
    });
}

function rowOf(snapshot: Snapshot, row: number): string {
    return JSON.stringify(snapshot.columns.map(column => valueOf(column, row)));
}

function picture(snapshot: Snapshot): string {
    const rows = Array.from({ length: snapshot.rowCount }, (_, row) => rowOf(snapshot, row));
    const dictionaries = snapshot.columns.map(column => (column.kind === 'text' ? column.dictionary : null));
    return JSON.stringify([snapshot.version.toString(), rows, dictionaries]);
}

/** What a batch made, as ExGrid.Data's runner writes it. */
function answer(change: SnapshotChange): Json {
    const after = change.after;
    return {
        version: Number(after.version),
        removed: [...change.removed],
        added: [...change.added],
        after: after.columns.map(column => ({
            name: column.name,
            caption: column.caption,
            values: Array.from({ length: after.rowCount }, (_, row) => valueOf(column, row)),
            ...(column.kind === 'text' ? { dictionary: [...column.dictionary] } : {}),
        })),
    };
}

function refusal(error: unknown): Json {
    if (error instanceof SnapshotError) {
        return {
            refused: {
                message: error.message,
                row: error.row === undefined ? null : error.row,
                column: error.column === undefined ? null : error.column,
                key: error.key === undefined ? null : typeof error.key === 'bigint' ? integer(error.key) : error.key,
            },
        };
    }
    if (error instanceof TypeError) return { refused: 'argument' };
    throw error;
}

/** A step's batch made and applied, or its refusal as the case writes it; only the making and the applying
 * are caught. */
function attempt(target: Snapshot, step: JsonObject): { readonly change: SnapshotChange; readonly refusal?: undefined } | { readonly change?: undefined; readonly refusal: Json } {
    try {
        return { change: applyChangeBatch(target, batchOf(step)) };
    } catch (error) { // oxlint-disable-line ex-grid/no-catch -- a refusal is the answer the case holds; refusal() rethrows anything else
        return { refusal: refusal(error) };
    }
}

/** ADR-0067's fold: the rows the next version holds that the batch did not bring are the rows the version
 * before held less those it removed, in their order. */
function folds(change: SnapshotChange): void {
    const removed = new Set(change.removed);
    const added = new Set(change.added);
    expect(change.after.rowCount).toBe(change.before.rowCount - change.removed.length + change.added.length);
    const kept = Array.from({ length: change.before.rowCount }, (_, row) => row).filter(row => !removed.has(row)).map(row => rowOf(change.before, row));
    const stayed = Array.from({ length: change.after.rowCount }, (_, row) => row).filter(row => !added.has(row)).map(row => rowOf(change.after, row));
    expect(stayed).toEqual(kept);
}

describe('changeBatch', () => {
    for (const { name: file, json } of files) {
        describe(file, () => {
            for (const entry of list(key(json, 'cases')).filter(isObject)) {
                it(text(key(entry, 'name')), () => {
                    expect.hasAssertions();
                    const snapshot = build(key(entry, 'snapshot'));
                    const made: (Snapshot | undefined)[] = [];
                    list(key(entry, 'batches')).filter(isObject).forEach((step, i) => {
                        const on = step['on'];
                        const target = on === undefined ? snapshot : on === 'previous' ? made.at(-1) : made[Number(on)];
                        if (target === undefined) throw new Error(`Batch ${i} is applied to a step that was refused.`);
                        const before = picture(target);
                        const applied = attempt(target, step);
                        made.push(applied.change?.after);
                        if (applied.change !== undefined) folds(applied.change);
                        const answered = applied.change === undefined ? applied.refusal : answer(applied.change);
                        expect(answered, `batch ${i}`).toEqual(key(step, 'expect'));
                        expect(picture(target), `batch ${i} left the version it was applied to as it was`).toBe(before);
                    });
                });
            }
        });
    }
});
