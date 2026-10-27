// @vitest-environment happy-dom
// Layer 2 (ADR-0350 section 13): Reference Outlines in the DOM (ADR-0057). While an edit is open, every range the
// Consumer's References name is outlined in the selection overlay in its colour, and the keys' colours are told
// once per change; the columns a Consumer asks for are outlined over their bodies with or without an edit (DC-49,
// DC-50). Beneath the editor surface the edit is typed in, a layer draws its text with each Reference coloured.
import { afterEach, describe, expect, it, vi } from 'vitest';
import type { GridEditorReference, ReferenceKeyColour } from '../src/cells/reference-colouring';
import type { GridColumn } from '../src/dom/grid-column';
import type { GridOptions } from '../src/dom/grid-view';
import { selectionRange } from '../src/selection/range';
import { mount, painted, q, trades, unmountAll, type Trade } from './grid-harness';

const columns: readonly GridColumn<Trade>[] = [
    { name: 'Book', type: 'text', value: trade => trade.book, width: 120, editable: true },
    { name: 'Notional', type: 'number', value: trade => trade.notional, width: 100, editable: true },
    { name: 'Desk', type: 'text', value: trade => trade.book, width: 100 },
];

// Column letters and row numbers as a Sheet writes them, from A and 1.
const columnOf = (letter: string): number => letter.charCodeAt(0) - 65;
const rowOf = (digits: string): number => Number(digits) - 1;

// A Consumer's References, as a Sheet's would answer them: `B3` or `A1:B2` names cells, `[Name]` a key.
function referencesIn(text: string): readonly GridEditorReference[] {
    if (!text.startsWith('=')) return [];
    return [...text.matchAll(/([A-Z])(\d+)(?::([A-Z])(\d+))?|\[(\w+)\]/gu)].map(match => {
        const start = match.index;
        const { length } = match[0];
        const [, left, top, right, bottom, name] = match;
        if (name !== undefined) return { start, length, key: name };
        if (left === undefined || top === undefined) throw new Error('A cell is a letter and a number.');
        const topRow = rowOf(top);
        const leftColumn = columnOf(left);
        const bottomRow = bottom === undefined ? topRow : rowOf(bottom);
        const rightColumn = right === undefined ? leftColumn : columnOf(right);
        return { start, length, range: selectionRange(topRow, leftColumn, bottomRow - topRow + 1, rightColumn - leftColumn + 1) };
    });
}

const outlines = (root: HTMLElement): string[] =>
    [...root.querySelectorAll('.ex-viewport > .ex-selection > .ex-reference-outline')].map(piece => piece.className);

function mouse(root: HTMLElement, name: string, x: number, y: number): MouseEvent {
    const event = new MouseEvent(name, { bubbles: true, cancelable: true, button: 0, buttons: name === 'mouseup' ? 0 : 1 });
    Object.defineProperty(event, 'offsetX', { value: x });
    Object.defineProperty(event, 'offsetY', { value: y });
    q(root, '.ex-viewport').dispatchEvent(event);
    return event;
}

function key(name: string): void {
    const target = document.activeElement;
    if (target === null) throw new Error('Nothing holds the keyboard.');
    target.dispatchEvent(new KeyboardEvent('keydown', { key: name, bubbles: true, cancelable: true }));
}

// Opens Caret on the first row's Book cell, and writes `text` into the editor as the field does.
async function edit(root: HTMLElement, text: string): Promise<HTMLInputElement> {
    await painted();
    // The scroller takes DOM focus from a press the grid does not prevent, as the browser gives it.
    if (!mouse(root, 'mousedown', 10, 5).defaultPrevented) q(root, '.ex-scroller').focus();
    mouse(root, 'mouseup', 10, 5);
    await painted();
    key('F2');
    await painted();
    const field = q(root, 'input.ex-editor');
    if (!(field instanceof HTMLInputElement)) throw new TypeError('The editor is an input.');
    retype(field, text);
    await painted();
    return field;
}

function retype(field: HTMLInputElement, text: string): void {
    field.value = text;
    field.dispatchEvent(new Event('input', { bubbles: true }));
}

function mounted(options: Partial<GridOptions<Trade>>): ReturnType<typeof mount<Trade>> {
    return mount({ columns, rows: trades(20), rowHeight: 24, viewportHeight: 480, onEdit: () => undefined, ...options });
}

const grid = (options: Partial<GridOptions<Trade>>): HTMLElement => mounted(options).root;

afterEach(unmountAll);

