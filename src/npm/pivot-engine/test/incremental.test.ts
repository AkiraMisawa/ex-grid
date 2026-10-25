// The incremental computation against a fresh one (ADR-0153; LV-24, LV-29, LV-30), as the C# engine's
// IncrementalOracleTests hold it. Over recorded seeds, layouts are drawn at random — one or two row fields, none to
// two column fields, number, date, Boolean and blank Items, subtotals on, off and at the bottom, each form,
// collapsed and toggled Items, hidden ones, a report filter, grand totals off, Values on rows or columns, an order
// by label or by a Value Field — with every Aggregation and Show Values As cycled through their Value Fields; each
// is then driven through random Change Batches that add, change and remove records, Items appearing and leaving.
// After every batch the Window a report source shows equals the one a fresh source over the same data shows, row by
// row and cell by cell, in values, painted text and label widths; a row whose painted state did not change is the
// instance shown before, and one that did is a new one; and the Row Sequence Version moves exactly when the rows'
// order does (ADR-0011). A Window cancelled at any of its slices leaves the next one equal to a fresh computation,
// and still incremental.
import { createChangeBatch, createSnapshot, decimalFromText, type Snapshot } from '@ex-grid/data';
import { describe, expect, it } from 'vitest';
import {
    blankItemKey, booleanItemKey, createPivotLayout, datePartField, dateItemKey, LocalPivotReportSource, numberItemKey, pivotAggregations,
    pivotField, pivotSort, SnapshotPivotSource, textItemKey, writePivotLayoutJson, type PivotDisplayRow, type PivotField,
    type PivotFieldPlacementInput, type PivotItemKey, type PivotLayout, type PivotReportAnswer, type PivotReportRequest, type PivotReportSettings,
    type PivotReportUpdate, type PivotShowValuesAs, type PivotSlicing, type PivotValueFieldInput,
} from '../src/index';

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

/** The entry at `index`, which the caller picked from the list. */
function entry<T>(items: readonly T[], index: number): T {
    const item = items[index];
    if (item === undefined) throw new Error(`No entry ${index} of ${items.length}.`);
    return item;
}

/** A whole number in [from, to). */
function between(next: () => number, from: number, to: number): number {
    return from + Math.floor(next() * (to - from));
}

function pick<T>(next: () => number, items: readonly T[]): T {
    return entry(items, between(next, 0, items.length));
}

interface Row {
    readonly id: number;
    readonly region: string | null;
    readonly desk: string;
    readonly day: bigint;
    readonly flag: boolean;
    readonly qty: number;
    readonly amount: string;
    readonly measure: number;
}

// 'east' and 'East' are one Item, labelled by the first spelling among the records present (ADR-0060).
const regions = ['East', 'West', 'North', 'south', 'east'];
const showValuesAs: readonly PivotShowValuesAs[] = ['noCalculation', 'percentOfGrandTotal', 'percentOfColumnTotal', 'percentOfRowTotal'];
// Each Value Field's number format, or none for the format the Aggregation gives.
const formats = ['none', '0.00', 'G17', '0%', '#,##0'];
const axisFields = ['Region', 'Desk', 'Day', 'Month', 'Flag', 'Qty'];
const valueFields = ['Amount', 'Measure', 'Qty'];

/** A day of 2026 in .NET ticks, as a Snapshot's date column holds it. */
function dayOf(month: number, day: number): bigint {
    return (BigInt(Date.UTC(2026, month - 1, day)) + 62_135_596_800_000n) * 10_000n;
}

function rowOf(id: number, next: () => number): Row {
    return {
        id,
        region: between(next, 0, 9) === 0 ? null : pick(next, regions),
        desk: `Desk${between(next, 0, 6)}`,
        day: dayOf(1 + between(next, 0, 4), 1 + between(next, 0, 3)),
        flag: next() < 0.5,
        qty: between(next, -3, 8),
        amount: (between(next, -500, 2000) / 100).toFixed(2),
        measure: between(next, 0, 5) === 0 ? next() * 1e12 : Math.round((next() * 100 - 30) * 1000) / 1000,
    };
}

