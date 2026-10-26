import { isAsciiDigit, isAsciiLetter, upperInvariant } from './dotnet';
import { FormatError } from './format-error';

/** Excel's extent: how many rows a Sheet has, 1,048,576. */
export const rowCount = 1_048_576;

/** Excel's extent: how many columns a Sheet has, 16,384 (`A` to `XFD`). */
export const columnCount = 16_384;

/**
 * Where a cell is on a Sheet: a zero-based row and column inside Excel's extent. Written in A1 form (`A1` is
 * row 0, column 0; `XFD1048576` is the last cell). An address is where a cell is; a Reference is how a
 * Formula names it (CONTEXT.md).
 */
export class CellAddress {
    readonly #row: number;
    readonly #column: number;

    /** An address; one outside the Sheet's extent is refused with a `RangeError`, never clamped. */
    constructor(row: number, column: number) {
        if (!Number.isInteger(row) || row < 0 || row >= rowCount) throw new RangeError(`A row is 0 to ${rowCount - 1}; ${row} is not.`);
        if (!Number.isInteger(column) || column < 0 || column >= columnCount) throw new RangeError(`A column is 0 to ${columnCount - 1}; ${column} is not.`);
        this.#row = row;
        this.#column = column;
    }

    /** The zero-based row, 0 to 1,048,575. */
    get row(): number {
        return this.#row;
    }

    /** The zero-based column, 0 to 16,383. */
    get column(): number {
        return this.#column;
    }

    /** A number that names the address alone, row-major: two addresses are equal exactly where their keys are. */
    get key(): number {
        return this.#row * columnCount + this.#column;
    }

    /** The address a key names. */
    static fromKey(key: number): CellAddress {
        return new CellAddress(Math.floor(key / columnCount), key % columnCount);
    }

    /** Whether the two name the same cell. */
    equals(other: CellAddress): boolean {
        return this.#row === other.#row && this.#column === other.#column;
    }

    /** Row-major order: by row, then by column. */
    compareTo(other: CellAddress): number {
        return this.#row !== other.#row ? this.#row - other.#row : this.#column - other.#column;
    }

    /** Parses an address in A1 form, such as `B7` or `xfd1048576`; anything else is refused with a `FormatError`. */
    static parse(text: string): CellAddress {
        const address = CellAddress.tryParse(text);
        if (address === undefined) throw new FormatError(`'${text}' is not a cell address from A1 to XFD1048576.`);
        return address;
    }

    /**
     * Parses an address in A1 form, or `undefined`. Letters may be in either case; `$` markers are not part
     * of an address (they belong to a Reference) and are refused here.
     */
    static tryParse(text: string): CellAddress | undefined {
        if (text.length === 0) return undefined;
        let i = 0;
        let column = 0;
        while (i < text.length && isAsciiLetter(text.charCodeAt(i))) {
            if (i === 3) return undefined;
            column = column * 26 + (upperInvariant(text.charCodeAt(i)) - 0x41 + 1);
            i++;
        }
        if (i === 0 || i === text.length) return undefined;
        if (text[i] === '0') return undefined;
        let row = 0;
        for (; i < text.length; i++) {
            if (!isAsciiDigit(text.charCodeAt(i)) || row > rowCount) return undefined;
            row = row * 10 + (text.charCodeAt(i) - 0x30);
        }
        if (column > columnCount || row > rowCount) return undefined;
        return new CellAddress(row - 1, column - 1);
    }

    /** The column's letters: 0 is `A`, 25 is `Z`, 26 is `AA`, 16,383 is `XFD`. */
    static columnName(column: number): string {
        if (!Number.isInteger(column) || column < 0 || column >= columnCount) throw new RangeError(`A column is 0 to ${columnCount - 1}; ${column} is not.`);
        let name = '';
        let n = column + 1;
        while (n > 0) {
            n--;
            name = String.fromCharCode(0x41 + (n % 26)) + name;
            n = Math.floor(n / 26);
        }
        return name;
    }

    /** Parses column letters (`A` to `XFD`, either case) to a zero-based column, or `undefined`. */
    static tryParseColumn(letters: string): number | undefined {
        if (letters.length === 0 || letters.length > 3) return undefined;
        let n = 0;
        for (let i = 0; i < letters.length; i++) {
            const c = letters.charCodeAt(i);
            if (!isAsciiLetter(c)) return undefined;
            n = n * 26 + (upperInvariant(c) - 0x41 + 1);
        }
        if (n > columnCount) return undefined;
        return n - 1;
    }

    /** The address in A1 form, such as `C12`. */
    toString(): string {
        return CellAddress.columnName(this.#column) + String(this.#row + 1);
    }
}
