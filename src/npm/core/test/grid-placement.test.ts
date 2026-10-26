// @vitest-environment happy-dom
// Layer 2 (ADR-0350 section 13): a Consumer's placement of the Selection (ADR-0050, item 4), as the C# grid's
// PlaceSelectionAsync places it. The grid places the range and the Focus as a click leaves them, after an open
// edit commits, and refuses a request written under an order or a source it no longer shows (ADR-0011,
// ADR-0142).
import { describe, expect, it } from 'vitest';
import { defined } from '../src/must';
import type { GridEditIntent } from '../src/cells/edit-intent';
import type { GridClearIntent } from '../src/clipboard/clear-intent';
import type { GridPasteIntent } from '../src/clipboard/paste-intent';
import type { GridColumn } from '../src/dom/grid-column';
import type { GridContextMenuContext } from '../src/dom/grid-command';
import type { GridOptions } from '../src/dom/grid-view';
import type { GridRefusal } from '../src/dom/grid-refusal';
import type { GridSelection } from '../src/selection/selection';
import { GridSource } from '../src/source/grid-source';
import { mount, painted, q, trades, type Trade } from './grid-harness';

// Book edits; Notional rejects what is not a number.
const columns: readonly GridColumn<Trade>[] = [
    { name: 'Book', type: 'text', value: trade => trade.book, width: 120, editable: true },
    {
        name: 'Notional', type: 'number', value: trade => trade.notional, width: 100, editable: true,
        validate: (_row, text) => (Number.isNaN(Number(text)) ? { kind: 'reject', message: `'${text}' is not a number` } : { kind: 'accept' }),
    },
];

const rowY = (row: number): number => (row * 24) + 5;

function press(root: HTMLElement, x: number, y: number): void {
    for (const name of ['mousedown', 'mouseup']) {
        const event = new MouseEvent(name, { bubbles: true, cancelable: true, button: 0, buttons: name === 'mouseup' ? 0 : 1 });
        Object.defineProperty(event, 'offsetX', { value: x });
        Object.defineProperty(event, 'offsetY', { value: y });
        q(root, '.ex-viewport').dispatchEvent(event);
        if (name === 'mousedown' && !event.defaultPrevented) q(root, '.ex-scroller').focus();
    }
}

function key(name: string, init: KeyboardEventInit = {}): void {
    const target = document.activeElement;
    if (target === null) throw new Error('Nothing holds the keyboard.');
    target.dispatchEvent(new KeyboardEvent('keydown', { key: name, bubbles: true, cancelable: true, ...init }));
}

function heard(): {
    readonly edits: GridEditIntent<Trade>[]; readonly refusals: GridRefusal[]; readonly selections: GridSelection[];
    readonly options: Pick<GridOptions<Trade>, 'onEdit' | 'onRefused' | 'onSelectionChanged'>;
} {
    const edits: GridEditIntent<Trade>[] = [];
    const refusals: GridRefusal[] = [];
    const selections: GridSelection[] = [];
    return {
        edits, refusals, selections,
        options: {
            onEdit: intent => { edits.push(intent); },
            onRefused: refusal => { refusals.push(refusal); },
            onSelectionChanged: selection => { selections.push(selection); },
        },
    };
}

const range = (topRow: number, leftColumn: number, rowCount: number, columnCount: number) => ({ topRow, leftColumn, rowCount, columnCount });
const hasEditor = (root: HTMLElement): boolean => root.querySelector('.ex-editor') !== null;

