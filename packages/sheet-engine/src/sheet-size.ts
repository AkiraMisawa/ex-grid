import { CellAddress, columnCount, rowCount } from './cell-address';
import { CellRange } from './cell-range';

/**
 * How many rows and columns the Sheets of a Workbook hold (ADR-0450): Excel's 1,048,576 × 16,384 unless a Consumer
 * gives fewer. It bounds where a cell may hold an Entry and what the user reaches, not the address space: a Reference
 * past it is still a Reference, to a cell that is empty and can hold nothing.
 */
export class SheetSize {
    /** Excel's full size, 1,048,576 rows by 16,384 columns (`A1` to `XFD1048576`): what a Sheet holds unless given another. */
    static readonly excel = new SheetSize(rowCount, columnCount);

    /** How many rows, from 1 to 1,048,576. */
    readonly rows: number;

    /** How many columns, from 1 to 16,384. */
    readonly columns: number;

    /** A size of `rows` by `columns`; one of no rows or no columns, or larger than Excel's, is refused with a `RangeError`. */
    constructor(rows: number, columns: number) {
        if (!Number.isInteger(rows) || rows < 1 || rows > rowCount) {
            throw new RangeError(`A Sheet holds from 1 to 1,048,576 rows; ${String(rows)} is not that.`);
        }
        if (!Number.isInteger(columns) || columns < 1 || columns > columnCount) {
            throw new RangeError(`A Sheet holds from 1 to 16,384 columns; ${String(columns)} is not that.`);
        }
        this.rows = rows;
        this.columns = columns;
    }

    /** The last cell inside the size: `XFD1048576` for Excel's, `J10` for ten by ten. */
    get lastCell(): CellAddress {
        return new CellAddress(this.rows - 1, this.columns - 1);
    }

    /** Whether `address` lies inside the size. */
    contains(address: CellAddress): boolean {
        return address.row < this.rows && address.column < this.columns;
    }

    /** Whether a block of `rows` × `columns` with its top-left cell at `origin` lies wholly inside the size. */
    holds(origin: CellAddress, rows: number, columns: number): boolean {
        return origin.row + rows <= this.rows && origin.column + columns <= this.columns;
    }

    /**
     * `range` as it stands inside the size, or `undefined` when it runs past it. Whole columns and whole rows are
     * Excel's lines, inside when their columns or rows are, and are answered as long as the size's: `B:B` of ten rows
     * is `B1:B10`.
     */
    fit(range: CellRange): CellRange | undefined {
        const last = new CellAddress(range.isWholeColumns ? this.rows - 1 : range.last.row, range.isWholeRows ? this.columns - 1 : range.last.column);
        return this.contains(last) && this.contains(range.first) ? new CellRange(range.first, last) : undefined;
    }

    /** Whether the two are the same size. */
    equals(other: SheetSize): boolean {
        return this.rows === other.rows && this.columns === other.columns;
    }
}
