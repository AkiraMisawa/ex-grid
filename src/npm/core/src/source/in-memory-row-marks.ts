// The Row Marks of a `GridSource.from` source (ADR-0043), the counterpart of C#'s InMemoryRowMarks.
// Everything is in hand, so the marks are kept per row the source holds: by identity, which the source never
// loses, since a value update goes through `replaceRow` and the mark moves to the new object with it.
//
// "All" is taken as the result stands at the press: every Detail row in it is marked there and then, so a
// row that joins the result afterwards, an edit that now matches the Filter, is not.
//
// A source given a Row Key keeps them by key instead (ADR-0140: one value names a row for its marks, for a
// source and for the grid). Its rows change by new objects under the same key, and arrive and leave in
// Change Batches, so a mark follows the key: a changed row keeps its mark, and a removed row takes its mark
// with it (ADR-0141), when its removal is shown. Until a gathered change is published the marks read the
// version the grid shows, so a row still on screen can be pressed and is counted.

import { defined, itemAt } from '../must';
import type { RowKind } from '../paint/row-classes';
import { lineUpMarks, RowMarkCounts, type RowMarkIntent, type RowMarks } from '../rows/row-marks';
import type { RowRange } from '../viewport/row-range';
import type { RowKey, RowKeyOf } from './row-key';

/** What the marks read of the source that holds them. */
export interface MarksHost<Row> {
    /** The result as it stands, and its Row Sequence Version. */
    current(): { readonly window: readonly Row[]; readonly rowSequenceVersion: number };
    /** Under a Row Key: the version of the row under a key that the grid shows, which until a gathered
     * change is published is the one before it; `undefined` where it shows none. */
    shown(key: RowKey): Row | undefined;
    /** Under a Row Key: every row held now, in the source's order. */
    heldRows(): readonly Row[];
}

export class InMemoryRowMarks<Row> implements RowMarks<Row> {
    readonly #host: MarksHost<Row>;
    readonly #listeners = new Set<() => void>();
    #rowKind: ((row: Row) => RowKind) | undefined;

    // Without a key: the rows by position, an object's first position, and each position's next one holding
    // the same object (-1 ends the chain). One object handed over twice is one row by identity (ADR-0003),
    // so its positions share a mark; the chain is what lets a replacement at one of them take the mark with it
    // and leave the other its own.
    readonly #rows: Row[];
    readonly #slots = new Map<Row, number>();
    readonly #nextSame: number[];
    readonly #marked: boolean[];

    // Under a Row Key: the key, and the keys marked.
    readonly #rowKey: RowKeyOf<Row> | undefined;
    readonly #markedKeys = new Set<RowKey>();

    // The counts walk the whole result, so they are kept until what they are made of moves: the Window (a new
    // list on every requery), the marks, or the roles.
    #countedWindow: readonly Row[] | undefined;
    #counts: RowMarkCounts | undefined;

