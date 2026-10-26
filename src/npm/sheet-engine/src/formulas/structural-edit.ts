// One insertion or deletion of whole rows or columns (C#'s StructuralEdit in ReferenceRewriter.cs; ADR-0046):
// rows and columns are places, so what lies beyond the edit moves, and every Reference is rewritten to keep
// naming the same cells.
import { CellAddress, columnCount, rowCount } from '../cell-address';
import type { Reference } from './reference';

/** Which of a Sheet's two axes an insertion or deletion runs along. */
export type SheetAxis = 'rows' | 'columns';

/** What a structural edit asks of the Sheet it rewrites: whether a Reference names a cell of that Sheet. */
export interface LocalReferences {
    isLocal(reference: Reference): boolean;
}

/** One insertion or deletion of whole rows or columns. */
export class StructuralEdit {
    readonly axis: SheetAxis;
    readonly start: number;
    readonly count: number;
    readonly isInsert: boolean;

    constructor(axis: SheetAxis, start: number, count: number, isInsert: boolean) {
        this.axis = axis;
        this.start = start;
        this.count = count;
        this.isInsert = isInsert;
    }

    get size(): number {
        return this.axis === 'rows' ? rowCount : columnCount;
    }

    get #max(): number {
        return this.size - 1;
    }

    get #end(): number {
        return this.start + this.count - 1;
    }

    /** The edit that undoes this one. */
    get inverse(): StructuralEdit {
        return new StructuralEdit(this.axis, this.start, this.count, !this.isInsert);
    }

    equals(other: StructuralEdit): boolean {
        return this.axis === other.axis && this.start === other.start && this.count === other.count && this.isInsert === other.isInsert;
    }

    describe(): string {
        const noun = this.axis === 'rows' ? 'row' : 'column';
        const at = this.axis === 'rows' ? String(this.start + 1) : CellAddress.columnName(this.start);
        return `${this.isInsert ? 'inserting' : 'deleting'} ${String(this.count)} ${noun}${this.count === 1 ? '' : 's'} at ${at}`;
    }

    /** Throws a `RangeError` for a start or count outside the Sheet. */
    validate(): void {
        const noun = this.axis === 'rows' ? 'row' : 'column';
        if (!Number.isInteger(this.start) || this.start < 0 || this.start >= this.size) throw new RangeError(`A ${noun} is 0 to ${String(this.#max)}.`);
        if (!Number.isInteger(this.count) || this.count < 1 || this.count > this.size - this.start) {
            throw new RangeError(`The count is 1 to ${String(this.size - this.start)} here.`);
        }
    }

    /** Where a cell goes: its new address, or `undefined` when it is deleted or pushed off the edge. */
    move(address: CellAddress): CellAddress | undefined {
        const p = this.axis === 'rows' ? address.row : address.column;
        let moved: number;
        if (this.isInsert) {
            if (p < this.start) return address;
            moved = p + this.count;
            if (moved > this.#max) return undefined;
        } else {
            if (p < this.start) return address;
            if (p <= this.#end) return undefined;
            moved = p - this.count;
        }
        return this.axis === 'rows' ? new CellAddress(moved, address.column) : new CellAddress(address.row, moved);
    }

    /**
     * A Reference rewritten as Excel rewrites it: an insertion at or before a range's first cell moves it, one
     * inside it grows it; a deletion shrinks it, moves it, or — when it takes every cell it named — makes it
     * `#REF!` (`undefined`). A Reference that spans the whole axis (`A:A` for rows) is untouched, as is one
     * qualified with another Sheet's name (ADR-0046); one qualified with the Sheet's own name is rewritten like
     * any other. An insertion that pushes the cells a Reference names off the edge does not stop the insertion,
     * as it does not in Excel (observed, verification/2026-09-27-windows-excel): a range pushed partly off is cut
     * at the edge (`A2:A1048576`, a row inserted at 1, is `A3:A1048576`), and a Reference pushed wholly off is
     * `#REF!`.
     */
    map(reference: Reference, sheet: LocalReferences): Reference | undefined {
        if (!sheet.isLocal(reference)) return reference;
        const rows = this.axis === 'rows';
        if (rows && reference.shape === 'columns') return reference;
        if (!rows && reference.shape === 'rows') return reference;
        const p1 = rows ? reference.row1 : reference.column1;
        const p2 = rows ? reference.row2 : reference.column2;
        // A:A (A1:A1048576 is written so) spans the whole axis; an edit along it leaves it alone.
        if (reference.shape !== 'cell' && p1 === 0 && p2 === this.#max) return reference;

        let n1: number;
        let n2: number;
        if (this.isInsert) {
            if (p1 >= this.start) {
                n1 = p1 + this.count;
                n2 = p2 + this.count;
            } else if (p2 >= this.start) {
                n1 = p1;
                n2 = p2 + this.count;
            } else {
                return reference;
            }
            if (n1 > this.#max) return undefined;
            if (n2 > this.#max) n2 = this.#max;
        } else {
            if (p2 < this.start) return reference;
            if (p1 >= this.start && p2 <= this.#end) return undefined;
            n1 = p1 < this.start ? p1 : p1 > this.#end ? p1 - this.count : this.start;
            n2 = p2 > this.#end ? p2 - this.count : this.start - 1;
        }
        return rows ? reference.with({ row1: n1, row2: n2 }) : reference.with({ column1: n1, column2: n2 });
    }

    /** Whether a Reference covers any place at or beyond the edit along its axis. */
    reaches(reference: Reference, sheet: LocalReferences): boolean {
        return sheet.isLocal(reference) && (this.axis === 'rows' ? reference.area.row2 : reference.area.column2) >= this.start;
    }
}
