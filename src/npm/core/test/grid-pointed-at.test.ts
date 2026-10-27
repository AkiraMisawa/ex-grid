// @vitest-environment happy-dom
// Layer 2 (ADR-0350 section 13): a grid pointed at from outside (ADR-0058), in the DOM. While its declaration says
// so, a press on the rows or the header moves nothing here and is handed over as what it landed on; a drag onto
// another cell is handed over once more; the declaration's dashes and outlined columns are painted; and the grid
// answers the steps and reveals a Consumer pointing from elsewhere asks for (DC-55). The arrows pressed in an edit
// whose text was written from outside are handed to the Consumer that wrote it.
import { afterEach, describe, expect, it } from 'vitest';
import type { GridColumn } from '../src/dom/grid-column';
import type { GridOptions } from '../src/dom/grid-view';
import { GridPointedAt, type GridPointArrow, type GridPointedPress } from '../src/dom/pointed-at';
import type { GridSelection } from '../src/selection/selection';
import { mount, painted, q, trades, unmountAll, type Trade } from './grid-harness';

const columns: readonly GridColumn<Trade>[] = [
    { name: 'Book', type: 'text', value: trade => trade.book, width: 120, editable: true },
    { name: 'Notional', type: 'number', value: trade => trade.notional, width: 120 },
    { name: 'Desk', type: 'text', value: trade => trade.book, width: 120 },
];

const rowY = (row: number): number => (row * 24) + 5;
const columnX = (column: number): number => (column * 120) + 10;

function mouse(target: Element, name: string, x: number, y: number, init: MouseEventInit = {}): MouseEvent {
    const event = new MouseEvent(name, { bubbles: true, cancelable: true, button: 0, buttons: name === 'mouseup' || name === 'click' ? 0 : 1, ...init });
    Object.defineProperty(event, 'offsetX', { value: x });
    Object.defineProperty(event, 'offsetY', { value: y });
    target.dispatchEvent(event);
    return event;
}

const pressCell = (root: HTMLElement, row: number, column: number, init: MouseEventInit = {}): MouseEvent =>
    mouse(q(root, '.ex-viewport'), 'mousedown', columnX(column), rowY(row), init);

// A press the grid keeps as its own: the scroller takes DOM focus from it, as the browser gives it.
function pressOwn(root: HTMLElement, row: number, column: number): void {
    if (!pressCell(root, row, column).defaultPrevented) q(root, '.ex-scroller').focus();
    mouse(q(root, '.ex-viewport'), 'mouseup', columnX(column), rowY(row));
}

function key(root: HTMLElement, name: string, init: KeyboardEventInit = {}): void {
    const target = root.ownerDocument.activeElement;
    if (target === null) throw new Error('Nothing holds the keyboard.');
    target.dispatchEvent(new KeyboardEvent('keydown', { key: name, bubbles: true, cancelable: true, ...init }));
}

const all = (): boolean => true;

const isBook = (book: string) => (trade: Trade): boolean => trade.book === book;

interface Pointed {
    readonly root: HTMLElement;
    readonly view: ReturnType<typeof mount<Trade>>['view'];
    readonly declaration: GridPointedAt<Trade>;
    readonly presses: GridPointedPress<Trade>[];
    readonly selections: GridSelection[];
}

async function pointed(options: Partial<GridOptions<Trade>> = {}, declared = true): Promise<Pointed> {
    const presses: GridPointedPress<Trade>[] = [];
    const selections: GridSelection[] = [];
    const declaration = new GridPointedAt<Trade>(async press => { presses.push(press); });
    const { root, view } = mount<Trade>({
        columns, rows: trades(40), rowHeight: 24, viewportHeight: 240, ...(declared ? { pointedAt: declaration } : {}),
        onSelectionChanged: selection => { selections.push(selection); },
        onEdit: () => undefined,
        ...options,
    });
    await painted();
    declaration.isPointedAt = true;
    await painted();
    return { root, view, declaration, presses, selections };
}