describe('a placement of the Selection', () => {
    it('ADR-0050: the range and the Focus are placed as a click leaves them, heard once, and the Focus is scrolled into view', async () => {
        const listener = heard();
        const { root, view } = mount({ columns, rows: trades(1000), rowHeight: 24, viewportHeight: 480, ...listener.options });
        await painted();
        expect(view.placeSelection(range(500, 0, 3, 2), { row: 501, column: 1 }, 0)).toBe(true);
        await painted();
        const selection = defined(listener.selections.at(-1), 'the Selection heard');
        expect([selection.ranges, selection.focus, listener.selections.length]).toEqual([[range(500, 0, 3, 2)], { row: 501, column: 1 }, 1]);
        // On a grid that edits the Keyboard Field names the Focus (ADR-0080).
        expect(q(root, 'input.ex-key-field').getAttribute('aria-activedescendant')).toMatch(/r501c1$/);
        expect(q(root, '.ex-scroller').scrollTop).toBeGreaterThan(0);
        expect(listener.refusals).toEqual([]);
    });

    it('ADR-0050, ADR-0011: a request under another Row Sequence Version places nothing and is refused as the order moved', async () => {
        const listener = heard();
        const source = GridSource.from(trades(20), { rowKey: trade => trade.book });
        const { view } = mount({ columns, source, rowHeight: 24, viewportHeight: 480, ...listener.options });
        await painted();
        // A row deleted moves the order: every position after it names another row.
        source.replaceAll(trades(20).slice(1));
        await painted();
        expect(view.placeSelection(range(2, 0, 1, 1), { row: 2, column: 0 }, 0)).toBe(false);
        await painted();
        expect([listener.refusals, listener.selections]).toEqual([[{ kind: 'placement', reason: 'orderMoved' }], []]);
    });

    it('ADR-0050: a range outside the grid, or a Focus outside its range, throws and changes nothing', async () => {
        const listener = heard();
        const { view } = mount({ columns, rows: trades(20), rowHeight: 24, viewportHeight: 480, ...listener.options });
        await painted();
        expect(() => view.placeSelection(range(19, 0, 2, 1), { row: 19, column: 0 }, 0)).toThrow(RangeError);
        expect(() => view.placeSelection(range(0, 1, 1, 2), { row: 0, column: 1 }, 0)).toThrow(RangeError);
        expect(() => view.placeSelection(range(0, 0, 2, 1), { row: 3, column: 0 }, 0)).toThrow(/inside the range/);
        // Whatever else holds: a stale version does not hide the Consumer's mistake behind a refusal.
        expect(() => view.placeSelection(range(19, 0, 2, 1), { row: 19, column: 0 }, 7)).toThrow(RangeError);
        await painted();
        expect([listener.selections, listener.refusals]).toEqual([[], []]);
    });

    it('ADR-0050, ADR-0007: an open edit commits first, as a click away commits it, and then the range is placed', async () => {
        const listener = heard();
        const { root, view } = mount({ columns, rows: trades(20), rowHeight: 24, viewportHeight: 480, ...listener.options });
        await painted();
        press(root, 10, rowY(1));
        await painted();
        key('N');
        expect(hasEditor(root)).toBe(true);
        expect(view.placeSelection(range(5, 1, 1, 1), { row: 5, column: 1 }, 0)).toBe(true);
        await painted();
        expect([listener.edits.map(edit => edit.value), hasEditor(root)]).toEqual([['N'], false]);
        expect(defined(listener.selections.at(-1), 'the Selection heard').focus).toEqual({ row: 5, column: 1 });
    });

    it('ADR-0050, ADR-0034: a Reject that holds the open edit holds the placement too, which places nothing', async () => {
        const listener = heard();
        const { root, view } = mount({ columns, rows: trades(20), rowHeight: 24, viewportHeight: 480, ...listener.options });
        await painted();
        press(root, 130, rowY(0));
        await painted();
        key('a');
        expect(view.placeSelection(range(5, 0, 1, 1), { row: 5, column: 0 }, 0)).toBe(false);
        await painted();
        expect([listener.edits, hasEditor(root), listener.refusals]).toEqual([[], true, []]);
        expect(defined(listener.selections.at(-1), 'the Selection heard').focus).toEqual({ row: 0, column: 1 });
    });

    it('ADR-0050, ADR-0142: an edit whose commit hands over another source makes the request stale, refused as the source changed', async () => {
        const listener = heard();
        // The grid, for the handler to hand it another source once it is mounted.
        const held: { view?: ReturnType<typeof mount<Trade>>['view'] } = {};
        const options: GridOptions<Trade> = {
            columns, source: GridSource.from(trades(20)), rowHeight: 24, viewportHeight: 480, ...listener.options,
            onEdit: intent => {
                listener.edits.push(intent);
                defined(held.view, 'the grid').update({ ...options, source: GridSource.from(trades(20)) });
            },
        };
        const { root, view } = mount(options);
        held.view = view;
        await painted();
        press(root, 10, rowY(1));
        await painted();
        key('N');
        expect(view.placeSelection(range(5, 0, 1, 1), { row: 5, column: 0 }, 0)).toBe(false);
        expect(listener.refusals).toEqual([{ kind: 'placement', reason: 'sourceChanged' }]);
    });

    it('ADR-0050, ADR-0011: an edit whose commit moves the columns makes the request stale, refused as the columns moved', async () => {
        const listener = heard();
        const held: { view?: ReturnType<typeof mount<Trade>>['view'] } = {};
        const options: GridOptions<Trade> = {
            columns, rows: trades(20), rowHeight: 24, viewportHeight: 480, ...listener.options,
            onEdit: intent => {
                listener.edits.push(intent);
                defined(held.view, 'the grid').update({ ...options, columns: columns.toReversed() });
            },
        };
        const { root, view } = mount(options);
        held.view = view;
        await painted();
        press(root, 10, rowY(1));
        await painted();
        key('N');
        expect(view.placeSelection(range(5, 0, 1, 1), { row: 5, column: 0 }, 0)).toBe(false);
        expect(listener.refusals).toEqual([{ kind: 'placement', reason: 'columnsMoved' }]);
    });

    it('ADR-0050, ADR-0142: after the source is replaced, the grid hands the new source\'s own version, and placeSelection takes it', async () => {
        const listener = heard();
        const cleared: GridClearIntent[] = [];
        const pasted: GridPasteIntent[] = [];
        const contexts: GridContextMenuContext<Trade>[] = [];
        const options: GridOptions<Trade> = {
            columns, source: GridSource.from(trades(20)), rowHeight: 24, viewportHeight: 480, ...listener.options,
            onClear: intent => { cleared.push(intent); },
            onPaste: intent => { pasted.push(intent); },
            contextCommands: context => { contexts.push(context); return []; },
        };
        const { root, view } = mount(options);
        await painted();
        const next = GridSource.from(trades(30));
        view.update({ ...options, source: next });
        await painted();
        // Sorted twice, the new source stands at a version the grid's own count, carried past the old source,
        // does not: what the grid hands is told apart from what it judges by.
        next.setSorts([{ column: 'Book', direction: 'descending' }]);
        next.setSorts([{ column: 'Book', direction: 'ascending' }]);
        await painted();
        const own = next.snapshot().rowSequenceVersion;
        expect(own).toBe(2);
        // What a Consumer is handed is what the source says, as the C# grid hands it: the version the attribute,
        // the intents, the menu's context and a view read carry is the one placeSelection compares.
        press(root, 10, rowY(1));
        key('ArrowDown', { shiftKey: true });
        await painted();
        key('Delete');
        key('d', { ctrlKey: true });
        const clipboard = new Event('paste', { bubbles: true, cancelable: true });
        Object.defineProperty(clipboard, 'clipboardData', { value: { getData: (type: string) => (type === 'text/plain' ? 'X' : '') } });
        document.activeElement?.dispatchEvent(clipboard);
        const menu = new MouseEvent('contextmenu', { bubbles: true, cancelable: true, button: 2 });
        Object.defineProperty(menu, 'offsetX', { value: 10 });
        Object.defineProperty(menu, 'offsetY', { value: rowY(1) });
        q(root, '.ex-viewport').dispatchEvent(menu);
        await painted();
        expect([
            Number(q(root, '.ex-viewport').getAttribute('data-ex-sequence')),
            defined(cleared[0], 'the clear').rowSequenceVersion,
            ...pasted.map(intent => intent.rowSequenceVersion),
            defined(contexts[0], 'the menu\'s context').rowSequenceVersion,
            view.readView().rowSequenceVersion,
        ]).toEqual([own, own, own, own, own, own]);
        expect(view.placeSelection(range(25, 0, 1, 1), { row: 25, column: 0 }, own)).toBe(true);
        await painted();
        expect([listener.refusals, defined(listener.selections.at(-1), 'the Selection heard').focus])
            .toEqual([[], { row: 25, column: 0 }]);
    });

    it('ADR-0050: a grid with no rows has no cell to place on, and says so', async () => {
        const { view } = mount({ columns, rows: [], rowHeight: 24, viewportHeight: 480 });
        await painted();
        expect(() => view.placeSelection(range(0, 0, 1, 1), { row: 0, column: 0 }, 0)).toThrow(/no cells/);
    });

    it('ADR-0050: a destroyed grid places nothing, and says so', async () => {
        const { view } = mount({ columns, rows: trades(20), rowHeight: 24, viewportHeight: 480 });
        await painted();
        view.destroy();
        expect(() => view.placeSelection(range(0, 0, 1, 1), { row: 0, column: 0 }, 0)).toThrow(/destroyed/);
    });
});
