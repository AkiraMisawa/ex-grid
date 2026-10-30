// Live server data (ADR-0370 section 3): a server's Change Batch — the records its data lost and gained
// between two Source Versions — folded into the Leaf Aggregates a fetching source holds. The records are read
// as the bundled source reads records, into Leaf Aggregates of their own under the question held, so the
// engine finds their leaves by the binding that answers the question; the server names no leaf. A part is
// kept only where subtraction and addition keep it as the server answers it; anything else is asked again.

import { decimalCompare, decimalOfSum, wideAdd } from '../aggregates/exact-decimal';
import { partExtremes, partProduct, partSum, partVariance } from '../aggregates/parts';
import { PivotAnswer, PivotAnswerBuilder, type PivotAnswerValues, type PivotNumber } from '../cube/answer';
import { partBits, type PivotQuery } from '../cube/query';
import type { PivotField } from '../fields/pivot-field';
import { publicKeyId, type PivotItemKey } from '../items/item-key';
import { elementAt } from '../must';
import { AggregationPass } from './aggregation-pass';
import { detailSnapshot, type DetailValue } from './binding';

/** A server's Change Batch as the source keeps it until it is folded: its versions and its records. */
export interface ServerBatch {
    readonly from: string;
    readonly to: string;
    readonly removed: readonly (readonly DetailValue[])[];
    readonly added: readonly (readonly DetailValue[])[];
}

// One leaf of an answer: its Items, outermost row field first, and where it stands in the answer.
interface LeafAt {
    readonly items: readonly PivotItemKey[];
    readonly answer: PivotAnswer;
    readonly leaf: number;
}

/** Checks a batch's records against the source's fields, as they will be read: a record of another length,
 * or a value of no kind a record carries, is refused with the Snapshot's error, naming the record. */
export function checkBatchRecords(records: readonly (readonly DetailValue[])[], fields: readonly PivotField[]): void {
    detailSnapshot(records, fields);
}

/**
 * `held` with `batches` folded in, in order, under the last batch's `to`; or `undefined` where the fold cannot
 * keep the answer as the server would answer it, and the question is to be asked again: a batch out of chain
 * with the answer, a leaf left with fewer than no records, a part touched that subtraction and addition do not
 * keep exactly, or a fold that passes the cap.
 */
export function foldServerBatches(query: PivotQuery, held: PivotAnswer, batches: readonly ServerBatch[], fields: readonly PivotField[]): PivotAnswer | undefined {
    if (held.isRefused || held.isLevels || batches.length === 0) return undefined;
    let version = held.sourceVersion;
    for (const batch of batches) {
        if (batch.from !== version) return undefined;
        version = batch.to;
    }
    const removed = answerOf(query, batches.flatMap(batch => batch.removed), fields);
    const added = answerOf(query, batches.flatMap(batch => batch.added), fields);
    if (removed === null || added === null) return undefined;
    const lost = leavesOf(removed);
    const gained = leavesOf(added);
    const builder = new PivotAnswerBuilder(query, version);
    const kept = new Set<string>();
    const place = (id: string, at: LeafAt | undefined): boolean => {
        kept.add(id);
        return foldLeaf(builder, query, at, lost.get(id), gained.get(id));
    };
    for (const [id, at] of leavesOf(held)) {
        if (!place(id, at)) return undefined;
    }
    for (const id of gained.keys()) {
        if (!kept.has(id) && !place(id, undefined)) return undefined;
    }
    // A record lost from a leaf the answer does not hold is a batch that does not match it.
    for (const id of lost.keys()) {
        if (!kept.has(id)) return undefined;
    }
    const answer = builder.build();
    return answer.isRefused ? undefined : answer;
}

// The Leaf Aggregates of records under the question; undefined for no records, null where the pass refuses.
function answerOf(query: PivotQuery, records: readonly (readonly DetailValue[])[], fields: readonly PivotField[]): PivotAnswer | undefined | null {
    if (records.length === 0) return undefined;
    const { snapshot, bindings } = detailSnapshot(records, fields);
    const pass = new AggregationPass(snapshot, bindings, query);
    pass.step(pass.rowCount);
    const answer = pass.answer('');
    return answer.isRefused ? null : answer;
}

