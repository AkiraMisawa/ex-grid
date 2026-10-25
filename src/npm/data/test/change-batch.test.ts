// The TypeScript edges of a Change Batch the shared cases do not reach: long runs of batches, held to a plain
// model of the records, that carry the Record Key index and the positions through many versions and past the
// point where the index is built anew, and versions made from an older one; and the keys only a TypeScript
// caller can hand over (ADR-0064, ADR-0067).
import { describe, expect, it } from 'vitest';
import { applyChangeBatch, createChangeBatch, createSnapshot, type Snapshot } from '../src/index';

interface Trade {
    readonly id: bigint;
    readonly desk: string | null;
    readonly price: number | null;
}

/** A reproducible stream of numbers in [0, 1). */
function random(seed: number): () => number {
    let state = seed >>> 0;
    return () => {
        state = (state + 0x6d2b79f5) >>> 0;
        let t = state;
        t = Math.imul(t ^ (t >>> 15), t | 1);
        t ^= t + Math.imul(t ^ (t >>> 7), t | 61);
        return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
    };
}

function records(trades: readonly Trade[], recordKey?: string): Snapshot {
    return createSnapshot({
        ...(recordKey === undefined ? {} : { recordKey }),
        columns: [
            { name: 'Id', kind: 'integer', values: trades.map(t => t.id) },
            { name: 'Desk', kind: 'text', values: trades.map(t => t.desk) },
            { name: 'Price', kind: 'double', values: trades.map(t => t.price) },
        ],
    });
}

function held(snapshot: Snapshot): Trade[] {
    const [id, desk, price] = snapshot.columns;
    if (id?.kind !== 'integer' || desk?.kind !== 'text' || price?.kind !== 'double') throw new Error('Not a Snapshot of trades.');
    return Array.from({ length: snapshot.rowCount }, (_, row) => {
        const key = id.valueAt(row);
        if (key === null) throw new Error(`Row ${row} has no key.`);
        return { id: key, desk: desk.valueAt(row), price: price.valueAt(row) };
    });
}

function dictionaryOf(snapshot: Snapshot): readonly string[] {
    const desk = snapshot.column('Desk');
    if (desk.kind !== 'text') throw new Error('Desk is not Text.');
    return desk.dictionary;
}

const desks = ['EMEA', 'AMER', 'APAC', null, '', 'emea'];

/** The entry at `index`, which the caller picked from the list. */
function entry<T>(items: readonly T[], index: number): T {
    const item = items[index];
    if (item === undefined) throw new Error(`No entry ${index} of ${items.length}.`);
    return item;
}

/** One batch over `model`, applied to it as ADR-0064 says: a changed record in its place, a removed one
 * gone, the added at the end. */
function step(next: () => number, model: readonly Trade[], fresh: () => bigint): { batch: ReturnType<typeof createChangeBatch>; model: Trade[]; removedRows: number[]; addedRows: number[] } {
    const pick = (count: number): number[] => {
        const rows = new Set<number>();
        while (rows.size < Math.min(count, model.length)) rows.add(Math.floor(next() * model.length));
        return [...rows];
    };
    const touched = pick(Math.floor(next() * 40));
    const removedRows = touched.filter(() => next() < 0.5);
    const removedSet = new Set(removedRows);
    const changedRows = touched.filter(row => !removedSet.has(row));
    const trade = (id: bigint): Trade => ({
        id,
        desk: next() < 0.1 ? `Desk ${Math.floor(next() * 1000)}` : entry(desks, Math.floor(next() * desks.length)),
        price: next() < 0.1 ? null : Math.round(next() * 1e6) / 100,
    });
    const changed = new Map(changedRows.map(row => [row, trade(entry(model, row).id)]));
    // Some of the keys removed come back as new records, at the end.
    const again = removedRows.filter(() => next() < 0.2).map(row => entry(model, row).id);
    const added = [...again, ...Array.from({ length: Math.floor(next() * 30) }, fresh)].map(trade);
    const after = model.flatMap((t, row) => (removedSet.has(row) ? [] : [changed.get(row) ?? t])).concat(added);
    const leaving = [...touched].toSorted((a, b) => a - b);
    const kept: number[] = [];
    let at = 0;
    model.forEach((_, row) => {
        if (changed.has(row)) kept.push(at);
        if (!removedSet.has(row)) at++;
    });
    return {
        batch: createChangeBatch({
            added: records(added),
            changed: records(changedRows.toSorted((a, b) => b - a).map(row => changed.get(row)).filter(t => t !== undefined)),
            removedKeys: removedRows.map(row => entry(model, row).id),
        }),
        model: after,
        removedRows: leaving,
        addedRows: [...kept, ...added.map((_, i) => after.length - added.length + i)],
    };
}

describe('applyChangeBatch', () => {
    it('ADR-0064: a long run of batches holds what a plain model of the records holds, past the point where the key index is built anew, and a version made from an older one keeps its own keys', () => {
        const next = random(20261025);
        let id = 0n;
        const fresh = (): bigint => (id += 1n);
        let model: Trade[] = Array.from({ length: 2000 }, () => ({ id: fresh(), desk: entry(desks, Number(id % 6n)), price: Number(id) }));
        let snapshot = records(model, 'Id');
        const versions: { snapshot: Snapshot; model: Trade[] }[] = [];
        for (let round = 0; round < 400; round++) {
            versions.push({ snapshot, model });
            const made = step(next, model, fresh);
            const change = applyChangeBatch(snapshot, made.batch);
            expect(change.removed).toEqual(made.removedRows);
            expect(change.added).toEqual(made.addedRows);
            expect(change.after.version).toBe(snapshot.version + 1n);
            expect(held(change.after)).toEqual(made.model);
            expect(dictionaryOf(change.after).slice(0, dictionaryOf(snapshot).length)).toEqual(dictionaryOf(snapshot));
            if (round % 50 === 49) {
                // A batch on a version ten rounds old answers by that version's keys, not the newest's.
                const older = versions[round - 10];
                if (older === undefined) throw new Error('No version ten rounds old.');
                const branch = step(next, older.model, fresh);
                expect(held(applyChangeBatch(older.snapshot, branch.batch).after)).toEqual(branch.model);
            }
            snapshot = change.after;
            model = made.model;
        }
    });

    it('ADR-0064: a removed key past a 64-bit integer, a removed key of no kind and a Snapshot that is not one are refused as TypeErrors', () => {
        expect(() => createChangeBatch({ removedKeys: [2n ** 63n] })).toThrow(TypeError);
        expect(() => createChangeBatch({ removedKeys: [-(2n ** 63n) - 1n] })).toThrow(TypeError);
        expect(() => createChangeBatch({ removedKeys: [true as never] })).toThrow(TypeError);
        expect(() => createChangeBatch({ removedKeys: [Number.MAX_SAFE_INTEGER + 1] })).toThrow(TypeError);
        const snapshot = records([{ id: 1n, desk: 'A', price: 1 }], 'Id');
        expect(() => applyChangeBatch({ ...snapshot }, createChangeBatch())).toThrow(TypeError);
        expect(() => createChangeBatch({ added: { ...snapshot } })).toThrow(TypeError);
        expect(createChangeBatch({ removedKeys: [-(2n ** 63n), 7] }).removedKeys).toEqual([-(2n ** 63n), 7n]);
    });
});
