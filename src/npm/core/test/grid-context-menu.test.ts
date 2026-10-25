// @vitest-environment happy-dom
// Layer 2 (ADR-0350 section 13): the Context Menu, as the C# core answers it (ADR-0036). A secondary click
// or the keyboard opens it over the Selection, its first item takes the keyboard and its keys are MenuKeys'
// table (ADR-0039), it stands inside the grid's box (ADR-0040), and however the core closes it the keyboard
// is back on the grid.
import { describe, expect, it } from 'vitest';
import { defined } from '../src/must';
import type { GridCommand, GridContextMenuContext } from '../src/dom/grid-command';
import type { GridOptions } from '../src/dom/grid-view';
import type { GridSelection } from '../src/selection/selection';
import type { GridRefusal } from '../src/dom/grid-refusal';
import { columns, mount, painted, q, trades, type Trade } from './grid-harness';

const base = { columns, rows: trades(100), rowHeight: 24, viewportHeight: 320, viewportWidth: 720 } as const;

// Rows of 24 px under a header of 24; Book is 120 px wide and Notional 100.
const rowY = (row: number): number => (row * 24) + 5;

function pointer(root: HTMLElement, type: string, x: number, y: number, init: MouseEventInit = {}): MouseEvent {
    const event = new MouseEvent(type, { bubbles: true, cancelable: true, ...init });
    Object.defineProperty(event, 'offsetX', { value: x });
    Object.defineProperty(event, 'offsetY', { value: y });
    q(root, '.ex-viewport').dispatchEvent(event);
    return event;
}
const press = (root: HTMLElement, x: number, y: number, init: MouseEventInit = {}): MouseEvent =>
    pointer(root, 'mousedown', x, y, { button: 0, ...init });
const secondary = (root: HTMLElement, x: number, y: number): MouseEvent => {
    pointer(root, 'mousedown', x, y, { button: 2 });
    return pointer(root, 'contextmenu', x, y, { button: 2 });
};

function key(target: Element, name: string, init: KeyboardEventInit = {}): KeyboardEvent {
    const event = new KeyboardEvent('keydown', { key: name, bubbles: true, cancelable: true, ...init });
    target.dispatchEvent(event);
    return event;
}
function active(): Element {
    const holder = document.activeElement;
    if (holder === null) throw new Error('Nothing holds the keyboard.');
    return holder;
}
const settle = (): Promise<void> => new Promise(resolve => { setTimeout(resolve, 0); });

const menu = (root: HTMLElement): HTMLElement | null => root.querySelector<HTMLElement>(':scope > .ex-popover[role=menu]');
const items = (root: HTMLElement): HTMLButtonElement[] => [...root.querySelectorAll<HTMLButtonElement>('.ex-popover button[role=menuitem]')];
const labels = (root: HTMLElement): (string | null)[] => items(root).map(item => item.textContent);
const item = (root: HTMLElement, label: string): HTMLButtonElement =>
    defined(items(root).find(found => found.textContent === label), `the item ${label}`);

function heard(options: Partial<GridOptions<Trade>> = {}): {
    readonly root: HTMLElement;
    readonly view: ReturnType<typeof mount<Trade>>['view'];
    readonly told: ReturnType<typeof mount<Trade>>['told'];
    readonly selection: () => GridSelection;
    readonly contexts: GridContextMenuContext<Trade>[];
    readonly ran: string[];
} {
    let selection: GridSelection | undefined;
    const contexts: GridContextMenuContext<Trade>[] = [];
    const ran: string[] = [];
    const commands = (context: GridContextMenuContext<Trade>): readonly GridCommand[] => {
        contexts.push(context);
        return [
            { id: 'audit', enabled: false, invoke: () => { ran.push('audit'); } },
            { id: 'open-trade', enabled: true, invoke: () => { ran.push(`open-trade, the keyboard on ${active().className}`); } },
        ];
    };
    const { root, view, told } = mount<Trade>({
        ...base, onSelectionChanged: next => { selection = next; }, contextCommands: commands, ...options,
    });
    return { root, view, told, selection: () => defined(selection, 'the Selection heard'), contexts, ran };
}

