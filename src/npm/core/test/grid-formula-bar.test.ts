// @vitest-environment happy-dom
// Layer 2 (ADR-0350 section 13): the Formula Bar and its Name Box (ADR-0051). The bar reads the Focus cell's text,
// and is the Cell Editor's second surface: one uncommitted text in two places, committed once from either. The Name
// Box names the Focus in the Consumer's words and hands what is typed into it to the Consumer, which places the
// Selection with `placeSelection` (ADR-0050, item 4), taken against the Row Sequence Version it read.
import { describe, expect, it } from 'vitest';
import { defined } from '../src/must';
import type { GridEditIntent } from '../src/cells/edit-intent';
import type { GridColumn } from '../src/dom/grid-column';
import type { GridOptions } from '../src/dom/grid-view';
import { selectionRange } from '../src/selection/range';
import type { GridSelection } from '../src/selection/selection';
import { mount, painted, q, trades, type Trade } from './grid-harness';

const columns: readonly GridColumn<Trade>[] = [
    { name: 'Book', type: 'text', value: trade => trade.book, width: 120, editable: true },
    { name: 'Notional', type: 'number', value: trade => trade.notional, width: 100 },
];

const noText = (_trade: Trade): string => '';
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

function key(name: string, init: KeyboardEventInit = {}): KeyboardEvent {
    const event = new KeyboardEvent('keydown', { key: name, bubbles: true, cancelable: true, ...init });
    const target = document.activeElement;
    if (target === null) throw new Error('Nothing holds the keyboard.');
    target.dispatchEvent(event);
    return event;
}

function typeInto(field: HTMLInputElement, text: string): void {
    field.value += text;
    field.dispatchEvent(new Event('input', { bubbles: true }));
}

const barText = (root: HTMLElement): HTMLInputElement => q(root, '.ex-formula-bar > input.ex-formula-bar-text') as HTMLInputElement;
const nameBox = (root: HTMLElement): HTMLInputElement => q(root, '.ex-formula-bar input.ex-name-box') as HTMLInputElement;
const cellEditor = (root: HTMLElement): HTMLInputElement => q(root, '.ex-scroller input.ex-editor') as HTMLInputElement;
const hasCellEditor = (root: HTMLElement): boolean => root.querySelector('.ex-scroller .ex-editor') !== null;

async function grid(options: Partial<GridOptions<Trade>> = {}): Promise<{
    readonly root: HTMLElement; readonly edits: GridEditIntent<Trade>[]; readonly selections: GridSelection[];
    readonly entered: string[]; readonly view: ReturnType<typeof mount<Trade>>['view'];
}> {
    const edits: GridEditIntent<Trade>[] = [];
    const selections: GridSelection[] = [];
    const entered: string[] = [];
    const { root, view } = mount<Trade>({
        columns, rows: trades(20), rowHeight: 24, viewportHeight: 480, showFormulaBar: true,
        nameBoxLabel: cell => `R${String(cell.row + 1)}C${String(cell.column + 1)}`,
        onEdit: intent => { edits.push(intent); },
        onSelectionChanged: selection => { selections.push(selection); },
        onNameBoxEntered: typed => { entered.push(typed); },
        ...options,
    });
    await painted();
    return { root, edits, selections, entered, view };
}

