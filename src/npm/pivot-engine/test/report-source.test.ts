// The local report source's own conduct (ADR-0151 to ADR-0153, ADR-0068, PV-40), which the shared cases cannot
// write: a row its Window shows alike is the very row the Consumer holds, a change is stamped on the Consumer's
// clock once, a Window is computed in slices and stops when cancelled, and label geometry is estimated by
// character class and glyph. What a Window, Copy, the Summary, Items and Show Details answer is held by the
// cases (report-sources.test.ts).
import { createChangeBatch, createSnapshot, decimalFromText, type Snapshot } from '@ex-grid/data';
import { afterEach, describe, expect, it, vi } from 'vitest';
import {
    booleanItemKey, createPivotLayout, estimateLabelPx, LocalPivotReportSource, PivotChangeTimes, pivotCube, pivotReport, pivotReportDetailsQuery,
    queryForLayout, SnapshotPivotSource, textItemKey, type PivotLayout, type PivotReportAnswer, type PivotReportRequest, type PivotReportSettings,
    type PivotReportState, type PivotReportUpdate, type PivotSlicing,
} from '../src/index';

/** Entries keyed by their Id, a label and an exact amount. */
function entries(rows: readonly (readonly [number, string, string])[]): Snapshot {
    return createSnapshot({
        recordKey: 'Id',
        columns: [
            { name: 'Id', kind: 'integer', values: rows.map(r => r[0]) },
            { name: 'Label', kind: 'text', values: rows.map(r => r[1]) },
            { name: 'Amount', kind: 'decimal', values: rows.map(r => decimalFromText(r[2])) },
        ],
    });
}

function changed(id: number, label: string, amount: string): ReturnType<typeof createChangeBatch> {
    return createChangeBatch({ changed: entries([[id, label, amount]]) });
}

const layout: PivotLayout = createPivotLayout({ rows: [{ field: 'Label' }], values: [{ field: 'Amount', aggregation: 'sum', numberFormat: '0' }] });
const invariant: PivotReportSettings = { locale: 'invariant', words: {} };

let asked = 0;
function request(start: number, count: number, more: Partial<PivotReportRequest> = {}): PivotReportRequest {
    asked++;
    return { requestId: `r${asked}`, layout, settings: invariant, window: { start, count }, ...more };
}

function answered(update: PivotReportUpdate): PivotReportAnswer {
    if (update.refusal !== undefined) throw new Error(`Refused: ${update.refusal.message}`);
    return update;
}

function texts(state: PivotReportState): string[] {
    return state.rows.map(row => `${row.labels[0]?.text ?? ''} ${row.values[0]?.text ?? ''}`); // oxlint-disable-line ex-grid/no-stand-in-default -- an empty cell reads as nothing
}

