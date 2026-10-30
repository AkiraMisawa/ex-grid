// The Pivot Source a Blazor ExPivot hands its Script Core (ADR-0370 sections 1 and 3): the Consumer's C#
// PivotSource, seen from the browser as a server is, through a FetchingPivotSource whose fetchers ask .NET. Each
// question and each answer crosses as the PivotJson both engines read and write, so neither side reads the
// other's objects. The engine in the browser builds the report and its totals from the Leaf Aggregates .NET
// answers, as it does from a server's.

import {
    FetchingPivotSource, PivotJson, PivotOrderKeyError, PivotSourceFeatures, pivotField, pivotItemValue,
    type PivotAggregation, type PivotDatePart, type PivotField, type PivotFieldType, type PivotItemKey, type PivotOrderKeyValue,
    type PivotServerNotice,
} from '@ex-grid/pivot-engine';

/** What the bridge asks of the .NET object a Blazor ExPivot hands it: Blazor's `DotNetObjectReference`. */
export interface DotNetHandle {
    invokeMethodAsync(method: string, ...args: readonly unknown[]): Promise<unknown>;
}

/** A field as .NET declares it: its name, type and caption, whether it has an Order Key, the format its Items are
 * labelled with, the values its Items are declared in, and, for a date part, the Date column it is a part of. An
 * Order Key is a .NET function: .NET answers its keys with each answer that names the field's Items (ADR-0412). */
export interface WireField {
    readonly name: string;
    readonly type: PivotFieldType;
    readonly caption: string;
    readonly orderKey: boolean;
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

// What the source and its fetchers share: the transport's listener, a Refresh .NET has not yet answered, which
// settles with why it failed, or with nothing, and the Order Keys .NET has answered.
interface Link {
    notify: ((notice: PivotServerNotice) => void) | undefined;
    refreshing: Promise<Error | undefined> | undefined;
    readonly keys: OrderKeys;
}

/** An Item's Order Key as .NET answers it (ADR-0412): its .NET type and its value as the browser compares it, the
 * function's failure, or a key the browser cannot order as .NET does; `null` for no key. */
type WireOrderKey =
    | null
    | { readonly type: string; readonly text: string }
    | { readonly type: string; readonly boolean: boolean }
    | { readonly type: string; readonly number: string }
    | { readonly type: string; readonly bigint: string }
    | { readonly threw: string }
    | { readonly refused: string };

/** The line .NET answers before an answer that names Items of a field with an Order Key: their keys. */
interface KeysLine {
    readonly orderKeys: readonly { readonly field: string; readonly items: readonly (readonly [PivotItemKey, WireOrderKey])[] }[];
}

type ItemValue = string | number | bigint | boolean;

/** A key as it was answered: under which Source Version. */
interface HeldKey {
    readonly version: string;
    readonly key: WireOrderKey;
}

// The message of a failure the source answered in place of an answer, in its own words.
function failedMessage(text: string): string {
    const failed: unknown = (JSON.parse(text) as { readonly failed?: unknown }).failed; // oxlint-disable-line typescript/no-unsafe-type-assertion -- the line PivotSourceCrossing.Failed writes, whose message is checked here
    if (typeof failed !== 'string') throw new TypeError('Internal: .NET answered a failure with no message.');
    return failed;
}

// An answer, and the line of keys before it where .NET wrote one: neither holds a line break of its own.
function split(text: string): { readonly keys: KeysLine | undefined; readonly answer: string } {
    const at = text.indexOf('\n');
    if (at < 0) return { keys: undefined, answer: text };
    const keys = JSON.parse(text.slice(0, at)) as KeysLine; // oxlint-disable-line typescript/no-unsafe-type-assertion -- the line PivotSourceCrossing writes; each key is read as it is asked
    return { keys, answer: text.slice(at + 1) };
}

// The Order Keys .NET answered, by field and by the value each Item hands an Order Key: the engine asks a field's
// Order Key for the Items of an answer, which came with their keys. A key is held with the Source Version it was
// answered under, and the keys of the two newest versions are kept: the report shown and the one asked after it,
// which the pivot asks only once the one before it is answered. Within one version an Item has one key and a field
// keys of one .NET type, whichever answer brought them, so a key that contradicts another is refused, not chosen.
class OrderKeys {
    readonly #byField = new Map<string, Map<ItemValue, HeldKey>>();
    readonly #types = new Map<string, string>();
    readonly #versions: string[] = [];

