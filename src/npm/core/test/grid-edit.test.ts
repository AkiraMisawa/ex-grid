// @vitest-environment happy-dom
// Layer 2 (ADR-0350 section 13): the Cell Editor (ADR-0010) and the Keyboard Field (ADR-0080). Typing onto a
// cell that edits opens the editor holding what was typed; a commit raises one Edit Intent and the grid writes
// nothing (ADR-0007); a verdict or the Consumer may hold it (ADR-0034, ADR-0142); and the rows or columns
// moving under it are answered by the row the commit goes to (ADR-0011, ADR-0142).
import { describe, expect, it } from 'vitest';
import { defined } from '../src/must';
import type { EditDiscardReason, GridEditIntent } from '../src/cells/edit-intent';
import type { GridPasteIntent } from '../src/clipboard/paste-intent';
import type { GridColumn } from '../src/dom/grid-column';
import type { GridOptions } from '../src/dom/grid-view';
import type { GridRefusal } from '../src/dom/grid-refusal';
import type { CellPosition } from '../src/selection/cell';
import type { GridSelection } from '../src/selection/selection';
import { GridSource } from '../src/source/grid-source';
import { mount, painted, q, trades, type Trade } from './grid-harness';

// Book and Notional edit; the Desk does not. Notional's verdict rejects what is not a number and flags a
// negative one, as the Features page's does.
const columns: readonly GridColumn<Trade>[] = [
    { name: 'Book', type: 'text', value: trade => trade.book, width: 120, editable: true },
    {
        name: 'Notional', type: 'number', value: trade => trade.notional, width: 100, editable: true,
        validate: (_row, text) => (Number.isNaN(Number(text)) ? { kind: 'reject', message: `'${text}' is not a number` }
            : Number(text) < 0 ? { kind: 'flag', message: 'a negative notional needs approval' } : { kind: 'accept' }),
    },
    { name: 'Desk', type: 'text', value: trade => trade.book, width: 100 },
];

// Rows of 24 px under a header of 24; the columns' left edges.
const rowY = (row: number): number => (row * 24) + 5;
const book = 10;
const notional = 130;
const desk = 230;

function mouse(root: HTMLElement, name: string, x: number, y: number): MouseEvent {
    const event = new MouseEvent(name, { bubbles: true, cancelable: true, button: 0, buttons: name === 'mouseup' ? 0 : 1 });
    Object.defineProperty(event, 'offsetX', { value: x });
    Object.defineProperty(event, 'offsetY', { value: y });
    q(root, '.ex-viewport').dispatchEvent(event);
    return event;
}

/** A press on the rows, as the browser lands it: the scroller takes DOM focus by default, unless prevented. */
function press(root: HTMLElement, x: number, y: number): MouseEvent {
    const down = mouse(root, 'mousedown', x, y);
    if (!down.defaultPrevented) q(root, '.ex-scroller').focus();
    mouse(root, 'mouseup', x, y);
    return down;
}

/** A key typed where the keyboard is. */
function key(name: string, init: KeyboardEventInit = {}): KeyboardEvent {
    const event = new KeyboardEvent('keydown', { key: name, bubbles: true, cancelable: true, ...init });
    const target = document.activeElement;
    if (target === null) throw new Error('Nothing holds the keyboard.');
    target.dispatchEvent(event);
    return event;
}

/** Text typed into the editor: what the field does with a key the grid leaves to it. */
function type(root: HTMLElement, text: string): void {
    const field = editor(root);
    field.value += text;
    field.dispatchEvent(new Event('input', { bubbles: true }));
}

const editor = (root: HTMLElement): HTMLInputElement => q(root, 'input.ex-editor') as HTMLInputElement;
const keyField = (root: HTMLElement): HTMLInputElement => q(root, '.ex-viewport > .ex-key-field-layer > input.ex-key-field') as HTMLInputElement;
const hasEditor = (root: HTMLElement): boolean => root.querySelector('.ex-editor') !== null;