describe('LocalPivotReportSource', () => {
    it('ADR-0153: a row the new Window shows alike is the very row the Consumer holds; a changed row and its total are new', async () => {
        const data = new SnapshotPivotSource(entries([[1, 'A', '10'], [2, 'B', '20'], [3, 'C', '30']]));
        const source = new LocalPivotReportSource(data);
        const before = answered(await source.window(request(0, 10)));
        data.apply(changed(2, 'B', '25'));
        const after = answered(await source.window(request(0, 10, { baseline: before.metadata.version })));
        expect(texts(after)).toEqual(['A 10', 'B 25', 'C 30', 'Grand Total 65']);
        expect(after.rows[0]).toBe(before.rows[0]);
        expect(after.rows[2]).toBe(before.rows[2]);
        expect(after.rows[1]).not.toBe(before.rows[1]);
        expect(after.rows[3]).not.toBe(before.rows[3]);
        // A change the number format hides paints alike and marks nothing, and is still a new row: its exact value
        // is what Copy and the Summary read.
        data.apply(changed(3, 'C', '30.2'));
        const hidden = answered(await source.window(request(0, 10, { baseline: after.metadata.version })));
        expect(texts(hidden)).toEqual(texts(after));
        expect(hidden.rows[2]).not.toBe(after.rows[2]);
        expect(hidden.rows[2]?.changedIn).toEqual([undefined]);
        expect(hidden.rows[0]).toBe(before.rows[0]);
        source.dispose();
    });

    it('ADR-0151, ADR-0063: the question for the records behind each cell, resolved from a Window\'s rows, is the engine\'s own question for that cell', async () => {
        const regions = ['East', 'North', 'West', 'East', 'West'];
        const products = ['Apples', 'Pears', 'Plums', 'Pears', 'Apples'];
        const data = new SnapshotPivotSource(createSnapshot({
            recordKey: 'Id',
            columns: [
                { name: 'Id', kind: 'integer', values: regions.map((_, i) => i + 1) },
                { name: 'Region', kind: 'text', values: regions },
                { name: 'Product', kind: 'text', values: products },
                { name: 'Online', kind: 'boolean', values: [true, false, true, true, false] },
                { name: 'Amount', kind: 'decimal', values: regions.map((_, i) => decimalFromText(String(10 * (i + 1)))) },
            ],
        }));
        const shape = createPivotLayout({
            filters: [{ field: 'Online', hiddenItems: [booleanItemKey(false)] }],
            rows: [{ field: 'Region', hiddenItems: [textItemKey('North')] }],
            columns: [{ field: 'Product' }],
            values: [{ field: 'Amount', aggregation: 'sum' }],
        });
        const source = new LocalPivotReportSource(data);
        const shown = answered(await source.window(request(0, 100, { layout: shape })));
        const query = queryForLayout(shape);
        const report = pivotReport(pivotCube(query, await data.aggregate(query), data.fields), shape, { locale: 'invariant' });
        expect(shown.rows.length).toBe(report.rows.length);
        shown.rows.forEach((row, r) => {
            const engineRow = report.rows[r];
            if (engineRow === undefined) throw new Error(`The report has no row ${r}.`);
            for (let c = -1; c < shown.metadata.valueColumns.length; c++) {
                expect(pivotReportDetailsQuery(shown.metadata, row, c)).toEqual(report.detailsQuery(engineRow, c));
            }
        });
        source.dispose();
    });

    it('ADR-0152: a version keeps the layout and settings it was laid out under, whatever the Consumer does to its own objects after', async () => {
        const source = new LocalPivotReportSource(new SnapshotPivotSource(entries([[1, 'A', '1']])));
        const words: Record<string, string> = {};
        const settings = { locale: 'invariant', words };
        const shown = answered(await source.window(request(0, 2, { settings })));
        words['grand-total'] = 'Everything';
        expect(shown.metadata.settings.words).toEqual({});
        expect(Object.isFrozen(shown.metadata.settings)).toBe(true);
        expect(Object.isFrozen(shown.metadata.layout)).toBe(true);
        // Asked again under the words changed, it lays the report out again in them.
        const again = answered(await source.window(request(0, 2, { settings, baseline: shown.metadata.version })));
        expect(again.metadata.version).not.toBe(shown.metadata.version);
        expect(texts(again)).toEqual(['A 1', 'Everything 1']);
        source.dispose();
    });

    it('ADR-0152 (spine 1): a Copy\'s cap that is not a whole number is refused, never read as no cap', async () => {
        const source = new LocalPivotReportSource(new SnapshotPivotSource(entries([[1, 'A', '1']])));
        const shown = answered(await source.window(request(0, 2)));
        const columns = [...shown.metadata.labelColumns, ...shown.metadata.valueColumns].map(c => c.name);
        for (const maxCells of [Number.NaN, Number.POSITIVE_INFINITY, -1]) {
            const copy = await source.copy({ version: shown.metadata.version, columns, ranges: [{ top: 0, left: 0, bottom: 1, right: 1 }], maxCells });
            expect(copy.refusal).toEqual({ kind: 'invalidRequest', message: 'Copy needs a whole, nonnegative cap on its cells.' });
        }
        source.dispose();
    });

    it('ADR-0153: a source disposed while a Window is computed publishes nothing, and the Window rejects by name', async () => {
        const made: { source?: LocalPivotReportSource } = {};
        const slicing: PivotSlicing = {
            sliceBudgetMs: 0,
            yield: () => {
                made.source?.dispose();
                return Promise.resolve();
            },
        };
        const rows = Array.from({ length: 5000 }, (_, i) => [i + 1, `L${i % 700}`, String(i)] as const);
        const source = new LocalPivotReportSource(new SnapshotPivotSource(entries(rows)), { slicing });
        made.source = source;
        await expect(source.window(request(0, 10))).rejects.toThrow('The report source is disposed; make another over the Pivot Source.');
    });

    it('ADR-0153: a disposed source refuses by name, and a negative or broken Window is refused before any work', async () => {
        const source = new LocalPivotReportSource(new SnapshotPivotSource(entries([[1, 'A', '1']])));
        await expect(source.window(request(0, 1.5))).rejects.toThrow(TypeError);
        await expect(source.window(request(0, 1, { settings: { ...invariant, labelMetrics: {
            wideWidthPx: 0, digitWidthPx: 8, narrowWidthPx: 4, fullWidthPx: 16, otherWidthPx: 11, cellHorizontalPaddingPx: 3,
        } } }))).rejects.toThrow('Glyph widths must be finite and positive.');
        source.dispose();
        await expect(source.window(request(0, 1))).rejects.toThrow('The report source is disposed; make another over the Pivot Source.');
        await expect(source.copy({ version: 'v', columns: [], ranges: [] })).rejects.toThrow('disposed');
    });

    it('PV-40: a Window is computed in slices, stops at the next slice once cancelled, and leaves the source answering the next request as a fresh one', async () => {
        const rows = Array.from({ length: 5000 }, (_, i) => [i + 1, `L${i % 700}`, String(i)] as const);
        let yields = 0;
        let stopAt = Number.POSITIVE_INFINITY;
        const controller = new AbortController();
        const slicing: PivotSlicing = {
            sliceBudgetMs: 0,
            yield: () => {
                yields++;
                if (yields === stopAt) controller.abort(new Error('superseded'));
                return Promise.resolve();
            },
        };
        const fresh = answered(await new LocalPivotReportSource(new SnapshotPivotSource(entries(rows))).window(request(0, 800)));
        const source = new LocalPivotReportSource(new SnapshotPivotSource(entries(rows)), { slicing });
        stopAt = 3;
        await expect(source.window(request(0, 800), controller.signal)).rejects.toThrow('superseded');
        expect(yields).toBe(3);
        stopAt = Number.POSITIVE_INFINITY;
        const next = answered(await source.window(request(0, 800)));
        expect(yields).toBeGreaterThan(3);
        expect(texts(next)).toEqual(texts(fresh));
        expect(next.metadata.rowCount).toBe(fresh.metadata.rowCount);
        source.dispose();
    });
});

