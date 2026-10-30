/**
 * A cell named by its position in the current order: the row's position in the filtered and sorted
 * sequence, from 0, and the visible column's index, from 0. Never by identity: the grid does not know
 * identities outside the Window (ADR-0011).
 * @public
 */
export interface CellPosition {
    /** The row's position in the filtered and sorted sequence, from 0. */
    readonly row: number;
    /** The visible column's index, from 0. */
    readonly column: number;
}

/**
 * The bounds a selection transition is valid against: the row count after filtering and the visible
 * column count. Passed into every transition rather than held with the selection, because both change
 * independently of it, and a held copy would go stale exactly when positions become dangerous
 * (ADR-0011).
 * @public
 */
export interface GridExtent {
    /** The row count after filtering. */
    readonly rowCount: number;
    /** The visible column count. */
    readonly columnCount: number;
}

/**
 * An arrow key's direction (ADR-0012).
 * @public
 */
export type GridDirection = 'up' | 'down' | 'left' | 'right';

/**
 * The order Enter and Tab walk a range in: Enter runs down each column, Tab across each row; both wrap
 * and return to the start (ADR-0012).
 */
export type CycleOrder = 'columnMajor' | 'rowMajor';

/** A frozen cell position. */
export function cell(row: number, column: number): CellPosition {
    return Object.freeze({ row, column });
}

/** Whether two positions name the same cell. */
export function sameCell(a: CellPosition, b: CellPosition): boolean {
    return a.row === b.row && a.column === b.column;
}

/** A cell as the messages name it: (row, column). */
export function cellText(position: CellPosition): string {
    return `(${position.row}, ${position.column})`;
}
