// @vitest-environment happy-dom
// The adapter's Chrome slots (ADR-0390 section 3): a render function per slot, drawn through a portal into the slot
// the core made, and useMenuItems carrying the items' keys, clicks and focus to the core. The contract every
// Wrapper keeps runs here against a Chrome of plain buttons; what the adapter itself owes is tested after it.
import { createElement, StrictMode, useState, type ReactElement } from 'react';
import { act, cleanup, render } from '@testing-library/react';
import { createPortal } from 'react-dom';
import { afterEach, describe, expect, it } from 'vitest';
import type { GridColumn, GridMenuSlotContext } from '@ex-grid/core';
import { ExGrid } from '../src/ex-grid';
import { useMenuItems, type ExGridChrome } from '../src/chrome';
import { menuSlotContract } from './menu-slot-contract';

/** A menu of plain buttons, as a Wrapper draws its design system's: it counts its mounts, to show it keeps state. */
function PlainMenu({ context }: { readonly context: GridMenuSlotContext }): ReactElement {
    const itemProps = useMenuItems(context);
    const [mount] = useState(() => { plainMounts += 1; return plainMounts; });
    return createElement('div', { role: 'none', 'data-mount': mount },
        ...context.commands.map((command, index) => createElement('button', {
            ...itemProps(index), key: index, type: 'button', className: 'plain-item',
        }, context.label(command.id))));
}
let plainMounts = 0;

const plainMenu = (context: GridMenuSlotContext): ReactElement => createElement(PlainMenu, { context });
const plainChrome: ExGridChrome<unknown> = { columnMenu: plainMenu, contextMenu: plainMenu };

menuSlotContract('a Chrome of plain buttons', plainChrome);

interface Trade {
    readonly book: string;
}
const columns: readonly GridColumn<Trade>[] = [{ name: 'Book', type: 'text', value: trade => trade.book, width: 120 }];
const rows: readonly Trade[] = [{ book: 'Rates' }, { book: 'Credit' }];

async function settled(): Promise<void> {
    await act(async () => {
        for (let turn = 0; turn < 4; turn++) await Promise.resolve();
    });
}

function active(): Element {
    const holder = document.activeElement;
    if (holder === null) throw new Error('Nothing holds the keyboard.');
    return holder;
}

async function openColumnMenu(root: Element): Promise<void> {
    if (root instanceof HTMLElement) root.focus();
    const button = root.querySelector('.ex-header-cell > .ex-menu-button');
    for (const type of ['mousedown', 'mouseup', 'click']) button?.dispatchEvent(new MouseEvent(type, { bubbles: true, cancelable: true, button: 0 }));
    await settled();
}

/** Rendering an ExGrid with `chrome`, untyped, as a script may hand it. */
const withChrome = (chrome: unknown): () => void => () => { render(createElement(ExGrid<Trade>, { columns, rows, chrome: chrome as never })); };

/** A menu whose items take useMenuItems' props but not its ref: the hook cannot focus them. */
function Unreferenced({ context }: { readonly context: GridMenuSlotContext }): ReactElement {
    const itemProps = useMenuItems(context);
    return createElement('div', null, ...context.commands.map((command, index) => {
        const { ref: _, ...props } = itemProps(index);
        return createElement('button', { ...props, key: index }, command.id);
    }));
}

/** A menu whose first item holds a field of its own, drawn through a portal outside the menu, as a popup is. */
function MenuWithPopup({ context }: { readonly context: GridMenuSlotContext }): ReactElement {
    const itemProps = useMenuItems(context);
    return createElement('div', null, ...context.commands.map((command, index) => createElement('button', { ...itemProps(index), key: index },
        command.id, index === 0 ? createPortal(createElement('input', { className: 'popup-field' }), document.body) : null)));
}

afterEach(() => { cleanup(); });

