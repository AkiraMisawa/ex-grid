// One question's pass over a Snapshot (ADR-0060/0066): every row's Items in the placed fields, the rows a
// Hidden Item leaves out, the leaf of each included row, and the parts asked for, accumulated at each leaf;
// then the Leaf Aggregates. The C# engine's ItemSpace, LeafIndex, ValueAccumulator and AggregationPass,
// transcribed for a Snapshot that holds every row it has: the rows are read a chunk at a time, each placed
// field's Items for the chunk column by column, as C# reads them, so Items are met — and numbered — in the
// order C# meets them.

import type { DecimalColumn, IntegerColumn, Snapshot, SnapshotChange, SnapshotColumn, SnapshotDecimal, TextColumn } from '@ex-grid/data';
import { decimalCompare, decimalOfSum, decimalOfWhole, decimalToNumber, doubleOfSum, doubleOfWide, wideAdd } from '../aggregates/exact-decimal';
import { PartColumns, neumaier, partExtremes, partProduct, partSum, partVariance, sumToInexact, type SumPart } from '../aggregates/parts';
import { PivotAnswer, PivotAnswerAxis, PivotRefusals, answerValues, madeAnswer, type PivotSourceRefusal } from '../cube/answer';
import { partBits, type PivotQuery } from '../cube/query';
import { ItemKeys, fromPublicKey, keyIdOf, textItemKey, toPublicKey, type ItemKey, type PivotItemKey } from '../items/item-key';
import { foldedText } from '../text/casing';
import { elementAt, unreachable } from '../must';
import { datePartOfTicks, type FieldBinding } from './binding';
import { runWhole } from './slicing';

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
    #binding: FieldBinding;
    readonly #hiddenKeys: ReadonlySet<string> | undefined;
    readonly #keys: ItemKey[] = [];
    readonly #hidden: boolean[] = [];
    readonly #firstCode: number[] = [];
    #textColumn: TextColumn | undefined;
    // The dictionary folded so far: each folded spelling's Item, and the last code of each Item's chain.
    readonly #itemOfText = new Map<string, number>();
    readonly #lastCode: number[] = [];
    readonly #itemOfCode: number[] = [];
    readonly #nextCode: number[] = [];
    readonly #presence: number[] = [];
    readonly #numbers = new Map<number, number>();
    readonly #dates = new Map<bigint, number>();
    readonly #parts = new Map<number, number>();
    #blank = -1;
    #error = -1;
    #false = -1;
    #true = -1;

    constructor(binding: FieldBinding, hidden: readonly PivotItemKey[] | undefined) {
        this.#binding = binding;
        if (hidden !== undefined && hidden.length > 0) this.#hiddenKeys = new Set(hidden.map(key => keyIdOf(fromPublicKey(key))));
        this.#textColumn = textColumnOf(binding);
        this.#fold();
    }

    /** The field and the columns it reads. */
    get binding(): FieldBinding {
        return this.#binding;
    }

    /**
     * The space over the next version of the Snapshot (ADR-0067): `binding` reads the same columns of the
     * version a Change Batch made, whose dictionary holds the one before's codes and those the batch brought.
     * The codes it brought are folded as the constructor folds the dictionary, so each Item keeps its index,
     * and an Item that first appears takes the next.
     */
    rebind(binding: FieldBinding): void {
        const text = textColumnOf(binding);
        if ((text === undefined) !== (this.#textColumn === undefined) || binding.columns.length !== this.#binding.columns.length) {
            throw new Error(`Internal: '${binding.field.name}' is bound to columns of other kinds in the next version.`);
        }
        this.#binding = binding;
        this.#textColumn = text;
        this.#fold();
    }

    /** Whether the field is one Text column's, whose Items are its dictionary's spellings and (blank). */
    get isText(): boolean {
        return this.#textColumn !== undefined && this.#binding.single !== undefined;
    }

    /** The (blank) Item's index, or −1 while no row has been Blank. */
    get blankItem(): number {
        return this.#blank;
    }

    /** Takes a row away from the spellings counted (ADR-0067): the row is no longer held, and a spelling no
     * row carries any more no longer labels its Item. Read in the version the row was held in. */
    unmap(row: number): void {
        const column = this.#textColumn;
        if (column === undefined) return;
        const code = column.codeAt(row);
        if (code >= 0) this.#presence[code] = elementAt(this.#presence, code) - 1;
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

    // The Text column's dictionary, folded ignoring case in code order from the first code not folded yet: a
    // spelling joins the Item of an earlier one, or starts one.
    #fold(): void {
        const column = this.#textColumn;
        if (column === undefined) return;
        const dictionary = column.dictionary;
        for (let code = this.#itemOfCode.length; code < dictionary.length; code++) {
            const text = elementAt(dictionary, code);
            const folded = foldedText(text);
            let item = this.#itemOfText.get(folded);
            if (item === undefined) {
                item = this.#add(ItemKeys.text(text));
                this.#itemOfText.set(folded, item);
                this.#firstCode[item] = code;
            } else {
                this.#nextCode[elementAt(this.#lastCode, item)] = code;
            }
            this.#lastCode[item] = code;
            this.#itemOfCode.push(item);
            this.#nextCode.push(-1);
            this.#presence.push(0);
        }
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

function exactColumnOf(binding: FieldBinding): DecimalColumn | IntegerColumn | undefined {
    const single = binding.single;
    return single !== undefined && (single.kind === 'decimal' || single.kind === 'integer') && binding.part === undefined ? single : undefined;
}

function textColumnOf(binding: FieldBinding): TextColumn | undefined {
    let text: TextColumn | undefined;
    for (const column of binding.columns) {
        if (column.kind === 'text') text = column;
    }
    return text;
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
    assign(items: readonly Int32Array[], excluded: Uint8Array, leaves: Int32Array, bounded = true): { consumed: number; refused: boolean } {
        for (let i = 0; i < leaves.length; i++) {
            if (elementAt(excluded, i) !== 0) {
                leaves[i] = -1;
                continue;
            }
            let leaf: number;
            if (this.#levels === 0) {
                if (this.count === 0 && bounded && !this.#canMake()) return { consumed: i, refused: true };
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
                    if (bounded && !this.#canMake()) return { consumed: i, refused: true };
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

    /** How many leaves hold a record: the leaves a fresh pass over the same rows would make. */
    get held(): number {
        let held = 0;
        for (const records of this.records) if (records > 0) held++;
        return held;
    }

    /** Whether a fresh pass over the rows held would make more leaves than the cap. */
    get passesCap(): boolean {
        return this.held > this.#maxLeaves;
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
    #binding: FieldBinding;
    #exact: DecimalColumn | IntegerColumn | undefined;
    readonly #sum: boolean;
    readonly #extremes: boolean;
    readonly #perRow: boolean;
    // Each leaf's exact sum: a whole number at a power of ten.
    readonly #wide: bigint[] = [];
    readonly #wideScale: number[] = [];
    // The largest magnitude of a whole number summed, and the scales met: what bounds every partial sum a
    // fresh pass could reach, in any order (ADR-0067).
    #largest = 0n;
    #leastScale = Number.POSITIVE_INFINITY;
    #mostScale = 0;

    constructor(binding: FieldBinding, parts: number) {
        this.field = binding.field.name;
        this.#binding = binding;
        this.columns = new PartColumns(parts);
        this.#exact = exactColumnOf(binding);
        this.#sum = (parts & partSum) !== 0;
        this.#extremes = (parts & partExtremes) !== 0;
        this.#perRow = (parts & (partProduct | partVariance)) !== 0;
    }

    /** The accumulator over the next version of the Snapshot: `binding` reads the same column there. */
    rebind(binding: FieldBinding): void {
        const exact = exactColumnOf(binding);
        if ((exact === undefined) !== (this.#exact === undefined) || binding.single?.kind !== this.#binding.single?.kind) {
            throw new Error(`Internal: '${binding.field.name}' is bound to a column of another kind in the next version.`);
        }
        this.#binding = binding;
        this.#exact = exact;
    }

    /**
     * Whether every part asked for is kept by subtraction and addition, in any order (ADR-0067): the counts,
     * and the sum of an Integer or a Decimal column, which is a whole number. Extremes, a product, a variance
     * and a Double's sum are recomputed from a leaf's records whenever a batch touches it.
     */
    get subtracts(): boolean {
        if (this.#binding.single === undefined || this.#extremes || this.#perRow) return false;
        return !this.#sum || this.#exact !== undefined;
    }

    /**
     * Takes row `row` of the version it was held in out of its leaf, as {@link accumulate} put it in; false
     * where the leaf cannot be kept so — a non-finite number left, a sum already a double, or a whole number
     * that would no longer fit — and is to be recomputed from its records.
     */
    trySubtract(leaf: number, row: number): boolean {
        const single = this.#binding.single;
        if (single === undefined || !this.subtracts) return false;
        const counts = elementAt(this.columns.counts, leaf);
        if (this.#binding.part !== undefined) {
            if (!single.isBlank(row)) counts.values--;
            return true;
        }
        switch (single.kind) {
            case 'decimal':
            case 'integer': {
                if (single.isBlank(row)) return true;
                if (this.#sum) {
                    if (elementAt(defined(this.columns.sums), leaf).inexact) return false;
                    const value = single.kind === 'decimal' ? present(single.valueAt(row)) : decimalOfWhole(present(single.valueAt(row)));
                    const taken = wideAdd(elementAt(this.#wide, leaf), elementAt(this.#wideScale, leaf), -value.mantissa, value.scale);
                    if (taken === undefined) return false;
                    this.#wide[leaf] = taken.sum;
                    this.#wideScale[leaf] = taken.scale;
                }
                counts.values--;
                counts.numbers--;
                return true;
            }
            case 'double': {
                if (single.isBlank(row)) return true;
                if (!Number.isFinite(present(single.valueAt(row)))) return false;
                counts.values--;
                counts.numbers--;
                return true;
            }
            case 'text':
                if (single.codeAt(row) >= 0) counts.values--;
                return true;
            case 'date':
            case 'boolean':
                if (!single.isBlank(row)) counts.values--;
                return true;
            default:
                return unreachable(single);
        }
    }

    /**
     * Whether a leaf's exact sum is what a fresh pass would make of its records in any order: no partial sum
     * of them can pass the 128 bits a whole number is kept in, so none goes over to a double. Where one
     * could, the leaf is recomputed in the Snapshot's order, as a fresh pass sums it.
     */
    keepsExactly(leaf: number): boolean {
        if (this.#exact === undefined || !this.#sum) return true;
        if (elementAt(defined(this.columns.sums), leaf).inexact) return false;
        const numbers = BigInt(elementAt(this.columns.counts, leaf).numbers);
        if (numbers === 0n) return true;
        const spread = BigInt(this.#mostScale - this.#leastScale);
        return numbers * this.#largest * 10n ** spread < 1n << 126n;
    }

    /** Empties a leaf, to be accumulated again from its records. */
    reset(leaf: number): void {
        this.columns.resetCell(leaf);
        this.#wide[leaf] = 0n;
        this.#wideScale[leaf] = 0;
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
        if (this.#sum) {
            const magnitude = value.mantissa < 0n ? -value.mantissa : value.mantissa;
            if (magnitude > this.#largest) this.#largest = magnitude;
            if (value.scale < this.#leastScale) this.#leastScale = value.scale;
            if (value.scale > this.#mostScale) this.#mostScale = value.scale;
            this.#addToSum(leaf, value);
        }
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
 *
 * When it keeps its rows (`keepRows`, over a Snapshot with a Record Key), it remembers each row's leaf, and
 * each row's Item of a field that is not one Text column's: a Change Batch is then folded into it
 * ({@link fold}) rather than read again (ADR-0067). After any sequence of batches it answers what a fresh
 * pass over the Snapshot they made answers, read in that Snapshot's order: the same leaves and Items, in the
 * same order, and every part to the last bit.
 */
export class AggregationPass {
    readonly query: PivotQuery;
    #snapshot: Snapshot;
    readonly #axis: ItemSpace[];
    readonly #filters: ItemSpace[];
    readonly #leaves: LeafIndex;
    readonly #values: ValueAccumulator[];
    #refusal: PivotSourceRefusal | undefined;
    #read = 0;
    // The rows kept (ADR-0067): each row's leaf, −1 for a row a Hidden Item leaves out; and each row's Item
    // of an axis field whose Items are met in the rows' order, for the order they were met in.
    #leafOf: Int32Array | undefined;
    #rowItems: (Int32Array | undefined)[];
    // Whether a batch has been folded in: the Items and leaves are then numbered as they came, and the
    // answer lists them in the order a fresh pass would have met them.
    #folded = false;

    constructor(snapshot: Snapshot, bindings: ReadonlyMap<string, FieldBinding>, query: PivotQuery, keepRows = false) {
        this.query = query;
        this.#snapshot = snapshot;
        this.#axis = [...query.rows, ...query.columns].map(f => new ItemSpace(boundIn(bindings, f.field), f.hiddenItems));
        this.#filters = query.filters.filter(f => f.hiddenItems.length > 0).map(f => new ItemSpace(boundIn(bindings, f.field), f.hiddenItems));
        this.#values = query.values.map(v => new ValueAccumulator(boundIn(bindings, v.field), partBits(v.parts)));
        this.#leaves = new LeafIndex(this.#axis.length, query.maxLeaves);
        if (keepRows) {
            if ([...this.#axis, ...this.#filters].some(space => space.binding.single === undefined)) {
                throw new Error('Internal: a pass keeps the rows of a Snapshot whose fields each read one column.');
            }
            this.#leafOf = new Int32Array(snapshot.rowCount).fill(-1);
        }
        this.#rowItems = this.#axis.map(space => (keepRows && !space.isText ? new Int32Array(snapshot.rowCount) : undefined));
    }

    /** The Snapshot the pass has read: the one it began on, or the one the last batch folded in made. */
    get snapshot(): Snapshot {
        return this.#snapshot;
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
        const { items, leaves, refused } = this.#assign(at, n, true);
        if (refused) {
            this.#refusal = PivotRefusals.tooManyLeaves(this.query.maxLeaves);
            return false;
        }
        this.#keep(at, items, leaves);
        for (const values of this.#values) {
            values.ensureLeaves(this.#leaves.count);
            values.accumulate(at, leaves);
        }
        return true;
    }

    // The Items and the leaves of rows [at, at + n): each placed field's Items, the rows a Hidden Item leaves
    // out, and the leaf of every other row.
    #assign(at: number, n: number, bounded: boolean): { items: Int32Array[]; leaves: Int32Array; refused: boolean } {
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
        const { refused } = this.#leaves.assign(items, excluded, leaves, bounded);
        return { items, leaves, refused };
    }

    #keep(at: number, items: readonly Int32Array[], leaves: Int32Array): void {
        this.#leafOf?.set(leaves, at);
        this.#rowItems.forEach((kept, level) => kept?.set(elementAt(items, level), at));
    }

    /**
     * Brings the pass from the Snapshot it has read to the next version a Change Batch made (ADR-0067),
     * without reading again the rows the batch did not touch; `bindings` are the fields bound to that
     * version. False when it cannot — `change` does not start from the Snapshot read, it removes rows from a
     * pass that kept none, or a fresh pass would now pass the cap on leaves — and the pass is then to be
     * dropped, half folded, and the question asked afresh.
     *
     * 1. Each row removed, read in the version before, leaves its spellings and its leaf: the counts and an
     *    Integer or Decimal sum by subtraction, a whole number's, which is exact in any order; a leaf whose
     *    other parts it held is marked.
     * 2. The rows of the next version are the rows kept, in their order, and the rows the batch brought, in
     *    their places: a changed record where the one it replaces stood, an added one at the end. Each row
     *    brought is read as the pass reads any row; Items that first appear bring their leaves. A part kept
     *    by subtraction takes its number in; any other part does where the row comes after every row kept,
     *    as a fresh pass would take it last, and its leaf is marked where it does not.
     * 3. The marked leaves are recomputed from their rows, in the Snapshot's order, as a fresh pass computes
     *    them: every step of a double counts.
     */
    fold(change: SnapshotChange, bindings: ReadonlyMap<string, FieldBinding>): boolean {
        if (change.before !== this.#snapshot || this.#refusal !== undefined || this.#read !== this.#snapshot.rowCount) return false;
        const leafOf = this.#leafOf;
        if (leafOf === undefined && change.removed.length > 0) return false;
        const marked = this.#values.map(() => new Set<number>());
        const touched = new Set<number>();
        const records = this.#leaves.records;

        // 1. The rows removed, read in the version before.
        for (const row of change.removed) {
            for (const space of [...this.#axis, ...this.#filters]) space.unmap(row);
            const leaf = leafOf === undefined ? -1 : elementAt(leafOf, row);
            if (leaf < 0) continue;
            records[leaf] = elementAt(records, leaf) - 1;
            touched.add(leaf);
            this.#values.forEach((values, v) => {
                const leaves = elementAt(marked, v);
                if (!leaves.has(leaf) && !values.trySubtract(leaf, row)) leaves.add(leaf);
            });
        }

        // 2. The rows of the next version.
        const after = change.after;
        for (const space of this.#axis) space.rebind(boundIn(bindings, space.binding.field.name));
        for (const space of this.#filters) space.rebind(boundIn(bindings, space.binding.field.name));
        for (const values of this.#values) values.rebind(boundIn(bindings, values.field));
        const rowCount = after.rowCount;
        const nextLeafOf = leafOf === undefined ? undefined : new Int32Array(rowCount);
        const nextItems = this.#rowItems.map(kept => (kept === undefined ? undefined : new Int32Array(rowCount)));
        const added = change.added;
        // The last row of the next version that the one before held: a row brought after it comes last.
        let lastKept = -1;
        let removed = 0;
        let addedAt = 0;
        let before = 0;
        for (let row = 0; row < rowCount; row++) {
            if (addedAt < added.length && elementAt(added, addedAt) === row) {
                addedAt++;
                continue;
            }
            while (removed < change.removed.length && elementAt(change.removed, removed) === before) {
                removed++;
                before++;
            }
            if (nextLeafOf !== undefined && leafOf !== undefined) nextLeafOf[row] = elementAt(leafOf, before);
            nextItems.forEach((next, level) => {
                const kept = this.#rowItems[level];
                if (next !== undefined && kept !== undefined) next[row] = elementAt(kept, before);
            });
            lastKept = row;
            before++;
        }
        this.#leafOf = nextLeafOf;
        this.#rowItems = nextItems;
        for (let first = 0; first < added.length;) {
            let end = first + 1;
            while (end < added.length && elementAt(added, end) === elementAt(added, end - 1) + 1) end++;
            const at = elementAt(added, first);
            const n = end - first;
            const { items, leaves } = this.#assign(at, n, false);
            this.#keep(at, items, leaves);
            for (const leaf of leaves) if (leaf >= 0) touched.add(leaf);
            this.#values.forEach((values, v) => {
                values.ensureLeaves(this.#leaves.count);
                const leavesMarked = elementAt(marked, v);
                if (values.subtracts) {
                    values.accumulate(at, leaves);
                    return;
                }
                // Taken in where a fresh pass would take it, last; otherwise the leaf is recomputed.
                const taken = leaves.map((leaf, i) => {
                    if (leaf < 0 || leavesMarked.has(leaf)) return -1;
                    if (at + i > lastKept) return leaf;
                    leavesMarked.add(leaf);
                    return -1;
                });
                values.accumulate(at, taken);
            });
            first = end;
        }
        this.#snapshot = after;
        this.#read = rowCount;
        this.#folded = true;
        if (this.#leaves.passesCap) return false;

        // 3. The leaves whose parts subtraction cannot keep as a fresh pass makes them.
        this.#values.forEach((values, v) => {
            const leaves = elementAt(marked, v);
            if (values.subtracts) {
                for (const leaf of touched) if (!values.keepsExactly(leaf)) leaves.add(leaf);
            }
        });
        if (marked.every(leaves => leaves.size === 0)) return true;
        // A pass that kept no rows' leaves cannot find a leaf's rows: it is asked afresh.
        if (this.#leafOf === undefined) return false;
        this.#recompute(marked);
        return true;
    }

    // Each field's marked leaves, from nothing, over the rows each holds, in the Snapshot's order: the
    // operations a fresh pass performs on that leaf, in the same order, so the parts come out the same to
    // the last bit. One sweep over the rows serves every field.
    #recompute(marked: readonly ReadonlySet<number>[]): void {
        const leafOf = defined(this.#leafOf);
        const fields = this.#values.flatMap((values, v) => {
            const leaves = elementAt(marked, v);
            if (leaves.size === 0) return [];
            const of = new Uint8Array(this.#leaves.count);
            for (const leaf of leaves) {
                values.reset(leaf);
                of[leaf] = 1;
            }
            return [{ values, of, masked: new Int32Array(chunkRows) }];
        });
        for (let at = 0; at < leafOf.length; at += chunkRows) {
            const n = Math.min(chunkRows, leafOf.length - at);
            for (const { values, of, masked } of fields) {
                let any = false;
                for (let i = 0; i < n; i++) {
                    const leaf = elementAt(leafOf, at + i);
                    const take = leaf >= 0 && elementAt(of, leaf) === 1;
                    masked[i] = take ? leaf : -1;
                    any ||= take;
                }
                if (any) values.accumulate(at, masked.subarray(0, n));
            }
        }
    }

    /** The Leaf Aggregates (ADR-0066): the leaves in the order their first rows come; each axis field's
     * Items that some leaf carries, in the order they were met, a text Item spelled by its first spelling
     * among the records; and each field in Values, finished. */
    answer(sourceVersion: string): PivotAnswer {
        return runWhole(this.answerSteps(sourceVersion));
    }

    /** {@link answer}, a step at a time: it yields after each piece of work, for a caller to slice (PV-40). */
    *answerSteps(sourceVersion: string): Generator<void, PivotAnswer> {
        if (this.#refusal !== undefined) return PivotAnswer.refused(this.#refusal);
        const order = yield* this.#leafOrder();
        const axes: PivotAnswerAxis[] = [];
        for (let level = 0; level < this.#axis.length; level++) {
            const space = elementAt(this.#axis, level);
            const itemOfLeaf = elementAt(this.#leaves.itemOfLeaf, level);
            const carried = new Uint8Array(space.count);
            for (const leaf of order) carried[elementAt(itemOfLeaf, leaf)] = 1;
            const renumbered = new Int32Array(space.count).fill(-1);
            const keys: PivotItemKey[] = [];
            for (const item of yield* this.#itemOrder(level)) {
                if (elementAt(carried, item) === 0) continue;
                renumbered[item] = keys.length;
                keys.push(space.publicKeyOf(item));
            }
            yield;
            const field = space.binding.field.name;
            axes.push(new PivotAnswerAxis(field, keys, order.map(leaf => elementAt(renumbered, elementAt(itemOfLeaf, leaf)))));
            yield;
        }
        const records = order.map(leaf => elementAt(this.#leaves.records, leaf));
        const values: ReturnType<typeof answerValues>[] = [];
        for (const accumulator of this.#values) {
            values.push(answerValues(accumulator.field, accumulator.finished(order), order.length));
            yield;
        }
        const rows = this.query.rows.length;
        return madeAnswer(sourceVersion, axes.slice(0, rows), axes.slice(rows), order.length, records, values);
    }

    // The leaves that hold records, in the order a fresh pass makes them: the order of their first rows.
    *#leafOrder(): Generator<void, number[]> {
        const held = this.#leaves.records.flatMap((records, leaf) => (records > 0 ? [leaf] : []));
        const leafOf = this.#leafOf;
        if (!this.#folded || leafOf === undefined) return held;
        const first = new Float64Array(this.#leaves.count).fill(Number.POSITIVE_INFINITY);
        for (let row = leafOf.length - 1; row >= 0; row--) {
            const leaf = elementAt(leafOf, row);
            if (leaf >= 0) first[leaf] = row;
            if (row % chunkRows === 0) yield;
        }
        return held.toSorted((a, b) => elementAt(first, a) - elementAt(first, b));
    }

    // A level's Items in the order a fresh pass meets them: a Text column's in its dictionary's order with
    // (blank) after them, any other field's in the order of the first rows that carry them.
    *#itemOrder(level: number): Generator<void, number[]> {
        const space = elementAt(this.#axis, level);
        const indexed = Array.from({ length: space.count }, (_, item) => item);
        if (!this.#folded) return indexed;
        if (space.isText) {
            const blank = space.blankItem;
            return blank < 0 ? indexed : [...indexed.filter(item => item !== blank), blank];
        }
        const rowItems = this.#rowItems[level];
        if (rowItems === undefined) return indexed;
        const first = new Float64Array(space.count).fill(Number.POSITIVE_INFINITY);
        for (let row = rowItems.length - 1; row >= 0; row--) {
            first[elementAt(rowItems, row)] = row;
            if (row % chunkRows === 0) yield;
        }
        return indexed.toSorted((a, b) => elementAt(first, a) - elementAt(first, b));
    }
}

function boundIn(bindings: ReadonlyMap<string, FieldBinding>, field: string): FieldBinding {
    const found = bindings.get(field);
    if (found === undefined) throw new Error(`Internal: '${field}' is not bound.`);
    return found;
}

function exclude(space: ItemSpace, items: Int32Array, excluded: Uint8Array): void {
    for (let i = 0; i < items.length; i++) {
        if (space.isHidden(elementAt(items, i))) excluded[i] = 1;
    }
}

/** A field's Items over all the data a Snapshot holds (ADR-0060/0066): every row's Item, not narrowed by any
 * Hidden Item, in the order they were met, a text Item spelled by its first spelling. */
export function itemsOfField(snapshot: Snapshot, binding: FieldBinding): readonly ItemKey[] {
    return runWhole(itemsOfFieldSteps(snapshot, binding));
}

/** {@link itemsOfField}, yielding after each chunk of rows (PV-40). */
export function* itemsOfFieldSteps(snapshot: Snapshot, binding: FieldBinding): Generator<void, readonly ItemKey[]> {
    const space = new ItemSpace(binding, undefined);
    const rows: number[] = [];
    for (let at = 0; at < snapshot.rowCount; at += chunkRows) {
        const n = Math.min(chunkRows, snapshot.rowCount - at);
        const items = new Int32Array(n);
        space.map(at, items);
        for (const item of items) rows[item] = (rows[item] ?? 0) + 1; // oxlint-disable-line ex-grid/no-stand-in-default -- an Item not met yet has no row
        yield;
    }
    const keys: ItemKey[] = [];
    for (let item = 0; item < space.count; item++) {
        if ((rows[item] ?? 0) > 0) keys.push(space.spelledKeyOf(item)); // oxlint-disable-line ex-grid/no-stand-in-default -- an Item no row carries has no row
    }
    return keys;
}
