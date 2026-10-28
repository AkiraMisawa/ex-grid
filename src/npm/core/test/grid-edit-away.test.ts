// @vitest-environment happy-dom
// Layer 2 (ADR-0350 section 13): an edit away among another set of rows (ADR-0331 §5). A Consumer that shows several
// sets of rows in one grid, a Sheet's Workbook its Sheets, hands each set's view with its rows; an edit open as another
// set is shown stays open with the rows it was opened among, its editor holding the keyboard unseen, Point pointing from
// the Focus of the rows shown, and its commit landing on the row it was opened on.
import { afterEach, describe, expect, it } from 'vitest';
import { defined } from '../src/must';
import type { GridEditIntent } from '../src/cells/edit-intent';
import type { GridColumn } from '../src/dom/grid-column';
import type { GridPasteIntent } from '../src/clipboard/paste-intent';
import type { GridRefusal } from '../src/dom/grid-refusal';
import type { GridViewRead } from '../src/dom/grid-view';
import type { SelectionRange } from '../src/selection/range';
import type { GridSelection } from '../src/selection/selection';
import type { GridSource, GridSourceState } from '../src/source/grid-source';
import type { RowRange } from '../src/viewport/row-range';
import { mount, painted, q, trades, unmountAll, type Trade } from './grid-harness';

const columns: readonly GridColumn<Trade>[] = [
    { name: 'Book', type: 'text', value: trade => trade.book, width: 120, editable: true },
    { name: 'Notional', type: 'number', value: trade => trade.notional, width: 100, editable: true },
];

// As a Sheet names cells, and where a Reference can go: after `=`, an operator, `(` or `,` in a Formula.
const referenceText = (range: SelectionRange): string => `${String.fromCharCode(65 + range.leftColumn)}${String(range.topRow + 1)}`;
const pointAt = (text: string, caret: number): boolean => text.startsWith('=') && '=+-*/(,'.includes(text.charAt(caret - 1));

function press(root: HTMLElement, row: number, column: number): void {
    for (const name of ['mousedown', 'mouseup']) {
        const event = new MouseEvent(name, { bubbles: true, cancelable: true, button: 0, buttons: name === 'mouseup' ? 0 : 1 });
        Object.defineProperty(event, 'offsetX', { value: (column * 120) + 10 });
        Object.defineProperty(event, 'offsetY', { value: (row * 24) + 5 });
        q(root, '.ex-viewport').dispatchEvent(event);
        if (name === 'mousedown' && !event.defaultPrevented) q(root, '.ex-scroller').focus();
    }
}

function key(name: string, init: KeyboardEventInit = {}): KeyboardEvent {
    const target = document.activeElement;
    if (target === null) throw new Error('Nothing holds the keyboard.');
    const event = new KeyboardEvent('keydown', { key: name, bubbles: true, cancelable: true, ...init });
    target.dispatchEvent(event);
    target.dispatchEvent(new KeyboardEvent('keyup', { key: name, bubbles: true }));
    return event;
}

const editor = (root: HTMLElement): HTMLInputElement => q(root, 'input.ex-editor') as HTMLInputElement;
const a1 = { range: { topRow: 0, leftColumn: 0, rowCount: 1, columnCount: 1 }, focus: { row: 0, column: 0 } };

afterEach(() => { unmountAll(); });

/** One source over the set of rows shown, as a Sheet's is: no Row Key, the whole set its Window, and a new version
 * whenever another set is shown. */
class SetsSource implements GridSource<Trade> {
    readonly #listeners = new Set<() => void>();
    #rows: readonly Trade[];
    #version = 0;

    constructor(rows: readonly Trade[]) {
        this.#rows = rows;
    }

