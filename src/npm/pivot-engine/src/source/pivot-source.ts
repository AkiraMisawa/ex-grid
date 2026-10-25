// A Pivot Source (ADR-0066): what answers a report's questions — the Leaf Aggregates of a question, a page
// of a field's Items, a page of the records behind a cell — under a Source Version, or refuses by name.
// The bundled sources answer over a Snapshot, or over records in memory read into one; a Consumer's own
// source fetches its answers from a server. Every source refuses the same things the same way: a field it
// does not have, and a part only Aggregations it does not offer read. The C# engine's PivotSource,
// SnapshotPivotSource, RecordPivotSource, FetchingPivotSource and their pages.

import type { Snapshot, SnapshotDecimal } from '@ex-grid/data';
import { partsOf } from '../aggregates/parts';
import { PivotAnswer, PivotRefusals, type PivotSourceRefusal } from '../cube/answer';
import { partBits, placedFields, type PivotDetailsQuery, type PivotItemsQuery, type PivotQuery } from '../cube/query';
import { declaredFields, type PivotField, type PivotRecordField } from '../fields/pivot-field';
import { compareInvariant, fromPublicKey, keyIdOf, toPublicKey, type ItemKey, type PivotItemKey } from '../items/item-key';
import { readRecordValue } from '../items/record-value';
import { pivotAggregations, type PivotAggregation } from '../layout/pivot-enums';
import { defined, elementAt, unreachable } from '../must';
import { upperInvariant } from '../text/casing';
import { AggregationPass, itemsOfField } from './aggregation-pass';
import { bindSnapshot, detailAt, itemKeyAt, recordSnapshot, type FieldBinding } from './binding';

// ---- What a source offers -----------------------------------------------------------------------

/** The Aggregations a source answers, and whether it can be refreshed. */
export class PivotSourceFeatures {
    /** Every Aggregation, in the declared order. */
    static readonly all: PivotSourceFeatures = new PivotSourceFeatures(pivotAggregations);

    /** The Aggregations offered, in the declared order. */
    readonly aggregations: readonly PivotAggregation[];
    /** Whether Refresh asks the source again. */
    readonly canRefresh: boolean;

    /** The features of the Aggregations given, refusing one the engine does not know. */
    constructor(aggregations: Iterable<PivotAggregation>, canRefresh = false) {
        const offered = new Set<PivotAggregation>();
        for (const aggregation of aggregations) {
            if (!pivotAggregations.includes(aggregation)) throw new RangeError(`Unknown PivotAggregation (${aggregation}).`);
            offered.add(aggregation);
        }
        this.aggregations = Object.freeze(pivotAggregations.filter(a => offered.has(a)));
        this.canRefresh = canRefresh;
        Object.freeze(this);
    }

    /** Whether the source answers an Aggregation. */
    offers(aggregation: PivotAggregation): boolean {
        return this.aggregations.includes(aggregation);
    }

    /** The Aggregations that read a part, when the source offers none of them; empty otherwise. */
    missingFor(part: number): readonly PivotAggregation[] {
        const readers = pivotAggregations.filter(a => partsOf(a) === part);
        return readers.some(a => this.offers(a)) ? [] : readers;
    }
}

// ---- The pages ----------------------------------------------------------------------------------

function refusedError(what: string, refusal: PivotSourceRefusal): Error {
    return new Error(`The source refused to ${what}: ${refusal.message}`);
}

/** A page of a field's Items (ADR-0066), under the Source Version they were listed at, or a refusal. */
export class PivotItemPage {
    /** The refusal, when the source refused. */
    readonly refusal: PivotSourceRefusal | undefined;
    readonly #sourceVersion: string | undefined;
    readonly #items: readonly PivotItemKey[];
    readonly #total: number;

    private constructor(sourceVersion: string | undefined, items: readonly PivotItemKey[], total: number, refusal: PivotSourceRefusal | undefined) {
        this.#sourceVersion = sourceVersion;
        this.#items = items;
        this.#total = total;
        this.refusal = refusal;
        Object.freeze(this);
    }

    /** A page: the Items listed, and how many match in all. */
    static of(sourceVersion: string, items: readonly PivotItemKey[], total: number): PivotItemPage {
        if (typeof sourceVersion !== 'string') throw new TypeError('A page names its Source Version.');
        if (!Number.isSafeInteger(total) || total < items.length) {
            throw new RangeError(`A page of ${items.length} Items has at least that many in total, not ${total}.`);
        }
        return new PivotItemPage(sourceVersion, Object.freeze([...items]), total, undefined);
    }

