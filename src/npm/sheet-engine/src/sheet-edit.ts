// One user operation on a Sheet, described before it is done (C#'s SheetEdit.cs; ADR-0048, ADR-0331).
import { CellAddress } from './cell-address';
import type { HorizontalAlignment } from './cell-format';
import { CellFormatChange } from './cell-format-change';
import { CellRange } from './cell-range';
import { StructuralEdit } from './formulas/structural-edit';
import { elementAt, defined } from './must';
import { NumberFormat } from './number-format';
import { Sheet, type AddressedState, type EntryOfCell, type FillDirection, type RewrittenEntry, type SheetBlock, SheetStep, type TypedCell } from './sheet';
import { SheetChange, SheetColumnWidth, SheetRefusal, SheetRefusedError } from './sheet-change';
import type { Workbook } from './workbook';

const alignments: readonly HorizontalAlignment[] = ['general', 'left', 'center', 'right'];
const fillDirections: readonly FillDirection[] = ['down', 'right', 'up', 'left'];

/**
 * One user operation on a Sheet, described before it is done: an edit, a paste, a Cell Format, an insertion or
 * deletion, a fill, or an operation on the Workbook's Sheets. `Workbook.do` does it and returns the one step that
 * undoes it (ADR-0048): a paste over a thousand cells is one step.
 */
export abstract class SheetEdit {
    /** @internal */
    protected constructor() {
        // Only this module's edits derive from it, as C#'s private protected constructor allows.
    }

    /** Text typed into one cell, read under the Sheet's culture (`Sheet.enter`). */
    static enter(address: CellAddress, typed: string): SheetEdit {
        return SheetEdit.enterMany([[address, typed]]);
    }

    /** Several typed texts as one operation, such as a paste from another program. */
    static enterMany(typed: Iterable<TypedCell>): SheetEdit {
        const list = [...typed];
        return new CellsEdit(list.map(([a]) => a), sheet => sheet.enterMany(list));
    }

    /** Entries set or cleared (`undefined`) as one operation. */
    static setEntries(entries: Iterable<EntryOfCell>): SheetEdit {
        const list = [...entries];
        return new CellsEdit(list.map(([a]) => a), sheet => sheet.setEntries(list));
    }

    /** A Number Format set on cells; `undefined` is General. */
    static setNumberFormatOf(addresses: Iterable<CellAddress>, format: NumberFormat | undefined): SheetEdit {
        const list = [...addresses];
        return new CellsEdit(list, sheet => sheet.setNumberFormatOf(list, format));
    }

    /** A horizontal alignment set on cells. */
    static setAlignmentOf(addresses: Iterable<CellAddress>, alignment: HorizontalAlignment): SheetEdit {
        const list = [...addresses];
        return new CellsEdit(list, sheet => sheet.setAlignmentOf(list, alignment));
    }

    /**
     * A Number Format set on a range (`Sheet.setNumberFormat`); `undefined` is General. Whole columns and whole rows
     * are recorded as one entry each, and undoing the step puts back every level exactly (ADR-0047).
     */
    static setNumberFormat(range: CellRange, format: NumberFormat | undefined): SheetEdit {
        return new CellFormatEdit(range, new CellFormatChange({ numberFormat: format ?? NumberFormat.general }));
    }

    /** A horizontal alignment set on a range, recorded as `setNumberFormat` records a Number Format. Throws a RangeError for one that is not an alignment. */
    static setAlignment(range: CellRange, alignment: HorizontalAlignment): SheetEdit {
        if (!alignments.includes(alignment)) throw new RangeError(`Not an alignment: ${alignment}.`);
        return new CellFormatEdit(range, new CellFormatChange({ alignment }));
    }

