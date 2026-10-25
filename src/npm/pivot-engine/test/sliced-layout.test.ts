// The work after a pass, in slices (ADR-0066, PV-40): the cube made from an answer and the report laid out from
// a cube yield whenever a slice is spent, stop at the next yield once their signal is cancelled, and give
// exactly what the synchronous forms give — over every report case's layout, and over an answer of some
// eighteen thousand leaves. How the work paces itself is the Script Core's own, and held here; what a report
// says is the cases'.
import { createSnapshot, type Snapshot } from '@ex-grid/data';
import { describe, expect, it } from 'vitest';
import { cubeParts, type AxisNode, type PivotCube } from '../src/cube/cube';
import { sortedSteps } from '../src/source/slicing';
import {
    aggregatePivot, computePivot, pivotCube, pivotCubeAsync, pivotReport, pivotReportAsync, queryForLayout, RecordPivotSource, SnapshotPivotSource,
    createPivotLayout, type PivotAnswer, type PivotField, type PivotLayout, type PivotLayoutInput, type PivotQuery, type PivotReport, type PivotSlicing,
} from '../src/index';
import { array, caseFiles, dataSet, layoutOf, must, object, optionsOf, refusalOf, text, type DataSet, type Json, type JsonObject } from './cases';
import { everyReading, read } from './readings';

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

const noData: DataSet = { fields: [], records: [], keyCalls: [] };

// A report read back whole, as the cases read one.
function whole(report: PivotReport): Json {
    return read(report, everyReading, noData);
}

// A large report's rows, columns and spans, which is all its layout decides: its values are read from the same
// cube by the same reader whichever way it was laid out.
function laidOut(report: PivotReport): Json {
    return {
        rows: report.rows.map(row => `${row.key.toString()} ${row.labels.map(label => `${label.indent}${label.text ?? ''}${label.toggle?.isCollapsed === true ? '+' : ''}`).join('|')}`), // oxlint-disable-line ex-grid/no-stand-in-default -- a label with no text reads as nothing, as the cases read it
        columns: report.valueColumns.map(column => column.name),
        spans: report.headerSpans.map(span => `${span.label}@${span.firstColumn}+${span.columnCount} t${span.tier}x${span.tierSpan}`),
    };
}

// A tree's nodes, depth first.
function nodesOf(root: AxisNode): AxisNode[] {
    return [root, ...root.children.flatMap(nodesOf)];
}

function named(node: AxisNode): string {
    return node.item === undefined ? 'root' : `${node.level}:${JSON.stringify(node.item.publicKey)}`;
}

// A value as JSON, a bigint and a double written whole.
function written(value: unknown): string {
    return JSON.stringify(value, (_, held: unknown) => (typeof held === 'bigint' || typeof held === 'number' ? String(held) : held));
}

// What a synchronous form refuses.
function refusal(work: () => unknown): JsonObject {
    try {
        work();
    } catch (error) { // oxlint-disable-line ex-grid/no-catch -- the refusal is what is compared
        return refusalOf(error);
    }
    throw new Error('Nothing was refused.');
}

// What a cube holds: each tree, an Item a node, and what every cell of the layout's Value Fields reads.
function cubeJson(cube: PivotCube, layout: PivotLayout): Json {
    const parts = cubeParts(cube);
    const rows = nodesOf(parts.rowRoot);
    const columns = nodesOf(parts.columnRoot);
    return {
        rows: rows.map(named),
        columns: columns.map(named),
        included: cube.includedRecordCount,
        cells: layout.values.map(value => rows.flatMap(row => columns.map(column =>
            written(parts.read(row, column, parts.sources.indexOf(value.field), value.aggregation))))),
    };
}

// What a reading gives, or the refusal it meets.
async function attempt(reading: () => Promise<Json>): Promise<Json> {
    try {
        return await reading();
    } catch (error) { // oxlint-disable-line ex-grid/no-catch -- a refusal is the reading's answer, compared below
        return refusalOf(error);
    }
}