/** What the Consumer hears. `refuse` refuses every edit with its sentence. */
function heard(refuse?: string): {
    readonly edits: GridEditIntent<Trade>[]; readonly refusals: GridRefusal[]; readonly discards: EditDiscardReason[];
    readonly editing: boolean[]; readonly selections: GridSelection[]; readonly doubleClicks: CellPosition[];
    readonly options: Pick<GridOptions<Trade>, 'onEdit' | 'onRefused' | 'onEditDiscarded' | 'onEditingChanged' | 'onSelectionChanged' | 'onCellDoubleClick'>;
} {
    const edits: GridEditIntent<Trade>[] = [];
    const refusals: GridRefusal[] = [];
    const discards: EditDiscardReason[] = [];
    const editing: boolean[] = [];
    const selections: GridSelection[] = [];
    const doubleClicks: CellPosition[] = [];
    return {
        edits, refusals, discards, editing, selections, doubleClicks,
        options: {
            onEdit: intent => {
                edits.push(intent);
                if (refuse !== undefined) intent.refuse(refuse);
            },
            onRefused: refusal => { refusals.push(refusal); },
            onEditDiscarded: reason => { discards.push(reason); },
            onEditingChanged: open => { editing.push(open); },
            onSelectionChanged: selection => { selections.push(selection); },
            onCellDoubleClick: cell => { doubleClicks.push(cell); },
        },
    };
}

const focusOf = (selections: readonly GridSelection[]): CellPosition | undefined => defined(selections.at(-1), 'a Selection heard').focus;

async function grid(listener: ReturnType<typeof heard>, options: Partial<GridOptions<Trade>> = {}): Promise<HTMLElement> {
    const { root } = mount({ columns, rows: trades(20), rowHeight: 24, viewportHeight: 480, ...listener.options, ...options });
    await painted();
    return root;
}

describe('the Keyboard Field', () => {
    it('ADR-0080 / A11Y-4: on a grid that edits it is the one tab stop, carries aria-activedescendant and is read-only over a cell that does not edit', async () => {
        const root = await grid(heard());
        const field = keyField(root);
        expect([field.getAttribute('tabindex'), root.getAttribute('tabindex')]).toEqual(['0', '-1']);
        // It stands first among the Viewport's children, ahead of the selection layers.
        expect(q(root, '.ex-viewport').firstElementChild).toBe(field.parentElement);

        press(root, desk, rowY(2));
        await painted();
        expect(document.activeElement).toBe(field);
        expect(field.getAttribute('aria-activedescendant')).toMatch(/r2c2$/);
        expect(root.hasAttribute('aria-activedescendant')).toBe(false);
        expect(field.hasAttribute('readonly')).toBe(true);
        // A press is no keyboard's arrival: the root's ring is not drawn (KB-12).
        expect(root.hasAttribute('data-ex-focus-visible')).toBe(false);

        press(root, book, rowY(2));
        await painted();
        expect(field.hasAttribute('readonly')).toBe(false);
    });

    it('ADR-0080 / A11Y-5: a grid nobody hears an edit from has none, and keeps the keyboard on its root', async () => {
        const listener = heard();
        const { onEdit: _unheard, ...rest } = listener.options;
        const root = await grid({ ...listener, options: rest });
        expect(root.querySelector('.ex-key-field')).toBe(null);
        expect(root.getAttribute('tabindex')).toBe('0');
        press(root, book, rowY(1));
        await painted();
        expect(document.activeElement).toBe(root);
        key('x');
        await painted();
        expect(hasEditor(root)).toBe(false);
    });

    it('ADR-0080: focus put on the root goes on to the field, and text put into it that is no key is removed', async () => {
        const root = await grid(heard());
        root.focus();
        expect(document.activeElement).toBe(keyField(root));
        keyField(root).value = 'dropped';
        keyField(root).dispatchEvent(new InputEvent('input', { bubbles: true }));
        expect(keyField(root).value).toBe('');
    });
});

