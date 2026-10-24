import { cell, cellText, sameCell, type CellPosition, type CycleOrder, type GridDirection, type GridExtent } from './cell';
import {
    bottomRow, fromCorners, rangeCellCount, rangeContains, rightColumn, sameRange, selectionRange, spansEveryRow,
    subtractArea, type SelectionRange,
} from './range';

/**
 * The selection as a Consumer reads it: a list of rectangles in position space, one Focus (Excel's
 * active cell, which typing enters and the Name Box names) and the range that holds it (ADR-0011,
 * ADR-0012, ADR-0052). It never changes; each gesture makes a new one.
 *
 * Its positions are positions in the current order, so it is dropped, never remapped, when the order
 * or the visible columns change (ADR-0011).
 * @public
 */
export interface GridSelection {
    /**
     * The rectangles in the order they were made, which is also the order Enter and Tab cycle through
     * them (ADR-0012) and the order they are painted in (ADR-0008). A take-out puts a range's fragments
     * where the range stood, bottom to top, as Excel's `Selection.Address` lists them (ADR-0052).
     */
    readonly ranges: readonly SelectionRange[];
    /** Whether nothing is selected. The Focus, the Extent and the Focus range are then undefined. */
    readonly isEmpty: boolean;
    /**
     * Excel's active cell (ADR-0052): the one cell typing enters, the Cell Editor opens on, the Name Box
     * names and `aria-activedescendant` points at. It stays where it is while a range is extended, and
     * Enter and Tab move it inside the selection without changing the selection. Always inside the
     * Focus range; undefined when nothing is selected.
     */
    readonly focus: CellPosition | undefined;
    /**
     * The end of the Focus range that moves when it is extended, by Shift+arrow, Shift+click,
     * Ctrl+Shift+arrow or a drag, and that the grid keeps in view while it is (ADR-0052). On each axis it
     * is the edge opposite the Focus; where the Focus is on neither edge of an axis, it is the Focus's
     * own row or column, and an extension along that axis changes nothing. Undefined when nothing is
     * selected.
     */
    readonly extent: CellPosition | undefined;
    /** The range holding the Focus, the one an extension, Ctrl+Space, Shift+Space and Ctrl+. act on
     * (ADR-0052). Undefined when nothing is selected. */
    readonly focusRange: SelectionRange | undefined;
    /**
     * The selected-cell count for the status display: the sum of the rectangles' areas, so it needs no
     * data (ADR-0014). Overlapping ranges count twice, as Excel's status bar counts them.
     */
    readonly cellCount: number;
    /** Whether the cell lies in any range. */
    contains(position: CellPosition): boolean;
    /** Whether every cell of the column is selected, whichever ranges hold them: the column is wholly
     * selected, and a Ctrl+click on its Heading takes it out (ADR-0050, item 1). */
    coversColumn(column: number, extent: GridExtent): boolean;
    /** Whether every cell of the row is selected, whichever ranges hold them (ADR-0050, item 1). */
    coversRow(row: number, extent: GridExtent): boolean;
    /**
     * The columns covered by a range that spans every row, ascending, each once: what resizing several
     * whole columns at once acts on (ADR-0016). A range short of any row selects cells, not columns.
     */
    wholeColumns(extent: GridExtent): readonly number[];
    /** Whether the other selection was made by the same gestures: the same ranges in the same order, the
     * same Focus, and the same range holding it (ADR-0003). */
    equals(other: GridSelection): boolean;
}

/**
 * The selection and the transitions that make it (ADR-0011, ADR-0012, ADR-0052). Every gesture is a
 * pure transition returning a new value, and each takes the current extent, because the selection never
 * holds data-dependent bounds.
 *
 * Extending a range moves its Extent, the end opposite the Focus; the Focus stays where it is. Which
 * range holds the Focus is stored, never derived from geometry: ranges can overlap, and inferring
 * membership from coordinates would pick an arbitrary one of them.
 *
 * Dropping the selection is the holder's act: when the Row Sequence Version or the visible columns
 * change, the holder takes `Selection.empty` (ADR-0011). A transition applied to a selection that no
 * longer fits the extent throws: that is a missed drop, and positions would quietly name other cells.
 *
 * A drag is not a transition of its own: the press is a click and each move a Shift+click, and a drag
 * across Headings maps the same way onto the whole-column and whole-row transitions (ADR-0050, item 1).
 */
export class Selection implements GridSelection {
    /** Nothing selected: no range, and no Focus or Extent. */
    static readonly empty = new Selection([], cell(0, 0), 0, []);

