import { elementAt } from '../must';
import type { CellPosition, GridDirection, GridExtent } from '../selection/cell';
import { bottomRow, rangeCellCount, rangeContains, rightColumn, selectionRange, type SelectionRange } from '../selection/range';
import type { Selection } from '../selection/selection';

// The copy and paste decision rules: pure and stateless, the reference for what the clipboard layer may
// do. Copy refuses rather than truncates (ADR-0005); paste never spills outside the selection (ADR-0014);
// a refusal always says which rule it hit. No extent guards stale positions here: a same-sized reorder is
// invisible to one, and the holder drops the selection when the Row Sequence Version changes (ADR-0011).
// The one extent that appears is a spill's, the edge a spilled block may not cross (ADR-0050, item 3).

/**
 * Why a copy is refused (ADR-0005, ADR-0011, ADR-0014). The rules here give the first three; the rest
 * are the grid's, after the fact: rows that could not be had, a clipboard the browser would not write,
 * and a Consumer's copy answer that refused (ADR-0050, item 9).
 */
export type CopyRefusalReason =
    | 'emptySelection'
    | 'misalignedShape'
    | 'tooLarge'
    | 'rowsUnavailable'
    | 'clipboardUnavailable'
    | 'refusedByConsumer';

/** How the aligned segments combine into one block (ADR-0011): stacked when they share a column span,
 * side by side when they share a row span. A single range is `vertical`. */
export type CopyOrientation = 'vertical' | 'horizontal';

/**
 * The approved copy: the rectangles to emit, in position order (by top row when vertical, by left column
 * when horizontal) and never creation order, so the pasted block reads as the screen does; gaps close up,
 * as in Excel (ADR-0011). Positional, like the selection it came from.
 */
export interface CopyPlan {
    /** The rectangles, in position order. */
    readonly segments: readonly SelectionRange[];
    /** How they combine. */
    readonly orientation: CopyOrientation;
    /** The block's height in rows, a header row not counted. */
    readonly totalRows: number;
    /** The block's width in columns. */
    readonly totalColumns: number;
}

/** A copy approved with its plan, or refused with its reason: never both, never neither (ADR-0005). */
export type CopyDecision =
    | { readonly refused: false; readonly plan: CopyPlan }
    | { readonly refused: true; readonly reason: CopyRefusalReason };

/**
 * Why a paste or a fill key is refused (ADR-0014, ADR-0035, ADR-0050 item 3). The rules here give the
 * shape and declaration reasons and `tooLarge` for a fill's source; the rest are the grid's: a clipboard
 * past its byte ceiling, a fill's source rows not had whole, an order or a Source that moved since the
 * gesture was aimed (ADR-0011, ADR-0142), an unreadable clipboard, and an Escape that cancelled the paste.
 */
export type PasteRefusalReason =
    | 'emptySelection'
    | 'singleCellTarget'
    | 'shapeMismatch'
    | 'disjointTarget'
    | 'targetNotEditable'
    | 'tooLarge'
    | 'spillPastExtent'
    | 'nothingToFillFrom'
    | 'multipleRanges'
    | 'sourceUnavailable'
    | 'orderMoved'
    | 'sourceChanged'
    | 'unreadable'
    | 'cancelled';

/** The dimensions of the block on the clipboard, at least one cell each way (ADR-0014). */
export interface PasteShape {
    /** How many rows the block has. */
    readonly rows: number;
    /** How many columns the block has. */
    readonly columns: number;
}

/** A coordinate inside the copied block, (0, 0) its top-left: a different space from a grid position. */
export interface SourceCell {
    /** The row within the block, from 0. */
    readonly row: number;
    /** The column within the block, from 0. */
    readonly column: number;
}

/** A block of `rows` × `columns`. A shape with no rows or columns is a parser defect, not a refusal: when
 * the clipboard holds nothing tabular the rules are never asked (ADR-0014). */
export function pasteShape(rows: number, columns: number): PasteShape {
    const shape = { rows, columns };
    checkShape(shape);
    return Object.freeze(shape);
}

/** A shape not made by {@link pasteShape} is refused by name rather than divided by. */
function checkShape(shape: PasteShape): void {
    if (!Number.isSafeInteger(shape.rows) || shape.rows < 1) {
        throw new RangeError(`A paste shape has at least one row. (rows: ${shape.rows})`);
    }
    if (!Number.isSafeInteger(shape.columns) || shape.columns < 1) {
        throw new RangeError(`A paste shape has at least one column. (columns: ${shape.columns})`);
    }
}

function isSingleCell(shape: PasteShape): boolean {
    return shape.rows === 1 && shape.columns === 1;
}

