// @vitest-environment happy-dom
// Layer 2 (ADR-0350 section 13): declared keys (ADR-0050, item 14). A key the Consumer names is taken from the
// browser and raised, with whether an edit is open and the Selection at the key, and means nothing to the core.
import { afterEach, describe, expect, it } from 'vitest';
import type { GridDeclaredKeyPress } from '../src/dom/grid-view';
import { columns, mount, painted, q, trades, unmountAll } from './grid-harness';

afterEach(unmountAll);

function press(root: HTMLElement, x: number, y: number): void {
    for (const type of ['mousedown', 'mouseup']) {
        const event = new MouseEvent(type, { bubbles: true, cancelable: true, button: 0, buttons: type === 'mousedown' ? 1 : 0 });
        Object.defineProperty(event, 'offsetX', { value: x });
        Object.defineProperty(event, 'offsetY', { value: y });
        q(root, '.ex-viewport').dispatchEvent(event);
    }
}

function key(target: HTMLElement, name: string): KeyboardEvent {
    const event = new KeyboardEvent('keydown', { key: name, bubbles: true, cancelable: true });
    target.dispatchEvent(event);
    return event;
}

describe('declared keys', () => {
    it('ADR-0050 item 14: a declared key is taken and raised with the Selection at the key, and moves nothing', async () => {
        const heard: GridDeclaredKeyPress[] = [];
        const { root } = mount({ columns, rows: trades(20), rowHeight: 24, viewportHeight: 480, declaredKeys: ['F9'], onDeclaredKey: p => { heard.push(p); } });
        await painted();
        press(root, 130, 29);
        await painted();
        const taken = key(root, 'F9');
        const left = key(root, 'F8');
        await painted();
        expect([taken.defaultPrevented, left.defaultPrevented]).toEqual([true, false]);
        expect(heard.map(p => [p.key, p.editOpen, p.selection.focus, p.rowSequenceVersion])).toEqual([['F9', false, { row: 1, column: 1 }, 0]]);
    });

    it('ADR-0050 item 14: with an edit open the key is raised as such, and the editor keeps its text', async () => {
        const heard: GridDeclaredKeyPress[] = [];
        const editable = columns.map(column => ({ ...column, editable: true }));
        const { root } = mount({
            columns: editable, rows: trades(20), rowHeight: 24, viewportHeight: 480, onEdit: () => undefined,
            declaredKeys: ['F9'], onDeclaredKey: p => { heard.push(p); },
        });
        await painted();
        press(root, 130, 5);
        await painted();
        key(q(root, '.ex-key-field'), 'F2');
        await painted();
        const editor = q(root, '.ex-editor');
        if (!(editor instanceof HTMLInputElement)) throw new TypeError('The editor is an input.');
        const before = editor.value;
        expect(key(editor, 'F9').defaultPrevented).toBe(true);
        await painted();
        expect([heard.map(p => p.editOpen), root.querySelector('.ex-editor') === editor, editor.value]).toEqual([[true], true, before]);
    });

    it('ADR-0100: the Alt key alone, declared, is raised; with another modifier held it is not', async () => {
        const heard: string[] = [];
        const { root } = mount({ columns, rows: trades(20), rowHeight: 24, viewportHeight: 480, declaredKeys: ['Alt+Alt'], onDeclaredKey: p => { heard.push(p.key); } });
        await painted();
        press(root, 130, 5);
        await painted();
        const alone = new KeyboardEvent('keydown', { key: 'Alt', altKey: true, bubbles: true, cancelable: true });
        const chord = new KeyboardEvent('keydown', { key: 'Alt', altKey: true, ctrlKey: true, bubbles: true, cancelable: true });
        root.dispatchEvent(alone);
        root.dispatchEvent(chord);
        expect([heard, alone.defaultPrevented, chord.defaultPrevented]).toEqual([['Alt+Alt'], true, false]);
    });

    it('ADR-0050 item 14: a key nobody hears, a key the core answers and a key in no canonical form are refused by name', () => {
        const base = { columns, rows: trades(2), rowHeight: 24, viewportHeight: 480 };
        expect(() => mount({ ...base, declaredKeys: ['F9'] })).toThrow(/no onDeclaredKey hears them/u);
        expect(() => mount({ ...base, declaredKeys: ['Enter'], onDeclaredKey: () => undefined })).toThrow(/'Enter' is one the core answers itself/u);
        expect(() => mount({ ...base, declaredKeys: ['Shift+Control+b'], onDeclaredKey: () => undefined })).toThrow(/not in the canonical form/u);
    });
});