function records(rows: readonly Row[]): Snapshot {
    return createSnapshot({
        recordKey: 'Id',
        columns: [
            { name: 'Id', kind: 'integer', values: rows.map(r => r.id) },
            { name: 'Region', kind: 'text', values: rows.map(r => r.region) },
            { name: 'Desk', kind: 'text', values: rows.map(r => r.desk) },
            { name: 'Day', kind: 'date', values: rows.map(r => r.day) },
            { name: 'Flag', kind: 'boolean', values: rows.map(r => r.flag) },
            { name: 'Qty', kind: 'integer', values: rows.map(r => r.qty) },
            { name: 'Amount', kind: 'decimal', values: rows.map(r => decimalFromText(r.amount)) },
            { name: 'Measure', kind: 'double', values: rows.map(r => r.measure) },
        ],
    });
}

const fields: readonly PivotField[] = [
    pivotField({ name: 'Id', type: 'number' }),
    pivotField({ name: 'Region', type: 'text' }),
    pivotField({ name: 'Desk', type: 'text' }),
    pivotField({ name: 'Day', type: 'date' }),
    datePartField('Month', 'Day', 'month'),
    pivotField({ name: 'Flag', type: 'boolean' }),
    pivotField({ name: 'Qty', type: 'number' }),
    pivotField({ name: 'Amount', type: 'number' }),
    pivotField({ name: 'Measure', type: 'number' }),
];

// An Item a field may have, to hide or to toggle: sometimes one no record carries yet.
function itemOf(field: string, next: () => number): PivotItemKey {
    switch (field) {
        case 'Region': return between(next, 0, 4) === 0 ? blankItemKey : textItemKey(pick(next, ['East', 'West', 'North', 'south', 'Nowhere']));
        case 'Desk': return textItemKey(`Desk${between(next, 0, 8)}`);
        case 'Day': return dateItemKey(dayOf(1 + between(next, 0, 4), 1 + between(next, 0, 3)));
        case 'Month': return numberItemKey(1 + between(next, 0, 4));
        case 'Flag': return booleanItemKey(next() < 0.5);
        default: return numberItemKey(between(next, -3, 8));
    }
}

function drawLayout(next: () => number, ordinal: number): PivotLayout {
    const pool = axisFields.toSorted(() => next() - 0.5);
    const take = (): string => {
        const field = pool.shift();
        if (field === undefined) throw new Error('No axis field is left to draw.');
        return field;
    };
    const rows = Array.from({ length: 1 + between(next, 0, 2) }, take);
    const columns = Array.from({ length: between(next, 0, 3) }, take);
    const valueCount = 1 + between(next, 0, 3);
    const values = Array.from({ length: valueCount }, (_, v): PivotValueFieldInput => {
        // Every Aggregation and every Show Values As, cycled across the layouts drawn.
        const format = pick(next, formats);
        return {
            field: pick(next, valueFields),
            aggregation: entry(pivotAggregations, (ordinal * 3 + v) % pivotAggregations.length),
            showValuesAs: entry(showValuesAs, (ordinal + v) % showValuesAs.length),
            ...(format === 'none' ? {} : { numberFormat: format }),
        };
    });
    const place = (field: string): PivotFieldPlacementInput => {
        const order = between(next, 0, 5);
        return {
            field,
            subtotals: between(next, 0, 4) !== 0,
            collapsed: between(next, 0, 6) === 0,
            sort: order === 0 ? pivotSort('descending')
                : order <= 2 ? pivotSort(next() < 0.5 ? 'ascending' : 'descending', between(next, 0, valueCount))
                    : pivotSort('ascending'),
            hiddenItems: between(next, 0, 3) === 0 ? [itemOf(field, next)] : [],
            toggledItems: between(next, 0, 3) === 0 ? [itemOf(field, next)] : [],
        };
    };
    const filters: PivotFieldPlacementInput[] = [];
    const placedRows = rows.map(place);
    const placedColumns = columns.map(place);
    if (next() < 0.5) {
        const filter = take();
        filters.push({ field: filter, hiddenItems: next() < 0.5 ? [itemOf(filter, next)] : [] });
    }
    return createPivotLayout({
        rows: placedRows,
        columns: placedColumns,
        filters,
        values,
        form: pick(next, ['compact', 'outline', 'tabular'] as const),
        subtotalsAtTop: next() < 0.5,
        grandTotalRow: between(next, 0, 4) !== 0,
        grandTotalColumn: between(next, 0, 4) !== 0,
        repeatItemLabels: between(next, 0, 3) === 0,
        valuesAxis: next() < 0.5 ? 'rows' : 'columns',
    });
}

