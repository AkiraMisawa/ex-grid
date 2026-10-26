// @vitest-environment happy-dom
// Layer 2 (ADR-0350 section 13): the Mark Column in the DOM. A row's checkbox and the header's, Space over a
// Selection in the Mark Column, the count beside the grid, and a press judged against what it was made on
// (ADR-0043, MK-1 to MK-5, MK-9).
import { describe, expect, it } from 'vitest';
import { defined } from '../src/must';
import { markColumn, resolveColumns, type GridColumn } from '../src/dom/grid-column';
import type { GridRefusal } from '../src/dom/grid-refusal';
import { rowsSelectedIn } from '../src/dom/grid-marks';
import type { RowMarkIntent, RowMarks } from '../src/rows/row-marks';
import { selectionRange } from '../src/selection/range';
import { GridSource } from '../src/source/grid-source';
import type { InMemoryGridSource } from '../src/source/in-memory-source';
import { columns, mount, painted, q, rowsOf, trades, type Trade } from './grid-harness';

const mark = markColumn<Trade>({ width: 40 });
const marked: readonly GridColumn<Trade>[] = [mark, ...columns];

// Mark 0-40, Book 40-160, Notional 160-260; rows of 24 px under a header of 24.
const rowY = (row: number): number => (row * 24) + 5;

function press(root: HTMLElement, left: number, row: number, init: MouseEventInit = {}): void {
    const event = new MouseEvent('mousedown', { bubbles: true, cancelable: true, button: 0, buttons: 1, ...init });
    Object.defineProperty(event, 'offsetX', { value: left });
    Object.defineProperty(event, 'offsetY', { value: rowY(row) });
    q(root, '.ex-viewport').dispatchEvent(event);
}

function key(target: HTMLElement, name: string, init: KeyboardEventInit = {}): KeyboardEvent {
    const event = new KeyboardEvent('keydown', { key: name, bubbles: true, cancelable: true, ...init });
    target.dispatchEvent(event);
    return event;
}

function mouse(target: Element, type: string): MouseEvent {
    const event = new MouseEvent(type, { bubbles: true, cancelable: true, button: 0, buttons: type === 'mousedown' ? 1 : 0 });
    target.dispatchEvent(event);
    return event;
}

function click(target: Element): MouseEvent {
    const down = mouse(target, 'mousedown');
    mouse(target, 'mouseup');
    mouse(target, 'click');
    return down;
}

const prefix = (root: HTMLElement): string => root.id.replace(/root$/, '');
const boxAt = (root: HTMLElement, row: number): HTMLElement => q(root, `#${prefix(root)}r${row}c0 .ex-mark`);
const headerBox = (root: HTMLElement): HTMLElement => q(root, '.ex-header .ex-mark');
const checked = (root: HTMLElement, rows: number): (string | null)[] =>
    Array.from({ length: rows }, (_, row) => boxAt(root, row).getAttribute('aria-checked'));
const count = (root: HTMLElement): string | null | undefined => root.querySelector('.ex-status .ex-mark-count')?.textContent;

/** A source whose marks tell every intent they are handed, before applying it. */
function spiedSource(rows: readonly Trade[], options: Parameters<typeof GridSource.from<Trade>>[1] = {}): {
    readonly source: InMemoryGridSource<Trade>; readonly intents: RowMarkIntent<Trade>[];
} {
    const source = GridSource.from(rows, options);
    const intents: RowMarkIntent<Trade>[] = [];
    const { marks } = source;
    const apply = marks.onMarkIntent.bind(marks);
    marks.onMarkIntent = intent => {
        intents.push(intent);
        return apply(intent);
    };
    return { source, intents };
}

function refusals(): { readonly heard: GridRefusal<Trade>[]; readonly onRefused: (refusal: GridRefusal<Trade>) => void } {
    const heard: GridRefusal<Trade>[] = [];
    return { heard, onRefused: refusal => { heard.push(refusal); } };
}

