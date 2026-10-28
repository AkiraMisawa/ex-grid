// The bridge's own tests (ADR-0370 section 1): what crosses between .NET and ExPivot's Script Core. What the pivot
// draws and decides is the Script Core's, tested there, and what the engines answer is the shared cases'; here, that
// a question crosses to .NET as PivotJson and its answer comes back as the answer .NET gave, that a question
// abandoned is cancelled in .NET, that a Refresh is answered before the question after it, and that a field and a
// notice arrive as .NET declared them.
import { describe, expect, it } from 'vitest';
import { gridCommandIds } from '@ex-grid/core';
import {
    LocalPivotReportSource, PivotJson, PivotSourceFeatures, RecordPivotSource, createPivotLayout, pivotQuery, pivotRecordField, pivotWordIdList,
    type PivotSourceChanged,
} from '@ex-grid/pivot-engine';
import { dressedChrome, labelIds, wireSelection, type WireOptions } from '../src/blazor-pivot';
import { DotNetPivotSource, type DotNetHandle, type WireSource } from '../src/dotnet-pivot-source';

interface Trade {
    readonly region: string;
    readonly amount: number;
}

const fields = [
    pivotRecordField<Trade>({ name: 'Region', type: 'text', value: t => t.region }),
    pivotRecordField<Trade>({ name: 'Amount', type: 'number', value: t => t.amount }),
];

const trades: readonly Trade[] = [
    { region: 'East', amount: 3 }, { region: 'West', amount: 4 }, { region: 'East', amount: 5 },
];

/** .NET, as the bridge calls it: every call recorded, a question answered by the bundled source over the trades, as
 * a C# source would, unless `hold` keeps it waiting. */
class FakeDotNet implements DotNetHandle {
    readonly calls: { readonly method: string; readonly args: readonly unknown[] }[] = [];
    inner = new RecordPivotSource(trades, fields);
    hold: Promise<void> | undefined;
    failing: string | undefined;
    refreshed: (() => void) | undefined;
    refreshFailed: ((error: Error) => void) | undefined;

    async invokeMethodAsync(method: string, ...args: readonly unknown[]): Promise<unknown> {
        this.calls.push({ method, args });
        switch (method) {
            case 'Aggregate': {
                if (this.hold !== undefined) await this.hold;
                // PivotSourceCrossing.Failed: the source's own words, in place of an answer.
                if (this.failing !== undefined) return JSON.stringify({ failed: this.failing });
                return PivotJson.writeAnswer(this.inner.aggregateNow(PivotJson.readQuery(String(args[2]))));
            }
            case 'Refresh':
                return new Promise<void>((resolve, reject) => {
                    this.refreshed = resolve;
                    this.refreshFailed = reject;
                });
            case 'Cancel':
                return null;
            default:
                throw new Error(`the bridge called ${method}`);
        }
    }
}

const wire: WireSource = {
    generation: 1,
    fields: [
        { name: 'Region', type: 'text', caption: 'Region', orderKey: false, format: null, itemOrder: ['West'], column: null, datePart: null },
        { name: 'Amount', type: 'number', caption: 'Amount', orderKey: false, format: '#,##0.00', itemOrder: [], column: null, datePart: null },
    ],
    aggregations: new RecordPivotSource([], fields).features.aggregations,
    canRefresh: true,
    countsItems: false,
    answersLevels: false,
};

/** A promise the test settles: `open` lets go what awaits it. */
function gate(): { readonly promise: Promise<void>; readonly open: () => void } {
    let open: (() => void) | undefined;
    const promise = new Promise<void>(resolve => { open = resolve; });
    if (open === undefined) throw new Error('A promise runs its executor at once.');
    return { promise, open };
}

const byRegion = pivotQuery({ rows: [{ field: 'Region' }], values: [{ field: 'Amount', parts: ['sum'] }] });