describe('opening the Context Menu', () => {
    it('CTX-1 / ADR-0036: a secondary click outside the Selection collapses it onto its cell first; inside, the Selection stands', async () => {
        const { root, selection } = heard();
        await painted();
        press(root, 10, rowY(1));
        press(root, 150, rowY(3), { shiftKey: true });
        await painted();
        const block = selection().ranges;
        const inside = secondary(root, 150, rowY(2));
        await painted();
        // The browser's own menu never appears over the rows.
        expect([inside.defaultPrevented, menu(root) !== null, selection().ranges]).toEqual([true, true, block]);
        secondary(root, 10, rowY(6));
        await painted();
        expect([selection().ranges, selection().focus]).toEqual([[{ topRow: 6, leftColumn: 0, rowCount: 1, columnCount: 1 }], { row: 6, column: 0 }]);
        expect(root.querySelectorAll('.ex-popover')).toHaveLength(1);
    });

    it('CTX-2 / CTX-3: the core\'s commands, then the Consumer\'s, which are handed the row, the column and the Selection as rectangles in their version', async () => {
        const { root, contexts } = heard();
        await painted();
        press(root, 10, rowY(1));
        press(root, 150, rowY(2), { shiftKey: true });
        secondary(root, 150, rowY(2));
        await painted();
        // A command the built-in table does not know is shown as its id; a disabled one is shown.
        expect(labels(root)).toEqual(['Copy', 'Copy with headers', 'audit', 'open-trade']);
        expect(items(root).map(found => found.disabled)).toEqual([false, false, true, false]);
        const context = defined(contexts[0], 'the context');
        expect([context.row, context.column, context.columnType, context.selection, context.commands.map(command => command.id)]).toEqual([
            { book: 'Book-2', notional: 2000 }, 'Notional', 'number', [{ topRow: 1, leftColumn: 0, rowCount: 2, columnCount: 2 }], ['copy', 'copy-with-headers'],
        ]);
        expect(typeof context.rowSequenceVersion).toBe('number');
    });

    it('ADR-0036 / ADR-0050 item 13: the Consumer words the commands, and may leave Copy with headers off', async () => {
        const { root } = heard({ commandLabel: id => (id === 'copy' ? 'コピー' : id === 'open-trade' ? 'Open trade' : undefined), hideCopyWithHeaders: true });
        await painted();
        secondary(root, 10, rowY(1));
        await painted();
        expect(labels(root)).toEqual(['コピー', 'audit', 'Open trade']);
    });

    it('CTX-4 / KB-29: the ContextMenu key and Shift+F10 open it at the Focus cell\'s corner, its first item takes the keyboard, and Escape gives the keyboard back', async () => {
        const { root } = heard();
        await painted();
        press(root, 150, rowY(2));
        await painted();
        root.focus();
        const opened = key(root, 'ContextMenu');
        await painted();
        expect([opened.defaultPrevented, menu(root)?.getAttribute('style')]).toEqual([true,
            'left: 120px; top: 72px; min-width: 200px; max-width: 320px; max-height: 248px']);
        expect(active()).toBe(item(root, 'Copy'));
        key(active(), 'Escape');
        await painted();
        expect([menu(root), active()]).toEqual([null, root]);
        expect(key(root, 'F10', { shiftKey: true }).defaultPrevented).toBe(true);
        await painted();
        expect(active()).toBe(item(root, 'Copy'));
        // Escape on the grid peels the menu before it leaves the grid (ADR-0012's layering).
        root.focus();
        key(root, 'Escape');
        await painted();
        expect([menu(root), root.querySelector('[data-ex-escape-leaves]') !== null]).toEqual([null, true]);
    });
});

describe('inside the Context Menu', () => {
    it('KB-30 / ADR-0039: the arrows, Home and End move among the enabled items and wrap; Enter closes it and gives the keyboard back before the command runs', async () => {
        const { root, ran } = heard();
        await painted();
        secondary(root, 10, rowY(1));
        await painted();
        const moves: (string | null)[] = [];
        for (const [name, init] of [['ArrowDown', {}], ['ArrowDown', {}], ['ArrowDown', {}], ['ArrowUp', {}], ['Home', {}], ['End', {}]] as const) {
            expect(key(active(), name, init).defaultPrevented).toBe(true);
            moves.push(active().textContent);
        }
        expect(moves).toEqual(['Copy with headers', 'open-trade', 'Copy', 'open-trade', 'Copy', 'open-trade']);
        key(active(), 'Enter');
        await painted();
        expect([menu(root), ran, active()]).toEqual([null, ['open-trade, the keyboard on ex-grid'], root]);
    });

    it('KB-30: Tab closes it as a Cancel, and Space runs the item as Enter does', async () => {
        const { root, ran } = heard();
        await painted();
        secondary(root, 10, rowY(1));
        key(active(), 'Tab');
        await painted();
        expect([menu(root), ran, active()]).toEqual([null, [], root]);
        secondary(root, 10, rowY(1));
        key(active(), 'End');
        key(active(), ' ');
        await painted();
        expect([menu(root), ran]).toEqual([null, ['open-trade, the keyboard on ex-grid']]);
    });

    it('ADR-0039: an item focused by a press that did not end in a click on it is the menu\'s place: Enter runs that item', async () => {
        const { root, ran } = heard();
        await painted();
        secondary(root, 10, rowY(1));
        await painted();
        item(root, 'open-trade').focus();
        key(active(), 'Enter');
        await painted();
        expect([menu(root), ran]).toEqual([null, ['open-trade, the keyboard on ex-grid']]);
    });

    it('KB-17 / ADR-0010: a press elsewhere in the instance dismisses it and keeps its own meaning; a click on an item runs it', async () => {
        const { root, ran, selection } = heard();
        await painted();
        secondary(root, 10, rowY(1));
        await painted();
        press(root, 150, rowY(5));
        await painted();
        expect([menu(root), ran, selection().focus]).toEqual([null, [], { row: 5, column: 1 }]);
        secondary(root, 10, rowY(1));
        item(root, 'audit').click();
        item(root, 'open-trade').click();
        // Gone inside the click, with the keyboard back on the grid: a ↓ typed straight after it is the grid's,
        // never the menu's, whatever renders afterwards (ADR-0044's note of 2026-10-12).
        expect([menu(root), ran, active()]).toEqual([null, ['open-trade, the keyboard on ex-grid'], root]);
        key(active(), 'ArrowDown');
        await painted();
        expect(selection().focus).toEqual({ row: 2, column: 0 });
    });
});

