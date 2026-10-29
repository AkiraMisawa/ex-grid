// @vitest-environment happy-dom
// Layer 2 (ADR-0390 section 7): the Chrome slots of the column menu and the Context Menu, as the core holds
// them. A slot is handed the core's commands in the core's order, each enabled as the core says, and what a key
// means is the core's table against the place the core keeps; running, closing and where the keyboard goes are
// the core's. The contents here are a plain Chrome written against the context alone, as a Wrapper is.
import { afterEach, describe, expect, it } from 'vitest';
import { defined } from '../src/must';
import type { GridChromeSlot, GridColumnMenuSlotContext, GridContextMenuSlotContext, GridMenuSlotContext } from '../src/dom/grid-chrome';
import type { GridCommand } from '../src/dom/grid-command';
import type { GridOptions } from '../src/dom/grid-view';
import { columns, mount, painted, q, trades, unmountAll, type Trade } from './grid-harness';

afterEach(unmountAll);

const base = { columns, rows: trades(100), rowHeight: 24, viewportHeight: 320, viewportWidth: 720 } as const;
const rowY = (row: number): number => (row * 24) + 5;

function pointer(root: HTMLElement, type: string, x: number, y: number, init: MouseEventInit = {}): void {
    const event = new MouseEvent(type, { bubbles: true, cancelable: true, ...init });
    Object.defineProperty(event, 'offsetX', { value: x });
    Object.defineProperty(event, 'offsetY', { value: y });
    q(root, '.ex-viewport').dispatchEvent(event);
}
// A press puts the keyboard on the grid's root first, as the browser's own default does.
function secondary(root: HTMLElement, x: number, y: number): void {
    root.focus();
    pointer(root, 'mousedown', x, y, { button: 2 });
    pointer(root, 'contextmenu', x, y, { button: 2 });
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
function openColumnMenu(root: HTMLElement, column: number): void {
    const button = defined(root.querySelectorAll('.ex-header-cell > .ex-menu-button')[column], `column ${column}'s ▾`);
    root.focus();
    for (const type of ['mousedown', 'mouseup', 'click']) button.dispatchEvent(new MouseEvent(type, { bubbles: true, cancelable: true, button: 0 }));
}

/** What a plain Chrome's menu was handed and did: each context it was mounted with, and each cleanup. */
interface Drawn<Context> {
    readonly contexts: Context[];
    readonly slots: HTMLElement[];
    cleanups: number;
}

/** A menu drawn by a Chrome of its own, as a Wrapper draws one: an item per command, every key handed to the core,
 * and the core's answer carried out. It focuses the first enabled item at each focus request. */
function plainMenu<Context extends GridMenuSlotContext>(drawn: Drawn<Context>): GridChromeSlot<Context> {
    return (slot, context) => {
        drawn.contexts.push(context);
        drawn.slots.push(slot);
        const items = context.commands.map((command, index) => {
            const item = document.createElement('button');
            item.type = 'button';
            item.className = 'plain-item';
            item.setAttribute('role', 'menuitem');
            item.textContent = context.label(command.id);
            item.disabled = !command.enabled;
            item.addEventListener('keydown', event => {
                event.preventDefault();
                const meaning = context.resolveKey(event.key, event.shiftKey, event.ctrlKey || event.altKey || event.metaKey);
                if (meaning.kind === 'move') context.takeFocus(defined(items[meaning.item], 'the item moved to'));
                else if (meaning.kind === 'run') defined(context.commands[meaning.item], 'the item run').invoke();
                else if (meaning.kind === 'close') context.close();
            });
            item.addEventListener('click', () => { command.invoke(); });
            item.addEventListener('focus', () => { context.itemFocused(index); });
            return item;
        });
        slot.append(...items);
        const first = context.commands.findIndex(command => command.enabled);
        if (first >= 0) context.takeFocus(defined(items[first], 'the first enabled item'), { preventScroll: true });
        return () => {
            drawn.cleanups += 1;
            for (const item of items) item.remove();
        };
    };
}

const oneCommand = (): readonly GridCommand[] => [{ id: 'open-trade', enabled: true, invoke: () => undefined }];
const plainItems = (root: HTMLElement): HTMLButtonElement[] => [...root.querySelectorAll<HTMLButtonElement>('.ex-popover .plain-item')];
const builtInItems = (root: HTMLElement): Element[] => [...root.querySelectorAll('.ex-popover button[role=menuitem]:not(.plain-item)')];

function contextMenuGrid(options: Partial<GridOptions<Trade>> = {}): {
    readonly root: HTMLElement;
    readonly drawn: Drawn<GridContextMenuSlotContext<Trade>>;
    readonly ran: string[];
    readonly view: ReturnType<typeof mount<Trade>>['view'];
} {
    const drawn: Drawn<GridContextMenuSlotContext<Trade>> = { contexts: [], slots: [], cleanups: 0 };
    const ran: string[] = [];
    const contextCommands = (): readonly GridCommand[] => [
        { id: 'audit', enabled: false, invoke: () => { ran.push('audit'); } },
        { id: 'open-trade', enabled: true, invoke: () => { ran.push(`open-trade, the keyboard on ${active().className}`); } },
    ];
    const { root, view } = mount<Trade>({ ...base, contextCommands, chrome: { contextMenu: plainMenu(drawn) }, ...options });
    return { root, drawn, ran, view };
}

/** How many inputs a grid under `options` counts for an Enter that runs the Context Menu's first command. */
const answeredBy = async (options: Partial<GridOptions<Trade>>): Promise<number> => {
    const { root } = mount<Trade>({ ...base, ...options });
    await painted();
    secondary(root, 10, rowY(1));
    const before = Number(q(root, '[data-ex-answered]').getAttribute('data-ex-answered'));
    key(active(), 'Enter');
    await painted();
    return Number(q(root, '[data-ex-answered]').getAttribute('data-ex-answered')) - before;
};

describe('the Context Menu\'s slot', () => {
    it('ADR-0390 / ADR-0036: the slot is the menu the core places, handed the core\'s commands in order, enabled as the core says, with their words and what the menu was opened over', async () => {
        const { root, drawn } = contextMenuGrid({ commandLabel: id => (id === 'open-trade' ? 'Open trade' : undefined) });
        await painted();
        secondary(root, 150, rowY(2));
        await painted();
        const context = defined(drawn.contexts[0], 'the context');
        const slot = defined(drawn.slots[0], 'the slot');
        expect([slot.className, slot.getAttribute('role'), slot.parentElement]).toEqual(['ex-popover', 'menu', root]);
        expect(context.commands.map(command => [command.id, command.enabled])).toEqual([
            ['copy', true], ['copy-with-headers', true], ['audit', false], ['open-trade', true],
        ]);
        expect(plainItems(root).map(item => item.textContent)).toEqual(['Copy', 'Copy with headers', 'audit', 'Open trade']);
        expect([context.row, context.column, context.columnType, context.selection, context.focusRequest]).toEqual([
            { book: 'Book-2', notional: 2000 }, 'Notional', 'number', [{ topRow: 2, leftColumn: 1, rowCount: 1, columnCount: 1 }], 1,
        ]);
        expect(typeof context.rowSequenceVersion).toBe('number');
        expect(Object.isFrozen(context)).toBe(true);
        // The built-in Chrome draws nothing beside the slot's contents.
        expect([builtInItems(root), active()]).toEqual([[], plainItems(root)[0]]);
    });

    it('ADR-0039: a key is the core\'s table against the core\'s place; Enter closes the menu and gives the keyboard back before the command runs, and the contents go', async () => {
        const { root, drawn, ran } = contextMenuGrid();
        await painted();
        secondary(root, 10, rowY(1));
        await painted();
        const moves: (string | null)[] = [];
        for (const name of ['ArrowDown', 'ArrowDown', 'ArrowDown', 'ArrowUp', 'Home', 'End']) {
            expect(key(active(), name).defaultPrevented).toBe(true);
            moves.push(active().textContent);
        }
        expect(moves).toEqual(['Copy with headers', 'open-trade', 'Copy', 'open-trade', 'Copy', 'open-trade']);
        key(active(), 'Enter');
        expect([root.querySelector('.ex-popover'), ran, active(), drawn.cleanups]).toEqual([null, ['open-trade, the keyboard on ex-grid'], root, 1]);
    });

    it('ADR-0039: an item focused by another way than the keys is the menu\'s place; Tab closes as a Cancel', async () => {
        const { root, ran, drawn } = contextMenuGrid();
        await painted();
        secondary(root, 10, rowY(1));
        defined(plainItems(root)[1], 'Copy with headers').focus();
        key(active(), 'ArrowDown');
        expect(active().textContent).toBe('open-trade');
        key(active(), 'Tab');
        expect([root.querySelector('.ex-popover'), ran, active(), drawn.cleanups]).toEqual([null, [], root, 1]);
    });

    it('ADR-0010: a click on a disabled command runs nothing, and a command invoked after its menu went runs nothing', async () => {
        const { root, drawn, ran } = contextMenuGrid();
        await painted();
        secondary(root, 10, rowY(1));
        const stale = defined(drawn.contexts[0], 'the context');
        defined(stale.commands[2], 'audit').invoke();
        expect([ran, root.querySelector('.ex-popover') !== null]).toEqual([[], true]);
        key(active(), 'Escape');
        defined(stale.commands[3], 'open-trade').invoke();
        stale.close();
        expect([ran, stale.resolveKey('ArrowDown', false, false).kind]).toEqual([[], 'none']);
    });

    it('ADR-0039: while the contents\' own popup is open, Escape is the popup\'s; once it closes, Escape closes the menu', async () => {
        const { root, drawn } = contextMenuGrid();
        await painted();
        secondary(root, 10, rowY(1));
        const context = defined(drawn.contexts[0], 'the context');
        context.innerPopupChanged(true);
        key(active(), 'Escape');
        expect(root.querySelector('.ex-popover')).not.toBeNull();
        context.innerPopupChanged(false);
        key(active(), 'Escape');
        expect([root.querySelector('.ex-popover'), active()]).toEqual([null, root]);
    });

    it('ADR-0204: takeFocus gives a control the keyboard only while the keyboard is still the grid\'s', async () => {
        const { root, drawn } = contextMenuGrid();
        await painted();
        secondary(root, 10, rowY(1));
        const field = document.createElement('input');
        document.body.append(field);
        field.focus();
        defined(drawn.contexts[0], 'the context').takeFocus(defined(plainItems(root)[1], 'an item'));
        expect(active()).toBe(field);
        field.remove();
    });

    it('ADR-0204 / SRV-5: the opening focus a slot gives later, as a Razor slot does a round trip on, is not taken once another grid holds the keyboard', async () => {
        const contexts: GridContextMenuSlotContext<Trade>[] = [];
        const later: GridChromeSlot<GridContextMenuSlotContext<Trade>> = (slot, context) => {
            contexts.push(context);
            const item = document.createElement('button');
            item.className = 'later-item';
            slot.append(item);
            return () => { item.remove(); };
        };
        const { root } = mount<Trade>({ ...base, contextCommands: oneCommand, chrome: { contextMenu: later } });
        const { root: other } = mount<Trade>({ ...base });
        await painted();
        secondary(root, 10, rowY(1));
        other.focus();
        const item = q(root, '.later-item');
        defined(contexts[0], 'the context').takeFocus(item);
        expect(active()).toBe(other);
        // Brought back to the grid, the keyboard is the grid's again, and the item takes it.
        root.focus();
        defined(contexts[0], 'the context').takeFocus(item);
        expect(active()).toBe(item);
    });

    it('ADR-0220: one key that runs a command is one input answered, as under the built-in Chrome', async () => {
        const slotted = await answeredBy({ chrome: { contextMenu: plainMenu<GridContextMenuSlotContext<Trade>>({ contexts: [], slots: [], cleanups: 0 }) } });
        expect(slotted).toBe(await answeredBy({}));
    });

    it('ADR-0039: a press elsewhere closes the menu and the keyboard it held comes back to the grid, though the contents went first', async () => {
        const { root } = contextMenuGrid();
        await painted();
        secondary(root, 10, rowY(1));
        expect(active().className).toBe('plain-item');
        q(root, '.ex-header-cell').dispatchEvent(new MouseEvent('mousedown', { bubbles: true, cancelable: true, button: 0 }));
        expect([root.querySelector('.ex-popover'), active()]).toEqual([null, root]);
    });

    it('ADR-0020: the contents go with the grid', async () => {
        const { root, drawn, view } = contextMenuGrid();
        await painted();
        secondary(root, 10, rowY(1));
        view.destroy();
        expect(drawn.cleanups).toBe(1);
    });
});

function columnMenuGrid(): { readonly root: HTMLElement; readonly drawn: Drawn<GridColumnMenuSlotContext> } {
    const drawn: Drawn<GridColumnMenuSlotContext> = { contexts: [], slots: [], cleanups: 0 };
    const { root } = mount<Trade>({ ...base, chrome: { columnMenu: plainMenu(drawn) } });
    return { root, drawn };
}

describe('the column menu\'s slot', () => {
    it('ADR-0390 / ADR-0044: the slot is the commands\' menu above the core\'s filter, handed the column, its commands with their letters, and the core\'s order', async () => {
        const { root, drawn } = columnMenuGrid();
        await painted();
        openColumnMenu(root, 1);
        const context = defined(drawn.contexts[0], 'the context');
        const slot = defined(drawn.slots[0], 'the slot');
        expect([slot.className, slot.getAttribute('role'), slot.getAttribute('aria-label'), slot.nextElementSibling?.className]).toEqual([
            'ex-popover-commands', 'menu', 'Notional', 'ex-focus-wrap',
        ]);
        expect([context.column, context.columnType]).toEqual(['Notional', 'number']);
        expect(context.commands.map(command => [command.id, command.enabled, context.letter(command.id)])).toEqual([
            ['sort-ascending', true, 'S'], ['sort-descending', true, 'O'], ['clear-filter', false, 'C'], ['hide', false, undefined],
            ['pin', false, undefined], ['unpin', false, undefined], ['size-to-fit', false, undefined],
        ]);
        expect(active()).toBe(plainItems(root)[0]);
    });

    it('ADR-0039: opened from the ▾ while a field of the page holds the keyboard, the menu takes it, as the built-in one does', async () => {
        const { root } = columnMenuGrid();
        await painted();
        const field = document.createElement('input');
        document.body.append(field);
        field.focus();
        const button = defined(root.querySelectorAll('.ex-header-cell > .ex-menu-button')[1], 'the ▾');
        for (const type of ['mousedown', 'mouseup', 'click']) button.dispatchEvent(new MouseEvent(type, { bubbles: true, cancelable: true, button: 0 }));
        expect(active()).toBe(plainItems(root)[0]);
        field.remove();
    });

    it('ADR-0044: Excel\'s letters run their command through the core; S sorts and closes', async () => {
        const { root } = columnMenuGrid();
        await painted();
        openColumnMenu(root, 1);
        key(active(), 'o');
        await painted();
        expect([root.querySelector('.ex-popover'), active(), q(root, '.ex-header-cell:nth-child(2)').getAttribute('aria-sort')])
            .toEqual([null, root, 'descending']);
    });

    it('ADR-0044: Tab moves into the filter, which the core carries out, and Tab off its end asks the contents for the keyboard again on the same slot', async () => {
        const { root, drawn } = columnMenuGrid();
        await painted();
        openColumnMenu(root, 1);
        const first = active();
        key(first, 'Tab');
        expect(q(root, '.ex-popover .ex-popover-filter').contains(active())).toBe(true);
        q(root, '.ex-popover > .ex-focus-wrap:last-child').focus();
        const again = defined(drawn.contexts[1], 'the context mounted again');
        expect([drawn.contexts.length, drawn.cleanups, drawn.slots[1], again.focusRequest]).toEqual([2, 1, drawn.slots[0], 2]);
        expect(active()).toBe(plainItems(root)[0]);
        // The place is back on the first item: ↓ moves to the second.
        key(active(), 'ArrowDown');
        expect(active().textContent).toBe('Sort descending');
    });
});

/** Mounting a grid with `chrome`, untyped, as a script may hand it. */
const chromeOf = (chrome: unknown): () => void => () => { mount<Trade>({ ...base, chrome: chrome as never }); };

describe('a grid\'s chrome', () => {
    it('ADR-0044: contents that cannot be drawn again for a focus request close the menu, rather than leave it empty', async () => {
        let mounts = 0;
        const failing: GridChromeSlot<GridColumnMenuSlotContext> = (slot, context) => {
            mounts += 1;
            if (mounts > 1) throw new Error('drawn once only');
            return plainMenu<GridColumnMenuSlotContext>({ contexts: [], slots: [], cleanups: 0 })(slot, context);
        };
        const { root } = mount<Trade>({ ...base, chrome: { columnMenu: failing } });
        await painted();
        openColumnMenu(root, 1);
        key(active(), 'Tab');
        expect(() => { q(root, '.ex-popover > .ex-focus-wrap:last-child').focus(); }).toThrow('drawn once only');
        expect([root.querySelector('.ex-popover'), active()]).toEqual([null, root]);
    });

    it('ADR-0390: a slot not given is the built-in Chrome\'s', async () => {
        const { root } = mount<Trade>({ ...base, chrome: {} });
        await painted();
        secondary(root, 10, rowY(1));
        expect(builtInItems(root).map(item => item.textContent)).toEqual(['Copy', 'Copy with headers']);
    });

    it('ADR-0390: chrome that is not an object of mount functions is refused by name', () => {
        expect(chromeOf('plain')).toThrow('chrome is an object of Chrome slots');
        expect(chromeOf({ filterMenu: () => () => undefined })).toThrow("chrome has no slot 'filterMenu'");
        expect(chromeOf({ columnMenu: 'menu' })).toThrow('chrome.columnMenu is a mount function');
    });

    it('ADR-0350 section 9: a mount that answers no cleanup is refused by name, and leaves no menu standing', async () => {
        const { root } = mount<Trade>({ ...base, chrome: { contextMenu: (() => undefined) as unknown as GridChromeSlot<GridContextMenuSlotContext<Trade>> } });
        await painted();
        expect(() => { secondary(root, 10, rowY(1)); }).toThrow('chrome.contextMenu answered undefined');
        expect([root.querySelector('.ex-popover'), active()]).toEqual([null, root]);
    });
});
