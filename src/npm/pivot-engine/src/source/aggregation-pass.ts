// One question's pass over a Snapshot (ADR-0060/0066): every row's Items in the placed fields, the rows a
// Hidden Item leaves out, the leaf of each included row, and the parts asked for, accumulated at each leaf;
// then the Leaf Aggregates. The C# engine's ItemSpace, LeafIndex, ValueAccumulator and AggregationPass,
// transcribed for a Snapshot that holds every row it has: the rows are read a chunk at a time, each placed
// field's Items for the chunk column by column, as C# reads them, so Items are met — and numbered — in the
// order C# meets them.

import type { DecimalColumn, IntegerColumn, Snapshot, SnapshotColumn, SnapshotDecimal, TextColumn } from '@ex-grid/data';
import { decimalCompare, decimalOfSum, decimalOfWhole, decimalToNumber, doubleOfSum, doubleOfWide, wideAdd } from '../aggregates/exact-decimal';
import { PartColumns, neumaier, partExtremes, partProduct, partSum, partVariance, sumToInexact, type SumPart } from '../aggregates/parts';
import { PivotAnswer, PivotAnswerAxis, PivotRefusals, answerValues, madeAnswer, type PivotSourceRefusal } from '../cube/answer';
import { partBits, type PivotQuery } from '../cube/query';
import { ItemKeys, fromPublicKey, keyIdOf, textItemKey, toPublicKey, type ItemKey, type PivotItemKey } from '../items/item-key';
import { foldedText } from '../text/casing';
import { elementAt, unreachable } from '../must';
import { datePartOfTicks, type FieldBinding } from './binding';

/** The most rows read column by column at once, as C#'s pass reads them. */
export const chunkRows = 4096;

// ---- Items --------------------------------------------------------------------------------------

/**
 * One field's Items over a Snapshot (ADR-0060), and how each row maps to one. Text is told apart ignoring
 * case: the column's dictionary is folded, every code of one Item mapping to it, and the Item is spelled by
 * the lowest of its codes some row carries. A number is its value — an Integer 1, a Decimal 1.0 and a Double
 * 1 are one Item — and a non-finite Double is #NUM!. A date is its clock value, a date part the part's
 * number. Item indexes are handed out as Items are first met.
 */
export class ItemSpace {
    readonly binding: FieldBinding;
    readonly #hiddenKeys: ReadonlySet<string> | undefined;
    readonly #keys: ItemKey[] = [];
    readonly #hidden: boolean[] = [];
    readonly #firstCode: number[] = [];
    readonly #textColumn: TextColumn | undefined;
    #itemOfCode: number[] = [];
    #nextCode: number[] = [];
    #presence: number[] = [];
    readonly #numbers = new Map<number, number>();
    readonly #dates = new Map<bigint, number>();
    readonly #parts = new Map<number, number>();
    #blank = -1;
    #error = -1;
    #false = -1;
    #true = -1;

    constructor(binding: FieldBinding, hidden: readonly PivotItemKey[] | undefined) {
        this.binding = binding;
        if (hidden !== undefined && hidden.length > 0) this.#hiddenKeys = new Set(hidden.map(key => keyIdOf(fromPublicKey(key))));
        for (const column of binding.columns) {
            if (column.kind === 'text') this.#textColumn = column;
        }
        this.#fold();
    }

    /** How many Items the space has handed out. */
    get count(): number {
        return this.#keys.length;
    }

    /** Whether the field hides any Item. */
    get hidesAny(): boolean {
        return this.#hiddenKeys !== undefined;
    }