/**
 * The approved paste: the target ranges and how the block tiles onto them, from each range's top-left.
 * Ranges and modulo arithmetic, never a per-cell list, so a whole-column target of a million rows stays
 * one rectangle, rows off screen or not yet fetched included (ADR-0014).
 */
export class PastePlan {
    /** The ranges to fill: more than one only with a 1×1 block; a spill is one range, the block itself. */
    readonly targets: readonly SelectionRange[];
    /** The block on the clipboard. */
    readonly source: PasteShape;
    /** One value of plain text over more than one cell: the one target is the top-left cell of the range
     * made last, and the Selection collapses to it once the Consumer accepts (ADR-0014, amended
     * 2026-09-29). */
    readonly collapsesSelection: boolean;

    /** @internal */
    constructor(targets: readonly SelectionRange[], source: PasteShape, collapsesSelection = false) {
        checkShape(source);
        this.targets = Object.freeze([...targets]);
        this.source = source;
        this.collapsesSelection = collapsesSelection;
    }

    /**
     * Which block cell lands on a target cell. Several ranges come only with a 1×1 block, so the first
     * that holds the cell answers. A cell outside every target is a caller's defect, and throws.
     */
    sourceCellFor(target: CellPosition): SourceCell {
        const range = this.targets.find(each => rangeContains(each, target));
        if (range === undefined) {
            throw new RangeError(`The cell (${target.row}, ${target.column}) is not inside any target range of this paste.`);
        }
        return Object.freeze({
            row: (target.row - range.topRow) % this.source.rows,
            column: (target.column - range.leftColumn) % this.source.columns,
        });
    }
}

/** A paste approved with its plan, or refused with its reason; a refusal for a column that is not
 * Editable names the first such column, by position in the current order (ADR-0035, ADR-0203). */
export type PasteDecision =
    | { readonly refused: false; readonly plan: PastePlan }
    | { readonly refused: true; readonly reason: 'targetNotEditable'; readonly column: number }
    | { readonly refused: true; readonly reason: Exclude<PasteRefusalReason, 'targetNotEditable'> };

/** What a fill key reads and writes once approved (ADR-0035): `source`, written over the paste's one
 * target by the paste arithmetic of ADR-0014. */
export interface FillPlan {
    /** The cells read: the range's top row or left column, or the row above or column to the left of a
     * range one cell deep. */
    readonly source: SelectionRange;
    /** The write, in the shape a paste has. */
    readonly paste: PastePlan;
}

/** A fill key's answer (ADR-0035): an approved plan, or the paste reason it was refused for. */
export type FillDecision =
    | { readonly refused: false; readonly plan: FillPlan }
    | { readonly refused: true; readonly reason: 'targetNotEditable'; readonly column: number }
    | { readonly refused: true; readonly reason: Exclude<PasteRefusalReason, 'targetNotEditable'> };

/**
 * The default copy cap, in cells, the unit of the status display's count (ADR-0014). A safety valve, not
 * a daily path (ADR-0005); exactly at the cap still copies.
 */
export const defaultCellCap = 1_000_000;

function checkCap(cellCap: number): void {
    // A cap below one cell is a misconfiguration, not a refusal.
    if (!Number.isSafeInteger(cellCap) || cellCap < 1) {
        throw new RangeError(`A cell cap is a whole number of at least one cell. (cellCap: ${cellCap})`);
    }
}

function refuse<R extends Exclude<PasteRefusalReason, 'targetNotEditable'> | CopyRefusalReason>(reason: R) {
    return Object.freeze({ refused: true as const, reason });
}

function refuseNotEditable(column: number) {
    return Object.freeze({ refused: true as const, reason: 'targetNotEditable' as const, column });
}

/**
 * The copy rules (ADR-0005, ADR-0011): an empty selection is refused, then a misaligned one, then one past
 * the cap. `withHeaders` counts the header row against the cap, because the cap judges what is copied:
 * a selection exactly on the cap copies plainly and refuses with headers.
 */
export function planCopy(
    selection: Selection,
    options: { readonly cellCap?: number; readonly withHeaders?: boolean } = {},
): CopyDecision {
    const { cellCap = defaultCellCap, withHeaders = false } = options;
    checkCap(cellCap);
    if (selection.isEmpty) {
        return refuse('emptySelection');
    }
    // Alignment before the cap: overlapping ranges double-count, so until the shapes line up there is no
    // well-formed block for the cap to judge.
    const plan = alignedPlan(selection.ranges);
    if (plan === undefined) {
        return refuse('misalignedShape');
    }
    const rows = plan.totalRows + (withHeaders ? 1 : 0);
    if (rows * plan.totalColumns > cellCap) {
        return refuse('tooLarge');
    }
    return Object.freeze({ refused: false, plan });
}