describe('a C# Pivot Source seen from the browser (ADR-0370 sections 1 and 3)', () => {
    it('declares the fields as .NET sent them', () => {
        const source = new DotNetPivotSource(new FakeDotNet(), wire);
        expect(source.fields.map(field => [field.name, field.type, field.format, field.itemOrder])).toEqual([
            ['Region', 'text', undefined, ['West']],
            ['Amount', 'number', '#,##0.00', []],
        ]);
        expect(source.features).toEqual(new PivotSourceFeatures(wire.aggregations, true, false, false));
    });

    it('asks .NET each question as PivotJson, under its generation, and answers with what .NET answered', async () => {
        const dotnet = new FakeDotNet();
        const source = new DotNetPivotSource(dotnet, wire);
        const answer = await source.aggregate(byRegion);

        const [asked] = dotnet.calls;
        expect(asked?.method).toBe('Aggregate');
        expect(asked?.args[0]).toBe(1);
        expect(PivotJson.readQuery(String(asked?.args[2]))).toEqual(byRegion);
        expect(PivotJson.writeAnswer(answer)).toBe(PivotJson.writeAnswer(dotnet.inner.aggregateNow(byRegion)));
    });

    it('fails a question the C# source failed, in the source\'s own words', async () => {
        const dotnet = new FakeDotNet();
        dotnet.failing = 'The page cut the server off.';
        const source = new DotNetPivotSource(dotnet, wire);
        await expect(source.aggregate(byRegion)).rejects.toThrow('The page cut the server off.');
    });

    it('cancels in .NET a question the pivot abandoned', async () => {
        const dotnet = new FakeDotNet();
        const held = gate();
        dotnet.hold = held.promise;
        const source = new DotNetPivotSource(dotnet, wire);
        const controller = new AbortController();
        const asked = source.aggregate(byRegion, controller.signal);
        await Promise.resolve();

        controller.abort();
        held.open();
        await asked;
        const call = dotnet.calls.find(each => each.method === 'Aggregate')?.args[1];
        expect(dotnet.calls.filter(each => each.method === 'Cancel').map(each => each.args[0])).toEqual([call]);
    });

    it('answers a question asked after a Refresh only once .NET has refreshed', async () => {
        const dotnet = new FakeDotNet();
        const source = new DotNetPivotSource(dotnet, wire);
        source.refresh();
        const asked = source.aggregate(byRegion);
        await new Promise(resolve => { setTimeout(resolve, 0); });
        expect(dotnet.calls.map(call => call.method)).toEqual(['Refresh']);

        dotnet.refreshed?.();
        await asked;
        expect(dotnet.calls.map(call => call.method)).toEqual(['Refresh', 'Aggregate']);
    });

    it('fails the questions that waited for a Refresh .NET failed, and asks those after them', async () => {
        const dotnet = new FakeDotNet();
        const source = new DotNetPivotSource(dotnet, wire);
        source.refresh();
        const waited = source.aggregate(byRegion);
        dotnet.refreshFailed?.(new Error('the database is down'));
        await expect(waited).rejects.toThrow('the database is down');

        await source.aggregate(byRegion);
        expect(dotnet.calls.map(call => call.method)).toEqual(['Refresh', 'Aggregate']);
    });

    it('declares a date part with the format and the order .NET gave it', () => {
        const source = new DotNetPivotSource(new FakeDotNet(), {
            ...wire,
            fields: [{ name: 'Month', type: 'date', caption: 'Month', orderKey: false, format: 'MMM', itemOrder: [], column: 'Date', datePart: 'month' }],
        });
        expect(source.fields.map(field => [field.name, field.column, field.datePart, field.format])).toEqual([['Month', 'Date', 'month', 'MMM']]);
    });

    it('passes on the notice .NET heard its source say, to whoever listens', () => {
        const source = new DotNetPivotSource(new FakeDotNet(), wire);
        const heard: PivotSourceChanged[] = [];
        const stop = source.subscribe(changed => { heard.push(changed); });
        source.hear(PivotJson.writeSourceChanged({ sourceVersion: 'v2' }));
        stop();
        source.hear(PivotJson.writeSourceChanged({ sourceVersion: 'v3' }));
        expect(heard.map(changed => changed.sourceVersion)).toEqual(['v2']);
    });
});

describe('the words .NET answers for the pivot (ADR-0370 section 10)', () => {
    it('asks every id the pivot paints, its grid\'s commands included, once each', () => {
        const ids = labelIds();
        expect(ids).toEqual(expect.arrayContaining([...pivotWordIdList(), gridCommandIds.copy, gridCommandIds.sortAscending]));
        expect(new Set(ids).size).toBe(ids.length);
    });
});

const wireOptions = (stylesheets?: WireOptions['stylesheets']): WireOptions => ({ locale: 'en-US', hears: [], ...(stylesheets === undefined ? {} : { stylesheets }) });

describe('the Chrome the pivot is handed (ADR-0123, ERR-11)', () => {
    const mud = { name: 'mud-ex-pivot.css', stamp: 'a'.repeat(64) };

    it('ERR-11: carries the stylesheets the Consumer\'s Chrome brings beside the slots .NET fills, the same object while neither changes', () => {
        const slots = { toolbar: () => () => undefined };
        const chrome = dressedChrome(slots);
        expect(chrome(wireOptions())).toBe(slots);
        const dressed = chrome(wireOptions([mud]));
        expect(dressed).toEqual({ toolbar: slots.toolbar, stylesheets: [mud] });
        expect(chrome(wireOptions([{ ...mud }]))).toBe(dressed);
        expect(chrome(wireOptions())).toBe(slots);
        // With no slot filled with Razor, a Chrome that brings a stylesheet is one still.
        expect(dressedChrome(undefined)(wireOptions([mud]))).toEqual({ stylesheets: [mud] });
    });
});