    /** Whether an Item is hidden. */
    isHidden(item: number): boolean {
        return elementAt(this.#hidden, item);
    }

    /** The Item's key, a text Item by its lowest code's text. */
    keyOf(item: number): ItemKey {
        return elementAt(this.#keys, item);
    }

    /** The public key of an Item: a text Item spelled as the first of its codes some row carries. */
    publicKeyOf(item: number): PivotItemKey {
        return elementAt(this.#firstCode, item) < 0 ? toPublicKey(this.keyOf(item)) : textItemKey(this.#spelling(item));
    }

    /** The engine's key of an Item, a text Item spelled as {@link publicKeyOf} spells it. */
    spelledKeyOf(item: number): ItemKey {
        return elementAt(this.#firstCode, item) < 0 ? this.keyOf(item) : ItemKeys.text(this.#spelling(item));
    }

    #spelling(item: number): string {
        const column = defined(this.#textColumn);
        for (let code = elementAt(this.#firstCode, item); code >= 0; code = elementAt(this.#nextCode, code)) {
            if (elementAt(this.#presence, code) > 0) return elementAt(column.dictionary, code);
        }
        return elementAt(column.dictionary, elementAt(this.#firstCode, item));
    }

    // The Text column's dictionary, folded ignoring case in code order: a spelling joins the Item of an
    // earlier one, or starts one.
    #fold(): void {
        const column = this.#textColumn;
        if (column === undefined) return;
        const itemOfText = new Map<string, number>();
        const lastCode: number[] = [];
        const size = column.dictionary.length;
        this.#itemOfCode = Array.from({ length: size }, () => -1);
        this.#nextCode = Array.from({ length: size }, () => -1);
        this.#presence = Array.from({ length: size }, () => 0);
        column.dictionary.forEach((text, code) => {
            const folded = foldedText(text);
            let item = itemOfText.get(folded);
            if (item === undefined) {
                item = this.#add(ItemKeys.text(text));
                itemOfText.set(folded, item);
                this.#firstCode[item] = code;
            } else {
                this.#nextCode[elementAt(lastCode, item)] = code;
            }
            lastCode[item] = code;
            this.#itemOfCode[code] = item;
            this.#nextCode[code] = -1;
        });
    }

    /** The Items of rows [`from`, `from + items.length`), each text code counted. */
    map(from: number, items: Int32Array): void {
        const single = this.binding.single;
        if (single !== undefined) {
            this.#mapColumn(single, from, items, true);
            return;
        }
        items.fill(-1);
        for (const column of this.binding.columns) this.#mapColumn(column, from, items, false);
        for (let i = 0; i < items.length; i++) {
            if (elementAt(items, i) < 0) items[i] = this.#blankItem();
        }
    }

    #mapColumn(column: SnapshotColumn, from: number, items: Int32Array, blanks: boolean): void {
        for (let i = 0; i < items.length; i++) {
            const row = from + i;
            if (column.kind === 'text') {
                const code = column.codeAt(row);
                if (code < 0) {
                    if (blanks) items[i] = this.#blankItem();
                    continue;
                }
                items[i] = elementAt(this.#itemOfCode, code);
                this.#presence[code] = elementAt(this.#presence, code) + 1;
                continue;
            }
            if (column.isBlank(row)) {
                if (blanks) items[i] = this.#blankItem();
                continue;
            }
            items[i] = this.#itemOfValue(column, row);
        }
    }

    #itemOfValue(column: SnapshotColumn, row: number): number {
        switch (column.kind) {
            case 'decimal': return this.#number(decimalToNumber(present(column.valueAt(row))));
            case 'integer': return this.#number(Number(present(column.valueAt(row))));
            case 'double': return this.#number(present(column.valueAt(row)));
            case 'date': {
                const ticks = present(column.valueAt(row));
                const part = this.binding.part;
                return part === undefined ? this.#date(ticks) : this.#part(datePartOfTicks(part, ticks));
            }
            case 'boolean': return this.#boolean(present(column.valueAt(row)));
            case 'text': throw new Error('Internal: text is mapped by its codes.');
            default: return unreachable(column);
        }
    }

    #blankItem(): number {
        if (this.#blank < 0) this.#blank = this.#add(ItemKeys.blank);
        return this.#blank;
    }

    #boolean(value: boolean): number {
        if (value) {
            if (this.#true < 0) this.#true = this.#add(ItemKeys.boolean(true));
            return this.#true;
        }
        if (this.#false < 0) this.#false = this.#add(ItemKeys.boolean(false));
        return this.#false;
    }

    #number(value: number): number {
        if (!Number.isFinite(value)) {
            if (this.#error < 0) this.#error = this.#add(ItemKeys.error);
            return this.#error;
        }
        const number = value === 0 ? 0 : value;
        let item = this.#numbers.get(number);
        if (item === undefined) {
            item = this.#add(ItemKeys.number(number));
            this.#numbers.set(number, item);
        }
        return item;
    }

    #date(ticks: bigint): number {
        let item = this.#dates.get(ticks);
        if (item === undefined) {
            item = this.#add(ItemKeys.date(ticks));
            this.#dates.set(ticks, item);
        }
        return item;
    }

    #part(value: number): number {
        let item = this.#parts.get(value);
        if (item === undefined) {
            item = this.#add(ItemKeys.number(value));
            this.#parts.set(value, item);
        }
        return item;
    }

    #add(key: ItemKey): number {
        const item = this.#keys.length;
        this.#keys.push(key);
        this.#hidden.push(this.#hiddenKeys?.has(keyIdOf(key)) === true);
        this.#firstCode.push(-1);
        return item;
    }
}

function present<T>(value: T | null): T {
    if (value === null) throw new Error('Internal: a value that is not Blank reads as null.');
    return value;
}

function defined<T>(value: T | undefined): T {
    if (value === undefined) throw new Error('Internal: a text Item without its Text column.');
    return value;
}

// ---- Leaves -------------------------------------------------------------------------------------

/**
 * The leaves of one question (ADR-0066): one per combination of the row and column fields' Items that has
 * an included record, each with its Items and its record count, made in the order their first rows are
 * read. A trie of Items, a Map a level.
 */
class LeafIndex {
    readonly itemOfLeaf: number[][];
    readonly records: number[] = [];
    readonly #levels: number;
    readonly #maxLeaves: number;
    readonly #children: Map<number, number>[] = [new Map<number, number>()];

    constructor(levels: number, maxLeaves: number) {
        this.#levels = levels;
        this.#maxLeaves = maxLeaves;
        this.itemOfLeaf = Array.from({ length: levels }, () => []);
    }

    get count(): number {
        return this.records.length;
    }

    /**
     * Finds or makes the leaf of each row of a chunk; an excluded row gets −1. Answers how many rows were
     * consumed: all of them, or — when a new leaf would pass the cap — the rows before the one that would
     * make it, and `refused` (ADR-0066: the question is refused as soon as the cap is passed).
     */
    assign(items: readonly Int32Array[], excluded: Uint8Array, leaves: Int32Array): { consumed: number; refused: boolean } {
        for (let i = 0; i < leaves.length; i++) {
            if (elementAt(excluded, i) !== 0) {
                leaves[i] = -1;
                continue;
            }
            let leaf: number;
            if (this.#levels === 0) {
                if (this.count === 0 && !this.#canMake()) return { consumed: i, refused: true };
                if (this.count === 0) this.#make(items, i);
                leaf = 0;
            } else {
                let node = 0;
                for (let level = 0; level < this.#levels - 1; level++) {
                    const children = elementAt(this.#children, node);
                    const item = elementAt(elementAt(items, level), i);
                    let next = children.get(item);
                    if (next === undefined) {
                        next = this.#children.length;
                        this.#children.push(new Map());
                        children.set(item, next);
                    }
                    node = next;
                }
                const last = elementAt(this.#children, node);
                const item = elementAt(elementAt(items, this.#levels - 1), i);
                let found = last.get(item);
                if (found === undefined) {
                    if (!this.#canMake()) return { consumed: i, refused: true };
                    found = this.#make(items, i);
                    last.set(item, found);
                }
                leaf = found;
            }
            this.records[leaf] = elementAt(this.records, leaf) + 1;
            leaves[i] = leaf;
        }
        return { consumed: leaves.length, refused: false };
    }

    #canMake(): boolean {
        return this.count < this.#maxLeaves;
    }

    #make(items: readonly Int32Array[], row: number): number {
        const leaf = this.records.length;
        this.records.push(0);
        for (let level = 0; level < this.#levels; level++) elementAt(this.itemOfLeaf, level).push(elementAt(elementAt(items, level), row));
        return leaf;
    }
}

// ---- Values -------------------------------------------------------------------------------------

/**
 * One field in Values, accumulated at each leaf into the parts asked for (ADR-0060/0066). An Integer or
 * Decimal column is summed exactly as a whole number at a power of ten, so the order its numbers arrive in
 * cannot change the sum, and the finished sum is a decimal without trailing zeros, or a double where no
 * decimal holds it. A Double column is summed per row with Neumaier's compensation. Text, dates and Booleans
 * are counted and are never a number. A field read through an accessor whose values are of several kinds is
 * folded per row in the data's order, each value by its own kind.
 *
 * The whole number is kept in 128 bits, as C# keeps it, and past them the sum is a double from then on. C#
 * adds a run of a segment's 64-bit numbers before folding it into the sum, where this adds them one by one:
 * the two leave for double at the same number save where a sum within a run's reach of 2^127 is brought
 * back by the run's later numbers, and after leaving, C#'s double adds a run's total where this adds each.
 */
class ValueAccumulator {
    readonly field: string;
    readonly columns: PartColumns;
    readonly #binding: FieldBinding;
    readonly #exact: DecimalColumn | IntegerColumn | undefined;
    readonly #sum: boolean;
    readonly #extremes: boolean;
    readonly #perRow: boolean;
    // Each leaf's exact sum: a whole number at a power of ten.
    readonly #wide: bigint[] = [];
    readonly #wideScale: number[] = [];

    constructor(binding: FieldBinding, parts: number) {
        this.field = binding.field.name;
        this.#binding = binding;
        this.columns = new PartColumns(parts);
        const single = binding.single;
        this.#exact = single !== undefined && (single.kind === 'decimal' || single.kind === 'integer') && binding.part === undefined ? single : undefined;
        this.#sum = (parts & partSum) !== 0;
        this.#extremes = (parts & partExtremes) !== 0;
        this.#perRow = (parts & (partProduct | partVariance)) !== 0;
    }

    /** Room for `leaves` leaves. */
    ensureLeaves(leaves: number): void {
        while (this.columns.count < leaves) {
            this.columns.addCell();
            this.#wide.push(0n);
            this.#wideScale.push(0);
        }
    }

    /** Folds rows [`from`, `from + leaves.length`) into their leaves; a row whose leaf is −1 is not read. */
    accumulate(from: number, leaves: Int32Array): void {
        const single = this.#binding.single;
        if (single === undefined) {
            this.#accumulateVariant(from, leaves);
            return;
        }
        for (let i = 0; i < leaves.length; i++) {
            const leaf = elementAt(leaves, i);
            if (leaf < 0) continue;
            const row = from + i;
            if (this.#binding.part !== undefined) {
                if (!single.isBlank(row)) this.columns.addOther(leaf);
                continue;
            }
            switch (single.kind) {
                case 'decimal':
                    if (!single.isBlank(row)) this.#addExact(leaf, present(single.valueAt(row)));
                    break;
                case 'integer':
                    if (!single.isBlank(row)) this.#addExact(leaf, decimalOfWhole(present(single.valueAt(row))));
                    break;
                case 'double':
                    if (!single.isBlank(row)) this.columns.addDouble(leaf, present(single.valueAt(row)));
                    break;
                case 'text':
                    if (single.codeAt(row) >= 0) this.columns.addOther(leaf);
                    break;
                case 'date':
                case 'boolean':
                    if (!single.isBlank(row)) this.columns.addOther(leaf);
                    break;
                default:
                    unreachable(single);
            }
        }
    }

    // An exact column's number: counted, its sum kept as a whole number, its extremes compared exactly, and
    // the parts kept in double given the double C# converts its decimal to.
    #addExact(leaf: number, value: SnapshotDecimal): void {
        const counts = elementAt(this.columns.counts, leaf);
        const first = counts.numbers === 0;
        if (this.#sum) this.#addToSum(leaf, value);
        if (this.#extremes) {
            const extremes = elementAt(defined(this.columns.extremes), leaf);
            if (first) {
                extremes.exactMin = value;
                extremes.exactMax = value;
            } else {
                if (decimalCompare(value, extremes.exactMin) < 0) extremes.exactMin = value;
                if (decimalCompare(value, extremes.exactMax) > 0) extremes.exactMax = value;
            }
        }
        if (this.#perRow) this.columns.addInexactParts(leaf, decimalToNumber(value), counts.numbers + 1);
        counts.values++;
        counts.numbers++;
    }

    // Adds a number to a leaf's exact sum; past 128 bits, the sum is a double from then on — money stays
    // exact until it cannot, then takes Excel's own arithmetic (ADR-0060).
    #addToSum(leaf: number, value: SnapshotDecimal): void {
        const sum = elementAt(defined(this.columns.sums), leaf);
        if (sum.inexact) {
            neumaier(sum, decimalToNumber(value));
            return;
        }
        const wide = elementAt(this.#wide, leaf);
        const wideScale = elementAt(this.#wideScale, leaf);
        const added = wideAdd(wide, wideScale, value.mantissa, value.scale);
        if (added !== undefined) {
            this.#wide[leaf] = added.sum;
            this.#wideScale[leaf] = added.scale;
            return;
        }
        sum.inexact = true;
        sum.double = doubleOfWide(wide, wideScale);
        sum.compensation = 0;
        neumaier(sum, decimalToNumber(value));
    }

    #accumulateVariant(from: number, leaves: Int32Array): void {
        for (let i = 0; i < leaves.length; i++) {
            const leaf = elementAt(leaves, i);
            if (leaf < 0) continue;
            const row = from + i;
            const column = this.#binding.columns.find(candidate => !candidate.isBlank(row));
            if (column === undefined) continue;
            switch (column.kind) {
                case 'decimal':
                    this.columns.addExact(leaf, present(column.valueAt(row)));
                    break;
                case 'integer':
                    this.columns.addExact(leaf, decimalOfWhole(present(column.valueAt(row))));
                    break;
                case 'double':
                    this.columns.addDouble(leaf, present(column.valueAt(row)));
                    break;
                case 'text':
                case 'date':
                case 'boolean':
                    this.columns.addOther(leaf);
                    break;
                default:
                    unreachable(column);
            }
        }
    }

    /** The finished parts of the leaves `order` names, in that order. */
    finished(order: readonly number[]): PartColumns {
        const finished = new PartColumns(this.columns.parts);
        for (const leaf of order) {
            const cell = finished.copyCell(this.columns, leaf);
            const sum: SumPart | undefined = this.#exact !== undefined && this.#sum ? elementAt(defined(finished.sums), cell) : undefined;
            if (sum !== undefined && !sum.inexact) {
                const wide = elementAt(this.#wide, leaf);
                const scale = elementAt(this.#wideScale, leaf);
                const exact = decimalOfSum(wide, scale);
                if (exact !== undefined) {
                    sum.exact = exact;
                } else {
                    sumToInexact(sum);
                    sum.double = doubleOfSum(wide, scale);
                }
            }
        }
        finished.finish();
        finished.canonicalize();
        return finished;
    }
}

// ---- The pass -----------------------------------------------------------------------------------

/**
 * One question's pass over a Snapshot. Every axis field's Items are taken for every row, left out or not:
 * Items are over all the data, and so is a text Item's first spelling (ADR-0060).
 */
export class AggregationPass {
    readonly query: PivotQuery;
    readonly #snapshot: Snapshot;
    readonly #axis: ItemSpace[];
    readonly #filters: ItemSpace[];
    readonly #leaves: LeafIndex;
    readonly #values: ValueAccumulator[];
    #refusal: PivotSourceRefusal | undefined;
    #read = 0;

    constructor(snapshot: Snapshot, bindings: ReadonlyMap<string, FieldBinding>, query: PivotQuery) {
        this.query = query;
        this.#snapshot = snapshot;
        const bound = (field: string): FieldBinding => {
            const found = bindings.get(field);
            if (found === undefined) throw new Error(`Internal: '${field}' is not bound.`);
            return found;
        };
        this.#axis = [...query.rows, ...query.columns].map(f => new ItemSpace(bound(f.field), f.hiddenItems));
        this.#filters = query.filters.filter(f => f.hiddenItems.length > 0).map(f => new ItemSpace(bound(f.field), f.hiddenItems));
        this.#values = query.values.map(v => new ValueAccumulator(bound(v.field), partBits(v.parts)));
        this.#leaves = new LeafIndex(this.#axis.length, query.maxLeaves);
    }

    /** How many rows the pass steps over. */
    get rowCount(): number {
        return this.#snapshot.rowCount;
    }

    /** The refusal, once the answer would pass the cap on leaves. */
    get refusal(): PivotSourceRefusal | undefined {
        return this.#refusal;
    }

    /** Reads the next rows, up to `to`, a chunk at a time; false once the question is refused. */
    step(to: number): boolean {
        while (this.#read < to) {
            const n = Math.min(chunkRows, to - this.#read);
            if (!this.#chunk(this.#read, n)) return false;
            this.#read += n;
        }
        return true;
    }

    #chunk(at: number, n: number): boolean {
        const excluded = new Uint8Array(n);
        const items = this.#axis.map(space => {
            const mapped = new Int32Array(n);
            space.map(at, mapped);
            if (space.hidesAny) exclude(space, mapped, excluded);
            return mapped;
        });
        for (const filter of this.#filters) {
            const mapped = new Int32Array(n);
            filter.map(at, mapped);
            exclude(filter, mapped, excluded);
        }
        const leaves = new Int32Array(n);
        const { refused } = this.#leaves.assign(items, excluded, leaves);
        if (refused) {
            this.#refusal = PivotRefusals.tooManyLeaves(this.query.maxLeaves);
            return false;
        }
        for (const values of this.#values) {
            values.ensureLeaves(this.#leaves.count);
            values.accumulate(at, leaves);
        }
        return true;
    }

    /** The Leaf Aggregates (ADR-0066): the leaves in the order they were made; each axis field's Items that
     * some leaf carries, in the order they were met, a text Item spelled by its first spelling among the
     * records; and each field in Values, finished. */
    answer(sourceVersion: string): PivotAnswer {
        if (this.#refusal !== undefined) return PivotAnswer.refused(this.#refusal);
        const order = this.#leaves.records.flatMap((records, leaf) => (records > 0 ? [leaf] : []));
        const axes = this.#axis.map((space, level) => {
            const itemOfLeaf = elementAt(this.#leaves.itemOfLeaf, level);
            const renumbered = new Int32Array(space.count).fill(-1);
            for (const leaf of order) renumbered[elementAt(itemOfLeaf, leaf)] = 0;
            const keys: PivotItemKey[] = [];
            for (let item = 0; item < space.count; item++) {
                if (elementAt(renumbered, item) < 0) continue;
                renumbered[item] = keys.length;
                keys.push(space.publicKeyOf(item));
            }
            const field = space.binding.field.name;
            return new PivotAnswerAxis(field, keys, order.map(leaf => elementAt(renumbered, elementAt(itemOfLeaf, leaf))));
        });
        const records = order.map(leaf => elementAt(this.#leaves.records, leaf));
        const values = this.#values.map(accumulator => answerValues(accumulator.field, accumulator.finished(order), order.length));
        const rows = this.query.rows.length;
        return madeAnswer(sourceVersion, axes.slice(0, rows), axes.slice(rows), order.length, records, values);
    }
}

function exclude(space: ItemSpace, items: Int32Array, excluded: Uint8Array): void {
    for (let i = 0; i < items.length; i++) {
        if (space.isHidden(elementAt(items, i))) excluded[i] = 1;
    }
}

/** A field's Items over all the data a Snapshot holds (ADR-0060/0066): every row's Item, not narrowed by any
 * Hidden Item, in the order they were met, a text Item spelled by its first spelling. */
export function itemsOfField(snapshot: Snapshot, binding: FieldBinding): readonly ItemKey[] {
    const space = new ItemSpace(binding, undefined);
    const rows: number[] = [];
    for (let at = 0; at < snapshot.rowCount; at += chunkRows) {
        const n = Math.min(chunkRows, snapshot.rowCount - at);
        const items = new Int32Array(n);
        space.map(at, items);
        for (const item of items) rows[item] = (rows[item] ?? 0) + 1; // oxlint-disable-line ex-grid/no-stand-in-default -- an Item not met yet has no row
    }
    const keys: ItemKey[] = [];
    for (let item = 0; item < space.count; item++) {
        if ((rows[item] ?? 0) > 0) keys.push(space.spelledKeyOf(item)); // oxlint-disable-line ex-grid/no-stand-in-default -- an Item no row carries has no row
    }
    return keys;
}
