// What copying a Formula does to its References (C#'s ReferenceShift.cs; ADR-0048): each relative part moves by
// the distance copied, each absolute part (`$`) stays. A Reference moved off the Sheet names no cells, and Excel
// writes it `#REF!`.
import { columnCount, rowCount } from '../cell-address';
import type { Reference } from './reference';

/** The Reference moved `rows` down and `columns` across; `undefined` where it is moved off the Sheet. */
export function shiftReference(reference: Reference, rows: number, columns: number): Reference | undefined {
    let r1 = reference.row1Absolute ? reference.row1 : reference.row1 + rows;
    let r2 = reference.row2Absolute ? reference.row2 : reference.row2 + rows;
    let c1 = reference.column1Absolute ? reference.column1 : reference.column1 + columns;
    let c2 = reference.column2Absolute ? reference.column2 : reference.column2 + columns;
    // A whole column spans every row whatever is copied, and a whole row every column.
    if (reference.shape === 'columns') [r1, r2] = [reference.row1, reference.row2];
    if (reference.shape === 'rows') [c1, c2] = [reference.column1, reference.column2];
    if (!inside(r1, rowCount) || !inside(r2, rowCount) || !inside(c1, columnCount) || !inside(c2, columnCount)) return undefined;

    let r1Absolute = reference.row1Absolute;
    let r2Absolute = reference.row2Absolute;
    let c1Absolute = reference.column1Absolute;
    let c2Absolute = reference.column2Absolute;
    // A$1:A3 copied down past row 1 is written from its top: A$1 stays first only while it is on top.
    if (r2 < r1) [r1, r1Absolute, r2, r2Absolute] = [r2, r2Absolute, r1, r1Absolute];
    if (c2 < c1) [c1, c1Absolute, c2, c2Absolute] = [c2, c2Absolute, c1, c1Absolute];
    return reference.with({
        row1: r1, row2: r2, column1: c1, column2: c2,
        row1Absolute: r1Absolute, row2Absolute: r2Absolute, column1Absolute: c1Absolute, column2Absolute: c2Absolute,
    });
}

function inside(position: number, size: number): boolean {
    return position >= 0 && position < size;
}
