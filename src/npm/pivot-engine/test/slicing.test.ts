// The bundled source's work in slices (ADR-0066, PV-40): a question's pass, the answer assembled from it or
// from the pass held, a field's Items and the records behind a cell each yield whenever a slice is spent, stop at
// the next yield once their signal is cancelled, and give exactly what unsliced work gives. The cases hold what a
// source answers; how it paces itself is the Script Core's own, and held here.
import { createChangeBatch, createSnapshot, type Snapshot } from '@ex-grid/data';
import { describe, expect, it } from 'vitest';
import { pivotDetailsQuery, pivotItemsQuery, pivotQuery, SnapshotPivotSource, type PivotSlicing } from '../src/index';
import { answerJson } from './source-cases';

const books = Array.from({ length: 60 }, (_, i) => `Book ${i}`);

/** Twenty thousand trades over sixty books and three hundred days. */
function trades(): Snapshot {
    const count = 20_000;
    return createSnapshot({
        recordKey: 'Id',
        columns: [
            { name: 'Id', kind: 'integer', values: Array.from({ length: count }, (_, i) => i + 1) },
            { name: 'Book', kind: 'text', values: Array.from({ length: count }, (_, i) => `Book ${i % books.length}`) },
            { name: 'Day', kind: 'integer', values: Array.from({ length: count }, (_, i) => Math.floor(i / books.length) % 300) },
            { name: 'Price', kind: 'double', values: Array.from({ length: count }, (_, i) => ((i * 7919) % 10_007) / 16) },
        ],
    });
}

const large = pivotQuery({ rows: ['Book', 'Day'], values: [{ field: 'Price', parts: ['sum', 'extremes', 'variance'] }] });

/** Slices that end after every step, the yields counted, the work going on at once; and, when `stopAt` is
 * given, the signal cancelled at that yield. */
function everyStep(stopAt?: number): { slicing: PivotSlicing; readonly yields: () => number; readonly signal: AbortSignal } {
    const controller = new AbortController();
    let yields = 0;
    return {
        slicing: {
            sliceBudgetMs: 0,
            yield: () => {
                yields++;
                if (yields === stopAt) controller.abort(new Error('superseded'));
                return Promise.resolve();
            },
        },
        yields: () => yields,
        signal: controller.signal,
    };
}

const never: PivotSlicing = { sliceBudgetMs: 86_400_000 };

