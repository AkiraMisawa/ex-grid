// A grid pointed at from outside (ADR-0058), the counterpart of C#'s GridPointedAt and the records it hands over:
// a Consumer's declaration that a Formula edited elsewhere on the page points at this grid's cells, what the grid
// paints meanwhile, and how it hands over a press and answers a step. What a press means is the Consumer's (a
// Pointing Scope's); the grid knows no Formula.

import type { GridDirection } from '../selection/cell';
import type { GridOutlinedColumn } from './reference-outlines';

/**
 * What a press handed over by a grid pointed at from outside landed on (ADR-0058). The grid names the shape and
 * decides nothing about it:
 *
 * - `cell`: one cell, pressed without Shift; the press names its row and column.
 * - `columnHeader`: a column's own header, pressed without Shift; the press names the column.
 * - `severalCells`: a Shift+press on a cell, a press dragged onto another cell, or a press on a Row Heading or
 *   on the corner.
 * - `severalColumns`: a Shift+press on a column's header, or one dragged onto another column.
 * - `headerGroup`: a Header Group's rectangle (ADR-0032).
 */
export type GridPointedPressKind = 'cell' | 'columnHeader' | 'severalCells' | 'severalColumns' | 'headerGroup';

/**
 * What a step from a cell or a column of a grid pointed at from outside reached (ADR-0058; DC-55):
 *
 * - `cell`: a cell, named by the step's row and column; down from a column, the column's first row.
 * - `edge`: nothing that way; nothing moves.
 * - `rowNotArrived`: the row one step up or down, or a column's first row, has not arrived.
 * - `notHeld`: the grid does not hold the cell or the column to step from, is not pointed at, or takes no
 *   declaration.
 * - `column`: a whole column, as a step left or right from a column reaches.
 */
export type GridPointedStepKind = 'cell' | 'edge' | 'rowNotArrived' | 'notHeld' | 'column';

/** An arrow key in the edit while what a press outside the grid wrote stands there (ADR-0058, "The keyboard"): its
 * direction, and whether Shift (`extends`) or the Primary Modifier (`toEdge`) was held. */
export interface GridPointArrow {
    /** Which way the arrow points. */
    readonly direction: GridDirection;
    /** Shift was held: refused as a range is. */
    readonly extends?: boolean;
    /** The Primary Modifier was held: refused, since where the data ends is not known to the grid. */
    readonly toEdge?: boolean;
}

/** A press a grid pointed at from outside hands over (ADR-0058). */
export interface GridPointedPress<TRow> {
    /** What the press landed on. */
    readonly kind: GridPointedPressKind;
    /** The pressed row, for a cell; `undefined` for a row that has not arrived, and for other shapes. */
    readonly row?: TRow | undefined;
    /** The pressed grid column's name, for a cell or a column header. */
    readonly column?: string;
    /** Whether this hands over a drag that began with a press already handed over. */
    readonly dragged?: boolean;
}

/** Where a step from a cell or a column of a grid pointed at from outside landed (ADR-0058; DC-55). */
export interface GridPointedStep<TRow> {
    /** What the step reached. */
    readonly kind: GridPointedStepKind;
    /** The row reached, for `cell`; `undefined` where it has not arrived. */
    readonly row?: TRow | undefined;
    /** The grid column reached, for `cell`, `column` and `rowNotArrived`. */
    readonly column?: string;
}

/** The dashes over the cell or the column a press wrote for (ADR-0058, "What is drawn"): over `column`'s cell in
 * the row `row` answers true for, or down the column's body when `row` is `undefined`. */
export interface GridPointDashes<TRow> {
    /** Which painted row is the dashed one; `undefined` for dashes down the column. */
    readonly row: ((row: TRow) => boolean) | undefined;
    /** The grid column's name. */
    readonly column: string;
}

/** What a grid that takes a {@link GridPointedAt} answers for it: a step from a cell or a column, and scrolling
 * a cell or a column into view (DC-55). */
export interface PointedAtGrid<TRow> {
    /** The cell one step from the cell in the row `isRow` finds, in `column`, in the grid's current order;
     * left and right pass over the grid columns `isColumn` refuses. */
    step(isRow: (row: TRow) => boolean, column: string, direction: GridDirection, isColumn: (column: string) => boolean): Promise<GridPointedStep<TRow>>;
    /** What one step from `column`, as a column, reaches: its first row down, or another column left or right. */
    stepFromColumn(column: string, direction: GridDirection, isColumn: (column: string) => boolean): Promise<GridPointedStep<TRow>>;
    /** Scrolls the cell in the row `isRow` finds, in `column`, into view; false when the grid holds no such row. */
    reveal(isRow: (row: TRow) => boolean, column: string): Promise<boolean>;
    /** Scrolls `column` into view across only; false when the grid shows no such column. */
    revealColumn(column: string): Promise<boolean>;
}

