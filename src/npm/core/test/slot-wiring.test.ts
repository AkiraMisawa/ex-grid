// @vitest-environment happy-dom
// Layer 2 (ADR-0410 sections 2 and 5): a menu slot's contents drawn later than their slot, as a Blazor adapter's
// arrive a round trip after the core mounts it, and wired from the attributes they write by the DOM functions both
// adapters call. The keys typed meanwhile are the menu's, answered against the place the core keeps, and the
// contents take the keyboard where that place then stands.
import { afterEach, describe, expect, it } from 'vitest';
import { defined } from '../src/must';
import type { GridChromeSlot, GridColumnMenuSlotContext } from '../src/dom/grid-chrome';
import { menuItemAttribute, wireMenuItems } from '../src/dom/slot-wiring';
import { columns, mount, painted, q, trades, unmountAll, type Trade } from './grid-harness';

afterEach(unmountAll);

const base = { columns, rows: trades(100), rowHeight: 24, viewportHeight: 320, viewportWidth: 720 } as const;

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

/** A column menu whose contents arrive when the test draws them, as a Blazor adapter's do: each mount is held, and
 * nothing is drawn in the slot until {@link Late.draw}. */
interface Late {
    readonly slots: HTMLElement[];
    readonly contexts: GridColumnMenuSlotContext[];
    cleanups: number;
    /** Draws the latest mount's contents, an item per command marked by its index, and wires them. */
    draw(): HTMLButtonElement[];
}

function lateMenu(): { readonly late: Late; readonly slot: GridChromeSlot<GridColumnMenuSlotContext> } {
    const late: Late = {
        slots: [],
        contexts: [],
        cleanups: 0,
        draw: () => {
            const slot = defined(late.slots.at(-1), 'a mounted slot');
            const context = defined(late.contexts.at(-1), 'its context');
            const items = context.commands.map((command, index) => {
                const item = document.createElement('button');
                item.type = 'button';
                item.textContent = context.label(command.id);
                item.setAttribute(menuItemAttribute, String(index));
                return item;
            });
            slot.append(...items);
            wireMenuItems(slot, context);
            return items;
        },
    };
    const slot: GridChromeSlot<GridColumnMenuSlotContext> = (element, context) => {
        late.slots.push(element);
        late.contexts.push(context);
        return () => {
            late.cleanups += 1;
            element.replaceChildren();
        };
    };
    return { late, slot };
}

function lateGrid(): { readonly root: HTMLElement; readonly late: Late } {
    const { late, slot } = lateMenu();
    const { root } = mount<Trade>({ ...base, chrome: { columnMenu: slot } });
    return { root, late };
}

describe('a menu slot\'s contents wired from their attributes', () => {
    it('ADR-0410 section 2: each marked control takes the menu\'s role and the core\'s state, and the first enabled one the keyboard', async () => {
        const { root, late } = lateGrid();
        await painted();
        openColumnMenu(root, 1);
        const items = late.draw();
        expect(items.map(item => [item.getAttribute('role'), item.tabIndex, item.getAttribute('aria-disabled'), item.getAttribute('data-ex-claims')]))
            .toEqual([
                ['menuitem', -1, 'false', 'click keydown'], ['menuitem', -1, 'false', 'click keydown'], ['menuitem', -1, 'true', null],
                ['menuitem', -1, 'true', null], ['menuitem', -1, 'true', null], ['menuitem', -1, 'true', null], ['menuitem', -1, 'true', null],
            ]);
        expect(active()).toBe(items[0]);
    });

    it('ADR-0039: a key on an item is the core\'s table against its place, and a press runs the item\'s command', async () => {
        const { root, late } = lateGrid();
        await painted();
        openColumnMenu(root, 1);
        const items = late.draw();
        expect(key(active(), 'ArrowDown').defaultPrevented).toBe(true);
        expect(active()).toBe(items[1]);
        defined(items[1], 'Sort descending').click();
        await painted();
        expect([root.querySelector('.ex-popover'), q(root, '.ex-header-cell:nth-child(2)').getAttribute('aria-sort')]).toEqual([null, 'descending']);
    });

    it('ADR-0410 section 2: a control marked with no command\'s index is refused by name', async () => {
        const { root, late } = lateGrid();
        await painted();
        openColumnMenu(root, 1);
        const slot = defined(late.slots[0], 'the slot');
        const stray = document.createElement('button');
        stray.setAttribute(menuItemAttribute, '9');
        slot.append(stray);
        expect(() => wireMenuItems(slot, defined(late.contexts[0], 'the context'))).toThrow('marked data-ex-menu-item="9"');
    });
});

describe('keys typed before a menu slot\'s contents arrive', () => {
    it('ADR-0410 section 5: End and Enter on the grid run the last enabled command, as on the items', async () => {
        const { root, late } = lateGrid();
        await painted();
        openColumnMenu(root, 1);
        expect(key(root, 'End').defaultPrevented).toBe(true);
        key(root, 'Enter');
        await painted();
        expect([root.querySelector('.ex-popover'), q(root, '.ex-header-cell:nth-child(2)').getAttribute('aria-sort')]).toEqual([null, 'descending']);
        expect(late.cleanups).toBe(late.slots.length);
    });

    it('ADR-0410 section 5: a move asks the contents again, and they take the keyboard on the item the place stands on', async () => {
        const { root, late } = lateGrid();
        await painted();
        openColumnMenu(root, 1);
        key(root, 'ArrowDown');
        const asked = defined(late.contexts.at(-1), 'the context asked again');
        expect([late.contexts.length, late.slots[1], asked.focusRequest, asked.focusItem]).toEqual([2, late.slots[0], 2, 1]);
        const items = late.draw();
        expect(active()).toBe(items[1]);
        // The contents hold the keyboard now: a key on the grid is the grid's again, and the item's keys the menu's.
        key(active(), 'ArrowUp');
        expect(active()).toBe(items[0]);
    });

    it('ADR-0410 section 5 / ADR-0044: E typed before the contents arrive puts the keyboard in the filter\'s search, and the contents leave it there', async () => {
        const { root, late } = lateGrid();
        await painted();
        openColumnMenu(root, 1);
        key(root, 'e');
        const search = active();
        expect(q(root, '.ex-popover .ex-popover-filter').contains(search)).toBe(true);
        late.draw();
        expect(active()).toBe(search);
    });
});