describe('the Formula Bar (ADR-0051)', () => {
    it('ADR-0051: the bar reads the Focus cell\'s text and the Name Box names the Focus, and its band comes off the Viewport', async () => {
        const { root } = await grid();
        press(root, 10, rowY(3));
        await painted();
        expect(barText(root).value).toBe('Book-3');
        expect(nameBox(root).value).toBe('R4C1');
        expect([nameBox(root).getAttribute('aria-label'), barText(root).getAttribute('aria-label')]).toEqual(['Name Box', 'Formula Bar']);
        // Neither field is a tab stop: the grid is one (ADR-0020).
        expect([nameBox(root).getAttribute('tabindex'), barText(root).getAttribute('tabindex')]).toEqual(['-1', '-1']);
        expect(q(root, '.ex-scroller').style.height).toBe(`${String(480 - Number.parseFloat(q(root, '.ex-formula-bar').style.height))}px`);
        expect(root.style.paddingTop).toBe(q(root, '.ex-formula-bar').style.height);
        // A cell that does not edit is read in the bar, not edited there.
        press(root, 130, rowY(3));
        await painted();
        expect([barText(root).value, barText(root).readOnly]).toEqual(['3000', true]);
    });

    it('ADR-0051: typing in the bar opens a Caret edit shown over the cell too; Enter commits it once, moves down and hands the keyboard back', async () => {
        const { root, edits, selections } = await grid();
        press(root, 10, rowY(1));
        await painted();
        const text = barText(root);
        text.focus();
        typeInto(text, '-x');
        await painted();
        expect(cellEditor(root).value).toBe('Book-1-x');
        expect(document.activeElement).toBe(text);
        // Caret: an arrow moves within the text, it does not commit (ADR-0010).
        expect(key('ArrowLeft').defaultPrevented).toBe(false);
        key('Enter');
        await painted();
        expect(edits.map(edit => [edit.row.book, edit.value])).toEqual([['Book-1', 'Book-1-x']]);
        expect(hasCellEditor(root)).toBe(false);
        expect(defined(selections.at(-1), 'a Selection').focus).toEqual({ row: 2, column: 0 });
        expect(document.activeElement).not.toBe(text);
        expect(root.contains(document.activeElement)).toBe(true);
        expect(barText(root).value).toBe('Book-2');
    });

    it('ADR-0051: what is typed in the cell is shown in the bar, and Escape from the bar cancels the one edit', async () => {
        const { root, edits } = await grid();
        press(root, 10, rowY(0));
        await painted();
        key('Q');
        await painted();
        expect(barText(root).value).toBe('Q');
        typeInto(cellEditor(root), 'r');
        expect(barText(root).value).toBe('Qr');
        barText(root).focus();
        key('Escape');
        await painted();
        expect(edits).toEqual([]);
        expect(hasCellEditor(root)).toBe(false);
        expect(barText(root).value).toBe('Book-0');
    });

    it('ADR-0125: a cell with no text of its own may show the Consumer\'s borrowed text, dimmed and read only', async () => {
        const { root } = await grid({
            columns: [{ ...defined(columns[0], 'Book'), value: noText }, defined(columns[1], 'Notional')],
            formulaBarBorrowedText: (trade, column) => (column.name === 'Book' && trade.notional === 2000 ? '=SEQUENCE(3)' : undefined),
        });
        press(root, 10, rowY(2));
        await painted();
        expect([barText(root).value, barText(root).readOnly, barText(root).classList.contains('ex-formula-bar-borrowed')]).toEqual(['=SEQUENCE(3)', true, true]);
        press(root, 10, rowY(1));
        await painted();
        expect([barText(root).value, barText(root).readOnly, barText(root).classList.contains('ex-formula-bar-borrowed')]).toEqual(['', false, false]);
    });
});

describe('the Name Box (ADR-0051) and placeSelection (ADR-0050, item 4)', () => {
    it('ADR-0051: Enter in the Name Box hands the Consumer what was typed, which places the Selection against the version it read', async () => {
        const { root, entered, selections, view } = await grid();
        const box = nameBox(root);
        box.focus();
        box.value = 'R6C2';
        box.dispatchEvent(new Event('input', { bubbles: true }));
        q(root, 'form.ex-name-box-form').dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
        expect(entered).toEqual(['R6C2']);
        expect(root.contains(document.activeElement)).toBe(true);
        expect(document.activeElement).not.toBe(box);

        expect(view.placeSelection(selectionRange(5, 1, 3, 1), { row: 5, column: 1 }, 0)).toBe(true);
        await painted();
        expect(defined(selections.at(-1), 'a Selection').focus).toEqual({ row: 5, column: 1 });
        expect(nameBox(root).value).toBe('R6C2');
        // Taken against a version that has moved, it is refused, and nothing moves (the spine's sixth rule).
        expect(view.placeSelection(selectionRange(0, 0, 1, 1), { row: 0, column: 0 }, 1)).toBe(false);
        expect(defined(selections.at(-1), 'a Selection').focus).toEqual({ row: 5, column: 1 });
    });

    it('ADR-0050 item 4: a placement is refused while an edit stands', async () => {
        const { root, view } = await grid();
        press(root, 10, rowY(0));
        await painted();
        key('Z');
        await painted();
        expect(view.placeSelection(selectionRange(4, 0, 1, 1), { row: 4, column: 0 }, 0)).toBe(false);
        expect(cellEditor(root).value).toBe('Z');
    });
});

describe('the Formula Bar\'s edges (ADR-0051)', () => {
    it('ADR-0051: over a cell that does not edit, Escape in the bar hands the keyboard back, and typing there opens nothing', async () => {
        const { root, edits } = await grid();
        press(root, 130, rowY(2));
        await painted();
        barText(root).focus();
        typeInto(barText(root), 'x');
        await painted();
        expect(hasCellEditor(root)).toBe(false);
        expect(barText(root).value).toBe('2000');
        expect(key('Escape').defaultPrevented).toBe(true);
        expect(document.activeElement).not.toBe(barText(root));
        expect(root.contains(document.activeElement)).toBe(true);
        expect(edits).toEqual([]);
    });

    it('ADR-0051: Enter in the Name Box with nothing typed names nothing; a press into it commits an edit standing, in place', async () => {
        const { root, entered, edits, selections } = await grid();
        press(root, 10, rowY(0));
        await painted();
        key('Q');
        await painted();
        nameBox(root).focus();
        await painted();
        expect(edits.map(edit => edit.value)).toEqual(['Q']);
        expect(hasCellEditor(root)).toBe(false);
        expect(defined(selections.at(-1), 'a Selection').focus).toEqual({ row: 0, column: 0 });
        q(root, 'form.ex-name-box-form').dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
        expect(entered).toEqual([]);
        expect(document.activeElement).not.toBe(nameBox(root));
    });
});
