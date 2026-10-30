// The Row Marks a grid paints (ADR-0043): who holds them, the header's checkbox, a row's checkbox, and the count
// beside the grid. The marks are the Consumer's; the grid asks each painted row whether it is marked, paints the
// header from the counts the Consumer answers, and reports every gesture as one intent.

import { markCountText, markLabelIds } from '../chrome/mark-words';
import { rowMarkHeaderState, type RowMarks } from '../rows/row-marks';
import type { RowKind } from '../paint/row-classes';
import { rowRunsOf } from '../selection/heading-marks';
import type { SelectionRange } from '../selection/range';
import type { RowRange } from '../viewport/row-range';

/** What a checkbox's `aria-checked` says: none, all, or some, which is also what counts not yet known claim. */
type Checked = 'false' | 'true' | 'mixed';

const headerChecked: Readonly<Record<ReturnType<typeof rowMarkHeaderState>, Checked>> = { none: 'false', some: 'mixed', all: 'true' };

/** The marks a grid binds, and what it paints of them outside its rows. */
export class GridMarks<Row> {
    /** The header's checkbox: one element for the grid's life, so a header built again keeps the press made on
     * it. */
    readonly headerBox: HTMLButtonElement;
    readonly #count: HTMLElement;
    readonly #changed: () => void;
    #marks: RowMarks<Row> | undefined;
    #unsubscribe: (() => void) | undefined;
    #rowKind: ((row: Row) => RowKind) | undefined;

    /** Marks painted in `doc`; `changed` hears that the marks or their counts moved, so the grid paints again. */
    constructor(doc: Document, changed: () => void) {
        this.#changed = changed;
        this.headerBox = checkbox(doc, 'ex-mark ex-mark-all');
        // The count stands in the grid's status line (ADR-0014, ADR-0043) only while it says something.
        this.#count = doc.createElement('span');
        this.#count.className = 'ex-mark-count';
    }

    /** Who holds the marks, while a Mark Column is declared. */
    get marks(): RowMarks<Row> | undefined {
        return this.#marks;
    }

    /** Binds the marks of the grid's Mark Column, or none: heard while bound, and told the grid's Row Kind whenever
     * it is another, which leaves Group and Total rows out of "all" and the counts (ADR-0024, ADR-0043). */
    bind(marks: RowMarks<Row> | undefined, rowKind: ((row: Row) => RowKind) | undefined): void {
        if (marks === this.#marks) {
            if (marks !== undefined && rowKind !== this.#rowKind) marks.setRowKind(rowKind);
            this.#rowKind = rowKind;
            return;
        }
        this.#unsubscribe?.();
        this.#unsubscribe = undefined;
        // The marks let go keep no Row Kind of this grid's: no grid declares it over them any more.
        if (this.#marks !== undefined && this.#rowKind !== undefined) this.#marks.setRowKind(undefined);
        this.#marks = marks;
        this.#rowKind = rowKind;
        if (marks === undefined) return;
        marks.setRowKind(rowKind);
        this.#unsubscribe = marks.subscribe(this.#changed);
    }

    /** Whether a row is marked; asked only for the rows a pass paints, and only while a Mark Column stands. */
    isMarked(row: Row): boolean {
        return this.#marks !== undefined && this.#marks.isMarked(row);
    }

    /**
     * Writes the header's checkbox and the count from the counts the Consumer answers, never from the Window: a
     * fully marked Window inside a larger result is "some" (ADR-0043, MK-2). Counts not yet known claim neither
     * none nor all. Under a pager the header shows `page`, what the grid asked of the page's rows, since its press
     * lines up the page (ADR-0015). Answers the count while it says something, for the grid to stand in its status
     * line.
     */
    paint(words: (id: string) => string, page?: Checked): HTMLElement | undefined {
        const counts = this.#marks?.counts;
        setIfChanged(this.headerBox, 'aria-checked', page ?? (counts === undefined ? 'mixed' : headerChecked[rowMarkHeaderState(counts)]));
        setIfChanged(this.headerBox, 'aria-label', words(markLabelIds.markAllRows));
        const text = this.#marks === undefined ? undefined : markCountText(counts, words);
        if (text === undefined) return undefined;
        if (this.#count.textContent !== text) this.#count.textContent = text;
        return this.#count;
    }

    /** Stops hearing the marks. */
    destroy(): void {
        this.bind(undefined, undefined);
    }
}

/** A row's checkbox (ADR-0043): a button carrying the checkbox role rather than a native checkbox, which flips its
 * own state on the press, before the Consumer has answered, and would show a mark nobody holds. */
export function rowMarkBox(doc: Document, marked: boolean, label: string): HTMLButtonElement {
    const box = checkbox(doc, 'ex-mark');
    box.setAttribute('aria-checked', marked ? 'true' : 'false');
    box.setAttribute('aria-label', label);
    return box;
}

/**
 * The rows Space marks with the Focus in the Mark Column (ADR-0043, MK-1): every row with a selected cell in that
 * column, once each, as positions in the order the Selection was made under, since past the Window the grid has
 * no identity to name them by. Ranges that overlap or touch are one.
 */
export function rowsSelectedIn(ranges: readonly SelectionRange[], column: number): readonly RowRange[] {
    return rowRunsOf(ranges.filter(range => range.leftColumn <= column && column < range.leftColumn + range.columnCount));
}

// Never holding the keyboard, as an action's button does not (ADR-0033, ADR-0037): out of the tab sequence, its
// press prevented by the grid.
function checkbox(doc: Document, className: string): HTMLButtonElement {
    const box = doc.createElement('button');
    box.type = 'button';
    box.className = className;
    box.setAttribute('role', 'checkbox');
    box.setAttribute('data-ex-claims', 'click');
    box.tabIndex = -1;
    return box;
}

function setIfChanged(element: Element, name: string, value: string): void {
    if (element.getAttribute(name) !== value) element.setAttribute(name, value);
}