describe('SnapshotPivotSource, sliced', () => {
    it('PV-40: a question over a large Snapshot yields between slices, its pass and its answer, and answers what unsliced work answers', async () => {
        const snapshot = trades();
        const pieces = everyStep();
        const sliced = await new SnapshotPivotSource(snapshot, undefined, pieces.slicing).aggregate(large);
        const whole = await new SnapshotPivotSource(snapshot, undefined, never).aggregate(large);
        expect(sliced.leafCount).toBeGreaterThan(10_000);
        // After every 4,096 rows of the pass, and between the steps of the answer's assembly.
        expect(pieces.yields()).toBeGreaterThan(Math.ceil(snapshot.rowCount / 4096));
        expect(answerJson(sliced)).toEqual(answerJson(whole));
    });

    it('PV-40: the answer assembled from the pass held, after a batch, is assembled in slices too', async () => {
        const pieces = everyStep();
        const source = new SnapshotPivotSource(trades(), undefined, pieces.slicing);
        await source.aggregate(large);
        source.apply(createChangeBatch({ removedKeys: [1n, 2n, 3n] }));
        const before = pieces.yields();
        const answered = await source.aggregate(large);
        expect(pieces.yields()).toBeGreaterThan(before);
        expect(answerJson(answered)).toEqual(answerJson(await new SnapshotPivotSource(source.snapshot, undefined, never).aggregate(large)));
    });

    it('PV-40: a question cancelled before it starts rejects with the signal\'s reason, and does nothing', async () => {
        const pieces = everyStep();
        const source = new SnapshotPivotSource(trades(), undefined, pieces.slicing);
        const reason = new Error('superseded');
        await expect(source.aggregate(large, AbortSignal.abort(reason))).rejects.toBe(reason);
        await expect(source.items(pivotItemsQuery({ field: 'Book', sourceVersion: source.sourceVersion }), AbortSignal.abort(reason))).rejects.toBe(reason);
        await expect(source.details(pivotDetailsQuery({ sourceVersion: source.sourceVersion }), AbortSignal.abort(reason))).rejects.toBe(reason);
        expect(pieces.yields()).toBe(0);
    });

    it('PV-40: a question cancelled during its pass or its answer stops at the next yield, and the source answers the next one whole', async () => {
        const snapshot = trades();
        const counted = everyStep();
        const expected = answerJson(await new SnapshotPivotSource(snapshot, undefined, counted.slicing).aggregate(large));
        // Cancelled in the pass (its first yields) and in the answer's assembly (its last).
        for (const stopAt of [2, counted.yields() - 1]) {
            const pieces = everyStep(stopAt);
            const source = new SnapshotPivotSource(snapshot, undefined, pieces.slicing);
            await expect(source.aggregate(large, pieces.signal)).rejects.toThrow('superseded');
            expect(pieces.yields(), `stopped at yield ${stopAt}`).toBe(stopAt);
            expect(answerJson(await source.aggregate(large))).toEqual(expected);
        }
    });

    it('PV-40: a question cancelled while the answer held is assembled stops there; the pass held still folds the next batch', async () => {
        const snapshot = trades();
        let stop: AbortController | undefined;
        let yields = 0;
        const source = new SnapshotPivotSource(snapshot, undefined, {
            sliceBudgetMs: 0,
            yield: () => {
                yields++;
                stop?.abort(new Error('superseded'));
                return Promise.resolve();
            },
        });
        await source.aggregate(large);
        source.apply(createChangeBatch({ removedKeys: [10n] }));
        stop = new AbortController();
        const at = yields;
        await expect(source.aggregate(large, stop.signal)).rejects.toThrow('superseded');
        expect(yields).toBe(at + 1);
        stop = undefined;
        source.apply(createChangeBatch({ removedKeys: [11n] }));
        expect(answerJson(await source.aggregate(large))).toEqual(answerJson(await new SnapshotPivotSource(source.snapshot, undefined, never).aggregate(large)));
    });

    it('PV-40: a field\'s Items and the records behind a cell are read in slices, and stop when cancelled', async () => {
        const snapshot = trades();
        const pieces = everyStep();
        const source = new SnapshotPivotSource(snapshot, undefined, pieces.slicing);
        const items = await source.items(pivotItemsQuery({ field: 'Book', sourceVersion: source.sourceVersion }));
        const read = pieces.yields();
        const details = await source.details(pivotDetailsQuery({ sourceVersion: source.sourceVersion, count: 5 }));
        expect(read).toBeGreaterThan(0);
        expect(pieces.yields()).toBeGreaterThan(read);
        expect(items.total).toBe(books.length);
        expect(details.total).toBe(20_000);
        const stopped = everyStep(1);
        const cancelled = new SnapshotPivotSource(snapshot, undefined, stopped.slicing);
        await expect(cancelled.items(pivotItemsQuery({ field: 'Book', sourceVersion: cancelled.sourceVersion }), stopped.signal)).rejects.toThrow('superseded');
    });

    it('PV-40: a slice budget that is no number of milliseconds, or a yield that is no function, is refused when the source is made', () => {
        const snapshot = trades();
        expect(() => new SnapshotPivotSource(snapshot, undefined, { sliceBudgetMs: -1 })).toThrow(RangeError);
        expect(() => new SnapshotPivotSource(snapshot, undefined, { sliceBudgetMs: Number.NaN })).toThrow(RangeError);
        expect(() => new SnapshotPivotSource(snapshot, undefined, { yield: 'soon' as never })).toThrow(TypeError);
    });
});