/**
 * The paste rules (ADR-0014), with the Editable declaration (ADR-0035) asked of each column the target
 * covers, by position in the current order. There is no form without it: an entry point that skips the
 * declaration walks past it.
 *
 * `spillWithin`, the Consumer's declaration that a paste may spill (ADR-0050, item 3), carries the grid's
 * extent: a block onto one cell is then planned as the block from that cell, refused when it would cross
 * the extent. `plainTextOnly`, a block read from plain text with no table: one value over more than one
 * cell goes into the top-left of the range made last alone (ADR-0014, amended 2026-09-29).
 */
export function planPaste(
    target: Selection,
    source: PasteShape,
    columnIsEditable: (column: number) => boolean,
    options: { readonly spillWithin?: GridExtent; readonly plainTextOnly?: boolean } = {},
): PasteDecision {
    const { spillWithin, plainTextOnly = false } = options;
    checkShape(source);
    if (target.isEmpty) {
        return refuse('emptySelection');
    }
    // Before every shape rule (ADR-0035): a shape refusal advises reselecting a target of the same shape,
    // which over a column that is not Editable would be a lie. Refused whole, never column by column.
    const notEditable = firstNotEditable(target.ranges, columnIsEditable);
    if (notEditable !== undefined) {
        return refuseNotEditable(notEditable);
    }
    // One value fills every selected cell, the bulk-entry shape and the only paste a disjoint target takes
    // (ADR-0011, ADR-0014); one value of plain text goes into one cell, as in Excel.
    if (isSingleCell(source)) {
        if (plainTextOnly && target.cellCount > 1) {
            const corner = target.topLeftOfRangeMadeLast;
            if (corner === undefined) {
                throw new Error('Internal: a selection of more than one cell has a range made last.');
            }
            return Object.freeze({ refused: false, plan: new PastePlan([selectionRange(corner.row, corner.column, 1, 1)], source, true) });
        }
        return Object.freeze({ refused: false, plan: new PastePlan(target.ranges, source) });
    }
    // Before the single-cell case: a disjoint target holding a 1×1 range is refused as disjoint, as in Excel.
    if (target.ranges.length > 1) {
        return refuse('disjointTarget');
    }
    const only = elementAt(target.ranges, 0);
    if (rangeCellCount(only) === 1) {
        return spillWithin === undefined
            ? refuse('singleCellTarget')
            : planSpill(only.topRow, only.leftColumn, source, columnIsEditable, spillWithin);
    }
    return only.rowCount % source.rows === 0 && only.columnCount % source.columns === 0
        ? Object.freeze({ refused: false, plan: new PastePlan(target.ranges, source) })
        : refuse('shapeMismatch');
}

/**
 * A block onto one cell, declared to spill (ADR-0050, item 3). "May not" before "cannot", as ADR-0035
 * orders them: the block's columns that exist are judged for Editable before the edge is. A block that
 * does not fit is refused, never clipped, which would write fewer cells than were copied.
 */
function planSpill(
    topRow: number, leftColumn: number, source: PasteShape, columnIsEditable: (column: number) => boolean, extent: GridExtent,
): PasteDecision {
    const lastColumn = Math.min(leftColumn + source.columns, extent.columnCount) - 1;
    for (let column = leftColumn; column <= lastColumn; column++) {
        if (!columnIsEditable(column)) {
            return refuseNotEditable(column);
        }
    }
    if (topRow + source.rows > extent.rowCount || leftColumn + source.columns > extent.columnCount) {
        return refuse('spillPastExtent');
    }
    return Object.freeze({ refused: false, plan: new PastePlan([selectionRange(topRow, leftColumn, source.rows, source.columns)], source) });
}

/**
 * Ctrl+D and Ctrl+R (ADR-0035): the range's top row (left column) is the source and the rest the target;
 * a range one row tall (one column wide) fills from the row above (the column to its left). The target
 * passes the paste's Editable gate; the source is only read. A source past `cellCap` is refused as
 * `tooLarge`: it would be read whole, like a copy (ADR-0005).
 */