    readonly ranges: readonly SelectionRange[];
    readonly #focus: CellPosition;
    /** The index of the range holding the Focus. Meaningless when empty. */
    readonly #focusRangeIndex: number;
    /**
     * For each range, which range the user made it as: a number that rises with the order of making. The
     * fragments a take-out leaves share the number of the range they were cut from, which is what "the
     * range made last" means after a take-out (ADR-0052, "What the third run settled").
     */
    readonly #origins: readonly number[];

    private constructor(
        ranges: readonly SelectionRange[], focus: CellPosition, focusRangeIndex: number, origins?: readonly number[]) {
        if (ranges.length > 0) {
            const holder = ranges[focusRangeIndex];
            if (holder === undefined || !rangeContains(holder, focus)) {
                throw new Error('Internal: the Focus range index must name a range containing the Focus (ADR-0052).');
            }
        }
        const made = origins ?? (ranges.length === 0 ? [] : ranges.length === 1 ? [0] : undefined);
        if (made === undefined) {
            throw new Error('Internal: several ranges need their origins (ADR-0052).');
        }
        if (made.length !== ranges.length) {
            throw new Error('Internal: every range has one origin (ADR-0052).');
        }
        this.ranges = Object.freeze([...ranges]);
        this.#focus = focus;
        this.#focusRangeIndex = focusRangeIndex;
        this.#origins = Object.freeze([...made]);
    }

    get isEmpty(): boolean {
        return this.ranges.length === 0;
    }

    get focus(): CellPosition | undefined {
        return this.isEmpty ? undefined : this.#focus;
    }

    get extent(): CellPosition | undefined {
        return this.isEmpty ? undefined : extentOf(this.#heldRange, this.#focus);
    }

    get focusRange(): SelectionRange | undefined {
        return this.isEmpty ? undefined : this.#heldRange;
    }

    get #heldRange(): SelectionRange {
        return this.ranges[this.#focusRangeIndex]!;
    }

    /**
     * The top-left cell of the range made last: where one value of plain text is pasted alone over a
     * selection of more than one cell (ADR-0014, amended 2026-09-29). When a take-out has cut that range
     * into fragments, it is the first cell, by rows, of the fragments, as a take-out's own Focus is
     * (ADR-0052). Undefined when nothing is selected.
     */
    get topLeftOfRangeMadeLast(): CellPosition | undefined {
        if (this.isEmpty) {
            return undefined;
        }
        const last = Math.max(...this.#origins);
        let first: CellPosition | undefined;
        this.ranges.forEach((range, i) => {
            if (this.#origins[i] !== last) {
                return;
            }
            if (first === undefined || range.topRow < first.row || (range.topRow === first.row && range.leftColumn < first.column)) {
                first = cell(range.topRow, range.leftColumn);
            }
        });
        return first;
    }

    get cellCount(): number {
        let sum = 0;
        for (const range of this.ranges) {
            sum += rangeCellCount(range);
        }
        return sum;
    }

    contains(position: CellPosition): boolean {
        return this.ranges.some(range => rangeContains(range, position));
    }

    coversColumn(column: number, extent: GridExtent): boolean {
        return this.#coversLine(
            extent.rowCount,
            range => range.leftColumn <= column && column <= rightColumn(range),
            range => [range.topRow, bottomRow(range)]);
    }

    coversRow(row: number, extent: GridExtent): boolean {
        return this.#coversLine(
            extent.columnCount,
            range => range.topRow <= row && row <= bottomRow(range),
            range => [range.leftColumn, rightColumn(range)]);
    }

