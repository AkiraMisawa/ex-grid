// The Sheet's one undo stack (ADR-0048, ADR-0331 §3): each operation the user makes is one entry, however many
// engine steps it took, and an undo or a redo of it says which Sheet to show and which range to select.

import { CellAddress, CellRange, type Sheet, type Workbook, type WorkbookChange, type WorkbookStep } from '@ex-grid/sheet-engine';
import { elementAt } from './must';

/** One operation on the stack: its engine steps in the order done, the Sheet it was done from, and the ranges an
 * undo or a redo of it selects. */
export class SheetOperation {
    readonly steps: readonly WorkbookStep[];
    readonly from: Sheet;
    readonly ranges: readonly CellRange[];

    constructor(steps: readonly WorkbookStep[], from: Sheet, ranges: readonly CellRange[]) {
        this.steps = steps;
        this.from = from;
        this.ranges = ranges;
    }

    /** The Sheet to show once the operation is undone or redone: its own, else the one it was done from, else the
     * Workbook's. */
    shownAfter(workbook: Workbook): Sheet {
        const sheet = elementAt(this.steps, 0).sheet;
        if (workbook.sheets.includes(sheet)) return sheet;
        return workbook.sheets.includes(this.from) ? this.from : workbook.shown;
    }

    /** The range an undo or a redo selects, as Excel's does: the one range, or the range that holds them all
     * where they tile it without overlapping, else the first. Undefined for an operation on the Sheets. */
    selects(): CellRange | undefined {
        const [first] = this.ranges;
        if (first === undefined) return undefined;
        const distinct = this.ranges.filter((range, i) => this.ranges.findIndex(other => other.equals(range)) === i);
        if (distinct.length === 1) return first;
        const top = Math.min(...distinct.map(r => r.first.row));
        const left = Math.min(...distinct.map(r => r.first.column));
        const bottom = Math.max(...distinct.map(r => r.last.row));
        const right = Math.max(...distinct.map(r => r.last.column));
        for (let i = 0; i < distinct.length; i++) {
            for (let j = i + 1; j < distinct.length; j++) {
                if (overlap(elementAt(distinct, i), elementAt(distinct, j))) return first;
            }
        }
        const cells = distinct.reduce((sum, r) => sum + r.cellCount, 0);
        return cells === (bottom - top + 1) * (right - left + 1)
            ? new CellRange(new CellAddress(top, left), new CellAddress(bottom, right))
            : first;
    }
}

/** An operation undone or redone, and what each of its steps changed. */
export interface SheetOperationStepped {
    readonly operation: SheetOperation;
    readonly changes: readonly WorkbookChange[];
}

/** The undo stack. */
export class SheetHistory {
    readonly #done: SheetOperation[] = [];
    readonly #undone: SheetOperation[] = [];

    get canUndo(): boolean { return this.#done.length > 0; }

    get canRedo(): boolean { return this.#undone.length > 0; }

    /** Records an operation of one or more steps; `ranges` in place of the first step's, where given. */
    record(steps: readonly WorkbookStep[], from: Sheet, ranges?: readonly CellRange[]): void {
        const [first] = steps;
        if (first === undefined) throw new RangeError('An operation is at least one step.');
        this.#done.push(new SheetOperation([...steps], from, ranges ?? first.ranges));
        this.#undone.length = 0;
    }

    /** Adds `step` to the latest operation where it began with `first` and nothing was undone since: the columns
     * of one resize are one operation. */
    tryExtend(first: WorkbookStep, step: WorkbookStep): boolean {
        const latest = this.#done.at(-1);
        if (latest === undefined || this.#undone.length > 0 || latest.steps[0] !== first) return false;
        this.#done[this.#done.length - 1] = new SheetOperation([...latest.steps, step], latest.from, [...latest.ranges, ...step.ranges]);
        return true;
    }

    undo(): SheetOperationStepped | undefined {
        const operation = this.#done.pop();
        if (operation === undefined) return undefined;
        const changes = operation.steps.toReversed().map(step => step.undo());
        this.#undone.push(operation);
        return { operation, changes };
    }

    redo(): SheetOperationStepped | undefined {
        const operation = this.#undone.pop();
        if (operation === undefined) return undefined;
        const changes = operation.steps.map(step => step.redo());
        this.#done.push(operation);
        return { operation, changes };
    }

    clear(): void {
        this.#done.length = 0;
        this.#undone.length = 0;
    }
}

function overlap(a: CellRange, b: CellRange): boolean {
    return a.first.row <= b.last.row && b.first.row <= a.last.row && a.first.column <= b.last.column && b.first.column <= a.last.column;
}