function leavesOf(answer: PivotAnswer | undefined): Map<string, LeafAt> {
    const leaves = new Map<string, LeafAt>();
    if (answer === undefined) return leaves;
    const axes = [...answer.rows, ...answer.columns];
    for (let leaf = 0; leaf < answer.leafCount; leaf++) {
        const items = axes.map(axis => elementAt(axis.items, axis.itemAt(leaf)));
        leaves.set(items.map(publicKeyId).join('\u0000'), { items, answer, leaf });
    }
    return leaves;
}

// One leaf of the folded answer: held, less what was lost, plus what was gained. False where it cannot be kept.
function foldLeaf(builder: PivotAnswerBuilder, query: PivotQuery, held: LeafAt | undefined, lost: LeafAt | undefined, gained: LeafAt | undefined): boolean {
    const records = recordsOf(held) - recordsOf(lost) + recordsOf(gained);
    if (records < 0) return false;
    const touched = lost !== undefined || gained !== undefined;
    if (records === 0) return query.values.every((_, v) => countsOf(held, v).values - countsOf(lost, v).values + countsOf(gained, v).values === 0);
    const items = (held ?? gained)?.items;
    if (items === undefined) return false;
    const leaf = builder.addLeaf(items, records);
    for (let v = 0; v < query.values.length; v++) {
        const parts = partBits(elementAt(query.values, v).parts);
        if (!touched) {
            copyParts(builder, leaf, v, parts, held);
            continue;
        }
        if (!foldValue(builder, leaf, v, parts, records, held, lost, gained)) return false;
    }
    return true;
}

function recordsOf(at: LeafAt | undefined): number {
    return at === undefined ? 0 : at.answer.recordsAt(at.leaf);
}

function valuesOf(at: LeafAt, v: number): PivotAnswerValues {
    return elementAt(at.answer.values, v);
}

function countsOf(at: LeafAt | undefined, v: number): { readonly values: number; readonly numbers: number; readonly nonFinite: boolean } {
    if (at === undefined) return { values: 0, numbers: 0, nonFinite: false };
    const values = valuesOf(at, v);
    return { values: values.valuesAt(at.leaf), numbers: values.numbersAt(at.leaf), nonFinite: values.nonFiniteAt(at.leaf) };
}

// A leaf no batch touched: its parts as held.
function copyParts(builder: PivotAnswerBuilder, leaf: number, v: number, parts: number, held: LeafAt | undefined): void {
    if (held === undefined) throw new Error('Internal: a leaf no batch touched is a leaf the answer held.');
    const counts = countsOf(held, v);
    builder.setCounts(leaf, v, counts.values, counts.numbers, counts.nonFinite);
    const values = valuesOf(held, v);
    if ((parts & partSum) !== 0) builder.setSum(leaf, v, values.sumAt(held.leaf));
    if ((parts & partExtremes) !== 0) builder.setExtremes(leaf, v, values.minAt(held.leaf), values.maxAt(held.leaf));
    if ((parts & partProduct) !== 0) builder.setProduct(leaf, v, values.productAt(held.leaf));
    if ((parts & partVariance) !== 0) builder.setVariance(leaf, v, values.meanAt(held.leaf), values.m2At(held.leaf));
}