    /**
     * The parts `change` names set on several ranges as one step (ADR-0046, ADR-0071), such as a selection of several
     * rectangles, whole columns and whole rows among them. The change's Borders are relative to each range, so each
     * range gets its own outline. Undoing the step puts back every level exactly. Throws when there is no range, or the
     * change sets nothing.
     */
    static setCellFormat(ranges: Iterable<CellRange>, change: CellFormatChange): SheetEdit {
        return new CellFormatsEdit(Sheet.checkCellFormat(ranges, change), change);
    }

    /**
     * A width the user set on every column `columns` spans, in characters (`Sheet.setColumnWidth`): set by the user, so
     * an entry never widens them; `undefined` puts them back at the default width. Undoing the step puts back every
     * column's width, and its kind, exactly (ADR-0046). Throws a RangeError for a width not more than 0 and at most 255.
     */
    static setColumnWidth(columns: CellRange, width: number | undefined): SheetEdit {
        const checked = Sheet.checkColumnWidth(width);
        return new ColumnWidthEdit(columns, checked === undefined ? undefined : new SheetColumnWidth(checked, 'setByUser'));
    }

    /**
     * A width an entry widened every column `columns` spans to, in characters (`Sheet.setAutomaticColumnWidth`):
     * widened by entry, and widened again by a longer entry (ADR-0046, 2026-09-28; CW-028). Undoing the step puts back
     * every column's width, and its kind, exactly.
     */
    static setAutomaticColumnWidth(columns: CellRange, width: number): SheetEdit {
        const checked = Sheet.checkColumnWidth(width);
        if (checked === undefined) throw new Error('Internal: a width checked is a width.');
        return new ColumnWidthEdit(columns, new SheetColumnWidth(checked, 'widenedByEntry'));
    }

    /** Rows inserted (`Sheet.insertRows`). */
    static insertRows(row: number, count = 1): SheetEdit {
        return new StructureEdit(new StructuralEdit('rows', row, count, true));
    }

    /** Rows deleted (`Sheet.deleteRows`). */
    static deleteRows(row: number, count = 1): SheetEdit {
        return new StructureEdit(new StructuralEdit('rows', row, count, false));
    }

    /** Columns inserted (`Sheet.insertColumns`). */
    static insertColumns(column: number, count = 1): SheetEdit {
        return new StructureEdit(new StructuralEdit('columns', column, count, true));
    }

    /** Columns deleted (`Sheet.deleteColumns`). */
    static deleteColumns(column: number, count = 1): SheetEdit {
        return new StructureEdit(new StructuralEdit('columns', column, count, false));
    }

    /**
     * A block copied inside ExSheet, pasted (ADR-0048, ADR-0071): Entries with their relative References shifted by the
     * distance pasted (a Reference shifted off the Sheet is `#REF!`, as in Excel), Cell Formats with them.
     *
     * - At a cell, its top-left cell lands there. A block that would run past the Sheet's edge is refused by name (ADR-0050).
     * - Over a range, it is repeated to fill it, as Excel repeats a paste over a selection that is a whole multiple of
     *   the block's size.
     * - Over several ranges, it is pasted over each as one operation (ADR-0014, ADR-0180); a cell two ranges share is
     *   written once, with what the later range gives it.
     *
     * Throws when there is no range, or a range is not a whole multiple of the block in both directions.
     */
    static paste(block: SheetBlock, at: CellAddress | CellRange | Iterable<CellRange>): SheetEdit {
        if (at instanceof CellAddress) {
            const refusal = Sheet.offSheet(at, block.rowCount, block.columnCount);
            return new PlacedEdit(refusal, () => Sheet.place(block, at),
                refusal === undefined ? [new CellRange(at, new CellAddress(at.row + block.rowCount - 1, at.column + block.columnCount - 1))] : []);
        }
        if (at instanceof CellRange) {
            checkWholeMultiple(block, at);
            return new PlacedEdit(undefined, () => [...tiles(block, at)].flatMap(origin => [...Sheet.place(block, origin)]), [at]);
        }
        const ranges = [...at];
        if (ranges.length === 0) throw new RangeError('There is no range to paste over.');
        for (const target of ranges) checkWholeMultiple(block, target);
        return new PlacedEdit(undefined, () => {
            const states = new Map<number, AddressedState>();
            for (const target of ranges) {
                for (const origin of tiles(block, target)) {
                    for (const placed of Sheet.place(block, origin)) {
                        // A later range's state replaces an earlier one's where it stood, as a C# Dictionary's indexer does.
                        states.set(placed.address.key, placed);
                    }
                }
            }
            return [...states.values()];
        }, ranges);
    }