describe('ExGrid\'s chrome', () => {
    it('ADR-0390 / ADR-0044: a focus request mounts the contents again on the same slot, and the component keeps its state', async () => {
        const { container } = render(createElement(StrictMode, null, createElement(ExGrid<Trade>, { columns, rows, chrome: plainChrome })));
        await settled();
        const root = container.firstElementChild;
        if (root === null) throw new Error('The grid drew no root.');
        await openColumnMenu(root);
        const menu = root.querySelector('[data-mount]');
        const mount = menu?.getAttribute('data-mount');
        const first = active();
        expect(first.className).toBe('plain-item');
        // Tab goes into the filter, which the core carries out; Tab off its end hands the keyboard back.
        first.dispatchEvent(new KeyboardEvent('keydown', { key: 'Tab', bubbles: true, cancelable: true }));
        await settled();
        expect(root.querySelector('.ex-popover-filter')?.contains(active())).toBe(true);
        root.querySelector<HTMLElement>('.ex-popover > .ex-focus-wrap:last-child')?.focus();
        await settled();
        expect([root.querySelector('[data-mount]'), root.querySelector('[data-mount]')?.getAttribute('data-mount'), active()]).toEqual([menu, mount, first]);
        // Closed, the contents go with the menu.
        active().dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true, cancelable: true }));
        await settled();
        expect([root.querySelector('.plain-item'), active()]).toEqual([null, root]);
    });

    it('ADR-0390: a slot not given is the built-in Chrome\'s', async () => {
        const { container } = render(createElement(ExGrid<Trade>, { columns, rows, chrome: { contextMenu: plainMenu } }));
        await settled();
        const root = container.firstElementChild;
        if (root === null) throw new Error('The grid drew no root.');
        await openColumnMenu(root);
        expect([root.querySelector('.plain-item'), root.querySelectorAll('.ex-popover button[role=menuitem]').length]).toEqual([null, 7]);
    });

    it('ADR-0390: a key or a click in a popup an item opened through a portal is the popup\'s, not the item\'s', async () => {
        const ran: string[] = [];
        const { container } = render(createElement(ExGrid<Trade>, {
            columns, rows, chrome: { contextMenu: context => createElement(MenuWithPopup, { context }) },
            contextCommands: () => [{ id: 'audit', enabled: true, invoke: () => { ran.push('audit'); } }],
        }));
        await settled();
        const root = container.firstElementChild;
        if (!(root instanceof HTMLElement)) throw new Error('The grid drew no root.');
        root.focus();
        root.dispatchEvent(new KeyboardEvent('keydown', { key: 'ArrowDown', bubbles: true, cancelable: true }));
        root.dispatchEvent(new KeyboardEvent('keydown', { key: 'ContextMenu', bubbles: true, cancelable: true }));
        await settled();
        const field = document.querySelector('.popup-field');
        if (field === null) throw new Error('The popup stands.');
        const typed = new KeyboardEvent('keydown', { key: 'a', bubbles: true, cancelable: true });
        field.dispatchEvent(typed);
        field.dispatchEvent(new MouseEvent('click', { bubbles: true, cancelable: true }));
        await settled();
        expect([typed.defaultPrevented, ran, root.querySelector('.ex-popover') !== null]).toEqual([false, [], true]);
    });

    it('ADR-0390: chrome that is not an object of render functions is refused by name', () => {
        expect(withChrome({ filterMenu: () => null })).toThrow("chrome has no slot 'filterMenu'");
        expect(withChrome({ contextMenu: 'menu' })).toThrow('chrome.contextMenu is a render function');
    });

    it('ADR-0390: an item drawn without useMenuItems\' props is named when the core asks it for the keyboard', async () => {
        const { container } = render(createElement(ExGrid<Trade>, {
            columns, rows, chrome: { columnMenu: context => createElement(Unreferenced, { context }) },
        }));
        await settled();
        const root = container.firstElementChild;
        if (root === null) throw new Error('The grid drew no root.');
        await expect(openColumnMenu(root)).rejects.toThrow("The menu's item 0 is not drawn");
    });
});
