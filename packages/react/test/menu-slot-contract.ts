// The contract every Wrapper keeps with the menu slots (ADR-0390 section 7): run at layer 2 against each one
// alike, the built-in-free test Chrome here and each design system's Wrapper. The items are the core's commands
// in the core's order, each enabled as the core says, with the core's words; the first enabled item takes the
// keyboard; a key is answered by the core's table; and the Wrapper decides nothing: a disabled item runs nothing,
// and a command runs with the menu closed and the keyboard back on the grid.
import { createElement } from 'react';
import { act, cleanup, render } from '@testing-library/react';
import { afterEach, describe, expect, it } from 'vitest';
import type { GridColumn, GridCommand } from '@ex-grid/core';
import { ExGrid } from '../src/ex-grid';
import type { ExGridChrome } from '../src/chrome';
import type { ContractHost } from './contract-host';

interface Trade {
    readonly book: string;
    readonly notional: number;
}

const columns: readonly GridColumn<Trade>[] = [
    { name: 'Book', type: 'text', value: trade => trade.book, width: 120 },
    { name: 'Notional', type: 'number', value: trade => trade.notional, width: 100 },
];
const rows: readonly Trade[] = Array.from({ length: 20 }, (_, at) => ({ book: `Book-${at}`, notional: at * 1000 }));

/** Lets the core's pass, and the render it asks of React, run. */
async function settled(): Promise<void> {
    await act(async () => {
        for (let turn = 0; turn < 4; turn++) await Promise.resolve();
    });
}

function key(target: Element, name: string): void {
    target.dispatchEvent(new KeyboardEvent('keydown', { key: name, bubbles: true, cancelable: true }));
}

function active(): Element {
    const holder = document.activeElement;
    if (holder === null) throw new Error('Nothing holds the keyboard.');
    return holder;
}

/** The items a Wrapper drew in the menu the core named, each as its words and whether it may run. */
function drawnItems(menu: Element): { readonly element: HTMLElement; readonly label: string; readonly enabled: boolean }[] {
    return [...menu.querySelectorAll<HTMLElement>('[role=menuitem]')].map(element => ({
        element,
        label: element.textContent.trim(),
        enabled: !element.matches(':disabled') && element.getAttribute('aria-disabled') !== 'true',
    }));
}

function menuOf(root: Element, selector: string): Element {
    const menu = root.querySelector(selector);
    if (menu === null) throw new Error(`No menu stands at ${selector}.`);
    return menu;
}

/** An ExGrid under `chrome`, with a disabled command of the Consumer's and an enabled one, worded by the Consumer. */
function mounted(chrome: ExGridChrome<unknown>, host: ContractHost): { readonly root: HTMLElement; readonly ran: string[] } {
    const ran: string[] = [];
    const contextCommands = (): readonly GridCommand[] => [
        { id: 'audit', enabled: false, invoke: () => { ran.push('audit'); } },
        { id: 'open-trade', enabled: true, invoke: () => { ran.push(`open-trade, the keyboard on ${active().className}`); } },
    ];
    const { container } = render(createElement(ExGrid<Trade>, {
        columns, rows, rowHeight: 24, viewportHeight: 320, viewportWidth: 720, contextCommands, chrome,
        commandLabel: id => (id === 'open-trade' ? 'Open trade' : undefined),
    }), host);
    const root = container.querySelector('[role=grid]'); // a provider may draw beside it
    if (!(root instanceof HTMLElement)) throw new Error('The grid drew no root.');
    return { root, ran };
}

/** Opens the Context Menu on the second row, by a secondary click, and answers the menu. */
async function openContextMenu(root: HTMLElement): Promise<Element> {
    root.focus();
    for (const type of ['mousedown', 'contextmenu']) {
        const event = new MouseEvent(type, { bubbles: true, cancelable: true, button: 2 });
        Object.defineProperty(event, 'offsetX', { value: 10 });
        Object.defineProperty(event, 'offsetY', { value: 29 });
        root.querySelector('.ex-viewport')?.dispatchEvent(event);
    }
    await settled();
    return menuOf(root, ':scope > .ex-popover[role=menu]');
}

/**
 * Registers the contract's tests against a Wrapper's Chrome: `chrome` names it, and must fill the column menu's
 * and the Context Menu's slots.
 */
export function menuSlotContract(name: string, chrome: ExGridChrome<unknown>, host: ContractHost = {}): void {
    describe(`${name}: the menu slots' contract (ADR-0390 section 7)`, () => {
        afterEach(() => { cleanup(); });

        it('ADR-0036: the Context Menu draws the core\'s commands in the core\'s order, each enabled as the core says, in the core\'s words', async () => {
            const { root } = mounted(chrome, host);
            await settled();
            const items = drawnItems(await openContextMenu(root));
            expect(items.map(item => [item.label, item.enabled])).toEqual([
                ['Copy', true], ['Copy with headers', true], ['audit', false], ['Open trade', true],
            ]);
            expect(active()).toBe(items[0]?.element);
        });

        it('ADR-0039: a key is the core\'s table; Enter runs the item with the menu closed and the keyboard back on the grid', async () => {
            const { root, ran } = mounted(chrome, host);
            await settled();
            await openContextMenu(root);
            const moved: string[] = [];
            for (const pressed of ['ArrowDown', 'ArrowDown', 'ArrowDown', 'Home', 'End']) {
                key(active(), pressed);
                await settled();
                moved.push(active().textContent.trim());
            }
            expect(moved).toEqual(['Copy with headers', 'Open trade', 'Copy', 'Copy', 'Open trade']);
            key(active(), 'Enter');
            await settled();
            expect([root.querySelector('.ex-popover'), ran, active()]).toEqual([null, ['open-trade, the keyboard on ex-grid'], root]);
        });

        it('ADR-0010: a click on a disabled item runs nothing, and a click on an enabled one runs it', async () => {
            const { root, ran } = mounted(chrome, host);
            await settled();
            const items = drawnItems(await openContextMenu(root));
            items[2]?.element.click();
            expect(ran).toEqual([]);
            items[3]?.element.click();
            await settled();
            expect([root.querySelector('.ex-popover'), ran]).toEqual([null, ['open-trade, the keyboard on ex-grid']]);
        });

        it('ADR-0044: the column menu draws its column\'s commands, and Excel\'s letters run through the core', async () => {
            const { root } = mounted(chrome, host);
            await settled();
            root.focus();
            const button = root.querySelectorAll<HTMLElement>('.ex-header-cell > .ex-menu-button')[1];
            for (const type of ['mousedown', 'mouseup', 'click']) button?.dispatchEvent(new MouseEvent(type, { bubbles: true, cancelable: true, button: 0 }));
            await settled();
            const items = drawnItems(menuOf(root, ':scope > .ex-popover > .ex-popover-commands[role=menu]'));
            expect(items.map(item => item.enabled)).toEqual([true, true, false, false, false, false, false]);
            expect(active()).toBe(items[0]?.element);
            key(active(), 'o');
            await settled();
            expect([root.querySelector('.ex-popover'), active(), root.querySelectorAll('.ex-header-cell')[1]?.getAttribute('aria-sort')])
                .toEqual([null, root, 'descending']);
        });
    });
}
