// A computation's cells (ADR-0153): the first cube's own, which nothing writes once that cube is made, under
// pages an update copies before it writes them, and the cell index likewise, the first cube's under an overlay
// of the keys added or taken away since. A computation's first version copies nothing; a later one copies the
// pages it wrote and shares every other. The C# engine's ComputationCells.

import type { SnapshotDecimal } from '@ex-grid/data';
import { decimalAdd } from '../aggregates/exact-decimal';
import { emptyValue, PartColumns, type AggregateValue } from '../aggregates/parts';
import { cellKey, type CubeCells } from '../cube/cube';
import type { PivotAggregation } from '../layout/pivot-enums';
import { defined, elementAt } from '../must';
import { PagedArray, pageSize, type PagedWriter } from './paged';

// An initial key a later version took away.
const gone = -1;

// The overlay of keys is merged into the index once it holds this many, and half as many as the index: so an
// update copies a bounded overlay, and a long run of updates costs the index once more over every few of them.
const mergedFrom = 4096;

/** A cell's parts, where a version holds them: each field's columns, and the cell's place in them. */
export interface CellAt {
    readonly columns: readonly PartColumns[];
    readonly offset: number;
}

/** One version of a computation's cells. */
export class ComputationCells implements CubeCells {
    readonly #initial: ReadonlyMap<number, number>;
    readonly #changed: ReadonlyMap<number, number>;
    readonly #baseline: readonly PartColumns[];
    readonly #baselineCount: number;
    readonly #pages: PagedArray<readonly PartColumns[]>;
    /** How many cells were ever numbered: the next cell's number. */
    readonly count: number;

    private constructor(initial: ReadonlyMap<number, number>, changed: ReadonlyMap<number, number>, baseline: readonly PartColumns[],
        baselineCount: number, pages: PagedArray<readonly PartColumns[]>, count: number) {
        this.#initial = initial;
        this.#changed = changed;
        this.#baseline = baseline;
        this.#baselineCount = baselineCount;
        this.#pages = pages;
        this.count = count;
    }

    /** A computation's first version: the first cube's cells, shared. */
    static of(cells: ReadonlyMap<number, number>, values: readonly PartColumns[]): ComputationCells {
        return new ComputationCells(cells, new Map(), values, cells.size, PagedArray.empty(), cells.size);
    }

    /** The cell a key names in this version, or `undefined` for none. */
    cellOf(key: number): number | undefined {
        const changed = this.#changed.get(key);
        if (changed !== undefined) return changed === gone ? undefined : changed;
        return this.#initial.get(key);
    }

    /** The cell a key names; it must name one. */
    mustCellOf(key: number): number {
        const cell = this.cellOf(key);
        if (cell === undefined) throw new Error(`Internal: no cell has the key ${key} (ADR-0153).`);
        return cell;
    }

    /** Where a cell's parts are in this version. */
    at(cell: number): CellAt {
        const page = this.#pages.get(Math.floor(cell / pageSize));
        return page === undefined ? { columns: this.#baseline, offset: cell } : { columns: page, offset: cell % pageSize };
    }

    read(row: number, column: number, source: number, aggregation: PivotAggregation): AggregateValue {
        const cell = this.cellOf(cellKey(row, column));
        if (cell === undefined) return emptyValue;
        const { columns, offset } = this.at(cell);
        return elementAt(columns, source).read(offset, aggregation);
    }

    /** A writer of the next version, which starts as this one. */
    writer(): CellsWriter {
        return new CellsWriter(this.#initial, this.#changed, this.#baseline, this.#baselineCount, this.#pages.writer(), this.count,
            (initial, changed, pages, count) => new ComputationCells(initial, changed, this.#baseline, this.#baselineCount, pages, count));
    }
}

type Publish = (initial: ReadonlyMap<number, number>, changed: ReadonlyMap<number, number>, pages: PagedArray<readonly PartColumns[]>,
    count: number) => ComputationCells;

/** The next version of the cells, written: a page copied the first time a cell of it is written, the overlay of
 * keys the first time a key is added or taken away. */
export class CellsWriter {
    readonly #initial: ReadonlyMap<number, number>;
    readonly #held: ReadonlyMap<number, number>;
    #changed: Map<number, number> | undefined;
    readonly #baseline: readonly PartColumns[];
    readonly #baselineCount: number;
    readonly #pages: PagedWriter<readonly PartColumns[]>;
    readonly #written = new Map<number, PartColumns[]>();
    readonly #publish: Publish;
    #count: number;

    constructor(initial: ReadonlyMap<number, number>, changed: ReadonlyMap<number, number>, baseline: readonly PartColumns[], baselineCount: number,
        pages: PagedWriter<readonly PartColumns[]>, count: number, publish: Publish) {
        this.#initial = initial;
        this.#held = changed;
        this.#baseline = baseline;
        this.#baselineCount = baselineCount;
        this.#pages = pages;
        this.#count = count;
        this.#publish = publish;
    }

    #overlay(): ReadonlyMap<number, number> {
        return this.#changed ?? this.#held;
    }

    #own(): Map<number, number> {
        this.#changed ??= new Map(this.#held);
        return this.#changed;
    }

    #cellOf(key: number): number | undefined {
        const changed = this.#overlay().get(key);
        if (changed !== undefined) return changed === gone ? undefined : changed;
        return this.#initial.get(key);
    }