afterEach(unmountAll);

describe('Pointed at from outside (ADR-0058)', () => {
    it('a press on a cell is handed over as the cell, keeps DOM focus where it was, and moves no Selection; its click means nothing', async () => {
        const { root, presses, selections } = await pointed();
        expect(root.classList.contains('ex-pointed-at')).toBe(true);
        const before = selections.length;
        expect(pressCell(root, 2, 1).defaultPrevented).toBe(true);
        mouse(q(root, '.ex-viewport'), 'mouseup', columnX(1), rowY(2));
        mouse(q(root, '.ex-viewport'), 'dblclick', columnX(1), rowY(2));
        expect(mouse(q(root, '.ex-viewport'), 'contextmenu', columnX(1), rowY(2), { button: 2 }).defaultPrevented).toBe(true);
        await painted();
        expect(presses).toEqual([{ kind: 'cell', row: { book: 'Book-2', notional: 2000 }, column: 'Notional' }]);
        expect(selections.length).toBe(before);
        expect(root.querySelector('input.ex-editor')).toBeNull();
        expect(root.querySelector('.ex-context-menu')).toBeNull();
    });

    it('Shift is several cells, past the last column is nothing, and a drag onto another cell is handed over once behind its press', async () => {
        const { root, presses } = await pointed();
        pressCell(root, 1, 0, { shiftKey: true });
        mouse(q(root, '.ex-viewport'), 'mousedown', 400, rowY(1));
        pressCell(root, 3, 0);
        mouse(q(root, '.ex-viewport'), 'mousemove', columnX(0), rowY(3));
        mouse(q(root, '.ex-viewport'), 'mousemove', columnX(0), rowY(5));
        mouse(q(root, '.ex-viewport'), 'mousemove', columnX(0), rowY(6));
        await painted();
        await painted();
        expect(presses.map(press => [press.kind, press.dragged === true])).toEqual([['severalCells', false], ['cell', false], ['severalCells', true]]);
    });

    it('a press on a column header is the column, Shift several columns, and neither sorts', async () => {
        const { root, presses } = await pointed();
        const header = q(root, '.ex-header');
        expect(mouse(header, 'mousedown', columnX(2), 5).defaultPrevented).toBe(true);
        mouse(header, 'mouseup', columnX(2), 5);
        mouse(header, 'click', columnX(2), 5);
        mouse(header, 'mousedown', columnX(1), 5, { shiftKey: true });
        mouse(header, 'click', columnX(1), 5, { shiftKey: true });
        await painted();
        expect(presses).toEqual([{ kind: 'columnHeader', column: 'Desk' }, { kind: 'severalColumns' }]);
        expect([...root.querySelectorAll('.ex-header-cell')].map(cell => cell.getAttribute('aria-sort'))).toEqual(['none', 'none', 'none']);
    });

    it('a grid with an edit of its own open is not pointed at: a press goes to its edit', async () => {
        const { root, presses, declaration } = await pointed();
        declaration.isPointedAt = false;
        await painted();
        pressOwn(root, 1, 0);
        await painted();
        key(root, 'F2');
        declaration.isPointedAt = true;
        await painted();
        expect(root.classList.contains('ex-pointed-at')).toBe(false);
        pressCell(root, 4, 0);
        await painted();
        expect(presses).toEqual([]);
    });

    it('the dashes are painted over the named row\'s cell while it is painted, or down a column, and the declared columns are outlined', async () => {
        const { root, declaration } = await pointed();
        declaration.dashes = { row: isBook('Book-3'), column: 'Notional' };
        await painted();
        const dashes = (): (string | null)[] => [...root.querySelectorAll('.ex-viewport > .ex-selection > .ex-point-dashes')].map(piece => piece.getAttribute('style'));
        expect(dashes()).toEqual([expect.stringMatching(/height: 24px/u)]);
        declaration.dashes = { row: isBook('Book-39'), column: 'Notional' };
        await painted();
        expect(dashes()).toEqual([]);
        declaration.dashes = { row: undefined, column: 'Desk' };
        declaration.outlinedColumns = [{ column: 'Book', colour: 3 }];
        await painted();
        expect(dashes()).toHaveLength(1);
        expect([...root.querySelectorAll('.ex-viewport > .ex-selection > .ex-reference-outline')].map(piece => piece.className)).toEqual(['ex-reference-outline ex-reference-3']);
    });

    it('DC-55: steps from a cell and from a column are answered in the current order, passing over columns the Consumer refuses', async () => {
        const { declaration } = await pointed();
        expect(await declaration.step(isBook('Book-1'), 'Book', 'down', all)).toEqual({ kind: 'cell', row: { book: 'Book-2', notional: 2000 }, column: 'Book' });
        expect(await declaration.step(isBook('Book-0'), 'Book', 'up', all)).toEqual({ kind: 'edge' });
        expect(await declaration.step(isBook('Book-1'), 'Book', 'right', name => name !== 'Notional')).toEqual({ kind: 'cell', row: { book: 'Book-1', notional: 1000 }, column: 'Desk' });
        expect(await declaration.step(isBook('Book-1'), 'Desk', 'right', all)).toEqual({ kind: 'edge' });
        expect(await declaration.step(isBook('Nobody'), 'Book', 'down', all)).toEqual({ kind: 'notHeld' });
        expect(await declaration.stepFromColumn('Notional', 'down', all)).toEqual({ kind: 'cell', row: { book: 'Book-0', notional: 0 }, column: 'Notional' });
        expect(await declaration.stepFromColumn('Notional', 'up', all)).toEqual({ kind: 'edge' });
        expect(await declaration.stepFromColumn('Notional', 'left', all)).toEqual({ kind: 'column', column: 'Book' });
        expect(await declaration.reveal(isBook('Book-30'), 'Desk')).toBe(true);
        expect(await declaration.revealColumn('Missing')).toBe(false);
        declaration.isPointedAt = false;
        expect(await declaration.step(isBook('Book-1'), 'Book', 'down', all)).toEqual({ kind: 'notHeld' });
    });

    it('a grid destroyed answers its declaration no more', async () => {
        const { view, declaration } = await pointed();
        view.destroy();
        expect(await declaration.step(isBook('Book-1'), 'Book', 'down', all)).toEqual({ kind: 'notHeld' });
    });

    it('a pointedAt that is not a declaration is refused by name', () => {
        const notADeclaration: unknown = { isPointedAt: true };
        expect(() => mount<Trade>({ columns, rows: trades(2), pointedAt: notADeclaration as never })).toThrow(/pointedAt/u);
    });
});

describe('Arrows over text written from outside (ADR-0058, "The keyboard")', () => {
    it('are handed to the Consumer with Shift and the Primary Modifier, and move nothing here', async () => {
        const arrows: GridPointArrow[] = [];
        const { root, view } = await pointed({
            pointAt: (text, caret) => text.startsWith('=') && caret === text.length && text.endsWith('='),
            referenceText: range => `R${String(range.topRow)}`,
            onPointArrowFromOutside: arrow => { arrows.push(arrow); },
        }, false);
        pressOwn(root, 1, 0);
        await painted();
        key(root, '=');
        await painted();
        expect(view.writePointedText('Positions[PV]')).toBe(true);
        key(root, 'ArrowDown');
        key(root, 'ArrowLeft', { shiftKey: true, ctrlKey: true });
        await painted();
        expect(arrows).toEqual([{ direction: 'down', extends: false, toEdge: false }, { direction: 'left', extends: true, toEdge: true }]);
        const editor = q(root, 'input.ex-editor');
        expect(editor instanceof HTMLInputElement ? editor.value : undefined).toBe('=Positions[PV]');
    });
});