    /** The source's refusal to list the Items. */
    static refused(refusal: PivotSourceRefusal): PivotItemPage {
        return new PivotItemPage(undefined, [], 0, refusal);
    }

    /** Whether the source refused. */
    get isRefused(): boolean {
        return this.refusal !== undefined;
    }

    /** The Source Version the Items were listed at. */
    get sourceVersion(): string {
        if (this.refusal !== undefined) throw refusedError('list Items', this.refusal);
        return defined(this.#sourceVersion, 'a page that was not refused has a Source Version');
    }

    /** The Items listed. */
    get items(): readonly PivotItemKey[] {
        if (this.refusal !== undefined) throw refusedError('list Items', this.refusal);
        return this.#items;
    }

    /** How many Items match in all, the listed ones included. */
    get total(): number {
        if (this.refusal !== undefined) throw refusedError('list Items', this.refusal);
        return this.#total;
    }
}

/** A value a record behind a cell carries: text, an exact number, a double, a date by its clock ticks, a
 * Boolean, or `null` for a Blank. A date part is the part's number. */
export type PivotDetailValue = string | SnapshotDecimal | number | bigint | boolean | null;

/** One record behind a cell: each field's value, in the page's field order, and the Consumer's record where
 * the source holds one. */
export interface PivotDetailRecord {
    /** Each field's value, in the page's field order. */
    readonly values: readonly PivotDetailValue[];
    /** The Consumer's record, where the source holds one. */
    readonly record?: unknown;
}

/** A page of the records behind a cell (ADR-0063/0066), or a refusal. */
export class PivotDetailPage {
    /** The refusal, when the source refused. */
    readonly refusal: PivotSourceRefusal | undefined;
    readonly #sourceVersion: string | undefined;
    readonly #fields: readonly PivotField[];
    readonly #start: number;
    readonly #total: number;
    readonly #records: readonly PivotDetailRecord[];

    private constructor(sourceVersion: string | undefined, fields: readonly PivotField[], start: number, total: number,
        records: readonly PivotDetailRecord[], refusal: PivotSourceRefusal | undefined) {
        this.#sourceVersion = sourceVersion;
        this.#fields = fields;
        this.#start = start;
        this.#total = total;
        this.#records = records;
        this.refusal = refusal;
        Object.freeze(this);
    }

    /** A page: the fields, where it starts, how many records there are in all, and the records. */
    static of(sourceVersion: string, fields: readonly PivotField[], start: number, total: number, records: readonly PivotDetailRecord[]): PivotDetailPage {
        if (typeof sourceVersion !== 'string') throw new TypeError('A page names its Source Version.');
        if (!Number.isSafeInteger(start) || start < 0) throw new RangeError(`A page starts at a record from 0, not ${start}.`);
        if (!Number.isSafeInteger(total) || total < 0) throw new RangeError(`A page counts at least no record, not ${total}.`);
        if (records.length > 0 && start + records.length > total) {
            throw new RangeError(`A page of ${records.length} records from ${start} needs at least ${start + records.length} in total.`);
        }
        const copied = records.map(record => {
            if (record.values.length !== fields.length) {
                throw new TypeError(`A record carries ${record.values.length} values, and the page has ${fields.length} fields.`);
            }
            const values = Object.freeze([...record.values]);
            return Object.freeze(record.record === undefined ? { values } : { values, record: record.record });
        });
        return new PivotDetailPage(sourceVersion, Object.freeze([...fields]), start, total, Object.freeze(copied), undefined);
    }

    /** The source's refusal to show the records. */
    static refused(refusal: PivotSourceRefusal): PivotDetailPage {
        return new PivotDetailPage(undefined, [], 0, 0, [], refusal);
    }

    /** Whether the source refused. */
    get isRefused(): boolean {
        return this.refusal !== undefined;
    }