describe('The cube and the report, in slices', () => {
    for (const file of caseFiles('pivotReport')) {
        it(`PV-40: every layout of pivot/${file.file} is laid out in slices as at once, and refused as at once`, async () => {
            expect.hasAssertions();
            for (const entry of file.cases) {
                const name = text(must(entry, 'name'));
                // A case that names a refusal may meet it anywhere, its cube included, and has nothing more to
                // slice once it does; any other case is laid out, and its cube made, whole.
                const refuses = entry['throws'] !== undefined;
                const options = optionsOf(file.document, entry);
                let data: DataSet;
                let layout: PivotLayout;
                let cube: PivotCube;
                try {
                    data = dataSet(file.document, entry);
                    layout = layoutOf(must(entry, 'layout'));
                    const cubeOf = entry['cubeOf'];
                    cube = aggregatePivot(data.records, data.fields, cubeOf === undefined ? layout : layoutOf(cubeOf));
                } catch (error) {
                    if (refuses) continue;
                    throw error;
                }
                const expected = await attempt(() => Promise.resolve(whole(pivotReport(cube, layout, options))));
                const pieces = everyStep();
                expect(await attempt(async () => whole(await pivotReportAsync(cube, layout, options, pieces.slicing))), name).toEqual(expected);
                if (refuses) continue;

                // Rows compared in slices compare as at once.
                const report = pivotReport(cube, layout, options);
                for (const compared of entry['sameRowsAs'] === undefined ? [] : array(entry['sameRowsAs'])) {
                    const other = object(compared);
                    const records = other['records'] === undefined ? data.records : dataSet(file.document, { ...entry, records: other['records'] }).records;
                    const otherReport = computePivot(records, data.fields, other['layout'] === undefined ? layout : layoutOf(other['layout']), options);
                    expect(await report.hasSameRowsAsAsync(otherReport, everyStep().slicing), name).toBe(report.hasSameRowsAs(otherReport));
                }

                // The cube of the same answer, made in slices, is the cube made at once.
                const source = new RecordPivotSource(data.records, data.fields);
                const query = queryForLayout(layout, Number.MAX_SAFE_INTEGER);
                const answer = await source.aggregate(query);
                const atOnce = pivotCube(query, answer, data.fields);
                const sliced = await pivotCubeAsync(query, answer, data.fields, everyStep().slicing);
                expect(cubeJson(sliced, layout), name).toEqual(cubeJson(atOnce, layout));
            }
        });
    }
});

const books = 60;

/** Twenty thousand trades over sixty books, three hundred days and four products, and a price of each. */
function trades(): Snapshot {
    const count = 20_000;
    return createSnapshot({
        columns: [
            { name: 'Book', kind: 'text', values: Array.from({ length: count }, (_, i) => `Book ${i % books}`) },
            { name: 'Day', kind: 'integer', values: Array.from({ length: count }, (_, i) => Math.floor(i / books) % 300) },
            { name: 'Product', kind: 'text', values: Array.from({ length: count }, (_, i) => `Product ${i % 7 % 4}`) },
            { name: 'Notional', kind: 'decimal', values: Array.from({ length: count }, (_, i) => ({ mantissa: BigInt(((i * 7919) % 100_003) * 5), scale: 2 })) },
            { name: 'Price', kind: 'double', values: Array.from({ length: count }, (_, i) => ((i * 7919) % 10_007) / 16) },
        ],
    });
}

const largeInput: PivotLayoutInput = {
    rows: ['Day', 'Book'],
    columns: ['Product'],
    values: ['Notional', { field: 'Price', aggregation: 'stdDev' }, { field: 'Price', aggregation: 'max' }],
};
const large = createPivotLayout(largeInput);

interface Answered {
    readonly fields: readonly PivotField[];
    readonly query: PivotQuery;
    readonly answer: PivotAnswer;
    readonly cube: PivotCube;
}

const snapshot = trades();

async function answered(layout: PivotLayout): Promise<Answered> {
    const source = new SnapshotPivotSource(snapshot, undefined, { sliceBudgetMs: 86_400_000 });
    const query = queryForLayout(layout, Number.MAX_SAFE_INTEGER);
    const answer = await source.aggregate(query);
    return { fields: source.fields, query, answer, cube: pivotCube(query, answer, source.fields) };
}

