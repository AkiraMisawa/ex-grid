// The live fold of a SnapshotPivotSource (ADR-0067), held to the source's own definition: after any run of
// Change Batches, the answer it brings up to date is the one a fresh source over the Snapshot the batches made
// answers, Items, leaves and numbers to the last bit, in that Snapshot's order. The shared cases pin what both
// engines say about a batch; this run covers the many shapes a fold meets — a removed row whose leaf empties,
// an Item that leaves or comes back in another spelling, a changed row that moves to another leaf, a double
// that arrives before the rows kept, a decimal whose scale grows past what subtraction holds — over every part.
import { createChangeBatch, createSnapshot, decimalFromText, type ChangeBatch, type Snapshot } from '@ex-grid/data';
import { afterEach, describe, expect, it, vi } from 'vitest';
import {
    blankItemKey, booleanItemKey, datePartField, pivotField, pivotItemsQuery, pivotQuery, SnapshotPivotSource, textItemKey, type PivotAnswer, type PivotField, type PivotPart,
    type PivotQuery,
} from '../src/index';
import { AggregationPass } from '../src/source/aggregation-pass';
import { answerJson } from './source-cases';

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

interface Trade {
    readonly id: bigint;
    readonly desk: string | null;
    readonly flag: boolean | null;
    readonly qty: bigint | null;
    readonly price: number | null;
    readonly amount: string | null;
    readonly day: bigint | null;
}

const desks = ['EMEA', 'emea', 'AMER', 'APAC', '', null, 'Amer'];
const day = 864_000_000_000n;
const epoch2020 = 637_134_336_000_000_000n;

/** The entry at `index`, which the caller picked from the list. */
function entry<T>(items: readonly T[], index: number): T {
    const item = items[index];
    if (item === undefined) throw new Error(`No entry ${index} of ${items.length}.`);
    return item;
}

/** An amount: mostly a few digits, now and then a scale far past the others' or digits near a Decimal's 96
 * bits, so that a sum passes what a whole number is kept in and the bound on an exact sum's subtraction is met. */
function amountOf(next: () => number, scale: number): string {
    const roll = next();
    const sign = next() < 0.4 ? '-' : '';
    if (roll < 0.02) return `${sign}0.${'0'.repeat(20)}${Math.floor(next() * 9) + 1}`;
    if (roll < 0.04) return `${sign}${Math.floor(next() * 7) + 1}${'9'.repeat(27)}`;
    return (Math.floor((next() - 0.3) * 10 ** (scale + 4)) / 10 ** scale).toFixed(scale);
}

function tradeOf(next: () => number, id: bigint): Trade {
    const pick = <T>(items: readonly T[]): T => entry(items, Math.floor(next() * items.length));
    const roll = next();
    const price = roll < 0.1 ? null : roll < 0.13 ? pick([Number.NaN, Number.POSITIVE_INFINITY, -0]) : (next() - 0.4) * 10 ** Math.floor(next() * 6);
    const scale = Math.floor(next() * 4);
    return {
        id,
        desk: next() < 0.05 ? `Desk ${Math.floor(next() * 50)}` : pick(desks),
        flag: next() < 0.1 ? null : next() < 0.5,
        qty: next() < 0.1 ? null : BigInt(Math.floor((next() - 0.3) * 1000)),
        price,
        amount: next() < 0.1 ? null : amountOf(next, scale),
        day: next() < 0.08 ? null : epoch2020 + BigInt(Math.floor(next() * 900)) * day,
    };
}

function records(trades: readonly Trade[], recordKey?: string): Snapshot {
    return createSnapshot({
        ...(recordKey === undefined ? {} : { recordKey }),
        columns: [
            { name: 'Id', kind: 'integer', values: trades.map(t => t.id) },
            { name: 'Desk', kind: 'text', values: trades.map(t => t.desk) },
            { name: 'Flag', kind: 'boolean', values: trades.map(t => t.flag) },
            { name: 'Qty', kind: 'integer', values: trades.map(t => t.qty) },
            { name: 'Price', kind: 'double', values: trades.map(t => t.price) },
            { name: 'Amount', kind: 'decimal', values: trades.map(t => (t.amount === null ? null : decimalFromText(t.amount))) },
            { name: 'Day', kind: 'date', values: trades.map(t => t.day) },
        ],
    });
}

