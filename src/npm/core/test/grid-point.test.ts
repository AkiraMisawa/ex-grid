// @vitest-environment happy-dom
// Layer 2 (ADR-0350 section 13): Point, the fourth editing state (ADR-0051), in the DOM. Where the Consumer says a
// Reference can go at the caret, the arrows and a press move a pointing outline and write its Reference at the caret,
// the Selection and the Focus staying put; typing or a caret moved ends it; F4 cycles the pointed Reference whole; and
// a Consumer pointing from outside writes and takes back its own text (ADR-0058).
import { afterEach, describe, expect, it } from 'vitest';
import type { GridColumn } from '../src/dom/grid-column';
import type { GridOptions } from '../src/dom/grid-view';
import type { GridPointState } from '../src/dom/point-mode';
import type { SelectionRange } from '../src/selection/range';
import type { GridSelection } from '../src/selection/selection';
import { mount, painted, q, trades, unmountAll, type Trade } from './grid-harness';

const columns: readonly GridColumn<Trade>[] = [
    { name: 'Book', type: 'text', value: trade => trade.book, width: 120, editable: true },
    { name: 'Notional', type: 'number', value: trade => trade.notional, width: 100, editable: true },
    { name: 'Desk', type: 'text', value: trade => trade.book, width: 100, editable: true },
];

// As a Sheet names cells: a column letter from A, a row number from 1.
const cellName = (row: number, column: number): string => `${String.fromCharCode(65 + column)}${String(row + 1)}`;
const referenceText = (range: SelectionRange): string => (range.rowCount === 1 && range.columnCount === 1
    ? cellName(range.topRow, range.leftColumn)
    : `${cellName(range.topRow, range.leftColumn)}:${cellName(range.topRow + range.rowCount - 1, range.leftColumn + range.columnCount - 1)}`);
// A Reference can go after `=`, an operator, `(` or `,` in a Formula.
const pointAt = (text: string, caret: number): boolean => text.startsWith('=') && '=+-*/(,'.includes(text.charAt(caret - 1));

const rowY = (row: number): number => (row * 24) + 5;
const columnX = (column: number): number => (column * 120) + 10;

function mouse(root: HTMLElement, name: string, x: number, y: number, init: MouseEventInit = {}): MouseEvent {
    const event = new MouseEvent(name, { bubbles: true, cancelable: true, button: 0, buttons: name === 'mouseup' ? 0 : 1, ...init });
    Object.defineProperty(event, 'offsetX', { value: x });
    Object.defineProperty(event, 'offsetY', { value: y });
    q(root, '.ex-viewport').dispatchEvent(event);
    return event;
}

function press(root: HTMLElement, row: number, column: number, init: MouseEventInit = {}): MouseEvent {
    const down = mouse(root, 'mousedown', columnX(column), rowY(row), init);
    if (!down.defaultPrevented) q(root, '.ex-scroller').focus();
    mouse(root, 'mouseup', columnX(column), rowY(row), init);
    return down;
}

function key(name: string, init: KeyboardEventInit = {}): KeyboardEvent {
    const target = document.activeElement;
    if (target === null) throw new Error('Nothing holds the keyboard.');
    const event = new KeyboardEvent('keydown', { key: name, bubbles: true, cancelable: true, ...init });
    target.dispatchEvent(event);
    target.dispatchEvent(new KeyboardEvent('keyup', { key: name, bubbles: true }));
    return event;
}

function editor(root: HTMLElement): HTMLInputElement {
    const found = q(root, 'input.ex-editor');
    if (!(found instanceof HTMLInputElement)) throw new TypeError('The editor is an input.');
    return found;
}

// Typed at the caret, as the field types a character the grid leaves to it.
function type(root: HTMLElement, text: string): void {
    const field = editor(root);
    const at = field.selectionStart ?? field.value.length;
    field.setRangeText(text, at, field.selectionEnd ?? at, 'end');
    field.dispatchEvent(new Event('input', { bubbles: true }));
}

const pointOutline = (root: HTMLElement): (string | null)[] =>
    [...root.querySelectorAll('.ex-viewport > .ex-selection > .ex-point')].map(piece => piece.getAttribute('class'));

interface Heard {
    readonly states: GridPointState[];
    readonly selections: GridSelection[];
    readonly edits: string[];
}

async function grid(options: Partial<GridOptions<Trade>> = {}): Promise<{ readonly root: HTMLElement; readonly heard: Heard; readonly view: ReturnType<typeof mount<Trade>>['view'] }> {
    const heard: Heard = { states: [], selections: [], edits: [] };
    const { root, view } = mount<Trade>({
        columns, rows: trades(20), rowHeight: 24, viewportHeight: 480, pointAt, referenceText,
        onPointStateChanged: state => { heard.states.push(state); },
        onSelectionChanged: selection => { heard.selections.push(selection); },
        onEdit: intent => { heard.edits.push(intent.value); },
        ...options,
    });
    await painted();
    press(root, 1, 1);
    await painted();
    return { root, heard, view };
}

afterEach(unmountAll);