// A run against a source whose clock is `skew` ms away from the Consumer's: what the Consumer's stamps say,
// step by step, as offsets from its own start.
async function run(skew: number): Promise<string[]> {
    const rows: [number, string, string][] = Array.from({ length: 6 }, (_, i) => [i, `L${i}`, String(10 * i)]);
    const data = new SnapshotPivotSource(entries(rows));
    const start = Date.UTC(2026, 9, 8, 9);
    let client = start;
    let server = start + skew;
    const source = new LocalPivotReportSource(data, { now: () => server });
    const times = new PivotChangeTimes();
    const settings: PivotReportSettings = { ...invariant, changeHighlightMs: 1000 };
    let held: PivotReportState | undefined;
    const steps: string[] = [];
    const read = async (step: string, first: number): Promise<void> => {
        const state = answered(await source.window(request(first, 2, { settings, ...(held === undefined ? {} : { baseline: held.metadata.version }) })));
        held = state;
        times.adopt(state, client, 1000);
        for (const row of state.rows) {
            const at = times.changedAt(row, 0);
            steps.push(`${step}: ${row.labels[0]?.text ?? ''} ${at === undefined ? '-' : `${at - start} ms`}`); // oxlint-disable-line ex-grid/no-stand-in-default -- a row with no label reads as nothing
        }
    };
    const advance = (ms: number): void => {
        client += ms;
        server += ms;
    };
    const change = (id: number, amount: string): void => {
        data.apply(changed(id, `L${id}`, amount));
    };
    await read('first', 0);
    advance(3000);
    change(0, '1');
    change(4, '41');
    await read('changed', 0);
    advance(400);
    await read('scrolled', 3);
    advance(400);
    change(5, '51');
    await read('changed again', 4);
    advance(300);
    await read('back', 0);
    advance(1000);
    change(1, '11');
    await read('later', 0);
    await read('later, scrolled', 4);
    source.dispose();
    return steps;
}