// One field in Values at a leaf a batch touched: the counts by subtraction and addition, an exact sum while it
// stays within 128 bits, and the extremes where numbers came and none left. False for anything else.
function foldValue(builder: PivotAnswerBuilder, leaf: number, v: number, parts: number, records: number,
    held: LeafAt | undefined, lost: LeafAt | undefined, gained: LeafAt | undefined): boolean {
    const was = countsOf(held, v);
    const out = countsOf(lost, v);
    const came = countsOf(gained, v);
    const values = was.values - out.values + came.values;
    const numbers = was.numbers - out.numbers + came.numbers;
    if (values < 0 || numbers < 0 || numbers > values || values > records) return false;
    // A non-finite number is not taken away; one brought in leaves only the counts to keep.
    if (out.nonFinite) return false;
    const nonFinite = was.nonFinite || came.nonFinite;
    const numbersMoved = out.numbers > 0 || came.numbers > 0;
    if (numbersMoved && (parts & (partProduct | partVariance)) !== 0) return false;
    if (numbersMoved && came.nonFinite && (parts & (partSum | partExtremes)) !== 0) return false;
    builder.setCounts(leaf, v, values, numbers, nonFinite);
    if ((parts & partSum) !== 0) {
        const sum = numbersMoved ? foldedSum(sumOf(held, v), sumOf(lost, v), sumOf(gained, v)) : sumOf(held ?? gained, v);
        if (sum === undefined) return false;
        builder.setSum(leaf, v, sum);
    }
    if ((parts & partExtremes) !== 0) {
        if (out.numbers > 0) return false;
        const extremes = foldedExtremes(held, gained, v);
        if (extremes === undefined) return false;
        builder.setExtremes(leaf, v, extremes.min, extremes.max);
    }
    // No number came or left: a product and a variance stand as the leaf's numbers made them.
    const base = held ?? gained;
    if (base === undefined) return false;
    if ((parts & partProduct) !== 0) builder.setProduct(leaf, v, valuesOf(base, v).productAt(base.leaf));
    if ((parts & partVariance) !== 0) builder.setVariance(leaf, v, valuesOf(base, v).meanAt(base.leaf), valuesOf(base, v).m2At(base.leaf));
    return true;
}

const zero: PivotNumber = Object.freeze({ kind: 'exact', value: Object.freeze({ mantissa: 0n, scale: 0 }) });

function sumOf(at: LeafAt | undefined, v: number): PivotNumber {
    return at === undefined ? zero : valuesOf(at, v).sumAt(at.leaf);
}

// held − lost + gained, exactly, or undefined where any is a double or the sum leaves 128 bits or a decimal.
function foldedSum(held: PivotNumber, lost: PivotNumber, gained: PivotNumber): PivotNumber | undefined {
    if (held.kind !== 'exact' || lost.kind !== 'exact' || gained.kind !== 'exact') return undefined;
    const less = wideAdd(held.value.mantissa, held.value.scale, -lost.value.mantissa, lost.value.scale);
    if (less === undefined) return undefined;
    const more = wideAdd(less.sum, less.scale, gained.value.mantissa, gained.value.scale);
    if (more === undefined) return undefined;
    const sum = decimalOfSum(more.sum, more.scale);
    return sum === undefined ? undefined : { kind: 'exact', value: sum };
}

// The extremes of the numbers held and gained, none lost; the held one kept on a tie, as a pass that met it
// first keeps it. Undefined where one side is exact and the other a double.
function foldedExtremes(held: LeafAt | undefined, gained: LeafAt | undefined, v: number): { readonly min: PivotNumber; readonly max: PivotNumber } | undefined {
    const had = held !== undefined && countsOf(held, v).numbers > 0 ? held : undefined;
    const came = gained !== undefined && countsOf(gained, v).numbers > 0 ? gained : undefined;
    const source = had ?? came;
    if (source === undefined) {
        // No number at all: the extremes a leaf of no numbers carries, as held.
        const at = held ?? gained;
        if (at === undefined) return undefined;
        return { min: valuesOf(at, v).minAt(at.leaf), max: valuesOf(at, v).maxAt(at.leaf) };
    }
    if (had === undefined || came === undefined) return { min: valuesOf(source, v).minAt(source.leaf), max: valuesOf(source, v).maxAt(source.leaf) };
    const min = extreme(valuesOf(had, v).minAt(had.leaf), valuesOf(came, v).minAt(came.leaf), -1);
    const max = extreme(valuesOf(had, v).maxAt(had.leaf), valuesOf(came, v).maxAt(came.leaf), 1);
    return min === undefined || max === undefined ? undefined : { min, max };
}

// The one of two extremes further toward `side` (−1 the smaller, 1 the larger), `held` on a tie.
function extreme(held: PivotNumber, came: PivotNumber, side: -1 | 1): PivotNumber | undefined {
    if (held.kind === 'exact' && came.kind === 'exact') return decimalCompare(came.value, held.value) === side ? came : held;
    if (held.kind === 'double' && came.kind === 'double') return Math.sign(came.value - held.value) === side ? came : held;
    return undefined;
}
