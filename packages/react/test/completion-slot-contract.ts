// The contract every Wrapper keeps with the completion slot (ADR-0390 section 7): run at layer 2 against each one
// alike. The Consumer's candidates are drawn in the core's box beneath the editor, each carrying the id the editor
// names, so ↑ and ↓, which stay the editor's, move what assistive technology hears; a click on a candidate accepts it
// with the keyboard left in the editor; and the argument hint is drawn beneath the list.
import { createElement } from 'react';
import { act, cleanup, render } from '@testing-library/react';
import { afterEach, describe, expect, it } from 'vitest';
import { EditorCompletion, EditorHint, type GridColumn } from '@ex-grid/core';
import { ExGrid } from '../src/ex-grid';
import type { ExGridChrome } from '../src/chrome';
import type { ContractHost } from './contract-host';

interface Trade {
    readonly book: string;
}

const rows: readonly Trade[] = Array.from({ length: 20 }, (_, at) => ({ book: `Book-${at}` }));
const columns: readonly GridColumn<Trade>[] = [{ name: 'Book', type: 'text', value: trade => trade.book, width: 120, editable: true }];

// A sheet's answer, cut down: the names that begin with the letters before the caret, and after `SUM(` the hint.
function answer(text: string, caret: number): EditorCompletion | undefined {
    const before = text.slice(0, caret);
    if (before.endsWith('SUM(')) return new EditorCompletion({ candidates: [], hint: new EditorHint({ text: 'SUM(number1, ...)', emphasisStart: 4, emphasisLength: 7 }) });
    const letters = /^=([A-Z]+)$/u.exec(before)?.[1];
    if (letters === undefined) return undefined;
    const candidates = ['SUM', 'SUMIF'].filter(name => name.startsWith(letters))
        .map(name => ({ label: name, start: 1, length: letters.length, insert: `${name}(` }));
    return new EditorCompletion({ candidates });
}

async function settled(): Promise<void> {
    await act(async () => {
        for (let turn = 0; turn < 4; turn++) await new Promise(resolve => { setTimeout(resolve, 0); });
    });
}

function active(): HTMLInputElement | HTMLTextAreaElement {
    const holder = document.activeElement;
    if (!(holder instanceof HTMLInputElement || holder instanceof HTMLTextAreaElement)) throw new Error('No text field holds the keyboard.');
    return holder;
}

function key(name: string): void {
    active().dispatchEvent(new KeyboardEvent('keydown', { key: name, bubbles: true, cancelable: true }));
}

/** Types `text` at the end of the field as the browser does: through its own value setter, and an input event. */
function typeText(field: HTMLInputElement | HTMLTextAreaElement, text: string): void {
    if (!Reflect.set(Object.getPrototypeOf(field) as object, 'value', field.value + text, field)) throw new Error('This field has no value setter.');
    field.setSelectionRange(field.value.length, field.value.length);
    field.dispatchEvent(new Event('input', { bubbles: true }));
}

/** The element the editor names by `attribute`, inside the core's completion box. */
function named(root: HTMLElement, attribute: 'aria-controls' | 'aria-activedescendant'): HTMLElement {
    const id = active().getAttribute(attribute);
    const box = root.querySelector('.ex-completion');
    const element = id === null ? null : root.ownerDocument.getElementById(id);
    if (box === null || element === null || !box.contains(element)) throw new Error(`The editor's ${attribute} names nothing in the completion box.`);
    return element;
}

/** An ExGrid under `chrome` asking the answer above, with `=` and `typed` written in Book's first cell. */
async function typing(chrome: ExGridChrome<unknown>, host: ContractHost, typed: string): Promise<HTMLElement> {
    const { container } = render(createElement(ExGrid<Trade>, {
        columns, rows, rowHeight: 24, viewportHeight: 320, viewportWidth: 720, chrome, onEdit: () => undefined, completeEditorText: answer,
    }), host);
    const root = container.querySelector('[role=grid]'); // a provider may draw beside it
    if (!(root instanceof HTMLElement)) throw new Error('The grid drew no root.');
    await settled();
    const viewport = root.querySelector('.ex-viewport');
    for (const type of ['mousedown', 'mouseup']) {
        const event = new MouseEvent(type, { bubbles: true, cancelable: true, button: 0, buttons: type === 'mousedown' ? 1 : 0 });
        Object.defineProperty(event, 'offsetX', { value: 10 });
        Object.defineProperty(event, 'offsetY', { value: 5 });
        viewport?.dispatchEvent(event);
    }
    root.focus();
    await settled();
    root.ownerDocument.activeElement?.dispatchEvent(new KeyboardEvent('keydown', { key: '=', bubbles: true, cancelable: true }));
    await settled();
    typeText(active(), typed);
    await settled();
    return root;
}

/**
 * Registers the contract's tests against a Wrapper's Chrome: `chrome` names it, and must fill the editorCompletion
 * slot.
 */
export function completionSlotContract(name: string, chrome: ExGridChrome<unknown>, host: ContractHost = {}): void {
    describe(`${name}: the contract of the completion slot (ADR-0390 section 7)`, () => {
        afterEach(() => { cleanup(); });

        it('ADR-0051: the candidates stand in the core\'s box, the list and the chosen one named by the editor, and ↓ moves the choice', async () => {
            const root = await typing(chrome, host, 'SU');
            expect(named(root, 'aria-controls').textContent).toMatch(/SUM.*SUMIF/su);
            expect(named(root, 'aria-activedescendant').textContent).toMatch(/^\s*SUM\s*$/u);
            key('ArrowDown');
            await settled();
            const chosen = named(root, 'aria-activedescendant');
            expect([chosen.textContent.trim(), chosen.getAttribute('aria-selected')]).toEqual(['SUMIF', 'true']);
        });

        it('ADR-0051: a click on a candidate accepts it, and the editor keeps the keyboard', async () => {
            const root = await typing(chrome, host, 'SU');
            const field = active();
            key('ArrowDown');
            await settled();
            const candidate = named(root, 'aria-activedescendant');
            const down = new MouseEvent('mousedown', { bubbles: true, cancelable: true, button: 0 });
            candidate.dispatchEvent(down);
            candidate.dispatchEvent(new MouseEvent('click', { bubbles: true, cancelable: true, button: 0 }));
            await settled();
            expect([down.defaultPrevented, field.value, document.activeElement === field]).toEqual([true, '=SUMIF(', true]);
        });

        it('ADR-0051: the argument hint is drawn in the core\'s box', async () => {
            const root = await typing(chrome, host, 'SUM(');
            expect(root.querySelector('.ex-completion')?.textContent).toContain('SUM(number1, ...)');
        });
    });
}
