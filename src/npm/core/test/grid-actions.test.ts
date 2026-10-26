// @vitest-environment happy-dom
// Layer 2 (ADR-0350 section 13): Action and Template Columns in the DOM. The grid's own buttons, Space on
// their cells and Interactive inside a cell with several, a press resolved to its row or refused, and a
// Template cell's content mounted into a slot the core keeps (ADR-0020, ADR-0037, ADR-0142).
import { describe, expect, it } from 'vitest';
import { defined } from '../src/must';
import { actionColumn, resolveColumns, type GridActionEvent, type GridColumn, type GridTemplate } from '../src/dom/grid-column';
import type { GridRefusal } from '../src/dom/grid-refusal';
import { GridSource } from '../src/source/grid-source';
import { columns, mount, painted, q, rowsOf, scrollTo, trades, type Trade } from './grid-harness';

const open = actionColumn<Trade>('Open', [{ name: 'open', label: 'Open' }], { width: 80 });
const review = actionColumn<Trade>('Review', [
    { name: 'approve', label: 'Approve' }, { name: 'query', label: 'Query' }, { name: 'escalate', label: 'Escalate', className: 'icon-flag' },
], { width: 200 });

const emptyTemplate: GridTemplate<Trade> = () => () => undefined;

// Book 0-120, Notional 120-220, Open 220-300, Review 300-500, and a Template column after them at 500-600.
const x = { open: 230, review: 310, note: 510 } as const;
// Rows of 24 px under a header of 24.
const rowY = (row: number): number => (row * 24) + 5;