describe('the Cell Editor', () => {
    it('ED-2 / ED-4: a character opens Overwrite holding it; Enter commits one intent and moves down, and the grid writes nothing', async () => {
        const listener = heard();
        const root = await grid(listener);
        press(root, book, rowY(0));
        await painted();
        const typed = key('X');
        expect(typed.defaultPrevented).toBe(true);
        await painted();
        expect([editor(root).value, document.activeElement === editor(root)]).toEqual(['X', true]);
        expect(editor(root).getAttribute('data-ex-cell')).toBe('0 0');
        expect(listener.editing).toEqual([true]);

        type(root, 'yz');
        key('Enter');
        await painted();
        const intent = defined(listener.edits[0], 'the intent');
        expect([listener.edits.length, intent.row.book, intent.column, intent.value, intent.seenText, intent.replacedText])
            .toEqual([1, 'Book-0', 'Book', 'Xyz', 'Book-0', 'Book-0']);
        expect(hasEditor(root)).toBe(false);
        expect(q(root, '[id$="r0c0"]').textContent).toBe('Book-0');
        expect(focusOf(listener.selections)).toEqual({ row: 1, column: 0 });
        // The keyboard is back in the field, and Enter came by the keyboard (KB-12).
        expect(document.activeElement).toBe(keyField(root));
        expect(root.hasAttribute('data-ex-focus-visible')).toBe(true);
        expect(listener.editing).toEqual([true, false]);
    });

    it('ED-13 / ADR-0010: Control and Alt together type a character; either alone opens nothing, and nothing opens over a cell that does not edit', async () => {
        const root = await grid(heard());
        press(root, book, rowY(0));
        await painted();
        key('q', { altKey: true });
        key('q', { ctrlKey: true });
        await painted();
        expect(hasEditor(root)).toBe(false);
        key('q', { ctrlKey: true, altKey: true });
        await painted();
        expect(editor(root).value).toBe('q');
        key('Escape');
        press(root, desk, rowY(0));
        await painted();
        expect(key('x').defaultPrevented).toBe(false);
        await painted();
        expect(hasEditor(root)).toBe(false);
    });

    it('ED-2: F2 opens Caret on the cell\'s text, where the arrows are the field\'s; F2 again is Overwrite, where an arrow commits and moves', async () => {
        const listener = heard();
        const root = await grid(listener);
        press(root, book, rowY(3));
        await painted();
        key('F2');
        await painted();
        expect([editor(root).value, editor(root).selectionStart]).toEqual(['Book-3', 6]);
        expect(key('ArrowLeft').defaultPrevented).toBe(false);
        expect(key('Home', { shiftKey: true }).defaultPrevented).toBe(false);
        expect(listener.edits).toEqual([]);

        key('F2');
        key('ArrowRight');
        await painted();
        expect(defined(listener.edits[0], 'the intent').value).toBe('Book-3');
        expect(focusOf(listener.selections)).toEqual({ row: 3, column: 1 });
    });

    it('ED-2: in Overwrite Shift+↓ commits and extends the Selection, as it does with no edit open', async () => {
        const listener = heard();
        const root = await grid(listener);
        press(root, book, rowY(2));
        await painted();
        key('A');
        key('ArrowDown', { shiftKey: true });
        await painted();
        expect(defined(listener.edits[0], 'the intent').value).toBe('A');
        const selection = defined(listener.selections.at(-1), 'the Selection');
        expect([selection.ranges, selection.focus]).toEqual([[{ topRow: 2, leftColumn: 0, rowCount: 2, columnCount: 1 }], { row: 2, column: 0 }]);
    });

    it('ED-3: Escape cancels with no intent; Backspace opens an empty Overwrite; a key with nothing selected opens on the first cell on screen', async () => {
        const listener = heard();
        const root = await grid(listener);
        root.focus();
        key('Q');
        await painted();
        expect([editor(root).value, editor(root).getAttribute('data-ex-cell')]).toEqual(['Q', '0 0']);
        key('Escape');
        await painted();
        expect([hasEditor(root), listener.edits.length, document.activeElement === keyField(root)]).toEqual([false, 0, true]);
        expect(focusOf(listener.selections)).toEqual({ row: 0, column: 0 });

        key('Backspace');
        await painted();
        expect(editor(root).value).toBe('');
        // While an edit stands, an Escape on the root would cancel it, not leave (ADR-0230).
        expect(q(root, '[data-ex-answered]').hasAttribute('data-ex-escape-leaves')).toBe(false);
    });

    it('ADR-0010: a press on another cell commits and selects it; a double click on a cell that edits opens Caret on its text', async () => {
        const listener = heard();
        const root = await grid(listener);
        press(root, book, rowY(0));
        await painted();
        key('Z');
        await painted();
        press(root, notional, rowY(4));
        await painted();
        expect(defined(listener.edits[0], 'the intent').value).toBe('Z');
        expect(focusOf(listener.selections)).toEqual({ row: 4, column: 1 });
        expect(document.activeElement).toBe(keyField(root));

        mouse(root, 'dblclick', notional, rowY(4));
        await painted();
        expect([editor(root).value, document.activeElement === editor(root)]).toEqual(['4000', true]);
        key('Escape');
        mouse(root, 'dblclick', desk, rowY(5));
        await painted();
        expect([hasEditor(root), listener.doubleClicks]).toEqual([false, [{ row: 5, column: 2 }]]);
    });

    it('ADR-0034 ED-15: a Reject holds every commit gesture, shows and says the Consumer\'s sentence, and Escape is the way out', async () => {
        const listener = heard();
        const root = await grid(listener);
        press(root, notional, rowY(0));
        await painted();
        key('a');
        type(root, 'bc');
        key('Enter');
        await painted();
        expect([editor(root).value, editor(root).getAttribute('aria-invalid')]).toEqual(['abc', 'true']);
        const message = q(root, '.ex-message');
        expect([message.textContent, editor(root).getAttribute('aria-describedby')]).toEqual(['\'abc\' is not a number', message.id]);
        expect(q(root, '.ex-announce').textContent).toBe('\'abc\' is not a number');
        key('Tab');
        key('ArrowDown', { shiftKey: true });
        // A press past the editor keeps the keyboard in it and selects nothing.
        expect(press(root, book, rowY(5)).defaultPrevented).toBe(true);
        await painted();
        expect([listener.edits.length, hasEditor(root), focusOf(listener.selections)]).toEqual([0, true, { row: 0, column: 1 }]);

        key('Escape');
        await painted();
        expect([hasEditor(root), root.querySelector('.ex-message')]).toEqual([false, null]);
        // A Flag raises the intent unchanged.
        key('-');
        type(root, '5');
        key('Enter');
        expect(defined(listener.edits[0], 'the flagged intent').value).toBe('-5');
    });

    it('ADR-0142: an intent the Consumer refuses holds the editor with its sentence; a refusal after onEdit returned throws', async () => {
        const listener = heard('Locked by the desk.');
        const root = await grid(listener);
        press(root, book, rowY(1));
        await painted();
        key('N');
        key('Enter');
        await painted();
        expect([listener.edits.length, editor(root).value, q(root, '.ex-message').textContent]).toEqual([1, 'N', 'Locked by the desk.']);
        expect(focusOf(listener.selections)).toEqual({ row: 1, column: 0 });
        expect(() => { defined(listener.edits[0], 'the intent').refuse('Too late.'); }).toThrow(/answered when onEdit returned/);
    });

    it('ADR-0035 ADR-0007: Ctrl+Enter fills the Selection with one paste intent naming the cell typed in; over a column that does not edit it is refused and the editor stays', async () => {
        const pastes: GridPasteIntent[] = [];
        const listener = heard();
        const root = await grid(listener, { onPaste: intent => { pastes.push(intent); } });
        press(root, book, rowY(1));
        key('ArrowDown', { shiftKey: true });
        key('ArrowRight', { shiftKey: true });
        await painted();
        key('7');
        key('Enter', { ctrlKey: true });
        await painted();
        const fill = defined(pastes[0], 'the fill');
        expect([fill.cellCount, fill.values, fill.enteredAt, listener.edits.length, hasEditor(root)]).toEqual([4, [['7']], { row: 1, column: 0 }, 0, false]);

        key('ArrowRight', { shiftKey: true });
        await painted();
        key('8');
        key('Enter', { ctrlKey: true });
        await painted();
        expect([pastes.length, editor(root).value]).toEqual([1, '8']);
        expect(listener.refusals).toEqual([{ kind: 'paste', reason: 'targetNotEditable' }]);
    });
});

