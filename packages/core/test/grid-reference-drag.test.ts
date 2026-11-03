// @vitest-environment happy-dom
// Layer 2 (ADR-0350 section 13): a Reference Outline dragged by its edge or a corner square (ADR-0480, DC-67). With a
// Consumer that rewrites a dragged Reference, every outline over the grid's cells carries grips while an edit is open:
// an edge moves the Reference keeping its size, a square resizes it from the opposite corner, the text is rewritten as
// the range reached changes, the caret stays with the text around it, and Escape puts the Reference back.
import { afterEach, describe, expect, it } from 'vitest';
import type { GridEditorReference } from '../src/cells/reference-colouring';
import type { GridColumn } from '../src/dom/grid-column';
import type { GridOptions } from '../src/dom/grid-view';
import { selectionRange, type SelectionRange } from '../src/selection/range';
import { mount, painted, q, trades, unmountAll, type Trade } from './grid-harness';

// Four columns of 100px, so a cell's place is arithmetic: column c spans x 100c to 100(c+1), row r y 24r to 24(r+1).
const columns: readonly GridColumn<Trade>[] = ['A', 'B', 'C', 'D'].map(name => ({
    name, type: 'text', value: (trade: Trade) => trade.book, width: 100, editable: true,
}));

const columnOf = (letter: string): number => letter.charCodeAt(0) - 65;
const rowOf = (digits: string): number => Number(digits) - 1;
const letterOf = (column: number): string => String.fromCharCode(65 + column);

// A Consumer's References, as a Sheet's would answer them: `B3` or `A1:B2`, a `$` before either part kept.
function referencesIn(text: string): readonly GridEditorReference[] {
    if (!text.startsWith('=')) return [];
    return [...text.matchAll(/\$?([A-Z])\$?(\d+)(?::\$?([A-Z])\$?(\d+))?/gu)].map(match => {
        const [, left, top, right, bottom] = match;
        if (left === undefined || top === undefined) throw new Error('A cell is a letter and a number.');
        const topRow = Math.min(rowOf(top), rowOf(bottom ?? top));
        const leftColumn = Math.min(columnOf(left), columnOf(right ?? left));
        const range = selectionRange(topRow, leftColumn, Math.abs(rowOf(bottom ?? top) - rowOf(top)) + 1, Math.abs(columnOf(right ?? left) - columnOf(left)) + 1);
        return { start: match.index, length: match[0].length, range };
    });
}

// The Reference rewritten to name `range`, a leading `$` kept, as a Sheet keeps each part's form.
const asked: [string, SelectionRange][] = [];
function moveReference(reference: string, range: SelectionRange): string {
    asked.push([reference, range]);
    const dollar = reference.startsWith('$') ? '$' : '';
    const first = `${dollar}${letterOf(range.leftColumn)}${dollar}${String(range.topRow + 1)}`;
    if (range.rowCount === 1 && range.columnCount === 1) return first;
    return `${first}:${letterOf(range.leftColumn + range.columnCount - 1)}${String(range.topRow + range.rowCount)}`;
}

const grips = (root: HTMLElement): Element[] => [...root.querySelectorAll('.ex-viewport > .ex-selection > .ex-reference-grip')];

function grip(root: HTMLElement, outline: number, kind: string): HTMLElement {
    const found = grips(root).filter(each => each.getAttribute('data-ex-grip') === kind)[outline];
    if (!(found instanceof HTMLElement)) throw new Error(`No ${kind} grip on outline ${String(outline)}.`);
    return found;
}

// The cell's middle, in the Viewport's offsets.
const middle = (column: number, row: number): readonly [number, number] => [(column * 100) + 50, (row * 24) + 12];

function mouse(target: Element, name: string, x: number, y: number, init: MouseEventInit = {}): MouseEvent {
    const event = new MouseEvent(name, { bubbles: true, cancelable: true, button: 0, buttons: name === 'mouseup' ? 0 : 1, ...init });
    Object.defineProperty(event, 'offsetX', { value: x });
    Object.defineProperty(event, 'offsetY', { value: y });
    target.dispatchEvent(event);
    return event;
}

// A press on a grip, at its middle.
function pressGrip(target: HTMLElement, init: MouseEventInit = {}): MouseEvent {
    const width = Number.parseFloat(target.style.width);
    const height = Number.parseFloat(target.style.height);
    return mouse(target, 'mousedown', width / 2, height / 2, init);
}

