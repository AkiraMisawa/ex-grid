import { CellAddress, columnCount, rowCount } from './cell-address';
import { isAsciiDigit } from './dotnet';
import { FormatError } from './format-error';

/**
 * A rectangle of cells on a Sheet, inclusive at both corners, held from its top-left corner (`first`) to its
 * bottom-right one (`last`) whichever corners it was made from. Written `A1` for one cell and `A1:B2`
 * otherwise.
 */
export class CellRange {
    readonly #first: CellAddress;
    readonly #last: CellAddress;

    /** The rectangle spanned by two corners, in either order; one corner alone is that one cell. */
    constructor(corner1: CellAddress, corner2: CellAddress = corner1) {
        this.#first = new CellAddress(Math.min(corner1.row, corner2.row), Math.min(corner1.column, corner2.column));
        this.#last = new CellAddress(Math.max(corner1.row, corner2.row), Math.max(corner1.column, corner2.column));
    }

    /** The top-left cell. */
    get first(): CellAddress {
        return this.#first;
    }

    /** The bottom-right cell. */
    get last(): CellAddress {
        return this.#last;
    }

    /** How many rows it spans. */
    get rowCount(): number {
        return this.#last.row - this.#first.row + 1;
    }

    /** How many columns it spans. */
    get columnCount(): number {
        return this.#last.column - this.#first.column + 1;
    }

    /** How many cells it holds. */
    get cellCount(): number {
        return this.rowCount * this.columnCount;
    }

    /** Whether `address` lies inside it. */
    contains(address: CellAddress): boolean {
        return address.row >= this.#first.row && address.row <= this.#last.row
            && address.column >= this.#first.column && address.column <= this.#last.column;
    }

    /** Whether it spans every row: whole columns, such as `B:D` (ADR-0047). */
    get isWholeColumns(): boolean {
        return this.#first.row === 0 && this.#last.row === rowCount - 1;
    }

    /** Whether it spans every column: whole rows, such as `2:4` (ADR-0047). */
    get isWholeRows(): boolean {
        return this.#first.column === 0 && this.#last.column === columnCount - 1;
    }

    /** Whether the two are the same rectangle. */
    equals(other: CellRange): boolean {
        return this.#first.equals(other.#first) && this.#last.equals(other.#last);
    }

    /** Whole columns `first` to `last` (0 is `A`), as `A:A` names one. */
    static wholeColumns(first: number, last: number): CellRange {
        return new CellRange(new CellAddress(0, first), new CellAddress(rowCount - 1, last));
    }

    /** Whole rows `first` to `last` (0 is row `1`), as `1:1` names one. */
    static wholeRows(first: number, last: number): CellRange {
        return new CellRange(new CellAddress(first, 0), new CellAddress(last, columnCount - 1));
    }

    /** Its cells in row-major order. */
    *cells(): IterableIterator<CellAddress> {
        for (let row = this.#first.row; row <= this.#last.row; row++) {
            for (let column = this.#first.column; column <= this.#last.column; column++) yield new CellAddress(row, column);
        }
    }

    /** Parses `B7`, `B7:C9` (corners in either order, letters in either case), whole columns `B:D` or whole rows `2:4`; no `$`. Anything else is a `FormatError`. */
    static parse(text: string): CellRange {
        const range = CellRange.tryParse(text);
        if (range === undefined) throw new FormatError(`'${text}' is not a cell range such as A1 or A1:B2.`);
        return range;
    }

    /** Parses as `parse` does, or `undefined`. */
    static tryParse(text: string): CellRange | undefined {
        const colon = text.indexOf(':');
        if (colon < 0) {
            const single = CellAddress.tryParse(text);
            return single === undefined ? undefined : new CellRange(single);
        }
        const left = text.slice(0, colon);
        const right = text.slice(colon + 1);
        const c1 = CellAddress.tryParseColumn(left);
        const c2 = CellAddress.tryParseColumn(right);
        if (c1 !== undefined && c2 !== undefined) return CellRange.wholeColumns(Math.min(c1, c2), Math.max(c1, c2));
        const r1 = parseRow(left);
        const r2 = parseRow(right);
        if (r1 !== undefined && r2 !== undefined) return CellRange.wholeRows(Math.min(r1, r2), Math.max(r1, r2));
        const a = CellAddress.tryParse(left);
        const b = CellAddress.tryParse(right);
        if (a === undefined || b === undefined) return undefined;
        return new CellRange(a, b);
    }

    /** `A1` for one cell, `A1:B2` otherwise. */
    toString(): string {
        return this.#first.equals(this.#last) ? this.#first.toString() : `${this.#first.toString()}:${this.#last.toString()}`;
    }
}

function parseRow(text: string): number | undefined {
    if (text.length === 0 || text.length > 7 || text[0] === '0') return undefined;
    for (let i = 0; i < text.length; i++) {
        if (!isAsciiDigit(text.charCodeAt(i))) return undefined;
    }
    const n = Number(text);
    if (n > rowCount) return undefined;
    return n - 1;
}
