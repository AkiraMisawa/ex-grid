// A Pivot Source (ADR-0066): what answers a report's questions — the Leaf Aggregates of a question, a page
// of a field's Items, a page of the records behind a cell — under a Source Version, or refuses by name.
// The bundled sources answer over a Snapshot, or over records in memory read into one; a Consumer's own
// source fetches its answers from a server. Every source refuses the same things the same way: a field it
// does not have, and a part only Aggregations it does not offer read. The C# engine's PivotSource,
// SnapshotPivotSource, RecordPivotSource, FetchingPivotSource and their pages.

import { applyChangeBatch, type ChangeBatch, type Snapshot, type SnapshotChange, type SnapshotDecimal } from '@ex-grid/data';
import { partsOf } from '../aggregates/parts';
import { PivotAnswer, PivotRefusals, type PivotSourceRefusal } from '../cube/answer';
import { partBits, placedFields, queriesEqual, type PivotDetailsQuery, type PivotItemsQuery, type PivotQuery } from '../cube/query';
import { declaredFields, type PivotField, type PivotRecordField } from '../fields/pivot-field';
import { compareInvariant, fromPublicKey, keyIdOf, toPublicKey, type ItemKey, type PivotItemKey } from '../items/item-key';
import { readRecordValue } from '../items/record-value';
import { pivotAggregations, type PivotAggregation } from '../layout/pivot-enums';
import { defined, elementAt, unreachable } from '../must';
import { foldedText } from '../text/casing';
import { AggregationPass, chunkRows, itemsOfField, itemsOfFieldSteps } from './aggregation-pass';
import { Slicer, checkedSlicing, runWhole, type CheckedSlicing, type PivotSlicing } from './slicing';
import { levelAnswerSteps } from '../levels/level-answers';
import { bindSnapshot, detailAt, itemKeyAt, recordSnapshot, type FieldBinding } from './binding';
import { checkBatchRecords, foldServerBatches, type ServerBatch } from './server-batches';

// ---- What a source offers -----------------------------------------------------------------------

/** The Aggregations a source answers, whether it can be refreshed, and whether it counts its fields' Items. */
export class PivotSourceFeatures {
    /** Every Aggregation, in the declared order. */
    static readonly all: PivotSourceFeatures = new PivotSourceFeatures(pivotAggregations);

    /** The Aggregations offered, in the declared order. */
    readonly aggregations: readonly PivotAggregation[];
    /** Whether Refresh asks the source again. */
    readonly canRefresh: boolean;
    /** Whether the source answers each field's count of Items ({@link PivotSource.itemCounts}, ADR-0370
     * section 3): the report then asks it once when the source is opened and again under a new Source
     * Version. */
    readonly countsItems: boolean;
    /** Whether the source answers a question carrying levels whose leaves pass its cap with the cells of the
     * groups shown ({@link PivotAnswer.isLevels}, ADR-0370 section 3): a report source over it then asks with the
     * groups its layout shows, and again when an expand shows groups the answer did not hold. */
    readonly answersLevels: boolean;