describe('the report\'s Selection as .NET reads it (ADR-0201\'s note of 2026-10-27)', () => {
    it('ADR-0201: no Selection crosses as no range and no Focus, which .NET reads as the empty Selection', () => {
        expect(wireSelection({ ranges: [], focus: undefined, focusRange: undefined })).toEqual({ ranges: [], focus: null, focusRangeIndex: 0 });
    });

    it('ADR-0052: the range holding the Focus is named by identity, so of two alike rectangles the one that holds it is named', () => {
        const first = { topRow: 1, leftColumn: 1, rowCount: 1, columnCount: 1 };
        const second = { ...first };
        expect(wireSelection({ ranges: [first, second], focus: { row: 1, column: 1 }, focusRange: second })).toEqual({
            ranges: [first, second], focus: { row: 1, column: 1 }, focusRangeIndex: 1,
        });
    });
});

/** .NET answering a field's Order Key as PivotSourceCrossing writes it (ADR-0412): each answer that names Items comes
 * after a line of their keys, here the given entry for each Region, and the next answers' keys as `then` gives them. */
class KeyedDotNet extends FakeDotNet {
    readonly answers: Readonly<Record<string, unknown>>[];

    constructor(...answers: Readonly<Record<string, unknown>>[]) {
        super();
        this.answers = answers;
    }

    override async invokeMethodAsync(method: string, ...args: readonly unknown[]): Promise<unknown> {
        const answer = await super.invokeMethodAsync(method, ...args);
        if (method !== 'Aggregate') return answer;
        const keys = this.answers.length > 1 ? this.answers.shift() : this.answers[0];
        if (keys === undefined) throw new Error('The test gave no keys.');
        const items = Object.entries(keys).map(([region, key]) => [{ kind: 'text', value: region }, key]);
        return `${JSON.stringify({ orderKeys: [{ field: 'Region', items }] })}\n${String(answer)}`;
    }
}

const keyedWire: WireSource = {
    ...wire,
    fields: wire.fields.map(field => (field.name === 'Region' ? { ...field, orderKey: true, itemOrder: [] } : field)),
};

const byRegionReport = {
    layout: createPivotLayout({ rows: ['Region'], values: [{ field: 'Amount', aggregation: 'sum' }], grandTotalRow: false }),
    settings: { locale: 'en-US', words: {}, changeHighlightMs: 0 },
    window: { start: 0, count: 100 },
};

// The regions as the report orders them, or why it refused, each answer's keys as given in turn.
async function regionsOrdered(...answers: Readonly<Record<string, unknown>>[]): Promise<readonly string[] | string> {
    const dotnet = new KeyedDotNet(...answers);
    const source = new DotNetPivotSource(dotnet, keyedWire);
    const reports = new LocalPivotReportSource(source);
    let update = await reports.window({ requestId: 'r1', ...byRegionReport });
    for (let next = 2; next <= answers.length; next++) {
        // Asked afresh, under the version .NET answers next.
        dotnet.inner = new RecordPivotSource([...trades], fields);
        source.hear(PivotJson.writeSourceChanged({ sourceVersion: `v${next}` }));
        update = await reports.window({ requestId: `r${next}`, ...byRegionReport });
    }
    reports.dispose();
    if (update.refusal !== undefined) return update.refusal.message;
    return update.rows.map(row => String(row.items.at(-1)?.value));
}

const int = (value: number): unknown => ({ type: 'System.Int32', bigint: String(value) });

describe('a C# Order Key, answered with its Items (ADR-0412)', () => {
    it('ADR-0412: the report orders a field\'s Items by the keys .NET answered with them', async () => {
        expect(await regionsOrdered({ East: int(2), West: int(1) })).toEqual(['West', 'East']);
        expect(await regionsOrdered({ East: null, West: { type: 'System.Double', number: '-0.5' } })).toEqual(['West', 'East']);
    });

    it('ADR-0412: an answer under another version of the data is ordered by the keys it came with', async () => {
        expect(await regionsOrdered({ East: int(2), West: int(1) }, { East: int(1), West: int(2) })).toEqual(['East', 'West']);
    });

    it('ADR-0412, ADR-0060: a key .NET failed to give is refused only as the report orders by it, naming the field and the Item', async () => {
        expect(await regionsOrdered({ East: { threw: 'no rank for East' }, West: null })).toBe('The Order Key of Region failed on \'East\'.');
        expect(await regionsOrdered({ East: { refused: 'The Order Key of Region gave \'East\' a key of type Version.' }, West: null }))
            .toBe('The Order Key of Region gave \'East\' a key of type Version.');
    });

    it('ADR-0412: keys of two .NET types that cross alike are refused, whichever answer brought them', async () => {
        const answers = [{ East: int(2) }, { West: { type: 'System.Decimal', bigint: '10000000000000000000000000000' } }];
        const dotnet = new KeyedDotNet(...answers);
        const source = new DotNetPivotSource(dotnet, keyedWire);
        await source.aggregate(byRegion);
        await source.aggregate(byRegion);
        const regionKey = source.fields[0]?.orderKey;
        expect(regionKey?.('East')).toBe(2n);
        expect(() => regionKey?.('West')).toThrow('The Order Key of Region gave \'West\' a key of type System.Decimal after keys of type System.Int32');
    });
});