describe('LocalPivotReportSource.subscribe', () => {
    afterEach(() => {
        vi.restoreAllMocks();
    });

    it('ADR-0153: every listener hears a new Source Version, whatever another one throws, and the error is reported', () => {
        const reported: (() => void)[] = [];
        vi.spyOn(globalThis, 'queueMicrotask').mockImplementation(callback => {
            reported.push(callback);
        });
        const data = new SnapshotPivotSource(entries([[1, 'A', '1']]));
        const source = new LocalPivotReportSource(data);
        const heard: string[] = [];
        source.subscribe(() => {
            throw new Error('a view failed');
        });
        source.subscribe(notice => heard.push(notice.sourceVersion));
        data.apply(changed(1, 'A', '2'));
        expect(heard).toEqual([data.sourceVersion]);
        expect(reported).toHaveLength(1);
        expect(() => reported[0]?.()).toThrow('a view failed');
        source.dispose();
    });
});

describe('PivotChangeTimes', () => {
    it('ADR-0068, ADR-0153: highlight onset and expiry are the Consumer\'s: stamped as a change is first adopted, on its clock, whatever the source\'s clock says', async () => {
        const steps = await run(0);
        for (const skew of [-5000, 5000, 86_400_000]) expect(await run(skew)).toEqual(steps);
        expect(steps).toEqual([
            'first: L0 -', 'first: L1 -',
            // Adopted at 3,000 ms on the Consumer's clock: both changes of the version start there.
            'changed: L0 3000 ms', 'changed: L1 -',
            // L4 changed in that version, off screen: scrolled to, it shows as of then.
            'scrolled: L3 -', 'scrolled: L4 3000 ms',
            'changed again: L4 3000 ms', 'changed again: L5 3800 ms',
            // More than a second after it was published, the source no longer marks the first change: its
            // highlight, from 3,000 ms on the Consumer's clock, has ended too.
            'back: L0 -', 'back: L1 -',
            'later: L0 -', 'later: L1 5100 ms',
            'later, scrolled: L4 -', 'later, scrolled: L5 -',
        ]);
    });

    it('ADR-0068: a change the source lists is stamped once; unlisted and its highlight over, it is forgotten', async () => {
        const data = new SnapshotPivotSource(entries([[1, 'A', '1']]));
        const source = new LocalPivotReportSource(data);
        const first = answered(await source.window(request(0, 1)));
        data.apply(changed(1, 'A', '2'));
        const state = answered(await source.window(request(0, 1, { baseline: first.metadata.version })));
        const mark = state.rows[0]?.changedIn[0];
        expect(mark).toBe(state.metadata.version);
        const times = new PivotChangeTimes();
        times.adopt(state, 1000, 1000);
        times.adopt(state, 6000, 1000);
        expect(times.at(mark)).toBe(1000);
        times.adopt({ ...state, metadata: { ...state.metadata, changeMarks: [] }, rows: [] }, 7000, 1000);
        expect(times.at(mark)).toBeUndefined();
        source.dispose();
    });
});

describe('estimateLabelPx', () => {
    it('ADR-0151, ADR-0153: a label is estimated by its characters\' classes and the glyphs given, a supplementary one included', () => {
        const metrics = {
            wideWidthPx: 12, digitWidthPx: 8, narrowWidthPx: 4, fullWidthPx: 16, otherWidthPx: 11, cellHorizontalPaddingPx: 3,
            glyphWidths: { [0x57]: 14, [0x1D11E]: 13 },
        };
        expect(estimateLabelPx(metrics, '%1 日W𝄞x')).toBe(12 + 8 + 4 + 16 + 14 + 13 + 11 + 6);
        // A lone surrogate is a character of the other class, as .NET reads it.
        expect(estimateLabelPx(metrics, '\uD834')).toBe(11 + 6);
    });
});