    /** The features of the Aggregations given, refusing one the engine does not know. */
    constructor(aggregations: Iterable<PivotAggregation>, canRefresh = false, countsItems = false, answersLevels = false) {
        const offered = new Set<PivotAggregation>();
        for (const aggregation of aggregations) {
            if (!pivotAggregations.includes(aggregation)) throw new RangeError(`Unknown PivotAggregation (${aggregation}).`);
            offered.add(aggregation);
        }
        this.aggregations = Object.freeze(pivotAggregations.filter(a => offered.has(a)));
        this.canRefresh = canRefresh;
        this.countsItems = countsItems;
        this.answersLevels = answersLevels;
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
        if (!Number.isSafeInteger(total)) throw new TypeError(`A page counts its Items in a whole number, not ${total}.`);
        if (total < items.length) throw new RangeError(`A page of ${items.length} Items has at least that many in total.`);
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

/**
 * Each field's count of Items, under the Source Version it was counted at (ADR-0370 section 3): what a
 * server's source answers when its features declare {@link PivotSourceFeatures.countsItems}, so that the
 * Field List shows the count beside a field and a layout's leaves are bounded before it is asked
 * ({@link pivotLeafBound}). A field the source did not count is absent, never zero.
 */
export class PivotItemCounts {
    /** The Source Version the Items were counted at. */
    readonly sourceVersion: string;
    /** Each counted field's count of Items, by the field's name. */
    readonly counts: ReadonlyMap<string, number>;

    /** The counts of a Source Version, refusing a count that is not a whole number of at least 0. */
    constructor(sourceVersion: string, counts: Iterable<readonly [string, number]>) {
        if (typeof sourceVersion !== 'string') throw new TypeError('A Source Version is text.');
        const read = new Map<string, number>();
        for (const [field, count] of counts) {
            if (typeof field !== 'string' || field.length === 0) throw new TypeError('A counted field is named.');
            if (!Number.isSafeInteger(count)) throw new TypeError(`A field has a whole number of Items; '${field}' was counted ${count}.`);
            if (count < 0) throw new RangeError(`A field has no fewer than 0 Items; '${field}' was counted ${count}.`);
            if (read.has(field)) throw new TypeError(`The field '${field}' is counted twice.`);
            read.set(field, count);
        }
        this.sourceVersion = sourceVersion;
        this.counts = read;
        Object.freeze(this);
    }

    /** The field's count of Items, or `undefined` when the source did not count it. */
    countOf(field: string): number | undefined {
        return this.counts.get(field);
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
    /** Each field's count of Items now (ADR-0370 section 3), asked of a source whose features declare
     * {@link PivotSourceFeatures.countsItems}. The bundled sources answer it, for a server that answers from
     * one, and do not declare it: in the page, a layout past the cap is refused as soon as a count would be
     * known. */
    itemCounts?(signal?: AbortSignal): Promise<PivotItemCounts>;
    /** Tells `listener` each time the source's data moves on (ADR-0067, ADR-0370 section 3); the function
     * returned stops telling it. A source whose data never moves has none. */
    subscribe?(listener: (changed: PivotSourceChanged) => void): () => void;
    /** Refresh: lets go of whatever the source holds for its questions, so the next one is asked of its data.
     * A source that holds nothing past its data has none; a source that wraps another passes it on. */
    refresh?(): void;
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

// A page of a field's Items: those whose text holds the search under OrdinalIgnoreCase, as C#'s Contains
// finds it (the long s is not an s), in the invariant order, the first `max` of them.
function itemPage(keys: readonly ItemKey[], query: PivotItemsQuery, sourceVersion: string): PivotItemPage {
    const search = query.search === undefined ? undefined : foldedText(query.search);
    const matches = search === undefined ? keys : keys.filter(key => {
        const value = toPublicKey(key).value;
        return value !== undefined && foldedText(value).includes(search);
    });
    const sorted = matches.toSorted(compareInvariant);
    return PivotItemPage.of(sourceVersion, sorted.slice(0, query.max).map(toPublicKey), sorted.length);
}

// The rows behind a cell, in the Snapshot's order: those carrying every Item of the paths and no Hidden
// Item; the page's positions, and how many there are in all. It yields after each chunk of rows.
function* rowsBehindSteps(snapshot: Snapshot, bindings: ReadonlyMap<string, FieldBinding>, query: PivotDetailsQuery): Generator<void, { readonly rows: readonly number[]; readonly total: number }> {
    const bound = (field: string): FieldBinding => defined(bindings.get(field), `the field '${field}' is bound`);
    const conditions = [...query.rowItems, ...query.columnItems].map(step => ({ binding: bound(step.field), id: keyIdOf(fromPublicKey(step.item)) }));
    const hidden = query.hiddenItems.filter(h => h.hiddenItems.length > 0)
        .map(h => ({ binding: bound(h.field), ids: new Set(h.hiddenItems.map(key => keyIdOf(fromPublicKey(key)))) }));
    const rows: number[] = [];
    let total = 0;
    for (let row = 0; row < snapshot.rowCount; row++) {
        if (row % chunkRows === chunkRows - 1) yield;
        if (!conditions.every(c => keyIdOf(itemKeyAt(c.binding, row)) === c.id)) continue;
        if (hidden.some(h => h.ids.has(keyIdOf(itemKeyAt(h.binding, row))))) continue;
        if (total >= query.start && total - query.start < query.count) rows.push(row);
        total++;
    }
    return { rows, total };
}

function rowsBehind(snapshot: Snapshot, bindings: ReadonlyMap<string, FieldBinding>, query: PivotDetailsQuery): { readonly rows: readonly number[]; readonly total: number } {
    return runWhole(rowsBehindSteps(snapshot, bindings, query));
}

/** What a source says when its data moved on (ADR-0067): the Source Version it answers under now. */
export interface PivotSourceChanged {
    /** The Source Version the source answers under from now; absent where the source does not know it yet,
     * as a server's notice may not, and the next answer carries it. A source over a Snapshot always knows it. */
    readonly sourceVersion?: string;
}

// A version a source answers under, and its fields' bindings to that version's columns.
interface Bound {
    readonly snapshot: Snapshot;
    readonly bindings: ReadonlyMap<string, FieldBinding>;
}

// The pass a source holds for the question it answered last, which batches are folded into; and the
// batches applied while an answer was being assembled from it, to fold in once none is.
interface HeldPass {
    readonly pass: AggregationPass;
    assembling: number;
    readonly deferred: { readonly change: SnapshotChange; readonly bindings: ReadonlyMap<string, FieldBinding> }[];
}

let aggregateOn: (source: SnapshotPivotSource, query: PivotQuery, slicer: Slicer) => Promise<PivotAnswer>;

/** A source over a Snapshot asked a question on another's slices: a report source's pass, cube and report
 * share one Slicer, so the work yields only where a slice is spent, as the C# engine's does. */
export function aggregateSliced(source: SnapshotPivotSource, query: PivotQuery, slicer: Slicer): Promise<PivotAnswer> {
    return aggregateOn(source, query, slicer);
}

/** A Snapshot the source answers from, its fields' bindings, and the Source Version it answers under. */
export interface BoundSnapshot {
    readonly snapshot: Snapshot;
    readonly bindings: ReadonlyMap<string, FieldBinding>;
    readonly version: string;
}

/** What a report's computation reads of a Snapshot's source, which keeps its own pass (ADR-0153): not part of
 * the public surface. */
export interface SnapshotSourceLink {
    /** The Snapshot the source answers from now. */
    capture(): BoundSnapshot;
    /** Tells `listener` each Change Batch the source applies, with the next version's bindings, before its
     * subscribers are told; the function returned stops it. */
    watch(listener: (change: SnapshotChange, bindings: ReadonlyMap<string, FieldBinding>) => void): () => void;
    /** Keeps answering Items and records under the version a computation published. */
    remember(snapshot: Snapshot, bindings: ReadonlyMap<string, FieldBinding>): void;
    /** The Source Version of a Snapshot of the source. */
    versionOf(snapshot: Snapshot): string;
}

let linkOf: (source: SnapshotPivotSource) => SnapshotSourceLink;

/** The link a report's computation keeps to a Snapshot's source. */
export function snapshotSourceLink(source: SnapshotPivotSource): SnapshotSourceLink {
    return linkOf(source);
}

/**
 * The bundled source over a Snapshot (ADR-0066): it answers every Aggregation under the version
 * `{identity}:{snapshot version}`, in slices (PV-40).
 *
 * **Live data** (ADR-0067): {@link SnapshotPivotSource.apply} takes a Change Batch. The Snapshot makes the next one, the answer the
 * source holds for its last question is brought up to date from what the batch removed and added rather than
 * read again — the exact parts by subtraction and addition, every other part recomputed for the leaves the
 * batch touched — and each listener {@link SnapshotPivotSource.subscribe}d is told the new Source Version. Asked the same question
 * again, the source answers from what it holds: after any sequence of batches, what a fresh source over the
 * Snapshot they made answers, read in that Snapshot's order, to the last bit. A batch applied while an answer
 * is assembled from what it holds waits until the answer is made, so that no answer is half a batch.
 *
 * It answers a field's Items and a cell's records under its current version and under the versions of its
 * last {@link SnapshotPivotSource.answersHeld} answers — a Snapshot is immutable, so their records still add
 * up — and refuses an older one.
 */
export class SnapshotPivotSource implements PivotSource {
    /** How many of its latest answers' versions the source keeps answering Items and records under, besides
     * its current one. */
    static readonly answersHeld = 4;

    /** The fields bound to the Snapshot's columns. */
    readonly fields: readonly PivotField[];
    /** Every Aggregation. */
    readonly features: PivotSourceFeatures = PivotSourceFeatures.all;
    readonly #declared: readonly PivotField[] | undefined;
    readonly #slicing: CheckedSlicing;
    readonly #id = newIdentity();
    readonly #answered: Bound[] = [];
    readonly #listeners = new Set<(changed: PivotSourceChanged) => void>();
    readonly #watchers = new Set<(change: SnapshotChange, bindings: ReadonlyMap<string, FieldBinding>) => void>();
    #snapshot: Snapshot;
    #bindings: ReadonlyMap<string, FieldBinding>;
    #held: HeldPass | undefined;
    #heldAnswer: PivotAnswer | undefined;

    /** A source over a Snapshot: `fields` bound to its columns, or one field per column; its work paced by
     * `slicing`. */
    constructor(snapshot: Snapshot, fields?: readonly PivotField[], slicing?: PivotSlicing) {
        const bound = bindSnapshot(snapshot, fields);
        this.fields = Object.freeze([...bound.fields]);
        this.#declared = fields;
        this.#bindings = bound.bindings;
        this.#snapshot = snapshot;
        this.#slicing = checkedSlicing(slicing);
    }

    /** The Snapshot the source answers from now: the one it was made with, or the one the last Change Batch
     * made. */
    get snapshot(): Snapshot {
        return this.#snapshot;
    }

    /** The version the source answers under now. */
    get sourceVersion(): string {
        return this.#versionOf(this.#snapshot);
    }

    #versionOf(snapshot: Snapshot): string {
        return `${this.#id}:${snapshot.version}`;
    }

    /** Tells `listener` each new Source Version, after the source has moved on to it; the function returned
     * stops telling it. */
    subscribe(listener: (changed: PivotSourceChanged) => void): () => void {
        if (typeof listener !== 'function') throw new TypeError('A listener is a function.');
        const held = (changed: PivotSourceChanged): void => {
            listener(changed);
        };
        this.#listeners.add(held);
        return () => {
            this.#listeners.delete(held);
        };
    }

    /**
     * Applies a Change Batch (ADR-0067): the Snapshot makes the next one, whole or not at all; the answer held
     * for the last question is brought up to date from what the batch removed and added; and each listener is
     * told the new Source Version, after the source has moved on. A batch the Snapshot refuses throws its
     * `SnapshotError`, and nothing of it is applied. An error a listener throws does not stop the others being
     * told, nor make the batch look refused: it is thrown again from a microtask, as an event listener's is.
     */
    apply(batch: ChangeBatch): SnapshotChange {
        const change = applyChangeBatch(this.#snapshot, batch);
        const bindings = bindSnapshot(change.after, this.#declared).bindings;
        this.#snapshot = change.after;
        this.#bindings = bindings;
        const held = this.#held;
        if (held !== undefined) {
            // An answer being assembled from the pass reads it between slices: the batch waits until it is
            // made, so that the answer is the version before the batch, whole.
            if (held.assembling > 0) held.deferred.push({ change, bindings });
            else if (!foldDeferred(held) || !foldHeld(held, change, bindings)) this.#held = undefined;
        }
        this.#heldAnswer = undefined;
        for (const watcher of Array.from(this.#watchers)) watcher(change, bindings);
        const changed = Object.freeze({ sourceVersion: this.#versionOf(change.after) });
        // A copy, so that a listener subscribed while the others are told is told from the next batch.
        for (const listener of Array.from(this.#listeners)) {
            // A listener that applied a batch itself has told every listener of the newer version.
            if (this.#snapshot !== change.after) break;
            try {
                listener(changed);
            } catch (error) { // oxlint-disable-line ex-grid/no-catch -- the batch is applied whatever a listener does; its error is reported, as an event listener's is, not swallowed
                queueMicrotask(() => {
                    throw error;
                });
            }
        }
        return change;
    }

    /**
     * The Leaf Aggregates of a question, or a refusal, read in slices — or, for the question it answered last,
     * from the pass it holds and has folded every batch into since. Past the cap, a question carrying levels
     * is answered with the cells of the groups shown, at most that many (ADR-0370 section 3), though the source
     * does not declare it, for a server that answers from it. The answer is assembled in slices too, and a
     * cancelled question rejects with the signal's reason at the next slice.
     */
    async aggregate(query: PivotQuery, signal?: AbortSignal): Promise<PivotAnswer> {
        aborted(signal);
        const refusal = queryRefusal(this.fields, this.features, query);
        if (refusal !== undefined) return PivotAnswer.refused(refusal);
        return this.#aggregate(query, new Slicer(this.#slicing, signal));
    }

    static {
        linkOf = (source): SnapshotSourceLink => {
            const link: SnapshotSourceLink = {
                capture: () => Object.freeze({ snapshot: source.#snapshot, bindings: source.#bindings, version: source.#versionOf(source.#snapshot) }),
                watch: listener => {
                    source.#watchers.add(listener);
                    return () => {
                        source.#watchers.delete(listener);
                    };
                },
                remember: (snapshot, bindings) => {
                    source.#remember(snapshot, bindings);
                },
                versionOf: snapshot => source.#versionOf(snapshot),
            };
            return Object.freeze(link);
        };
        aggregateOn = (source, query, slicer) => {
            const refusal = queryRefusal(source.fields, source.features, query);
            return refusal === undefined ? source.#aggregate(query, slicer) : Promise.resolve(PivotAnswer.refused(refusal));
        };
    }

    async #aggregate(query: PivotQuery, slicer: Slicer): Promise<PivotAnswer> {
        const snapshot = this.#snapshot;
        const bindings = this.#bindings;
        if (!this.#heldUpToDate()) this.#held = undefined;
        const held = this.#held;
        if (held !== undefined && queriesEqual(held.pass.query, query) && held.pass.snapshot === snapshot) {
            this.#remember(snapshot, bindings);
            if (this.#heldAnswer !== undefined) return this.#heldAnswer;
            return this.#assembleHeld(held, snapshot, slicer);
        }
        const pass = new AggregationPass(snapshot, bindings, query, snapshot.recordKey !== undefined);
        for (let at = 0; at < pass.rowCount;) {
            const to = Math.min(pass.rowCount, at + chunkRows);
            if (!pass.step(to)) break;
            at = to;
            await slicer.step();
        }
        let answer = await slicer.run(pass.answerSteps(this.#versionOf(snapshot)));
        // Past the cap, a question with levels is answered with the cells it shows (ADR-0370 section 3).
        if (pass.refusal?.kind === 'tooManyLeaves' && query.levels !== undefined) {
            answer = await slicer.run(levelAnswerSteps(snapshot, bindings, query, this.#versionOf(snapshot)));
        }
        if (pass.refusal === undefined && this.#snapshot === snapshot) {
            this.#held = { pass, assembling: 0, deferred: [] };
            this.#heldAnswer = answer;
        }
        if (!answer.isRefused) this.#remember(snapshot, bindings);
        return answer;
    }

    // An answer assembled from the pass held, in slices: a batch applied meanwhile waits, and is folded in
    // once no assembly reads the pass.
    async #assembleHeld(held: HeldPass, snapshot: Snapshot, slicer: Slicer): Promise<PivotAnswer> {
        held.assembling++;
        let answer: PivotAnswer;
        try {
            answer = await slicer.run(held.pass.answerSteps(this.#versionOf(snapshot)));
        } finally {
            held.assembling--;
        }
        if (this.#held === held && this.#snapshot === snapshot) this.#heldAnswer = answer;
        // The batches applied meanwhile are folded in by the next batch or question (#heldUpToDate).
        return answer;
    }

    // Folds in the batches the held pass deferred, once no answer is being assembled from it; false when one
    // could not be folded, and the pass is to be dropped.
    #heldUpToDate(): boolean {
        const held = this.#held;
        return held === undefined || held.assembling > 0 || foldDeferred(held);
    }

    // The Snapshot of a version the source still answers under, with its fields' bindings: the current one, or
    // one of its last answers'.
    #heldVersion(version: string): Bound | undefined {
        if (this.#versionOf(this.#snapshot) === version) return { snapshot: this.#snapshot, bindings: this.#bindings };
        return this.#answered.find(bound => this.#versionOf(bound.snapshot) === version);
    }

    #remember(snapshot: Snapshot, bindings: ReadonlyMap<string, FieldBinding>): void {
        const at = this.#answered.findIndex(bound => bound.snapshot === snapshot);
        if (at >= 0) this.#answered.splice(at, 1);
        this.#answered.push({ snapshot, bindings });
        if (this.#answered.length > SnapshotPivotSource.answersHeld) this.#answered.shift();
    }

    /** A page of a field's Items over all the data of the version asked, read in slices; refused under a
     * version the source no longer holds. */
    async items(query: PivotItemsQuery, signal?: AbortSignal): Promise<PivotItemPage> {
        aborted(signal);
        const refusal = itemsRefusal(this.fields, query);
        if (refusal !== undefined) return PivotItemPage.refused(refusal);
        const bound = this.#heldVersion(query.sourceVersion);
        if (bound === undefined) return PivotItemPage.refused(PivotRefusals.sourceVersionNotHeld(query.sourceVersion));
        const { snapshot, bindings } = bound;
        const binding = defined(bindings.get(query.field), `the field '${query.field}' is bound`);
        const keys = await new Slicer(this.#slicing, signal).run(itemsOfFieldSteps(snapshot, binding));
        return itemPage(keys, query, query.sourceVersion);
    }

    /** Each field's count of Items in the current Snapshot, read in slices, field after field (ADR-0370
     * section 3): what a server answering from this source answers its Field List with. A count is the
     * length of the field's whole page of Items. */
    async itemCounts(signal?: AbortSignal): Promise<PivotItemCounts> {
        aborted(signal);
        const snapshot = this.#snapshot;
        const bindings = this.#bindings;
        const slicer = new Slicer(this.#slicing, signal);
        const counts: [string, number][] = [];
        for (const field of this.fields) {
            const keys = await slicer.run(itemsOfFieldSteps(snapshot, defined(bindings.get(field.name), `the field '${field.name}' is bound`)));
            counts.push([field.name, keys.length]);
        }
        return new PivotItemCounts(this.#versionOf(snapshot), counts);
    }

    /** A page of the records behind a cell in the version asked, in the Snapshot's order, read in slices;
     * refused under a version the source no longer holds. */
    async details(query: PivotDetailsQuery, signal?: AbortSignal): Promise<PivotDetailPage> {
        aborted(signal);
        const refusal = detailsRefusal(this.fields, query);
        if (refusal !== undefined) return PivotDetailPage.refused(refusal);
        const held = this.#heldVersion(query.sourceVersion);
        if (held === undefined) return PivotDetailPage.refused(PivotRefusals.sourceVersionNotHeld(query.sourceVersion));
        const { snapshot, bindings } = held;
        const { rows, total } = await new Slicer(this.#slicing, signal).run(rowsBehindSteps(snapshot, bindings, query));
        const bound = this.fields.map(field => defined(bindings.get(field.name), `the field '${field.name}' is bound`));
        const records = rows.map(row => ({ values: bound.map(binding => detailAt(binding, row)) }));
        return PivotDetailPage.of(query.sourceVersion, this.fields, query.start, total, records);
    }
}

// Folds in the batches a held pass deferred, in order; false when one could not be folded.
function foldDeferred(held: HeldPass): boolean {
    while (held.deferred.length > 0) {
        const next = defined(held.deferred.shift(), 'a deferred batch');
        if (!foldHeld(held, next.change, next.bindings)) return false;
    }
    return true;
}

// Folds a batch into the source's own pass. No computation reads what it touched, so that is forgotten at once.
function foldHeld(held: HeldPass, change: SnapshotChange, bindings: ReadonlyMap<string, FieldBinding>): boolean {
    if (!held.pass.fold(change, bindings)) return false;
    held.pass.clearChanges();
    return true;
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

    /** The Leaf Aggregates of a question, on the calling thread; past the cap, for a question carrying levels,
     * the cells of the groups shown (ADR-0370 section 3). */
    aggregateNow(query: PivotQuery): PivotAnswer {
        const refusal = queryRefusal(this.fields, this.features, query);
        if (refusal !== undefined) return PivotAnswer.refused(refusal);
        const { snapshot, bindings } = this.#read();
        const pass = new AggregationPass(snapshot, bindings, query);
        pass.step(pass.rowCount);
        const answer = pass.answer(this.sourceVersion);
        // Past the cap, a question with levels is answered with the cells it shows (ADR-0370 section 3).
        if (pass.refusal?.kind === 'tooManyLeaves' && query.levels !== undefined) {
            return runWhole(levelAnswerSteps(snapshot, bindings, query, this.sourceVersion));
        }
        return answer;
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

    /** Each field's count of Items over the records (ADR-0370 section 3). */
    itemCounts(signal?: AbortSignal): Promise<PivotItemCounts> {
        aborted(signal);
        return Promise.resolve(new PivotItemCounts(this.sourceVersion, this.fields.map(field => [field.name, this.#allItems(field.name).length] as const)));
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

/** That a server's data moved on, however the Consumer learned it (ADR-0370 section 3): the question is asked
 * again. */
export interface PivotServerChanged {
    /** Names the notice: the data moved on, and its changes are not told. */
    readonly kind: 'changed';
    /** The Source Version the server answers under now, where the Consumer knows it. */
    readonly sourceVersion?: string;
}

/**
 * A server's Change Batch (ADR-0370 section 3): the records its data lost and the records it gained between two
 * Source Versions, each with its values in the source's field order, as a record behind a cell carries them —
 * a date by its ticks, a date part by its number. A changed record is the old one lost and the new one
 * gained.
 */
export interface PivotServerChangeBatch {
    /** Names the notice: the records the data lost and gained are told. */
    readonly kind: 'changeBatch';
    /** The Source Version the batch starts from. */
    readonly from: string;
    /** The Source Version it makes. */
    readonly to: string;
    /** The records the data lost, each with the values it had. */
    readonly removed: readonly (readonly PivotDetailValue[])[];
    /** The records the data gained. */
    readonly added: readonly (readonly PivotDetailValue[])[];
}

/** What the Consumer's transport tells a fetching source: that the data moved on, or how. */
export type PivotServerNotice = PivotServerChanged | PivotServerChangeBatch;

/** How a fetching source asks its server. */
export interface PivotFetchers {
    /** Asks the server for the Leaf Aggregates of a question. */
    readonly aggregate: (query: PivotQuery, signal?: AbortSignal) => Promise<PivotAnswer>;
    /** Asks the server for a page of a field's Items. */
    readonly items: (query: PivotItemsQuery, signal?: AbortSignal) => Promise<PivotItemPage>;
    /** Asks the server for a page of the records behind a cell. */
    readonly details: (query: PivotDetailsQuery, signal?: AbortSignal) => Promise<PivotDetailPage>;
    /** Asks the server for each field's count of Items, when the features declare that it counts them. */
    readonly itemCounts?: (signal?: AbortSignal) => Promise<PivotItemCounts>;
    /**
     * Starts the Consumer's transport telling the source that the server's data moved on (ADR-0370 section 3):
     * WebSocket, server-sent events, SignalR or polling, the Consumer's own. The source calls it when its first
     * listener subscribes, with the function the transport calls for each notice or Change Batch, and calls the
     * function it returns when its last listener leaves.
     */
    readonly subscribe?: (notify: (notice: PivotServerNotice) => void) => () => void;
}

// The answer a fetching source holds for the question it answered last, and the batches heard since.
interface HeldServerAnswer {
    readonly query: PivotQuery;
    readonly answer: PivotAnswer;
    readonly batches: ServerBatch[];
    // The records the batches carry: past the answer's leaves, asking again is the cheaper.
    records: number;
}

/**
 * A source whose answers a Consumer fetches from a server (ADR-0066). The engine refuses what every source
 * refuses before asking, and holds what comes back to the question: an answer to another question, more
 * Items or records than were asked for, or records of other fields fail by name.
 *
 * **Live data** (ADR-0370 section 3): the Consumer's transport, started by {@link PivotFetchers.subscribe},
 * tells the source that the server's data moved on, or hands it a Change Batch of the records the data lost
 * and gained. Each listener {@link FetchingPivotSource.subscribe}d is told the new Source Version. Asked the
 * question it answered last again, the source answers from the Leaf Aggregates it holds with the batches
 * folded in, where subtraction and addition keep every part they touch exactly; otherwise — a notice, a batch
 * out of chain, a sum that is a double, a product, a variance, an extreme a number left, Level Aggregates — it
 * asks the server again. An answer fetched while a notice or a batch arrived is not held.
 */
export class FetchingPivotSource implements PivotSource {
    /** The fields the server has. */
    readonly fields: readonly PivotField[];
    /** The Aggregations the server answers. */
    readonly features: PivotSourceFeatures;
    readonly #fetch: PivotFetchers;
    readonly #listeners = new Set<(changed: PivotSourceChanged) => void>();
    #stop: (() => void) | undefined;
    #held: HeldServerAnswer | undefined;
    // How many notices and batches the source has heard: an answer fetched across one is not held.
    #heard = 0;

    /** A source of the server's fields and Aggregations, asked through the fetchers; the features declare
     * that it counts Items exactly when a fetcher counts them. */
    constructor(fields: readonly PivotField[], features: PivotSourceFeatures, fetch: PivotFetchers) {
        declaredFields(fields);
        if (features.countsItems !== (fetch.itemCounts !== undefined)) {
            throw new TypeError(features.countsItems
                ? 'The source declares that it counts its fields\' Items, and no fetcher counts them (ADR-0370 section 3).'
                : 'A fetcher counts the fields\' Items, and the source\'s features do not declare it (ADR-0370 section 3).');
        }
        this.fields = Object.freeze([...fields]);
        this.features = features;
        this.#fetch = fetch;
    }

    /** The server's Leaf Aggregates, held to the question; for the question answered last, the answer held
     * with the batches heard since folded in, where they fold. */
    async aggregate(query: PivotQuery, signal?: AbortSignal): Promise<PivotAnswer> {
        const refusal = queryRefusal(this.fields, this.features, query);
        if (refusal !== undefined) return PivotAnswer.refused(refusal);
        const held = this.#held;
        if (held !== undefined && queriesEqual(held.query, query)) {
            if (held.batches.length === 0) return held.answer;
            const folded = foldServerBatches(query, held.answer, held.batches, this.fields);
            this.#held = folded === undefined ? undefined : { query, answer: folded, batches: [], records: 0 };
            if (folded !== undefined) return folded;
        }
        const heard = this.#heard;
        const answer = await this.#fetch.aggregate(query, signal);
        if (!answer.isRefused) {
            const mismatch = answer.mismatch(query);
            if (mismatch !== undefined) throw new Error(`The server answered another question than the one asked: ${mismatch} (ADR-0066).`);
        }
        // Held to fold batches into: an answer of leaves, fetched while a transport runs to say what moves, with
        // nothing heard while it was asked. Without a transport nothing would say the answer went stale.
        this.#held = this.#stop !== undefined && !answer.isRefused && !answer.isLevels && heard === this.#heard
            ? { query, answer, batches: [], records: 0 }
            : undefined;
        return answer;
    }

    /** Tells `listener` each new Source Version the Consumer's transport reports, after the source has moved on
     * to it; the function returned stops telling it. The transport runs while a listener is subscribed. */
    subscribe(listener: (changed: PivotSourceChanged) => void): () => void {
        if (typeof listener !== 'function') throw new TypeError('A listener is a function.');
        const held = (changed: PivotSourceChanged): void => {
            listener(changed);
        };
        const fetch = this.#fetch;
        const starts = this.#listeners.size === 0 && fetch.subscribe !== undefined;
        this.#listeners.add(held);
        if (starts) {
            let stop: (() => void) | undefined;
            try {
                // Called on the fetchers, as the other fetchers are, so a fetcher written as a method keeps its `this`.
                stop = fetch.subscribe(notice => {
                    this.#hear(notice);
                });
            } catch (error) {
                this.#listeners.delete(held);
                throw error;
            }
            if (typeof (stop as unknown) !== 'function') {
                this.#listeners.delete(held);
                throw new TypeError("The fetchers' subscribe returns the function that stops the transport (ADR-0370 section 3).");
            }
            this.#stop = stop;
        }
        return () => {
            if (!this.#listeners.delete(held) || this.#listeners.size > 0) return;
            this.#stopTransport();
        };
    }

    /** Refresh: lets go of the answer held, so the next question is asked of the server. */
    refresh(): void {
        this.#held = undefined;
    }

    // Stops the transport, and lets go of the answer held: nothing would say what moves while it is stopped.
    #stopTransport(): void {
        const stop = this.#stop;
        this.#stop = undefined;
        this.#held = undefined;
        stop?.();
    }

    // A notice or a batch the Consumer's transport handed the source: checked whole before anything moves, so a
    // malformed one throws to the transport and changes nothing.
    #hear(notice: PivotServerNotice): void {
        if (typeof notice !== 'object' || (notice as unknown) === null) throw new TypeError('A notice is an object: changed, or changeBatch (ADR-0370 section 3).');
        let changed: PivotSourceChanged;
        switch (notice.kind) {
            case 'changed': {
                const version = notice.sourceVersion;
                if (version !== undefined && (typeof version !== 'string' || version.length === 0)) throw new TypeError('A notice names its Source Version with text, or not at all.');
                this.#held = undefined;
                changed = Object.freeze(version === undefined ? {} : { sourceVersion: version });
                break;
            }
            case 'changeBatch': {
                const batch = checkedBatch(notice, this.fields);
                const held = this.#held;
                const end = held === undefined ? undefined : (held.batches.at(-1)?.to ?? held.answer.sourceVersion);
                if (held === undefined || end !== batch.from || held.records + batch.removed.length + batch.added.length > held.answer.leafCount + chunkRows) {
                    this.#held = undefined;
                } else {
                    held.batches.push(batch);
                    held.records += batch.removed.length + batch.added.length;
                }
                changed = Object.freeze({ sourceVersion: batch.to });
                break;
            }
            default:
                throw new TypeError(`'${String((notice as { readonly kind: unknown }).kind)}' is no notice a fetching source takes: changed, or changeBatch (ADR-0370 section 3).`);
        }
        this.#heard++;
        // A copy, so that a listener subscribed while the others are told is told from the next notice.
        for (const listener of Array.from(this.#listeners)) {
            try {
                listener(changed);
            } catch (error) { // oxlint-disable-line ex-grid/no-catch -- the notice is taken whatever a listener does; its error is reported, as an event listener's is, not swallowed
                queueMicrotask(() => {
                    throw error;
                });
            }
        }
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

    /** The server's count of each field's Items (ADR-0370 section 3). Counts naming a field the source does
     * not declare are refused by name. */
    async itemCounts(signal?: AbortSignal): Promise<PivotItemCounts> {
        const fetch = this.#fetch.itemCounts;
        if (fetch === undefined) throw new Error('This Pivot Source does not count its fields\' Items (ADR-0370 section 3).');
        const counts = await fetch(signal);
        for (const field of counts.counts.keys()) {
            if (!knows(this.fields, field)) throw new Error(`The server counted the Items of '${field}', a field the source does not declare (ADR-0370 section 3).`);
        }
        return counts;
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

// A Change Batch as the transport handed it, checked: its versions are text, and its records are the source's
// fields' values, as they will be read.
function checkedBatch(batch: PivotServerChangeBatch, fields: readonly PivotField[]): ServerBatch {
    const { from, to, removed, added } = batch;
    if (typeof from !== 'string' || from.length === 0 || typeof to !== 'string' || to.length === 0) {
        throw new TypeError('A Change Batch names the Source Versions it goes from and to, with text (ADR-0370 section 3).');
    }
    if (from === to) throw new TypeError(`A Change Batch goes from one Source Version to another, not from '${from}' to itself (ADR-0370 section 3).`);
    for (const [side, records] of [['removed', removed], ['added', added]] as const) {
        if (!Array.isArray(records)) throw new TypeError(`A Change Batch's ${side} records are an array (ADR-0370 section 3).`);
        try {
            checkBatchRecords(records, fields);
        } catch (error) {
            if (error instanceof Error) throw new TypeError(`A Change Batch's ${side} records: ${error.message}`, { cause: error });
            throw error;
        }
    }
    return Object.freeze({ from, to, removed: copiedRecords(removed), added: copiedRecords(added) });
}

// Records copied, so that a transport reusing its arrays changes nothing the source holds.
function copiedRecords(records: readonly (readonly PivotDetailValue[])[]): readonly (readonly PivotDetailValue[])[] {
    return Object.freeze(records.map(record => Object.freeze([...record])));
}