    /** The Source Version the records were read at. */
    get sourceVersion(): string {
        if (this.refusal !== undefined) throw refusedError('show the records', this.refusal);
        return defined(this.#sourceVersion, 'a page that was not refused has a Source Version');
    }

    /** The fields each record carries a value of, in order. */
    get fields(): readonly PivotField[] {
        if (this.refusal !== undefined) throw refusedError('show the records', this.refusal);
        return this.#fields;
    }

    /** The place of the page's first record among all of them. */
    get start(): number {
        if (this.refusal !== undefined) throw refusedError('show the records', this.refusal);
        return this.#start;
    }

    /** How many records are behind the cell in all. */
    get total(): number {
        if (this.refusal !== undefined) throw refusedError('show the records', this.refusal);
        return this.#total;
    }

    /** The page's records. */
    get records(): readonly PivotDetailRecord[] {
        if (this.refusal !== undefined) throw refusedError('show the records', this.refusal);
        return this.#records;
    }
}

// ---- The source ---------------------------------------------------------------------------------

/** What a report asks its data (ADR-0066). Each question is answered under a Source Version, or refused. */
export interface PivotSource {
    /** The fields the source has. */
    readonly fields: readonly PivotField[];
    /** The Aggregations it answers. */
    readonly features: PivotSourceFeatures;
    /** The Leaf Aggregates of a question. */
    aggregate(query: PivotQuery, signal?: AbortSignal): Promise<PivotAnswer>;
    /** A page of a field's Items over all the data. */
    items(query: PivotItemsQuery, signal?: AbortSignal): Promise<PivotItemPage>;
    /** A page of the records behind a cell. */
    details(query: PivotDetailsQuery, signal?: AbortSignal): Promise<PivotDetailPage>;
}

function knows(fields: readonly PivotField[], name: string): boolean {
    return fields.some(field => field.name === name);
}

/** What every source refuses of a question, the same way: a field it does not have, and a part that only
 * Aggregations it does not offer read. */
export function queryRefusal(fields: readonly PivotField[], features: PivotSourceFeatures, query: PivotQuery): PivotSourceRefusal | undefined {
    for (const field of placedFields(query)) {
        if (!knows(fields, field.field)) return PivotRefusals.unknownField(field.field);
    }
    for (const value of query.values) {
        if (!knows(fields, value.field)) return PivotRefusals.unknownField(value.field);
        const parts = partBits(value.parts);
        for (const part of [1, 2, 4, 8]) {
            if ((parts & part) === 0) continue;
            const missing = features.missingFor(part);
            if (missing.length > 0) return PivotRefusals.aggregationNotOffered(value.field, missing);
        }
    }
    return undefined;
}

/** What every source refuses of a question for Items: a field it does not have. */
export function itemsRefusal(fields: readonly PivotField[], query: PivotItemsQuery): PivotSourceRefusal | undefined {
    return knows(fields, query.field) ? undefined : PivotRefusals.unknownField(query.field);
}

/** What every source refuses of a question for records: a field it does not have. */
export function detailsRefusal(fields: readonly PivotField[], query: PivotDetailsQuery): PivotSourceRefusal | undefined {
    for (const field of [...query.rowItems.map(i => i.field), ...query.columnItems.map(i => i.field), ...query.hiddenItems.map(h => h.field)]) {
        if (!knows(fields, field)) return PivotRefusals.unknownField(field);
    }
    return undefined;
}

function aborted(signal: AbortSignal | undefined): void {
    signal?.throwIfAborted();
}

function newIdentity(): string {
    return globalThis.crypto.randomUUID().replaceAll('-', '');
}

// A page of a field's Items: those whose text holds the search ignoring case, in the invariant order, the
// first `max` of them.
function itemPage(keys: readonly ItemKey[], query: PivotItemsQuery, sourceVersion: string): PivotItemPage {
    const search = query.search === undefined ? undefined : upperInvariant(query.search);
    const matches = search === undefined ? keys : keys.filter(key => {
        const value = toPublicKey(key).value;
        return value !== undefined && upperInvariant(value).includes(search);
    });
    const sorted = matches.toSorted(compareInvariant);
    return PivotItemPage.of(sourceVersion, sorted.slice(0, query.max).map(toPublicKey), sorted.length);
}

// The rows behind a cell, in the Snapshot's order: those carrying every Item of the paths and no Hidden
// Item; the page's positions, and how many there are in all.
function rowsBehind(snapshot: Snapshot, bindings: ReadonlyMap<string, FieldBinding>, query: PivotDetailsQuery): { readonly rows: readonly number[]; readonly total: number } {
    const bound = (field: string): FieldBinding => defined(bindings.get(field), `the field '${field}' is bound`);
    const conditions = [...query.rowItems, ...query.columnItems].map(step => ({ binding: bound(step.field), id: keyIdOf(fromPublicKey(step.item)) }));
    const hidden = query.hiddenItems.filter(h => h.hiddenItems.length > 0)
        .map(h => ({ binding: bound(h.field), ids: new Set(h.hiddenItems.map(key => keyIdOf(fromPublicKey(key)))) }));
    const rows: number[] = [];
    let total = 0;
    for (let row = 0; row < snapshot.rowCount; row++) {
        if (!conditions.every(c => keyIdOf(itemKeyAt(c.binding, row)) === c.id)) continue;
        if (hidden.some(h => h.ids.has(keyIdOf(itemKeyAt(h.binding, row))))) continue;
        if (total >= query.start && total - query.start < query.count) rows.push(row);
        total++;
    }
    return { rows, total };
}

/** The bundled source over a Snapshot (ADR-0066): it answers every Aggregation under the version
 * `{identity}:{snapshot version}`. */
export class SnapshotPivotSource implements PivotSource {
    /** The fields bound to the Snapshot's columns. */
    readonly fields: readonly PivotField[];
    /** Every Aggregation. */
    readonly features: PivotSourceFeatures = PivotSourceFeatures.all;
    /** The Snapshot it answers over. */
    readonly snapshot: Snapshot;
    readonly #bindings: ReadonlyMap<string, FieldBinding>;
    readonly #id = newIdentity();

