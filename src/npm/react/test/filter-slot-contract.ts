// The contract every Wrapper keeps with the filter panel's slot (ADR-0390 section 7): run at layer 2 against each
// one alike. The values are the core's, each ticked as the core says, and OK applies the core's answer; a typed
// value that does not read applies nothing, and the panel says why in the core's words; Tab, Shift+Tab and E on
// a command put the keyboard where the core asks; and Excel's letters on the value list are the core's.
import { createElement } from 'react';
import { act, cleanup, render } from '@testing-library/react';
import { afterEach, describe, expect, it } from 'vitest';
import { GridSource, type FilterSpec, type GridColumn } from '@ex-grid/core';
import { ExGrid } from '../src/ex-grid';
import type { ExGridChrome } from '../src/chrome';
import type { ContractHost } from './contract-host';

interface Trade {
    readonly trader: string | null;
    readonly id: number;
}

const traders = ['Novak', 'Osei', null, 'Ishikawa'];
const rows: readonly Trade[] = Array.from({ length: 40 }, (_, at) => ({ trader: traders[at % traders.length] ?? null, id: at })); // oxlint-disable-line ex-grid/no-stand-in-default -- the list holds a Blank on purpose
const columns: readonly GridColumn<Trade>[] = [
    { name: 'Trader', type: 'text', value: trade => trade.trader, width: 120, filterUi: 'valueList' },
    { name: 'Id', type: 'number', value: trade => trade.id, width: 80 },
];
const focusable = 'input:not([disabled]), select:not([disabled]), textarea:not([disabled]), button:not([disabled]), [tabindex]:not([tabindex="-1"])';

async function settled(): Promise<void> {
    await act(async () => {
        for (let turn = 0; turn < 6; turn++) await Promise.resolve();
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

function panelOf(root: Element): HTMLElement {
    const panel = root.querySelector<HTMLElement>(':scope > .ex-popover > .ex-popover-filter');
    if (panel === null) throw new Error('No filter panel stands.');
    return panel;
}

/** The checkbox of the value whose label reads `words`: a label around it or one naming it, as a design system's
 * checkbox may set its label beside the box rather than around it. */
function entry(panel: Element, words: string): HTMLInputElement {
    const box = [...panel.querySelectorAll('input[type=checkbox]')]
        .find(each => each instanceof HTMLInputElement && (each.labels?.[0]?.textContent.trim() ?? each.getAttribute('aria-label')) === words);
    if (!(box instanceof HTMLInputElement)) throw new Error(`No checkbox reads ${words}.`);
    return box;
}

function buttonOf(panel: Element, words: string): HTMLButtonElement {
    const button = [...panel.querySelectorAll('button')].find(each => each.textContent.trim() === words);
    if (button === undefined) throw new Error(`No button reads ${words}.`);
    return button;
}

/** An ExGrid under `chrome` over a source whose Filter the test reads. */
function mounted(chrome: ExGridChrome<unknown>, host: ContractHost): { readonly root: HTMLElement; readonly spec: (column: string) => FilterSpec | undefined } {
    const source = GridSource.from(rows);
    const { container } = render(createElement(ExGrid<Trade>, { columns, source, rowHeight: 24, viewportHeight: 320, viewportWidth: 720, chrome }), host);
    const root = container.querySelector('[role=grid]'); // a provider may draw beside it
    if (!(root instanceof HTMLElement)) throw new Error('The grid drew no root.');
    return { root, spec: column => source.snapshot().filter?.columns[column] };
}

async function openColumnMenu(root: HTMLElement, column: number): Promise<HTMLElement> {
    root.focus();
    const button = root.querySelectorAll('.ex-header-cell > .ex-menu-button')[column];
    for (const type of ['mousedown', 'mouseup', 'click']) button?.dispatchEvent(new MouseEvent(type, { bubbles: true, cancelable: true, button: 0 }));
    await settled();
    return panelOf(root);
}

/**
 * Registers the contract's tests against a Wrapper's Chrome: `chrome` names it, and must fill the filter panel's
 * slot. `ok` is the words of the panel's OK, and `search` the selector of its search box.
 */
export function filterPanelSlotContract(name: string, chrome: ExGridChrome<unknown>, ok = 'OK', search = 'input[type=search]', host: ContractHost = {}): void {
    describe(`${name}: the filter panel slot's contract (ADR-0390 section 7)`, () => {
        afterEach(() => { cleanup(); });

        it('ADR-0009: the value list is the core\'s, every value ticked; OK applies what was ticked, and the keyboard is back on the grid', async () => {
            const { root, spec } = mounted(chrome, host);
            await settled();
            const panel = await openColumnMenu(root, 0);
            expect(['Novak', 'Osei', 'Ishikawa'].map(words => entry(panel, words).checked)).toEqual([true, true, true]);
            entry(panel, 'Osei').click();
            await settled();
            buttonOf(panel, ok).click();
            await settled();
            expect([spec('Trader'), root.querySelector('.ex-popover'), active()]).toEqual([
                { clauses: [{ operator: 'in', values: ['Novak', null, 'Ishikawa'] }] }, null, root,
            ]);
        });

        it('ADR-0023 / KB-31: the condition\'s field submits the slot\'s form; a typed value that does not read applies nothing, and the panel says why in the core\'s words', async () => {
            const { root, spec } = mounted(chrome, host);
            await settled();
            const panel = await openColumnMenu(root, 1);
            const field = panel.querySelector<HTMLInputElement>('input:not([type]), input[type=text]');
            if (field === null) throw new Error('The condition form has no field for its value.');
            // Enter in it submits the slot's form, which applies as OK does (KB-31): no form of the contents' own between.
            expect(field.form).toBe(panel);
            act(() => { typeText(field, '12abc'); });
            buttonOf(panel, ok).click();
            await settled();
            expect([spec('Id'), root.querySelector('.ex-popover') !== null, panel.textContent.includes('“12abc”')]).toEqual([undefined, true, true]);
        });

        it('ADR-0044: Tab, Shift+Tab and E on a command put the keyboard on the panel\'s first control, its last, and its search box', async () => {
            const { root } = mounted(chrome, host);
            await settled();
            const panel = await openColumnMenu(root, 0);
            const command = active();
            const controls = (): Element[] => [...panel.querySelectorAll(focusable)];
            key(command, 'Tab');
            await settled();
            expect(active()).toBe(controls()[0]);
            root.querySelector<HTMLElement>('.ex-popover > .ex-focus-wrap:last-child')?.focus();
            await settled();
            expect(active()).toBe(command);
            key(command, 'Tab', { shiftKey: true });
            await settled();
            expect(active()).toBe(controls().at(-1));
            root.querySelector<HTMLElement>('.ex-popover > .ex-focus-wrap:last-child')?.focus();
            await settled();
            key(active(), 'e');
            await settled();
            expect(active()).toBe(panel.querySelector(search));
        });

        it('ADR-0044: S on the value list sorts through the core and closes; another letter is the list\'s own', async () => {
            const { root } = mounted(chrome, host);
            await settled();
            const panel = await openColumnMenu(root, 0);
            const box = entry(panel, 'Novak');
            box.focus();
            expect(key(box, 'x').defaultPrevented).toBe(false);
            expect(key(box, 's').defaultPrevented).toBe(true);
            await settled();
            expect([root.querySelector('.ex-popover'), root.querySelector('.ex-header-cell')?.getAttribute('aria-sort'), active()]).toEqual([null, 'ascending', root]);
        });
    });
}