const settings: PivotReportSettings = {
    locale: 'en-US',
    words: {},
    changeHighlightMs: 86_400_000,
    labelMetrics: { wideWidthPx: 9, digitWidthPx: 7, narrowWidthPx: 4, fullWidthPx: 14, otherWidthPx: 7, cellHorizontalPaddingPx: 4 },
};

let asked = 0;
function request(layout: PivotLayout, baseline?: string): PivotReportRequest {
    asked++;
    return { requestId: `r${asked}`, layout, settings, window: { start: 0, count: 100_000 }, ...(baseline === undefined ? {} : { baseline }) };
}

function answered(update: PivotReportUpdate, where: string): PivotReportAnswer {
    if (update.refusal !== undefined) throw new Error(`${where}: refused: ${update.refusal.message}`);
    return update;
}

function keyOf(row: PivotDisplayRow): string {
    return JSON.stringify([row.role, row.valueField, row.items]);
}

// A row as a fresh report paints it: everything but its change marks, which a fresh source has none of.
function painted(row: PivotDisplayRow): unknown {
    return { key: keyOf(row), carriesValues: row.carriesValues, labels: row.labels, values: row.values, rowPath: row.rowPath };
}

// What a Window says of the whole report: everything but its versions.
function metadata(state: PivotReportAnswer): unknown {
    const { rowCount, labelColumns, valueColumns, headerSpans, headerTierCount, valueCaptions, labelWidths } = state.metadata;
    return { rowCount, labelColumns, valueColumns, headerSpans, headerTierCount, valueCaptions, labelWidths };
}

function valueColumnNames(state: PivotReportAnswer): string[] {
    return state.metadata.valueColumns.map(c => c.name);
}

// The Window equals the one a fresh source over the same data shows: the rows and their order, labels, each value's
// painted text, exact and approximate number and error, the headers, spans and label widths.
async function expectSameAsFresh(shown: PivotReportAnswer, data: SnapshotPivotSource, layout: PivotLayout, where: string): Promise<void> {
    const source = new LocalPivotReportSource(new SnapshotPivotSource(data.snapshot, fields));
    const fresh = answered(await source.window(request(layout)), where);
    source.dispose();
    expect(metadata(shown), where).toEqual(metadata(fresh));
    expect(shown.rows.map(keyOf), `${where}: the rows' order`).toEqual(fresh.rows.map(keyOf));
    // Compared as text first, which is quick; a difference is then told row by row.
    if (JSON.stringify(shown.rows.map(painted), replacer) === JSON.stringify(fresh.rows.map(painted), replacer)) return;
    shown.rows.forEach((row, r) => {
        expect(painted(row), `${where}: row ${r}`).toEqual(painted(entry(fresh.rows, r)));
    });
}

// A row whose painted state — labels, values under the same value columns, change marks — did not change is the
// instance shown before; one that did is new. When the value columns themselves changed, a row painted against them
// may be new either way. The Row Sequence Version moves exactly when the rows' order does (ADR-0011).
function expectSharesUnchangedRows(before: PivotReportAnswer, after: PivotReportAnswer, where: string): void {
    const previous = new Map(before.rows.map(row => [keyOf(row), row]));
    const sameColumns = JSON.stringify(valueColumnNames(before)) === JSON.stringify(valueColumnNames(after));
    for (const row of after.rows) {
        const old = previous.get(keyOf(row));
        if (old === undefined) continue;
        const same = sameColumns && JSON.stringify([painted(old), old.changedIn], replacer) === JSON.stringify([painted(row), row.changedIn], replacer);
        if (!sameColumns) {
            if (old === row) expect(painted(row), `${where}: ${keyOf(row)} is the instance shown before, with other values`).toEqual(painted(old));
            continue;
        }
        expect(old === row, `${where}: ${keyOf(row)} ${same ? 'did not change, and is a new instance' : 'changed, and is the instance shown before'}`).toBe(same);
    }
    const reordered = JSON.stringify(before.rows.map(keyOf)) !== JSON.stringify(after.rows.map(keyOf));
    expect(before.metadata.rowSequenceVersion !== after.metadata.rowSequenceVersion,
        `${where}: the rows ${reordered ? 'moved' : 'kept their order'}`).toBe(reordered);
}

// JSON for a painted row: an exact value's mantissa is a bigint, and a number may be −0 or not finite.
function replacer(_key: string, value: unknown): unknown {
    if (typeof value === 'bigint') return `${value}n`;
    if (typeof value === 'number' && (Object.is(value, -0) || !Number.isFinite(value))) return `${String(value)}${Object.is(value, -0) ? '-0' : ''}`;
    return value;
}

