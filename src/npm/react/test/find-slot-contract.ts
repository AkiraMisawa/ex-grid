// The contract every Wrapper keeps with the find panel's slot (ADR-0390 section 7): run at layer 2 against each one
// alike. The find field takes the keyboard at the opening; what is typed is the core's to search, and Enter in the
// field steps from the Focus, through the core's form; Ctrl+H shows Replace in the same panel, and what Replace All
// came to is said in the core's words.
import { createElement } from 'react';
import { act, cleanup, render } from '@testing-library/react';
import { afterEach, describe, expect, it } from 'vitest';
import { replacedCells, type GridColumn, type GridSelection } from '@ex-grid/core';
import { ExGrid } from '../src/ex-grid';
import type { ExGridChrome } from '../src/chrome';

interface Deal {
    readonly trader: string;
}

const traders = ['Ishikawa', 'Novak', 'Osei', 'Lee'];
const rows: readonly Deal[] = Array.from({ length: 40 }, (_, at) => ({ trader: traders[Math.floor(at / 3) % traders.length] ?? 'Nobody' })); // oxlint-disable-line ex-grid/no-stand-in-default -- the index is always inside the list
const columns: readonly GridColumn<Deal>[] = [{ name: 'Trader', type: 'text', value: deal => deal.trader, width: 120, editable: true }];

async function settled(): Promise<void> {
    await act(async () => {
        for (let turn = 0; turn < 4; turn++) await new Promise(resolve => { setTimeout(resolve, 0); });
    });
}

function key(target: Element, name: string, init: KeyboardEventInit = {}): KeyboardEvent {
    const event = new KeyboardEvent('keydown', { key: name, bubbles: true, cancelable: true, ...init });
    target.dispatchEvent(event);
    return event;
}

function active(): Element {
    const holder = document.activeElement;
    if (holder === null) throw new Error('Nothing holds the keyboard.');
    return holder;
}

/** Types `text` as the browser does: through the field's own value setter, which a framework's change tracking
 * reads, and an input event after it. */
function typeText(field: HTMLInputElement, text: string): void {
    if (!Reflect.set(HTMLInputElement.prototype, 'value', text, field)) throw new Error('This document\'s fields have no value setter.');
    field.dispatchEvent(new Event('input', { bubbles: true }));
}

function mounted(chrome: ExGridChrome<unknown>): { readonly root: HTMLElement; readonly selections: GridSelection[] } {
    const selections: GridSelection[] = [];
    const { container } = render(createElement(ExGrid<Deal>, {
        columns, rows, rowHeight: 24, viewportHeight: 320, viewportWidth: 720, findsHeaders: false, chrome,
        onSelectionChanged: selection => { selections.push(selection); }, onEdit: () => undefined, onReplace: () => replacedCells(12, 0),
    }));
    const root = container.firstElementChild;
    if (!(root instanceof HTMLElement)) throw new Error('The grid drew no root.');
    return { root, selections };
}

function panelOf(root: Element): HTMLElement {
    const panel = root.querySelector<HTMLElement>(':scope > .ex-popover-find');
    if (panel === null) throw new Error('No find panel stands.');
    return panel;
}

/**
 * Registers the contract's tests against a Wrapper's Chrome: `chrome` names it, and must fill the find panel's slot.
 * `replaceAll` is the words of its Replace All.
 */
export function findPanelSlotContract(name: string, chrome: ExGridChrome<unknown>, replaceAll = 'Replace All'): void {
    describe(`${name}: the find panel slot's contract (ADR-0390 section 7)`, () => {
        afterEach(() => { cleanup(); });

        it('FD-1 / FD-5: the find field takes the keyboard; Enter in it steps from the Focus through the core\'s form, and the keyboard stays', async () => {
            const { root, selections } = mounted(chrome);
            await settled();
            root.focus();
            key(root, 'f', { ctrlKey: true });
            await settled();
            const field = active();
            if (!(field instanceof HTMLInputElement) || !panelOf(root).contains(field)) throw new Error('The find field does not hold the keyboard.');
            expect(field.form).toBe(panelOf(root).querySelector('form'));
            act(() => { typeText(field, 'novak'); });
            await settled();
            key(field, 'Enter');
            field.form?.requestSubmit();
            await settled();
            expect([selections.at(-1)?.focus, active()]).toEqual([{ row: 3, column: 0 }, field]);
        });

        it('ADR-0332 §2 / §3: Ctrl+H shows Replace in the same panel, and Replace All says what the Consumer answered', async () => {
            const { root } = mounted(chrome);
            await settled();
            root.focus();
            key(root, 'h', { ctrlKey: true });
            await settled();
            const field = active();
            if (!(field instanceof HTMLInputElement)) throw new Error('The find field does not hold the keyboard.');
            act(() => { typeText(field, 'novak'); });
            const button = [...panelOf(root).querySelectorAll('button')].find(each => each.textContent.trim() === replaceAll);
            if (button === undefined) throw new Error(`No button reads ${replaceAll}.`);
            button.click();
            await settled();
            expect([root.querySelectorAll('.ex-popover').length, panelOf(root).textContent.includes('Replaced 12 cells.')]).toEqual([1, true]);
        });
    });
}