    /** A source over a Snapshot: `fields` bound to its columns, or one field per column. */
    constructor(snapshot: Snapshot, fields?: readonly PivotField[]) {
        const bound = bindSnapshot(snapshot, fields);
        this.fields = Object.freeze([...bound.fields]);
        this.#bindings = bound.bindings;
        this.snapshot = snapshot;
    }

    /** The version the source answers under now. */
    get sourceVersion(): string {
        return `${this.#id}:${this.snapshot.version}`;
    }

    /** The Leaf Aggregates of a question, or a refusal. */
    aggregate(query: PivotQuery, signal?: AbortSignal): Promise<PivotAnswer> {
        aborted(signal);
        return Promise.resolve(this.aggregateNow(query));
    }

    /** The Leaf Aggregates of a question, on the calling thread. */
    aggregateNow(query: PivotQuery): PivotAnswer {
        const refusal = queryRefusal(this.fields, this.features, query);
        if (refusal !== undefined) return PivotAnswer.refused(refusal);
        const pass = new AggregationPass(this.snapshot, this.#bindings, query);
        pass.step(pass.rowCount);
        return pass.answer(this.sourceVersion);
    }

    /** A page of a field's Items. */
    items(query: PivotItemsQuery, signal?: AbortSignal): Promise<PivotItemPage> {
        aborted(signal);
        const refusal = itemsRefusal(this.fields, query);
        if (refusal !== undefined) return Promise.resolve(PivotItemPage.refused(refusal));
        if (query.sourceVersion !== this.sourceVersion) return Promise.resolve(PivotItemPage.refused(PivotRefusals.sourceVersionNotHeld(query.sourceVersion)));
        const binding = defined(this.#bindings.get(query.field), `the field '${query.field}' is bound`);
        return Promise.resolve(itemPage(itemsOfField(this.snapshot, binding), query, query.sourceVersion));
    }

    /** A page of the records behind a cell. */
    details(query: PivotDetailsQuery, signal?: AbortSignal): Promise<PivotDetailPage> {
        aborted(signal);
        const refusal = detailsRefusal(this.fields, query);
        if (refusal !== undefined) return Promise.resolve(PivotDetailPage.refused(refusal));
        if (query.sourceVersion !== this.sourceVersion) return Promise.resolve(PivotDetailPage.refused(PivotRefusals.sourceVersionNotHeld(query.sourceVersion)));
        const { rows, total } = rowsBehind(this.snapshot, this.#bindings, query);
        const bindings = this.fields.map(field => defined(this.#bindings.get(field.name), `the field '${field.name}' is bound`));
        const records = rows.map(row => ({ values: bindings.map(binding => detailAt(binding, row)) }));
        return Promise.resolve(PivotDetailPage.of(query.sourceVersion, this.fields, query.start, total, records));
    }
}

// A value as a record behind a cell carries it.
function detailOfValue(raw: unknown): PivotDetailValue {
    const value = readRecordValue(raw);
    switch (value.kind) {
        case 'blank': return null;
        case 'text': return value.text;
        case 'decimal': return value.decimal;
        case 'double': return value.number;
        case 'date': return value.ticks;
        case 'boolean': return value.value;
        default: return unreachable(value);
    }
}

let allItemsOfRecords: <TRecord>(source: RecordPivotSource<TRecord>, field: string) => readonly ItemKey[];

/** Every Item of a field over all of a record source's records, in the order they were met. */
export function allItemsOf<TRecord>(source: RecordPivotSource<TRecord>, field: string): readonly ItemKey[] {
    return allItemsOfRecords(source, field);
}

/** The bundled source over records in memory (ADR-0066): read into a Snapshot on the first question, each
 * field through its accessor; it answers every Aggregation under one version, the records being fixed. */
export class RecordPivotSource<TRecord> implements PivotSource {
    /** The fields, each read through its accessor. */
    readonly fields: readonly PivotRecordField<TRecord>[];
    /** Every Aggregation. */
    readonly features: PivotSourceFeatures = PivotSourceFeatures.all;
    /** The one version the source answers under. */
    readonly sourceVersion = newIdentity();
    /** The records, as given. */
    readonly records: readonly TRecord[];
    #data: { readonly snapshot: Snapshot; readonly bindings: ReadonlyMap<string, FieldBinding> } | undefined;

    /** A source over records, refusing a name declared twice among the fields. */
    constructor(records: readonly TRecord[], fields: readonly PivotRecordField<TRecord>[]) {
        declaredFields(fields);
        this.records = records;
        this.fields = Object.freeze([...fields]);
    }

    // The Snapshot, read once; an accessor that throws fails every question, naming the row and the field.
    #read(): { readonly snapshot: Snapshot; readonly bindings: ReadonlyMap<string, FieldBinding> } {
        this.#data ??= recordSnapshot(this.records, this.fields);
        return this.#data;
    }

    /** The Leaf Aggregates of a question, or a refusal. */
    aggregate(query: PivotQuery, signal?: AbortSignal): Promise<PivotAnswer> {
        aborted(signal);
        return Promise.resolve(this.aggregateNow(query));
    }

    /** The Leaf Aggregates of a question, on the calling thread. */
    aggregateNow(query: PivotQuery): PivotAnswer {
        const refusal = queryRefusal(this.fields, this.features, query);
        if (refusal !== undefined) return PivotAnswer.refused(refusal);
        const { snapshot, bindings } = this.#read();
        const pass = new AggregationPass(snapshot, bindings, query);
        pass.step(pass.rowCount);
        return pass.answer(this.sourceVersion);
    }

    // Every Item of a field over all the records, in the order they were met.
    #allItems(field: string): readonly ItemKey[] {
        const { snapshot, bindings } = this.#read();
        return itemsOfField(snapshot, defined(bindings.get(field), `the field '${field}' is bound`));
    }

    static {
        allItemsOfRecords = (source, field) => source.#allItems(field);
    }

    /** A page of a field's Items. */
    items(query: PivotItemsQuery, signal?: AbortSignal): Promise<PivotItemPage> {
        aborted(signal);
        const refusal = itemsRefusal(this.fields, query);
        if (refusal !== undefined) return Promise.resolve(PivotItemPage.refused(refusal));
        if (query.sourceVersion !== this.sourceVersion) return Promise.resolve(PivotItemPage.refused(PivotRefusals.sourceVersionNotHeld(query.sourceVersion)));
        return Promise.resolve(itemPage(this.#allItems(query.field), query, this.sourceVersion));
    }

    /** A page of the records behind a cell, each carrying its record. */
    details(query: PivotDetailsQuery, signal?: AbortSignal): Promise<PivotDetailPage> {
        aborted(signal);
        const refusal = detailsRefusal(this.fields, query);
        if (refusal !== undefined) return Promise.resolve(PivotDetailPage.refused(refusal));
        if (query.sourceVersion !== this.sourceVersion) return Promise.resolve(PivotDetailPage.refused(PivotRefusals.sourceVersionNotHeld(query.sourceVersion)));
        const { snapshot, bindings } = this.#read();
        const { rows, total } = rowsBehind(snapshot, bindings, query);
        const records = recordsAt(this.records, rows).map(record => ({ values: this.fields.map(field => detailOfValue(field.value(record))), record }));
        return Promise.resolve(PivotDetailPage.of(this.sourceVersion, this.fields, query.start, total, records));
    }

    /** The records behind a cell, in their order, refusing by name a field the source does not have. */
    recordsBehind(query: PivotDetailsQuery): readonly TRecord[] {
        const refusal = detailsRefusal(this.fields, query);
        if (refusal !== undefined) throw new Error(refusal.message);
        const { snapshot, bindings } = this.#read();
        return recordsAt(this.records, rowsBehind(snapshot, bindings, query).rows);
    }
}

// The records at rows given in ascending order: the Snapshot's rows are the records, in order.
function recordsAt<TRecord>(records: readonly TRecord[], rows: readonly number[]): readonly TRecord[] {
    const wanted = new Set(rows);
    return records.filter((_, row) => wanted.has(row));
}

/** How a fetching source asks its server. */
export interface PivotFetchers {
    /** Asks the server for the Leaf Aggregates of a question. */
    readonly aggregate: (query: PivotQuery, signal?: AbortSignal) => Promise<PivotAnswer>;
    /** Asks the server for a page of a field's Items. */
    readonly items: (query: PivotItemsQuery, signal?: AbortSignal) => Promise<PivotItemPage>;
    /** Asks the server for a page of the records behind a cell. */
    readonly details: (query: PivotDetailsQuery, signal?: AbortSignal) => Promise<PivotDetailPage>;
}

/** A source whose answers a Consumer fetches from a server (ADR-0066). The engine refuses what every source
 * refuses before asking, and holds what comes back to the question: an answer to another question, more
 * Items or records than were asked for, or records of other fields fail by name. */
export class FetchingPivotSource implements PivotSource {
    /** The fields the server has. */
    readonly fields: readonly PivotField[];
    /** The Aggregations the server answers. */
    readonly features: PivotSourceFeatures;
    readonly #fetch: PivotFetchers;

    /** A source of the server's fields and Aggregations, asked through the fetchers. */
    constructor(fields: readonly PivotField[], features: PivotSourceFeatures, fetch: PivotFetchers) {
        declaredFields(fields);
        this.fields = Object.freeze([...fields]);
        this.features = features;
        this.#fetch = fetch;
    }

    /** The server's Leaf Aggregates, held to the question. */
    async aggregate(query: PivotQuery, signal?: AbortSignal): Promise<PivotAnswer> {
        const refusal = queryRefusal(this.fields, this.features, query);
        if (refusal !== undefined) return PivotAnswer.refused(refusal);
        const answer = await this.#fetch.aggregate(query, signal);
        if (!answer.isRefused) {
            const mismatch = answer.mismatch(query);
            if (mismatch !== undefined) throw new Error(`The server answered another question than the one asked: ${mismatch} (ADR-0066).`);
        }
        return answer;
    }

    /** The server's page of Items, held to the most asked for. */
    async items(query: PivotItemsQuery, signal?: AbortSignal): Promise<PivotItemPage> {
        const refusal = itemsRefusal(this.fields, query);
        if (refusal !== undefined) return PivotItemPage.refused(refusal);
        const page = await this.#fetch.items(query, signal);
        if (!page.isRefused && page.items.length > query.max) {
            throw new Error(`The server answered ${page.items.length} Items, and ${query.max} were asked for (ADR-0066).`);
        }
        return page;
    }

    /** The server's page of records, held to the fields and the page asked for. */
    async details(query: PivotDetailsQuery, signal?: AbortSignal): Promise<PivotDetailPage> {
        const refusal = detailsRefusal(this.fields, query);
        if (refusal !== undefined) return PivotDetailPage.refused(refusal);
        const page = await this.#fetch.details(query, signal);
        if (page.isRefused) return page;
        const same = page.fields.length === this.fields.length
            && page.fields.every((field, i) => field.name === elementAt(this.fields, i).name && field.type === elementAt(this.fields, i).type);
        if (!same) {
            throw new Error(`The server's records carry the fields [${page.fields.map(f => f.name).join(', ')}], `
                + `and the source declares [${this.fields.map(f => f.name).join(', ')}] (ADR-0066).`);
        }
        if (page.records.length > query.count || page.start !== query.start) {
            throw new Error(`The server answered ${page.records.length} records from ${page.start}, and ${query.count} from ${query.start} were asked for (ADR-0066).`);
        }
        return page;
    }
}