describe('keys that open an edit, and keys that do not', () => {
    it('ADR-0037 ADR-0010: Space opens Overwrite onto a Focus the user has seen; Shift+Space selects the rows; with nothing selected Space opens nothing', async () => {
        const listener = heard();
        const root = await grid(listener);
        root.focus();
        key(' ');
        await painted();
        expect(hasEditor(root)).toBe(false);

        press(root, book, rowY(2));
        await painted();
        key(' ', { shiftKey: true });
        await painted();
        expect([hasEditor(root), defined(listener.selections.at(-1), 'the Selection').ranges])
            .toEqual([false, [{ topRow: 2, leftColumn: 0, rowCount: 1, columnCount: 3 }]]);
        key(' ');
        await painted();
        expect(editor(root).value).toBe(' ');
    });

    it('ADR-0142: typing aimed at a Selection the order took away before it was painted opens nothing, said once; once painted, a key starts on the first cell', async () => {
        const listener = heard();
        const source = GridSource.from(trades(20));
        const { root } = mount({ columns, source, rowHeight: 24, viewportHeight: 480, ...listener.options });
        await painted();
        press(root, book, rowY(7));
        await painted();
        source.setSorts([{ column: 'Notional', direction: 'descending' }]);
        key('5');
        key('6');
        expect([hasEditor(root), listener.discards]).toEqual([false, ['orderMoved']]);
        await painted();
        key('7');
        await painted();
        expect([editor(root).value, editor(root).getAttribute('data-ex-cell'), listener.discards]).toEqual(['7', '0 0', ['orderMoved']]);
    });

    it('ADR-0080: a composition finished on the Keyboard Field opens Overwrite holding its text, and leaves none in the field', async () => {
        const listener = heard();
        const root = await grid(listener);
        press(root, book, rowY(1));
        await painted();
        const field = keyField(root);
        field.value = '日本';
        const composed = new CompositionEvent('compositionend', { data: '日本', bubbles: true });
        // happy-dom keeps no `data` of its own.
        Object.defineProperty(composed, 'data', { value: '日本' });
        field.dispatchEvent(composed);
        await painted();
        expect([editor(root).value, field.value, document.activeElement === editor(root)]).toEqual(['日本', '', true]);
    });

    it('ADR-0080 ADR-0050: a grid that stops editing hands the keyboard to its root; one destroyed with an edit open says the edit ended', async () => {
        const listener = heard();
        const rows = trades(20);
        const { root, view } = mount({ columns, rows, rowHeight: 24, viewportHeight: 480, ...listener.options });
        await painted();
        press(root, book, rowY(1));
        await painted();
        expect(document.activeElement).toBe(keyField(root));
        view.update({ columns, rows, rowHeight: 24, viewportHeight: 480 });
        await painted();
        expect([document.activeElement === root, root.getAttribute('tabindex')]).toEqual([true, '0']);

        view.update({ columns, rows, rowHeight: 24, viewportHeight: 480, ...listener.options });
        await painted();
        key('Q');
        await painted();
        view.destroy();
        expect(listener.editing).toEqual([true, false]);
    });
});