function press(root: HTMLElement, left: number, row: number): void {
    const event = new MouseEvent('mousedown', { bubbles: true, cancelable: true, button: 0, buttons: 1 });
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

/** A click on an element as the browser makes one: its press, its release and its click. */
function click(target: Element): MouseEvent {
    const down = mouse(target, 'mousedown');
    mouse(target, 'mouseup');
    mouse(target, 'click');
    return down;
}

const prefix = (root: HTMLElement): string => root.id.replace(/root$/, '');
const cellAt = (root: HTMLElement, row: number, column: number): HTMLElement => q(root, `#${prefix(root)}r${row}c${column}`);
const buttonsAt = (root: HTMLElement, row: number, column: number): HTMLButtonElement[] =>
    [...cellAt(root, row, column).querySelectorAll('button')];

function heard(): {
    readonly actions: string[]; readonly refusals: GridRefusal<Trade>[];
    readonly onAction: (event: GridActionEvent<Trade>) => void; readonly onRefused: (refusal: GridRefusal<Trade>) => void;
} {
    const actions: string[] = [];
    const refusals: GridRefusal<Trade>[] = [];
    return {
        actions, refusals,
        onAction: ({ row, column, action }) => { actions.push(`${column}.${action} on ${row.book}`); },
        onRefused: refusal => { refusals.push(refusal); },
    };
}

describe('Action Columns', () => {
    it('ADR-0020: an Action Column declares a non-empty list of named, labelled actions, and nothing a value would carry', () => {
        expect(() => actionColumn('Bad', [])).toThrow(TypeError);
        expect(() => actionColumn('Bad', [{ name: 'a', label: 'A' }, { name: 'a', label: 'Again' }])).toThrow(/two actions named 'a'/);
        expect(() => actionColumn('Bad', [{ name: 'a', label: '' }])).toThrow(/no label/);
        expect(() => resolveColumns<Trade>([{ name: 'Bad', type: 'text', value: () => 1, actions: [{ name: 'a', label: 'A' }] }]))
            .toThrow(/made by actionColumn/);
        expect(() => resolveColumns<Trade>([{ ...open, editable: true }])).toThrow(/editable/);
        expect(() => resolveColumns<Trade>([{ name: 'Note', type: 'text', value: trade => trade.book, template: emptyTemplate, editable: true }])).toThrow(/editable/);
        const [resolved] = resolveColumns([open]);
        expect(defined(resolved, 'the column').paintsValue).toBe(false);
        expect(open.queryable).toBe(false);
    });

    it('ADR-0020, ADR-0037: a button is the grid\'s own, out of the tab sequence, never focused by a press, and named by its label', async () => {
        const { root } = mount({ columns: [...columns, open, review], rows: trades(50), rowHeight: 24, viewportHeight: 480 });
        await painted();
        const [plain] = buttonsAt(root, 0, 2);
        const [, , icon] = buttonsAt(root, 0, 3);
        expect(defined(plain, 'Open').outerHTML).toBe('<button type="button" data-ex-claims="click" class="ex-action" tabindex="-1" title="Open">Open</button>');
        expect(defined(icon, 'Escalate').outerHTML)
            .toBe('<button type="button" data-ex-claims="click" class="ex-action icon-flag" tabindex="-1" title="Escalate" aria-label="Escalate"></button>');
        expect(mouse(defined(plain, 'Open'), 'mousedown').defaultPrevented).toBe(true);
        expect(cellAt(root, 0, 2).getAttribute('role')).toBe('gridcell');
    });

    it('ADR-0020: Space on a one-action cell fires it on the Focus\'s row; Enter and Tab fire nothing; a held Space fires once', async () => {
        const listener = heard();
        const { root } = mount({ columns: [...columns, open, review], rows: trades(50), rowHeight: 24, viewportHeight: 480, ...listener });
        await painted();
        // With nothing selected, Space only places the Focus: it never fires on a cell the user has not seen.
        key(root, ' ');
        await painted();
        expect(listener.actions).toEqual([]);
        press(root, x.open, 3);
        await painted();
        expect(key(root, ' ').defaultPrevented).toBe(true);
        expect(key(root, ' ', { repeat: true }).defaultPrevented).toBe(true);
        key(root, ' ', { repeat: true });
        await painted();
        expect(listener.actions).toEqual(['Open.open on Book-3']);
        // Firing moves nothing: the keyboard and the Focus stay.
        expect(root.getAttribute('aria-activedescendant')).toBe(`${prefix(root)}r3c2`);
        key(root, 'Enter');
        key(root, 'Tab');
        await painted();
        expect(listener.actions).toEqual(['Open.open on Book-3']);
    });

    it('ADR-0037: Space enters a cell with several actions; the arrows choose, clamped; Space fires the chosen one and leaves', async () => {
        const listener = heard();
        const { root } = mount({ columns: [...columns, open, review], rows: trades(50), rowHeight: 24, viewportHeight: 480, ...listener });
        await painted();
        press(root, x.review, 2);
        await painted();
        const others = rowsOf(root).filter((_, i) => i !== 2);
        key(root, ' ');
        await painted();
        expect(listener.actions).toEqual([]);
        const chosen = (): string[] => buttonsAt(root, 2, 3).map(button => `${button.className}${button.id === '' ? '' : ` #${button.id}`}`);
        expect(chosen()).toEqual([`ex-action ex-action-chosen #${prefix(root)}r2c3a0`, 'ex-action', 'ex-action icon-flag']);
        expect(root.getAttribute('aria-activedescendant')).toBe(`${prefix(root)}r2c3a0`);
        expect(q(root, '[data-ex-answered]').hasAttribute('data-ex-escape-leaves')).toBe(false);
        // Only the engaged row paints again (ADR-0003).
        expect(rowsOf(root).filter((_, i) => i !== 2)).toEqual(others);

        key(root, 'ArrowLeft');
        await painted();
        expect(root.getAttribute('aria-activedescendant')).toBe(`${prefix(root)}r2c3a0`);
        key(root, 'End');
        await painted();
        expect(root.getAttribute('aria-activedescendant')).toBe(`${prefix(root)}r2c3a2`);
        key(root, 'ArrowRight');
        key(root, 'ArrowLeft');
        await painted();
        expect(chosen()).toEqual(['ex-action', `ex-action ex-action-chosen #${prefix(root)}r2c3a1`, 'ex-action icon-flag']);
        // The Focus never moved, nor did the keyboard.
        expect(q(root, '.ex-focus').getAttribute('style')).toContain('left: 300px');

        key(root, ' ');
        await painted();
        expect(listener.actions).toEqual(['Review.query on Book-2']);
        expect(root.getAttribute('aria-activedescendant')).toBe(`${prefix(root)}r2c3`);
        expect(chosen()).toEqual(['ex-action', 'ex-action', 'ex-action icon-flag']);
    });

    it('ADR-0037: Escape leaves the cell and keeps the Focus; any other key leaves it and keeps its meaning, Enter firing nothing', async () => {
        const listener = heard();
        const { root } = mount({ columns: [...columns, open, review], rows: trades(50), rowHeight: 24, viewportHeight: 480, ...listener });
        await painted();
        press(root, x.review, 2);
        key(root, ' ');
        await painted();
        key(root, 'Escape');
        await painted();
        expect(root.getAttribute('aria-activedescendant')).toBe(`${prefix(root)}r2c3`);
        expect(q(root, '[data-ex-answered]').hasAttribute('data-ex-escape-leaves')).toBe(true);
        // The next Escape is the way out of the grid, which the first was not: Tab is released only now.
        key(root, ' ');
        key(root, 'ArrowRight');
        key(root, 'Enter');
        await painted();
        expect(listener.actions).toEqual([]);
        expect(root.getAttribute('aria-activedescendant')).toBe(`${prefix(root)}r3c3`);
        expect(buttonsAt(root, 2, 3).every(button => button.className !== 'ex-action ex-action-chosen')).toBe(true);
    });

    it('ADR-0020, ADR-0037: a click fires its button\'s action on its row, ends Interactive, and moves no Focus', async () => {
        const listener = heard();
        const { root } = mount({ columns: [...columns, open, review], rows: trades(50), rowHeight: 24, viewportHeight: 480, ...listener });
        await painted();
        press(root, x.review, 2);
        key(root, ' ');
        await painted();
        click(defined(buttonsAt(root, 5, 3)[2], 'Escalate on row 5'));
        await painted();
        expect(listener.actions).toEqual(['Review.escalate on Book-5']);
        expect(root.getAttribute('aria-activedescendant')).toBe(`${prefix(root)}r2c3`);
        // A press dragged off its button, the platform's way to cancel, is no click.
        mouse(defined(buttonsAt(root, 6, 2)[0], 'Open on row 6'), 'mousedown');
        mouse(q(root, '.ex-viewport'), 'mouseup');
        mouse(q(root, '.ex-viewport'), 'click');
        await painted();
        expect(listener.actions).toEqual(['Review.escalate on Book-5']);
    });

    it('ADR-0142: a press whose button a new version of its row took away acts once, at its release, on the row as it is now', async () => {
        const listener = heard();
        const rows = trades(50);
        const source = GridSource.from(rows);
        const { root } = mount({ columns: [...columns, open], source, rowHeight: 24, viewportHeight: 480, ...listener });
        await painted();
        const pressed = defined(buttonsAt(root, 4, 2)[0], 'Open on row 4');
        mouse(pressed, 'mousedown');
        const edited = { book: 'Book-4 (edited)', notional: 1 };
        source.replaceRow(defined(rows[4], 'row 4'), edited);
        await painted();
        expect(pressed.isConnected).toBe(false);
        // The browser clicks the nearest ancestor both events share, which is no button.
        mouse(defined(buttonsAt(root, 4, 2)[0], 'Open on row 4, painted again'), 'mouseup');
        mouse(q(root, '.ex-viewport'), 'click');
        await painted();
        expect(listener.actions).toEqual(['Open.open on Book-4 (edited)']);
    });

    it('ADR-0142: a press cancelled by its release elsewhere fires nothing later, and none fires on another row\'s button', async () => {
        const listener = heard();
        const rows = trades(50);
        const source = GridSource.from(rows);
        const { root } = mount({ columns: [...columns, open], source, rowHeight: 24, viewportHeight: 480, ...listener });
        await painted();
        // Pressed, dragged off and released on the rows: the platform's cancel, with no click after it.
        mouse(defined(buttonsAt(root, 5, 2)[0], 'Open on row 5'), 'mousedown');
        mouse(q(root, '.ex-viewport'), 'mouseup');
        source.replaceRow(defined(rows[5], 'row 5'), { book: 'Book-5 (edited)', notional: 5 });
        await painted();
        mouse(q(root, '.ex-viewport'), 'mousedown');
        mouse(defined(buttonsAt(root, 9, 2)[0], 'Open on row 9'), 'mouseup');
        // Pressed, its row painted anew, and released over the same action of another row.
        mouse(defined(buttonsAt(root, 6, 2)[0], 'Open on row 6'), 'mousedown');
        source.replaceRow(defined(rows[6], 'row 6'), { book: 'Book-6 (edited)', notional: 6 });
        await painted();
        mouse(defined(buttonsAt(root, 7, 2)[0], 'Open on row 7'), 'mouseup');
        mouse(q(root, '.ex-viewport'), 'click');
        await painted();
        expect(listener.actions).toEqual([]);
    });

    it('ADR-0034: a click on an action past an open editor commits it first, and a Reject holds it and fires nothing', async () => {
        const order: string[] = [];
        let reject = false;
        const book: GridColumn<Trade> = { name: 'Book', type: 'text', value: trade => trade.book, width: 120, editable: true };
        const { root, view } = mount({
            columns: [book, defined(columns[1], 'Notional'), open], rows: trades(50), rowHeight: 24, viewportHeight: 480,
            onEdit: intent => {
                order.push(`edit ${intent.value}`);
                if (reject) intent.refuse('No.');
            },
            onAction: ({ row }) => { order.push(`open ${row.book}`); },
        });
        await painted();
        press(root, 10, 3);
        await painted();
        key(q(root, '.ex-key-field'), 'x');
        await painted();
        click(defined(buttonsAt(root, 3, 2)[0], 'Open on row 3'));
        await painted();
        expect(order).toEqual(['edit x', 'open Book-3']);
        reject = true;
        key(q(root, '.ex-key-field'), 'y');
        await painted();
        click(defined(buttonsAt(root, 3, 2)[0], 'Open on row 3'));
        await painted();
        expect(order).toEqual(['edit x', 'open Book-3', 'edit y']);
        expect(root.querySelector('.ex-editor')).not.toBeNull();
        view.destroy();
    });

    it('ADR-0142: a press on what a replaced source painted is refused, naming no row; one whose row left the Window is refused', async () => {
        const listener = heard();
        const rows = trades(50);
        const options = { columns: [...columns, open], rowHeight: 24, viewportHeight: 480, ...listener };
        const { root, view, told } = mount({ ...options, rows });
        await painted();
        const pressed = defined(buttonsAt(root, 1, 2)[0], 'Open on row 1');
        mouse(pressed, 'mousedown');
        view.update({ ...options, rows: trades(50) });
        mouse(pressed, 'mouseup');
        mouse(pressed, 'click');
        expect(listener.refusals).toEqual([{ kind: 'action', reason: 'sourceChanged', row: undefined, column: 'Open', action: 'open' }]);

        const keyed = GridSource.from(trades(50), { rowKey: trade => trade.book });
        view.update({ ...options, source: keyed });
        await painted();
        const gone = defined(buttonsAt(root, 7, 2)[0], 'Open on row 7');
        mouse(gone, 'mousedown');
        keyed.apply({ removedKeys: ['Book-7'] });
        mouse(gone, 'mouseup');
        mouse(gone, 'click');
        expect(listener.refusals.at(-1)).toEqual({ kind: 'action', reason: 'rowLeftTheWindow', row: undefined, column: 'Open', action: 'open' });
        expect(listener.actions).toEqual([]);
        // Said in the live region too, in a row command's words (A11Y-16): said again, in the next frame.
        told.frame();
        expect(q(root, '.ex-announce').textContent).toBe('The row is no longer available. Repeat the command on the current data.');
    });

    it('ADR-0142: Space aimed with a Selection a sort has dropped fires nothing, and the action it was aimed at is refused', async () => {
        const listener = heard();
        const source = GridSource.from(trades(50));
        const { root } = mount({ columns: [...columns, open, review], source, rowHeight: 24, viewportHeight: 480, ...listener });
        await painted();
        press(root, x.review, 2);
        key(root, ' ');
        key(root, 'ArrowRight');
        await painted();
        source.setSorts([{ column: 'Notional', direction: 'descending' }]);
        key(root, ' ');
        await painted();
        expect(listener.actions).toEqual([]);
        expect(listener.refusals).toEqual([{ kind: 'action', reason: 'orderMoved', row: undefined, column: 'Review', action: 'query' }]);
    });
});

describe('Template Columns', () => {
    interface Mounted { readonly book: string; readonly request: number; readonly connected: boolean }

    function noteColumn(): { readonly column: GridColumn<Trade>; readonly mounts: Mounted[]; readonly cleanups: string[] } {
        const mounts: Mounted[] = [];
        const cleanups: string[] = [];
        const template: GridTemplate<Trade> = (slot, cell) => {
            const input = document.createElement('input');
            input.className = 'ex-interactive';
            input.tabIndex = -1;
            input.value = cell.row.book;
            slot.append(input);
            mounts.push({ book: cell.row.book, request: cell.focusRequest, connected: slot.isConnected });
            if (cell.focusRequest !== 0) cell.takeKeyboard(input);
            return () => {
                input.remove();
                cleanups.push(cell.row.book);
            };
        };
        return { column: { name: 'Note', type: 'text', value: trade => trade.book, template, width: 100 }, mounts, cleanups };
    }

    it('ADR-0020: content is mounted once its row stands in the DOM, kept while it is painted, and taken away when it is not', async () => {
        const note = noteColumn();
        const { root, view, told } = mount({ columns: [...columns, open, review, note.column], rows: trades(200), rowHeight: 24, viewportHeight: 480 });
        await painted();
        const count = rowsOf(root).length;
        expect(note.mounts).toHaveLength(count);
        expect(note.mounts.every(({ connected, request }) => connected && request === 0)).toBe(true);
        expect(cellAt(root, 0, 4).querySelector('input')?.value).toBe('Book-0');
        // A fling paints no scrollable cell, so it takes their content away until scrolling stands still.
        await scrollTo(root, 24 * 100);
        expect(note.cleanups).toHaveLength(count);
        told.clock.advance(150);
        await painted();
        expect(rowsOf(root).every(row => row.querySelector('.ex-interactive') !== null)).toBe(true);
        view.destroy();
        expect(note.cleanups).toHaveLength(note.mounts.length);
    });

    it('ADR-0020: a row painted again keeps its slot, and its content is mounted again into it only when its row changed', async () => {
        const note = noteColumn();
        const rows = trades(50);
        const source = GridSource.from(rows, { rowKey: trade => trade.book });
        const { root } = mount({ columns: [...columns, note.column], source, rowHeight: 24, viewportHeight: 480 });
        await painted();
        const slot = cellAt(root, 3, 2);
        const row = slot.parentElement;
        const mounted = note.mounts.length;
        source.replaceRow(defined(rows[3], 'row 3'), { book: 'Book-3', notional: -3 });
        await painted();
        expect(cellAt(root, 3, 2).parentElement).not.toBe(row);
        expect(cellAt(root, 3, 2)).toBe(slot);
        expect(note.mounts.slice(mounted)).toEqual([{ book: 'Book-3', request: 0, connected: true }]);
        expect(note.cleanups).toEqual(['Book-3']);
        // Without a Row Key a new instance is another row (ADR-0140): its cell is a new slot.
        const unkeyed = noteColumn();
        const plain = trades(50);
        const plainSource = GridSource.from(plain);
        const other = mount({ columns: [...columns, unkeyed.column], source: plainSource, rowHeight: 24, viewportHeight: 480 });
        await painted();
        const before = cellAt(other.root, 3, 2);
        plainSource.replaceRow(defined(plain[3], 'row 3'), { book: 'Book-3', notional: -3 });
        await painted();
        expect(cellAt(other.root, 3, 2)).not.toBe(before);
        expect(unkeyed.cleanups).toEqual(['Book-3']);
    });

    it('ADR-0037: a cell Space brings into view is mounted once, with the request', async () => {
        const note = noteColumn();
        const { root, told, view } = mount({ columns: [...columns, note.column], rows: trades(200), rowHeight: 24, viewportHeight: 480 });
        await painted();
        press(root, 230, 2);
        await scrollTo(root, 24 * 100);
        told.clock.advance(150);
        await painted();
        const mounted = note.mounts.length;
        key(root, ' ');
        await painted();
        expect(note.mounts.slice(mounted).filter(({ book }) => book === 'Book-2')).toEqual([{ book: 'Book-2', request: 1, connected: true }]);
        expect(document.activeElement).toBe(q(cellAt(root, 2, 2), 'input'));
        view.destroy();
    });

    it('ADR-0037: takeKeyboard is handed a control inside its own cell, which takes the keyboard or holds one that does', async () => {
        let take: ((control: HTMLElement) => void) | undefined;
        const template: GridTemplate<Trade> = (slot, cell) => {
            take = cell.takeKeyboard;
            slot.append(document.createElement('span'));
            return () => undefined;
        };
        const { root } = mount({ columns: [...columns, { name: 'Note', type: 'text', value: trade => trade.book, template, width: 100 }], rows: trades(5), rowHeight: 24, viewportHeight: 480 });
        await painted();
        const handed = defined(take, 'takeKeyboard');
        const outside = document.createElement('input');
        document.body.append(outside);
        expect(() => { handed(outside); }).toThrow(/inside its own cell/);
        expect(() => { handed(q(cellAt(root, 0, 2), 'span')); }).toThrow(/holds no enabled control/);
        outside.remove();
    });

    it('ADR-0037: Space hands the cell one focus request, its control takes the keyboard, Escape gives it back, and no later mount is handed it', async () => {
        const note = noteColumn();
        const rows = trades(50);
        const source = GridSource.from(rows);
        const { root } = mount({ columns: [...columns, note.column], source, rowHeight: 24, viewportHeight: 480 });
        await painted();
        press(root, 230, 2);
        key(root, ' ');
        await painted();
        expect(note.mounts.at(-1)).toEqual({ book: 'Book-2', request: 1, connected: true });
        const input = q(cellAt(root, 2, 2), 'input');
        expect(document.activeElement).toBe(input);
        // The control's own keys are its own, and the grid's Focus stays.
        key(input, 'ArrowDown');
        await painted();
        expect(root.getAttribute('aria-activedescendant')).toBe(`${prefix(root)}r2c2`);
        expect(key(input, 'Escape').defaultPrevented).toBe(true);
        expect(document.activeElement).toBe(root);

        source.replaceRow(defined(rows[2], 'row 2'), { book: 'Book-2 (edited)', notional: 2 });
        await painted();
        expect(note.mounts.at(-1)).toEqual({ book: 'Book-2 (edited)', request: 0, connected: true });
        expect(document.activeElement).toBe(root);
    });
});