/**
 * A grid's declaration that it is pointed at from outside (ADR-0058), the counterpart of C#'s
 * `GridPointedAt<TRow>`: what the Scope tells the grid to paint, and how the grid hands over a press. The grid
 * that takes it attaches itself with {@link GridPointedAt.attach} to answer steps, and hears each change through
 * {@link GridPointedAt.onChanged}.
 */
export class GridPointedAt<TRow> {
    readonly #listeners = new Set<() => void>();
    #isPointedAt = false;
    #pointingRootId: string | undefined;
    #dashes: GridPointDashes<TRow> | undefined;
    #outlinedColumns: readonly GridOutlinedColumn[] | undefined;
    #grid: PointedAtGrid<TRow> | undefined;

    /** Where the grid hands each press while it is pointed at. */
    readonly onPress: (press: GridPointedPress<TRow>) => Promise<void>;

    /** A declaration whose presses go to `onPress`. */
    constructor(onPress: (press: GridPointedPress<TRow>) => Promise<void>) {
        this.onPress = onPress;
    }

    /** Whether the grid is pointed at now: a press on it moves neither focus nor its Selection, and is handed over. */
    get isPointedAt(): boolean {
        return this.#isPointedAt;
    }

    set isPointedAt(value: boolean) {
        if (this.#isPointedAt === value) return;
        this.#isPointedAt = value;
        this.#changed();
    }

    /** The id of the root of the Sheet that points, which the grid names while it is pointed at (ADR-0058, "On a
     * circuit"); `undefined` before one has pointed. */
    get pointingRootId(): string | undefined {
        return this.#pointingRootId;
    }

    set pointingRootId(value: string | undefined) {
        if (this.#pointingRootId === value) return;
        this.#pointingRootId = value;
        this.#changed();
    }

    /** The dashes over the cell or the column a press wrote for, or `undefined`. */
    get dashes(): GridPointDashes<TRow> | undefined {
        return this.#dashes;
    }

    set dashes(value: GridPointDashes<TRow> | undefined) {
        if (this.#dashes === value) return;
        this.#dashes = value;
        this.#changed();
    }

    /** The grid columns outlined in Reference colours while a Formula reads them, or `undefined`. A new list is
     * the change signal: the same list set again changes nothing. */
    get outlinedColumns(): readonly GridOutlinedColumn[] | undefined {
        return this.#outlinedColumns;
    }

    set outlinedColumns(value: readonly GridOutlinedColumn[] | undefined) {
        if (this.#outlinedColumns === value) return;
        this.#outlinedColumns = value;
        this.#changed();
    }

    /** Calls `listener` after each change above; the function returned stops it. */
    onChanged(listener: () => void): () => void {
        this.#listeners.add(listener);
        return () => {
            this.#listeners.delete(listener);
        };
    }

    /** The grid that takes this declaration attaches itself to answer steps; the function returned detaches it. */
    attach(grid: PointedAtGrid<TRow>): () => void {
        this.#grid = grid;
        return () => {
            if (this.#grid === grid) this.#grid = undefined;
        };
    }

    /** Asks the grid for the cell one step from the one in the row `isRow` finds (DC-55); `notHeld` with no grid. */
    async step(isRow: (row: TRow) => boolean, column: string, direction: GridDirection, isColumn: (column: string) => boolean): Promise<GridPointedStep<TRow>> {
        named(column);
        return this.#grid === undefined ? { kind: 'notHeld' } : this.#grid.step(isRow, column, direction, isColumn);
    }

    /** Asks the grid what one step from `column`, as a column, reaches (DC-55); `notHeld` with no grid. */
    async stepFromColumn(column: string, direction: GridDirection, isColumn: (column: string) => boolean): Promise<GridPointedStep<TRow>> {
        named(column);
        return this.#grid === undefined ? { kind: 'notHeld' } : this.#grid.stepFromColumn(column, direction, isColumn);
    }

    /** Asks the grid to scroll the cell into view; false with no grid. */
    async reveal(isRow: (row: TRow) => boolean, column: string): Promise<boolean> {
        named(column);
        return this.#grid === undefined ? false : this.#grid.reveal(isRow, column);
    }

    /** Asks the grid to scroll the column into view across only; false with no grid. */
    async revealColumn(column: string): Promise<boolean> {
        named(column);
        return this.#grid === undefined ? false : this.#grid.revealColumn(column);
    }

    #changed(): void {
        for (const listener of this.#listeners) listener();
    }
}

function named(column: string): void {
    if (column.length === 0) throw new RangeError('A grid column is named (ADR-0058).');
}