describe('an edit under rows and columns that move', () => {
    it('ADR-0142 LV-20: without a Row Key, a commit after the order moved is refused, and the editor stays with the typing', async () => {
        const listener = heard();
        const source = GridSource.from(trades(20));
        const { root } = mount({ columns, source, rowHeight: 24, viewportHeight: 480, ...listener.options });
        await painted();
        press(root, book, rowY(2));
        await painted();
        key('W');
        source.setSorts([{ column: 'Notional', direction: 'descending' }]);
        await painted();
        expect(editor(root).value).toBe('W');
        key('Enter');
        await painted();
        expect(listener.refusals).toEqual([{ kind: 'commit', reason: 'orderMoved', cell: { row: 2, column: 0 }, column: 'Book' }]);
        expect([listener.edits.length, editor(root).value]).toEqual([0, 'W']);
    });

    it('ADR-0011 ADR-0142: with a Row Key the editor and the Focus follow their row, and the commit lands on it', async () => {
        const listener = heard();
        const source = GridSource.from(trades(20), { rowKey: trade => trade.book });
        const { root } = mount({ columns, source, rowHeight: 24, viewportHeight: 480, ...listener.options });
        await painted();
        press(root, book, rowY(2));
        await painted();
        key('W');
        source.setSorts([{ column: 'Notional', direction: 'descending' }]);
        await painted();
        expect(editor(root).getAttribute('data-ex-cell')).toBe('17 0');
        expect(focusOf(listener.selections)).toEqual({ row: 17, column: 0 });
        key('Enter');
        await painted();
        expect(defined(listener.edits[0], 'the intent').row.book).toBe('Book-2');
    });

    it('ADR-0011: other columns or another source discard the edit, said by its reason, and the keyboard comes back', async () => {
        const listener = heard();
        const rows = trades(20);
        const { root, view } = mount({ columns, rows, rowHeight: 24, viewportHeight: 480, ...listener.options });
        await painted();
        press(root, book, rowY(2));
        await painted();
        key('W');
        await painted();
        view.update({ columns: columns.slice(0, 2), rows, rowHeight: 24, viewportHeight: 480, ...listener.options });
        await painted();
        expect([hasEditor(root), listener.discards, listener.edits.length]).toEqual([false, ['columnsChanged'], 0]);
        expect(document.activeElement).toBe(keyField(root));

        key('V');
        await painted();
        view.update({ columns: columns.slice(0, 2), rows: trades(5), rowHeight: 24, viewportHeight: 480, ...listener.options });
        await painted();
        expect(listener.discards).toEqual(['columnsChanged', 'sourceChanged']);
        expect(listener.editing).toEqual([true, false, true, false]);
    });

    it('ADR-0035: a column no longer Editable at the commit discards the edit', async () => {
        const listener = heard();
        const rows = trades(20);
        const { root, view } = mount({ columns, rows, rowHeight: 24, viewportHeight: 480, ...listener.options });
        await painted();
        press(root, book, rowY(2));
        await painted();
        key('W');
        await painted();
        view.update({ columns: columns.map(column => ({ ...column, editable: false })), rows, rowHeight: 24, viewportHeight: 480, ...listener.options });
        // Other column objects under the same names are the same columns: the edit stands until it commits.
        await painted();
        expect(editor(root).value).toBe('W');
        key('Enter');
        await painted();
        expect([hasEditor(root), listener.discards, listener.edits.length]).toEqual([false, ['columnNoLongerEditable'], 0]);
    });
});