describe('what closes it besides', () => {
    it('ADR-0011: columns handed over in another order take the Selection, and the menu with it', async () => {
        const { root, view } = heard();
        await painted();
        secondary(root, 10, rowY(1));
        await painted();
        view.update({ ...base, columns: columns.toReversed() });
        await painted();
        expect([menu(root), active()]).toEqual([null, root]);
    });

    it('ADR-0018 / ADR-0039: a menu closed that way after the keyboard went to a field of the page\'s leaves the keyboard there', async () => {
        const { root, view } = heard();
        await painted();
        secondary(root, 10, rowY(1));
        await painted();
        const field = document.createElement('input');
        document.body.append(field);
        field.focus();
        view.update({ ...base, columns: columns.toReversed() });
        await painted();
        expect([menu(root), active()]).toEqual([null, field]);
        field.remove();
    });

    it('ADR-0040: it opens on the side of the pointer with more room and is bounded by it; a box shrunk below a row of room closes it', async () => {
        const { root, told } = heard({ viewportHeight: 'stretch' });
        told.viewport({ contentHeightPx: 320 });
        await painted();
        secondary(root, 10, rowY(9));
        await painted();
        // 24 + 221 down a 320 px box: more room above, so it hangs from the pointer.
        expect(menu(root)?.getAttribute('style')).toBe('left: 10px; top: 245px; transform: translateY(-100%); min-width: 200px; max-width: 320px; max-height: 245px');
        // Its room is the side it opened on, the header band included: it closes only once the whole box is
        // under a row.
        told.viewport({ contentHeightPx: 30 });
        await painted();
        expect(menu(root)).not.toBeNull();
        told.viewport({ contentHeightPx: 20 });
        await painted();
        expect([menu(root), active()]).toEqual([null, root]);
    });
});

describe('the menu\'s copy', () => {
    it('ADR-0005 / ADR-0036: Copy with headers writes the header row above the block by the asynchronous route, and counts it against the cap', async () => {
        const refusals: GridRefusal[] = [];
        const { root, told } = heard({ copyCellCap: 5, onRefused: refusal => { refusals.push(refusal); } });
        await painted();
        press(root, 10, rowY(1));
        press(root, 150, rowY(2), { shiftKey: true });
        secondary(root, 10, rowY(1));
        item(root, 'Copy').click();
        await settle();
        expect(told.clipboard.map(written => written.text.split('\r\n')[0])).toEqual(['Book-1\t1000']);
        // Two rows of two and the header row are six cells, past the cap of five: nothing is written.
        secondary(root, 10, rowY(1));
        item(root, 'Copy with headers').click();
        await settle();
        expect([told.clipboard.length, refusals]).toEqual([1, [{ kind: 'copy', reason: 'tooLarge' }]]);
    });

    it('ADR-0005: the header row is each column\'s header, in both flavours', async () => {
        const { root, told } = heard();
        await painted();
        press(root, 10, rowY(1));
        press(root, 150, rowY(2), { shiftKey: true });
        secondary(root, 10, rowY(1));
        item(root, 'Copy with headers').click();
        await settle();
        const written = defined(told.clipboard[0], 'the copy written');
        expect([written.text.split('\r\n').slice(0, 2), written.html.includes('<th>Book</th><th>Notional</th>')])
            .toEqual([['Book\tNotional', 'Book-1\t1000'], true]);
    });
});