    /**
     * Text pasted from another program, each field taken as if the user had typed it into its cell under the Sheet's
     * culture: `=A1+1` becomes a Formula and `1,234` a number under `en-US` (ADR-0048). A field beginning with `=` that
     * cannot be read as a Formula is taken as text, as Excel takes a pasted `=1+`. `fields` is rectangular, rows of
     * columns, with the top-left field at `origin`; an empty field clears its cell. A block that would run past the
     * Sheet's edge is refused by name (ADR-0050). Throws when the rows are not all the same length, or there are none.
     */
    static pasteText(fields: readonly (readonly string[])[], origin: CellAddress): SheetEdit {
        const first = fields[0];
        if (first === undefined || first.length === 0) throw new RangeError('Nothing to paste.');
        const width = first.length;
        if (fields.some(row => row.length !== width)) throw new RangeError('The pasted fields are not rectangular.');
        const refusal = Sheet.offSheet(origin, fields.length, width);
        if (refusal !== undefined) return new RefusedEdit(refusal);
        const typed: TypedCell[] = [];
        fields.forEach((row, r) => {
            row.forEach((field, c) => typed.push([new CellAddress(origin.row + r, origin.column + c), field]));
        });
        return SheetEdit.pasteFields(typed);
    }

    /**
     * Text pasted from another program, given field by field, each taken as `pasteText` takes it; with `entries`,
     * some cells carry their exact Value, set as they are, such as an invariant number with every digit its double
     * holds (ADR-0048, ADR-0050 item 10). The fields are read first, so a format typing gives a cell is given before
     * the Entries are set; a cell named in both takes its Entry. One operation: one step, and one change (ADR-0180).
     */
    static pasteFields(fields: Iterable<TypedCell>, entries?: Iterable<EntryOfCell>): SheetEdit {
        const typed = [...fields];
        if (entries === undefined) return new CellsEdit(typed.map(([a]) => a), sheet => sheet.enterMany(typed, true));
        const exact = [...entries];
        return new CellsEdit([...typed.map(([a]) => a), ...exact.map(([a]) => a)],
            sheet => SheetChange.merge([sheet.enterMany(typed, true), sheet.setEntries(exact)]));
    }

    /**
     * A Fill Intent resolved as Excel fills (ADR-0050, item 5): `target` extends `source` in `direction`, given as the
     * new cells alone or as the whole extended range. Formulas are copied with their References shifted, two or more
     * numbers continue as Excel's linear trend, a single date goes on by day, a single number in a line of a fill from
     * whole columns or whole rows goes on by one, and any other single number, text without a pattern, booleans, Error
     * Values and blanks are copied. Any other pattern is refused (`fillPatternNotSupported`), never filled with copies;
     * `Workbook.check` says so before anything is written.
     */
    static fill(source: CellRange, target: CellRange, direction: FillDirection): SheetEdit {
        if (!fillDirections.includes(direction)) throw new RangeError(`Not a fill direction: ${direction}.`);
        return new FillEdit(source, target, direction, false);
    }

    /**
     * Excel's fill keys, Ctrl+D and Ctrl+R (ADR-0035; ADR-0050, item 5, 2026-09-28): `target` extends `source` in
     * `direction` as a copy of it, repeated, and never as a series. Only a target that does not extend the source along
     * one axis is refused.
     */
    static fillCopy(source: CellRange, target: CellRange, direction: FillDirection): SheetEdit {
        if (!fillDirections.includes(direction)) throw new RangeError(`Not a fill direction: ${direction}.`);
        return new FillEdit(source, target, direction, true);
    }