const fields: readonly PivotField[] = [
    pivotField({ name: 'Id', type: 'number' }),
    pivotField({ name: 'Desk', type: 'text' }),
    pivotField({ name: 'Flag', type: 'boolean' }),
    pivotField({ name: 'Qty', type: 'number' }),
    pivotField({ name: 'Price', type: 'number' }),
    pivotField({ name: 'Amount', type: 'number' }),
    pivotField({ name: 'Day', type: 'date' }),
    datePartField('Year', 'Day', 'year'),
    datePartField('Month', 'Day', 'month'),
];

const allParts: readonly PivotPart[] = ['sum', 'extremes', 'product', 'variance'];

/** A question over the trades: a layout of Desk, Flag, Qty, Year and Month, some of them hiding Items, and
 * values over the numbers with parts drawn at random. */
function questionOf(next: () => number): PivotQuery {
    const layouts: readonly (readonly string[])[] = [[], ['Desk'], ['Desk', 'Flag'], ['Year', 'Month'], ['Qty'], ['Flag', 'Desk'], ['Price']];
    const rows = entry(layouts, Math.floor(next() * layouts.length));
    const columns = next() < 0.4 ? ['Flag', 'Year'].filter(f => !rows.includes(f)).slice(0, 1) : [];
    const hiding = next() < 0.3;
    const placed = (field: string): string | { field: string; hiddenItems: ReturnType<typeof textItemKey>[] } => {
        if (!hiding || (field !== 'Desk' && field !== 'Flag')) return field;
        return { field, hiddenItems: field === 'Desk' ? [textItemKey('AMER'), blankItemKey] : [booleanItemKey(false)] };
    };
    const filters = next() < 0.3 && !rows.includes('Desk') ? [{ field: 'Desk', hiddenItems: [textItemKey('emea')] }] : [];
    const values = ['Qty', 'Price', 'Amount', 'Desk'].filter(() => next() < 0.6).map(field => ({
        field,
        parts: field === 'Desk' ? [] : allParts.filter(() => next() < 0.5),
    }));
    return pivotQuery({ rows: rows.map(placed), columns: columns.map(placed), filters, values, ...(next() < 0.05 ? { maxLeaves: 3 } : {}) });
}

/** A batch over `model`: some records changed in place, some removed, some keys come back, some new. */
function batchOf(next: () => number, model: readonly Trade[], fresh: () => bigint, size: number): { batch: ChangeBatch; model: Trade[] } {
    const touched = new Set<number>();
    const count = Math.min(model.length, Math.floor(next() * size));
    while (touched.size < count) touched.add(Math.floor(next() * model.length));
    const removed = [...touched].filter(() => next() < 0.4);
    const removedSet = new Set(removed);
    const changed = new Map([...touched].filter(row => !removedSet.has(row)).map(row => [row, tradeOf(next, entry(model, row).id)]));
    const again = removed.filter(() => next() < 0.3).map(row => entry(model, row).id);
    const added = [...again, ...Array.from({ length: Math.floor(next() * size) }, fresh)].map(id => tradeOf(next, id));
    return {
        batch: createChangeBatch({
            added: records(added),
            changed: records([...changed.values()]),
            removedKeys: removed.map(row => entry(model, row).id),
        }),
        model: model.flatMap((t, row) => (removedSet.has(row) ? [] : [changed.get(row) ?? t])).concat(added),
    };
}

/** A trade on desk A that carries only its amount. */
function plainTrade(id: bigint, amount: string): Trade {
    return { id, desk: 'A', flag: true, qty: 1n, price: 1, amount, day: epoch2020 };
}

