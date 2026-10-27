// The bridge's own tests (ADR-0370 section 1): what crosses between .NET and ExPivot's Script Core. What the pivot
// draws and decides is the Script Core's, tested there, and what the engines answer is the shared cases'; here, that
// a question crosses to .NET as PivotJson and its answer comes back as the answer .NET gave, that a question
// abandoned is cancelled in .NET, that a Refresh is answered before the question after it, and that a field and a
// notice arrive as .NET declared them.
import { describe, expect, it } from 'vitest';
import {
    PivotJson, PivotSourceFeatures, RecordPivotSource, pivotQuery, pivotRecordField, type PivotSourceChanged,
} from '@ex-grid/pivot-engine';
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
    readonly inner = new RecordPivotSource(trades, fields);
    hold: Promise<void> | undefined;
    refreshed: (() => void) | undefined;
    refreshFailed: ((error: Error) => void) | undefined;

    async invokeMethodAsync(method: string, ...args: readonly unknown[]): Promise<unknown> {
        this.calls.push({ method, args });
        switch (method) {
            case 'Aggregate': {
                if (this.hold !== undefined) await this.hold;
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
        { name: 'Region', type: 'text', caption: 'Region', format: null, itemOrder: ['West'], column: null, datePart: null },
        { name: 'Amount', type: 'number', caption: 'Amount', format: '#,##0.00', itemOrder: [], column: null, datePart: null },
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
            fields: [{ name: 'Month', type: 'date', caption: 'Month', format: 'MMM', itemOrder: [], column: 'Date', datePart: 'month' }],
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