    /**
     * Excel's Ctrl+Enter (ADR-0050, item 5, 2026-09-28): `typed`, read under the Sheet's culture as entered in
     * `enteredAt`, written into every cell of `targets` as one operation. A Formula is written as entered in
     * `enteredAt`, and into every other cell with its relative References shifted by that cell's offset from it, by the
     * rule `fillCopy` shifts them. Anything else is entered into every cell as typed. Overlapping ranges write their
     * shared cells once. Throws when there is no target, or `enteredAt` lies outside every target; doing it throws a
     * FormulaSyntaxError for a Formula that cannot be read, and nothing changes.
     */
    static enterInto(targets: Iterable<CellRange>, enteredAt: CellAddress, typed: string): SheetEdit {
        const ranges = [...targets];
        if (ranges.length === 0) throw new RangeError('There is no cell to enter into.');
        if (!ranges.some(range => range.contains(enteredAt))) throw new RangeError(`${enteredAt.toString()} is not inside any target range.`);
        const seen = new Set<number>();
        const cells: CellAddress[] = [];
        for (const range of ranges) {
            for (const cell of range.cells()) {
                if (seen.has(cell.key)) continue;
                seen.add(cell.key);
                cells.push(cell);
            }
        }
        return new CellsEdit(cells, sheet => sheet.enterInto(cells, enteredAt, typed), focusFirst(ranges, enteredAt));
    }

    /**
     * The Sheet renamed (ADR-0046, ADR-0331): every Reference on every Sheet qualified with the old name is rewritten to
     * the new one, as Excel rewrites them. A rename is a change, so `RAND` and `RANDBETWEEN` draw again (ADR-0180); a
     * rename to the name the Sheet already has changes nothing. A name another Sheet has, without regard to case, is
     * refused (`sheetNameTaken`). Throws a RangeError for a name a Sheet cannot have.
     */
    static rename(name: string): SheetEdit {
        return new RenameEdit(Sheet.checkName(name));
    }

    /**
     * A new, empty Sheet inserted beside the Sheet the edit is done on — after it, as Excel's `+` button adds one, or
     * before it, as Shift+F11 does — named `name`, or `Sheet` and the next number of the Workbook's count of the Sheets
     * it has made, as Excel numbers one (ADR-0331). Undoing it takes the Sheet out again, and gives its number back. The
     * step's Sheet is the new Sheet.
     */
    static insertSheet(name?: string, before = false): SheetEdit {
        return new InsertSheetEdit(name === undefined ? undefined : Sheet.checkName(name), before);
    }

    /**
     * The Sheet deleted (ADR-0331): every Reference on another Sheet qualified with its name becomes `#REF!`. Undoing it
     * gives back the Sheet and those Formulas exactly. The Workbook's last Sheet is not deleted (`lastSheet`).
     */
    static deleteSheet(): SheetEdit {
        return new DeleteSheetEdit();
    }

    /** The Sheet moved to `index` in the order of the Sheets after the move (ADR-0331). Throws a RangeError for a negative index. */
    static moveSheet(index: number): SheetEdit {
        if (!Number.isInteger(index) || index < 0) throw new RangeError(`${String(index)} is not a place in the order.`);
        return new MoveSheetEdit(index);
    }

    /**
     * A copy of the Sheet — its Entries, Cell Formats and column widths — named as Excel names one, `Name (2)`, placed at
     * `index` in the order after the copy, or just after the Sheet (ADR-0331). A Reference in it qualified with the
     * Sheet's name names the copy. The step's Sheet is the copy.
     */
    static copySheet(index?: number): SheetEdit {
        if (index !== undefined && (!Number.isInteger(index) || index < 0)) throw new RangeError(`${String(index)} is not a place in the order.`);
        return new CopySheetEdit(index);
    }