describe('Point (ADR-0051)', () => {
    it('ED-9: = then ↓ ↓ writes the Reference two rows down, the Focus stays, and typing ends pointing', async () => {
        const { root, heard } = await grid();
        key('=');
        await painted();
        expect(heard.states).toEqual(['inPoint']);
        expect(key('ArrowDown').defaultPrevented).toBe(true);
        key('ArrowDown');
        await painted();
        expect(editor(root).value).toBe('=B4');
        expect(pointOutline(root)).toEqual(['ex-point']);
        expect(heard.selections.at(-1)?.focus).toEqual({ row: 1, column: 1 });
        type(root, '+');
        await painted();
        expect(pointOutline(root)).toEqual([]);
        // Pointing starts again from the edited cell where the text allows it.
        key('ArrowLeft');
        await painted();
        expect(editor(root).value).toBe('=B4+A2');
        key('Enter');
        await painted();
        expect(heard.edits).toEqual(['=B4+A2']);
        expect(heard.states.at(-1)).toBe('none');
    });

    it('ADR-0051: Shift extends the outline, Shift+Home reaches the row\'s first column, and the Reference is rewritten in place', async () => {
        const { root } = await grid();
        key('=');
        key('ArrowRight');
        key('ArrowDown', { shiftKey: true });
        await painted();
        expect(editor(root).value).toBe('=C2:C3');
        key('Home', { shiftKey: true });
        await painted();
        expect(editor(root).value).toBe('=A2:C3');
    });

    it('ADR-0058 Q53: End where pointing could start is taken and writes nothing; where it could not, Overwrite commits and moves', async () => {
        const { root, heard } = await grid();
        key('=');
        expect(key('End').defaultPrevented).toBe(true);
        await painted();
        expect([editor(root).value, heard.edits]).toEqual(['=', []]);
        type(root, '1');
        key('ArrowDown');
        await painted();
        expect(heard.edits).toEqual(['=1']);
        expect(root.querySelector('input.ex-editor')).toBeNull();
    });

    it('ADR-0051: Caret\'s arrows are the field\'s, and F2 from Caret points where a Reference can go', async () => {
        const { root } = await grid();
        key('=');
        // F2 from Overwrite is Caret, whose arrows are the field's.
        key('F2');
        expect(key('ArrowDown').defaultPrevented).toBe(false);
        key('F2');
        expect(key('ArrowDown').defaultPrevented).toBe(true);
        await painted();
        expect(editor(root).value).toBe('=B3');
        // F2 from Point is Caret: the outline goes, the Reference stays, and the arrows are the field's.
        key('F2');
        await painted();
        expect(pointOutline(root)).toEqual([]);
        expect(key('ArrowDown').defaultPrevented).toBe(false);
        expect(editor(root).value).toBe('=B3');
    });

    it('ADR-0051: a press on a cell points without committing, Shift extends from the outline, and a drag extends it', async () => {
        const { root, heard } = await grid();
        key('=');
        expect(press(root, 4, 0).defaultPrevented).toBe(true);
        await painted();
        expect(editor(root).value).toBe('=A5');
        expect(document.activeElement).toBe(editor(root));
        press(root, 5, 2, { shiftKey: true });
        await painted();
        expect(editor(root).value).toBe('=A5:C6');
        mouse(root, 'mousedown', columnX(0), rowY(7));
        mouse(root, 'mousemove', columnX(1), rowY(9));
        mouse(root, 'mouseup', columnX(1), rowY(9));
        await painted();
        expect(editor(root).value).toBe('=A8:B10');
        expect(heard.edits).toEqual([]);
        expect(heard.selections.at(-1)?.focus).toEqual({ row: 1, column: 1 });
    });

    it('ADR-0051: a press where no Reference can go commits, as a press past the editor does', async () => {
        const { root, heard } = await grid();
        key('1');
        press(root, 4, 0);
        await painted();
        expect(heard.edits).toEqual(['1']);
    });

    it('ADR-0051 R1 (vi): F4 cycles the pointed Reference whole, the caret after it, and pointing goes on', async () => {
        const cycles: [string, number, number][] = [];
        const { root } = await grid({
            cycleReference: (text, start, end) => {
                cycles.push([text, start, end]);
                const rewritten = `${text.slice(0, start)}$${text.slice(start, end)}${text.slice(end)}`;
                return { text: rewritten, selectionStart: start, selectionEnd: end + 1 };
            },
        });
        key('=');
        key('ArrowDown');
        key('ArrowDown', { shiftKey: true });
        expect(key('F4').defaultPrevented).toBe(true);
        await painted();
        expect(cycles).toEqual([['=B3:B4', 1, 6]]);
        expect([editor(root).value, editor(root).selectionStart, editor(root).selectionEnd]).toEqual(['=$B3:B4', 7, 7]);
        key('ArrowDown');
        await painted();
        expect(editor(root).value).toBe('=B4');
    });

    it('ADR-0057: Point\'s dashes lie inside the Reference Outline of the range they point at', async () => {
        const { root } = await grid({
            referencesIn: text => (text === '=B3' ? [{ start: 1, length: 2, range: { topRow: 2, leftColumn: 1, rowCount: 1, columnCount: 1 } }] : []),
        });
        key('=');
        key('ArrowDown');
        await painted();
        expect(pointOutline(root)).toEqual(['ex-point ex-point-on-reference']);
    });

    it('ADR-0058: a text written from outside stands in Point with no outline, the arrows move nothing, and taking it back restores what was pointed', async () => {
        const { root, heard, view } = await grid();
        key('=');
        key('ArrowDown');
        expect(view.writePointedText('Positions[PV]')).toBe(true);
        await painted();
        expect([editor(root).value, pointOutline(root), heard.states.at(-1)]).toEqual(['=Positions[PV]', [], 'writtenFromOutside']);
        expect(key('ArrowDown').defaultPrevented).toBe(true);
        expect(editor(root).value).toBe('=Positions[PV]');
        expect(view.takeBackPointedText()).toBe(true);
        await painted();
        expect([editor(root).value, pointOutline(root), heard.states.at(-1)]).toEqual(['=B3', ['ex-point'], 'inPoint']);
        expect(view.takeBackPointedText()).toBe(false);
        type(root, '1');
        expect(view.writePointedText('X')).toBe(false);
    });

    it('ADR-0051: pointAt without referenceText is refused by name', () => {
        expect(() => mount<Trade>({ columns, rows: trades(2), pointAt })).toThrow(/referenceText/u);
    });
});