    take(line: KeysLine, version: string): void {
        this.#hold(version);
        for (const { field, items } of line.orderKeys) {
            let held = this.#byField.get(field);
            if (held === undefined) this.#byField.set(field, held = new Map<ItemValue, HeldKey>());
            for (const [item, key] of items) {
                const value = pivotItemValue(item);
                if (value === undefined) throw new TypeError(`Internal: .NET answered an Order Key for an Item no key orders, of the kind ${item.kind}.`);
                held.set(value, { version, key: this.#checked(field, item, version, key, held.get(value)) });
            }
        }
    }

    // The field's key for the Item that hands it `value`, as the engine asks it.
    of(field: string, caption: string, value: ItemValue): PivotOrderKeyValue | undefined {
        const held = this.#byField.get(field)?.get(value);
        if (held === undefined) throw new PivotOrderKeyError(field, `Internal: .NET answered no Order Key of ${caption} for '${String(value)}'.`);
        const { key } = held;
        if (key === null) return undefined;
        if ('threw' in key) throw new Error(key.threw);
        if ('refused' in key) throw new PivotOrderKeyError(field, key.refused);
        if ('text' in key) return key.text;
        if ('boolean' in key) return key.boolean;
        if ('number' in key) return Number(key.number);
        return BigInt(key.bigint);
    }

    // Keeps the keys of `version` and of the version before it, and lets go of any older.
    #hold(version: string): void {
        if (this.#versions.includes(version)) return;
        this.#versions.push(version);
        const gone = this.#versions.length > 2 ? this.#versions.shift() : undefined;
        if (gone === undefined) return;
        for (const held of this.#byField.values()) {
            for (const [value, { version: at }] of held) if (at === gone) held.delete(value);
        }
        for (const typed of this.#types.keys()) if (typed.endsWith(`\n${gone}`)) this.#types.delete(typed);
    }

    // `key` as it stands beside the keys already answered under `version`: refused where it contradicts them.
    #checked(field: string, item: PivotItemKey, version: string, key: WireOrderKey, before: HeldKey | undefined): WireOrderKey {
        const named = item.value ?? item.kind;
        if (before !== undefined && before.version === version && JSON.stringify(before.key) !== JSON.stringify(key)) {
            return { refused: `The Order Key of ${field} gave '${named}' two keys under one version of the data; an Order Key gives an Item one key.` };
        }
        if (key === null || !('type' in key)) return key;
        const typed = `${field}\n${version}`;
        const first = this.#types.get(typed);
        if (first === undefined) {
            this.#types.set(typed, key.type);
            return key;
        }
        if (first === key.type) return key;
        return { refused: `The Order Key of ${field} gave '${named}' a key of type ${key.type} after keys of type ${first}; a field's keys are of one type.` };
    }
}

/** The C# source, asked through .NET, with what .NET hears it say passed on by {@link DotNetPivotSource.hear}. */
export class DotNetPivotSource extends FetchingPivotSource {
    readonly generation: number;
    readonly #dotnet: DotNetHandle;
    readonly #link: Link;

    constructor(dotnet: DotNetHandle, wire: WireSource) {
        const link: Link = { notify: undefined, refreshing: undefined, keys: new OrderKeys() };
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
                // The source failed, in its own words (PivotSourceCrossing.Failed): the question fails with them.
                if (answer.startsWith('{"failed":')) throw new Error(failedMessage(answer));
                return answer;
            } finally {
                signal?.removeEventListener('abort', cancel);
            }
        };
        const features = new PivotSourceFeatures(wire.aggregations, wire.canRefresh, wire.countsItems, wire.answersLevels);
        super(wire.fields.map(field => fieldOf(field, link.keys)), features, {
            // An answer that names Items of a field with an Order Key comes after their keys, kept, under its Source
            // Version, before the engine reads the answer and asks them.
            aggregate: async (query, signal) => {
                const { keys, answer } = split(await ask('Aggregate', PivotJson.writeQuery(query), signal));
                const read = PivotJson.readAnswer(answer);
                if (keys !== undefined) link.keys.take(keys, read.sourceVersion);
                return read;
            },
            items: async (query, signal) => {
                const { keys, answer } = split(await ask('Items', PivotJson.writeItemsQuery(query), signal));
                const read = PivotJson.readItemPage(answer);
                if (keys !== undefined) link.keys.take(keys, read.sourceVersion);
                return read;
            },
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

    /** Takes what .NET heard its source say: a `PivotSourceChanged`, or a bundled source's Change Batch, which the
     * source folds into the answer it holds (ADR-0370 section 3's note of 2026-10-30), in PivotJson. Heard while
     * nothing listens, it is nobody's: a listener that subscribes later asks afresh. */
    hear(json: string): void {
        const notice = PivotJson.readServerNotice(json, this.fields);
        this.#link.notify?.(notice);
    }

    /** Excel's Refresh: lets go of the answer held, and asks .NET to refresh its source. The next question waits for
     * it, and answers the change the Refresh made: .NET does not pass on the notice its source says meanwhile. */
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

// Every setting .NET declared, a date part's included, checked as the engine checks a field of the page's own. An
// Order Key reads the keys .NET answered.
function fieldOf(wire: WireField, keys: OrderKeys): PivotField {
    return pivotField({
        name: wire.name,
        type: wire.type,
        caption: wire.caption,
        ...(wire.orderKey ? { orderKey: (value: string | number | bigint | boolean) => keys.of(wire.name, wire.caption, value) } : {}),
        ...(wire.format === null ? {} : { format: wire.format }),
        ...(wire.itemOrder.length === 0 ? {} : { itemOrder: wire.itemOrder }),
        ...(wire.column === null ? {} : { column: wire.column }),
        ...(wire.datePart === null ? {} : { datePart: wire.datePart }),
    });
}