    show(rows: readonly Trade[]): void {
        this.#rows = rows;
        this.#version += 1;
        for (const listener of this.#listeners) listener();
    }

    subscribe(listener: () => void): () => void {
        this.#listeners.add(listener);
        return () => { this.#listeners.delete(listener); };
    }

    snapshot(): GridSourceState<Trade> {
        return { window: this.#rows, windowStart: 0, totalCount: this.#rows.length, loading: false, rowSequenceVersion: this.#version, sorts: [], filter: undefined };
    }

    setColumns(): void { /* nothing is queried */ }
    setSorts(): void { throw new Error('The sets are not sorted.'); }
    setFilter(): void { throw new Error('The sets are not filtered.'); }
    needRange(): Promise<void> { return Promise.resolve(); }
    rows(range: RowRange): Promise<readonly Trade[]> { return Promise.resolve(this.#rows.slice(range.start, range.start + range.count)); }
    distinctValues(): Promise<never> { return Promise.reject(new Error('The sets have no value lists.')); }
}

/** One grid over two sets of rows under one source, as a Workbook's two Sheets: `away` shows the other set, new to the
 * grid, with A1 selected, and `home` the first again with the view read as it was left. */
async function twoSets(): Promise<{
    readonly root: HTMLElement; readonly view: ReturnType<typeof mount<Trade>>['view']; readonly edits: GridEditIntent<Trade>[];
    readonly refusals: GridRefusal[]; readonly selections: GridSelection[]; readonly away: () => GridViewRead; readonly home: (read: GridViewRead) => void;
}> {
    const first = trades(20);
    const other = trades(20).map(trade => ({ ...trade, book: `Other-${trade.book}` }));
    const source = new SetsSource(first);
    const edits: GridEditIntent<Trade>[] = [];
    const refusals: GridRefusal[] = [];
    const selections: GridSelection[] = [];
    const { root, view } = mount<Trade>({
        columns, source, rowHeight: 24, viewportHeight: 480, pointAt, referenceText,
        onEdit: intent => { edits.push(intent); }, onPaste: (_intent: GridPasteIntent) => undefined,
        onRefused: refusal => { refusals.push(refusal); }, onSelectionChanged: selection => { selections.push(selection); },
    });
    await painted();
    const away = (): GridViewRead => {
        const read = view.readView();
        source.show(other);
        view.showNewView(source.snapshot().rowSequenceVersion, a1);
        return read;
    };
    const home = (read: GridViewRead): void => {
        source.show(first);
        view.showViewWith(read, source.snapshot().rowSequenceVersion);
    };
    return { root, view, edits, refusals, selections, away, home };
}

describe('an edit away among another set of rows (ADR-0331 §5)', () => {
    it('ADR-0331 §5: an edit open as another set is shown stays open, unseen with the keyboard, and its commit lands on the row it opened on', async () => {
        const grid = await twoSets();
        press(grid.root, 2, 0);
        await painted();
        key('W');
        grid.away();
        await painted();
        expect([editor(grid.root).classList.contains('ex-editor-away'), document.activeElement === editor(grid.root), grid.view.readEditText()])
            .toEqual([true, true, 'W']);
        expect(defined(grid.selections.at(-1), 'the Selection shown').focus).toEqual({ row: 0, column: 0 });
        key('Enter');
        await painted();
        const intent = defined(grid.edits[0], 'the intent');
        expect([intent.row.book, intent.column, intent.value, intent.replacedText]).toEqual(['Book-2', 'Book', 'W', 'Book-2']);
        expect(grid.view.readEditText()).toBe(undefined);
    });

    it('ADR-0331 §5: its own set\'s view handed back brings the editor back over its cell, and the commit moves from there', async () => {
        const grid = await twoSets();
        press(grid.root, 2, 0);
        await painted();
        key('W');
        const read = grid.away();
        await painted();
        grid.home(read);
        await painted();
        expect([editor(grid.root).classList.contains('ex-editor-away'), editor(grid.root).getAttribute('data-ex-cell')]).toEqual([false, '2 0']);
        key('Enter');
        await painted();
        expect([defined(grid.edits[0], 'the intent').row.book, defined(grid.selections.at(-1), 'the Selection').focus]).toEqual(['Book-2', { row: 3, column: 0 }]);
    });

    it('ADR-0331 §5: away, Point points from the Focus of the rows shown, and an outline left among the rows replaced is pointed afresh in its place', async () => {
        const grid = await twoSets();
        press(grid.root, 2, 0);
        await painted();
        key('=');
        key('ArrowDown');
        await painted();
        expect(editor(grid.root).value).toBe('=A4');
        grid.away();
        await painted();
        // The outline stood over the rows left: the next arrow points from A1 of the rows shown, over what was written.
        key('ArrowDown');
        await painted();
        expect(editor(grid.root).value).toBe('=A2');
        press(grid.root, 5, 1);
        await painted();
        expect([editor(grid.root).value, defined(grid.selections.at(-1), 'the Selection').focus]).toEqual(['=B6', { row: 0, column: 0 }]);
    });

    it('ADR-0331 §5: away, a Ctrl+Enter fill is refused as aimed under another order, and the edit stays', async () => {
        const grid = await twoSets();
        press(grid.root, 2, 0);
        await painted();
        key('W');
        grid.away();
        await painted();
        key('Enter', { ctrlKey: true });
        await painted();
        expect([grid.refusals, grid.view.readEditText()]).toEqual([[{ kind: 'paste', reason: 'orderMoved' }], 'W']);
    });

    it('ADR-0331: a new view places what it is handed once its version is taken, and refuses a Focus outside its range', async () => {
        const grid = await twoSets();
        expect(() => { grid.view.showNewView(0, { range: a1.range, focus: { row: 1, column: 0 } }); }).toThrow(RangeError);
        grid.away();
        await painted();
        expect(defined(grid.selections.at(-1), 'the Selection shown').focus).toEqual({ row: 0, column: 0 });
    });
});