/** One random Change Batch over `held`, applied to `data`; `held` follows it. */
function applyBatch(next: () => number, data: SnapshotPivotSource, held: Map<number, Row>, fresh: () => number): void {
    const added: Row[] = [];
    const changed: Row[] = [];
    const removed: number[] = [];
    const touched = new Set<number>();
    for (let n = 1 + between(next, 0, 4); n > 0; n--) {
        const choice = between(next, 0, 10);
        if (choice < 2 || held.size < 20) {
            let row = rowOf(fresh(), next);
            // Now and then an Item no record had.
            if (between(next, 0, 3) === 0) row = { ...row, desk: `New${between(next, 0, 3)}` };
            added.push(row);
            touched.add(row.id);
            continue;
        }
        const id = entry([...held.keys()], between(next, 0, held.size));
        if (touched.has(id)) continue;
        touched.add(id);
        const old = held.get(id);
        if (old === undefined) throw new Error(`No record ${id} is held.`);
        if (choice < 4) {
            removed.push(id);
        } else if (choice < 7) {
            // Only the measures move: an order by value, a total or a percentage may change with no Item
            // appearing or leaving.
            const moved = rowOf(id, next);
            changed.push({ ...old, amount: (Number(moved.amount) * 10).toFixed(2), measure: moved.measure });
        } else {
            changed.push({ ...rowOf(id, next), desk: next() < 0.5 ? old.desk : `Desk${between(next, 0, 8)}` });
        }
    }
    data.apply(createChangeBatch({ added: records(added), changed: records(changed), removedKeys: removed }));
    for (const row of [...added, ...changed]) held.set(row.id, row);
    for (const id of removed) held.delete(id);
}