    /** The marks of a source over `rows` without a key, kept by identity; or, given a key, kept by key. */
    constructor(host: MarksHost<Row>, rows: readonly Row[], rowKey: RowKeyOf<Row> | undefined) {
        this.#host = host;
        this.#rowKey = rowKey;
        this.#rows = rowKey === undefined ? [...rows] : [];
        this.#marked = this.#rows.map(() => false);
        this.#nextSame = this.#rows.map(() => -1);
        for (let i = this.#rows.length - 1; i >= 0; i--) this.#link(itemAt(this.#rows, i), i);
    }

    isMarked(row: Row): boolean {
        if (!this.#isDetail(row)) return false;
        if (this.#rowKey !== undefined) return this.#markedKeys.has(this.#rowKey(row));
        const slot = this.#slots.get(row);
        return slot !== undefined && itemAt(this.#marked, slot);
    }

    get counts(): RowMarkCounts {
        const { window } = this.#host.current();
        if (this.#counts === undefined || this.#countedWindow !== window) {
            this.#counts = this.#count(window);
            this.#countedWindow = window;
        }
        return this.#counts;
    }

    /** The marked rows, as the objects the source holds now, in the source's order: what an action runs over.
     * Rows, never positions (ADR-0043): positions name other rows after a Sort. An object handed over twice
     * is one row, listed once. Under a Row Key, a row whose removal is gathered and not yet shown is no
     * longer held, and is not listed. */
    get markedRows(): readonly Row[] {
        const rowKey = this.#rowKey;
        if (rowKey !== undefined) {
            return Object.freeze(this.#host.heldRows().filter(row => this.#markedKeys.has(rowKey(row)) && this.#isDetail(row)));
        }
        const listed = new Set<Row>();
        return Object.freeze(this.#rows.filter((row, i) => {
            if (!itemAt(this.#marked, i) || !this.#isDetail(row) || listed.has(row)) return false;
            listed.add(row);
            return true;
        }));
    }

    setRowKind(rowKind: ((row: Row) => RowKind) | undefined): void {
        if (rowKind === this.#rowKind) return;
        this.#rowKind = rowKind;
        this.#counts = undefined;
    }

    onMarkIntent(intent: RowMarkIntent<Row>): Promise<void> {
        let changed: boolean;
        switch (intent.kind) {
            case 'oneRow': changed = this.#markOne(intent.row, intent.marked); break;
            case 'allRows': changed = this.#markResult(intent.marked, intent.rowSequenceVersion); break;
            case 'positions': changed = this.#markPositions(intent.ranges, intent.rowSequenceVersion); break;
            default: throw new RangeError(`A Row Mark intent is oneRow, allRows or positions, not ${JSON.stringify((intent as { kind: unknown }).kind)}.`);
        }
        if (changed) {
            this.#counts = undefined;
            this.#announce();
        }
        return Promise.resolve();
    }

    subscribe(listener: () => void): () => void {
        this.#listeners.add(listener);
        return () => this.#listeners.delete(listener);
    }

    /** Under a Row Key, the source published new versions of its rows and showed the removal of the rows under
     * `removed`: their marks go with them, and a row added later under one of those keys starts unmarked. The
     * counts read the rows' roles, so they are counted again; a mark that went is a change, and is heard. */
    published(removed: readonly RowKey[]): void {
        if (this.#rowKey === undefined) return;
        this.#counts = undefined;
        let changed = false;
        for (const key of removed) changed = this.#markedKeys.delete(key) || changed;
        if (changed) this.#announce();
    }

    /** Without a key, the source replaced the row at position `index` by a new object: the mark belongs to the
     * row, so the replacement carries it. A copy of the old object at another position keeps its own. Should
     * the replacement already stand elsewhere, it is one row by identity, and takes the mark that moved. */
    replaced(index: number, row: Row, replacement: Row): void {
        if (this.#rowKey !== undefined) return;
        const marked = itemAt(this.#marked, index);
        this.#unlink(row, index);
        this.#rows[index] = replacement;
        this.#link(replacement, index);
        this.#set(defined(this.#slots.get(replacement), 'the replacement linked just above'), marked);
        this.#counts = undefined;
    }

    #announce(): void {
        for (const listener of Array.from(this.#listeners)) listener();
    }

    #markOne(row: Row, marked: boolean): boolean {
        if (!this.#isDetail(row)) return false;
        const rowKey = this.#rowKey;
        if (rowKey !== undefined) {
            // A row the grid shows nothing under the key of is not one it can have painted.
            const key = rowKey(row);
            if (this.#host.shown(key) === undefined) return false;
            return this.#setKey(key, marked);
        }
        const slot = this.#slots.get(row);
        return slot !== undefined && this.#set(slot, marked);
    }

    #markResult(marked: boolean, version: number): boolean {
        // "All" is the result the header was pressed over. A Filter changed since moved the version, and the
        // current result is not the one the user saw: refused, as a positional gesture is.
        const { window, rowSequenceVersion } = this.#host.current();
        if (version !== rowSequenceVersion) return false;
        let changed = false;
        for (const row of window) {
            if (!this.#isDetail(row)) continue;
            const rowKey = this.#rowKey;
            if (rowKey !== undefined) {
                changed = this.#setKey(rowKey(row), marked) || changed;
                continue;
            }
            const slot = this.#slots.get(row);
            if (slot !== undefined) changed = this.#set(slot, marked) || changed;
        }
        return changed;
    }

    #markPositions(ranges: readonly RowRange[], version: number): boolean {
        // Positions taken under another order name other rows: refused, and nothing is marked (ADR-0043, as
        // Held Selection refuses a selection across a reorder).
        const { window, rowSequenceVersion } = this.#host.current();
        if (version !== rowSequenceVersion) return false;
        const rowKey = this.#rowKey;
        // Each row once, by its key or its chain's slot.
        const named = new Map<RowKey | number, boolean>();
        for (const range of ranges) {
            const end = Math.min(range.start + range.count, window.length);
            for (let i = range.start; i < end; i++) {
                const row = itemAt(window, i);
                if (!this.#isDetail(row)) continue;
                if (rowKey !== undefined) {
                    const key = rowKey(row);
                    named.set(key, this.#markedKeys.has(key));
                    continue;
                }
                const slot = this.#slots.get(row);
                if (slot !== undefined) named.set(slot, itemAt(this.#marked, slot));
            }
        }
        if (named.size === 0) return false;
        const target = lineUpMarks([...named.values()].filter(Boolean).length, named.size);
        let changed = false;
        for (const name of named.keys()) {
            changed = (rowKey === undefined ? this.#set(name as number, target) : this.#setKey(name, target)) || changed; // oxlint-disable-line typescript/no-unsafe-type-assertion -- without a key every name is a slot
        }
        return changed;
    }

    #setKey(key: RowKey, marked: boolean): boolean {
        if (this.#markedKeys.has(key) === marked) return false;
        if (marked) this.#markedKeys.add(key);
        else this.#markedKeys.delete(key);
        return true;
    }

    /** Marks or unmarks every position of the object whose chain starts at `slot`. True when anything moved. */
    #set(slot: number, marked: boolean): boolean {
        let changed = false;
        for (let at = slot; at >= 0; at = itemAt(this.#nextSame, at)) {
            if (this.#marked[at] !== marked) {
                this.#marked[at] = marked;
                changed = true;
            }
        }
        return changed;
    }

    /** Puts a position at the head of its object's chain. */
    #link(row: Row, index: number): void {
        const first = this.#slots.get(row);
        this.#nextSame[index] = first === undefined ? -1 : first;
        this.#slots.set(row, index);
    }

    /** Takes a position out of its object's chain. */
    #unlink(row: Row, index: number): void {
        const first = this.#slots.get(row);
        if (first === undefined) return;
        const next = itemAt(this.#nextSame, index);
        if (first === index) {
            if (next < 0) this.#slots.delete(row);
            else this.#slots.set(row, next);
        } else {
            let at = first;
            while (itemAt(this.#nextSame, at) >= 0 && itemAt(this.#nextSame, at) !== index) at = itemAt(this.#nextSame, at);
            if (itemAt(this.#nextSame, at) === index) this.#nextSame[at] = next;
        }
        this.#nextSame[index] = -1;
    }

    #isDetail(row: Row): boolean {
        return this.#rowKind === undefined || this.#rowKind(row) === 'detail';
    }

    /** The counts, one per row: an object handed over twice is one row, counted once, as `markedRows` lists it
     * once and the action it feeds runs over it once (ADR-0043, ADR-0003). Under a Row Key the Window holds
     * each key once, as the source vouches, and the marked rows outside it are counted from the keys marked,
     * by the versions the grid shows, as the Window's are. */
    #count(window: readonly Row[]): RowMarkCounts {
        let markedInResult = 0;
        let rowsInResult = 0;
        let marked = 0;
        const rowKey = this.#rowKey;
        if (rowKey !== undefined) {
            for (const row of window) {
                if (!this.#isDetail(row)) continue;
                rowsInResult++;
                if (this.#markedKeys.has(rowKey(row))) markedInResult++;
            }
            for (const key of this.#markedKeys) {
                const row = this.#host.shown(key);
                if (row !== undefined && this.#isDetail(row)) marked++;
            }
        } else {
            const seen = new Set<number>();
            for (const row of window) {
                const slot = this.#slots.get(row);
                if (slot === undefined || !this.#isDetail(row) || seen.has(slot)) continue;
                seen.add(slot);
                rowsInResult++;
                if (itemAt(this.#marked, slot)) markedInResult++;
            }
            // Counted at the head of its chain only: once per object.
            this.#rows.forEach((row, i) => {
                if (itemAt(this.#marked, i) && this.#slots.get(row) === i && this.#isDetail(row)) marked++;
            });
        }
        return new RowMarkCounts({ markedInResult, rowsInResult, markedOutsideResult: marked - markedInResult });
    }
}