describe('Reference Outlines (ADR-0057)', () => {
    it('DC-49: each range the text names is outlined in its colour, a range named twice twice in one colour, and a commit takes them away', async () => {
        const root = grid({ referencesIn });
        const field = await edit(root, '=B3+A1:B2+B3');
        expect(outlines(root)).toEqual([
            'ex-reference-outline ex-reference-1', 'ex-reference-outline ex-reference-2', 'ex-reference-outline ex-reference-1',
        ]);
        // Typing asks again of the text as it now stands.
        retype(field, '=C4');
        await painted();
        expect(outlines(root)).toEqual(['ex-reference-outline ex-reference-1']);
        key('Enter');
        await painted();
        expect(outlines(root)).toEqual([]);
    });

    it('DC-49: an outline is cut to the grid, and one wholly off it is drawn nowhere', async () => {
        const root = grid({ referencesIn });
        await edit(root, '=C19:D25+Z1');
        const pieces = [...root.querySelectorAll('.ex-viewport > .ex-selection > .ex-reference-outline')];
        expect(pieces.map(piece => piece.className)).toEqual(['ex-reference-outline ex-reference-1']);
        // Two rows of the one column the grid has there: rows 19 and 20, Desk alone.
        expect(pieces[0]?.getAttribute('style')).toMatch(/height: 48px/u);
        expect(pieces[0]?.getAttribute('style')).toMatch(/width: 100px/u);
    });

    it('DC-49: each key\'s colour is told once per change, a range\'s never, and an empty list as the edit ends', async () => {
        const told: (readonly ReferenceKeyColour[])[] = [];
        const root = grid({ referencesIn, onReferenceKeyColoursChanged: keys => { told.push(keys); } });
        const field = await edit(root, '=A1+[Price]');
        retype(field, '=A1+[Price]+1');
        await painted();
        retype(field, '=A1+[Price]+[Qty]');
        await painted();
        key('Escape');
        await painted();
        expect(told).toEqual([[{ key: 'Price', colour: 2 }], [{ key: 'Price', colour: 2 }, { key: 'Qty', colour: 3 }], []]);
    });

    it('DC-50: the columns asked for are outlined over their bodies with no edit and no Selection; a name not shown nowhere', async () => {
        const root = grid({ outlinedColumns: [{ column: 'Notional', colour: 4 }, { column: 'Missing', colour: 5 }, { column: 'Notional', colour: 6 }] });
        await painted();
        const pieces = [...root.querySelectorAll('.ex-viewport > .ex-selection > .ex-reference-outline')];
        expect(pieces.map(piece => piece.className)).toEqual(['ex-reference-outline ex-reference-4', 'ex-reference-outline ex-reference-6']);
        expect(pieces[0]?.getAttribute('style')).toMatch(/width: 100px/u);
    });

    it('ADR-0057: a column asked for in a colour outside the palette, or with no name, is refused by name', () => {
        expect(() => grid({ outlinedColumns: [{ column: 'Book', colour: 9 as never }] })).toThrow(/palette/u);
        expect(() => grid({ outlinedColumns: [{ column: '', colour: 1 }] })).toThrow(/outlinedColumns\[0\]/u);
    });

    it('DC-49: a grid destroyed with an edit open tells that no key is read any more', async () => {
        const told: (readonly ReferenceKeyColour[])[] = [];
        const { root, view } = mounted({ referencesIn, onReferenceKeyColoursChanged: keys => { told.push(keys); } });
        await edit(root, '=[Price]');
        view.destroy();
        expect(told).toEqual([[{ key: 'Price', colour: 1 }], []]);
    });

    it('ADR-0057: an answer refused is refused in the input that asked it, and the passes after it paint', async () => {
        const root = grid({ referencesIn: text => (text === '=bad' ? [{ start: 0, length: 9, key: 'X' }] : referencesIn(text)) });
        const field = await edit(root, '=A1');
        expect(outlines(root)).toEqual(['ex-reference-outline ex-reference-1']);
        field.value = '=bad';
        expect(() => { field.dispatchEvent(new Event('input')); }).toThrow(/does not lie inside/u);
        await painted();
        expect(outlines(root)).toEqual([]);
        retype(field, '=B2');
        await painted();
        expect(outlines(root)).toEqual(['ex-reference-outline ex-reference-1']);
    });

    it('DC-49: a handler handed over in place of another is told the keys standing', async () => {
        const first: (readonly ReferenceKeyColour[])[] = [];
        const second: (readonly ReferenceKeyColour[])[] = [];
        const options = { columns, rows: trades(20), rowHeight: 24, viewportHeight: 480, onEdit: () => undefined, referencesIn };
        const { root, view } = mount<Trade>({ ...options, onReferenceKeyColoursChanged: keys => { first.push(keys); } });
        await edit(root, '=[Price]');
        view.update({ ...options, onReferenceKeyColoursChanged: keys => { second.push(keys); } });
        await painted();
        expect([first, second]).toEqual([[[{ key: 'Price', colour: 1 }]], [[{ key: 'Price', colour: 1 }]]]);
    });
});