function byKey(a: { readonly key: number }, b: { readonly key: number }): number {
    return a.key - b.key;
}

describe('Siblings ordered in slices', () => {
    it('PV-40: a list sorted a piece at a time is the list toSorted sorts, stably, at every length', () => {
        let seed = 7;
        const next = (): number => {
            seed = (seed * 1_103_515_245 + 12_345) % 2_147_483_648;
            return seed / 2_147_483_648;
        };
        for (const length of [0, 1, 2, 127, 128, 129, 1000, 1023, 1024, 1025, 4097]) {
            // Few keys, so most comparisons tie, and the place a tie keeps is the stability held; and the list
            // sorted already, and backwards.
            const random = Array.from({ length }, (_, at) => ({ key: Math.floor(next() * 40), at }));
            for (const [kind, list] of [['random', random], ['sorted', random.toSorted(byKey)], ['backwards', random.toSorted(byKey).toReversed()]] as const) {
                let yields = 0;
                let comparisons = 0;
                const steps = sortedSteps(list, (a, b) => {
                    comparisons++;
                    return byKey(a, b);
                });
                let step = steps.next();
                while (step.done !== true) {
                    yields++;
                    step = steps.next();
                }
                expect(step.value, `${length} ${kind}`).toEqual(list.toSorted(byKey));
                // A short list is sorted at once; a long one yields as it merges; one in order already costs a
                // comparison an element.
                if (length <= 128) expect(yields, `${length} ${kind}`).toBe(0);
                if (length >= 1000) expect(yields, `${length} ${kind}`).toBeGreaterThan(length / 1024);
                if (kind === 'sorted' && length > 128) expect(comparisons, `${length} sorted`).toBeLessThan(length);
            }
        }
    });
});