describe('the incremental computation (ADR-0153)', () => {
    for (const seed of [20261008, 153, 4242, 7, 99991, 31337]) {
        it(`ADR-0153 (LV-24/LV-29/LV-30): over random layouts and Change Batches, seed ${seed}, the incremental Window equals a fresh computation, keeps the instance of every row it did not change, and moves the Row Sequence Version exactly when the order moves`, { timeout: 60_000 }, async () => {
            const next = random(seed);
            for (let drawn = 0; drawn < 6; drawn++) {
                const layout = drawLayout(next, (seed % 1000) + drawn);
                const held = new Map(Array.from({ length: 120 }, (_, i) => [i, rowOf(i, next)]));
                let nextId = 1000;
                const data = new SnapshotPivotSource(records([...held.values()]), fields);
                const source = new LocalPivotReportSource(data);
                const context = `seed ${seed}, layout ${drawn}: ${writePivotLayoutJson(layout)}`;
                let shown = answered(await source.window(request(layout)), context);
                await expectSameAsFresh(shown, data, layout, `${context}, first`);
                for (let step = 0; step < 12; step++) {
                    applyBatch(next, data, held, () => nextId++);
                    const where = `${context}, step ${step}`;
                    const after = answered(await source.window(request(layout, shown.metadata.version)), where);
                    await expectSameAsFresh(after, data, layout, where);
                    expectSharesUnchangedRows(shown, after, where);
                    shown = after;
                }
                source.dispose();
            }
        });
    }

    it('ADR-0153, ADR-0060: when the record that spelt an Item leaves, the Item and every row beneath it take the next spelling, in their labels and their Items', async () => {
        const rows = [{ ...rowOf(1, random(1)), region: 'east', desk: 'D0' }, { ...rowOf(2, random(2)), region: 'East', desk: 'D1' }];
        const data = new SnapshotPivotSource(records(rows), fields);
        const source = new LocalPivotReportSource(data);
        const layout = createPivotLayout({ rows: ['Region', 'Desk'], columns: ['Flag'], values: [{ field: 'Qty', aggregation: 'count' }] });
        const before = answered(await source.window(request(layout)), 'first');
        data.apply(createChangeBatch({ removedKeys: [1] }));
        const after = answered(await source.window(request(layout, before.metadata.version)), 'after');
        await expectSameAsFresh(after, data, layout, 'after');
        expect(after.rows.map(row => row.items.map(item => item.value))).toEqual([['East'], ['East', 'D1'], []]);
        source.dispose();
    });

    it('ADR-0153, PV-40: a Window cancelled at any of its slices — while batches are folded in, or while a layout is laid out aside — leaves the next one equal to a fresh computation, and the update after it incremental', { timeout: 300_000 }, async () => {
        // Enough leaves that an update, and the totals a maximum merges again, take many slices.
        const layout = createPivotLayout({
            rows: [{ field: 'Desk' }, { field: 'Qty', sort: pivotSort('descending', 0) }, { field: 'Day' }],
            columns: [{ field: 'Region' }, { field: 'Flag' }],
            values: [{ field: 'Amount', aggregation: 'sum' }, { field: 'Measure', aggregation: 'max' }],
        });
        const sorted = createPivotLayout({ ...layout, rows: [{ field: 'Desk', sort: pivotSort('descending', 1) }, { field: 'Qty' }, { field: 'Day' }] });
        for (const kind of ['value-only', 'structural', 'relayout'] as const) {
            // The slices the work takes, counted first with nothing cancelled.
            let total = 0;
            const stops = (): number[] => (kind === 'relayout'
                ? Array.from({ length: total }, (_, i) => i + 1)
                // Batches folded in place are not cancelled, so a slice at the start, the middle and the end stand for every other.
                : [1, Math.ceil(total / 2), total]);
            for (let stopAt = 0, at = 0; stopAt === 0 || at < stops().length; stopAt = entry(stops(), at++)) {
                const next = random(stopAt);
                const held = new Map(Array.from({ length: 3000 }, (_, i) => [i, rowOf(i, next)]));
                const data = new SnapshotPivotSource(records([...held.values()]), fields);
                let yields = 0;
                let stopping = Number.POSITIVE_INFINITY;
                const controller = new AbortController();
                const slicing: PivotSlicing = {
                    sliceBudgetMs: 0,
                    yield: () => {
                        yields++;
                        if (yields === stopping) controller.abort(new Error('superseded'));
                        return Promise.resolve();
                    },
                };
                const source = new LocalPivotReportSource(data, { slicing });
                const where = `${kind}, cancelled at slice ${stopAt}`;
                const first = answered(await source.window(request(layout)), where);
                const freshYields = yields;
                const shift = (ids: readonly number[], structural: boolean, removedKeys: readonly number[], added: readonly Row[]): void => {
                    const changed = ids.map(id => {
                        const old = held.get(id);
                        if (old === undefined) throw new Error(`No record ${id} is held.`);
                        const moved = rowOf(id, next);
                        return structural ? moved : { ...old, amount: moved.amount, measure: moved.measure };
                    });
                    data.apply(createChangeBatch({ changed: records(changed), added: records(added), removedKeys }));
                    for (const row of [...changed, ...added]) held.set(row.id, row);
                    for (const id of removedKeys) held.delete(id);
                };
                if (kind !== 'relayout') {
                    const structural = kind === 'structural';
                    shift(Array.from({ length: 900 }, (_, i) => 3 * i + 1), structural, structural ? [0, 3, 6] : [],
                        structural ? [{ ...rowOf(5000, next), desk: 'New0' }] : []);
                }
                yields = 0;
                stopping = stopAt === 0 ? Number.POSITIVE_INFINITY : stopAt;
                const cancelling = source.window(request(kind === 'relayout' ? sorted : layout, first.metadata.version), controller.signal);
                // Work laid out aside stops at the slice; batches folded in place are carried through whole, and the
                // cancellation is not heard.
                if (kind === 'relayout' && stopAt > 0) await expect(cancelling, `${where}: of ${total} slices`).rejects.toThrow('superseded');
                else answered(await cancelling, where);
                if (stopAt === 0) total = yields;
                stopping = Number.POSITIVE_INFINITY;
                const shown = kind === 'relayout' ? sorted : layout;
                const again = answered(await source.window(request(shown, first.metadata.version)), where);
                await expectSameAsFresh(again, data, shown, `${where}, the Window after`);
                shift([17], kind === 'structural', [], []);
                yields = 0;
                const later = answered(await source.window(request(shown, again.metadata.version)), where);
                await expectSameAsFresh(later, data, shown, `${where}, the update after`);
                expectSharesUnchangedRows(again, later, `${where}, the update after`);
                // An update of one record takes a few slices, where a fresh computation takes one for every piece of the data.
                expect(yields, `${where}: the update's slices, against a fresh computation's ${freshYields}`).toBeLessThan(freshYields / 4);
                source.dispose();
            }
            expect(total, `${kind}: the work cancelled takes several slices`).toBeGreaterThan(4);
        }
    });
});