export function planFill(
    selection: Selection,
    direction: GridDirection,
    columnIsEditable: (column: number) => boolean,
    options: { readonly cellCap?: number } = {},
): FillDecision {
    const { cellCap = defaultCellCap } = options;
    if (direction !== 'down' && direction !== 'right') {
        throw new RangeError(`A fill runs down (Ctrl+D) or right (Ctrl+R). (direction: ${direction})`);
    }
    checkCap(cellCap);
    if (selection.isEmpty) {
        return refuse('emptySelection');
    }
    // The declaration first (ADR-0035), each range judged on the target it would have alone, so a column
    // no reselection cures is named before the selection is refused for being several ranges.
    for (const candidate of selection.ranges) {
        const notEditable = firstNotEditable([targetOf(candidate, direction)], columnIsEditable);
        if (notEditable !== undefined) {
            return refuseNotEditable(notEditable);
        }
    }
    if (selection.ranges.length > 1) {
        return refuse('multipleRanges');
    }
    const range = elementAt(selection.ranges, 0);
    const source = sourceOf(range, direction);
    if (source === undefined) {
        return refuse('nothingToFillFrom');
    }
    if (rangeCellCount(source) > cellCap) {
        return refuse('tooLarge');
    }
    const paste = new PastePlan([targetOf(range, direction)], pasteShape(source.rowCount, source.columnCount));
    return Object.freeze({ refused: false, plan: Object.freeze({ source, paste }) });
}

/** What a fill key writes over one range: the rows below its top row (columns right of its left column),
 * or the whole range when it is one row tall (one column wide) and fills from outside it. */
function targetOf(range: SelectionRange, direction: 'down' | 'right'): SelectionRange {
    if (direction === 'down') {
        return range.rowCount === 1 ? range : selectionRange(range.topRow + 1, range.leftColumn, range.rowCount - 1, range.columnCount);
    }
    return range.columnCount === 1 ? range : selectionRange(range.topRow, range.leftColumn + 1, range.rowCount, range.columnCount - 1);
}

/** What a fill key reads for one range, or `undefined` at the first row (column) with nothing before it. */
function sourceOf(range: SelectionRange, direction: 'down' | 'right'): SelectionRange | undefined {
    if (direction === 'down') {
        if (range.rowCount > 1) {
            return selectionRange(range.topRow, range.leftColumn, 1, range.columnCount);
        }
        return range.topRow === 0 ? undefined : selectionRange(range.topRow - 1, range.leftColumn, 1, range.columnCount);
    }
    if (range.columnCount > 1) {
        return selectionRange(range.topRow, range.leftColumn, range.rowCount, 1);
    }
    return range.leftColumn === 0 ? undefined : selectionRange(range.topRow, range.leftColumn - 1, range.rowCount, 1);
}

/** The first column the ranges cover, range by range, that is not Editable, or `undefined`. Columns, not
 * cells: the row half of editability is the Window's, and a target covers rows not yet fetched. */
function firstNotEditable(ranges: readonly SelectionRange[], columnIsEditable: (column: number) => boolean): number | undefined {
    for (const range of ranges) {
        for (let column = range.leftColumn; column <= rightColumn(range); column++) {
            if (!columnIsEditable(column)) {
                return column;
            }
        }
    }
    return undefined;
}

function alignedPlan(ranges: readonly SelectionRange[]): CopyPlan | undefined {
    if (ranges.length === 1) {
        const only = elementAt(ranges, 0);
        return Object.freeze({ segments: Object.freeze([only]), orientation: 'vertical', totalRows: only.rowCount, totalColumns: only.columnCount });
    }
    // A shared column span with disjoint row spans stacks; the transpose sits side by side (ADR-0011).
    // For two or more ranges the branches exclude each other: equal spans cannot also be disjoint.
    const vertical = ranges.toSorted((a, b) => a.topRow - b.topRow);
    const first = elementAt(vertical, 0);
    if (vertical.every(each => each.leftColumn === first.leftColumn && each.columnCount === first.columnCount)
        && disjoint(vertical, bottomRow, each => each.topRow)) {
        const totalRows = vertical.reduce((sum, each) => sum + each.rowCount, 0);
        return Object.freeze({ segments: Object.freeze(vertical), orientation: 'vertical', totalRows, totalColumns: first.columnCount });
    }
    const horizontal = ranges.toSorted((a, b) => a.leftColumn - b.leftColumn);
    const left = elementAt(horizontal, 0);
    if (horizontal.every(each => each.topRow === left.topRow && each.rowCount === left.rowCount)
        && disjoint(horizontal, rightColumn, each => each.leftColumn)) {
        const totalColumns = horizontal.reduce((sum, each) => sum + each.columnCount, 0);
        return Object.freeze({ segments: Object.freeze(horizontal), orientation: 'horizontal', totalRows: left.rowCount, totalColumns });
    }
    return undefined;
}

/** Sorted by their start; an overlap or an exact duplicate fails, since emitting the same cells twice
 * would be a quietly wrong total (ADR-0005). */
function disjoint(sorted: readonly SelectionRange[], end: (range: SelectionRange) => number, start: (range: SelectionRange) => number): boolean {
    return sorted.every((each, i) => i === 0 || end(elementAt(sorted, i - 1)) < start(each));
}