// The layer beneath a field, and what it carries.
function layerOf(field: HTMLInputElement): { readonly text: string | null; readonly colours: string | null } {
    const layer = field.previousElementSibling;
    if (layer === null || !layer.classList.contains('ex-reference-text')) throw new Error('No coloured text stands before the field.');
    return { text: layer.getAttribute('data-ex-text'), colours: layer.getAttribute('data-ex-colours') };
}

const prefix = (root: HTMLElement): string => root.id.replace(/root$/u, '');

// A Custom Highlight API for a DOM that has none: a Highlight is a set of ranges, the registry a map.
class FakeHighlight extends Set<AbstractRange> {}

describe('The coloured text (ADR-0057)', () => {
    it('stands before the editor with the field\'s text and each Reference\'s colour, shows only in the field holding the keyboard, and goes with the edit', async () => {
        const root = grid({ referencesIn, showFormulaBar: true });
        const field = await edit(root, '=B3+[Price]');
        const p = prefix(root);
        expect(layerOf(field)).toEqual({ text: '=B3+[Price]', colours: `1,2,${p}reference-1 4,7,${p}reference-2` });
        expect(field.classList.contains('ex-reference-text-shown')).toBe(true);
        // The bar carries the same text, and keeps its plain text while the keyboard is in the cell.
        const bar = q(root, 'input.ex-formula-bar-text');
        if (!(bar instanceof HTMLInputElement)) throw new TypeError('The bar\'s text is an input.');
        expect(layerOf(bar)).toEqual({ text: '=B3+[Price]', colours: `1,2,${p}reference-1 4,7,${p}reference-2` });
        expect(bar.classList.contains('ex-reference-text-shown')).toBe(false);
        key('Enter');
        await painted();
        expect(root.querySelector('.ex-reference-text-cell')).toBeNull();
        expect(layerOf(bar)).toEqual({ text: '', colours: '' });
    });

    it('is not shown while an IME composes in the field, and comes back as the composition ends', async () => {
        const root = grid({ referencesIn });
        const field = await edit(root, '=B3');
        field.dispatchEvent(new CompositionEvent('compositionstart', { bubbles: true }));
        expect(field.classList.contains('ex-reference-text-shown')).toBe(false);
        field.dispatchEvent(new CompositionEvent('compositionend', { bubbles: true }));
        expect(field.classList.contains('ex-reference-text-shown')).toBe(true);
    });

    it('the Reference Point writes lies on the grey ground in its pointed shade, until a key typed after it ends pointing', async () => {
        const root = grid({
            referencesIn,
            pointAt: (text, caret) => caret === text.length && /[=+]$/u.test(text),
            referenceText: range => `${String.fromCharCode(65 + range.leftColumn)}${range.topRow + 1}`,
        });
        const field = await edit(root, '=B3+');
        // F2 turns Caret into Point, where the arrows point.
        key('F2');
        key('ArrowDown');
        await painted();
        const p = prefix(root);
        expect(field.value).toBe('=B3+A2');
        expect(layerOf(field).colours).toBe(`4,2,${p}reference-pointed 1,2,${p}reference-1 4,2,${p}reference-2-pointed`);
        retype(field, '=B3+A2*');
        await painted();
        expect(layerOf(field).colours).toBe(`1,2,${p}reference-1 4,2,${p}reference-2`);
    });

    it('colours through highlights of the grid\'s own names, taken out as the edit ends and with the grid', async () => {
        vi.stubGlobal('Highlight', FakeHighlight);
        const registry = new Map<string, FakeHighlight>();
        const css = CSS;
        vi.stubGlobal('CSS', { escape: (text: string) => css.escape(text), highlights: registry });
        try {
            const { root, view } = mounted({ referencesIn });
            const field = await edit(root, '=B3');
            const p = prefix(root);
            const coloured = registry.get(`${p}reference-1`);
            if (coloured === undefined) throw new Error('No Reference was coloured.');
            const [range] = [...coloured];
            expect([range?.startOffset, range?.endOffset]).toEqual([1, 3]);
            expect(root.querySelector('style')?.textContent).toContain(`::highlight(${p}reference-1){color:var(--ex-reference-text-1)}`);
            key('Escape');
            await painted();
            expect(coloured.size).toBe(0);
            expect(field.isConnected).toBe(false);
            view.destroy();
            expect(registry.has(`${p}reference-1`)).toBe(false);
        } finally {
            vi.unstubAllGlobals();
        }
    });
});