    /** Whether the Sheet would refuse this operation as it stands, and why. Changes nothing. @internal */
    check(_sheet: Sheet): SheetRefusal | undefined {
        return undefined;
    }

    /** Does the operation on `sheet`, checked already, and answers the step that undoes it. @internal */
    abstract apply(sheet: Sheet): SheetStep;

    /** The ranges the operation changes on its Sheet: what an undo or a redo of it selects (ADR-0331 §3). None for an operation on the Sheets. @internal */
    get ranges(): readonly CellRange[] {
        return [];
    }

    /** The smallest range holding every one of `cells`; none for no cells. @internal */
    static bounding(cells: Iterable<CellAddress>): CellRange[] {
        let top = Number.MAX_SAFE_INTEGER;
        let left = Number.MAX_SAFE_INTEGER;
        let bottom = -1;
        let right = -1;
        for (const cell of cells) {
            top = Math.min(top, cell.row);
            left = Math.min(left, cell.column);
            bottom = Math.max(bottom, cell.row);
            right = Math.max(right, cell.column);
        }
        return bottom < 0 ? [] : [new CellRange(new CellAddress(top, left), new CellAddress(bottom, right))];
    }
}

function checkWholeMultiple(block: SheetBlock, target: CellRange): void {
    if (target.rowCount % block.rowCount !== 0 || target.columnCount % block.columnCount !== 0) {
        throw new RangeError(`A ${String(target.rowCount)} × ${String(target.columnCount)} target is not a whole multiple of a ${String(block.rowCount)} × ${String(block.columnCount)} block.`);
    }
}

/** The top-left cell of each copy of `block` that fills `target`. */
function* tiles(block: SheetBlock, target: CellRange): Generator<CellAddress> {
    for (let row = target.first.row; row <= target.last.row; row += block.rowCount) {
        for (let column = target.first.column; column <= target.last.column; column += block.columnCount) yield new CellAddress(row, column);
    }
}

/** `ranges` with the one holding `cell` first. */
function focusFirst(ranges: readonly CellRange[], cell: CellAddress): readonly CellRange[] {
    const at = ranges.findIndex(range => range.contains(cell));
    return at <= 0 ? ranges : [elementAt(ranges, at), ...ranges.filter((_, i) => i !== at)];
}

/** An operation on cells in place: undone by putting back what those cells recorded before, redone by what they recorded after. */
class CellsEdit extends SheetEdit {
    readonly #touched: readonly CellAddress[];
    readonly #apply: (sheet: Sheet) => SheetChange;
    #ranges: readonly CellRange[] | undefined;

    constructor(touched: readonly CellAddress[], apply: (sheet: Sheet) => SheetChange, ranges?: readonly CellRange[]) {
        super();
        this.#touched = touched;
        this.#apply = apply;
        this.#ranges = ranges;
    }

    // The cells it names, unless it was given the ranges they were named from.
    override get ranges(): readonly CellRange[] {
        this.#ranges ??= SheetEdit.bounding(this.#touched);
        return this.#ranges;
    }

    apply(sheet: Sheet): SheetStep {
        const distinct = new Map<number, CellAddress>();
        for (const address of this.#touched) if (!distinct.has(address.key)) distinct.set(address.key, address);
        const addresses = [...distinct.values()];
        const before = sheet.record(addresses);
        const change = this.#apply(sheet);
        const after = sheet.record(addresses);
        return new SheetStep(sheet, change, s => s.restore(before), s => s.restore(after));
    }
}

/** Cells written whole — Entry and Cell Format — such as a paste of Entries: the states are computed when the edit is done, and a refusal known in advance stops it. */
class PlacedEdit extends SheetEdit {
    readonly #refusal: SheetRefusal | undefined;
    readonly #states: () => Iterable<AddressedState>;
    readonly #ranges: readonly CellRange[];