describe('The Mark Column', () => {
    it('ADR-0043: a Mark Column has no value, at most one stands, and somebody holds its marks', () => {
        const [resolved] = resolveColumns([mark]);
        expect(defined(resolved, 'the column')).toMatchObject({ mark: true, paintsValue: false });
        expect(mark).toMatchObject({ name: 'Mark', header: '', queryable: false, align: 'center' });
        expect(() => resolveColumns([mark, markColumn<Trade>({ name: 'Again' })])).toThrow(/both Mark Columns/);
        expect(() => resolveColumns<Trade>([{ ...mark, editable: true }])).toThrow(/markColumn/);
        expect(() => resolveColumns<Trade>([{ ...mark, queryable: true }])).toThrow(/markColumn/);

        const unmarked: GridSource<Trade> = GridSource.fetch<Trade>(() => Promise.resolve({ rows: [], start: 0, totalCount: 0 }));
        expect(() => mount({ columns: marked, source: unmarked })).toThrow(/nobody holds the marks/);
        const source = GridSource.from(trades(5));
        expect(() => mount({ columns: marked, source, marks: GridSource.from(trades(5)).marks })).toThrow(/silently win/);
        // Over rows the grid holds, made again for every new array, marks it kept would go without a word.
        expect(() => mount({ columns: marked, rows: trades(5) })).toThrow(/marks would go with them/);
        // A holder that declares no counts at all is not one, as an untyped Consumer may hand it.
        const countless: unknown = { isMarked: () => false, onMarkIntent: () => Promise.resolve(), setRowKind: () => undefined, subscribe: () => () => undefined };
        expect(() => mount({ columns: marked, rows: trades(5), marks: countless as RowMarks<Trade> })).toThrow(/holds the Row Marks/);
    });

    it('ADR-0043: a row\'s checkbox is the grid\'s own, out of the tab sequence, never focused by a press, and named', async () => {
        const { root, view } = mount({ columns: marked, source: GridSource.from(trades(50)), rowHeight: 24, viewportHeight: 480 });
        await painted();
        expect(boxAt(root, 0).outerHTML)
            .toBe('<button type="button" class="ex-mark" role="checkbox" data-ex-claims="click" tabindex="-1" aria-checked="false" aria-label="Mark row"></button>');
        expect(mouse(boxAt(root, 0), 'mousedown').defaultPrevented).toBe(true);
        expect(headerBox(root).getAttribute('aria-label')).toBe('Mark all rows');
        expect(headerBox(root).getAttribute('aria-checked')).toBe('false');
        // Nothing marked, nothing said.
        expect(count(root)).toBeUndefined();
        view.destroy();
    });

    it('ADR-0043, MK-3, MK-4: a checkbox marks its row in one intent, and only that row paints again', async () => {
        const { source, intents } = spiedSource(trades(50));
        const { root, view } = mount({ columns: marked, source, rowHeight: 24, viewportHeight: 480 });
        await painted();
        const before = rowsOf(root);
        click(boxAt(root, 3));
        await painted();
        expect(intents).toEqual([{ kind: 'oneRow', row: source.snapshot().window[3], marked: true }]);
        expect(checked(root, 5)).toEqual(['false', 'false', 'false', 'true', 'false']);
        const after = rowsOf(root);
        expect(after.filter((row, i) => row !== before[i])).toEqual([after[3]]);
        expect(headerBox(root).getAttribute('aria-checked')).toBe('mixed');
        expect(count(root)).toBe('1 marked');
        // Pressed again, it asks for the state it does not show.
        click(boxAt(root, 3));
        await painted();
        expect(intents.at(-1)).toMatchObject({ kind: 'oneRow', marked: false });
        expect(count(root)).toBeUndefined();
        view.destroy();
    });

    it('ADR-0043, MK-2: the header shows the counts the marks answer, never the Window, and claims nothing they did not say', async () => {
        const { source, intents } = spiedSource(trades(200));
        const { root, view } = mount({ columns: marked, source, rowHeight: 24, viewportHeight: 480 });
        await painted();
        // Every painted row marked, in a result that holds more: "some".
        const shown = rowsOf(root).length;
        expect(shown).toBeLessThan(200);
        key(root, ' ');
        press(root, 10, 0);
        press(root, 10, shown - 1, { shiftKey: true });
        key(root, ' ');
        await painted();
        expect(checked(root, shown).every(state => state === 'true')).toBe(true);
        expect(headerBox(root).getAttribute('aria-checked')).toBe('mixed');
        expect(count(root)).toBe(`${shown} marked`);
        // Pressing "some" marks all; pressing "all" unmarks all.
        click(headerBox(root));
        await painted();
        expect(intents.at(-1)).toEqual({ kind: 'allRows', marked: true, rowSequenceVersion: source.snapshot().rowSequenceVersion });
        expect(headerBox(root).getAttribute('aria-checked')).toBe('true');
        expect(count(root)).toBe('200 marked');
        click(headerBox(root));
        await painted();
        expect(intents.at(-1)).toMatchObject({ kind: 'allRows', marked: false });
        expect(headerBox(root).getAttribute('aria-checked')).toBe('false');
        view.destroy();

        // Counts not yet known: the header claims neither none nor all, and nothing is said.
        const unknown: RowMarks<Trade> = {
            isMarked: () => false, counts: undefined, onMarkIntent: () => Promise.resolve(), setRowKind: () => undefined,
            subscribe: () => () => undefined,
        };
        const other = mount({ columns: marked, rows: trades(5), marks: unknown });
        await painted();
        expect(headerBox(other.root).getAttribute('aria-checked')).toBe('mixed');
        expect(count(other.root)).toBeUndefined();
        other.view.destroy();
    });

    it('ADR-0043, MK-1, MK-3: Space in the Mark Column brings the selected rows into line in one intent; elsewhere it marks nothing', async () => {
        const { source, intents } = spiedSource(trades(100_000));
        const { root, view } = mount({ columns: marked, source, rowHeight: 24, viewportHeight: 480 });
        await painted();
        click(boxAt(root, 2));
        // Rows 1-3, row 2 marked: a mixed block, all marked.
        press(root, 10, 1);
        press(root, 10, 3, { shiftKey: true });
        key(root, ' ');
        await painted();
        expect(intents.at(-1)).toEqual({ kind: 'positions', ranges: [{ start: 1, count: 3 }], rowSequenceVersion: 0 });
        expect(checked(root, 5)).toEqual(['false', 'true', 'true', 'true', 'false']);
        // All marked: Space unmarks them all.
        key(root, ' ');
        await painted();
        expect(checked(root, 5)).toEqual(['false', 'false', 'false', 'false', 'false']);

        // A Selection reaching into the Mark Column from a data cell marks nothing.
        const asked = intents.length;
        press(root, 50, 1);
        press(root, 10, 3, { shiftKey: true });
        key(root, ' ');
        await painted();
        expect(intents).toHaveLength(asked);

        // Ctrl+A then Space in the Mark Column is the header's gesture, by key: one intent over every row.
        press(root, 10, 0);
        key(root, 'a', { ctrlKey: true });
        key(root, ' ');
        await painted();
        expect(intents.slice(asked)).toEqual([{ kind: 'positions', ranges: [{ start: 0, count: 100_000 }], rowSequenceVersion: 0 }]);
        expect(count(root)).toBe('100000 marked');
        view.destroy();
    });

    it('ADR-0043: the rows Space names are each selected row once, ranges that overlap or touch made one', () => {
        expect(rowsSelectedIn([
            selectionRange(5, 0, 3, 2), selectionRange(0, 0, 2, 1), selectionRange(2, 1, 3, 1), selectionRange(9, 1, 1, 1),
            selectionRange(20, 3, 4, 1),
        ], 1)).toEqual([{ start: 2, count: 6 }, { start: 9, count: 1 }]);
        expect(rowsSelectedIn([selectionRange(0, 2, 5, 2)], 1)).toEqual([]);
    });

    it('ADR-0043, MK-5: marks survive a sort, on the same rows at their new places; the Selection does not', async () => {
        const { source } = spiedSource(trades(10));
        const { root, view } = mount({ columns: marked, source, rowHeight: 24, viewportHeight: 480 });
        await painted();
        click(boxAt(root, 1));
        press(root, 50, 4);
        await painted();
        expect(root.getAttribute('aria-activedescendant')).toBe(`${prefix(root)}r4c1`);
        source.setSorts([{ column: 'Notional', direction: 'descending' }]);
        await painted();
        // Book-1 is the ninth of ten now.
        expect(checked(root, 10)).toEqual(['false', 'false', 'false', 'false', 'false', 'false', 'false', 'false', 'true', 'false']);
        expect(source.marks.markedRows.map(({ book }) => book)).toEqual(['Book-1']);
        expect(root.hasAttribute('aria-activedescendant')).toBe(false);
        view.destroy();
    });

    it('MK-9: a row\'s checkbox marks its row by identity across an order move; the header\'s refuses an order moved since', async () => {
        const { source, intents } = spiedSource(trades(50));
        const listener = refusals();
        const { root, view } = mount({ columns: marked, source, rowHeight: 24, viewportHeight: 480, ...listener });
        await painted();
        const box = boxAt(root, 2);
        mouse(box, 'mousedown');
        source.setSorts([{ column: 'Notional', direction: 'descending' }]);
        mouse(box, 'mouseup');
        mouse(box, 'click');
        await painted();
        expect(source.marks.markedRows.map(({ book }) => book)).toEqual(['Book-2']);

        const header = headerBox(root);
        mouse(header, 'mousedown');
        source.setSorts([]);
        mouse(header, 'mouseup');
        mouse(header, 'click');
        await painted();
        expect(listener.heard).toEqual([{ kind: 'mark', reason: 'orderMoved' }]);
        expect(intents).toHaveLength(1);
        expect(source.marks.markedRows.map(({ book }) => book)).toEqual(['Book-2']);
        // A click no press came before, a script's, names what the header names now.
        mouse(header, 'click');
        await painted();
        expect(intents.at(-1)).toMatchObject({ kind: 'allRows', marked: true });
        view.destroy();
    });

    it('MK-9: a press made on what a replaced source painted marks nothing and is refused, a row\'s said aloud', async () => {
        const listener = refusals();
        const options = { columns: marked, rowHeight: 24, viewportHeight: 480, ...listener };
        const first = spiedSource(trades(50));
        const { root, view, told } = mount({ ...options, source: first.source });
        await painted();
        const box = boxAt(root, 1);
        const header = headerBox(root);
        mouse(box, 'mousedown');
        const second = spiedSource(trades(50));
        view.update({ ...options, source: second.source });
        mouse(box, 'mouseup');
        mouse(box, 'click');
        mouse(header, 'mousedown');
        view.update({ ...options, source: first.source });
        mouse(header, 'mouseup');
        mouse(header, 'click');
        await painted();
        expect(listener.heard).toEqual([{ kind: 'mark', reason: 'sourceChanged' }, { kind: 'mark', reason: 'sourceChanged' }]);
        expect([...first.intents, ...second.intents]).toEqual([]);
        told.frame();
        expect(q(root, '.ex-announce').textContent).toBe('The row is no longer available. Repeat the command on the current data.');
        view.destroy();
    });

    it('MK-9: a press whose checkbox a new version of its row took away marks once, at its release; one whose row left is refused', async () => {
        const rows = trades(50);
        const { source, intents } = spiedSource(rows);
        const { root, view } = mount({ columns: marked, source, rowHeight: 24, viewportHeight: 480 });
        await painted();
        const pressed = boxAt(root, 4);
        mouse(pressed, 'mousedown');
        const edited = { book: 'Book-4 (edited)', notional: 1 };
        source.replaceRow(defined(rows[4], 'row 4'), edited);
        await painted();
        expect(pressed.isConnected).toBe(false);
        // The browser clicks the nearest ancestor both events share, which is no checkbox.
        mouse(boxAt(root, 4), 'mouseup');
        mouse(q(root, '.ex-viewport'), 'click');
        await painted();
        expect(intents).toEqual([{ kind: 'oneRow', row: edited, marked: true }]);
        expect(boxAt(root, 4).getAttribute('aria-checked')).toBe('true');
        view.destroy();

        const keyed = spiedSource(trades(50), { rowKey: trade => trade.book });
        const listener = refusals();
        const other = mount({ columns: marked, source: keyed.source, rowHeight: 24, viewportHeight: 480, ...listener });
        await painted();
        const gone = boxAt(other.root, 7);
        mouse(gone, 'mousedown');
        keyed.source.apply({ removedKeys: ['Book-7'] });
        mouse(gone, 'mouseup');
        mouse(gone, 'click');
        expect(listener.heard).toEqual([{ kind: 'mark', reason: 'rowLeftTheWindow' }]);
        expect(keyed.intents).toEqual([]);
        other.view.destroy();
    });

    it('MK-9: a press whose checkbox an order move took away marks its row by identity at its release over another row\'s', async () => {
        const { source, intents } = spiedSource(trades(50));
        const listener = refusals();
        const { root, view } = mount({ columns: marked, source, rowHeight: 24, viewportHeight: 480, ...listener });
        await painted();
        const pressed = boxAt(root, 2);
        mouse(pressed, 'mousedown');
        source.setSorts([{ column: 'Notional', direction: 'descending' }]);
        await painted();
        expect(pressed.isConnected).toBe(false);
        // Released over the checkbox now standing there, another row's; the browser clicks no checkbox.
        mouse(boxAt(root, 2), 'mouseup');
        mouse(q(root, '.ex-viewport'), 'click');
        await painted();
        expect(intents).toHaveLength(1);
        expect(intents[0]).toMatchObject({ kind: 'oneRow', marked: true });
        expect(source.marks.markedRows.map(({ book }) => book)).toEqual(['Book-2']);
        expect(listener.heard).toEqual([]);
        view.destroy();
    });

    it('ADR-0043, principle 6: a row\'s checkbox asks for the state it showed at the press, whatever the marks answer since', async () => {
        const rows = trades(50);
        const { source, intents } = spiedSource(rows);
        const { root, view } = mount({ columns: marked, source, rowHeight: 24, viewportHeight: 480 });
        await painted();
        const box = boxAt(root, 3);
        expect(box.getAttribute('aria-checked')).toBe('false');
        mouse(box, 'mousedown');
        // Another holder of the marks marks the row before the press's click is heard, and before the grid
        // paints it: the in-memory marks apply it at once, and the pass that would paint it is still owed.
        void source.marks.onMarkIntent({ kind: 'oneRow', row: defined(rows[3], 'row 3'), marked: true });
        expect(box.isConnected).toBe(true);
        mouse(box, 'mouseup');
        mouse(box, 'click');
        await painted();
        expect(intents).toEqual([
            { kind: 'oneRow', row: rows[3], marked: true },
            { kind: 'oneRow', row: rows[3], marked: true },
        ]);
        expect(source.marks.markedRows.map(({ book }) => book)).toEqual(['Book-3']);
        view.destroy();
    });

    it('MK-9: a press on the header\'s checkbox released elsewhere is over, and a later click with no press names the header now', async () => {
        const { source, intents } = spiedSource(trades(50));
        const listener = refusals();
        const { root, view } = mount({ columns: marked, source, rowHeight: 24, viewportHeight: 480, ...listener });
        await painted();
        const header = headerBox(root);
        mouse(header, 'mousedown');
        // Dragged off and released on the header: the platform's cancel, and no click.
        mouse(q(root, '.ex-header'), 'mouseup');
        source.setSorts([{ column: 'Notional', direction: 'descending' }]);
        await painted();
        mouse(header, 'click');
        await painted();
        expect(listener.heard).toEqual([]);
        expect(intents).toEqual([{ kind: 'allRows', marked: true, rowSequenceVersion: source.snapshot().rowSequenceVersion }]);
        view.destroy();
    });
});