function key(name: string): void {
    const target = document.activeElement;
    if (target === null) throw new Error('Nothing holds the keyboard.');
    target.dispatchEvent(new KeyboardEvent('keydown', { key: name, bubbles: true, cancelable: true }));
}

function retype(field: HTMLInputElement, text: string, caret = text.length): void {
    field.value = text;
    field.setSelectionRange(caret, caret);
    field.dispatchEvent(new Event('input', { bubbles: true }));
}

// Opens Caret on the first row's first cell, and writes `text` into the editor as the field does.
async function edit(root: HTMLElement, text: string, caret = text.length): Promise<HTMLInputElement> {
    await painted();
    const viewport = q(root, '.ex-viewport');
    if (!mouse(viewport, 'mousedown', 10, 5).defaultPrevented) q(root, '.ex-scroller').focus();
    mouse(viewport, 'mouseup', 10, 5);
    await painted();
    key('F2');
    await painted();
    const field = q(root, 'input.ex-editor');
    if (!(field instanceof HTMLInputElement)) throw new TypeError('The editor is an input.');
    retype(field, text, caret);
    await painted();
    return field;
}

const grid = (options: Partial<GridOptions<Trade>>, moves = true): HTMLElement =>
    mount({ columns, rows: trades(40), rowHeight: 24, viewportHeight: 480, onEdit: () => undefined, referencesIn, ...(moves ? { moveReference } : {}), ...options }).root;

afterEach(() => {
    unmountAll();
    asked.length = 0;
});