    constructor(refusal: SheetRefusal | undefined, states: () => Iterable<AddressedState>, ranges: readonly CellRange[]) {
        super();
        this.#refusal = refusal;
        this.#states = states;
        this.#ranges = ranges;
    }

    override check(): SheetRefusal | undefined {
        return this.#refusal;
    }

    override get ranges(): readonly CellRange[] {
        return this.#ranges;
    }

    apply(sheet: Sheet): SheetStep {
        const placed = sheet.settle(this.#states());
        return new CellsEdit(placed.map(p => p.address), s => s.restore(placed)).apply(sheet);
    }
}

/** A fill, planned against the Sheet as it stands when it is checked or done. */
class FillEdit extends SheetEdit {
    readonly source: CellRange;

    readonly target: CellRange;

    readonly direction: FillDirection;

    readonly copyOnly: boolean;

    constructor(source: CellRange, target: CellRange, direction: FillDirection, copyOnly: boolean) {
        super();
        this.source = source;
        this.target = target;
        this.direction = direction;
        this.copyOnly = copyOnly;
    }

    override check(sheet: Sheet): SheetRefusal | undefined {
        return sheet.planFill(this.source, this.target, this.direction, this.copyOnly).refusal;
    }

    // The source and what it was extended over, as Excel's fill leaves them selected.
    override get ranges(): readonly CellRange[] {
        const { source, target } = this;
        return [new CellRange(
            new CellAddress(Math.min(source.first.row, target.first.row), Math.min(source.first.column, target.first.column)),
            new CellAddress(Math.max(source.last.row, target.last.row), Math.max(source.last.column, target.last.column)))];
    }

    apply(sheet: Sheet): SheetStep {
        const plan = sheet.planFill(this.source, this.target, this.direction, this.copyOnly);
        if (plan.refusal !== undefined) throw new SheetRefusedError(plan.refusal);
        // From whole columns or whole rows: the target's own formats, then its cells.
        if (plan.levels !== undefined) return sheet.doFillWithLevels(plan);
        const settled = sheet.settle(plan.states);
        return new CellsEdit(settled.map(s => s.address), s => s.restore(settled)).apply(sheet);
    }
}

/** A change set on a range: undone by the row and column levels and the cells it touched put back exactly, redone by setting it again. */
class CellFormatEdit extends SheetEdit {
    readonly range: CellRange;

    readonly change: CellFormatChange;

    constructor(range: CellRange, change: CellFormatChange) {
        super();
        this.range = range;
        this.change = change;
    }

    override get ranges(): readonly CellRange[] {
        return [this.range];
    }

    apply(sheet: Sheet): SheetStep {
        const outcome = sheet.applyCellFormat(this.range, this.change);
        return new SheetStep(sheet, outcome.change, s => s.undoCellFormat(outcome), s => s.applyCellFormat(this.range, this.change).change);
    }
}

/** A change set on several ranges: undone by every range's part undone in reverse, redone by setting it again. */
class CellFormatsEdit extends SheetEdit {
    readonly #ranges: readonly CellRange[];

    readonly change: CellFormatChange;

    constructor(ranges: readonly CellRange[], change: CellFormatChange) {
        super();
        this.change = change;
        this.#ranges = ranges;
    }

    override get ranges(): readonly CellRange[] {
        return this.#ranges;
    }