describe('A large answer, in slices', { timeout: 20_000 }, () => {
    it('PV-40: its cube and its report yield between slices, and give what the synchronous forms give', async () => {
        const { fields, query, answer, cube } = await answered(large);
        expect(answer.leafCount).toBeGreaterThan(15_000);
        const cubePieces = everyStep();
        const sliced = await pivotCubeAsync(query, answer, fields, cubePieces.slicing);
        expect(cubePieces.yields()).toBeGreaterThan(answer.leafCount / 1024);
        expect(cubeJson(sliced, large)).toEqual(cubeJson(cube, large));
        // Every exact sum in one form (ADR-0064), in every piece the cells were written in: 0.50 is 0.5.
        const parts = cubeParts(sliced);
        const sums = nodesOf(parts.rowRoot).flatMap(row => nodesOf(parts.columnRoot).map(column => parts.read(row, column, 0, 'sum')));
        expect(sums.length).toBeGreaterThan(3 * 1024);
        expect(sums.filter(sum => sum.kind === 'number' && sum.exact !== undefined && sum.exact.scale > 0 && sum.exact.mantissa % 10n === 0n)).toEqual([]);
        for (const layout of [
            large,
            createPivotLayout({ ...largeInput, form: 'tabular', rows: [{ field: 'Day', sort: { direction: 'descending' } }, { field: 'Book', sort: { direction: 'descending', byValue: 0 } }] }),
            createPivotLayout({ ...largeInput, rows: [{ field: 'Day', collapsed: true }, 'Book'], valuesAxis: 'rows' }),
        ]) {
            const reportPieces = everyStep();
            const report = await pivotReportAsync(cube, layout, undefined, reportPieces.slicing);
            expect(reportPieces.yields()).toBeGreaterThan(4);
            expect(laidOut(report)).toEqual(laidOut(pivotReport(cube, layout)));
        }
    });

    it('PV-40: rows compared in slices compare as at once, and yield between slices', async () => {
        const { cube } = await answered(large);
        const report = pivotReport(cube, large);
        const collapsed = pivotReport(cube, createPivotLayout({ ...largeInput, rows: [{ field: 'Day', collapsed: true }, 'Book'] }));
        const again = everyStep();
        expect(await report.hasSameRowsAsAsync(pivotReport(cube, large), again.slicing)).toBe(true);
        expect(again.yields()).toBeGreaterThan(10);
        for (const other of [collapsed, report, undefined]) {
            expect(await report.hasSameRowsAsAsync(other, everyStep().slicing)).toBe(report.hasSameRowsAs(other));
        }
        const stopped = everyStep(2);
        await expect(report.hasSameRowsAsAsync(pivotReport(cube, large), stopped.slicing, stopped.signal)).rejects.toThrow('superseded');
        expect(stopped.yields()).toBe(2);
    });

    it('PV-40: thousands of siblings are ordered in slices, by label and by value, as at once', async () => {
        for (const layout of [
            createPivotLayout({ rows: ['Price'], values: ['Notional'] }),
            createPivotLayout({ rows: [{ field: 'Price', sort: { direction: 'descending' } }], values: ['Notional'] }),
            createPivotLayout({ rows: [{ field: 'Notional', sort: { direction: 'descending', byValue: 0 } }], values: ['Price'] }),
            createPivotLayout({ rows: ['Product', { field: 'Notional', sort: { direction: 'ascending', byValue: 0 } }], columns: ['Book'], values: [{ field: 'Price', aggregation: 'min' }] }),
        ]) {
            const { cube } = await answered(layout);
            const root = cubeParts(cube).rowRoot;
            expect(Math.max(root.children.length, ...root.children.map(child => child.children.length))).toBeGreaterThan(1000);
            expect(laidOut(await pivotReportAsync(cube, layout, undefined, everyStep().slicing))).toEqual(laidOut(pivotReport(cube, layout)));
        }
    });

    it('PV-40: cancelled work stops at the next yield and rejects with the signal\'s reason; work cancelled before it starts does nothing', async () => {
        const { fields, query, answer, cube } = await answered(large);
        const counted = everyStep();
        await pivotCubeAsync(query, answer, fields, counted.slicing);
        const cubeYields = counted.yields();
        await pivotReportAsync(cube, large, undefined, counted.slicing);
        const reportYields = counted.yields() - cubeYields;
        for (const stopAt of [1, cubeYields - 1]) {
            const pieces = everyStep(stopAt);
            await expect(pivotCubeAsync(query, answer, fields, pieces.slicing, pieces.signal)).rejects.toThrow('superseded');
            expect(pieces.yields(), `the cube stopped at yield ${stopAt}`).toBe(stopAt);
        }
        for (const stopAt of [1, reportYields - 1]) {
            const pieces = everyStep(stopAt);
            await expect(pivotReportAsync(cube, large, undefined, pieces.slicing, pieces.signal)).rejects.toThrow('superseded');
            expect(pieces.yields(), `the report stopped at yield ${stopAt}`).toBe(stopAt);
        }
        const reason = new Error('superseded before it began');
        const before = everyStep();
        await expect(pivotCubeAsync(query, answer, fields, before.slicing, AbortSignal.abort(reason))).rejects.toBe(reason);
        await expect(pivotReportAsync(cube, large, undefined, before.slicing, AbortSignal.abort(reason))).rejects.toBe(reason);
        expect(before.yields()).toBe(0);
    });

    it('PV-40: what the synchronous forms refuse, the sliced forms reject with the same words', async () => {
        const { fields, answer, cube } = await answered(large);
        const other = queryForLayout(createPivotLayout({ rows: ['Book'] }));
        const elsewhere = createPivotLayout({ rows: ['Product'], values: ['Notional'] });
        expect(await attempt(async () => {
            await pivotCubeAsync(other, answer, fields, everyStep().slicing);
            return null;
        })).toEqual(refusal(() => pivotCube(other, answer, fields)));
        expect(await attempt(async () => whole(await pivotReportAsync(cube, elsewhere, undefined, everyStep().slicing))))
            .toEqual(refusal(() => pivotReport(cube, elsewhere)));
        await expect(pivotReportAsync(cube, large, undefined, { sliceBudgetMs: -1 })).rejects.toThrow(RangeError);
    });
});