describe('Dragging a Reference Outline (ADR-0480)', () => {
    it('DC-67: without moveReference no outline carries a grip, and with it each carries four edges and four squares', async () => {
        const without = grid({}, false);
        await edit(without, '=B3+A1:B2');
        expect(grips(without)).toEqual([]);
        unmountAll();
        const root = grid({});
        await edit(root, '=B3+A1:B2');
        expect(grips(root).map(each => each.getAttribute('data-ex-grip'))).toEqual([
            'top', 'bottom', 'left', 'right', 'topLeft', 'topRight', 'bottomLeft', 'bottomRight',
            'top', 'bottom', 'left', 'right', 'topLeft', 'topRight', 'bottomLeft', 'bottomRight',
        ]);
        expect(grip(root, 0, 'topLeft').className).toBe('ex-reference-grip ex-reference-grip-topLeft ex-reference-square ex-reference-1');
        // A square is 5px of colour and 1px of margin, centred on B3's top-left corner, the margin's width inline.
        expect(grip(root, 0, 'topLeft').getAttribute('style')).toBe('left: 96.5px; top: 44.5px; width: 7px; height: 7px; border-width: 1px');
        key('Enter');
        await painted();
        expect(grips(root)).toEqual([]);
    });

    it('DC-67: an edge moves the Reference keeping its size, rewritten each time it reaches another cell, and the caret moves with the text', async () => {
        const root = grid({});
        const field = await edit(root, '=B3+A1:B2');
        const viewport = q(root, '.ex-viewport');
        expect(pressGrip(grip(root, 1, 'right')).defaultPrevented).toBe(true);
        expect(root.getAttribute('data-ex-reference-drag')).toBe('move');
        // Held at B2, the cell of A1:B2 nearest the press on its right edge's middle; C4 is a column and two rows on.
        mouse(viewport, 'mousemove', ...middle(2, 3));
        expect(field.value).toBe('=B3+B3:C4');
        mouse(viewport, 'mousemove', ...middle(2, 3));
        expect(asked).toHaveLength(1);
        mouse(viewport, 'mouseup', ...middle(2, 3));
        expect(root.getAttribute('data-ex-reference-drag')).toBeNull();
        expect(field.value).toBe('=B3+B3:C4');
        expect(field.selectionStart).toBe(9);
        // The first Reference moved, the text after it moves with the caret.
        await painted();
        pressGrip(grip(root, 0, 'top'));
        mouse(viewport, 'mousemove', ...middle(3, 11));
        mouse(viewport, 'mouseup', ...middle(3, 11));
        expect(field.value).toBe('=D12+B3:C4');
        expect(field.selectionStart).toBe(10);
        expect(document.activeElement).toBe(field);
    });

    it('DC-67: a square resizes from the opposite corner, and turns over past it', async () => {
        const root = grid({});
        const field = await edit(root, '=SUM(B2:C3)');
        const viewport = q(root, '.ex-viewport');
        pressGrip(grip(root, 0, 'bottomRight'));
        expect(root.getAttribute('data-ex-reference-drag')).toBe('nwse');
        mouse(viewport, 'mousemove', ...middle(3, 5));
        expect(field.value).toBe('=SUM(B2:D6)');
        mouse(viewport, 'mousemove', ...middle(0, 0));
        expect(field.value).toBe('=SUM(A1:B2)');
        mouse(viewport, 'mouseup', ...middle(0, 0));
        pressGrip(grip(root, 0, 'topRight'));
        expect(root.getAttribute('data-ex-reference-drag')).toBe('nesw');
    });

    it('DC-67: a move is held inside the grid, and a press released where it was pressed changes nothing', async () => {
        const root = grid({});
        const field = await edit(root, '=B2:C3', 3);
        const viewport = q(root, '.ex-viewport');
        // Held at B3, nearest the middle of the left edge, and released there.
        pressGrip(grip(root, 0, 'left'));
        mouse(viewport, 'mouseup', ...middle(1, 2));
        expect(field.value).toBe('=B2:C3');
        expect(field.selectionStart).toBe(3);
        expect(asked).toEqual([]);
        pressGrip(grip(root, 0, 'left'));
        // Four columns: two wide, it stops at C and D.
        mouse(viewport, 'mousemove', 390, 12);
        expect(field.value).toBe('=C1:D2');
        // The caret stood inside the Reference, and goes to its end.
        expect(field.selectionStart).toBe(6);
    });

    it('DC-67: a press and a release with no move between reach the cell released on (the spine\'s sixth rule)', async () => {
        const root = grid({});
        const field = await edit(root, '=A1');
        pressGrip(grip(root, 0, 'bottom'));
        mouse(q(root, '.ex-viewport'), 'mouseup', ...middle(2, 6));
        expect(field.value).toBe('=C7');
    });

    it('DC-67: Escape mid-drag puts the Reference and the caret back and ends the drag; the edit stays, and a second Escape cancels it', async () => {
        const root = grid({});
        const field = await edit(root, '=A1+B2', 3);
        const viewport = q(root, '.ex-viewport');
        pressGrip(grip(root, 1, 'top'));
        mouse(viewport, 'mousemove', ...middle(3, 9));
        expect(field.value).toBe('=A1+D10');
        key('Escape');
        expect(field.value).toBe('=A1+B2');
        expect(field.selectionStart).toBe(3);
        expect(root.getAttribute('data-ex-reference-drag')).toBeNull();
        // The drag is over: a move with the button reaches nothing.
        mouse(viewport, 'mousemove', ...middle(2, 2));
        expect(field.value).toBe('=A1+B2');
        expect(q(root, 'input.ex-editor')).toBe(field);
        key('Escape');
        await painted();
        expect(root.querySelector('input.ex-editor')).toBeNull();
    });

    it('DC-67: text typed during the drag ends it, the span it held no longer the Reference\'s', async () => {
        const root = grid({});
        const field = await edit(root, '=A1');
        pressGrip(grip(root, 0, 'top'));
        retype(field, '=1+A1');
        mouse(q(root, '.ex-viewport'), 'mousemove', ...middle(2, 2));
        expect(field.value).toBe('=1+A1');
        expect(root.getAttribute('data-ex-reference-drag')).toBeNull();
    });

    it('DC-67: Escape after text typed mid-drag puts back nothing, the drag over already, and is the edit\'s Escape', async () => {
        const root = grid({});
        const field = await edit(root, '=A1');
        pressGrip(grip(root, 0, 'top'));
        retype(field, '=1+A1');
        key('Escape');
        expect(root.getAttribute('data-ex-reference-drag')).toBeNull();
        expect(asked).toEqual([]);
        await painted();
        expect(root.querySelector('input.ex-editor')).toBeNull();
    });

    it('DC-67: a Reference reaching past the grid is moved whole, and only its own edges carry grips', async () => {
        const root = grid({});
        const field = await edit(root, '=C1:F2');
        // Drawn as C1:D2, the grid's last column cutting it: no right edge, and no square on that side.
        expect(grips(root).map(each => each.getAttribute('data-ex-grip'))).toEqual(['top', 'bottom', 'left', 'topLeft', 'bottomLeft']);
        pressGrip(grip(root, 0, 'left'));
        mouse(q(root, '.ex-viewport'), 'mousemove', ...middle(1, 0));
        expect(field.value).toBe('=B1:E2');
        expect(asked.at(-1)).toEqual(['C1:F2', selectionRange(0, 1, 2, 4)]);
    });

    it('DC-67: a press on the outer half of the last column\'s edge holds that edge\'s cell in the row pressed', async () => {
        const root = grid({});
        const field = await edit(root, '=C1:D2');
        const right = grip(root, 0, 'right');
        // Half a pixel inside the strip's outer side: past the grid's last column, where no cell lies.
        mouse(right, 'mousedown', Number.parseFloat(right.style.width) - 0.5, 12);
        // Held at D1, so C3 is a column back and two rows down.
        mouse(q(root, '.ex-viewport'), 'mousemove', ...middle(2, 2));
        expect(field.value).toBe('=B3:C4');
    });

    it('ADR-0480: with Shift, a press on a grip is the press on the cell beneath it, and commits as one does', async () => {
        const root = grid({});
        const field = await edit(root, '=A1');
        pressGrip(grip(root, 0, 'topLeft'), { shiftKey: true });
        await painted();
        expect(root.querySelector('input.ex-editor')).toBeNull();
        expect(field.value).toBe('=A1');
    });

    it('DC-67: held in the bottom edge band the drag scrolls a step a tick, and the Reference follows the cell under the pointer', async () => {
        const { root, told } = mount({ columns, rows: trades(1000), rowHeight: 24, viewportHeight: 480, onEdit: () => undefined, referencesIn, moveReference });
        const field = await edit(root, '=A1');
        pressGrip(grip(root, 0, 'top'));
        // Fifteen pixels into the band of the 456 px the rows have: row 19, and six rows a tick.
        mouse(q(root, '.ex-viewport'), 'mousemove', 50, 451);
        expect(field.value).toBe('=A19');
        told.clock.advance(50);
        await painted();
        expect(field.value).toBe('=A25');
        mouse(root, 'mouseup', 50, 451);
        told.clock.advance(500);
        await painted();
        expect(field.value).toBe('=A25');
    });

    it('DC-67: pointing goes on across a drag of another Reference, its span moved with the text, and its arrows still point', async () => {
        const root = grid({
            pointAt: (text, caret) => /[=+]$/u.test(text.slice(0, caret)),
            referenceText: range => `${letterOf(range.leftColumn)}${String(range.topRow + 1)}`,
        });
        const field = await edit(root, '=A1+');
        const viewport = q(root, '.ex-viewport');
        // A press on B5 points, and the edit is in Point.
        mouse(viewport, 'mousedown', ...middle(1, 4));
        mouse(viewport, 'mouseup', ...middle(1, 4));
        await painted();
        expect(field.value).toBe('=A1+B5');
        pressGrip(grip(root, 0, 'top'));
        mouse(viewport, 'mousemove', ...middle(2, 9));
        mouse(viewport, 'mouseup', ...middle(2, 9));
        expect(field.value).toBe('=C10+B5');
        key('ArrowDown');
        await painted();
        expect(field.value).toBe('=C10+B6');
    });

    it('DC-67: the Reference Point writes, dragged, is pointed at where it was dragged to', async () => {
        const root = grid({
            pointAt: (text, caret) => /[=+]$/u.test(text.slice(0, caret)),
            referenceText: range => `${letterOf(range.leftColumn)}${String(range.topRow + 1)}`,
        });
        const field = await edit(root, '=');
        const viewport = q(root, '.ex-viewport');
        mouse(viewport, 'mousedown', ...middle(1, 1));
        mouse(viewport, 'mouseup', ...middle(1, 1));
        await painted();
        pressGrip(grip(root, 0, 'bottom'));
        mouse(viewport, 'mouseup', ...middle(2, 11));
        expect(field.value).toBe('=C12');
        key('ArrowDown');
        await painted();
        expect(field.value).toBe('=C13');
    });

    it('ADR-0480: an answer that is no text is refused by name', async () => {
        const root = grid({ moveReference: () => '' });
        await edit(root, '=A1');
        pressGrip(grip(root, 0, 'top'));
        expect(() => mouse(q(root, '.ex-viewport'), 'mousemove', ...middle(2, 2))).toThrow(/moveReference/u);
    });

    it('ADR-0480: moveReference that is not a function is refused by name', () => {
        expect(() => grid({ moveReference: 'A1' as never })).toThrow(/moveReference/u);
    });
});
