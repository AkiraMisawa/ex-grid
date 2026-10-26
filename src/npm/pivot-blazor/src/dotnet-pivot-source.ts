// The Pivot Source a Blazor ExPivot hands its Script Core (ADR-0370 sections 1 and 3): the Consumer's C#
// PivotSource, seen from the browser as a server is, through a FetchingPivotSource whose fetchers ask .NET. Each
// question and each answer crosses as the PivotJson both engines read and write, so neither side reads the
// other's objects. The engine in the browser builds the report and its totals from the Leaf Aggregates .NET
// answers, as it does from a server's.

import {
    FetchingPivotSource, PivotJson, PivotSourceFeatures, pivotField,
    type PivotAggregation, type PivotDatePart, type PivotField, type PivotFieldType, type PivotServerNotice,
} from '@ex-grid/pivot-engine';

/** What the bridge asks of the .NET object a Blazor ExPivot hands it: Blazor's `DotNetObjectReference`. */
export interface DotNetHandle {
    invokeMethodAsync(method: string, ...args: readonly unknown[]): Promise<unknown>;
}

/** A field as .NET declares it: its name, type and caption, the format its Items are labelled with, the values
 * its Items are declared in, and, for a date part, the Date column it is a part of. An Order Key is a .NET
 * function, which .NET refuses to send by name. */
export interface WireField {
    readonly name: string;
    readonly type: PivotFieldType;
    readonly caption: string;
    readonly format: string | null;
    readonly itemOrder: readonly (string | number | boolean)[];
    readonly column: string | null;
    readonly datePart: PivotDatePart | null;
}

/** What .NET says of its source: which of the component's sources it is, its fields, and what it answers. */
export interface WireSource {
    /** Which of the component's sources this is, counted from 1: what it asks of .NET names it, so .NET answers a
     * source the Consumer has since replaced with nothing it would act on. */
    readonly generation: number;
    readonly fields: readonly WireField[];
    readonly aggregations: readonly PivotAggregation[];
    readonly canRefresh: boolean;
    readonly countsItems: boolean;
    readonly answersLevels: boolean;
}

let calls = 0;

// What the source and its fetchers share: the transport's listener, and a Refresh .NET has not yet answered, which
// settles with why it failed, or with nothing.
interface Link {
    notify: ((notice: PivotServerNotice) => void) | undefined;
    refreshing: Promise<Error | undefined> | undefined;
}

/** The C# source, asked through .NET, with what .NET hears it say passed on by {@link DotNetPivotSource.hear}. */
export class DotNetPivotSource extends FetchingPivotSource {
    readonly generation: number;
    readonly #dotnet: DotNetHandle;
    readonly #link: Link;

    constructor(dotnet: DotNetHandle, wire: WireSource) {
        const link: Link = { notify: undefined, refreshing: undefined };
        const { generation } = wire;
        // A question waits for a Refresh asked before it, so it is answered from the data the Refresh brought; one that
        // failed fails the questions that waited for it, and those after them are asked as they come.
        const ask = async (method: string, json: string | null, signal: AbortSignal | undefined): Promise<string> => {
            const refreshing = link.refreshing;
            if (refreshing !== undefined) {
                const failure = await refreshing;
                if (link.refreshing === refreshing) link.refreshing = undefined;
                if (failure !== undefined) throw failure;
            }
            signal?.throwIfAborted();
            const call = ++calls;
            const cancel = (): void => { void dotnet.invokeMethodAsync('Cancel', call); };
            signal?.addEventListener('abort', cancel, { once: true });
            try {
                const answer = await dotnet.invokeMethodAsync(method, generation, call, json);
                if (typeof answer !== 'string') throw new TypeError(`Internal: .NET answered ${method} with something other than JSON text.`);
                return answer;
            } finally {
                signal?.removeEventListener('abort', cancel);
            }
        };
        const features = new PivotSourceFeatures(wire.aggregations, wire.canRefresh, wire.countsItems, wire.answersLevels);
        super(wire.fields.map(fieldOf), features, {
            aggregate: async (query, signal) => PivotJson.readAnswer(await ask('Aggregate', PivotJson.writeQuery(query), signal)),
            items: async (query, signal) => PivotJson.readItemPage(await ask('Items', PivotJson.writeItemsQuery(query), signal)),
            details: async (query, signal) => PivotJson.readDetailPage(await ask('Details', PivotJson.writeDetailsQuery(query), signal)),
            ...(wire.countsItems ? { itemCounts: async (signal?: AbortSignal) => PivotJson.readItemCounts(await ask('ItemCounts', null, signal)) } : {}),
            subscribe: notify => {
                link.notify = notify;
                return () => { link.notify = undefined; };
            },
        });
        this.generation = generation;
        this.#dotnet = dotnet;
        this.#link = link;
    }

    /** Takes what .NET heard its source say: a `PivotSourceChanged` in PivotJson. Heard while nothing listens, it
     * is nobody's: a listener that subscribes later asks afresh. */
    hear(json: string): void {
        const changed = PivotJson.readSourceChanged(json);
        this.#link.notify?.(changed.sourceVersion === undefined ? { kind: 'changed' } : { kind: 'changed', sourceVersion: changed.sourceVersion });
    }

    /** Excel's Refresh: lets go of the answer held, and asks .NET to refresh its source, which says it changed once
     * it has. The next question waits for it. */
    override refresh(): void {
        super.refresh();
        if (!this.features.canRefresh) return;
        this.#link.refreshing = failureOf(this.#dotnet.invokeMethodAsync('Refresh', this.generation));
    }
}

// Why `promise` failed, or nothing once it is kept: a failed Refresh is thrown to the questions that wait for it, and
// is never a rejection nobody heard.
async function failureOf(promise: Promise<unknown>): Promise<Error | undefined> {
    try {
        await promise;
        return undefined;
    } catch (error) { // oxlint-disable-line ex-grid/no-catch -- kept, and thrown to each question that waits for the Refresh
        return error instanceof Error ? error : new Error(`.NET failed the Refresh: ${String(error)}`);
    }
}

// Every setting .NET declared, a date part's included, checked as the engine checks a field of the page's own.
function fieldOf(wire: WireField): PivotField {
    return pivotField({
        name: wire.name,
        type: wire.type,
        caption: wire.caption,
        ...(wire.format === null ? {} : { format: wire.format }),
        ...(wire.itemOrder.length === 0 ? {} : { itemOrder: wire.itemOrder }),
        ...(wire.column === null ? {} : { column: wire.column }),
        ...(wire.datePart === null ? {} : { datePart: wire.datePart }),
    });
}
