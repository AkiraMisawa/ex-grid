// The rows of a GridSource.from given a Row Key (ADR-0141): the Consumer's rows by key, in the source's
// order, each with its ordinal, its place in that order. A changed row keeps the ordinal of the row it
// replaces; an added row takes the next, so it goes at the end; a removed row leaves a gap, closed when
// the gaps grow to half the rows. A whole new list in another order gives every row a new ordinal in that
// order. The ordinals only ever rise, so the source's order is ordinal order, and a row is found in it by
// binary search.

import { defined, elementAt } from '../must';
import { keyOf, quoted, type RowKey, type RowKeyOf } from './row-key';

interface Held<Row> {
    row: Row;
    ordinal: number;
}

/** The rows held, in order, with each row's ordinal beside it. */
export interface OrderedRows<Row> {
    readonly rows: readonly Row[];
    readonly ordinals: readonly number[];
}

/** Rows by Row Key, in the source's order. */
export class KeyedRows<Row> {
    readonly #rowKey: RowKeyOf<Row>;
    readonly #byKey = new Map<RowKey, Held<Row>>();
    // The slots in order: a row and its ordinal, or a gap a removed row left.
    #rows: (Row | undefined)[] = [];
    #ordinals: number[] = [];
    #gone: boolean[] = [];
    #goneCount = 0;
    #nextOrdinal: number;

    /** The rows in the order given. A key that repeats, or one that names no row, is refused by name: two
     * rows answering one key would be painted as one (ADR-0140). */
    constructor(rows: readonly Row[], rowKey: RowKeyOf<Row>) {
        this.#rowKey = rowKey;
        rows.forEach((row, index) => {
            const key = this.keyOf(row, `Row ${index.toString()}`);
            const first = this.#byKey.get(key);
            if (first !== undefined) {
                throw new TypeError(`Rows ${first.ordinal.toString()} and ${index.toString()} both have the Row Key ${quoted(key)}. `
                    + 'A Row Key tells one row from every other, and two rows under one key would be painted as one (ADR-0140, ADR-0141).');
            }
            this.#byKey.set(key, { row, ordinal: index });
            this.#rows.push(row);
            this.#ordinals.push(index);
            this.#gone.push(false);
        });
        this.#nextOrdinal = rows.length;
    }

    /** How many rows are held. */
    get count(): number {
        return this.#byKey.size;
    }

    /** A row's Row Key, refused by name when it names no row. */
    keyOf(row: Row, which: string): RowKey {
        return keyOf(this.#rowKey, row, which);
    }

    /** The row held under `key` now, and its ordinal; `undefined` when none is. */
    get(key: RowKey): { readonly row: Row; readonly ordinal: number } | undefined {
        const held = this.#byKey.get(key);
        return held === undefined ? undefined : { row: held.row, ordinal: held.ordinal };
    }

    /** Whether a row is held under `key`. */
    holds(key: RowKey): boolean {
        return this.#byKey.has(key);
    }

    /** Puts `row` in the place of the row held under its key. */
    change(key: RowKey, row: Row): void {
        const held = defined(this.#byKey.get(key), `the row held under ${quoted(key)}`);
        held.row = row;
        this.#rows[this.#slotOf(held.ordinal)] = row;
    }

    /** Adds `row` at the end of the order. */
    add(key: RowKey, row: Row): void {
        const ordinal = this.#nextOrdinal++;
        this.#byKey.set(key, { row, ordinal });
        this.#rows.push(row);
        this.#ordinals.push(ordinal);
        this.#gone.push(false);
    }

    /** Takes the row held under `key` out; the caller has checked that one is held. */
    remove(key: RowKey): void {
        const held = defined(this.#byKey.get(key), `the row held under ${quoted(key)}`);
        this.#byKey.delete(key);
        const slot = this.#slotOf(held.ordinal);
        this.#gone[slot] = true;
        this.#rows[slot] = undefined;
        this.#goneCount++;
        // Closed once the gaps are half the slots, so a day of bookings and cancellations costs what the
        // rows held do, and closing them costs, spread over the removals, a constant each.
        if (this.#goneCount > 64 && this.#goneCount * 2 > this.#rows.length) this.#compact();
    }

    /** Puts the rows in `keys`' order, which names every key held once (ADR-0141, D7: a whole new list sets
     * the order). Each row takes a new ordinal, above every ordinal given before, in that order. */
    reorder(keys: readonly RowKey[]): void {
        if (keys.length !== this.#byKey.size) {
            throw new Error(`Internal: a new order names ${keys.length.toString()} keys of the ${this.#byKey.size.toString()} held (ADR-0141).`);
        }
        const rows: Row[] = [];
        const ordinals: number[] = [];
        keys.forEach((key, index) => {
            const held = defined(this.#byKey.get(key), `the row held under ${quoted(key)}`);
            held.ordinal = this.#nextOrdinal + index;
            rows.push(held.row);
            ordinals.push(held.ordinal);
        });
        this.#rows = rows;
        this.#ordinals = ordinals;
        this.#gone = rows.map(() => false);
        this.#goneCount = 0;
        this.#nextOrdinal += keys.length;
    }

    /** Every key held, in no order. */
    keys(): IterableIterator<RowKey> {
        return this.#byKey.keys();
    }

    /** The rows held, in order, with their ordinals: what a whole requery reads. */
    snapshot(): OrderedRows<Row> {
        const rows: Row[] = [];
        const ordinals: number[] = [];
        this.#rows.forEach((row, slot) => {
            if (this.#gone[slot] === true || row === undefined) return;
            rows.push(row);
            ordinals.push(elementAt(this.#ordinals, slot));
        });
        return { rows, ordinals };
    }

    #slotOf(ordinal: number): number {
        let low = 0;
        let high = this.#ordinals.length;
        while (low < high) {
            const middle = (low + high) >>> 1;
            if (elementAt(this.#ordinals, middle) < ordinal) low = middle + 1;
            else high = middle;
        }
        if (this.#ordinals[low] !== ordinal) throw new Error(`Internal: the ordinal ${ordinal.toString()} is not held (ADR-0141).`);
        return low;
    }

    #compact(): void {
        const kept = this.#rows.flatMap((row, slot) => this.#gone[slot] === true || row === undefined
            ? [] : [{ row, ordinal: elementAt(this.#ordinals, slot) }]);
        this.#rows = kept.map(each => each.row);
        this.#ordinals = kept.map(each => each.ordinal);
        this.#gone = kept.map(() => false);
        this.#goneCount = 0;
    }
}