    apply(sheet: Sheet): SheetStep {
        const outcome = sheet.applyCellFormats(this.#ranges, this.change);
        return new SheetStep(sheet, outcome.change, s => s.undoCellFormats(outcome), s => s.applyCellFormats(this.#ranges, this.change).change);
    }
}

/** A width set on columns: undone by every column's width put back, redone by the widths it left. */
class ColumnWidthEdit extends SheetEdit {
    readonly columns: CellRange;

    readonly width: SheetColumnWidth | undefined;

    constructor(columns: CellRange, width: SheetColumnWidth | undefined) {
        super();
        this.columns = columns;
        this.width = width;
    }

    override get ranges(): readonly CellRange[] {
        return [this.columns];
    }

    apply(sheet: Sheet): SheetStep {
        const { change, before } = sheet.applyColumnWidth(this.columns, this.width);
        const after = sheet.columnWidthsNow();
        return new SheetStep(sheet, change, s => s.restoreColumnWidths(before), s => s.restoreColumnWidths(after));
    }
}

/** A rename: undone by the old name and the Formulas it rewrote put back. */
class RenameEdit extends SheetEdit {
    readonly name: string;

    constructor(name: string) {
        super();
        this.name = name;
    }

    override check(sheet: Sheet): SheetRefusal | undefined {
        return sheet.workbook.nameTaken(this.name, sheet);
    }

    apply(sheet: Sheet): SheetStep {
        const outcome = sheet.applyRename(this.name);
        return new SheetStep(sheet, outcome.change, s => s.undoRename(outcome), s => s.applyRename(this.name).change);
    }
}

/** What a Workbook operation changed on the Sheet it names, and how to undo and redo it. @internal */
export interface WorkbookEditDone {
    readonly sheet: Sheet;
    readonly change: SheetChange;
    readonly undo: () => SheetChange;
    readonly redo: () => SheetChange;
}

/** An operation on the Workbook's Sheets rather than on one Sheet's cells (ADR-0331). @internal */
export abstract class WorkbookEdit extends SheetEdit {
    abstract checkIn(workbook: Workbook, sheet: Sheet): SheetRefusal | undefined;

    abstract applyIn(workbook: Workbook, sheet: Sheet): WorkbookEditDone;

    override check(sheet: Sheet): SheetRefusal | undefined {
        return this.checkIn(sheet.workbook, sheet);
    }

    apply(sheet: Sheet): SheetStep {
        const done = this.applyIn(sheet.workbook, sheet);
        return new SheetStep(done.sheet, done.change, () => done.undo(), () => done.redo());
    }

    /**
     * A new Sheet's step, an insertion's or a copy's: undone, the Sheet is taken out and the count of Sheets made put
     * back; redone, it is put back and counted again.
     */
    protected static counted(workbook: Workbook, made: Sheet, index: number, numbered: number | undefined, counted: number, change: SheetChange): WorkbookEditDone {
        return {
            sheet: made,
            change,
            undo: () => {
                workbook.uncountSheetMade(counted);
                return workbook.takeOut(made, false, []);
            },
            redo: () => {
                workbook.countSheetMade(numbered);
                return workbook.reinsert(made, index);
            },
        };
    }

    protected static checkIndex(index: number, count: number): number {
        if (index > count - 1) throw new RangeError(`${String(index)} is past the last place in the order, ${String(count - 1)}.`);
        return index;
    }
}

class InsertSheetEdit extends WorkbookEdit {
    readonly name: string | undefined;

    readonly before: boolean;

    constructor(name: string | undefined, before: boolean) {
        super();
        this.name = name;
        this.before = before;
    }

    checkIn(workbook: Workbook): SheetRefusal | undefined {
        return this.name === undefined ? undefined : workbook.nameTaken(this.name, undefined);
    }

    applyIn(workbook: Workbook, sheet: Sheet): WorkbookEditDone {
        const index = workbook.indexOf(sheet) + (this.before ? 0 : 1);
        const numbered = this.name === undefined ? workbook.nextSheetNumber() : undefined;
        const counted = workbook.countSheetMade(numbered);
        const name = this.name ?? workbook.sheetName(defined(numbered, 'the next number'));
        const { sheet: inserted, change } = workbook.insertSheet(index, name);
        return WorkbookEdit.counted(workbook, inserted, index, numbered, counted, change);
    }
}

class DeleteSheetEdit extends WorkbookEdit {
    checkIn(workbook: Workbook, sheet: Sheet): SheetRefusal | undefined {
        return workbook.sheets.length === 1
            ? new SheetRefusal('lastSheet', `'${sheet.name}' is the Workbook's only Sheet, and a Workbook holds at least one.`)
            : undefined;
    }

    applyIn(workbook: Workbook, sheet: Sheet): WorkbookEditDone {
        const index = workbook.indexOf(sheet);
        const shown = workbook.shown === sheet;
        const rewritten: RewrittenEntry[] = [];
        const change = workbook.takeOut(sheet, true, rewritten);
        return {
            sheet,
            change,
            undo: () => workbook.putBack(sheet, index, rewritten, shown),
            redo: () => {
                rewritten.length = 0;
                return workbook.takeOut(sheet, true, rewritten);
            },
        };
    }
}

class MoveSheetEdit extends WorkbookEdit {
    readonly index: number;

    constructor(index: number) {
        super();
        this.index = index;
    }

    checkIn(): SheetRefusal | undefined {
        return undefined;
    }

    applyIn(workbook: Workbook, sheet: Sheet): WorkbookEditDone {
        const to = WorkbookEdit.checkIndex(this.index, workbook.sheets.length);
        const from = workbook.indexOf(sheet);
        if (from === to) return { sheet, change: SheetChange.none, undo: () => SheetChange.none, redo: () => SheetChange.none };
        const rewritten: RewrittenEntry[] = [];
        const change = workbook.move(sheet, to, rewritten);
        return {
            sheet,
            change,
            undo: () => workbook.moveBack(sheet, from, rewritten),
            redo: () => {
                rewritten.length = 0;
                return workbook.move(sheet, to, rewritten);
            },
        };
    }
}

class CopySheetEdit extends WorkbookEdit {
    readonly index: number | undefined;

    constructor(index: number | undefined) {
        super();
        this.index = index;
    }

    checkIn(): SheetRefusal | undefined {
        return undefined;
    }

    applyIn(workbook: Workbook, sheet: Sheet): WorkbookEditDone {
        const at = this.index === undefined ? workbook.indexOf(sheet) + 1 : WorkbookEdit.checkIndex(this.index, workbook.sheets.length + 1);
        const counted = workbook.countSheetMade(undefined);
        const { sheet: copy, change } = workbook.copy(sheet, at, workbook.copyName(sheet.name));
        return WorkbookEdit.counted(workbook, copy, at, undefined, counted, change);
    }
}

/** An operation refused before anything was computed for it. */
class RefusedEdit extends SheetEdit {
    readonly refusal: SheetRefusal;

    constructor(refusal: SheetRefusal) {
        super();
        this.refusal = refusal;
    }

    override check(): SheetRefusal | undefined {
        return this.refusal;
    }

    apply(): SheetStep {
        throw new SheetRefusedError(this.refusal);
    }
}

/**
 * An insertion or deletion: undone by the inverse edit, then putting back the Formulas it rewrote and the cells it
 * dropped, exactly as they were — the inverse alone does not restore a range a deletion shrank, nor a Reference it
 * made `#REF!`.
 */
class StructureEdit extends SheetEdit {
    readonly edit: StructuralEdit;

    constructor(edit: StructuralEdit) {
        super();
        this.edit = edit;
    }

    override check(sheet: Sheet): SheetRefusal | undefined {
        return sheet.checkStructural(this.edit);
    }

    // The whole rows or columns inserted, or deleted.
    override get ranges(): readonly CellRange[] {
        const { axis, start, count } = this.edit;
        return [axis === 'rows' ? CellRange.wholeRows(start, start + count - 1) : CellRange.wholeColumns(start, start + count - 1)];
    }

    apply(sheet: Sheet): SheetStep {
        const outcome = sheet.restructure(this.edit);
        return new SheetStep(sheet, outcome.change, s => s.unrestructure(this.edit, outcome), s => s.restructure(this.edit).change);
    }
}