    /** The cell a key names, numbered anew where it names none. */
    cell(key: number): number {
        const held = this.#cellOf(key);
        if (held !== undefined) return held;
        const cell = this.#count++;
        this.#own().set(key, cell);
        return cell;
    }

    /** Takes the key's cell away. */
    remove(key: number): void {
        if (this.#initial.has(key)) this.#own().set(key, gone);
        else if (this.#overlay().has(key)) this.#own().delete(key);
    }

    /** Where a cell's parts are, as written so far. */
    at(cell: number): CellAt {
        const page = this.#pages.get(Math.floor(cell / pageSize));
        return page === undefined ? { columns: this.#baseline, offset: cell } : { columns: page, offset: cell % pageSize };
    }

    // The page of a cell, this writer's own: copied from the version before, or from the first cube's cells.
    #write(cell: number): readonly PartColumns[] {
        const number = Math.floor(cell / pageSize);
        const own = this.#written.get(number);
        if (own !== undefined) return own;
        const held = this.#pages.get(number);
        const from = number * pageSize;
        const page = this.#baseline.map((baseline, v) => {
            const columns = new PartColumns(baseline.parts);
            for (let at = 0; at < pageSize; at++) {
                if (held !== undefined) columns.copyCell(elementAt(held, v), at);
                else if (from + at < this.#baselineCount) columns.copyCell(baseline, from + at);
                else columns.addCell();
            }
            return columns;
        });
        this.#written.set(number, page);
        this.#pages.set(number, page);
        return page;
    }

    /** Writes over a cell each field's parts at `at` of `values`. */
    put(cell: number, values: readonly PartColumns[], at: number): void {
        const page = this.#write(cell);
        page.forEach((columns, v) => {
            columns.setCell(cell % pageSize, elementAt(values, v), at);
        });
    }

    /**
     * Takes a leaf's parts before out of a total and puts its parts after in, where every part is a count or an
     * exact sum, which subtraction keeps exactly in any order (ADR-0153): `before` is `undefined` for a leaf that
     * is new, `after` for one emptied.
     */
    adjust(cell: number, before: CellAt | undefined, after: { readonly values: readonly PartColumns[]; readonly at: number } | undefined): void {
        const page = this.#write(cell);
        const offset = cell % pageSize;
        page.forEach((columns, v) => {
            const counts = elementAt(columns.counts, offset);
            const sums = columns.sums;
            if (after !== undefined) {
                const from = elementAt(after.values, v);
                const added = elementAt(from.counts, after.at);
                counts.values += added.values;
                counts.numbers += added.numbers;
                if (sums !== undefined) {
                    const sum = elementAt(sums, offset);
                    sum.exact = exactlyAdded(sum.exact, elementAt(defined(from.sums, 'the sums of a part kept by subtraction'), after.at).exact);
                }
            }
            if (before !== undefined) {
                const from = elementAt(before.columns, v);
                const taken = elementAt(from.counts, before.offset);
                counts.values -= taken.values;
                counts.numbers -= taken.numbers;
                if (sums !== undefined) {
                    const sum = elementAt(sums, offset);
                    sum.exact = exactlyAdded(sum.exact, negated(elementAt(defined(from.sums, 'the sums of a part kept by subtraction'), before.offset).exact));
                }
            }
        });
    }

    /** Empties a cell, to be merged again from its leaves. */
    reset(cell: number): void {
        for (const columns of this.#write(cell)) columns.resetCell(cell % pageSize);
    }

    /** Merges a leaf's cell into a total's. */
    merge(cell: number, leaf: number): void {
        const page = this.#write(cell);
        const { columns, offset } = this.at(leaf);
        page.forEach((into, v) => {
            into.merge(cell % pageSize, elementAt(columns, v), offset);
        });
    }

    /** Writes a cell's exact values in one form (ADR-0064). */
    finish(cell: number): void {
        const offset = cell % pageSize;
        for (const columns of this.#write(cell)) columns.canonicalize(offset, offset + 1);
    }

    /** The version written. */
    freeze(): ComputationCells {
        const pages = this.#pages.freeze();
        const changed = this.#overlay();
        if (changed.size < mergedFrom || changed.size * 2 < this.#initial.size) return this.#publish(this.#initial, changed, pages, this.#count);
        const merged = new Map(this.#initial);
        for (const [key, cell] of changed) {
            if (cell === gone) merged.delete(key);
            else merged.set(key, cell);
        }
        return this.#publish(merged, new Map(), pages, this.#count);
    }
}

function negated(value: SnapshotDecimal): SnapshotDecimal {
    return { mantissa: -value.mantissa, scale: value.scale };
}

// A sum the update keeps by addition: within the bound the computation checked, no decimal overflows.
function exactlyAdded(a: SnapshotDecimal, b: SnapshotDecimal): SnapshotDecimal {
    const added = decimalAdd(a, b);
    if (added === undefined) throw new Error('Internal: an exact total that subtraction keeps overflowed (ADR-0153).');
    return added;
}