/** An answer as the shared cases write it, or its refusal's kind and message. */
function written(answered: PivotAnswer): unknown {
    return answered.refusal === undefined ? answerJson(answered) : { refused: answered.refusal.kind, saying: answered.refusal.message };
}

afterEach(() => {
    vi.restoreAllMocks();
});

describe('SnapshotPivotSource, live', () => {
    it('ADR-0067: after every batch, the answer brought up to date is what a fresh source over the next Snapshot answers, to the last bit', async () => {
        const fold = vi.spyOn(AggregationPass.prototype, 'fold');
        for (let run = 0; run < 24; run++) {
            const next = random(20261025 + run);
            let id = 0n;
            const fresh = (): bigint => (id += 1n);
            let model = Array.from({ length: 50 + Math.floor(next() * 400) }, () => tradeOf(next, fresh()));
            const source = new SnapshotPivotSource(records(model, 'Id'), fields);
            let query = questionOf(next);
            for (let round = 0; round < 12; round++) {
                // Now and then another question, which the source answers afresh and holds from then on.
                if (next() < 0.15) query = questionOf(next);
                const answered = await source.aggregate(query);
                const expected = await new SnapshotPivotSource(source.snapshot, fields).aggregate(query);
                expect(written(answered), `run ${run}, round ${round}`).toEqual(written(expected));
                const made = batchOf(next, model, fresh, round % 4 === 3 ? 120 : 12);
                source.apply(made.batch);
                model = made.model;
            }
        }
        // The answers above were brought up to date, not read again.
        expect(fold.mock.results.filter(result => result.value === true).length).toBeGreaterThan(150);
    });

    it('ADR-0060: an exact sum a fresh pass would carry past 128 bits on its way is read again, not kept by subtraction', async () => {
        const huge = `7${'9'.repeat(27)}`;
        const trade = plainTrade;
        const source = new SnapshotPivotSource(records([trade(1n, huge), trade(2n, '1'), trade(3n, `-${huge}`)], 'Id'), fields);
        const query = pivotQuery({ rows: ['Desk'], values: [{ field: 'Amount', parts: ['sum'] }] });
        await source.aggregate(query);
        // Read in order, the huge sum aligned to the new row's scale passes 128 bits, and the sum is a double from
        // there; subtracting the old row and adding the new one would have kept it exact.
        source.apply(createChangeBatch({ changed: records([trade(2n, `0.${'0'.repeat(20)}1`)]) }));
        const answered = await source.aggregate(query);
        expect(answered.values[0]?.sumAt(0).kind).toBe('double');
        expect(written(answered)).toEqual(written(await new SnapshotPivotSource(source.snapshot, fields).aggregate(query)));
    });

    it('ADR-0067: a Snapshot with no Record Key folds batches that only add, and asks afresh after one that removes', async () => {
        const next = random(7);
        let id = 0n;
        const fresh = (): bigint => (id += 1n);
        const model = Array.from({ length: 200 }, () => tradeOf(next, fresh()));
        const source = new SnapshotPivotSource(records(model), fields);
        const query = pivotQuery({ rows: ['Desk'], values: [{ field: 'Price', parts: allParts }, { field: 'Amount', parts: ['sum'] }] });
        for (let round = 0; round < 6; round++) {
            await source.aggregate(query);
            source.apply(createChangeBatch({ added: records(Array.from({ length: 30 }, () => tradeOf(next, fresh()))) }));
            expect(written(await source.aggregate(query))).toEqual(written(await new SnapshotPivotSource(source.snapshot, fields).aggregate(query)));
        }
    });

    it('ADR-0067: each listener is told the next Source Version after the source has moved on, and a refused batch changes nothing', async () => {
        const next = random(11);
        const model = Array.from({ length: 20 }, (_, i) => tradeOf(next, BigInt(i + 1)));
        const source = new SnapshotPivotSource(records(model, 'Id'), fields);
        const query = pivotQuery({ rows: ['Desk'], values: ['Qty'] });
        const before = await source.aggregate(query);
        const heard: string[] = [];
        const stop = source.subscribe(changed => {
            heard.push(changed.sourceVersion);
            expect(source.sourceVersion).toBe(changed.sourceVersion);
        });
        source.apply(createChangeBatch({ removedKeys: [1n] }));
        expect(heard).toEqual([source.sourceVersion]);
        expect(() => source.apply(createChangeBatch({ removedKeys: [99n] }))).toThrow(/removes the key 99, which the Snapshot does not hold/u);
        expect(heard).toHaveLength(1);
        stop();
        source.apply(createChangeBatch({ removedKeys: [2n] }));
        expect(heard).toHaveLength(1);
        // The answer before the batches still reads as it did, and its version still lists Items.
        expect(before.sourceVersion).not.toBe(source.sourceVersion);
        const page = await source.items(pivotItemsQuery({ field: 'Desk', sourceVersion: before.sourceVersion }));
        expect(page.isRefused).toBe(false);
    });

    it('ADR-0067: a listener that throws neither stops the others being told nor makes the batch look refused; its error is thrown again from a microtask', () => {
        const next = random(19);
        const source = new SnapshotPivotSource(records(Array.from({ length: 5 }, (_, i) => tradeOf(next, BigInt(i + 1))), 'Id'), fields);
        const reported: (() => void)[] = [];
        vi.spyOn(globalThis, 'queueMicrotask').mockImplementation(callback => {
            reported.push(callback);
        });
        const heard: string[] = [];
        source.subscribe(() => {
            throw new Error('a listener failed');
        });
        source.subscribe(changed => heard.push(changed.sourceVersion));
        const change = source.apply(createChangeBatch({ removedKeys: [1n] }));
        expect(change.after).toBe(source.snapshot);
        expect(heard).toEqual([source.sourceVersion]);
        expect(reported).toHaveLength(1);
        expect(() => reported[0]?.()).toThrow('a listener failed');
    });

    it('ADR-0067: a listener that applies a batch itself leaves the others told of the newest version only', () => {
        const next = random(23);
        const source = new SnapshotPivotSource(records(Array.from({ length: 5 }, (_, i) => tradeOf(next, BigInt(i + 1))), 'Id'), fields);
        const heard: string[] = [];
        let again = true;
        source.subscribe(() => {
            if (!again) return;
            again = false;
            source.apply(createChangeBatch({ removedKeys: [2n] }));
        });
        source.subscribe(changed => heard.push(changed.sourceVersion));
        source.apply(createChangeBatch({ removedKeys: [1n] }));
        expect(heard).toEqual([source.sourceVersion]);
        expect(source.snapshot.version).toBe(2n);
    });

    it('ADR-0067: a batch applied while an answer is assembled from the held pass waits, so the answer is the version before it, whole', async () => {
        const next = random(13);
        let id = 0n;
        const fresh = (): bigint => (id += 1n);
        let model = Array.from({ length: 3000 }, () => tradeOf(next, fresh()));
        const source = new SnapshotPivotSource(records(model, 'Id'), fields, { sliceBudgetMs: 0, yield: () => Promise.resolve() });
        const query = pivotQuery({ rows: ['Desk', 'Year'], columns: ['Flag'], values: [{ field: 'Price', parts: allParts }] });
        await source.aggregate(query);
        let made = batchOf(next, model, fresh, 50);
        source.apply(made.batch);
        model = made.model;
        const at = source.snapshot;
        const asked = source.aggregate(query);
        // The assembly yields after every step: the batch lands between two of them.
        await Promise.resolve();
        made = batchOf(next, model, fresh, 50);
        source.apply(made.batch);
        const answered = await asked;
        expect(answered.sourceVersion).not.toBe(source.sourceVersion);
        expect(written(answered)).toEqual(written(await new SnapshotPivotSource(at, fields).aggregate(query)));
        expect(written(await source.aggregate(query))).toEqual(written(await new SnapshotPivotSource(source.snapshot, fields).aggregate(query)));
    });
});
