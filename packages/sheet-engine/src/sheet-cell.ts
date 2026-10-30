// A cell of one Sheet of a Workbook (C#'s SheetCell in Workbook.cs; ADR-0331): what a recalculation across the
// Workbook keys its work by. A C# record struct compares by value; here a set and a map key it by text.
import type { CellAddress } from './cell-address';
import type { Sheet } from './sheet';

/** A cell of one Sheet of a Workbook. @internal */
export class SheetCell {
    readonly sheet: Sheet;

    readonly address: CellAddress;

    constructor(sheet: Sheet, address: CellAddress) {
        this.sheet = sheet;
        this.address = address;
    }

    /** A key two SheetCells naming the same cell share. */
    get key(): string {
        return `${String(this.sheet.id)}:${String(this.address.key)}`;
    }
}

/** A set of SheetCells, compared by the cell they name, in the order first added. @internal */
export class SheetCellSet implements Iterable<SheetCell> {
    readonly #items = new Map<string, SheetCell>();

    constructor(cells: Iterable<SheetCell> = []) {
        for (const cell of cells) this.add(cell);
    }

    /** Adds `cell`; whether it was not there yet. */
    add(cell: SheetCell): boolean {
        const key = cell.key;
        if (this.#items.has(key)) return false;
        this.#items.set(key, cell);
        return true;
    }

    addAll(cells: Iterable<SheetCell>): void {
        for (const cell of cells) this.add(cell);
    }

    has(cell: SheetCell): boolean {
        return this.#items.has(cell.key);
    }

    delete(cell: SheetCell): boolean {
        return this.#items.delete(cell.key);
    }

    get size(): number {
        return this.#items.size;
    }

    [Symbol.iterator](): Iterator<SheetCell> {
        return this.#items.values();
    }
}

/** A map keyed by SheetCells, compared by the cell they name, in the order first set. @internal */
export class SheetCellMap<V> implements Iterable<[SheetCell, V]> {
    readonly #items = new Map<string, [SheetCell, V]>();

    get(cell: SheetCell): V | undefined {
        return this.#items.get(cell.key)?.[1];
    }

    has(cell: SheetCell): boolean {
        return this.#items.has(cell.key);
    }

    set(cell: SheetCell, value: V): void {
        const key = cell.key;
        const held = this.#items.get(key);
        if (held === undefined) this.#items.set(key, [cell, value]);
        else held[1] = value;
    }

    delete(cell: SheetCell): boolean {
        return this.#items.delete(cell.key);
    }

    get size(): number {
        return this.#items.size;
    }

    [Symbol.iterator](): Iterator<[SheetCell, V]> {
        return this.#items.values();
    }
}