    /** Whether the ranges crossing a line `length` long cover every position of it between them. */
    #coversLine(
        length: number, crossing: (range: SelectionRange) => boolean, span: (range: SelectionRange) => [number, number]): boolean {
        if (this.isEmpty || length <= 0) {
            return false;
        }
        const spans = this.ranges.filter(crossing).map(span).sort((a, b) => a[0] - b[0] || a[1] - b[1]);
        let covered = 0;
        for (const [first, last] of spans) {
            if (first > covered) {
                return false;
            }
            covered = Math.max(covered, last + 1);
        }
        return covered >= length;
    }

    wholeColumns(extent: GridExtent): readonly number[] {
        if (this.isEmpty || isDegenerate(extent)) {
            return [];
        }
        const columns = new Set<number>();
        for (const range of this.ranges) {
            if (!spansEveryRow(range, extent)) {
                continue;
            }
            for (let c = range.leftColumn; c <= rightColumn(range); c++) {
                columns.add(c);
            }
        }
        return [...columns].sort((a, b) => a - b);
    }

    equals(other: GridSelection): boolean {
        if (other === this) {
            return true;
        }
        if (!(other instanceof Selection) || this.ranges.length !== other.ranges.length) {
            return false;
        }
        if (!this.ranges.every((range, i) => sameRange(range, other.ranges[i]!))) {
            return false;
        }
        if (this.isEmpty) {
            return true;
        }
        return sameCell(this.#focus, other.#focus)
            && this.#focusRangeIndex === other.#focusRangeIndex
            && this.#origins.every((origin, i) => origin === other.#origins[i]);
    }

    /** Click: the Focus is the cell and the selection collapses to it (ADR-0012). */
    click(position: CellPosition, extent: GridExtent): Selection {
        if (isDegenerate(extent)) {
            return Selection.empty;
        }
        requireInside(position, extent);
        return Selection.#collapse(position);
    }

    /**
     * Shift+click, and each move of a drag: the Focus stays, and the range holding it is redrawn from the
     * Focus to the cell, which becomes its Extent (ADR-0052). The target is the cell the user pointed at,
     * so the range is redrawn even where Enter or Tab had walked the Focus inside it. The other ranges
     * stand. From nothing selected it is a click.
     */
    extendTo(position: CellPosition, extent: GridExtent): Selection {
        if (isDegenerate(extent)) {
            return Selection.empty;
        }
        requireInside(position, extent);
        if (this.isEmpty) {
            return Selection.#collapse(position);
        }
        this.#requireFits(extent);
        return new Selection(
            replaceAt(this.ranges, this.#focusRangeIndex, fromCorners(this.#focus, position)),
            this.#focus, this.#focusRangeIndex, this.#origins);
    }

    /**
     * Ctrl+click (ADR-0012, ADR-0052). On a cell not selected: adds a 1×1 range holding the Focus, the
     * range made last. On a selected cell: takes it out of every range containing it, each giving way in
     * place to its fragments, and the Focus goes to the first remaining cell, by rows, of the range made
     * last. The only selected cell cannot be taken out: the selection is returned as it was.
     */
    toggleRange(position: CellPosition, extent: GridExtent): Selection {
        if (isDegenerate(extent)) {
            return Selection.empty;
        }
        requireInside(position, extent);
        if (this.isEmpty) {
            return Selection.#collapse(position);
        }
        this.#requireFits(extent);
        const single = selectionRange(position.row, position.column, 1, 1);
        return this.contains(position) ? this.#takeOut(single) : this.#add(single, position);
    }

    /**
     * Ctrl+click on a Column Heading, or on a plain grid's column header (ADR-0050, item 1). On a column
     * not wholly selected: adds the whole column as the range made last, with the Focus on `focusRow` of
     * it (the holder passes its first visible row). On a column wholly selected: takes it out of every
     * range, and the Focus follows the take-out rule of a Ctrl+click on a cell. A take-out that would
     * leave nothing selected changes nothing. From nothing selected, the column alone.
     */
    toggleColumn(column: number, extent: GridExtent, focusRow: number): Selection {
        if (isDegenerate(extent)) {
            return Selection.empty;
        }
        requireColumn(column, extent);
        if (this.isEmpty) {
            return this.selectColumn(column, extent, focusRow);
        }
        this.#requireFits(extent);
        const whole = selectionRange(0, column, extent.rowCount, 1);
        return this.coversColumn(column, extent)
            ? this.#takeOut(whole)
            : this.#add(whole, cell(clamp(focusRow, 0, extent.rowCount - 1), column));
    }

    /** Ctrl+click on a Row Heading (ADR-0050, item 1): toggleColumn on the other axis, with the Focus on
     * `focusColumn` of an added row (the holder passes its first visible column). */
    toggleRow(row: number, extent: GridExtent, focusColumn: number): Selection {
        if (isDegenerate(extent)) {
            return Selection.empty;
        }
        requireRow(row, extent);
        if (this.isEmpty) {
            return this.selectRow(row, extent, focusColumn);
        }
        this.#requireFits(extent);
        const whole = selectionRange(row, 0, 1, extent.columnCount);
        return this.coversRow(row, extent)
            ? this.#takeOut(whole)
            : this.#add(whole, cell(row, clamp(focusColumn, 0, extent.columnCount - 1)));
    }

    /** Arrow: collapses the selection to one cell and moves it from the Focus, clamped at the grid's edge
     * (ADR-0012). Nothing selected stays so: there is no Focus to start from. */
    move(direction: GridDirection, extent: GridExtent): Selection {
        return this.#moveFocusTo(extent, focus => step(focus, direction, extent));
    }

    /**
     * Shift+arrow: the Extent moves one step and the Focus stays, so the range holding the Focus grows or
     * shrinks, and shrinking back through the Focus flips it (ADR-0052). Where the Focus is on neither
     * edge of the axis, the key changes nothing. The other axis keeps its span, so whole columns stay
     * whole under ← and → and whole rows under ↑ and ↓ (ADR-0012).
     */
    extend(direction: GridDirection, extent: GridExtent): Selection {
        return this.#extendAlong(extent, isHorizontal(direction), from => step(from, direction, extent));
    }

    /**
     * Ctrl+arrow: collapses and moves the Focus. With no edge answer, to the last or first row or column:
     * not Excel's block edge, which the grid cannot find without the data (ADR-0011). With one, to the
     * cell the Consumer names for the Focus and the direction, where the data ends (ADR-0050, item 2).
     * An answer off the Focus's line, behind it or outside the grid is refused by name rather than
     * followed.
     */
    moveToEdge(direction: GridDirection, extent: GridExtent, edge?: EdgeAnswer): Selection {
        return this.#moveFocusTo(extent, focus => edge === undefined
            ? edgeOf(focus, direction, extent)
            : answered(edge, focus, direction, extent));
    }

    /**
     * Ctrl+Shift+arrow: the Extent runs to the grid's edge, or with an edge answer to the cell the
     * Consumer names for the Extent, so a second press goes on from where the first stopped (ADR-0012,
     * ADR-0050, ADR-0052). Whole columns and whole rows stay whole, as under Shift+arrow.
     */
    extendToEdge(direction: GridDirection, extent: GridExtent, edge?: EdgeAnswer): Selection {
        return this.#extendAlong(extent, isHorizontal(direction), from => edge === undefined
            ? edgeOf(from, direction, extent)
            : answered(edge, from, direction, extent));
    }

    /** PageUp and PageDown (ADR-0012): collapse and move the Focus by a Viewport of rows. How many rows
     * that is, is view geometry the selection never holds, so the caller passes the signed delta; the
     * grid's edge clamps it. */
    moveByViewport(rowDelta: number, extent: GridExtent): Selection {
        return this.#moveFocusTo(extent, focus => stepRows(focus, rowDelta, extent));
    }

    /** Shift+PageUp and Shift+PageDown (ADR-0012, ADR-0052): the Extent moves by a Viewport of rows and
     * the Focus stays. */
    extendByViewport(rowDelta: number, extent: GridExtent): Selection {
        return this.#extendAlong(extent, false, from => stepRows(from, rowDelta, extent));
    }

    /**
     * Ctrl+A: every row after filtering across every visible column, as one rectangle, expressible
     * without holding the data (ADR-0011). The Focus stays where it is (ADR-0052, case 10); from nothing
     * selected it lands on the first cell.
     *
     * Under a pager, `context` names the rows in context, the page (ADR-0015): every cell of them, as one
     * range, with the Focus kept the same way. From nothing selected, or from a Focus outside the
     * context, the Focus lands on the context's first cell, because a range contains its own Focus.
     */
    selectAll(extent: GridExtent, context?: { readonly firstRow: number; readonly rowCount: number }): Selection {
        if (isDegenerate(extent)) {
            return Selection.empty;
        }
        if (context === undefined) {
            const all = selectionRange(0, 0, extent.rowCount, extent.columnCount);
            if (this.isEmpty) {
                return new Selection([all], cell(0, 0), 0);
            }
            this.#requireFits(extent);
            return new Selection([all], this.#focus, 0);
        }
        const start = clamp(context.firstRow, 0, extent.rowCount - 1);
        const count = clamp(context.rowCount, 1, extent.rowCount - start);
        const page = selectionRange(start, 0, count, extent.columnCount);
        if (this.isEmpty || !rangeContains(page, this.#focus)) {
            return new Selection([page], cell(start, 0), 0);
        }
        this.#requireFits(extent);
        return new Selection([page], this.#focus, 0);
    }

    /** Ctrl+Space: the range holding the Focus grows to every row, keeping its column span (ADR-0012,
     * ADR-0052). The Focus stays. Nothing selected stays so. */
    selectWholeColumns(extent: GridExtent): Selection {
        return this.#growFocusRange(extent, range => selectionRange(0, range.leftColumn, extent.rowCount, range.columnCount));
    }

    /** Shift+Space: the range holding the Focus grows to every visible column, keeping its row span
     * (ADR-0012, ADR-0052). The Focus stays. Nothing selected stays so. */
    selectWholeRows(extent: GridExtent): Selection {
        return this.#growFocusRange(extent, range => selectionRange(range.topRow, 0, range.rowCount, extent.columnCount));
    }

    /**
     * Enter and Tab (ADR-0012). With a range selected, only the Focus moves: Enter column-major, Tab
     * row-major, wrapping past the last cell into the next range in the order they were made, and from
     * the last range back to the first; `backward` (Shift) walks the other way. With a single cell, Enter
     * moves down and Tab right, the selection follows, and the grid's edge clamps. The Extent follows the
     * Focus to its new place in its range (ADR-0052).
     */
    cycleFocus(order: CycleOrder, backward: boolean, extent: GridExtent): Selection {
        if (order !== 'columnMajor' && order !== 'rowMajor') {
            throw new RangeError(`"${String(order)}" is not an order Enter or Tab walks (ADR-0012).`);
        }
        if (isDegenerate(extent) || this.isEmpty) {
            return Selection.empty;
        }
        this.#requireFits(extent);
        const only = this.ranges.length === 1 ? this.ranges[0]! : undefined;
        if (only !== undefined && rangeCellCount(only) === 1) {
            const direction: GridDirection = order === 'columnMajor'
                ? (backward ? 'up' : 'down')
                : (backward ? 'left' : 'right');
            return Selection.#collapse(step(this.#focus, direction, extent));
        }
        return this.#stepInside(order, backward);
    }

    /**
     * Ctrl+. (period): the Focus moves to the next corner of the range holding it, clockwise from the
     * top-left, and the selection does not change (ADR-0052, case 8). A corner the range shares with
     * another, in a range one row or one column deep, is passed over, so every press moves. From a cell
     * on an edge but not at a corner, the next corner clockwise along that edge; from inside the range,
     * the top-left. A single cell does not move.
     */
    moveFocusToNextCorner(extent: GridExtent): Selection {
        if (isDegenerate(extent) || this.isEmpty) {
            return Selection.empty;
        }
        this.#requireFits(extent);
        const range = this.#heldRange;
        const corners = [
            cell(range.topRow, range.leftColumn),
            cell(range.topRow, rightColumn(range)),
            cell(bottomRow(range), rightColumn(range)),
            cell(bottomRow(range), range.leftColumn),
        ] as const;
        const at = corners.findIndex(corner => sameCell(corner, this.#focus));
        if (at < 0) {
            // Not at a corner: the corner that ends the edge the Focus is on, walking clockwise; from
            // inside the range, the first corner.
            const next = this.#focus.row === range.topRow ? corners[1]
                : this.#focus.column === rightColumn(range) ? corners[2]
                    : this.#focus.row === bottomRow(range) ? corners[3]
                        : corners[0];
            return new Selection(this.ranges, next, this.#focusRangeIndex, this.#origins);
        }
        for (let offset = 1; offset < corners.length; offset++) {
            const candidate = corners[(at + offset) % corners.length]!;
            if (!sameCell(candidate, this.#focus)) {
                return new Selection(this.ranges, candidate, this.#focusRangeIndex, this.#origins);
            }
        }
        return this;
    }

    /** Shift+Backspace: the selection collapses to the Focus (ADR-0052, case 7). Nothing selected stays
     * so. */
    collapseToFocus(extent: GridExtent): Selection {
        if (isDegenerate(extent) || this.isEmpty) {
            return Selection.empty;
        }
        this.#requireFits(extent);
        return Selection.#collapse(this.#focus);
    }

    /**
     * Moves the Focus onto a cell inside the selection and leaves the ranges where they are: what a Find
     * step does when it searches the selection (ADR-0055). The range holding the cell becomes the Focus
     * range; with the cell in several, the one made latest. Throws a RangeError when the cell is selected
     * nowhere: moving the Focus out of the selection is a click.
     */
    focusOn(position: CellPosition): Selection {
        for (let i = this.ranges.length - 1; i >= 0; i--) {
            if (rangeContains(this.ranges[i]!, position)) {
                return new Selection(this.ranges, position, i, this.#origins);
            }
        }
        throw new RangeError(`The cell ${cellText(position)} is not in the selection; moving the Focus out of it is a click (ADR-0055).`);
    }

    /**
     * Shift+click on a column header (ADR-0012, ADR-0052): whole columns from the Focus's column to
     * `column`, which is where the Extent goes. The Focus stays, and its range is replaced; the other
     * ranges stand. From nothing selected, the clicked column alone, with the Focus on `focusRowIfEmpty`
     * (the holder passes its first visible row, so the Viewport does not move, KB-9).
     */
    extendToColumn(column: number, extent: GridExtent, focusRowIfEmpty = 0): Selection {
        if (isDegenerate(extent)) {
            return Selection.empty;
        }
        requireColumn(column, extent);
        if (this.isEmpty) {
            return this.selectColumn(column, extent, focusRowIfEmpty);
        }
        this.#requireFits(extent);
        const whole = selectionRange(0, Math.min(this.#focus.column, column), extent.rowCount, Math.abs(column - this.#focus.column) + 1);
        return new Selection(replaceAt(this.ranges, this.#focusRangeIndex, whole), this.#focus, this.#focusRangeIndex, this.#origins);
    }

    /** Shift+click on a Row Heading (ADR-0050, ADR-0052): extendToColumn on the other axis. */
    extendToRow(row: number, extent: GridExtent, focusColumnIfEmpty = 0): Selection {
        if (isDegenerate(extent)) {
            return Selection.empty;
        }
        requireRow(row, extent);
        if (this.isEmpty) {
            return this.selectRow(row, extent, focusColumnIfEmpty);
        }
        this.#requireFits(extent);
        const whole = selectionRange(Math.min(this.#focus.row, row), 0, Math.abs(row - this.#focus.row) + 1, extent.columnCount);
        return new Selection(replaceAt(this.ranges, this.#focusRangeIndex, whole), this.#focus, this.#focusRangeIndex, this.#origins);
    }

    /**
     * A Consumer's placement (ADR-0050, item 4): the range becomes the one range, with the Focus on
     * `focus`, as a click leaves it, only over a range the Consumer named. A range reaching outside the
     * grid, or a Focus outside the range, is refused with a RangeError rather than clamped: the Consumer
     * named cells that are not there.
     */
    place(range: SelectionRange, focus: CellPosition, extent: GridExtent): Selection {
        if (isDegenerate(extent)) {
            return Selection.empty;
        }
        const checked = selectionRange(range.topRow, range.leftColumn, range.rowCount, range.columnCount);
        if (bottomRow(checked) >= extent.rowCount || rightColumn(checked) >= extent.columnCount) {
            throw new RangeError(`The range reaches outside the grid (${extent.rowCount} rows × ${extent.columnCount} columns) (ADR-0050).`);
        }
        if (!rangeContains(checked, focus)) {
            throw new RangeError('The Focus is placed inside the range it belongs to (ADR-0050, ADR-0052).');
        }
        return new Selection([checked], cell(focus.row, focus.column), 0);
    }

    /**
     * A plain click on a column header that selects (ADR-0050, item 1): the whole column, as one range,
     * with the Focus on `focusRow` of it (the holder passes its first visible row, so the Viewport does
     * not move). Every other range goes, as a plain click on a cell collapses.
     */
    selectColumn(column: number, extent: GridExtent, focusRow: number): Selection {
        if (isDegenerate(extent)) {
            return Selection.empty;
        }
        requireColumn(column, extent);
        return new Selection(
            [selectionRange(0, column, extent.rowCount, 1)], cell(clamp(focusRow, 0, extent.rowCount - 1), column), 0);
    }

    /** A plain click on a Row Heading (ADR-0050, item 1): the whole row as one range, with the Focus on
     * `focusColumn` of it (the holder passes its first visible column). */
    selectRow(row: number, extent: GridExtent, focusColumn: number): Selection {
        if (isDegenerate(extent)) {
            return Selection.empty;
        }
        requireRow(row, extent);
        return new Selection(
            [selectionRange(row, 0, 1, extent.columnCount)], cell(row, clamp(focusColumn, 0, extent.columnCount - 1)), 0);
    }

    /** One cell selected, holding the Focus. */
    static #collapse(position: CellPosition): Selection {
        return new Selection([selectionRange(position.row, position.column, 1, 1)], cell(position.row, position.column), 0);
    }

    /** Adds the range as the range made last, holding the Focus (ADR-0012). */
    #add(range: SelectionRange, focus: CellPosition): Selection {
        const origins = [...this.#origins, this.#origins[this.#origins.length - 1]! + 1];
        return new Selection([...this.ranges, range], focus, this.ranges.length, origins);
    }

    /**
     * Takes the area out of every range, each giving way in place to its fragments, and puts the Focus on
     * the first remaining cell, by rows, of the range made last (ADR-0052). Nothing left means nothing
     * changes: the selection is never taken out whole.
     */
    #takeOut(area: SelectionRange): Selection {
        const remaining: SelectionRange[] = [];
        const cameFrom: number[] = [];
        this.ranges.forEach((range, i) => {
            for (const piece of subtractArea(range, area)) {
                remaining.push(piece);
                cameFrom.push(this.#origins[i]!);
            }
        });
        if (remaining.length === 0) {
            return this;
        }
        // The range made last is the one with the highest origin still holding a cell. When the take-out
        // emptied it (a 1×1 range made last, clicked again), the latest range still standing takes its
        // place (not observed in Excel; the nearest reading).
        const last = cameFrom[cameFrom.length - 1]!;
        let focusIndex = -1;
        remaining.forEach((piece, j) => {
            if (cameFrom[j] !== last) {
                return;
            }
            const held = remaining[focusIndex];
            if (held === undefined || piece.topRow < held.topRow || (piece.topRow === held.topRow && piece.leftColumn < held.leftColumn)) {
                focusIndex = j;
            }
        });
        const focusRange = remaining[focusIndex]!;
        return new Selection(remaining, cell(focusRange.topRow, focusRange.leftColumn), focusIndex, cameFrom);
    }

    #moveFocusTo(extent: GridExtent, destination: (focus: CellPosition) => CellPosition): Selection {
        if (isDegenerate(extent) || this.isEmpty) {
            return Selection.empty;
        }
        this.#requireFits(extent);
        return Selection.#collapse(destination(this.#focus));
    }

    /**
     * A keyboard extension along one axis (ADR-0052): the Extent moves to the destination of itself, and
     * the range holding the Focus spans from the Focus to it on that axis, keeping its span on the other,
     * which keeps whole columns whole under ← and → (ADR-0012). Where the Focus is on neither edge of the
     * axis, the key changes nothing (case 3).
     */
    #extendAlong(extent: GridExtent, horizontal: boolean, destination: (from: CellPosition) => CellPosition): Selection {
        if (isDegenerate(extent) || this.isEmpty) {
            return Selection.empty;
        }
        this.#requireFits(extent);
        const range = this.#heldRange;
        const focus = this.#focus;
        const interior = horizontal
            ? focus.column > range.leftColumn && focus.column < rightColumn(range)
            : focus.row > range.topRow && focus.row < bottomRow(range);
        if (interior) {
            return this;
        }
        const moved = destination(extentOf(range, focus));
        const redrawn = horizontal
            ? selectionRange(range.topRow, Math.min(focus.column, moved.column), range.rowCount, Math.abs(moved.column - focus.column) + 1)
            : selectionRange(Math.min(focus.row, moved.row), range.leftColumn, Math.abs(moved.row - focus.row) + 1, range.columnCount);
        return new Selection(replaceAt(this.ranges, this.#focusRangeIndex, redrawn), focus, this.#focusRangeIndex, this.#origins);
    }

    #growFocusRange(extent: GridExtent, grow: (range: SelectionRange) => SelectionRange): Selection {
        if (isDegenerate(extent) || this.isEmpty) {
            return Selection.empty;
        }
        this.#requireFits(extent);
        return new Selection(
            replaceAt(this.ranges, this.#focusRangeIndex, grow(this.#heldRange)), this.#focus, this.#focusRangeIndex, this.#origins);
    }

    /** One step of Enter or Tab inside the selection: the next cell of the Focus's range in the order, or
     * on into the next range in the order they were made, wrapping (ADR-0012). */
    #stepInside(order: CycleOrder, backward: boolean): Selection {
        const range = this.#heldRange;
        const next = ordinalOf(this.#focus, range, order) + (backward ? -1 : 1);
        if (next >= rangeCellCount(range)) {
            const following = (this.#focusRangeIndex + 1) % this.ranges.length;
            return new Selection(this.ranges, cellAt(this.ranges[following]!, 0, order), following, this.#origins);
        }
        if (next < 0) {
            const preceding = (this.#focusRangeIndex - 1 + this.ranges.length) % this.ranges.length;
            const entered = this.ranges[preceding]!;
            return new Selection(this.ranges, cellAt(entered, rangeCellCount(entered) - 1, order), preceding, this.#origins);
        }
        return new Selection(this.ranges, cellAt(range, next, order), this.#focusRangeIndex, this.#origins);
    }

    #requireFits(extent: GridExtent): void {
        const fits = this.ranges.every(range => bottomRow(range) < extent.rowCount && rightColumn(range) < extent.columnCount)
            && this.#focus.row < extent.rowCount && this.#focus.column < extent.columnCount;
        if (!fits) {
            throw new Error(
                `The selection no longer fits the grid (${extent.rowCount} rows × ${extent.columnCount} columns). `
                + 'Positions name other cells once the order changes: the holder takes the empty selection when the '
                + 'Row Sequence Version or the visible columns change (ADR-0011).');
        }
    }
}

/**
 * The Consumer's answer to Ctrl+arrow (ADR-0050, item 2): for a cell and a direction, the cell where the
 * data ends along that line. It is checked: on the asked cell's own row or column, not behind it, inside
 * the grid.
 */
export type EdgeAnswer = (from: CellPosition, direction: GridDirection) => CellPosition;

function answered(edge: EdgeAnswer, from: CellPosition, direction: GridDirection, extent: GridExtent): CellPosition {
    const answer = edge(from, direction);
    const inside = answer.row >= 0 && answer.row < extent.rowCount && answer.column >= 0 && answer.column < extent.columnCount;
    const onLine = direction === 'up' ? answer.column === from.column && answer.row <= from.row
        : direction === 'down' ? answer.column === from.column && answer.row >= from.row
            : direction === 'left' ? answer.row === from.row && answer.column <= from.column
                : answer.row === from.row && answer.column >= from.column;
    if (!inside || !onLine) {
        throw new Error(
            `The edge answer for ${direction} from ${cellText(from)} was ${cellText(answer)}, which is `
            + (inside ? 'not on that cell\'s line in that direction' : `outside the grid (${extent.rowCount} rows × ${extent.columnCount} columns)`)
            + '. Ctrl+arrow moves along one line only; answer a cell on it, or the cell asked about itself (ADR-0050).');
    }
    return cell(answer.row, answer.column);
}

/**
 * The Extent of a range for a Focus inside it (ADR-0052): on each axis the edge opposite the Focus's
 * edge, and the Focus's own coordinate where the range is one cell deep or the Focus is on neither edge.
 */
function extentOf(range: SelectionRange, focus: CellPosition): CellPosition {
    return cell(
        focus.row === range.topRow ? bottomRow(range) : focus.row === bottomRow(range) ? range.topRow : focus.row,
        focus.column === range.leftColumn ? rightColumn(range) : focus.column === rightColumn(range) ? range.leftColumn : focus.column);
}


function replaceAt(ranges: readonly SelectionRange[], index: number, replacement: SelectionRange): SelectionRange[] {
    return ranges.map((range, i) => (i === index ? replacement : range));
}

/** A grid with no rows or no columns has nothing to select: every transition yields nothing selected,
 * which is not an error. */
function isDegenerate(extent: GridExtent): boolean {
    return extent.rowCount <= 0 || extent.columnCount <= 0;
}

function requireInside(position: CellPosition, extent: GridExtent): void {
    if (!Number.isSafeInteger(position.row) || !Number.isSafeInteger(position.column)
        || position.row < 0 || position.row >= extent.rowCount || position.column < 0 || position.column >= extent.columnCount) {
        throw new RangeError(`The cell ${cellText(position)} is outside the grid (${extent.rowCount} rows × ${extent.columnCount} columns).`);
    }
}

function requireColumn(column: number, extent: GridExtent): void {
    if (!Number.isSafeInteger(column) || column < 0 || column >= extent.columnCount) {
        throw new RangeError(`Column ${column} is outside the grid (${extent.columnCount} columns).`);
    }
}

function requireRow(row: number, extent: GridExtent): void {
    if (!Number.isSafeInteger(row) || row < 0 || row >= extent.rowCount) {
        throw new RangeError(`Row ${row} is outside the grid (${extent.rowCount} rows).`);
    }
}

function clamp(value: number, least: number, most: number): number {
    return Math.min(Math.max(value, least), most);
}

function isHorizontal(direction: GridDirection): boolean {
    return direction === 'left' || direction === 'right';
}

function step(origin: CellPosition, direction: GridDirection, extent: GridExtent): CellPosition {
    switch (direction) {
        case 'up': return cell(Math.max(0, origin.row - 1), origin.column);
        case 'down': return cell(Math.min(extent.rowCount - 1, origin.row + 1), origin.column);
        case 'left': return cell(origin.row, Math.max(0, origin.column - 1));
        case 'right': return cell(origin.row, Math.min(extent.columnCount - 1, origin.column + 1));
    }
}

function stepRows(origin: CellPosition, rowDelta: number, extent: GridExtent): CellPosition {
    return cell(clamp(origin.row + rowDelta, 0, extent.rowCount - 1), origin.column);
}

function edgeOf(origin: CellPosition, direction: GridDirection, extent: GridExtent): CellPosition {
    switch (direction) {
        case 'up': return cell(0, origin.column);
        case 'down': return cell(extent.rowCount - 1, origin.column);
        case 'left': return cell(origin.row, 0);
        case 'right': return cell(origin.row, extent.columnCount - 1);
    }
}

/** Column-major counts down the column first (Enter); row-major across the row first (Tab). Ordinal 0
 * is the top-left cell and the last ordinal the bottom-right under either order (ADR-0012). */
function ordinalOf(position: CellPosition, range: SelectionRange, order: CycleOrder): number {
    const row = position.row - range.topRow;
    const column = position.column - range.leftColumn;
    return order === 'columnMajor' ? column * range.rowCount + row : row * range.columnCount + column;
}

function cellAt(range: SelectionRange, ordinal: number, order: CycleOrder): CellPosition {
    return order === 'columnMajor'
        ? cell(range.topRow + (ordinal % range.rowCount), range.leftColumn + Math.floor(ordinal / range.rowCount))
        : cell(range.topRow + Math.floor(ordinal / range.columnCount), range.leftColumn + (ordinal % range.columnCount));
}
