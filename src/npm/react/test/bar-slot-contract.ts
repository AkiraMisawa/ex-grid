// The contract every Wrapper keeps with the Formula Bar's slots (ADR-0390 section 7): run at layer 2 against each one
// alike. The text field in the core's box reads the Focus cell and is the Cell Editor's second surface, its typing
// committed by the core at Enter; the Name Box names the Focus, and what is typed into it reaches the Consumer at
// Enter. What the fields show and what their keys mean are the core's, whatever the Wrapper's framework renders.
import { createElement } from 'react';
import { act, cleanup, render } from '@testing-library/react';
import { afterEach, describe, expect, it } from 'vitest';
import type { GridColumn, GridEditIntent } from '@ex-grid/core';
import { ExGrid } from '../src/ex-grid';
import type { ExGridChrome } from '../src/chrome';
import type { ContractHost } from './contract-host';

interface Trade {
    readonly book: string;
    readonly notional: number;
}

const rows: readonly Trade[] = Array.from({ length: 20 }, (_, at) => ({ book: `Book-${at}`, notional: (at + 1) * 1000 }));
const columns: readonly GridColumn<Trade>[] = [
    { name: 'Book', type: 'text', value: trade => trade.book, width: 120, editable: true },
    { name: 'Notional', type: 'number', value: trade => trade.notional, width: 100 },
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

/** Types `text` at the end of the field as the browser does: through its own value setter, and an input event. */
function typeText(field: HTMLInputElement | HTMLTextAreaElement, text: string): void {
    if (!Reflect.set(Object.getPrototypeOf(field) as object, 'value', field.value + text, field)) throw new Error('This field has no value setter.');
    field.dispatchEvent(new Event('input', { bubbles: true }));
}

/** A press on a cell, as the browser lands it on the rows: Book is to 120 px, rows are 24 px. */
function press(root: HTMLElement, x: number, y: number): void {
    const viewport = root.querySelector('.ex-viewport');
    if (viewport === null) throw new Error('The grid drew no Viewport.');
    for (const type of ['mousedown', 'mouseup']) {
        const event = new MouseEvent(type, { bubbles: true, cancelable: true, button: 0, buttons: type === 'mousedown' ? 1 : 0 });
        Object.defineProperty(event, 'offsetX', { value: x });
        Object.defineProperty(event, 'offsetY', { value: y });
        viewport.dispatchEvent(event);
    }
}

/** The Wrapper's field inside the core's box `selector` in the bar. */
function fieldIn(root: HTMLElement, selector: string): HTMLInputElement | HTMLTextAreaElement {
    const box = root.querySelector(`.ex-formula-bar ${selector}`);
    const field = box?.querySelector('input, textarea');
    if (box === null || !(field instanceof HTMLInputElement || field instanceof HTMLTextAreaElement)) {
        throw new Error(`The Wrapper drew no field inside the core's ${selector} box.`);
    }
    return field;
}

async function shown(chrome: ExGridChrome<unknown>, host: ContractHost): Promise<{ readonly root: HTMLElement; readonly edits: GridEditIntent<Trade>[]; readonly entered: string[] }> {
    const edits: GridEditIntent<Trade>[] = [];
    const entered: string[] = [];
    const { container } = render(createElement(ExGrid<Trade>, {
        columns, rows, rowHeight: 24, viewportHeight: 320, viewportWidth: 720, chrome, showFormulaBar: true,
        nameBoxLabel: cell => `R${String(cell.row + 1)}C${String(cell.column + 1)}`,
        onEdit: intent => { edits.push(intent); }, onNameBoxEntered: typed => { entered.push(typed); },
    }), host);
    const root = container.querySelector('[role=grid]'); // a provider may draw beside it
    if (!(root instanceof HTMLElement)) throw new Error('The grid drew no root.');
    await settled();
    press(root, 10, 29);
    await settled();
    return { root, edits, entered };
}

/** Registers the contract's tests against a Wrapper's Chrome: `chrome` names it, and must fill the formulaBarText and
 * nameBox slots. */
export function formulaBarSlotContract(name: string, chrome: ExGridChrome<unknown>, host: ContractHost = {}): void {
    describe(`${name}: the Formula Bar slots' contract (ADR-0390 section 7)`, () => {
        afterEach(() => { cleanup(); });

        it('ADR-0051: the text field reads the Focus cell, typing in it is the editor\'s, and Enter commits it once', async () => {
            const { root, edits } = await shown(chrome, host);
            const text = fieldIn(root, '.ex-formula-bar-text');
            expect([text.value, text.readOnly]).toEqual(['Book-1', false]);
            act(() => { text.focus(); });
            act(() => { typeText(text, '-x'); });
            await settled();
            key('Enter');
            await settled();
            expect([edits.map(intent => intent.value), fieldIn(root, '.ex-formula-bar-text').value, root.contains(document.activeElement)])
                .toEqual([['Book-1-x'], 'Book-2', true]);
        });

        it('ADR-0051: the Name Box names the Focus, and Enter hands what was typed to the Consumer and the keyboard back to the grid', async () => {
            const { root, entered } = await shown(chrome, host);
            const box = fieldIn(root, '.ex-name-box');
            expect(box.value).toBe('R2C1');
            act(() => { box.focus(); });
            act(() => { typeText(box, '0'); });
            await settled();
            const form = root.querySelector<HTMLFormElement>('.ex-name-box-form');
            act(() => { form?.requestSubmit(); });
            await settled();
            expect([entered, root.contains(document.activeElement), document.activeElement === box]).toEqual([['R2C10'], true, false]);
        });
    });
}
