// The contract every Wrapper keeps with the Cell Editor's slot (ADR-0390 section 7): run at layer 2 against each one
// alike. A key typed on a cell opens the editor in the core's box with the Wrapper's field holding it and the
// keyboard; what is typed is the core's to commit at Enter, whatever the Wrapper's framework has rendered; and a
// Reject marks the Wrapper's field and keeps the keyboard in it, in the core's words.
import { createElement } from 'react';
import { act, cleanup, render } from '@testing-library/react';
import { afterEach, describe, expect, it } from 'vitest';
import type { GridColumn, GridEditIntent } from '@ex-grid/core';
import { ExGrid } from '../src/ex-grid';
import type { ExGridChrome } from '../src/chrome';

interface Trade {
    readonly book: string;
    readonly notional: number;
}

const rows: readonly Trade[] = Array.from({ length: 20 }, (_, at) => ({ book: `Book-${at}`, notional: (at + 1) * 1000 }));
const columns: readonly GridColumn<Trade>[] = [
    { name: 'Book', type: 'text', value: trade => trade.book, width: 120 },
    {
        name: 'Notional', type: 'number', value: trade => trade.notional, width: 100, editable: true,
        validate: (_row, text) => (Number.isNaN(Number(text)) ? { kind: 'reject', message: `'${text}' is not a number` } : { kind: 'accept' }),
    },
];

async function settled(): Promise<void> {
    await act(async () => {
        for (let turn = 0; turn < 4; turn++) await new Promise(resolve => { setTimeout(resolve, 0); });
    });
}

function key(name: string): void {
    const target = document.activeElement;
    if (target === null) throw new Error('Nothing holds the keyboard.');
    target.dispatchEvent(new KeyboardEvent('keydown', { key: name, bubbles: true, cancelable: true }));
}

function active(): Element {
    const holder = document.activeElement;
    if (holder === null) throw new Error('Nothing holds the keyboard.');
    return holder;
}

/** Types `text` at the end of the field as the browser does: through its own value setter, and an input event. */
function typeText(field: HTMLInputElement | HTMLTextAreaElement, text: string): void {
    if (!Reflect.set(Object.getPrototypeOf(field) as object, 'value', field.value + text, field)) throw new Error('This field has no value setter.');
    field.dispatchEvent(new Event('input', { bubbles: true }));
}

/** An ExGrid under `chrome` with the Focus on Notional's first cell and the keyboard on the grid. */
async function opened(chrome: ExGridChrome<unknown>): Promise<{ readonly root: HTMLElement; readonly edits: GridEditIntent<Trade>[] }> {
    const edits: GridEditIntent<Trade>[] = [];
    const { container } = render(createElement(ExGrid<Trade>, {
        columns, rows, rowHeight: 24, viewportHeight: 320, viewportWidth: 720, chrome, onEdit: intent => { edits.push(intent); },
    }));
    const root = container.firstElementChild;
    if (!(root instanceof HTMLElement)) throw new Error('The grid drew no root.');
    await settled();
    const viewport = root.querySelector('.ex-viewport');
    for (const type of ['mousedown', 'mouseup']) {
        const event = new MouseEvent(type, { bubbles: true, cancelable: true, button: 0, buttons: type === 'mousedown' ? 1 : 0 });
        Object.defineProperty(event, 'offsetX', { value: 130 });
        Object.defineProperty(event, 'offsetY', { value: 5 });
        viewport?.dispatchEvent(event);
    }
    root.focus();
    return { root, edits };
}

/** The field holding the keyboard, which must be the Wrapper's, inside the core's editor box. */
function editorField(root: HTMLElement): HTMLInputElement | HTMLTextAreaElement {
    const field = active();
    const box = root.querySelector('.ex-viewport .ex-editor');
    if (!(field instanceof HTMLInputElement || field instanceof HTMLTextAreaElement) || box === null || !box.contains(field) || field === box) {
        throw new Error('The Wrapper\'s field inside the core\'s editor box does not hold the keyboard.');
    }
    return field;
}

/** Registers the contract's tests against a Wrapper's Chrome: `chrome` names it, and must fill the cellEditor slot. */
export function cellEditorSlotContract(name: string, chrome: ExGridChrome<unknown>): void {
    describe(`${name}: the Cell Editor slot's contract (ADR-0390 section 7)`, () => {
        afterEach(() => { cleanup(); });

        it('ADR-0010: a key typed on a cell opens the Wrapper\'s field holding it and the keyboard, and Enter commits what was typed', async () => {
            const { root, edits } = await opened(chrome);
            key('1');
            await settled();
            const field = editorField(root);
            expect([field.value, field.selectionStart]).toEqual(['1', 1]);
            act(() => { typeText(field, '25'); });
            key('Enter');
            await settled();
            // The keyboard is back on the grid, which holds it in its own field (ADR-0080).
            expect([edits.map(intent => intent.value), root.querySelector('.ex-editor'), root.contains(active()), active() === field]).toEqual([['125'], null, true, false]);
        });

        it('ADR-0034: a Reject marks the Wrapper\'s field, which the core\'s message describes, and keeps the keyboard in it', async () => {
            const { root, edits } = await opened(chrome);
            key('a');
            await settled();
            key('Enter');
            await settled();
            const field = editorField(root);
            const message = root.querySelector(':scope > .ex-message');
            expect([edits.length, field.getAttribute('aria-invalid'), field.getAttribute('aria-describedby'), message?.textContent.trim()])
                .toEqual([0, 'true', message?.id, '\'a\' is not a number']);
        });
    });
}
